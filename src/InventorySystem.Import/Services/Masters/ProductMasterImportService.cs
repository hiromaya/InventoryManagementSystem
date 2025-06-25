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
/// 商品マスタCSV取込サービス
/// </summary>
public class ProductMasterImportService
{
    private readonly IProductMasterRepository _productMasterRepository;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly ILogger<ProductMasterImportService> _logger;

    public ProductMasterImportService(
        IProductMasterRepository productMasterRepository,
        IDataSetRepository dataSetRepository,
        ILogger<ProductMasterImportService> logger)
    {
        _productMasterRepository = productMasterRepository;
        _dataSetRepository = dataSetRepository;
        _logger = logger;
    }

    /// <summary>
    /// CSVファイルから商品マスタデータを取込む
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

        _logger.LogInformation("商品マスタCSV取込開始: {FilePath}, DataSetId: {DataSetId}", 
            filePath, dataSetId);

        try
        {
            // データセット作成
            var dataSet = new DataSet
            {
                Id = dataSetId,
                Name = $"商品マスタ取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                Description = $"商品マスタCSV取込: {Path.GetFileName(filePath)}",
                ProcessType = "ProductMaster",
                Status = DataSetStatus.Processing,
                RecordCount = 0,
                FilePath = filePath,
                JobDate = importDate,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now
            };
            
            await _dataSetRepository.CreateAsync(dataSet);

            // CSV読み込み処理
            var products = new List<ProductMaster>();
            var records = await ReadCsvFileAsync(filePath);
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            // 既存データをクリア（全件入れ替え）
            await _productMasterRepository.DeleteAllAsync();

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(record.ProductCode))
                    {
                        var error = $"行{index}: 商品コードが空です";
                        errorMessages.Add(error);
                        _logger.LogWarning(error);
                        continue;
                    }

                    var product = ConvertToEntity(record);
                    products.Add(product);
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
            if (products.Any())
            {
                await _productMasterRepository.InsertBulkAsync(products);
                _logger.LogInformation("商品マスタ保存完了: {Count}件", products.Count);
            }

            // データセットステータス更新
            await _dataSetRepository.UpdateRecordCountAsync(dataSetId, importedCount);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.PartialSuccess, errorMessage);
                _logger.LogWarning("商品マスタCSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    importedCount, errorMessages.Count);
            }
            else
            {
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Completed);
                _logger.LogInformation("商品マスタCSV取込完了: {Count}件", importedCount);
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
            _logger.LogError(ex, "商品マスタCSV取込エラー: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// CSVファイルを読み込む
    /// </summary>
    private async Task<List<ProductMasterCsv>> ReadCsvFileAsync(string filePath)
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

        var records = new List<ProductMasterCsv>();
        
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
                var record = csv.GetRecord<ProductMasterCsv>();
                if (record != null)
                {
                    // 最初の数件のみ詳細ログ
                    if (rowNumber <= 5)
                    {
                        _logger.LogDebug("行{Row}: コード={Code}, 名称={Name}", 
                            rowNumber, record.ProductCode, record.ProductName);
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
    private ProductMaster ConvertToEntity(ProductMasterCsv csv)
    {
        return new ProductMaster
        {
            ProductCode = csv.ProductCode?.Trim() ?? string.Empty,
            ProductName = csv.ProductName?.Trim() ?? string.Empty,
            ProductName2 = csv.ProductName2?.Trim(),
            ProductName3 = csv.ProductName3?.Trim(),
            ProductName4 = csv.ProductName4?.Trim(),
            ProductName5 = csv.ProductName5?.Trim(),
            SearchKana = csv.SearchKana?.Trim(),
            ShortName = csv.ShortName?.Trim(),
            PrintCode = csv.PrintCode?.Trim(),
            ProductCategory1 = csv.ProductCategory1?.Trim(),
            ProductCategory2 = csv.ProductCategory2?.Trim(),
            ProductCategory3 = csv.ProductCategory3?.Trim(),
            ProductCategory4 = csv.ProductCategory4?.Trim(),
            ProductCategory5 = csv.ProductCategory5?.Trim(),
            UnitCode = csv.UnitCode?.Trim(),
            CaseUnitCode = csv.CaseUnitCode?.Trim(),
            Case2UnitCode = csv.Case2UnitCode?.Trim(),
            CaseQuantity = csv.CaseQuantity,
            Case2Quantity = csv.Case2Quantity,
            StandardPrice = csv.StandardPrice,
            CaseStandardPrice = csv.CaseStandardPrice,
            IsStockManaged = csv.IsStockManaged,
            TaxRate = csv.GetTaxRate(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// データセットIDを生成
    /// </summary>
    private static string GenerateDataSetId()
    {
        return $"PRODMST_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
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