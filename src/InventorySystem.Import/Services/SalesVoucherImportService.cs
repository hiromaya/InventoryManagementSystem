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
using InventorySystem.Import.Validators;

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
    /// CSVファイルから売上伝票データを取込む（後方互換性のための既存メソッド）
    /// </summary>
    /// <param name="filePath">取込対象CSVファイルパス</param>
    /// <param name="startDate">フィルタ開始日付（nullの場合は全期間）</param>
    /// <param name="endDate">フィルタ終了日付（nullの場合は全期間）</param>
    /// <param name="departmentCode">部門コード（省略時はデフォルト部門）</param>
    /// <returns>データセットID</returns>
    public async Task<string> ImportAsync(string filePath, DateTime? startDate, DateTime? endDate, string? departmentCode = null)
    {
        // 既存の動作を維持（JobDateを指定日付で上書き）
        return await ImportAsync(filePath, startDate, endDate, departmentCode, preserveCsvDates: false);
    }

    /// <summary>
    /// CSVファイルから売上伝票データを取込む（期間指定対応版）
    /// </summary>
    /// <param name="filePath">取込対象CSVファイルパス</param>
    /// <param name="startDate">フィルタ開始日付（nullの場合は全期間）</param>
    /// <param name="endDate">フィルタ終了日付（nullの場合は全期間）</param>
    /// <param name="departmentCode">部門コード（省略時はデフォルト部門）</param>
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

        // 部門コードの設定（省略時はデフォルト部門を使用）
        departmentCode ??= _departmentSettings.DefaultDepartment;
        var department = _departmentSettings.GetDepartment(departmentCode);
        
        var dataSetId = GenerateDataSetId();
        var importedCount = 0;
        var skippedCount = 0;
        var errorMessages = new List<string>();
        var dateStatistics = new Dictionary<DateTime, int>(); // 日付別統計

        _logger.LogInformation("売上伝票CSV取込開始: {FilePath}, DataSetId: {DataSetId}, Department: {DepartmentCode}, StartDate: {StartDate}, EndDate: {EndDate}, PreserveCsvDates: {PreserveCsvDates}", 
            filePath, dataSetId, departmentCode, startDate?.ToString("yyyy-MM-dd") ?? "全期間", endDate?.ToString("yyyy-MM-dd") ?? "全期間", preserveCsvDates);

        try
        {
            // データセット作成
            var dataSet = new DataSet
            {
                Id = dataSetId,
                ProcessType = "Sales",
                Name = $"売上伝票取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                Description = $"売上伝票CSVファイル取込: {Path.GetFileName(filePath)}",
                CreatedAt = DateTime.Now,
                RecordCount = 0,
                Status = DataSetStatus.Processing,
                FilePath = filePath,
                JobDate = startDate ?? DateTime.Today,
                DepartmentCode = departmentCode,
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
                    if (CodeValidator.IsExcludedCode(record.CustomerCode)) // 得意先コードがオール0
                    {
                        _logger.LogInformation("行{index}: 得意先コードがオール0のためスキップします。伝票番号: {VoucherNumber}", index, record.VoucherNumber);
                        skippedCount++;
                        continue;
                    }

                    // 商品コードがオール0の場合もスキップ（新仕様）
                    if (CodeValidator.IsExcludedCode(record.ProductCode))
                    {
                        _logger.LogInformation("行{index}: 商品コードがオール0のためスキップします。伝票番号: {VoucherNumber}", index, record.VoucherNumber);
                        skippedCount++;
                        continue;
                    }

                    if (!record.IsValidSalesVoucher())
                    {
                        var validationError = record.GetValidationError();
                        var debugInfo = record.GetDebugInfo();
                        var error = $"行{index}: 不正な売上伝票データ - 伝票番号: {record.VoucherNumber}, 理由: {validationError}";
                        errorMessages.Add(error);
                        _logger.LogWarning("{Error}, データ詳細: {DebugInfo}", error, debugInfo);
                        continue;
                    }

                    var salesVoucher = record.ToEntity(dataSetId);
                    
                    // 日付フィルタリング処理（JobDateの改変は行わない）
                    if (startDate.HasValue && salesVoucher.JobDate.Date < startDate.Value.Date)
                    {
                        _logger.LogDebug("行{index}: JobDateが開始日以前のためスキップ - JobDate: {JobDate:yyyy-MM-dd}", index, salesVoucher.JobDate);
                        skippedCount++;
                        continue;
                    }

                    if (endDate.HasValue && salesVoucher.JobDate.Date > endDate.Value.Date)
                    {
                        _logger.LogDebug("行{index}: JobDateが終了日以後のためスキップ - JobDate: {JobDate:yyyy-MM-dd}", index, salesVoucher.JobDate);
                        skippedCount++;
                        continue;
                    }

                    // JobDateはCSVの値をそのまま使用（改変しない）
                    _logger.LogDebug("行{index}: JobDate={JobDate:yyyy-MM-dd} (CSVの値を保持)", index, salesVoucher.JobDate);
                    
                    // デバッグログ追加: エンティティ変換後
                    if (index <= 10)
                    {
                        _logger.LogDebug("Entity変換後: VoucherDate={VoucherDate:yyyy-MM-dd}, JobDate={JobDate:yyyy-MM-dd}", 
                            salesVoucher.VoucherDate, salesVoucher.JobDate);
                    }
                    salesVoucher.DepartmentCode = departmentCode;
                    salesVouchers.Add(salesVoucher);
                    importedCount++;
                    
                    // 日付別統計を収集
                    var jobDateKey = salesVoucher.JobDate.Date;
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
            
            // 統計情報のログ出力
            if (dateStatistics.Any())
            {
                _logger.LogInformation("日付別取込件数:");
                foreach (var kvp in dateStatistics.OrderBy(x => x.Key))
                {
                    _logger.LogInformation("  {Date:yyyy-MM-dd}: {Count}件", kvp.Key, kvp.Value);
                }
            }
            
            _logger.LogInformation("売上伝票CSV取込結果: 読込{Total}件, 成功{Success}件, スキップ{Skipped}件, エラー{Error}件", 
                records.Count, importedCount, skippedCount, errorMessages.Count);
            
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
        // GUIDの最初の8文字のみ使用
        var guid = Guid.NewGuid().ToString("N");
        return $"SALES_{DateTime.Now:yyyyMMdd_HHmmss}_{guid.Substring(0, 8)}";
    }

    /// <summary>
    /// 販売大臣CSVファイルを読み込む（171列フォーマット対応）
    /// </summary>
    private async Task<List<SalesVoucherDaijinCsv>> ReadDaijinCsvFileAsync(string filePath)
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
                    // デバッグログ追加: 各レコード読み込み時
                    if (rowNumber <= 11)
                    {
                        _logger.LogDebug("CSV行{LineNumber}: VoucherDate='{VoucherDate}', JobDate='{JobDate}', VoucherNumber='{VoucherNumber}'", 
                            rowNumber, record.VoucherDate, record.JobDate, record.VoucherNumber);
                    }
                    
                    // 最初の10件は詳細ログ出力（文字化け調査用）
                    if (rowNumber <= 11)
                    {
                        _logger.LogInformation("CSV行{Line}: 伝票番号='{VoucherNumber}', 得意先コード='{CustomerCode}', 得意先名='{CustomerName}', 商品コード='{ProductCode}', 商品名='{ProductName}'",
                            rowNumber, record.VoucherNumber, record.CustomerCode, record.CustomerName, record.ProductCode, record.ProductName);
                        
                        // 文字化け調査用: バイト表現を確認
                        if (!string.IsNullOrEmpty(record.CustomerName))
                        {
                            _logger.LogDebug("得意先名バイト列: {Bytes}", BitConverter.ToString(Encoding.UTF8.GetBytes(record.CustomerName)));
                        }
                        if (!string.IsNullOrEmpty(record.ShippingMarkName))
                        {
                            _logger.LogDebug("荷印名バイト列: {Bytes}", BitConverter.ToString(Encoding.UTF8.GetBytes(record.ShippingMarkName)));
                        }
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
            CreatedAt = dataSet.CreatedAt,
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
    public DateTime CreatedAt { get; set; }
    public List<object> ImportedData { get; set; } = new();
}