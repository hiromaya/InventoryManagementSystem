using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models;

namespace InventorySystem.Import.Services;

/// <summary>
/// 前月末在庫CSVインポートサービス
/// </summary>
public class PreviousMonthInventoryImportService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductMasterRepository _productMasterRepository;
    private readonly ILogger<PreviousMonthInventoryImportService> _logger;
    private readonly string _importPath;

    public PreviousMonthInventoryImportService(
        IInventoryRepository inventoryRepository,
        IProductMasterRepository productMasterRepository,
        ILogger<PreviousMonthInventoryImportService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _productMasterRepository = productMasterRepository;
        _logger = logger;
        _importPath = @"D:\InventoryImport\DeptA\Import\前月末在庫.csv";
    }

    /// <summary>
    /// 前月末在庫CSVをインポート
    /// </summary>
    public async Task<PreviousMonthImportResult> ImportAsync(DateTime targetDate)
    {
        var result = new PreviousMonthImportResult
        {
            StartTime = DateTime.Now,
            ImportType = "前月末在庫"
        };

        try
        {
            _logger.LogInformation("前月末在庫インポート開始: {TargetDate}", targetDate);

            if (!File.Exists(_importPath))
            {
                throw new FileNotFoundException($"インポートファイルが見つかりません: {_importPath}");
            }

            // CSVファイルを読み込む
            var records = await ReadCsvAsync(_importPath);
            result.TotalRecords = records.Count;

            // 有効なレコードをフィルタリング
            var validRecords = records.Where(r => r.IsValid()).ToList();
            _logger.LogInformation("有効レコード数: {Count}/{Total}", validRecords.Count, records.Count);

            // 在庫マスタの更新処理
            var processedCount = 0;
            var errorCount = 0;

            foreach (var record in validRecords)
            {
                try
                {
                    var key = record.GetNormalizedKey();
                    
                    // 商品マスタの存在チェック
                    var productExists = await _productMasterRepository.ExistsAsync(key.ProductCode);
                    if (!productExists)
                    {
                        _logger.LogWarning("商品マスタに存在しない商品コード: {ProductCode}", key.ProductCode);
                        errorCount++;
                        continue;
                    }

                    // 在庫マスタの更新または作成
                    var inventoryKey = new InventoryKey
                    {
                        ProductCode = key.ProductCode,
                        GradeCode = key.GradeCode,
                        ClassCode = key.ClassCode,
                        ShippingMarkCode = key.ShippingMarkCode,
                        ShippingMarkName = key.ShippingMarkName
                    };
                    // JobDateに関係なく既存レコードを検索
                    var inventoryMaster = await _inventoryRepository.GetByKeyAnyDateAsync(inventoryKey);

                    if (inventoryMaster != null)
                    {
                        // 既存レコードのJobDateと前月末在庫を更新
                        var oldJobDate = inventoryMaster.JobDate;
                        inventoryMaster.JobDate = targetDate;  // JobDateを更新
                        inventoryMaster.PreviousMonthQuantity = record.Quantity;
                        inventoryMaster.PreviousMonthAmount = record.Amount;
                        inventoryMaster.UpdatedDate = DateTime.Now;
                        
                        await _inventoryRepository.UpdateAsync(inventoryMaster);
                        _logger.LogDebug("在庫マスタ更新: {Key}, JobDate: {OldDate} -> {NewDate}", 
                            key, oldJobDate, targetDate);
                    }
                    else
                    {
                        // 新規レコードの作成（初回のみ）
                        inventoryMaster = new InventoryMaster
                        {
                            Key = inventoryKey,
                            PreviousMonthQuantity = record.Quantity,
                            PreviousMonthAmount = record.Amount,
                            CurrentStock = 0,
                            CurrentStockAmount = 0,
                            JobDate = targetDate,
                            CreatedDate = DateTime.Now,
                            UpdatedDate = DateTime.Now,
                            ProductName = "商品名未設定",
                            Unit = "PCS",
                            StandardPrice = 0,
                            ProductCategory1 = "",
                            ProductCategory2 = "",
                            DailyStock = 0,
                            DailyStockAmount = 0,
                            DailyFlag = '9',
                            DataSetId = "",
                            DailyGrossProfit = 0,
                            DailyAdjustmentAmount = 0,
                            DailyProcessingCost = 0,
                            FinalGrossProfit = 0
                        };
                        
                        await _inventoryRepository.CreateAsync(inventoryMaster);
                        _logger.LogDebug("在庫マスタ新規作成: {Key}", key);
                    }

                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "レコード処理エラー: {Record}", record);
                    errorCount++;
                    result.Errors.Add($"商品コード {record.ProductCode}: {ex.Message}");
                }
            }

            result.ProcessedRecords = processedCount;
            result.ErrorRecords = errorCount;
            result.EndTime = DateTime.Now;
            result.IsSuccess = errorCount == 0;
            result.Message = $"前月末在庫インポート完了: 処理 {processedCount}件, エラー {errorCount}件";

            _logger.LogInformation(result.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "前月末在庫インポートエラー");
            result.EndTime = DateTime.Now;
            result.IsSuccess = false;
            result.Message = $"インポートエラー: {ex.Message}";
            result.Errors.Add(ex.ToString());
            return result;
        }
    }

    /// <summary>
    /// CSVファイルを読み込む
    /// </summary>
    private async Task<List<PreviousMonthInventoryCsv>> ReadCsvAsync(string filePath)
    {
        var records = new List<PreviousMonthInventoryCsv>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Encoding = Encoding.UTF8,
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = context =>
            {
                _logger.LogWarning("不正なデータ: 行 {Row}, データ {Data}",
                    context.Context.Parser.Row, context.RawRecord);
            }
        };

        using var reader = new StreamReader(filePath, Encoding.UTF8);
        using var csv = new CsvReader(reader, config);

        await foreach (var record in csv.GetRecordsAsync<PreviousMonthInventoryCsv>())
        {
            records.Add(record);
        }

        _logger.LogInformation("CSVファイル読み込み完了: {Count}件", records.Count);
        return records;
    }
}

/// <summary>
/// 前月末在庫インポート結果
/// </summary>
public class PreviousMonthImportResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string ImportType { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int ErrorRecords { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();

    public TimeSpan Duration => EndTime - StartTime;
}

