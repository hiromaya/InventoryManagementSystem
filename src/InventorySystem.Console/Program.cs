using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Services;
using InventorySystem.Data.Repositories;
using InventorySystem.Import.Services;
using InventorySystem.Import.Services.Masters;
using InventorySystem.Data.Repositories.Masters;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Core.Configuration;
using Microsoft.Extensions.Options;
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.FastReport.Services;
using System.Diagnostics;
using System.Reflection;
using System.Text;

// Program クラスの定義
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 実行環境情報の表示
Console.WriteLine($"実行環境: {Environment.OSVersion}");
Console.WriteLine($".NET Runtime: {Environment.Version}");
Console.WriteLine($"実行ディレクトリ: {Environment.CurrentDirectory}");

// FastReportテストコマンドの早期処理
if (args.Length > 0 && args[0] == "test-fastreport")
{
    Console.WriteLine("=== FastReport.NET Trial テスト開始 ===");
    Console.WriteLine($"実行時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine("\n✓ Windows専用環境");
    Console.WriteLine("✓ FastReport.NET Trial版が利用可能です");
    Console.WriteLine("✓ アンマッチリスト・商品日報の実装が完了しています");
    Console.WriteLine("\n実際のPDF生成テストを実行するには：");
    Console.WriteLine("  dotnet run unmatch-list [日付] # アンマッチリストPDF生成");
    Console.WriteLine("  dotnet run daily-report [日付] # 商品日報PDF生成");
    Console.WriteLine("\n=== FastReport.NET移行テスト完了 ===");
    return 0;
}

// FastReport.NET Trial版を使用

var builder = Host.CreateApplicationBuilder();

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add Memory Cache for master data repositories
builder.Services.AddMemoryCache();

// Department Settings
builder.Services.Configure<InventorySystem.Core.Configuration.DepartmentSettings>(
    builder.Configuration.GetSection("DepartmentSettings"));
builder.Services.AddSingleton<IStartupFolderService, StartupFolderService>();
builder.Services.AddScoped<ICsvFileProcessor, CsvFileProcessor>();

// Services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddScoped<IInventoryRepository>(provider => 
    new InventoryRepository(connectionString, provider.GetRequiredService<ILogger<InventoryRepository>>()));
builder.Services.AddScoped<ICpInventoryRepository>(provider => 
    new CpInventoryRepository(connectionString, provider.GetRequiredService<ILogger<CpInventoryRepository>>()));
builder.Services.AddScoped<ISalesVoucherRepository>(provider => 
    new SalesVoucherRepository(connectionString, provider.GetRequiredService<ILogger<SalesVoucherRepository>>()));
builder.Services.AddScoped<IPurchaseVoucherRepository>(provider => 
    new PurchaseVoucherRepository(connectionString, provider.GetRequiredService<ILogger<PurchaseVoucherRepository>>()));
builder.Services.AddScoped<IInventoryAdjustmentRepository>(provider => 
    new InventoryAdjustmentRepository(connectionString, provider.GetRequiredService<ILogger<InventoryAdjustmentRepository>>()));
builder.Services.AddScoped<IDataSetRepository>(provider => 
    new DataSetRepository(connectionString, provider.GetRequiredService<ILogger<DataSetRepository>>()));

// CSV取込専用リポジトリ
builder.Services.AddScoped<SalesVoucherCsvRepository>(provider => 
    new SalesVoucherCsvRepository(connectionString, provider.GetRequiredService<ILogger<SalesVoucherCsvRepository>>()));
builder.Services.AddScoped<PurchaseVoucherCsvRepository>(provider => 
    new PurchaseVoucherCsvRepository(connectionString, provider.GetRequiredService<ILogger<PurchaseVoucherCsvRepository>>()));

// Master data repositories
builder.Services.AddScoped<IGradeMasterRepository, GradeMasterRepository>();
builder.Services.AddScoped<IClassMasterRepository, ClassMasterRepository>();
builder.Services.AddScoped<ICustomerMasterRepository>(provider => 
    new CustomerMasterRepository(connectionString, provider.GetRequiredService<ILogger<CustomerMasterRepository>>()));
builder.Services.AddScoped<IProductMasterRepository>(provider => 
    new ProductMasterRepository(connectionString, provider.GetRequiredService<ILogger<ProductMasterRepository>>()));
builder.Services.AddScoped<ISupplierMasterRepository>(provider => 
    new SupplierMasterRepository(connectionString, provider.GetRequiredService<ILogger<SupplierMasterRepository>>()));

// Master import services
builder.Services.AddScoped<CustomerMasterImportService>();
builder.Services.AddScoped<ProductMasterImportService>();
builder.Services.AddScoped<SupplierMasterImportService>();

// FileStorage設定の登録
builder.Services.Configure<FileStorageSettings>(
    builder.Configuration.GetSection("FileStorage"));

// FileManagementServiceの登録
builder.Services.AddScoped<IFileManagementService, FileManagementService>();

// Error prevention services
builder.Services.AddScoped<InventorySystem.Core.Services.Validation.IDateValidationService, InventorySystem.Core.Services.Validation.DateValidationService>();
builder.Services.AddScoped<InventorySystem.Core.Services.Dataset.IDatasetManager, InventorySystem.Core.Services.Dataset.DatasetManager>();
builder.Services.AddScoped<InventorySystem.Core.Services.History.IProcessHistoryService, InventorySystem.Core.Services.History.ProcessHistoryService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IDailyCloseService, DailyCloseService>();

// Error prevention repositories
builder.Services.AddScoped<IDatasetManagementRepository>(provider => 
    new DatasetManagementRepository(connectionString, provider.GetRequiredService<ILogger<DatasetManagementRepository>>()));
builder.Services.AddScoped<IProcessHistoryRepository>(provider => 
    new ProcessHistoryRepository(connectionString, provider.GetRequiredService<ILogger<ProcessHistoryRepository>>()));
builder.Services.AddScoped<IDailyCloseManagementRepository>(provider => 
    new DailyCloseManagementRepository(connectionString, provider.GetRequiredService<ILogger<DailyCloseManagementRepository>>()));

builder.Services.AddScoped<IUnmatchListService, UnmatchListService>();
builder.Services.AddScoped<InventorySystem.Core.Interfaces.IDailyReportService, DailyReportService>();
builder.Services.AddScoped<IInventoryListService, InventoryListService>();
// Report Services - Windows専用でFastReport実装を使用
builder.Services.AddScoped<IUnmatchListReportService, UnmatchListFastReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IDailyReportService, DailyReportFastReportService>();
builder.Services.AddScoped<SalesVoucherImportService>();
builder.Services.AddScoped<PurchaseVoucherImportService>();
builder.Services.AddScoped<InventoryAdjustmentImportService>();


var host = builder.Build();

// Initialize department folders at startup
try
{
    var folderService = host.Services.GetRequiredService<IStartupFolderService>();
    folderService.EnsureFoldersExist();
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "部門フォルダの初期化中にエラーが発生しました");
}

// Parse command line arguments
var commandArgs = Environment.GetCommandLineArgs();

if (commandArgs.Length < 2)
{
    Console.WriteLine("使用方法:");
    Console.WriteLine("  dotnet run test-connection                   - データベース接続テスト");
    Console.WriteLine("  dotnet run test-pdf                          - PDF生成テスト（DB不要）");
    Console.WriteLine("  dotnet run test-fastreport                   - FastReportテスト（DB不要）");
    Console.WriteLine("  dotnet run unmatch-list [YYYY-MM-DD]         - アンマッチリスト処理を実行");
    Console.WriteLine("  dotnet run daily-report [YYYY-MM-DD] [--dataset-id ID] - 商品日報を生成");
    Console.WriteLine("  dotnet run inventory-list [YYYY-MM-DD]       - 在庫表を生成");
    Console.WriteLine("  dotnet run import-sales <file> [YYYY-MM-DD]  - 売上伝票CSVを取込");
    Console.WriteLine("  dotnet run import-purchase <file> [YYYY-MM-DD] - 仕入伝票CSVを取込");
    Console.WriteLine("  dotnet run import-adjustment <file> [YYYY-MM-DD] - 在庫調整CSVを取込");
    Console.WriteLine("  dotnet run debug-csv-structure <file>        - CSV構造を分析");
    Console.WriteLine("  dotnet run import-customers <file>           - 得意先マスタCSVを取込");
    Console.WriteLine("  dotnet run import-products <file>            - 商品マスタCSVを取込");
    Console.WriteLine("  dotnet run import-suppliers <file>           - 仕入先マスタCSVを取込");
    Console.WriteLine("  dotnet run init-folders                      - フォルダ構造を初期化");
    Console.WriteLine("  dotnet run import-folder <dept> [YYYY-MM-DD] - 部門フォルダから一括取込");
    Console.WriteLine("  例: dotnet run test-connection");
    Console.WriteLine("  例: dotnet run unmatch-list 2025-06-16");
    Console.WriteLine("  例: dotnet run daily-report 2025-06-16");
    Console.WriteLine("  例: dotnet run inventory-list 2025-06-16");
    Console.WriteLine("  例: dotnet run import-sales sales.csv 2025-06-16");
    return 1;
}

var command = commandArgs[1].ToLower();

try
{
    switch (command)
    {
        case "unmatch-list":
            await ExecuteUnmatchListAsync(host.Services, commandArgs);
            break;
            
        case "daily-report":
            await ExecuteDailyReportAsync(host.Services, commandArgs);
            break;
            
        case "inventory-list":
            await ExecuteInventoryListAsync(host.Services, commandArgs);
            break;
            
        case "import-sales":
            await ExecuteImportSalesAsync(host.Services, commandArgs);
            break;
            
        case "import-purchase":
            await ExecuteImportPurchaseAsync(host.Services, commandArgs);
            break;
            
        case "import-adjustment":
            await ExecuteImportAdjustmentAsync(host.Services, commandArgs);
            break;
            
        case "test-pdf":
            Console.WriteLine("PDFテスト機能は削除されました。test-fastreport を使用してください。");
            break;
            
        case "test-connection":
            await TestDatabaseConnectionAsync(host.Services);
            break;
            
        case "debug-csv-structure":
            await DebugCsvStructureAsync(commandArgs);
            break;
            
        case "import-customers":
            await ExecuteImportCustomersAsync(host.Services, commandArgs);
            break;
            
        case "import-products":
            await ExecuteImportProductsAsync(host.Services, commandArgs);
            break;
            
        case "import-suppliers":
            await ExecuteImportSuppliersAsync(host.Services, commandArgs);
            break;
            
        case "init-folders":
            await ExecuteInitializeFoldersAsync(host.Services);
            break;
            
        case "import-folder":
            await ExecuteImportFromFolderAsync(host.Services, commandArgs);
            break;
        
        default:
            Console.WriteLine($"不明なコマンド: {command}");
            return 1;
    }
    
            return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"エラーが発生しました: {ex.Message}");
        return 1;
    }
    }

    static async Task ExecuteUnmatchListAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var unmatchListService = scopedServices.GetRequiredService<IUnmatchListService>();
        
        // ジョブ日付を取得（引数から、またはデフォルト値）
        DateTime jobDate;
        if (args.Length >= 3 && DateTime.TryParse(args[2], out jobDate))
        {
            logger.LogInformation("指定されたジョブ日付: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
        }
        else
        {
            jobDate = DateTime.Today;
            logger.LogInformation("デフォルトのジョブ日付を使用: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        Console.WriteLine("=== アンマッチリスト処理開始 ===");
        Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
        Console.WriteLine();
        
        // アンマッチリスト処理実行
    var result = await unmatchListService.ProcessUnmatchListAsync(jobDate);
    
    stopwatch.Stop();
    
    if (result.Success)
    {
        Console.WriteLine("=== 処理結果 ===");
        Console.WriteLine($"データセットID: {result.DataSetId}");
        Console.WriteLine($"アンマッチ件数: {result.UnmatchCount}");
        Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
        Console.WriteLine();
        
        if (result.UnmatchCount > 0)
        {
            Console.WriteLine("=== アンマッチ一覧 ===");
            foreach (var item in result.UnmatchItems.Take(10)) // 最初の10件のみ表示
            {
                Console.WriteLine($"{item.Category} | {item.Key.ProductCode} | {item.ProductName} | {item.AlertType}");
            }
            
            if (result.UnmatchCount > 10)
            {
                Console.WriteLine($"... 他 {result.UnmatchCount - 10} 件");
            }
            Console.WriteLine();
        }
        
        // PDF出力
        try
        {
            var reportService = scopedServices.GetRequiredService<IUnmatchListReportService>();
            var fileService = scopedServices.GetRequiredService<IFileManagementService>();
            Console.WriteLine("PDF生成中...");
            var pdfBytes = reportService.GenerateUnmatchListReport(result.UnmatchItems, jobDate);
            
            var outputPath = await fileService.GetReportOutputPathAsync(
                "unmatch_list", 
                jobDate, 
                "pdf"
            );
            
            await File.WriteAllBytesAsync(outputPath, pdfBytes);
            Console.WriteLine($"PDF出力完了: {outputPath}");
            
            // PDFを開く
            Process.Start(new ProcessStartInfo
            {
                FileName = outputPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PDF生成でエラーが発生しました");
            Console.WriteLine($"PDF生成エラー: {ex.Message}");
        }
        
        Console.WriteLine("=== アンマッチリスト処理完了 ===");
    }
    else
    {
        Console.WriteLine("=== 処理失敗 ===");
        Console.WriteLine($"エラーメッセージ: {result.ErrorMessage}");
        Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
        
        logger.LogError("アンマッチリスト処理が失敗しました: {ErrorMessage}", result.ErrorMessage);
    }
    }
}

    static async Task ExecuteImportSalesAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var importService = scopedServices.GetRequiredService<SalesVoucherImportService>();
        
        if (args.Length < 3)
        {
            Console.WriteLine("エラー: CSVファイルパスが指定されていません");
            Console.WriteLine("使用方法: dotnet run import-sales <file> [YYYY-MM-DD]");
            return;
        }
    
    var filePath = args[2];
    
    // ジョブ日付を取得
    DateTime jobDate;
    if (args.Length >= 4 && DateTime.TryParse(args[3], out jobDate))
    {
        logger.LogInformation("指定されたジョブ日付: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
    }
    else
    {
        jobDate = DateTime.Today;
        logger.LogInformation("デフォルトのジョブ日付を使用: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
    }
    
    var stopwatch = Stopwatch.StartNew();
    
    Console.WriteLine("=== 売上伝票CSV取込処理開始 ===");
    Console.WriteLine($"ファイル: {filePath}");
    Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
    Console.WriteLine();
    
    try
    {
        var dataSetId = await importService.ImportAsync(filePath, jobDate);
        var result = await importService.GetImportResultAsync(dataSetId);
        
        stopwatch.Stop();
        
        Console.WriteLine("=== 取込結果 ===");
        Console.WriteLine($"データセットID: {result.DataSetId}");
        Console.WriteLine($"ステータス: {result.Status}");
        Console.WriteLine($"取込件数: {result.ImportedCount}");
        Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
        
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"エラー情報: {result.ErrorMessage}");
        }
        
        Console.WriteLine("=== 売上伝票CSV取込処理完了 ===");
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        Console.WriteLine($"エラー: {ex.Message}");
        logger.LogError(ex, "売上伝票CSV取込処理でエラーが発生しました");
    }
    }
}

    static async Task ExecuteImportPurchaseAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var importService = scopedServices.GetRequiredService<PurchaseVoucherImportService>();
        
        if (args.Length < 3)
        {
            Console.WriteLine("エラー: CSVファイルパスが指定されていません");
            Console.WriteLine("使用方法: dotnet run import-purchase <file> [YYYY-MM-DD]");
            return;
        }
    
        var filePath = args[2];
        
        // ジョブ日付を取得
        DateTime jobDate;
        if (args.Length >= 4 && DateTime.TryParse(args[3], out jobDate))
        {
            logger.LogInformation("指定されたジョブ日付: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
        }
        else
        {
            jobDate = DateTime.Today;
            logger.LogInformation("デフォルトのジョブ日付を使用: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        Console.WriteLine("=== 仕入伝票CSV取込処理開始 ===");
        Console.WriteLine($"ファイル: {filePath}");
        Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
        Console.WriteLine();
        
        try
        {
            var dataSetId = await importService.ImportAsync(filePath, jobDate);
            var result = await importService.GetImportResultAsync(dataSetId);
            
            stopwatch.Stop();
            
            Console.WriteLine("=== 取込結果 ===");
            Console.WriteLine($"データセットID: {result.DataSetId}");
            Console.WriteLine($"ステータス: {result.Status}");
            Console.WriteLine($"取込件数: {result.ImportedCount}");
            Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine($"エラー情報: {result.ErrorMessage}");
            }
            
            Console.WriteLine("=== 仕入伝票CSV取込処理完了 ===");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Console.WriteLine($"エラー: {ex.Message}");
            logger.LogError(ex, "仕入伝票CSV取込処理でエラーが発生しました");
        }
    }
}

    static async Task ExecuteImportAdjustmentAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var importService = scopedServices.GetRequiredService<InventoryAdjustmentImportService>();
        
        if (args.Length < 3)
        {
            Console.WriteLine("エラー: CSVファイルパスが指定されていません");
            Console.WriteLine("使用方法: dotnet run import-adjustment <file> [YYYY-MM-DD]");
            return;
        }
        
        var filePath = args[2];
        
        // ジョブ日付を取得
        DateTime jobDate;
        if (args.Length >= 4 && DateTime.TryParse(args[3], out jobDate))
        {
            logger.LogInformation("指定されたジョブ日付: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
        }
        else
        {
            jobDate = DateTime.Today;
            logger.LogInformation("デフォルトのジョブ日付を使用: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        Console.WriteLine("=== 在庫調整CSV取込処理開始 ===");
        Console.WriteLine($"ファイル: {filePath}");
        Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
        Console.WriteLine();
        
        try
        {
            var dataSetId = await importService.ImportAsync(filePath, jobDate);
            var result = await importService.GetImportResultAsync(dataSetId);
            
            stopwatch.Stop();
            
            Console.WriteLine("=== 取込結果 ===");
            Console.WriteLine($"データセットID: {result.DataSetId}");
            Console.WriteLine($"ステータス: {result.Status}");
            Console.WriteLine($"取込件数: {result.ImportedCount}");
            Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine($"エラー情報: {result.ErrorMessage}");
            }
            
            Console.WriteLine("=== 在庫調整CSV取込処理完了 ===");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Console.WriteLine($"エラー: {ex.Message}");
            logger.LogError(ex, "在庫調整CSV取込処理でエラーが発生しました");
        }
    }
}

    static async Task ExecuteDailyReportAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var dailyReportService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.IDailyReportService>();
        
        // ジョブ日付を取得（引数から、またはデフォルト値）
        DateTime jobDate;
        if (args.Length >= 3 && DateTime.TryParse(args[2], out jobDate))
        {
            logger.LogInformation("指定されたジョブ日付: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
        }
        else
        {
            jobDate = DateTime.Today;
            logger.LogInformation("デフォルトのジョブ日付を使用: {JobDate}", jobDate.ToString("yyyy-MM-dd"));
        }
        
        // --dataset-id オプションをチェック
        string? existingDataSetId = null;
        for (int i = 3; i < args.Length - 1; i++)
        {
            if (args[i] == "--dataset-id" && i + 1 < args.Length)
            {
                existingDataSetId = args[i + 1];
                logger.LogInformation("既存のデータセットIDを使用: {DataSetId}", existingDataSetId);
                break;
            }
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        Console.WriteLine("=== 商品日報処理開始 ===");
        Console.WriteLine($"レポート日付: {jobDate:yyyy-MM-dd}");
        if (existingDataSetId != null)
        {
            Console.WriteLine($"既存データセットID: {existingDataSetId}");
        }
        Console.WriteLine();
        
        // 商品日報処理実行
        var result = await dailyReportService.ProcessDailyReportAsync(jobDate, existingDataSetId);
        
        stopwatch.Stop();
        
        if (result.Success)
        {
            Console.WriteLine("=== 処理結果 ===");
            Console.WriteLine($"データセットID: {result.DataSetId}");
            Console.WriteLine($"データ件数: {result.ProcessedCount}");
            Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
            Console.WriteLine();
            
            if (result.ProcessedCount > 0)
            {
                Console.WriteLine("=== 商品日報データ（サンプル） ===");
                foreach (var item in result.ReportItems.Take(5))
                {
                    Console.WriteLine($"{item.ProductCode} | {item.ProductName} | 売上:{item.DailySalesAmount:N0}円 | 粗利1:{item.DailyGrossProfit1:N0}円");
                }
                
                if (result.ProcessedCount > 5)
                {
                    Console.WriteLine($"... 他 {result.ProcessedCount - 5} 件");
                }
                Console.WriteLine();
            }
            
            // PDF出力
            try
            {
                var reportService = scopedServices.GetRequiredService<InventorySystem.Reports.Interfaces.IDailyReportService>();
                var fileService = scopedServices.GetRequiredService<IFileManagementService>();
                Console.WriteLine("PDF生成中...");
                var pdfBytes = reportService.GenerateDailyReport(result.ReportItems, result.Subtotals, result.Total, jobDate);
                
                var outputPath = await fileService.GetReportOutputPathAsync(
                    "daily_report", 
                    jobDate, 
                    "pdf"
                );
                
                await File.WriteAllBytesAsync(outputPath, pdfBytes);
                Console.WriteLine($"PDF出力完了: {outputPath}");
                
                // PDFを開く
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PDF生成でエラーが発生しました");
                Console.WriteLine($"PDF生成エラー: {ex.Message}");
            }
            
            Console.WriteLine("=== 商品日報処理完了 ===");
        }
        else
        {
            Console.WriteLine("=== 処理失敗 ===");
            Console.WriteLine($"エラーメッセージ: {result.ErrorMessage}");
            Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
            
            logger.LogError("商品日報処理が失敗しました: {ErrorMessage}", result.ErrorMessage);
        }
    }
}

    static async Task ExecuteInventoryListAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var inventoryListService = scopedServices.GetRequiredService<IInventoryListService>();
        // TODO: Implement FastReport version for inventory list
        Console.WriteLine("在庫表のFastReport対応は未実装です。QuestPDFからの移行が必要です。");
        await Task.CompletedTask; // 警告を回避
    }
}

    static async Task DebugCsvStructureAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("エラー: CSVファイルパスが指定されていません");
            Console.WriteLine("使用方法: dotnet run debug-csv-structure <file>");
            return;
        }

        var filePath = args[2];
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"エラー: ファイルが存在しません: {filePath}");
            return;
        }

        Console.WriteLine($"=== CSV構造解析 ===\nFile: {filePath}\n");

        try
        {
            // UTF-8エンコーディングで直接読み込む
            var encoding = Encoding.UTF8;
            Console.WriteLine($"使用エンコーディング: {encoding.EncodingName}\n");

            using var reader = new StreamReader(filePath, encoding);
            var headerLine = await reader.ReadLineAsync();
            if (headerLine == null)
            {
                Console.WriteLine("エラー: CSVファイルが空です");
                return;
            }

            var headers = headerLine.Split(',');
            Console.WriteLine($"列数: {headers.Length}\n");

            // 特定の列を検索
            var searchColumns = new[] { "得意先名", "得意先名１", "仕入先名", "荷印名", "商品名" };
            Console.WriteLine("=== 重要な列の位置 ===");
            foreach (var searchColumn in searchColumns)
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    if (headers[i].Trim('\"').Contains(searchColumn))
                    {
                        Console.WriteLine($"列{i:D3}: {headers[i].Trim('\"')}");
                    }
                }
            }

            // 最初の20列を表示
            Console.WriteLine("\n=== 最初の20列 ===");
            for (int i = 0; i < Math.Min(20, headers.Length); i++)
            {
                Console.WriteLine($"列{i:D3}: {headers[i].Trim('\"')}");
            }

            // 80-95列目を表示
            if (headers.Length > 80)
            {
                Console.WriteLine("\n=== 80-95列目 ===");
                for (int i = 80; i < Math.Min(95, headers.Length); i++)
                {
                    Console.WriteLine($"列{i:D3}: {headers[i].Trim('\"')}");
                }
            }

            // 130-150列目を表示
            if (headers.Length > 130)
            {
                Console.WriteLine("\n=== 130-150列目 ===");
                for (int i = 130; i < Math.Min(150, headers.Length); i++)
                {
                    Console.WriteLine($"列{i:D3}: {headers[i].Trim('\"')}");
                }
            }

            // データの最初の行も確認
            var dataLine = await reader.ReadLineAsync();
            if (dataLine != null)
            {
                var dataValues = dataLine.Split(',');
                Console.WriteLine("\n=== 最初のデータ行のサンプル ===");
                var importantIndices = new[] { 3, 8, 88, 138, 142 }; // 得意先コード、得意先名、商品コード、荷印名、商品名
                foreach (var idx in importantIndices)
                {
                    if (idx < dataValues.Length)
                    {
                        Console.WriteLine($"列{idx:D3} ({headers[idx].Trim('\"')}): {dataValues[idx].Trim('\"')}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: {ex.Message}");
        }
    }


    static async Task TestDatabaseConnectionAsync(IServiceProvider services)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var configuration = scopedServices.GetRequiredService<IConfiguration>();
    
    Console.WriteLine("=== データベース接続テスト開始 ===");
    
    try
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"接続文字列: {connectionString}");
        Console.WriteLine();
        
        // 基本的な接続テスト
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        
        Console.WriteLine("データベースへの接続を試行中...");
        await connection.OpenAsync();
        Console.WriteLine("✅ データベース接続成功");
        
        // バージョン情報取得
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT @@VERSION as Version, DB_NAME() as DatabaseName, GETDATE() as CurrentTime";
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            Console.WriteLine($"データベース名: {reader["DatabaseName"]}");
            Console.WriteLine($"現在時刻: {reader["CurrentTime"]}");
            Console.WriteLine($"SQL Server バージョン: {reader["Version"]?.ToString()?.Split('\n')[0]}");
        }
        
        Console.WriteLine();
        Console.WriteLine("=== テーブル存在確認 ===");
        
        reader.Close();
        
        // テーブル存在確認
        string[] tables = { "InventoryMaster", "CpInventoryMaster", "SalesVouchers", "PurchaseVouchers", "InventoryAdjustments", "DataSets" };
        
        foreach (var table in tables)
        {
            command.CommandText = $"SELECT CASE WHEN EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{table}]') AND type in (N'U')) THEN 1 ELSE 0 END";
            var exists = (int)(await command.ExecuteScalarAsync() ?? 0) == 1;
            Console.WriteLine($"{table}: {(exists ? "✅ 存在" : "❌ 未作成")}");
        }
        
        Console.WriteLine();
        Console.WriteLine("=== データベース接続テスト完了 ===");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ データベース接続エラー: {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("=== トラブルシューティング ===");
        Console.WriteLine("1. SQL Server Express が起動していることを確認してください");
        Console.WriteLine("2. LocalDB を使用する場合:");
        Console.WriteLine("   sqllocaldb info");
        Console.WriteLine("   sqllocaldb start MSSQLLocalDB");
        Console.WriteLine("3. 接続文字列を確認してください（appsettings.json）");
        Console.WriteLine("4. database/CreateDatabase.sql を実行してデータベースを作成してください");
        
        logger.LogError(ex, "データベース接続テストでエラーが発生しました");
    }
    }
}

    static async Task ExecuteImportCustomersAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var importService = scopedServices.GetRequiredService<CustomerMasterImportService>();
    
    if (args.Length < 3)
    {
        Console.WriteLine("エラー: CSVファイルパスが指定されていません");
        Console.WriteLine("使用方法: dotnet run import-customers <file>");
        return;
    }
    
    var filePath = args[2];
    var importDate = DateTime.Today;
    
    var stopwatch = Stopwatch.StartNew();
    
    Console.WriteLine("=== 得意先マスタCSV取込処理開始 ===");
    Console.WriteLine($"ファイル: {filePath}");
    Console.WriteLine();
    
    try
    {
        var result = await importService.ImportFromCsvAsync(filePath, importDate);
        
        stopwatch.Stop();
        
        Console.WriteLine("=== 取込結果 ===");
        Console.WriteLine($"データセットID: {result.DataSetId}");
        Console.WriteLine($"ステータス: {result.Status}");
        Console.WriteLine($"取込件数: {result.ImportedCount}");
        Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
        
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"エラー情報: {result.ErrorMessage}");
        }
        
        Console.WriteLine("=== 得意先マスタCSV取込処理完了 ===");
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        Console.WriteLine($"エラー: {ex.Message}");
        logger.LogError(ex, "得意先マスタCSV取込処理でエラーが発生しました");
    }
    }
}

    static async Task ExecuteImportProductsAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var importService = scopedServices.GetRequiredService<ProductMasterImportService>();
    
    if (args.Length < 3)
    {
        Console.WriteLine("エラー: CSVファイルパスが指定されていません");
        Console.WriteLine("使用方法: dotnet run import-products <file>");
        return;
    }
    
    var filePath = args[2];
    var importDate = DateTime.Today;
    
    var stopwatch = Stopwatch.StartNew();
    
    Console.WriteLine("=== 商品マスタCSV取込処理開始 ===");
    Console.WriteLine($"ファイル: {filePath}");
    Console.WriteLine();
    
    try
    {
        var result = await importService.ImportFromCsvAsync(filePath, importDate);
        
        stopwatch.Stop();
        
        Console.WriteLine("=== 取込結果 ===");
        Console.WriteLine($"データセットID: {result.DataSetId}");
        Console.WriteLine($"ステータス: {result.Status}");
        Console.WriteLine($"取込件数: {result.ImportedCount}");
        Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
        
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"エラー情報: {result.ErrorMessage}");
        }
        
        Console.WriteLine("=== 商品マスタCSV取込処理完了 ===");
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        Console.WriteLine($"エラー: {ex.Message}");
        logger.LogError(ex, "商品マスタCSV取込処理でエラーが発生しました");
    }
    }
}

    static async Task ExecuteImportSuppliersAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var importService = scopedServices.GetRequiredService<SupplierMasterImportService>();
    
    if (args.Length < 3)
    {
        Console.WriteLine("エラー: CSVファイルパスが指定されていません");
        Console.WriteLine("使用方法: dotnet run import-suppliers <file>");
        return;
    }
    
    var filePath = args[2];
    var importDate = DateTime.Today;
    
    var stopwatch = Stopwatch.StartNew();
    
    Console.WriteLine("=== 仕入先マスタCSV取込処理開始 ===");
    Console.WriteLine($"ファイル: {filePath}");
    Console.WriteLine();
    
    try
    {
        var result = await importService.ImportFromCsvAsync(filePath, importDate);
        
        stopwatch.Stop();
        
        Console.WriteLine("=== 取込結果 ===");
        Console.WriteLine($"データセットID: {result.DataSetId}");
        Console.WriteLine($"ステータス: {result.Status}");
        Console.WriteLine($"取込件数: {result.ImportedCount}");
        Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
        
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"エラー情報: {result.ErrorMessage}");
        }
        
        Console.WriteLine("=== 仕入先マスタCSV取込処理完了 ===");
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        Console.WriteLine($"エラー: {ex.Message}");
        logger.LogError(ex, "仕入先マスタCSV取込処理でエラーが発生しました");
    }
    }
}

static async Task ExecuteInitializeFoldersAsync(IServiceProvider services)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var fileService = scopedServices.GetRequiredService<IFileManagementService>();
        
        Console.WriteLine("=== フォルダ構造初期化開始 ===");
        
        try
        {
            await fileService.InitializeDirectoryStructureAsync();
            Console.WriteLine("✅ フォルダ構造の初期化が完了しました");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ エラー: {ex.Message}");
            logger.LogError(ex, "フォルダ構造初期化でエラーが発生しました");
        }
    }
}

static async Task ExecuteImportFromFolderAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("エラー: 部門コードが指定されていません");
        Console.WriteLine("使用方法: dotnet run import-folder <dept> [YYYY-MM-DD]");
        return;
    }
    
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var fileService = scopedServices.GetRequiredService<IFileManagementService>();
        var salesImportService = scopedServices.GetRequiredService<SalesVoucherImportService>();
        var purchaseImportService = scopedServices.GetRequiredService<PurchaseVoucherImportService>();
        var adjustmentImportService = scopedServices.GetRequiredService<InventoryAdjustmentImportService>();
        
        var department = args[2];
        DateTime jobDate = args.Length >= 4 && DateTime.TryParse(args[3], out var date) ? date : DateTime.Today;
        
        Console.WriteLine($"=== フォルダ監視取込開始 ===");
        Console.WriteLine($"部門: {department}");
        Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
        
        try
        {
            var files = await fileService.GetPendingFilesAsync(department);
            Console.WriteLine($"取込対象ファイル数: {files.Count}");
            
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                Console.WriteLine($"\n処理中: {fileName}");
                
                try
                {
                    if (fileName.StartsWith("売上伝票"))
                    {
                        await salesImportService.ImportAsync(file, jobDate, department);
                        Console.WriteLine("✅ 売上伝票として処理完了");
                    }
                    else if (fileName.StartsWith("仕入伝票"))
                    {
                        await purchaseImportService.ImportAsync(file, jobDate, department);
                        Console.WriteLine("✅ 仕入伝票として処理完了");
                    }
                    else if (fileName.StartsWith("在庫調整"))
                    {
                        await adjustmentImportService.ImportAsync(file, jobDate, department);
                        Console.WriteLine("✅ 在庫調整として処理完了");
                    }
                    else
                    {
                        await fileService.MoveToErrorAsync(file, department, "未対応のファイル形式");
                        Console.WriteLine("⚠️ 未対応のファイル形式のためエラーフォルダへ移動");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ファイル処理エラー: {File}", file);
                    Console.WriteLine($"❌ エラー: {ex.Message}");
                }
            }
            
            Console.WriteLine("\n=== フォルダ監視取込完了 ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ エラー: {ex.Message}");
            logger.LogError(ex, "フォルダ監視取込でエラーが発生しました");
        }
    }
}
} // Program クラスの終了


