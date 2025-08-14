using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Import.Models.Masters;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using InventorySystem.Core.Models;
// using DataSetStatus = InventorySystem.Core.Interfaces.DataSetStatus; // 削除済み

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 仕入先マスタCSV取込サービス
/// </summary>
public class SupplierMasterImportService
{
    private readonly ISupplierMasterRepository _supplierMasterRepository;
    private readonly IDataSetManagementRepository _dataSetManagementRepository;
    private readonly IDataSetService _unifiedDataSetService;
    private readonly ILogger<SupplierMasterImportService> _logger;

    public SupplierMasterImportService(
        ISupplierMasterRepository supplierMasterRepository,
        IDataSetManagementRepository dataSetManagementRepository,
        IDataSetService unifiedDataSetService,
        ILogger<SupplierMasterImportService> logger)
    {
        _supplierMasterRepository = supplierMasterRepository;
        _dataSetManagementRepository = dataSetManagementRepository;
        _unifiedDataSetService = unifiedDataSetService;
        _logger = logger;
    }

    /// <summary>
    /// CSVファイルから仕入先マスタデータを取込む
    /// </summary>
    public async Task<ImportResult> ImportFromCsvAsync(string filePath, DateTime importDate)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSVファイルが見つかりません: {filePath}");
        }

        var dataSetId = GenerateDataSetId();
        var importedCount = 0;
        var errorMessages = new List<string>();

        _logger.LogInformation("仕入先マスタCSV取込開始: {FilePath}, DataSetId: {DataSetId}", 
            filePath, dataSetId);

        try
        {
            // 統一データセット作成
            dataSetId = await _unifiedDataSetService.CreateDataSetAsync(
                $"仕入先マスタ取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                "SUPPLIER",
                importDate,
                $"仕入先マスタCSVファイル取込: {Path.GetFileName(filePath)}",
                filePath);

            // CSV読み込み処理
            var suppliers = new List<SupplierMaster>();
            var records = await ReadCsvFileAsync(filePath);
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            // 既存データをクリア（全件入れ替え）
            await _supplierMasterRepository.DeleteAllAsync();

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(record.SupplierCode))
                    {
                        var error = $"行{index}: 仕入先コードが空です";
                        errorMessages.Add(error);
                        _logger.LogWarning(error);
                        continue;
                    }

                    var supplier = ConvertToEntity(record);
                    suppliers.Add(supplier);
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
            if (suppliers.Any())
            {
                await _supplierMasterRepository.InsertBulkAsync(suppliers);
                _logger.LogInformation("仕入先マスタ保存完了: {Count}件", suppliers.Count);
            }

            // データセットレコード数更新
            await _unifiedDataSetService.UpdateRecordCountAsync(dataSetId, importedCount);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _unifiedDataSetService.SetErrorAsync(dataSetId, errorMessage);
                _logger.LogWarning("仕入先マスタCSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    importedCount, errorMessages.Count);
            }
            else
            {
                await _unifiedDataSetService.UpdateStatusAsync(dataSetId, "Completed");
                _logger.LogInformation("仕入先マスタCSV取込完了: {Count}件", importedCount);
            }

            return new ImportResult
            {
                DataSetId = dataSetId,
                Status = errorMessages.Any() ? "Failed" : "Completed",
                ImportedCount = importedCount,
                ErrorMessage = errorMessages.Any() ? string.Join("\n", errorMessages) : null,
                FilePath = filePath,
                CreatedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            await _unifiedDataSetService.SetErrorAsync(dataSetId, ex.Message);
            _logger.LogError(ex, "仕入先マスタCSV取込エラー: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// CSVファイルを読み込む
    /// </summary>
    private async Task<List<SupplierMasterCsv>> ReadCsvFileAsync(string filePath)
    {
        // UTF-8エンコーディングで直接読み込む
        _logger.LogInformation("UTF-8エンコーディングでCSVファイルを読み込みます: {FilePath}", filePath);
        
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = context => 
            {
                _logger.LogWarning($"不正なデータ: 行 {context.Context?.Parser?.Row ?? 0}, フィールド {context.Field ?? "不明"}");
            },
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        });

        var records = new List<SupplierMasterCsv>();
        
        await csv.ReadAsync();
        csv.ReadHeader();
        _logger.LogInformation("ヘッダー読み込み完了");
        _logger.LogInformation("データ読み込み開始");
        
        var rowNumber = 0;
        while (await csv.ReadAsync())
        {
            rowNumber++;
            try
            {
                var record = csv.GetRecord<SupplierMasterCsv>();
                if (record != null)
                {
                    // 最初の数件のみ詳細ログ
                    if (rowNumber <= 5)
                    {
                        _logger.LogDebug("行{Row}: コード={Code}, 名称={Name}", 
                            rowNumber, record.SupplierCode, record.SupplierName);
                    }
                    records.Add(record);
                }
            }
            catch (CsvHelper.TypeConversion.TypeConverterException ex)
            {
                _logger.LogError($"データ型変換エラー - 行: {csv.Context.Parser?.Row ?? 0}");
                _logger.LogError($"値: '{ex.Text}' を変換できません");
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"行 {csv.Context.Parser?.Row ?? 0} の読み込みでエラー: {ex.Message}");
                continue;
            }
        }
        
        return records;
    }


    /// <summary>
    /// CSVレコードをEntityに変換
    /// </summary>
    private SupplierMaster ConvertToEntity(SupplierMasterCsv csv)
    {
        return new SupplierMaster
        {
            SupplierCode = csv.SupplierCode?.Trim() ?? string.Empty,
            SupplierName = csv.SupplierName?.Trim() ?? string.Empty,
            SupplierName2 = csv.SupplierName2?.Trim(),
            SearchKana = csv.SearchKana?.Trim(),
            ShortName = csv.ShortName?.Trim(),
            PostalCode = csv.PostalCode?.Trim(),
            Address1 = csv.Address1?.Trim(),
            Address2 = csv.Address2?.Trim(),
            Address3 = csv.Address3?.Trim(),
            PhoneNumber = csv.PhoneNumber?.Trim(),
            FaxNumber = csv.FaxNumber?.Trim(),
            SupplierCategory1 = FormatCategoryCode(csv.SupplierCategory1),
            SupplierCategory2 = FormatCategoryCode(csv.SupplierCategory2),
            SupplierCategory3 = FormatCategoryCode(csv.SupplierCategory3),
            PaymentCode = csv.PaymentCode?.Trim(),
            IsActive = csv.IsActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 分類コードを3桁の0埋め文字列に変換
    /// </summary>
    /// <param name="categoryValue">CSV から読み込んだ分類コード値</param>
    /// <returns>3桁フォーマット済みの分類コード、またはnull</returns>
    private string? FormatCategoryCode(string? categoryValue)
    {
        // nullまたは空文字の場合はnullを返す
        if (string.IsNullOrWhiteSpace(categoryValue))
            return null;

        var valueStr = categoryValue.Trim();
        
        // 数値として解析を試みる
        if (int.TryParse(valueStr, out int code))
        {
            // 範囲チェック（1～999まで対応）
            if (code < 0 || code > 999)
            {
                _logger.LogWarning("分類コードが範囲外です: {Code}", code);
                return valueStr; // 元の値をそのまま返す
            }
            
            // 3桁の0埋め形式に変換（例: 1 → "001", 35 → "035"）
            return code.ToString("D3");
        }
        
        // 数値でない場合は元の値をそのまま返す
        _logger.LogWarning("分類コードが数値ではありません: {ValueStr}", valueStr);
        return valueStr;
    }

    /// <summary>
    /// データセットIDを生成
    /// </summary>
    private static string GenerateDataSetId()
    {
        // GUIDの最初の8文字のみ使用
        var guid = Guid.NewGuid().ToString("N");
        return $"SUPMST_{DateTime.Now:yyyyMMdd_HHmmss}_{guid.Substring(0, 8)}";
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

        return new ImportResult
        {
            DataSetId = dataSetId,
            Status = dataSetMgmt.Status,
            ImportedCount = dataSetMgmt.RecordCount,
            ErrorMessage = dataSetMgmt.ErrorMessage,
            FilePath = dataSetMgmt.FilePath,
            CreatedAt = dataSetMgmt.CreatedAt
        };
    }
}