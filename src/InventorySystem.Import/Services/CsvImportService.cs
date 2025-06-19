using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Interfaces;
using InventorySystem.Import.Models;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services;

public class CsvImportService
{
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly ILogger<CsvImportService> _logger;

    public CsvImportService(
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        ILogger<CsvImportService> logger)
    {
        _salesVoucherRepository = salesVoucherRepository ?? throw new ArgumentNullException(nameof(salesVoucherRepository));
        _purchaseVoucherRepository = purchaseVoucherRepository ?? throw new ArgumentNullException(nameof(purchaseVoucherRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(bool Success, int ProcessedRecords, string? ErrorMessage)> ImportSalesDataAsync(
        string filePath, 
        string dataSetId)
    {
        try
        {
            _logger.LogInformation("Starting sales data import from {FilePath}. DataSetId: {DataSetId}", filePath, dataSetId);
            
            if (!File.Exists(filePath))
            {
                var errorMsg = $"File not found: {filePath}";
                _logger.LogError(errorMsg);
                return (false, 0, errorMsg);
            }

            var salesVouchers = new List<SalesVoucherCsv>();
            
            // CSV読み込み設定 (日本語対応)
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false, // ヘッダーなしと仮定
                Encoding = Encoding.UTF8,
                BadDataFound = null, // 不正データを無視
                MissingFieldFound = null // 欠損フィールドを無視
            };

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);
            
            try
            {
                salesVouchers = csv.GetRecords<SalesVoucherCsv>().ToList();
            }
            catch (Exception csvEx)
            {
                var errorMsg = $"CSV parsing error: {csvEx.Message}";
                _logger.LogError(csvEx, errorMsg);
                return (false, 0, errorMsg);
            }

            if (!salesVouchers.Any())
            {
                _logger.LogWarning("No data found in CSV file: {FilePath}", filePath);
                return (true, 0, null);
            }

            // バリデーション
            var validationErrors = ValidateSalesData(salesVouchers);
            if (validationErrors.Any())
            {
                var errorMsg = string.Join("; ", validationErrors);
                _logger.LogError("Validation errors: {Errors}", errorMsg);
                return (false, 0, errorMsg);
            }

            // エンティティに変換して一括登録
            var entities = salesVouchers.Select(csv => csv.ToEntity(dataSetId)).ToList();
            var result = await _salesVoucherRepository.BulkInsertAsync(entities);

            _logger.LogInformation("Successfully imported {Count} sales records from {FilePath}", 
                result, filePath);
            
            return (true, result, null);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error importing sales data: {ex.Message}";
            _logger.LogError(ex, errorMsg);
            return (false, 0, errorMsg);
        }
    }

    public async Task<(bool Success, int ProcessedRecords, string? ErrorMessage)> ImportPurchaseDataAsync(
        string filePath, 
        string dataSetId)
    {
        try
        {
            _logger.LogInformation("Starting purchase data import from {FilePath}. DataSetId: {DataSetId}", filePath, dataSetId);
            
            if (!File.Exists(filePath))
            {
                var errorMsg = $"File not found: {filePath}";
                _logger.LogError(errorMsg);
                return (false, 0, errorMsg);
            }

            var purchaseVouchers = new List<PurchaseVoucherCsv>();
            
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                Encoding = Encoding.UTF8,
                BadDataFound = null,
                MissingFieldFound = null
            };

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);
            
            try
            {
                purchaseVouchers = csv.GetRecords<PurchaseVoucherCsv>().ToList();
            }
            catch (Exception csvEx)
            {
                var errorMsg = $"CSV parsing error: {csvEx.Message}";
                _logger.LogError(csvEx, errorMsg);
                return (false, 0, errorMsg);
            }

            if (!purchaseVouchers.Any())
            {
                _logger.LogWarning("No data found in CSV file: {FilePath}", filePath);
                return (true, 0, null);
            }

            // バリデーション
            var validationErrors = ValidatePurchaseData(purchaseVouchers);
            if (validationErrors.Any())
            {
                var errorMsg = string.Join("; ", validationErrors);
                _logger.LogError("Validation errors: {Errors}", errorMsg);
                return (false, 0, errorMsg);
            }

            // エンティティに変換して一括登録
            var entities = purchaseVouchers.Select(csv => csv.ToEntity(dataSetId)).ToList();
            var result = await _purchaseVoucherRepository.BulkInsertAsync(entities);

            _logger.LogInformation("Successfully imported {Count} purchase records from {FilePath}", 
                result, filePath);
            
            return (true, result, null);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error importing purchase data: {ex.Message}";
            _logger.LogError(ex, errorMsg);
            return (false, 0, errorMsg);
        }
    }

    public async Task<bool> ProcessFileWatcherAsync(
        string watchFolder, 
        string processedFolder, 
        string errorFolder,
        string filePattern = "*.csv")
    {
        try
        {
            _logger.LogInformation("Starting file watcher processing in {WatchFolder}", watchFolder);
            
            if (!Directory.Exists(watchFolder))
            {
                _logger.LogWarning("Watch folder does not exist: {WatchFolder}", watchFolder);
                return false;
            }

            // 処理済みフォルダとエラーフォルダを作成
            Directory.CreateDirectory(processedFolder);
            Directory.CreateDirectory(errorFolder);

            var files = Directory.GetFiles(watchFolder, filePattern);
            
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var dataSetId = $"AUTO_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileNameWithoutExtension(fileName)}";
                
                try
                {
                    _logger.LogInformation("Processing file: {FileName}", fileName);
                    
                    // ファイル名による判定 (例では簡略化)
                    bool success;
                    int processedRecords;
                    string? errorMessage;
                    
                    if (fileName.ToLower().Contains("sales") || fileName.ToLower().Contains("売上"))
                    {
                        (success, processedRecords, errorMessage) = await ImportSalesDataAsync(file, dataSetId);
                    }
                    else if (fileName.ToLower().Contains("purchase") || fileName.ToLower().Contains("仕入"))
                    {
                        (success, processedRecords, errorMessage) = await ImportPurchaseDataAsync(file, dataSetId);
                    }
                    else
                    {
                        _logger.LogWarning("Unknown file type: {FileName}. Skipping.", fileName);
                        continue;
                    }

                    // ファイル移動
                    var targetFolder = success ? processedFolder : errorFolder;
                    var targetPath = Path.Combine(targetFolder, $"{DateTime.Now:yyyyMMdd_HHmmss}_{fileName}");
                    
                    File.Move(file, targetPath);
                    
                    if (success)
                    {
                        _logger.LogInformation("Successfully processed {FileName}. Records: {Records}", 
                            fileName, processedRecords);
                    }
                    else
                    {
                        _logger.LogError("Failed to process {FileName}. Error: {Error}", 
                            fileName, errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file: {FileName}", fileName);
                    
                    // エラーフォルダに移動
                    var errorPath = Path.Combine(errorFolder, $"ERROR_{DateTime.Now:yyyyMMdd_HHmmss}_{fileName}");
                    File.Move(file, errorPath);
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in file watcher processing");
            return false;
        }
    }

    private static List<string> ValidateSalesData(List<SalesVoucherCsv> salesVouchers)
    {
        var errors = new List<string>();
        
        for (int i = 0; i < salesVouchers.Count; i++)
        {
            var sales = salesVouchers[i];
            var rowNum = i + 1;
            
            if (string.IsNullOrWhiteSpace(sales.VoucherNumber))
                errors.Add($"Row {rowNum}: Invalid VoucherNumber");
                
            if (string.IsNullOrWhiteSpace(sales.ProductCode))
                errors.Add($"Row {rowNum}: ProductCode is required");
                
            if (sales.Quantity <= 0)
                errors.Add($"Row {rowNum}: Quantity must be greater than 0");
                
            if (sales.UnitPrice < 0)
                errors.Add($"Row {rowNum}: UnitPrice cannot be negative");
        }
        
        return errors;
    }

    private static List<string> ValidatePurchaseData(List<PurchaseVoucherCsv> purchaseVouchers)
    {
        var errors = new List<string>();
        
        for (int i = 0; i < purchaseVouchers.Count; i++)
        {
            var purchase = purchaseVouchers[i];
            var rowNum = i + 1;
            
            if (string.IsNullOrWhiteSpace(purchase.VoucherNumber))
                errors.Add($"Row {rowNum}: Invalid VoucherNumber");
                
            if (string.IsNullOrWhiteSpace(purchase.ProductCode))
                errors.Add($"Row {rowNum}: ProductCode is required");
                
            if (purchase.Quantity <= 0)
                errors.Add($"Row {rowNum}: Quantity must be greater than 0");
                
            if (purchase.UnitPrice < 0)
                errors.Add($"Row {rowNum}: UnitPrice cannot be negative");
        }
        
        return errors;
    }
}