using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 荷印マスタCSV取込サービス
/// </summary>
public class ShippingMarkMasterImportService : IShippingMarkMasterImportService
{
    private readonly ILogger<ShippingMarkMasterImportService> _logger;
    private readonly IShippingMarkMasterRepository _repository;

    public ShippingMarkMasterImportService(
        ILogger<ShippingMarkMasterImportService> logger,
        IShippingMarkMasterRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <summary>
    /// CSVファイルから荷印マスタデータを取込む
    /// </summary>
    public async Task<InventorySystem.Core.Interfaces.ImportResult> ImportAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSVファイルが見つかりません: {filePath}");
        }

        var importedCount = 0;
        var errorCount = 0;
        var errorMessages = new List<string>();

        _logger.LogInformation("荷印マスタCSV取込開始: {FilePath}", filePath);

        try
        {
            // CSV読み込み処理
            var records = await ReadCsvFileAsync(filePath);
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (!record.IsValid())
                    {
                        _logger.LogWarning("行 {Index}: 無効なレコード - 荷印コード: {Code}, 荷印名: {Name}", 
                            index, record.ShippingMarkCode, record.ManualShippingMark);
                        errorCount++;
                        continue;
                    }

                    // エンティティに変換
                    var shippingMark = new ShippingMarkMaster
                    {
                        ShippingMarkCode = record.ShippingMarkCode!,
                        ManualShippingMark = record.ManualShippingMark!,
                        SearchKana = record.SearchKana,
                        NumericValue1 = record.NumericValue1,
                        NumericValue2 = record.NumericValue2,
                        NumericValue3 = record.NumericValue3,
                        NumericValue4 = record.NumericValue4,
                        NumericValue5 = record.NumericValue5,
                        DateValue1 = record.DateValue1,
                        DateValue2 = record.DateValue2,
                        DateValue3 = record.DateValue3,
                        DateValue4 = record.DateValue4,
                        DateValue5 = record.DateValue5,
                        TextValue1 = record.TextValue1,
                        TextValue2 = record.TextValue2,
                        TextValue3 = record.TextValue3,
                        TextValue4 = record.TextValue4,
                        TextValue5 = record.TextValue5
                    };

                    // データベースに保存
                    await _repository.UpsertAsync(shippingMark);
                    
                    importedCount++;
                    _logger.LogDebug("行 {Index}: 荷印マスタ登録完了 - {Code}: {Name}", 
                        index, record.ShippingMarkCode, record.ManualShippingMark);
                }
                catch (Exception ex)
                {
                    var errorMessage = $"行 {index}: データ処理エラー - {ex.Message}";
                    _logger.LogError(ex, errorMessage);
                    errorMessages.Add(errorMessage);
                    errorCount++;
                }
            }

            var result = new InventorySystem.Core.Interfaces.ImportResult
            {
                ImportedCount = importedCount,
                ErrorCount = errorCount,
                IsSuccess = errorCount == 0,
                Message = $"荷印マスタ取込完了: 成功 {importedCount}件, エラー {errorCount}件",
                Errors = errorMessages
            };

            _logger.LogInformation(result.Message);
            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"荷印マスタCSV取込エラー: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            
            return new InventorySystem.Core.Interfaces.ImportResult
            {
                ImportedCount = 0,
                ErrorCount = 1,
                IsSuccess = false,
                Message = errorMessage,
                Errors = new List<string> { ex.ToString() }
            };
        }
    }

    /// <summary>
    /// CSVファイルを読み込む
    /// </summary>
    private async Task<List<ShippingMarkMasterCsv>> ReadCsvFileAsync(string filePath)
    {
        var records = new List<ShippingMarkMasterCsv>();

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

        await foreach (var record in csv.GetRecordsAsync<ShippingMarkMasterCsv>())
        {
            if (record != null)
            {
                records.Add(record);
            }
        }

        return records;
    }
}