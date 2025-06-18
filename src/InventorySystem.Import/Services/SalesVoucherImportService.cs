using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Import.Models;
using InventorySystem.Data.Repositories;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace InventorySystem.Import.Services;

/// <summary>
/// 売上伝票CSV取込サービス
/// </summary>
public class SalesVoucherImportService
{
    private readonly SalesVoucherCsvRepository _salesVoucherRepository;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly ILogger<SalesVoucherImportService> _logger;
    
    public SalesVoucherImportService(
        SalesVoucherCsvRepository salesVoucherRepository,
        IDataSetRepository dataSetRepository,
        ILogger<SalesVoucherImportService> logger)
    {
        _salesVoucherRepository = salesVoucherRepository;
        _dataSetRepository = dataSetRepository;
        _logger = logger;
    }

    /// <summary>
    /// CSVファイルから売上伝票データを取込む
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

        _logger.LogInformation("売上伝票CSV取込開始: {FilePath}, DataSetId: {DataSetId}", 
            filePath, dataSetId);

        try
        {
            // データセット作成
            var dataSet = new DataSet
            {
                Id = dataSetId,
                DataSetType = DataSetTypes.Sales,
                ImportedAt = DateTime.Now,
                RecordCount = 0,
                Status = DataSetStatus.Processing,
                FilePath = filePath,
                JobDate = jobDate
            };
            
            await _dataSetRepository.CreateAsync(dataSet);

            // CSV読み込み処理
            var salesVouchers = new List<SalesVoucher>();
            
            using var reader = new StringReader(File.ReadAllText(filePath, Encoding.UTF8));
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,  // ヘッダーなし
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim,
                Encoding = Encoding.UTF8
            });

            var records = csv.GetRecords<SalesVoucherCsv>().ToList();
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (!record.IsValidSalesVoucher())
                    {
                        var error = $"行{index}: 不正な売上伝票データ - 伝票番号: {record.VoucherNumber}";
                        errorMessages.Add(error);
                        _logger.LogWarning(error);
                        continue;
                    }

                    var salesVoucher = record.ToEntity(dataSetId);
                    salesVouchers.Add(salesVoucher);
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
            if (salesVouchers.Any())
            {
                const int batchSize = 1000;
                for (int i = 0; i < salesVouchers.Count; i += batchSize)
                {
                    var batch = salesVouchers.Skip(i).Take(batchSize);
                    await _salesVoucherRepository.BulkInsertAsync(batch);
                    
                    _logger.LogInformation("バッチ保存完了: {Start}-{End}件目", 
                        i + 1, Math.Min(i + batchSize, salesVouchers.Count));
                }
            }

            // データセットステータス更新
            await _dataSetRepository.UpdateRecordCountAsync(dataSetId, importedCount);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.PartialSuccess, errorMessage);
                _logger.LogWarning("売上伝票CSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    importedCount, errorMessages.Count);
            }
            else
            {
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Completed);
                _logger.LogInformation("売上伝票CSV取込完了: {Count}件", importedCount);
            }

            return dataSetId;
        }
        catch (Exception ex)
        {
            await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Failed, ex.Message);
            _logger.LogError(ex, "売上伝票CSV取込エラー: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// データセットIDを生成
    /// </summary>
    private static string GenerateDataSetId()
    {
        return $"SALES_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
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

        var importedData = await _salesVoucherRepository.GetByDataSetIdAsync(dataSetId);
        
        return new ImportResult
        {
            DataSetId = dataSetId,
            Status = dataSet.Status,
            ImportedCount = dataSet.RecordCount,
            ErrorMessage = dataSet.ErrorMessage,
            FilePath = dataSet.FilePath,
            ImportedAt = dataSet.ImportedAt,
            ImportedData = importedData.Cast<object>().ToList()
        };
    }
}

/// <summary>
/// 取込結果クラス
/// </summary>
public class ImportResult
{
    public string DataSetId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ImportedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FilePath { get; set; }
    public DateTime ImportedAt { get; set; }
    public List<object> ImportedData { get; set; } = new();
}