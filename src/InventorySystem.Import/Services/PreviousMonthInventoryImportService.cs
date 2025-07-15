using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Import.Models;
using InventorySystem.Data.Repositories;

namespace InventorySystem.Import.Services;

/// <summary>
/// 前月末在庫CSVインポートサービス
/// </summary>
public class PreviousMonthInventoryImportService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ILogger<PreviousMonthInventoryImportService> _logger;
    private readonly string _importPath;
    private readonly IDataSetManagementRepository _dataSetRepository;

    public PreviousMonthInventoryImportService(
        IInventoryRepository inventoryRepository,
        ILogger<PreviousMonthInventoryImportService> logger,
        IDataSetManagementRepository dataSetRepository)
    {
        _inventoryRepository = inventoryRepository;
        _logger = logger;
        _dataSetRepository = dataSetRepository;
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

            // 1. CSV読み込み（トランザクション外で実行）
            var records = await ReadCsvAsync(_importPath);
            result.TotalRecords = records.Count;
            _logger.LogInformation("=== ステップ1: CSV読み込み完了 ===");
            _logger.LogInformation("読み込みレコード数: {Count}", records.Count);

            // 2. 有効レコードフィルタリング（トランザクション外で実行）
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

            // DataSetIdを生成
            var dataSetId = DataSetManagement.GenerateDataSetId("INIT");
            _logger.LogInformation("DataSetId生成: {DataSetId}", dataSetId);
            
            // 前月末の日付を取得
            var importDate = GetLastDayOfPreviousMonth();
            
            // 3. トランザクション内での一括処理準備
            var inventoryList = new List<InventoryMaster>();
            var errorCount = 0;

            _logger.LogInformation("=== ステップ3: 初期在庫設定データ準備 ===");
            _logger.LogInformation("JobDate設定: {JobDate:yyyy-MM-dd}", importDate);

            // 在庫データリストを作成（トランザクション外で準備）
            foreach (var record in validRecords)
            {
                try
                {
                    var key = record.GetNormalizedKey();
                    
                    // CSVのジョブデート項目（48列目）を解析してJobDateとして設定
                    var jobDate = ParseJobDate(record.JobDate);
                    
                    _logger.LogDebug("処理中レコード: 商品={Product}, 等級={Grade}, 階級={Class}, 荷印={Mark}, 荷印名={MarkName}, JobDate={JobDate:yyyy-MM-dd}",
                        key.ProductCode, key.GradeCode, key.ClassCode, key.ShippingMarkCode, key.ShippingMarkName, jobDate);

                    // 在庫データを作成
                    var inventoryMaster = new InventoryMaster
                    {
                        Key = new InventoryKey
                        {
                            ProductCode = key.ProductCode,
                            GradeCode = key.GradeCode,
                            ClassCode = key.ClassCode,
                            ShippingMarkCode = key.ShippingMarkCode,
                            ShippingMarkName = key.ShippingMarkName
                        },
                        PreviousMonthQuantity = record.Quantity,
                        PreviousMonthAmount = record.Amount,
                        // 前月末在庫を現在在庫として初期化
                        CurrentStock = record.Quantity,
                        CurrentStockAmount = record.Amount,
                        JobDate = jobDate,
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now,
                        ProductName = "商品名未設定",
                        Unit = "PCS",
                        StandardPrice = 0,
                        ProductCategory1 = "",
                        ProductCategory2 = "",
                        // 前月末在庫を日次在庫として初期化
                        DailyStock = record.Quantity,
                        DailyStockAmount = record.Amount,
                        DailyFlag = '9',
                        DataSetId = dataSetId,
                        DailyGrossProfit = 0,
                        DailyAdjustmentAmount = 0,
                        DailyProcessingCost = 0,
                        FinalGrossProfit = 0,
                        ImportType = "INIT",
                        IsActive = true,
                        CreatedBy = "init-inventory"
                    };
                    
                    inventoryList.Add(inventoryMaster);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "レコード処理エラー: {Record}", record);
                    errorCount++;
                    result.Errors.Add($"商品コード {record.ProductCode}: {ex.Message}");
                }
            }
            
            _logger.LogInformation("データ準備完了: 処理対象 {Count}件, エラー {Error}件", inventoryList.Count, errorCount);
            
            // 4. トランザクション内で一括処理
            if (inventoryList.Any())
            {
                _logger.LogInformation("=== ステップ4: トランザクション処理開始 ===");
                
                // DataSetManagementエンティティを作成
                var dataSetManagement = new DataSetManagement
                {
                    DatasetId = dataSetId,
                    JobDate = importDate,
                    ProcessType = "INIT_INVENTORY",
                    ImportType = "INIT",
                    RecordCount = inventoryList.Count,
                    TotalRecordCount = inventoryList.Count,
                    IsActive = true,
                    IsArchived = false,
                    ParentDataSetId = null,
                    ImportedFiles = Path.GetFileName(_importPath),
                    CreatedAt = DateTime.Now,
                    CreatedBy = "init-inventory",
                    Department = "DeptA",
                    Notes = $"前月末在庫インポート: {inventoryList.Count}件"
                };
                
                // トランザクション内で一括処理
                var processedCount = await _inventoryRepository.ProcessInitialInventoryInTransactionAsync(
                    inventoryList, 
                    dataSetManagement,
                    deactivateExisting: true
                );
                
                _logger.LogInformation("=== ステップ5: トランザクション処理完了 ===");
                _logger.LogInformation("処理済み: {Processed}件", processedCount);
            }
            else
            {
                _logger.LogWarning("処理対象のデータがありません");
            }

            result.ProcessedRecords = inventoryList.Count;
            result.ErrorRecords = errorCount;
            result.EndTime = DateTime.Now;
            result.IsSuccess = errorCount == 0 && inventoryList.Any();
            result.Message = $"初期在庫設定完了: 処理 {inventoryList.Count}件, エラー {errorCount}件";

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

            // DataSetIdを生成
            var dataSetId = DataSetManagement.GenerateDataSetId("INIT");
            _logger.LogInformation("DataSetId生成: {DataSetId}", dataSetId);
            
            // 既存のINITデータを確認（対象期間のもの）
            if (!preserveCsvDates)
            {
                // 通常モード：指定日付のINITデータを確認
                var existingInitData = await _inventoryRepository.GetActiveInitInventoryAsync(startDate);
                if (existingInitData.Any())
                {
                    _logger.LogWarning("既存の前月末在庫データが存在します。DataSetId: {DataSetId}, 件数: {Count}件",
                        existingInitData.First().DataSetId, existingInitData.Count);
                    
                    // 既存データを無効化
                    await _inventoryRepository.DeactivateDataSetAsync(existingInitData.First().DataSetId);
                    _logger.LogInformation("既存データを無効化しました");
                }
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
                        // inventoryMaster.JobDate = recordJobDate;  // JobDateを更新 - コメントアウト：既存レコードのJobDateは変更しない
                        inventoryMaster.PreviousMonthQuantity = record.Quantity;
                        inventoryMaster.PreviousMonthAmount = record.Amount;
                        inventoryMaster.UpdatedDate = DateTime.Now;
                        inventoryMaster.DataSetId = dataSetId;      // DataSetIdを設定
                        inventoryMaster.ImportType = "INIT";        // ImportTypeを設定
                        inventoryMaster.IsActive = true;            // アクティブフラグ
                        
                        await _inventoryRepository.UpdateAsync(inventoryMaster);
                        _logger.LogDebug("在庫マスタ更新: {Key}, JobDate維持: {OldDate}, 前月末数量={Qty}, 前月末金額={Amt}", 
                            key, oldJobDate, record.Quantity, record.Amount);
                    }
                    else
                    {
                        // 新規レコードの作成（初回のみ）
                        inventoryMaster = new InventoryMaster
                        {
                            Key = inventoryKey,
                            PreviousMonthQuantity = record.Quantity,
                            PreviousMonthAmount = record.Amount,
                            // 前月末在庫を現在在庫として初期化
                            CurrentStock = record.Quantity,
                            CurrentStockAmount = record.Amount,
                            JobDate = recordJobDate,
                            CreatedDate = DateTime.Now,
                            UpdatedDate = DateTime.Now,
                            ProductName = "商品名未設定",
                            Unit = "PCS",
                            StandardPrice = 0,
                            ProductCategory1 = "",
                            ProductCategory2 = "",
                            // 前月末在庫を日次在庫として初期化
                            DailyStock = record.Quantity,
                            DailyStockAmount = record.Amount,
                            DailyFlag = '9',
                            DataSetId = dataSetId,             // 生成したDataSetIdを使用
                            DailyGrossProfit = 0,
                            DailyAdjustmentAmount = 0,
                            DailyProcessingCost = 0,
                            FinalGrossProfit = 0,
                            ImportType = "INIT",               // 前月末在庫を示す"INIT"
                            IsActive = true,                   // アクティブフラグ追加
                            CreatedBy = "init-inventory"       // 作成者情報追加
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
            
            // DataSetManagementに登録
            if (processedCount > 0)
            {
                await _dataSetRepository.CreateAsync(new DataSetManagement
                {
                    DatasetId = dataSetId,
                    JobDate = startDate,
                    ProcessType = "INIT_INVENTORY",
                    ImportType = "INIT",
                    RecordCount = processedCount,
                    TotalRecordCount = processedCount,
                    IsActive = true,
                    IsArchived = false,
                    ParentDataSetId = null,
                    ImportedFiles = Path.GetFileName(_importPath),
                    CreatedAt = DateTime.Now,
                    CreatedBy = "init-inventory",
                    Department = "DeptA",
                    Notes = $"前月末在庫インポート: {processedCount}件"
                });
                
                _logger.LogInformation("DataSetManagementに登録完了: DataSetId={DataSetId}", dataSetId);
            }

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
    /// 既存のimport-folderロジックと同じ多言語対応の実装を使用
    /// </summary>
    private DateTime ParseJobDate(string jobDateString)
    {
        if (string.IsNullOrWhiteSpace(jobDateString))
        {
            _logger.LogError("JobDate項目が空白です。CSVデータを確認してください。");
            throw new FormatException("JobDate項目が空白です。CSVデータを確認してください。");
        }

        // 既存のimport-folderロジックと同じ日付形式配列（多言語対応）
        string[] dateFormats = new[]
        {
            "yyyy/MM/dd",     // CSVで最も使用される形式（例：2025/06/01）
            "yyyy-MM-dd",     // ISO形式
            "yyyyMMdd",       // 8桁数値形式
            "yyyy/M/d",       // 月日が1桁の場合（例：2025/6/1）
            "yyyy-M-d",       // ISO形式で月日が1桁
            "dd/MM/yyyy",     // ヨーロッパ形式（例：01/06/2025）
            "dd.MM.yyyy",     // ドイツ語圏形式（例：01.06.2025）
            "M/d/yyyy",       // 米国形式（例：6/1/2025）
            "d/M/yyyy",       // 英国形式（例：1/6/2025）
            "d.M.yyyy"        // ドイツ語圏短縮形式（例：1.6.2025）
        };
        
        // InvariantCultureで複数形式を厳密に試行
        if (DateTime.TryParseExact(jobDateString.Trim(), dateFormats, 
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            _logger.LogDebug("JobDate解析成功 (厳密形式): '{Original}' -> {Parsed:yyyy-MM-dd}", jobDateString, date);
            return date;
        }
        
        // 最終手段：InvariantCultureで標準解析
        if (DateTime.TryParse(jobDateString.Trim(), CultureInfo.InvariantCulture, 
            DateTimeStyles.None, out var parsedDate))
        {
            _logger.LogDebug("JobDate解析成功 (標準解析): '{Original}' -> {Parsed:yyyy-MM-dd}", jobDateString, parsedDate);
            return parsedDate.Date;
        }
        
        // 解析失敗：詳細なエラー情報を提供
        _logger.LogError("JobDate項目の解析に失敗しました: '{JobDate}'。サポート形式: {Formats}", 
            jobDateString, string.Join(", ", dateFormats));
        throw new FormatException($"JobDate項目の解析に失敗しました: '{jobDateString}'。" +
            $"サポートされている形式: {string.Join(", ", dateFormats)}");
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
    
    /// <summary>
    /// 前月末の最終日を取得する
    /// </summary>
    private static DateTime GetLastDayOfPreviousMonth()
    {
        var today = DateTime.Today;
        var firstDayOfThisMonth = new DateTime(today.Year, today.Month, 1);
        return firstDayOfThisMonth.AddDays(-1);
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

