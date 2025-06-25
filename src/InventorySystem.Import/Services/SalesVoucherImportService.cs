using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Import.Models;
using InventorySystem.Data.Repositories;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using InventorySystem.Core.Configuration;
using InventorySystem.Core.Services;
using Microsoft.Extensions.Options;

namespace InventorySystem.Import.Services;

/// <summary>
/// 売上伝票CSV取込サービス
/// </summary>
public class SalesVoucherImportService
{
    private readonly SalesVoucherCsvRepository _salesVoucherRepository;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly ILogger<SalesVoucherImportService> _logger;
    private readonly DepartmentSettings _departmentSettings;
    private readonly ICsvFileProcessor _csvProcessor;
    
    public SalesVoucherImportService(
        SalesVoucherCsvRepository salesVoucherRepository,
        IDataSetRepository dataSetRepository,
        ILogger<SalesVoucherImportService> logger,
        IOptions<DepartmentSettings> departmentOptions,
        ICsvFileProcessor csvProcessor)
    {
        _salesVoucherRepository = salesVoucherRepository;
        _dataSetRepository = dataSetRepository;
        _logger = logger;
        _departmentSettings = departmentOptions.Value;
        _csvProcessor = csvProcessor;
    }

    /// <summary>
    /// CSVファイルから売上伝票データを取込む
    /// </summary>
    /// <param name="filePath">取込対象CSVファイルパス</param>
    /// <param name="jobDate">ジョブ日付</param>
    /// <param name="departmentCode">部門コード（省略時はデフォルト部門）</param>
    /// <returns>データセットID</returns>
    public async Task<string> ImportAsync(string filePath, DateTime jobDate, string? departmentCode = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSVファイルが見つかりません: {filePath}");
        }

        // 部門コードの設定（省略時はデフォルト部門を使用）
        departmentCode ??= _departmentSettings.DefaultDepartment;
        var department = _departmentSettings.GetDepartment(departmentCode);
        
        var dataSetId = GenerateDataSetId();
        var importedCount = 0;
        var errorMessages = new List<string>();

        _logger.LogInformation("売上伝票CSV取込開始: {FilePath}, DataSetId: {DataSetId}, Department: {DepartmentCode}", 
            filePath, dataSetId, departmentCode);

        try
        {
            // データセット作成
            var dataSet = new DataSet
            {
                Id = dataSetId,
                ProcessType = "Sales",
                Name = $"売上伝票取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                Description = $"売上伝票CSVファイル取込: {Path.GetFileName(filePath)}",
                ImportedAt = DateTime.Now,
                RecordCount = 0,
                Status = DataSetStatus.Processing,
                FilePath = filePath,
                JobDate = jobDate,
                DepartmentCode = departmentCode,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            
            await _dataSetRepository.CreateAsync(dataSet);

            // CSV読み込み処理（販売大臣フォーマット対応）
            var salesVouchers = new List<SalesVoucher>();
            var records = await ReadDaijinCsvFileAsync(filePath);
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
                    salesVoucher.DepartmentCode = departmentCode;
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

            // CSV処理成功時、ファイルをProcessedフォルダへ移動
            await _csvProcessor.MoveToProcessedAsync(filePath, departmentCode);

            return dataSetId;
        }
        catch (Exception ex)
        {
            await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Failed, ex.Message);
            _logger.LogError(ex, "売上伝票CSV取込エラー: {FilePath}", filePath);
            
            // エラー時、ファイルをErrorフォルダへ移動
            try
            {
                await _csvProcessor.MoveToErrorAsync(filePath, departmentCode, ex);
            }
            catch (Exception moveEx)
            {
                _logger.LogError(moveEx, "エラーファイルの移動に失敗しました: {FilePath}", filePath);
            }
            
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
    /// 販売大臣CSVファイルを読み込む（171列フォーマット対応）
    /// </summary>
    private async Task<List<SalesVoucherDaijinCsv>> ReadDaijinCsvFileAsync(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.GetEncoding("UTF-8"));
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
        
        var records = new List<SalesVoucherDaijinCsv>();
        var rowNumber = 1;
        
        while (await csv.ReadAsync())
        {
            rowNumber++;
            try
            {
                var record = csv.GetRecord<SalesVoucherDaijinCsv>();
                if (record != null)
                {
                    // 最初の10件は詳細ログ出力
                    if (rowNumber <= 11)
                    {
                        _logger.LogInformation("CSV行{Line}: 伝票番号='{VoucherNumber}', 得意先コード='{CustomerCode}', 得意先名='{CustomerName}', 商品コード='{ProductCode}', 商品名='{ProductName}'",
                            rowNumber, record.VoucherNumber, record.CustomerCode, record.CustomerName, record.ProductCode, record.ProductName);
                    }
                    
                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"行 {rowNumber} の読み込みでエラー: {ex.Message}");
                continue; // エラーがあっても処理を継続
            }
        }
        
        _logger.LogInformation("CSV読み込み完了: {Count}件", records.Count);
        return records;
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