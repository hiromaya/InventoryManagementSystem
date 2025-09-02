using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Import.Models;
using InventorySystem.Data.Repositories;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using InventorySystem.Core.Models;
using InventorySystem.Core.Services;
using InventorySystem.Import.Helpers;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;
// using DataSetStatus = InventorySystem.Core.Interfaces.DataSetStatus; // 削除済み

namespace InventorySystem.Import.Services;

/// <summary>
/// 仕入伝票CSV取込サービス
/// </summary>
public class PurchaseVoucherImportService
{
    private readonly PurchaseVoucherCsvRepository _purchaseVoucherRepository;
    private readonly IDataSetManagementRepository _dataSetManagementRepository;
    private readonly IDataSetService _unifiedDataSetService;
    private readonly ILogger<PurchaseVoucherImportService> _logger;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IDataSetIdManager _dataSetIdManager;
    private readonly string _connectionString;
    
    public PurchaseVoucherImportService(
        PurchaseVoucherCsvRepository purchaseVoucherRepository,
        IDataSetManagementRepository dataSetManagementRepository,
        IDataSetService unifiedDataSetService,
        ILogger<PurchaseVoucherImportService> logger,
        IInventoryRepository inventoryRepository,
        IDataSetIdManager dataSetIdManager,
        IConfiguration configuration)
    {
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _dataSetManagementRepository = dataSetManagementRepository;
        _unifiedDataSetService = unifiedDataSetService;
        _logger = logger;
        _inventoryRepository = inventoryRepository;
        _dataSetIdManager = dataSetIdManager;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    /// <summary>
    /// CSVファイルから仕入伝票データを取込む（後方互換性のための既存メソッド）
    /// </summary>
    /// <param name="filePath">取込対象CSVファイルパス</param>
    /// <param name="startDate">フィルタ開始日付（nullの場合は全期間）</param>
    /// <param name="endDate">フィルタ終了日付（nullの場合は全期間）</param>
    /// <param name="departmentCode">部門コード（省略時は使用しない）</param>
    /// <returns>データセットID</returns>
    public async Task<string> ImportAsync(string filePath, DateTime? startDate, DateTime? endDate, string? departmentCode = null)
    {
        // 既存の動作を維持（JobDateを指定日付で上書き）
        return await ImportAsync(filePath, startDate, endDate, departmentCode, preserveCsvDates: false);
    }

    /// <summary>
    /// CSVファイルから仕入伝票データを取込む（期間指定対応版）
    /// </summary>
    /// <param name="filePath">取込対象CSVファイルパス</param>
    /// <param name="startDate">フィルタ開始日付（nullの場合は全期間）</param>
    /// <param name="endDate">フィルタ紂了日付（nullの場合は全期間）</param>
    /// <param name="departmentCode">部門コード（省略時は使用しない）</param>
    /// <param name="preserveCsvDates">CSVの日付を保持するかどうか</param>
    /// <returns>データセットID</returns>
    public async Task<string> ImportAsync(string filePath, DateTime? startDate, DateTime? endDate, string? departmentCode = null, bool preserveCsvDates = false)
    {
        // preserveCsvDatesパラメータは廃止予定
        if (!preserveCsvDates)
        {
            _logger.LogWarning("preserveCsvDates=falseは廃止予定です。JobDateの改変は仕様違反のため、" +
                              "今後は常にCSVの汎用日付2の値を使用します。");
        }
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSVファイルが見つかりません: {filePath}");
        }

        var importedCount = 0;
        var skippedCount = 0;
        var errorMessages = new List<string>();
        var dateStatistics = new Dictionary<DateTime, int>(); // 日付別統計

        _logger.LogInformation("仕入伝票CSV取込開始: {FilePath}, Department: {DepartmentCode}, StartDate: {StartDate}, EndDate: {EndDate}, PreserveCsvDates: {PreserveCsvDates}", 
            filePath, departmentCode ?? "未指定", startDate?.ToString("yyyy-MM-dd") ?? "全期間", endDate?.ToString("yyyy-MM-dd") ?? "全期間", preserveCsvDates);

        string dataSetId = string.Empty;
        try
        {
            // まずCSVを読み込んでJobDateを特定
            var records = await ReadDaijinCsvFileAsync(filePath);
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            if (records.Count == 0)
            {
                throw new InvalidOperationException("CSVファイルにデータが存在しません");
            }

            // DataSetId決定ロジック
            DateTime effectiveJobDate;
            
            _logger.LogInformation("=== DataSetId決定プロセス開始 ===");
            _logger.LogInformation("入力パラメータ - StartDate: {StartDate}, EndDate: {EndDate}, PreserveCsvDates: {PreserveCsvDates}",
                startDate?.ToString("yyyy-MM-dd") ?? "null",
                endDate?.ToString("yyyy-MM-dd") ?? "null",
                preserveCsvDates);
            _logger.LogInformation("CSVレコード総数: {TotalRecords}件", records.Count);

            // 1. コマンドライン引数の日付を優先
            if (startDate.HasValue)
            {
                effectiveJobDate = startDate.Value;
                _logger.LogInformation("コマンドライン引数の日付を使用: {Date}", effectiveJobDate.ToString("yyyy-MM-dd"));
            }
            // 2. CSVの最初のレコードから取得
            else
            {
                var firstRecord = records.First();
                effectiveJobDate = DateParsingHelper.ParseJobDate(firstRecord.JobDate);
                
                _logger.LogInformation("CSVの最初のレコードからJobDateを取得: {Date} (入力値: {Input})", 
                    effectiveJobDate.ToString("yyyy-MM-dd"), firstRecord.JobDate);
            }

            // DataSetIdManagerを使って新しいDataSetIdを生成
            dataSetId = await _dataSetIdManager.CreateNewDataSetIdAsync(effectiveJobDate, "PurchaseVoucher");
            
            _logger.LogInformation("DataSetId決定: {DataSetId} (JobDate: {JobDate})", dataSetId, effectiveJobDate.ToString("yyyy-MM-dd"));
            _logger.LogInformation("=== DataSetId決定プロセス完了 ===");

            // ===== 新規追加: 既存DataSetの無効化 =====
            try
            {
                // 同一JobDate+ProcessTypeの既存DataSetを無効化
                await _unifiedDataSetService.DeactivateOldDataSetsAsync(
                    effectiveJobDate, "PURCHASE", dataSetId);
                
                _logger.LogInformation(
                    "既存の仕入伝票DataSetを無効化しました: JobDate={JobDate}",
                    effectiveJobDate.ToString("yyyy-MM-dd"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "既存DataSetの無効化中にエラーが発生しましたが、処理を続行します。");
            }
            // ===== 新規追加ここまで =====

            // 統一データセット作成（既存の仕組みとの互換性のため）
            dataSetId = await _unifiedDataSetService.CreateDataSetAsync(
                $"仕入伝票取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                "PURCHASE",
                effectiveJobDate,
                $"仕入伝票CSVファイル取込: {Path.GetFileName(filePath)}",
                filePath,
                dataSetId); // 生成済みのDataSetIdを渡す

            // 商品マスタを事前に読み込む（パフォーマンス向上のため）
            var productMasterDict = new Dictionary<string, string>();
            using (var productConnection = new SqlConnection(_connectionString))
            {
                var products = await productConnection.QueryAsync<(string ProductCode, string ProductName)>(
                    "SELECT ProductCode, ISNULL(ProductName, '') as ProductName FROM ProductMaster"
                );
                productMasterDict = products.ToDictionary(p => p.ProductCode, p => p.ProductName ?? "");
                _logger.LogInformation("商品マスタ読み込み完了: {Count}件", productMasterDict.Count);
            }

            // CSV読み込み処理（販売大臣フォーマット対応）
            var purchaseVouchers = new List<PurchaseVoucher>();

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    // 仕入先コードがオール0の場合はスキップ
                    if (record.SupplierCode == "00000")
                    {
                        _logger.LogInformation("行{index}: 仕入先コードがオール0のためスキップします。伝票番号: {VoucherNumber}", index, record.VoucherNumber);
                        skippedCount++;
                        continue;
                    }

                    // 商品コードがオール0の場合もスキップ
                    if (record.ProductCode == "00000")
                    {
                        _logger.LogInformation("行{index}: 商品コードがオール0のためスキップします。伝票番号: {VoucherNumber}", index, record.VoucherNumber);
                        skippedCount++;
                        continue;
                    }

                    if (!record.IsValidPurchaseVoucher())
                    {
                        var validationError = record.GetValidationError();
                        var debugInfo = record.GetDebugInfo();
                        var error = $"行{index}: 不正な仕入伝票データ - 伝票番号: {record.VoucherNumber}, 理由: {validationError}";
                        errorMessages.Add(error);
                        _logger.LogWarning("{Error}, データ詳細: {DebugInfo}", error, debugInfo);
                        continue;
                    }

                    var purchaseVoucher = record.ToEntity(dataSetId);
                    
                    // 商品名を商品マスタから設定
                    if (productMasterDict.TryGetValue(purchaseVoucher.ProductCode, out var productName))
                    {
                        purchaseVoucher.ProductName = productName;
                    }
                    else
                    {
                        _logger.LogWarning("行{index}: 商品マスタに商品コード {ProductCode} が存在しません", index, purchaseVoucher.ProductCode);
                        purchaseVoucher.ProductName = "";
                    }
                    
                    // 明細種別3（単品値引）の特別ログ
                    if (record.DetailType == "3" && record.Quantity == 0)
                    {
                        _logger.LogInformation("行{index}: 単品値引データ - 伝票番号: {VoucherNumber}, 金額: {Amount}", 
                            index, record.VoucherNumber, record.Amount);
                    }
                    
                    // 日付フィルタリング処理（JobDateの改変は行わない）
                    if (startDate.HasValue && purchaseVoucher.JobDate.Date < startDate.Value.Date)
                    {
                        _logger.LogDebug("行{index}: JobDateが開始日以前のためスキップ - JobDate: {JobDate:yyyy-MM-dd}", index, purchaseVoucher.JobDate);
                        skippedCount++;
                        continue;
                    }

                    if (endDate.HasValue && purchaseVoucher.JobDate.Date > endDate.Value.Date)
                    {
                        _logger.LogDebug("行{index}: JobDateが終了日以後のためスキップ - JobDate: {JobDate:yyyy-MM-dd}", index, purchaseVoucher.JobDate);
                        skippedCount++;
                        continue;
                    }

                    // JobDateはCSVの値をそのまま使用（改変しない）
                    _logger.LogDebug("行{index}: JobDate={JobDate:yyyy-MM-dd} (CSVの値を保持)", index, purchaseVoucher.JobDate);
                    
                    // デバッグログ追加: エンティティ変換後
                    if (index <= 10)
                    {
                        _logger.LogDebug("Entity変換後: VoucherDate={VoucherDate:yyyy-MM-dd}, JobDate={JobDate:yyyy-MM-dd}", 
                            purchaseVoucher.VoucherDate, purchaseVoucher.JobDate);
                    }
                    
                    purchaseVouchers.Add(purchaseVoucher);
                    importedCount++;
                    
                    // 日付別統計を収集
                    var jobDateKey = purchaseVoucher.JobDate.Date;
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
                    var error = $"行{index}: CSV変換エラー - {ex.Message}";
                    errorMessages.Add(error);
                    _logger.LogError(ex, error);
                }
            }

            // バッチ処理でデータベースに保存
            if (purchaseVouchers.Any())
            {
                const int batchSize = 1000;
                for (int i = 0; i < purchaseVouchers.Count; i += batchSize)
                {
                    var batch = purchaseVouchers.Skip(i).Take(batchSize);
                    await _purchaseVoucherRepository.BulkInsertAsync(batch);
                    
                    _logger.LogInformation("バッチ保存完了: {Start}-{End}件目", 
                        i + 1, Math.Min(i + batchSize, purchaseVouchers.Count));
                }
            }

            // データセットレコード数更新
            await _unifiedDataSetService.UpdateRecordCountAsync(dataSetId, importedCount);
            
            // 統計情報のログ出力
            if (preserveCsvDates && dateStatistics.Any())
            {
                _logger.LogInformation("日付別取込件数:");
                foreach (var kvp in dateStatistics.OrderBy(x => x.Key))
                {
                    _logger.LogInformation("  {Date:yyyy-MM-dd}: {Count}件", kvp.Key, kvp.Value);
                }
            }
            
            _logger.LogInformation("仕入伝票CSV取込結果: 成功{Success}件, スキップ{Skipped}件, エラー{Error}件", 
                importedCount, skippedCount, errorMessages.Count);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _unifiedDataSetService.SetErrorAsync(dataSetId, errorMessage);
                _logger.LogWarning("仕入伝票CSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    importedCount, errorMessages.Count);
            }
            else
            {
                await _unifiedDataSetService.UpdateStatusAsync(dataSetId, "Completed");
                _logger.LogInformation("仕入伝票CSV取込完了: {Count}件", importedCount);
                
                // 最終仕入日を更新
                if (startDate.HasValue && importedCount > 0)
                {
                    try
                    {
                        await _inventoryRepository.UpdateLastPurchaseDateAsync(startDate.Value);
                        _logger.LogInformation("最終仕入日を更新しました: {TargetDate:yyyy-MM-dd}", startDate.Value);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogWarning(updateEx, "最終仕入日の更新に失敗しました。処理は継続します。");
                    }
                }
            }

            return dataSetId;
        }
        catch (Exception ex)
        {
            await _unifiedDataSetService.SetErrorAsync(dataSetId, ex.Message);
            _logger.LogError(ex, "仕入伝票CSV取込エラー: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// 販売大臣CSVファイルを読み込む（171列フォーマット対応）
    /// </summary>
    private async Task<List<PurchaseVoucherDaijinCsv>> ReadDaijinCsvFileAsync(string filePath)
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
            TrimOptions = TrimOptions.None  // 手入力荷印の全角スペース保持のためTrimを無効化
        });

        // ヘッダーを読み込む
        await csv.ReadAsync();
        csv.ReadHeader();
        
        // デバッグログ追加: CSVヘッダー確認
        var headers = csv.HeaderRecord;
        _logger.LogDebug("CSVヘッダー数: {HeaderCount}, JobDate列インデックス: {JobDateIndex}", 
            headers?.Length ?? 0, Array.IndexOf(headers ?? new string[0], "ジョブデート"));
        
        var records = new List<PurchaseVoucherDaijinCsv>();
        var rowNumber = 1;
        
        while (await csv.ReadAsync())
        {
            rowNumber++;
            try
            {
                var record = csv.GetRecord<PurchaseVoucherDaijinCsv>();
                if (record != null)
                {
                    // デバッグログ追加: 各レコード読み込み時
                    if (rowNumber <= 11)
                    {
                        _logger.LogDebug("CSV行{LineNumber}: VoucherDate='{VoucherDate}', JobDate='{JobDate}', VoucherNumber='{VoucherNumber}'", 
                            rowNumber, record.VoucherDate, record.JobDate, record.VoucherNumber);
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
        
        return records;
    }


    /// <summary>
    /// 取込結果を取得
    /// </summary>
    public async Task<ImportResult> GetImportResultAsync(string dataSetId)
    {
        // DataSetManagementテーブルから取得
        var dataSetMgmt = await _dataSetManagementRepository.GetByIdAsync(dataSetId);
        if (dataSetMgmt == null)
        {
            throw new InvalidOperationException($"データセットが見つかりません: {dataSetId}");
        }
        
        // インポートされたデータを取得
        var importedData = await _purchaseVoucherRepository.GetByDataSetIdAsync(dataSetId);
        
        return new ImportResult
        {
            DataSetId = dataSetId,
            Status = dataSetMgmt.Status,
            ImportedCount = dataSetMgmt.RecordCount,
            ErrorMessage = dataSetMgmt.ErrorMessage,
            FilePath = dataSetMgmt.FilePath,
            CreatedAt = dataSetMgmt.CreatedAt,
            ImportedData = importedData.Cast<object>().ToList()
        };
    }

}