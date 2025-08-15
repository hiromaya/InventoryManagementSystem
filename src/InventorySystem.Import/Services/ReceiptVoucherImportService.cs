using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;
using InventorySystem.Import.Models.Csv;
using InventorySystem.Import.Helpers;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace InventorySystem.Import.Services;

/// <summary>
/// 入金伝票CSV取込サービス
/// </summary>
public class ReceiptVoucherImportService : IImportService
{
    private readonly IReceiptVoucherRepository _repository;
    private readonly IDataSetService _dataSetService;
    private readonly ILogger<ReceiptVoucherImportService> _logger;

    public string ServiceName => "入金伝票インポート";
    public int ProcessOrder => 40;

    public ReceiptVoucherImportService(
        IReceiptVoucherRepository repository,
        IDataSetService dataSetService,
        ILogger<ReceiptVoucherImportService> logger)
    {
        _repository = repository;
        _dataSetService = dataSetService;
        _logger = logger;
    }

    public bool CanHandle(string fileName)
    {
        return fileName.StartsWith("入金伝票") && fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
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

        _logger.LogInformation("入金伝票CSV取込開始: {FilePath}", filePath);

        try
        {
            // データセット作成
            var dataSetId = await _dataSetService.CreateDataSetAsync(
                $"入金伝票取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                "RECEIPT",
                importDate,
                $"入金伝票CSVファイル取込: {Path.GetFileName(filePath)}",
                filePath);

            // CSV読み込み処理
            var receiptVouchers = new List<ReceiptVoucher>();
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

                    var receiptVoucher = ConvertToEntity(record, dataSetId, importDate);
                    receiptVouchers.Add(receiptVoucher);
                    importedCount++;

                    _logger.LogDebug("行 {Index}: 入金伝票データ変換完了 - 伝票番号: {VoucherNumber}", 
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
            if (receiptVouchers.Any())
            {
                var insertedCount = await _repository.InsertBulkAsync(receiptVouchers);
                _logger.LogInformation("入金伝票データベース保存完了: {Count}件", insertedCount);
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

            _logger.LogInformation("入金伝票取込完了: 成功 {ImportedCount}件, エラー {ErrorCount}件", importedCount, errorCount);
            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"入金伝票CSV取込エラー: {ex.Message}";
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
    private async Task<List<ReceiptVoucherCsv>> ReadCsvFileAsync(string filePath)
    {
        var records = new List<ReceiptVoucherCsv>();

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

        await foreach (var record in csv.GetRecordsAsync<ReceiptVoucherCsv>())
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
    private static bool IsValidRecord(ReceiptVoucherCsv record)
    {
        return !string.IsNullOrWhiteSpace(record.VoucherNumber) &&
               !string.IsNullOrWhiteSpace(record.CustomerCode) &&
               !string.IsNullOrWhiteSpace(record.VoucherDate);
    }

    /// <summary>
    /// CSVレコードをエンティティに変換
    /// </summary>
    private static ReceiptVoucher ConvertToEntity(ReceiptVoucherCsv record, string dataSetId, DateTime importDate)
    {
        return new ReceiptVoucher
        {
            DataSetId = dataSetId,
            VoucherNumber = record.VoucherNumber,
            CustomerCode = CodeFormatter.FormatTo5Digits(record.CustomerCode),
            CustomerName = record.CustomerName ?? "",
            BillingCode = record.BillingCode ?? "",
            VoucherDate = DateTime.TryParseExact(record.VoucherDate, "yyyyMMdd", null, DateTimeStyles.None, out var vDate) ? vDate : importDate,
            JobDate = DateTime.TryParseExact(record.JobDate, "yyyyMMdd", null, DateTimeStyles.None, out var jDate) ? jDate : importDate,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }
}