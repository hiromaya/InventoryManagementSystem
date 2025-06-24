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

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// 仕入先マスタCSV取込サービス
/// </summary>
public class SupplierMasterImportService
{
    private readonly ISupplierMasterRepository _supplierMasterRepository;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly ILogger<SupplierMasterImportService> _logger;

    public SupplierMasterImportService(
        ISupplierMasterRepository supplierMasterRepository,
        IDataSetRepository dataSetRepository,
        ILogger<SupplierMasterImportService> logger)
    {
        _supplierMasterRepository = supplierMasterRepository;
        _dataSetRepository = dataSetRepository;
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
            // データセット作成
            var dataSet = new DataSet
            {
                Id = dataSetId,
                DataSetType = "SupplierMaster",
                ImportedAt = DateTime.Now,
                RecordCount = 0,
                Status = DataSetStatus.Processing,
                FilePath = filePath,
                JobDate = importDate
            };
            
            await _dataSetRepository.CreateAsync(dataSet);

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

            // データセットステータス更新
            await _dataSetRepository.UpdateRecordCountAsync(dataSetId, importedCount);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.PartialSuccess, errorMessage);
                _logger.LogWarning("仕入先マスタCSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    importedCount, errorMessages.Count);
            }
            else
            {
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Completed);
                _logger.LogInformation("仕入先マスタCSV取込完了: {Count}件", importedCount);
            }

            return new ImportResult
            {
                DataSetId = dataSetId,
                Status = errorMessages.Any() ? DataSetStatus.PartialSuccess : DataSetStatus.Completed,
                ImportedCount = importedCount,
                ErrorMessage = errorMessages.Any() ? string.Join("\n", errorMessages) : null,
                FilePath = filePath,
                ImportedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Failed, ex.Message);
            _logger.LogError(ex, "仕入先マスタCSV取込エラー: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// CSVファイルを読み込む
    /// </summary>
    private async Task<List<SupplierMasterCsv>> ReadCsvFileAsync(string filePath)
    {
        var encoding = DetectFileEncoding(filePath);
        _logger.LogInformation("CSVファイル読み込み開始: {FilePath}, エンコーディング: {Encoding}", filePath, encoding.EncodingName);
        
        using var reader = new StreamReader(filePath, encoding);
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
    /// ファイルのエンコーディングを自動判定
    /// </summary>
    private static Encoding DetectFileEncoding(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        
        // BOM付きUTF-8
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;
        
        // BOM付きUTF-16 LE
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;
        
        // BOM付きUTF-16 BE
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;
        
        // BOMなしの場合、Shift-JISとして扱う（日本語Windows環境のデフォルト）
        return Encoding.GetEncoding("Shift_JIS");
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
            SupplierCategory1 = csv.SupplierCategory1?.Trim(),
            SupplierCategory2 = csv.SupplierCategory2?.Trim(),
            SupplierCategory3 = csv.SupplierCategory3?.Trim(),
            PaymentCode = csv.PaymentCode?.Trim(),
            IsActive = csv.IsActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// データセットIDを生成
    /// </summary>
    private static string GenerateDataSetId()
    {
        return $"SUPMST_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
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

        return new ImportResult
        {
            DataSetId = dataSetId,
            Status = dataSet.Status,
            ImportedCount = dataSet.RecordCount,
            ErrorMessage = dataSet.ErrorMessage,
            FilePath = dataSet.FilePath,
            ImportedAt = dataSet.ImportedAt
        };
    }
}