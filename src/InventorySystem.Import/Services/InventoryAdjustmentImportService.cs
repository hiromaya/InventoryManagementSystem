using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Import.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using InventorySystem.Core.Models;
using InventorySystem.Core.Services;
using InventorySystem.Import.Helpers;
// using DataSetStatus = InventorySystem.Core.Interfaces.DataSetStatus; // 削除済み

namespace InventorySystem.Import.Services;

/// <summary>
/// 在庫調整CSV取込サービス
/// </summary>
public class InventoryAdjustmentImportService
{
    private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
    private readonly IDataSetManagementRepository _dataSetManagementRepository;
    private readonly IDataSetService _unifiedDataSetService;
    private readonly ILogger<InventoryAdjustmentImportService> _logger;
    private readonly IDataSetIdManager _dataSetIdManager;
    
    public InventoryAdjustmentImportService(
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        IDataSetManagementRepository dataSetManagementRepository,
        IDataSetService unifiedDataSetService,
        ILogger<InventoryAdjustmentImportService> logger,
        IDataSetIdManager dataSetIdManager)
    {
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _dataSetManagementRepository = dataSetManagementRepository;
        _unifiedDataSetService = unifiedDataSetService;
        _logger = logger;
        _dataSetIdManager = dataSetIdManager;
    }

    /// <summary>
    /// CSVファイルから在庫調整データを取込む（後方互換性のための既存メソッド）
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
    /// CSVファイルから在庫調整データを取込む（期間指定対応版）
    /// </summary>
    /// <param name="filePath">取込対象CSVファイルパス</param>
    /// <param name="startDate">フィルタ開始日付（nullの場合は全期間）</param>
    /// <param name="endDate">フィルタ終了日付（nullの場合は全期間）</param>
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

        _logger.LogInformation("在庫調整CSV取込開始: {FilePath}, Department: {DepartmentCode}, StartDate: {StartDate}, EndDate: {EndDate}, PreserveCsvDates: {PreserveCsvDates}", 
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
            dataSetId = await _dataSetIdManager.CreateNewDataSetIdAsync(effectiveJobDate, "InventoryAdjustment");
            
            _logger.LogInformation("DataSetId決定: {DataSetId} (JobDate: {JobDate})", dataSetId, effectiveJobDate.ToString("yyyy-MM-dd"));
            _logger.LogInformation("=== DataSetId決定プロセス完了 ===");

            // 統一データセット作成（既存の仕組みとの互換性のため）
            dataSetId = await _unifiedDataSetService.CreateDataSetAsync(
                $"在庫調整取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                "ADJUSTMENT",
                effectiveJobDate,
                $"在庫調整CSVファイル取込: {Path.GetFileName(filePath)}",
                filePath,
                dataSetId); // 生成済みのDataSetIdを渡す

            // CSV読み込み処理（販売大臣フォーマット対応）
            var adjustments = new List<InventoryAdjustment>();

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (record.IsSummaryRow())
                    {
                        continue; // 集計行はスキップ
                    }

                    // 商品コードがオール0の場合はスキップ
                    if (record.ProductCode == "00000")
                    {
                        _logger.LogInformation("行{index}: 商品コードがオール0のためスキップします。伝票番号: {VoucherNumber}", index, record.VoucherNumber);
                        skippedCount++;
                        continue;
                    }

                    if (!record.IsValidInventoryAdjustment())
                    {
                        var error = $"行{index}: 不正な在庫調整データ - 伝票番号: {record.VoucherNumber}";
                        errorMessages.Add(error);
                        _logger.LogWarning(error);
                        continue;
                    }

                    var adjustment = record.ToEntity(dataSetId);
                    
                    // 日付フィルタリング処理（JobDateの改変は行わない）
                    if (startDate.HasValue && adjustment.JobDate.Date < startDate.Value.Date)
                    {
                        _logger.LogDebug("行{index}: JobDateが開始日以前のためスキップ - JobDate: {JobDate:yyyy-MM-dd}", index, adjustment.JobDate);
                        skippedCount++;
                        continue;
                    }

                    if (endDate.HasValue && adjustment.JobDate.Date > endDate.Value.Date)
                    {
                        _logger.LogDebug("行{index}: JobDateが終了日以後のためスキップ - JobDate: {JobDate:yyyy-MM-dd}", index, adjustment.JobDate);
                        skippedCount++;
                        continue;
                    }

                    // JobDateはCSVの値をそのまま使用（改変しない）
                    _logger.LogDebug("行{index}: JobDate={JobDate:yyyy-MM-dd} (CSVの値を保持)", index, adjustment.JobDate);
                    
                    // デバッグログ追加: エンティティ変換後
                    if (index <= 10)
                    {
                        _logger.LogDebug("Entity変換後: VoucherDate={VoucherDate:yyyy-MM-dd}, JobDate={JobDate:yyyy-MM-dd}", 
                            adjustment.VoucherDate, adjustment.JobDate);
                    }
                    
                    // VoucherIdとLineNumberを設定
                    adjustment.VoucherId = $"{dataSetId}_{adjustment.VoucherNumber}";
                    adjustment.LineNumber = index; // 行番号を使用
                    adjustments.Add(adjustment);
                    importedCount++;
                    
                    // 日付別統計を収集
                    var jobDateKey = adjustment.JobDate.Date;
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
            
            _logger.LogInformation("在庫調整CSV取込結果: 読込{Total}件, 成功{Success}件, スキップ{Skipped}件, エラー{Error}件", 
                records.Count, importedCount, skippedCount, errorMessages.Count);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _unifiedDataSetService.SetErrorAsync(dataSetId, errorMessage);
                _logger.LogWarning("在庫調整CSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    importedCount, errorMessages.Count);
            }
            else
            {
                await _unifiedDataSetService.UpdateStatusAsync(dataSetId, "Completed");
                _logger.LogInformation("在庫調整CSV取込完了: {Count}件", importedCount);
            }

            return dataSetId;
        }
        catch (Exception ex)
        {
            await _unifiedDataSetService.SetErrorAsync(dataSetId, ex.Message);
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
        
        // デバッグログ追加: CSVヘッダー確認
        var headers = csv.HeaderRecord;
        _logger.LogDebug("CSVヘッダー数: {HeaderCount}, JobDate列インデックス: {JobDateIndex}", 
            headers?.Length ?? 0, Array.IndexOf(headers ?? new string[0], "ジョブデート"));
        
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
        var importedData = await _inventoryAdjustmentRepository.GetByDataSetIdAsync(dataSetId);
        
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