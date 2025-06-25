using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Import.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace InventorySystem.Import.Services;

/// <summary>
/// 在庫調整CSV取込サービス
/// </summary>
public class InventoryAdjustmentImportService
{
    private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly ILogger<InventoryAdjustmentImportService> _logger;
    
    public InventoryAdjustmentImportService(
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        IDataSetRepository dataSetRepository,
        ILogger<InventoryAdjustmentImportService> logger)
    {
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _dataSetRepository = dataSetRepository;
        _logger = logger;
    }

    /// <summary>
    /// CSVファイルから在庫調整データを取込む
    /// </summary>
    /// <param name="filePath">取込対象CSVファイルパス</param>
    /// <param name="jobDate">ジョブ日付</param>
    /// <returns>データセットID</returns>
    public async Task<string> ImportAsync(string filePath, DateTime jobDate)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSVファイルが見つかりません: {filePath}");
        }

        var dataSetId = GenerateDataSetId();
        var importedCount = 0;
        var errorMessages = new List<string>();

        _logger.LogInformation("在庫調整CSV取込開始: {FilePath}, DataSetId: {DataSetId}", 
            filePath, dataSetId);

        try
        {
            // データセット作成
            var dataSet = new DataSet
            {
                Id = dataSetId,
                ProcessType = "Adjustment",
                Name = $"在庫調整取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                Description = $"在庫調整CSVファイル取込: {Path.GetFileName(filePath)}",
                CreatedAt = DateTime.Now,
                RecordCount = 0,
                Status = DataSetStatus.Processing,
                FilePath = filePath,
                JobDate = jobDate,
                UpdatedAt = DateTime.Now
            };
            
            await _dataSetRepository.CreateAsync(dataSet);

            // CSV読み込み処理（販売大臣フォーマット対応）
            var adjustments = new List<InventoryAdjustment>();
            var records = await ReadDaijinCsvFileAsync(filePath);
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (!record.IsValidInventoryAdjustment())
                    {
                        var error = $"行{index}: 不正な在庫調整データ - 伝票番号: {record.VoucherNumber}";
                        errorMessages.Add(error);
                        _logger.LogWarning(error);
                        continue;
                    }

                    var adjustment = record.ToEntity(dataSetId);
                    // VoucherIdとLineNumberを設定
                    adjustment.VoucherId = $"{dataSetId}_{adjustment.VoucherNumber}";
                    adjustment.LineNumber = index; // 行番号を使用
                    adjustments.Add(adjustment);
                    importedCount++;
                }
                catch (Exception ex)
                {
                    var error = $"行{index}: CSV変換エラー - {ex.Message}";
                    errorMessages.Add(error);
                    _logger.LogError(ex, error);
                }
            }

            // バッチ処理でデータベースに保存
            if (adjustments.Any())
            {
                const int batchSize = 1000;
                for (int i = 0; i < adjustments.Count; i += batchSize)
                {
                    var batch = adjustments.Skip(i).Take(batchSize);
                    await _inventoryAdjustmentRepository.BulkInsertAsync(batch);
                    
                    _logger.LogInformation("バッチ保存完了: {Start}-{End}件目", 
                        i + 1, Math.Min(i + batchSize, adjustments.Count));
                }
            }

            // データセットステータス更新
            await _dataSetRepository.UpdateRecordCountAsync(dataSetId, importedCount);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.PartialSuccess, errorMessage);
                _logger.LogWarning("在庫調整CSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    importedCount, errorMessages.Count);
            }
            else
            {
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Completed);
                _logger.LogInformation("在庫調整CSV取込完了: {Count}件", importedCount);
            }

            return dataSetId;
        }
        catch (Exception ex)
        {
            await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Failed, ex.Message);
            _logger.LogError(ex, "在庫調整CSV取込エラー: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// 販売大臣CSVファイルを読み込む（171列フォーマット対応）
    /// </summary>
    private async Task<List<InventoryAdjustmentDaijinCsv>> ReadDaijinCsvFileAsync(string filePath)
    {
        // UTF-8エンコーディングで直接読み込む
        _logger.LogInformation("UTF-8エンコーディングでCSVファイルを読み込みます: {FilePath}", filePath);
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,  // ヘッダー検証を無効化
            MissingFieldFound = null, // 不足フィールドのエラーを無効化
            BadDataFound = context => 
            {
                _logger.LogWarning($"不正なデータ: 行 {context.Context?.Parser?.Row ?? 0}, フィールド {context.Field ?? "不明"}");
            },
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        });

        // ヘッダーを読み込む
        await csv.ReadAsync();
        csv.ReadHeader();
        
        var records = new List<InventoryAdjustmentDaijinCsv>();
        var rowNumber = 1;
        
        while (await csv.ReadAsync())
        {
            rowNumber++;
            try
            {
                var record = csv.GetRecord<InventoryAdjustmentDaijinCsv>();
                if (record != null)
                {
                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"行 {rowNumber} の読み込みでエラー: {ex.Message}");
                continue; // エラーがあっても処理を継続
            }
        }
        
        return records;
    }

    /// <summary>
    /// データセットIDを生成
    /// </summary>
    private static string GenerateDataSetId()
    {
        // GUIDの最初の8文字のみ使用
        var guid = Guid.NewGuid().ToString("N");
        return $"ADJUST_{DateTime.Now:yyyyMMdd_HHmmss}_{guid.Substring(0, 8)}";
    }

    /// <summary>
    /// 取込結果を取得
    /// </summary>
    public async Task<ImportResult> GetImportResultAsync(string dataSetId)
    {
        var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);
        if (dataSet == null)
        {
            throw new InvalidOperationException($"データセットが見つかりません: {dataSetId}");
        }

        var importedData = await _inventoryAdjustmentRepository.GetByDataSetIdAsync(dataSetId);
        
        return new ImportResult
        {
            DataSetId = dataSetId,
            Status = dataSet.Status,
            ImportedCount = dataSet.RecordCount,
            ErrorMessage = dataSet.ErrorMessage,
            FilePath = dataSet.FilePath,
            CreatedAt = dataSet.CreatedAt,
            ImportedData = importedData.Cast<object>().ToList()
        };
    }

}