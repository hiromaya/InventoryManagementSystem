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
/// 得意先マスタCSV取込サービス
/// </summary>
public class CustomerMasterImportService
{
    private readonly ICustomerMasterRepository _customerMasterRepository;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly ILogger<CustomerMasterImportService> _logger;

    public CustomerMasterImportService(
        ICustomerMasterRepository customerMasterRepository,
        IDataSetRepository dataSetRepository,
        ILogger<CustomerMasterImportService> logger)
    {
        _customerMasterRepository = customerMasterRepository;
        _dataSetRepository = dataSetRepository;
        _logger = logger;
    }

    /// <summary>
    /// CSVファイルから得意先マスタデータを取込む
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

        _logger.LogInformation("得意先マスタCSV取込開始: {FilePath}, DataSetId: {DataSetId}", 
            filePath, dataSetId);

        try
        {
            // データセット作成
            var dataSet = new DataSet
            {
                Id = dataSetId,
                Name = $"得意先マスタ取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                Description = $"得意先マスタCSV取込: {Path.GetFileName(filePath)}",
                ProcessType = "CustomerMaster",
                Status = DataSetStatus.Processing,
                RecordCount = 0,
                FilePath = filePath,
                JobDate = importDate,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            
            await _dataSetRepository.CreateAsync(dataSet);

            // CSV読み込み処理
            var customers = new List<CustomerMaster>();
            var records = await ReadCsvFileAsync(filePath);
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            // 既存データをクリア（全件入れ替え）
            await _customerMasterRepository.DeleteAllAsync();

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(record.CustomerCode))
                    {
                        var error = $"行{index}: 得意先コードが空です";
                        errorMessages.Add(error);
                        _logger.LogWarning(error);
                        continue;
                    }

                    var customer = ConvertToEntity(record);
                    customers.Add(customer);
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
            if (customers.Any())
            {
                await _customerMasterRepository.InsertBulkAsync(customers);
                _logger.LogInformation("得意先マスタ保存完了: {Count}件", customers.Count);
            }

            // データセットステータス更新
            await _dataSetRepository.UpdateRecordCountAsync(dataSetId, importedCount);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.PartialSuccess, errorMessage);
                _logger.LogWarning("得意先マスタCSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    importedCount, errorMessages.Count);
            }
            else
            {
                await _dataSetRepository.UpdateStatusAsync(dataSetId, DataSetStatus.Completed);
                _logger.LogInformation("得意先マスタCSV取込完了: {Count}件", importedCount);
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
            _logger.LogError(ex, "得意先マスタCSV取込エラー: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// CSVファイルを読み込む
    /// </summary>
    private async Task<List<CustomerMasterCsv>> ReadCsvFileAsync(string filePath)
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

        var records = new List<CustomerMasterCsv>();
        
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
                var record = csv.GetRecord<CustomerMasterCsv>();
                if (record != null)
                {
                    // 最初の数件のみ詳細ログ
                    if (rowNumber <= 5)
                    {
                        _logger.LogDebug("行{Row}: コード={Code}, 名称={Name}", 
                            rowNumber, record.CustomerCode, record.CustomerName);
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
    private CustomerMaster ConvertToEntity(CustomerMasterCsv csv)
    {
        return new CustomerMaster
        {
            CustomerCode = csv.CustomerCode?.Trim() ?? string.Empty,
            CustomerName = csv.CustomerName?.Trim() ?? string.Empty,
            CustomerName2 = csv.CustomerName2?.Trim(),
            SearchKana = csv.SearchKana?.Trim(),
            ShortName = csv.ShortName?.Trim(),
            PostalCode = csv.PostalCode?.Trim(),
            Address1 = csv.Address1?.Trim(),
            Address2 = csv.Address2?.Trim(),
            Address3 = csv.Address3?.Trim(),
            PhoneNumber = csv.PhoneNumber?.Trim(),
            FaxNumber = csv.FaxNumber?.Trim(),
            CustomerCategory1 = csv.CustomerCategory1?.Trim(),
            CustomerCategory2 = csv.CustomerCategory2?.Trim(),
            CustomerCategory3 = csv.CustomerCategory3?.Trim(),
            CustomerCategory4 = csv.CustomerCategory4?.Trim(),
            CustomerCategory5 = csv.CustomerCategory5?.Trim(),
            WalkingRate = csv.WalkingRate,
            BillingCode = csv.BillingCode?.Trim(),
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
        return $"CUSTMST_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
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