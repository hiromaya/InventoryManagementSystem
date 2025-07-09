using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Import.Models;

namespace InventorySystem.Import.Services;

/// <summary>
/// 前月末在庫CSVインポートサービス
/// </summary>
public class PreviousMonthInventoryImportService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ILogger<PreviousMonthInventoryImportService> _logger;
    private readonly string _importPath;

    public PreviousMonthInventoryImportService(
        IInventoryRepository inventoryRepository,
        ILogger<PreviousMonthInventoryImportService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _logger = logger;
        _importPath = @"D:\InventoryImport\DeptA\Import\前月末在庫.csv";
    }

    /// <summary>
    /// 前月末在庫CSVをインポート（後方互換性のための既存メソッド）
    /// </summary>
    public async Task<PreviousMonthImportResult> ImportAsync(DateTime targetDate)
    {
        // 既存の動作を維持
        return await ImportAsync(targetDate, null, false);
    }

    /// <summary>
    /// 初期在庫設定用のインポート（日付フィルタなし、すべてのデータを初期在庫として設定）
    /// </summary>
    public async Task<PreviousMonthImportResult> ImportForInitialInventoryAsync()
    {
        var result = new PreviousMonthImportResult
        {
            StartTime = DateTime.Now,
            ImportType = "初期在庫設定"
        };

        try
        {
            _logger.LogInformation("=== 初期在庫設定開始 ===");
            _logger.LogInformation("CSVファイルパス: {Path}", _importPath);
            _logger.LogInformation("日付フィルタ: 無効（すべてのデータを処理）");

            // ファイル存在確認
            if (!File.Exists(_importPath))
            {
                _logger.LogError("CSVファイルが見つかりません: {Path}", _importPath);
                throw new FileNotFoundException($"インポートファイルが見つかりません: {_importPath}");
            }

            // 1. CSV読み込み
            var records = await ReadCsvAsync(_importPath);
            result.TotalRecords = records.Count;
            _logger.LogInformation("=== ステップ1: CSV読み込み完了 ===");
            _logger.LogInformation("読み込みレコード数: {Count}", records.Count);

            // 2. 有効レコードフィルタリング
            var validRecords = records.Where(r => r.IsValid()).ToList();
            var invalidRecords = records.Where(r => !r.IsValid()).ToList();
            
            _logger.LogInformation("=== ステップ2: バリデーション完了 ===");
            _logger.LogInformation("有効レコード: {Valid}件", validRecords.Count);
            _logger.LogInformation("無効レコード: {Invalid}件", invalidRecords.Count);
            
            // 無効レコードの詳細（最初の10件）
            foreach (var invalid in invalidRecords.Take(10))
            {
                _logger.LogWarning("無効レコード詳細: 商品={Product}, 等級={Grade}, 階級={Class}, 荷印={Mark}, 理由={Reason}",
                    invalid.ProductCode ?? "(null)", 
                    invalid.GradeCode ?? "(null)", 
                    invalid.ClassCode ?? "(null)", 
                    invalid.ShippingMarkCode ?? "(null)",
                    invalid.GetValidationError());
            }

            // 3. 初期在庫設定用処理（日付フィルタなし）
            var processedCount = 0;
            var errorCount = 0;

            _logger.LogInformation("=== ステップ3: 初期在庫設定処理開始 ===");
            _logger.LogInformation("JobDate設定: CSVの48列目ジョブデート項目を使用（日付フィルタは無効）");

            foreach (var record in validRecords)
            {
                try
                {
                    var key = record.GetNormalizedKey();
                    
                    // CSVのジョブデート項目（48列目）を解析してJobDateとして設定
                    var jobDate = ParseJobDate(record.JobDate);
                    
                    _logger.LogDebug("処理中レコード: 商品={Product}, 等級={Grade}, 階級={Class}, 荷印={Mark}, 荷印名={MarkName}, JobDate={JobDate:yyyy-MM-dd}",
                        key.ProductCode, key.GradeCode, key.ClassCode, key.ShippingMarkCode, key.ShippingMarkName, jobDate);

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
                    
                    _logger.LogDebug("在庫マスタ検索結果: {Result}",
                        inventoryMaster != null ? "既存レコード更新" : "新規レコード作成");

                    if (inventoryMaster != null)
                    {
                        // 既存レコードの初期在庫を設定（CSVのJobDateを使用）
                        inventoryMaster.JobDate = jobDate;
                        inventoryMaster.PreviousMonthQuantity = record.Quantity;
                        inventoryMaster.PreviousMonthAmount = record.Amount;
                        inventoryMaster.UpdatedDate = DateTime.Now;
                        
                        await _inventoryRepository.UpdateAsync(inventoryMaster);
                        _logger.LogDebug("在庫マスタ更新: {Key}, JobDate={JobDate:yyyy-MM-dd}, 初期在庫数量={Qty}, 初期在庫金額={Amt}", 
                            key, jobDate, record.Quantity, record.Amount);
                    }
                    else
                    {
                        // 新規レコードの作成（CSVのJobDateを使用）
                        inventoryMaster = new InventoryMaster
                        {
                            Key = inventoryKey,
                            PreviousMonthQuantity = record.Quantity,
                            PreviousMonthAmount = record.Amount,
                            CurrentStock = 0,
                            CurrentStockAmount = 0,
                            JobDate = jobDate,
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
                        _logger.LogDebug("在庫マスタ新規作成: {Key}, JobDate={JobDate:yyyy-MM-dd}", key, jobDate);
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
            
            _logger.LogInformation("=== ステップ4: 処理完了 ===");
            _logger.LogInformation("処理済み: {Processed}件", processedCount);
            _logger.LogInformation("エラー: {Error}件", errorCount);

            result.ProcessedRecords = processedCount;
            result.ErrorRecords = errorCount;
            result.EndTime = DateTime.Now;
            result.IsSuccess = errorCount == 0;
            result.Message = $"初期在庫設定完了: 処理 {processedCount}件, エラー {errorCount}件";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初期在庫設定エラー");
            result.EndTime = DateTime.Now;
            result.IsSuccess = false;
            result.Message = $"初期在庫設定エラー: {ex.Message}";
            result.Errors.Add(ex.ToString());
            return result;
        }
    }

    /// <summary>
    /// 前月末在庫CSVをインポート（期間指定対応版）
    /// </summary>
    /// <param name="startDate">フィルタ開始日付</param>
    /// <param name="endDate">フィルタ終了日付（nullの場合はstartDateと同じ）</param>
    /// <param name="preserveCsvDates">CSVの日付を保持するかどうか</param>
    /// <returns>インポート結果</returns>
    public async Task<PreviousMonthImportResult> ImportAsync(DateTime startDate, DateTime? endDate, bool preserveCsvDates = false)
    {
        var result = new PreviousMonthImportResult
        {
            StartTime = DateTime.Now,
            ImportType = "前月末在庫"
        };

        try
        {
            var effectiveEndDate = endDate ?? startDate;
            
            _logger.LogInformation("=== 前月末在庫インポート開始 ===");
            _logger.LogInformation("対象期間: {StartDate:yyyy-MM-dd} ～ {EndDate:yyyy-MM-dd}", startDate, effectiveEndDate);
            _logger.LogInformation("CSVの日付保持: {PreserveCsvDates}", preserveCsvDates);
            _logger.LogInformation("CSVファイルパス: {Path}", _importPath);

            // ファイル存在確認
            if (!File.Exists(_importPath))
            {
                _logger.LogError("CSVファイルが見つかりません: {Path}", _importPath);
                throw new FileNotFoundException($"インポートファイルが見つかりません: {_importPath}");
            }

            // 1. CSV読み込み
            var records = await ReadCsvAsync(_importPath);
            result.TotalRecords = records.Count;
            _logger.LogInformation("=== ステップ1: CSV読み込み完了 ===");
            _logger.LogInformation("読み込みレコード数: {Count}", records.Count);

            // 2. 有効レコードフィルタリング
            var validRecords = records.Where(r => r.IsValid()).ToList();
            var invalidRecords = records.Where(r => !r.IsValid()).ToList();
            
            _logger.LogInformation("=== ステップ2: バリデーション完了 ===");
            _logger.LogInformation("有効レコード: {Valid}件", validRecords.Count);
            _logger.LogInformation("無効レコード: {Invalid}件", invalidRecords.Count);
            
            // 無効レコードの詳細（最初の10件）
            foreach (var invalid in invalidRecords.Take(10))
            {
                _logger.LogWarning("無効レコード詳細: 商品={Product}, 等級={Grade}, 階級={Class}, 荷印={Mark}, 理由={Reason}",
                    invalid.ProductCode ?? "(null)", 
                    invalid.GradeCode ?? "(null)", 
                    invalid.ClassCode ?? "(null)", 
                    invalid.ShippingMarkCode ?? "(null)",
                    invalid.GetValidationError());
            }

            // 3. スキップ数のカウント
            var skippedByDateFilter = 0;
            var processedCount = 0;
            var errorCount = 0;
            var dateStatistics = new Dictionary<DateTime, int>(); // 日付別統計

            _logger.LogInformation("=== ステップ3: レコード処理開始 ===");

            foreach (var record in validRecords)
            {
                try
                {
                    var key = record.GetNormalizedKey();
                    
                    _logger.LogDebug("処理中レコード: 商品={Product}, 等級={Grade}, 階級={Class}, 荷印={Mark}, 荷印名={MarkName}",
                        key.ProductCode, key.GradeCode, key.ClassCode, key.ShippingMarkCode, key.ShippingMarkName);
                    
                    // preserveCsvDatesモードの処理
                    DateTime recordJobDate;
                    if (preserveCsvDates)
                    {
                        // CSVのJobDateを保持（前月末在庫の場合、伝票日付をJobDateとして使用）
                        recordJobDate = ParseVoucherDate(record);
                        
                        // 日付フィルタリング
                        if (recordJobDate.Date < startDate.Date)
                        {
                            _logger.LogDebug("行{index}: JobDateが開始日以前のためスキップ - JobDate: {JobDate:yyyy-MM-dd}", 
                                validRecords.IndexOf(record) + 1, recordJobDate);
                            skippedByDateFilter++;
                            continue;
                        }
                        
                        if (recordJobDate.Date > effectiveEndDate.Date)
                        {
                            _logger.LogDebug("行{index}: JobDateが終了日以後のためスキップ - JobDate: {JobDate:yyyy-MM-dd}", 
                                validRecords.IndexOf(record) + 1, recordJobDate);
                            skippedByDateFilter++;
                            continue;
                        }
                    }
                    else
                    {
                        // 既存の動作：JobDateを指定日付で上書き
                        recordJobDate = startDate;
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
                    
                    _logger.LogDebug("在庫マスタ検索結果: {Result}",
                        inventoryMaster != null ? "既存レコード更新" : "新規レコード作成");

                    if (inventoryMaster != null)
                    {
                        // 既存レコードのJobDateと前月末在庫を更新
                        var oldJobDate = inventoryMaster.JobDate;
                        inventoryMaster.JobDate = recordJobDate;  // JobDateを更新
                        inventoryMaster.PreviousMonthQuantity = record.Quantity;
                        inventoryMaster.PreviousMonthAmount = record.Amount;
                        inventoryMaster.UpdatedDate = DateTime.Now;
                        
                        await _inventoryRepository.UpdateAsync(inventoryMaster);
                        _logger.LogDebug("在庫マスタ更新: {Key}, JobDate: {OldDate} -> {NewDate}, 前月末数量={Qty}, 前月末金額={Amt}", 
                            key, oldJobDate, recordJobDate, record.Quantity, record.Amount);
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
                            JobDate = recordJobDate,
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
                    
                    // 日付別統計を収集
                    var jobDateKey = recordJobDate.Date;
                    if (dateStatistics.ContainsKey(jobDateKey))
                    {
                        dateStatistics[jobDateKey]++;
                    }
                    else
                    {
                        dateStatistics[jobDateKey] = 1;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "レコード処理エラー: {Record}", record);
                    errorCount++;
                    result.Errors.Add($"商品コード {record.ProductCode}: {ex.Message}");
                }
            }

            // 統計情報のログ出力
            if (preserveCsvDates && dateStatistics.Any())
            {
                _logger.LogInformation("日付別取込件数:");
                foreach (var kvp in dateStatistics.OrderBy(x => x.Key))
                {
                    _logger.LogInformation("  {Date:yyyy-MM-dd}: {Count}件", kvp.Key, kvp.Value);
                }
            }
            
            _logger.LogInformation("=== ステップ4: 処理完了 ===");
            _logger.LogInformation("処理済み: {Processed}件", processedCount);
            _logger.LogInformation("日付フィルタでスキップ: {DateSkipped}件", skippedByDateFilter);
            _logger.LogInformation("エラー: {Error}件", errorCount);

            result.ProcessedRecords = processedCount;
            result.ErrorRecords = errorCount;
            result.EndTime = DateTime.Now;
            result.IsSuccess = errorCount == 0;
            result.Message = $"前月末在庫インポート完了: 処理 {processedCount}件, スキップ {skippedByDateFilter}件, エラー {errorCount}件";

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
    /// PreviousMonthInventoryCsvの日付解析メソッド
    /// </summary>
    private DateTime ParseVoucherDate(PreviousMonthInventoryCsv record)
    {
        if (DateTime.TryParseExact(record.VoucherDate, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        else if (DateTime.TryParseExact(record.VoucherDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }
        else
        {
            throw new FormatException($"伝票日付の解析に失敗しました: {record.VoucherDate}");
        }
    }

    /// <summary>
    /// CSVのJobDate項目（48列目）を解析するメソッド
    /// 初期在庫設定時に使用（参照用）
    /// </summary>
    private DateTime ParseJobDate(string jobDateString)
    {
        if (string.IsNullOrWhiteSpace(jobDateString))
        {
            throw new FormatException("JobDate項目が空白です。CSVデータを確認してください。");
        }

        // YYYYMMDD形式の解析を試行
        if (jobDateString.Length == 8 && 
            DateTime.TryParseExact(jobDateString, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            _logger.LogDebug("JobDate解析成功 (YYYYMMDD): {Original} -> {Parsed:yyyy-MM-dd}", jobDateString, date);
            return date;
        }

        // YYYY/MM/DD形式の解析を試行
        if (DateTime.TryParseExact(jobDateString, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            _logger.LogDebug("JobDate解析成功 (yyyy/MM/dd): {Original} -> {Parsed:yyyy-MM-dd}", jobDateString, date);
            return date;
        }

        // YYYY-MM-DD形式の解析を試行
        if (DateTime.TryParseExact(jobDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            _logger.LogDebug("JobDate解析成功 (yyyy-MM-dd): {Original} -> {Parsed:yyyy-MM-dd}", jobDateString, date);
            return date;
        }

        // M/d/yyyy形式の解析を試行（例：6/13/2025）
        if (DateTime.TryParseExact(jobDateString, "M/d/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            _logger.LogDebug("JobDate解析成功 (M/d/yyyy): {Original} -> {Parsed:yyyy-MM-dd}", jobDateString, date);
            return date;
        }

        // 一般的な日付解析を試行
        if (DateTime.TryParse(jobDateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            _logger.LogDebug("JobDate解析成功 (一般形式): {Original} -> {Parsed:yyyy-MM-dd}", jobDateString, date);
            return date.Date;
        }

        // 解析に失敗した場合はエラーとして扱う（フォールバックしない）
        _logger.LogError("JobDate項目の解析に失敗しました: '{JobDate}'。サポートされている形式: YYYYMMDD, yyyy/MM/dd, yyyy-MM-dd", jobDateString);
        throw new FormatException($"JobDate項目の解析に失敗しました: '{jobDateString}'。CSVデータの形式を確認してください。");
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

