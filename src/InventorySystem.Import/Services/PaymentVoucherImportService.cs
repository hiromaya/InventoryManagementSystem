using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;
using InventorySystem.Import.Models.Csv;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace InventorySystem.Import.Services;

/// <summary>
/// 支払伝票CSV取込サービス
/// </summary>
public class PaymentVoucherImportService : IImportService
{
    private readonly IPaymentVoucherRepository _repository;
    private readonly IDataSetService _dataSetService;
    private readonly ILogger<PaymentVoucherImportService> _logger;

    public string ServiceName => "支払伝票インポート";
    public int ProcessOrder => 41;

    public PaymentVoucherImportService(
        IPaymentVoucherRepository repository,
        IDataSetService dataSetService,
        ILogger<PaymentVoucherImportService> logger)
    {
        _repository = repository;
        _dataSetService = dataSetService;
        _logger = logger;
    }

    public bool CanHandle(string fileName)
    {
        return fileName.StartsWith("支払伝票") && fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ImportResult> ImportAsync(string filePath, DateTime importDate)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSVファイルが見つかりません: {filePath}");
        }

        var importedCount = 0;
        var errorCount = 0;
        var errorMessages = new List<string>();

        _logger.LogInformation("支払伝票CSV取込開始: {FilePath}", filePath);

        try
        {
            // データセット作成
            var dataSetInfo = new UnifiedDataSetInfo
            {
                ProcessType = "PAYMENT",
                ImportType = "IMPORT", 
                Name = $"支払伝票取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                Description = $"支払伝票CSVファイル取込: {Path.GetFileName(filePath)}",
                JobDate = importDate,
                FilePath = filePath,
                CreatedBy = "payment-import"
            };

            var dataSetId = await _dataSetService.CreateDataSetAsync(dataSetInfo);

            // CSV読み込み処理
            var paymentVouchers = new List<PaymentVoucher>();
            var records = await ReadCsvFileAsync(filePath);
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (!IsValidRecord(record))
                    {
                        _logger.LogWarning("行 {Index}: 無効なレコードをスキップ - 伝票番号: {VoucherNumber}", 
                            index, record.VoucherNumber);
                        errorCount++;
                        continue;
                    }

                    var paymentVoucher = ConvertToEntity(record, dataSetId, importDate);
                    paymentVouchers.Add(paymentVoucher);
                    importedCount++;

                    _logger.LogDebug("行 {Index}: 支払伝票データ変換完了 - 伝票番号: {VoucherNumber}", 
                        index, record.VoucherNumber);
                }
                catch (Exception ex)
                {
                    var errorMessage = $"行 {index}: データ処理エラー - {ex.Message}";
                    _logger.LogError(ex, errorMessage);
                    errorMessages.Add(errorMessage);
                    errorCount++;
                }
            }

            // データベースに一括挿入
            if (paymentVouchers.Any())
            {
                var insertedCount = await _repository.InsertBulkAsync(paymentVouchers);
                _logger.LogInformation("支払伝票データベース保存完了: {Count}件", insertedCount);
            }

            var result = new ImportResult
            {
                DataSetId = dataSetId,
                Status = errorCount == 0 ? "Completed" : "Failed",
                ImportedCount = importedCount,
                ErrorMessage = errorCount > 0 ? string.Join("; ", errorMessages) : null,
                FilePath = filePath,
                CreatedAt = DateTime.Now
            };

            _logger.LogInformation("支払伝票取込完了: 成功 {ImportedCount}件, エラー {ErrorCount}件", importedCount, errorCount);
            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"支払伝票CSV取込エラー: {ex.Message}";
            _logger.LogError(ex, errorMessage);

            return new ImportResult
            {
                DataSetId = "",
                Status = "Failed",
                ImportedCount = 0,
                ErrorMessage = errorMessage,
                FilePath = filePath,
                CreatedAt = DateTime.Now
            };
        }
    }

    /// <summary>
    /// CSVファイルを読み込む
    /// </summary>
    private async Task<List<PaymentVoucherCsv>> ReadCsvFileAsync(string filePath)
    {
        var records = new List<PaymentVoucherCsv>();

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

        await foreach (var record in csv.GetRecordsAsync<PaymentVoucherCsv>())
        {
            if (record != null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    /// <summary>
    /// レコードの妥当性を検証
    /// </summary>
    private static bool IsValidRecord(PaymentVoucherCsv record)
    {
        return !string.IsNullOrWhiteSpace(record.VoucherNumber) &&
               !string.IsNullOrWhiteSpace(record.SupplierCode) &&
               !string.IsNullOrWhiteSpace(record.VoucherDate);
    }

    /// <summary>
    /// CSVレコードをエンティティに変換
    /// </summary>
    private static PaymentVoucher ConvertToEntity(PaymentVoucherCsv record, string dataSetId, DateTime importDate)
    {
        return new PaymentVoucher
        {
            DataSetId = dataSetId,
            VoucherNumber = record.VoucherNumber,
            SupplierCode = record.SupplierCode,
            SupplierName = record.SupplierName ?? "",
            // BillingCode property is not available for PaymentVoucher
            VoucherDate = DateTime.TryParseExact(record.VoucherDate, "yyyyMMdd", null, DateTimeStyles.None, out var vDate) ? vDate : importDate,
            JobDate = DateTime.TryParseExact(record.JobDate, "yyyyMMdd", null, DateTimeStyles.None, out var jDate) ? jDate : importDate,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }
}