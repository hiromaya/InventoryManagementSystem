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
using InventorySystem.Core.Entities;
using InventorySystem.Core.Models;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using InventorySystem.Core.Interfaces.Services;
#if WINDOWS
using InventorySystem.Reports.FastReport.Services;
#else
using InventorySystem.Reports.Services;
#endif
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
builder.Services.AddScoped<IShippingMarkMasterRepository>(provider => 
    new ShippingMarkMasterRepository(connectionString, provider.GetRequiredService<ILogger<ShippingMarkMasterRepository>>()));
builder.Services.AddScoped<IRegionMasterRepository>(provider => 
    new RegionMasterRepository(connectionString, provider.GetRequiredService<ILogger<RegionMasterRepository>>()));

// Master import services
builder.Services.AddScoped<CustomerMasterImportService>();
builder.Services.AddScoped<ProductMasterImportService>();
builder.Services.AddScoped<SupplierMasterImportService>();
builder.Services.AddScoped<IShippingMarkMasterImportService, ShippingMarkMasterImportService>();
builder.Services.AddScoped<IRegionMasterImportService, RegionMasterImportService>();

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
builder.Services.AddScoped<ICpInventoryCreationService, CpInventoryCreationService>();
// Report Services - プラットフォーム別実装
#if WINDOWS
builder.Services.AddScoped<IUnmatchListReportService, UnmatchListFastReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IDailyReportService, DailyReportFastReportService>();
#else
builder.Services.AddScoped<IUnmatchListReportService, PlaceholderUnmatchListReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IDailyReportService, PlaceholderDailyReportService>();
#endif
builder.Services.AddScoped<SalesVoucherImportService>();
builder.Services.AddScoped<PurchaseVoucherImportService>();
builder.Services.AddScoped<InventoryAdjustmentImportService>();
builder.Services.AddScoped<PreviousMonthInventoryImportService>();

// 在庫マスタ最適化サービス
builder.Services.AddScoped<IInventoryMasterOptimizationService, InventorySystem.Data.Services.InventoryMasterOptimizationService>();

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
    Console.WriteLine("  dotnet run import-masters                    - 等級・階級マスタをインポート");
    Console.WriteLine("  dotnet run check-masters                     - 等級・階級マスタの登録状況を確認");
    Console.WriteLine("  dotnet run init-inventory [YYYY-MM-DD]       - 在庫マスタ初期データを作成");
    Console.WriteLine("  例: dotnet run test-connection");
    Console.WriteLine("  例: dotnet run unmatch-list 2025-06-16");
    Console.WriteLine("  例: dotnet run daily-report 2025-06-16");
    Console.WriteLine("  例: dotnet run inventory-list 2025-06-16");
    Console.WriteLine("  例: dotnet run import-sales sales.csv 2025-06-16");
    Console.WriteLine("  例: dotnet run import-masters");
    Console.WriteLine("  例: dotnet run check-masters");
    Console.WriteLine("  例: dotnet run init-inventory 2025-06-16");
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
        
        case "import-masters":
            await ExecuteImportMastersAsync(host.Services);
            break;
        
        case "check-masters":
            await ExecuteCheckMastersAsync(host.Services);
            break;
        
        case "import-previous-inventory":
            await ExecuteImportPreviousInventoryAsync(host.Services, commandArgs);
            break;
        
        case "init-inventory":
            await ExecuteInitInventoryCommand(host.Services, commandArgs);
            break;
        
        case "check-daily-close":
            await ExecuteCheckDailyCloseAsync(host.Services, commandArgs);
            break;
            
        case "create-cp-inventory":
            await ExecuteCreateCpInventoryAsync(host.Services, commandArgs);
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
        var reportService = scopedServices.GetRequiredService<IUnmatchListReportService>();
        var fileManagementService = scopedServices.GetRequiredService<IFileManagementService>();
        
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
        
        // PDF出力（0件でも生成）
        try
        {
            if (result.UnmatchCount == 0)
            {
                Console.WriteLine("アンマッチ件数が0件です。0件のPDFを生成します");
            }
            
            Console.WriteLine("PDF生成中...");
            var pdfBytes = reportService.GenerateUnmatchListReport(result.UnmatchItems, jobDate);
            
            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                // FileManagementServiceを使用してレポートパスを取得
                var pdfPath = await fileManagementService.GetReportOutputPathAsync("UnmatchList", jobDate, "pdf");
                
                await File.WriteAllBytesAsync(pdfPath, pdfBytes);
                
                Console.WriteLine($"PDFファイルを保存しました: {pdfPath}");
                Console.WriteLine($"ファイルサイズ: {pdfBytes.Length / 1024.0:F2} KB");
                
                // Windows環境では自動でPDFを開く
                #if WINDOWS
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = pdfPath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                catch (Exception openEx)
                {
                    logger.LogWarning(openEx, "PDFファイルの自動表示に失敗しました");
                }
                #endif
            }
            else
            {
                Console.WriteLine("PDF生成がスキップされました（環境制限またはデータなし）");
            }
        }
        catch (Exception pdfEx)
        {
            logger.LogError(pdfEx, "PDF生成中にエラーが発生しました");
            Console.WriteLine($"PDF生成エラー: {pdfEx.Message}");
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
        var reportService = scopedServices.GetRequiredService<InventorySystem.Reports.Interfaces.IDailyReportService>();
        
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
                Console.WriteLine("PDF生成中...");
                var pdfBytes = reportService.GenerateDailyReport(result.ReportItems, result.Subtotals, result.Total, jobDate);
                
                if (pdfBytes != null && pdfBytes.Length > 0)
                {
                    // PDFファイル保存
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var fileName = $"daily_report_{jobDate:yyyyMMdd}_{timestamp}.pdf";
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                    
                    await File.WriteAllBytesAsync(filePath, pdfBytes);
                    Console.WriteLine($"PDF出力完了: {fileName}");
                    
                    // Windows環境では自動でPDFを開く
                    #if WINDOWS
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        };
                        Process.Start(startInfo);
                    }
                    catch (Exception openEx)
                    {
                        logger.LogWarning(openEx, "PDFファイルの自動表示に失敗しました");
                    }
                    #endif
                }
                else
                {
                    Console.WriteLine("PDF生成がスキップされました（環境制限またはデータなし）");
                }
            }
            catch (Exception pdfEx)
            {
                logger.LogError(pdfEx, "PDF生成中にエラーが発生しました");
                Console.WriteLine($"PDF生成エラー: {pdfEx.Message}");
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

static async Task ExecuteImportMastersAsync(IServiceProvider services)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var gradeRepo = scopedServices.GetRequiredService<IGradeMasterRepository>();
        var classRepo = scopedServices.GetRequiredService<IClassMasterRepository>();
        
        Console.WriteLine("=== マスタデータインポート開始 ===");
        Console.WriteLine();
        
        try
        {
            // 等級マスタのインポート
            Console.WriteLine("等級マスタをインポート中...");
            var gradeCount = await gradeRepo.ImportFromCsvAsync();
            Console.WriteLine($"✅ 等級マスタ: {gradeCount}件インポートしました");
            Console.WriteLine();
            
            // 階級マスタのインポート
            Console.WriteLine("階級マスタをインポート中...");
            var classCount = await classRepo.ImportFromCsvAsync();
            Console.WriteLine($"✅ 階級マスタ: {classCount}件インポートしました");
            Console.WriteLine();
            
            Console.WriteLine("=== マスタデータインポート完了 ===");
            Console.WriteLine($"合計: {gradeCount + classCount}件のレコードをインポートしました");
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"❌ エラー: {ex.Message}");
            Console.WriteLine("CSVファイルが見つかりません。以下のパスにファイルが存在することを確認してください：");
            Console.WriteLine("  - D:\\InventoryImport\\DeptA\\Import\\等級汎用マスター１.csv");
            Console.WriteLine("  - D:\\InventoryImport\\DeptA\\Import\\階級汎用マスター２.csv");
            logger.LogError(ex, "マスタデータインポートでファイルが見つかりません");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ エラー: {ex.Message}");
            logger.LogError(ex, "マスタデータインポートでエラーが発生しました");
        }
    }
}

static async Task ExecuteCheckMastersAsync(IServiceProvider services)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var gradeRepo = scopedServices.GetRequiredService<IGradeMasterRepository>();
        var classRepo = scopedServices.GetRequiredService<IClassMasterRepository>();
        
        Console.WriteLine("=== マスタデータ登録状況確認 ===");
        Console.WriteLine();
        
        try
        {
            // 等級マスタの件数を確認
            Console.WriteLine("【等級マスタ】");
            var gradeCount = await gradeRepo.GetCountAsync();
            Console.WriteLine($"  登録件数: {gradeCount:N0}件");
            
            if (gradeCount > 0)
            {
                // サンプルデータを表示
                var allGrades = await gradeRepo.GetAllGradesAsync();
                var sampleGrades = allGrades.Take(5);
                Console.WriteLine("  サンプルデータ:");
                foreach (var grade in sampleGrades)
                {
                    Console.WriteLine($"    {grade.Key}: {grade.Value}");
                }
                if (allGrades.Count > 5)
                {
                    Console.WriteLine($"    ... 他 {allGrades.Count - 5}件");
                }
            }
            else
            {
                Console.WriteLine("  ⚠️ データが登録されていません");
                Console.WriteLine("  'dotnet run import-masters' でインポートしてください");
            }
            
            Console.WriteLine();
            
            // 階級マスタの件数を確認
            Console.WriteLine("【階級マスタ】");
            var classCount = await classRepo.GetCountAsync();
            Console.WriteLine($"  登録件数: {classCount:N0}件");
            
            if (classCount > 0)
            {
                // サンプルデータを表示
                var allClasses = await classRepo.GetAllClassesAsync();
                var sampleClasses = allClasses.Take(5);
                Console.WriteLine("  サンプルデータ:");
                foreach (var cls in sampleClasses)
                {
                    Console.WriteLine($"    {cls.Key}: {cls.Value}");
                }
                if (allClasses.Count > 5)
                {
                    Console.WriteLine($"    ... 他 {allClasses.Count - 5}件");
                }
            }
            else
            {
                Console.WriteLine("  ⚠️ データが登録されていません");
                Console.WriteLine("  'dotnet run import-masters' でインポートしてください");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== 確認完了 ===");
            Console.WriteLine($"合計: {gradeCount + classCount:N0}件のマスタデータが登録されています");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ エラー: {ex.Message}");
            logger.LogError(ex, "マスタデータ確認でエラーが発生しました");
        }
    }
}

static async Task ExecuteImportPreviousInventoryAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var importService = scopedServices.GetRequiredService<PreviousMonthInventoryImportService>();
        
        try
        {
            Console.WriteLine("=== 前月末在庫インポート開始 ===");
            
            // 対象年月の取得（引数から、またはデフォルト値）
            DateTime targetDate;
            if (args.Length >= 3 && DateTime.TryParse(args[2], out targetDate))
            {
                logger.LogInformation("指定された対象日付: {TargetDate}", targetDate.ToString("yyyy-MM-dd"));
            }
            else
            {
                targetDate = DateTime.Today;
                logger.LogInformation("デフォルトの対象日付を使用: {TargetDate}", targetDate.ToString("yyyy-MM-dd"));
            }
            
            // インポート実行
            var result = await importService.ImportAsync(targetDate);
            
            // 結果表示
            Console.WriteLine($"\n処理時間: {result.Duration.TotalSeconds:F2}秒");
            Console.WriteLine($"読込件数: {result.TotalRecords:N0}件");
            Console.WriteLine($"処理件数: {result.ProcessedRecords:N0}件");
            Console.WriteLine($"エラー件数: {result.ErrorRecords:N0}件");
            
            if (result.IsSuccess)
            {
                Console.WriteLine("\n✅ 前月末在庫インポートが正常に完了しました");
            }
            else
            {
                Console.WriteLine("\n⚠️ インポートは完了しましたが、エラーが発生しました");
                if (result.Errors.Count > 0)
                {
                    Console.WriteLine("\nエラー詳細:");
                    foreach (var error in result.Errors.Take(10))
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    if (result.Errors.Count > 10)
                    {
                        Console.WriteLine($"  ... 他 {result.Errors.Count - 10}件のエラー");
                    }
                }
            }
            
            Console.WriteLine("\n=== 前月末在庫インポート完了 ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ エラー: {ex.Message}");
            logger.LogError(ex, "前月末在庫インポートでエラーが発生しました");
        }
    }
}

/// <summary>
/// ファイル処理順序を取得
/// </summary>
private static int GetFileProcessOrder(string fileName)
{
    // Phase 1: マスタファイル（優先度1-8）
    if (fileName.Contains("等級汎用マスター")) return 1;
    if (fileName.Contains("階級汎用マスター")) return 2;
    if (fileName.Contains("荷印汎用マスター")) return 3;
    if (fileName.Contains("産地汎用マスター")) return 4;
    if (fileName == "商品.csv") return 5;
    if (fileName == "得意先.csv") return 6;
    if (fileName == "仕入先.csv") return 7;
    if (fileName == "単位.csv") return 8;
    
    // Phase 2: 初期在庫（優先度10）
    if (fileName == "前月末在庫.csv") return 10;
    
    // Phase 3: 伝票ファイル（優先度20-22）
    if (fileName.StartsWith("売上伝票")) return 20;
    if (fileName.StartsWith("仕入伝票")) return 21;
    if (fileName.StartsWith("在庫調整") || fileName.StartsWith("受注伝票")) return 22;
    
    // Phase 4: その他（優先度99）
    return 99;
}

static async Task ExecuteInitInventoryCommand(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var inventoryRepo = scopedServices.GetRequiredService<IInventoryRepository>();
        
        try
        {
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
            
            Console.WriteLine("=== 在庫マスタ初期データ作成開始 ===");
            Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
            Console.WriteLine();
            
            var stopwatch = Stopwatch.StartNew();
            
            // 在庫マスタ初期データ作成実行
            var count = await inventoryRepo.CreateInitialInventoryFromVouchersAsync(jobDate);
            
            stopwatch.Stop();
            
            Console.WriteLine($"\n処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
            Console.WriteLine($"作成件数: {count:N0}件");
            
            if (count > 0)
            {
                Console.WriteLine("\n✅ 在庫マスタ初期データ作成が正常に完了しました");
                logger.LogInformation("在庫マスタ初期データ作成完了: {Count}件", count);
            }
            else
            {
                Console.WriteLine("\n⚠️ 作成対象のデータがありませんでした");
                Console.WriteLine("売上・仕入・在庫調整伝票がインポートされているか確認してください");
                logger.LogWarning("在庫マスタ初期データ作成: 作成対象なし");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ エラー: {ex.Message}");
            logger.LogError(ex, "在庫マスタ初期データ作成でエラーが発生しました");
            throw;
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
        
        // ファイル管理サービス
        var fileService = scopedServices.GetRequiredService<IFileManagementService>();
        
        // 伝票インポートサービス
        var salesImportService = scopedServices.GetRequiredService<SalesVoucherImportService>();
        var purchaseImportService = scopedServices.GetRequiredService<PurchaseVoucherImportService>();
        var adjustmentImportService = scopedServices.GetRequiredService<InventoryAdjustmentImportService>();
        
        // マスタインポートサービス（利用可能なものを取得）
        var shippingMarkImportService = scopedServices.GetService<IShippingMarkMasterImportService>();
        var regionImportService = scopedServices.GetService<IRegionMasterImportService>();
        var productImportService = scopedServices.GetService<ProductMasterImportService>();
        var customerImportService = scopedServices.GetService<CustomerMasterImportService>();
        var supplierImportService = scopedServices.GetService<SupplierMasterImportService>();
        
        // リポジトリ（代替手段として使用）
        var gradeRepo = scopedServices.GetService<IGradeMasterRepository>();
        var classRepo = scopedServices.GetService<IClassMasterRepository>();
        var inventoryRepo = scopedServices.GetRequiredService<IInventoryRepository>();
        
        // 在庫マスタ最適化サービス（オプション - 後で追加）
        // var optimizationService = scopedServices.GetService<IInventoryMasterOptimizationService>();
        
        var department = args[2];
        DateTime jobDate = args.Length >= 4 && DateTime.TryParse(args[3], out var date) ? date : DateTime.Today;
        
        Console.WriteLine($"=== フォルダ監視取込開始 ===");
        Console.WriteLine($"部門: {department}");
        Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
        
        var errorCount = 0;
        var processedCounts = new Dictionary<string, int>();
        
        try
        {
            // 重複データクリア処理
            Console.WriteLine("\n既存データのクリア中...");
            await ClearExistingVoucherData(scopedServices, jobDate, department);
            Console.WriteLine("✅ 既存データクリア完了");
            
            // ファイル一覧の取得
            var files = await fileService.GetPendingFilesAsync(department);
            Console.WriteLine($"取込対象ファイル数: {files.Count}\n");
            
            // ファイルを処理順序でソート
            var sortedFiles = files
                .OrderBy(f => GetFileProcessOrder(Path.GetFileName(f)))
                .ThenBy(f => Path.GetFileName(f))
                .ToList();
            
            // 各ファイルの処理
            foreach (var file in sortedFiles)
            {
                var fileName = Path.GetFileName(file);
                Console.WriteLine($"処理中: {fileName}");
                
                try
                {
                    // ========== Phase 1: マスタ系ファイル ==========
                    if (fileName.Contains("等級汎用マスター"))
                    {
                        if (gradeRepo != null)
                        {
                            await gradeRepo.ImportFromCsvAsync();
                            Console.WriteLine("✅ 等級マスタとして処理完了");
                        }
                        else
                        {
                            logger.LogWarning("IGradeMasterRepositoryが未実装のため、等級マスタの取込をスキップします");
                            await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            continue;
                        }
                        await fileService.MoveToProcessedAsync(file, department);
                    }
                    else if (fileName.Contains("階級汎用マスター"))
                    {
                        if (classRepo != null)
                        {
                            await classRepo.ImportFromCsvAsync();
                            Console.WriteLine("✅ 階級マスタとして処理完了");
                        }
                        else
                        {
                            logger.LogWarning("IClassMasterRepositoryが未実装のため、階級マスタの取込をスキップします");
                            await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            continue;
                        }
                        await fileService.MoveToProcessedAsync(file, department);
                    }
                    else if (fileName.Contains("荷印汎用マスター"))
                    {
                        if (shippingMarkImportService != null)
                        {
                            var result = await shippingMarkImportService.ImportAsync(file);
                            Console.WriteLine($"✅ 荷印マスタとして処理完了 - {result.ImportedCount}件");
                            processedCounts["荷印マスタ"] = result.ImportedCount;
                        }
                        else
                        {
                            logger.LogWarning("IShippingMarkMasterImportServiceが未実装のため、荷印マスタの取込をスキップします");
                            await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            continue;
                        }
                        await fileService.MoveToProcessedAsync(file, department);
                    }
                    else if (fileName.Contains("産地汎用マスター"))
                    {
                        if (regionImportService != null)
                        {
                            var result = await regionImportService.ImportAsync(file);
                            Console.WriteLine($"✅ 産地マスタとして処理完了 - {result.ImportedCount}件");
                            processedCounts["産地マスタ"] = result.ImportedCount;
                        }
                        else
                        {
                            logger.LogWarning("IRegionMasterImportServiceが未実装のため、産地マスタの取込をスキップします");
                            await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            continue;
                        }
                        await fileService.MoveToProcessedAsync(file, department);
                    }
                    else if (fileName == "商品.csv")
                    {
                        if (productImportService != null)
                        {
                            var result = await productImportService.ImportFromCsvAsync(file, jobDate);
                            Console.WriteLine($"✅ 商品マスタとして処理完了 - {result.ImportedCount}件");
                            processedCounts["商品マスタ"] = result.ImportedCount;
                        }
                        else
                        {
                            logger.LogWarning("ProductMasterImportServiceが未実装のため、商品マスタの取込をスキップします");
                            await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            continue;
                        }
                        await fileService.MoveToProcessedAsync(file, department);
                    }
                    else if (fileName == "得意先.csv")
                    {
                        if (customerImportService != null)
                        {
                            var result = await customerImportService.ImportFromCsvAsync(file, jobDate);
                            Console.WriteLine($"✅ 得意先マスタとして処理完了 - {result.ImportedCount}件");
                            processedCounts["得意先マスタ"] = result.ImportedCount;
                        }
                        else
                        {
                            logger.LogWarning("CustomerMasterImportServiceが未実装のため、得意先マスタの取込をスキップします");
                            await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            continue;
                        }
                        await fileService.MoveToProcessedAsync(file, department);
                    }
                    else if (fileName == "仕入先.csv")
                    {
                        if (supplierImportService != null)
                        {
                            var result = await supplierImportService.ImportFromCsvAsync(file, jobDate);
                            Console.WriteLine($"✅ 仕入先マスタとして処理完了 - {result.ImportedCount}件");
                            processedCounts["仕入先マスタ"] = result.ImportedCount;
                        }
                        else
                        {
                            logger.LogWarning("SupplierMasterImportServiceが未実装のため、仕入先マスタの取込をスキップします");
                            await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            continue;
                        }
                        await fileService.MoveToProcessedAsync(file, department);
                    }
                    // ========== Phase 2: 初期在庫ファイル ==========
                    else if (fileName == "前月末在庫.csv")
                    {
                        logger.LogInformation("前月末在庫の処理を開始します");
                        
                        // PreviousMonthInventoryImportServiceを使用して処理
                        var previousMonthService = scopedServices.GetService<PreviousMonthInventoryImportService>();
                        if (previousMonthService == null)
                        {
                            logger.LogError("PreviousMonthInventoryImportServiceが登録されていません");
                            await fileService.MoveToErrorAsync(file, department, "Service_Not_Found");
                            continue;
                        }
                        
                        var result = await previousMonthService.ImportAsync(jobDate);
                        
                        if (result.IsSuccess)
                        {
                            await fileService.MoveToProcessedAsync(file, department);
                            logger.LogInformation("前月末在庫を初期在庫として処理完了: {Count}件", result.ProcessedRecords);
                            
                            // 処理実績に記録（最終サマリーに表示するため）
                            processedCounts["前月末在庫"] = result.ProcessedRecords;
                            Console.WriteLine($"✅ 前月末在庫として処理完了 - {result.ProcessedRecords}件");
                        }
                        else
                        {
                            await fileService.MoveToErrorAsync(file, department, result.Message);
                            logger.LogError("前月末在庫の処理に失敗: {Message}", result.Message);
                            
                            // エラーカウント増加
                            errorCount++;
                        }
                        
                        continue;
                    }
                    // ========== Phase 3: 伝票系ファイル ==========
                    else if (fileName.StartsWith("売上伝票"))
                    {
                        var dataSetId = await salesImportService.ImportAsync(file, jobDate, department);
                        Console.WriteLine($"✅ 売上伝票として処理完了 - データセットID: {dataSetId}");
                        processedCounts["売上伝票"] = 1; // TODO: 実際の件数を取得
                        // await fileService.MoveToProcessedAsync(file, department); // ImportService内で移動済み
                    }
                    else if (fileName.StartsWith("仕入伝票"))
                    {
                        var dataSetId = await purchaseImportService.ImportAsync(file, jobDate, department);
                        Console.WriteLine($"✅ 仕入伝票として処理完了 - データセットID: {dataSetId}");
                        processedCounts["仕入伝票"] = 1; // TODO: 実際の件数を取得
                        // await fileService.MoveToProcessedAsync(file, department); // ImportService内で移動済み
                    }
                    else if (fileName.StartsWith("受注伝票"))
                    {
                        // 受注伝票は在庫調整として処理
                        var dataSetId = await adjustmentImportService.ImportAsync(file, jobDate, department);
                        Console.WriteLine($"✅ 在庫調整として処理完了 - データセットID: {dataSetId}");
                        processedCounts["受注伝票（在庫調整）"] = 1; // TODO: 実際の件数を取得
                        // await fileService.MoveToProcessedAsync(file, department); // ImportService内で移動済み
                    }
                    else if (fileName.StartsWith("在庫調整"))
                    {
                        var dataSetId = await adjustmentImportService.ImportAsync(file, jobDate, department);
                        Console.WriteLine($"✅ 在庫調整として処理完了 - データセットID: {dataSetId}");
                        processedCounts["在庫調整"] = 1; // TODO: 実際の件数を取得
                        // await fileService.MoveToProcessedAsync(file, department); // ImportService内で移動済み
                    }
                    // ========== 未対応ファイル ==========
                    else if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        // 既知の未対応ファイル
                        string[] knownButUnsupported = {
                            "担当者", "単位", "商品分類", "得意先分類", 
                            "仕入先分類", "担当者分類", "支払伝票", "入金伝票"
                        };
                        
                        if (knownButUnsupported.Any(pattern => fileName.Contains(pattern)))
                        {
                            Console.WriteLine($"⚠️ {fileName} は現在未対応です（スキップ）");
                            await fileService.MoveToErrorAsync(file, department, "未対応のCSVファイル形式");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ {fileName} は認識できないCSVファイルです");
                            await fileService.MoveToErrorAsync(file, department, "不明なCSVファイル");
                        }
                    }
                    else
                    {
                        // CSV以外のファイル
                        await fileService.MoveToErrorAsync(file, department, "CSVファイル以外は処理対象外");
                        Console.WriteLine("⚠️ CSVファイル以外のためエラーフォルダへ移動");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ファイル処理中にエラーが発生しました: {FileName}", fileName);
                    Console.WriteLine($"❌ エラー: {fileName} - {ex.Message}");
                    
                    // エラーファイルは移動せずに続行
                    errorCount++;
                    continue;
                }
                
                Console.WriteLine(); // 各ファイル処理後に改行
            }
            
            // ========== 在庫マスタ最適化処理 ==========
            logger.LogInformation("========== 在庫マスタ最適化処理開始 ==========");
            logger.LogInformation("本日取り込まれたデータを対象に最適化を実行します（CreatedDate = {Today}）", DateTime.Today);

            try
            {
                // 接続文字列の取得
                var configuration = scopedServices.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("接続文字列が設定されていません");
                }
                
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                
                // 今回の取込で登録されたすべての日付を取得
                logger.LogInformation("取り込まれたデータの日付を確認中...");
                
                // CreatedDateが今日のデータから日付を取得（DataSetIdの形式に依存しない）
                var importedDatesQuery = @"
                    SELECT DISTINCT CAST(JobDate AS DATE) as JobDate
                    FROM (
                        SELECT JobDate FROM SalesVouchers 
                        WHERE CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE)
                        UNION
                        SELECT JobDate FROM PurchaseVouchers 
                        WHERE CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE)
                        UNION
                        SELECT JobDate FROM InventoryAdjustments 
                        WHERE CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE)
                    ) AS AllDates
                    ORDER BY JobDate";
                
                var importedDates = await connection.QueryAsync<DateTime>(importedDatesQuery);
                
                var dateList = importedDates.ToList();
                
                if (!dateList.Any())
                {
                    logger.LogWarning("本日取り込まれたデータが見つかりません。CreatedDate={Today}", DateTime.Today);
                    Console.WriteLine("⚠️ 在庫マスタ最適化対象のデータがありません（本日取り込まれたデータなし）");
                }
                else
                {
                    logger.LogInformation("取り込まれた日付: {Count}日分 - {Dates}", 
                        dateList.Count, 
                        string.Join(", ", dateList.Select(d => d.ToString("yyyy-MM-dd"))));
                    
                    var totalProcessed = 0;
                    var totalInserted = 0;
                    var totalUpdated = 0;
                    var totalErrors = 0;
                    
                    // 各日付に対して最適化を実行
                    foreach (var targetDate in dateList)
                    {
                        try
                        {
                            logger.LogInformation("日付 {Date:yyyy-MM-dd} の在庫マスタ最適化を実行中...", targetDate);
                            
                            // インラインでMERGE文を実行
                            var mergeResult = await ExecuteInventoryOptimizationForDate(
                                connection, targetDate, department, logger);
                            
                            totalProcessed += mergeResult.ProcessedCount;
                            totalInserted += mergeResult.InsertedCount;
                            totalUpdated += mergeResult.UpdatedCount;
                            
                            logger.LogInformation(
                                "日付 {Date:yyyy-MM-dd} 完了: 処理={Processed}, 新規={Inserted}, 更新={Updated}",
                                targetDate, 
                                mergeResult.ProcessedCount,
                                mergeResult.InsertedCount, 
                                mergeResult.UpdatedCount);
                        }
                        catch (Exception dateEx)
                        {
                            logger.LogError(dateEx, "日付 {Date:yyyy-MM-dd} の処理でエラーが発生しました", targetDate);
                            totalErrors++;
                            // エラーが発生しても次の日付の処理を続行
                        }
                    }
                    
                    // 最終結果の表示
                    logger.LogInformation(
                        "在庫マスタ最適化完了: 処理日数={DateCount}, 総処理={ProcessedCount}, " +
                        "新規={InsertedCount}, 更新={UpdatedCount}, エラー={ErrorCount}",
                        dateList.Count,
                        totalProcessed,
                        totalInserted,
                        totalUpdated,
                        totalErrors);
                        
                    if (totalErrors > 0)
                    {
                        Console.WriteLine($"⚠️ 在庫マスタ最適化完了（一部エラー）: {dateList.Count}日分、{totalProcessed}件処理、{totalErrors}件のエラー");
                    }
                    else
                    {
                        Console.WriteLine($"✅ 在庫マスタ最適化完了: {dateList.Count}日分、{totalProcessed}件処理（新規{totalInserted}件、更新{totalUpdated}件）");
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                logger.LogError(sqlEx, 
                    "在庫マスタ最適化処理でSQLエラーが発生しました。" +
                    "エラーコード: {Number}, 重大度: {Class}, 状態: {State}", 
                    sqlEx.Number, sqlEx.Class, sqlEx.State);
                    
                Console.WriteLine($"❌ 在庫マスタ最適化でデータベースエラーが発生しました: {sqlEx.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "在庫マスタ最適化処理で予期しないエラーが発生しました");
                Console.WriteLine($"❌ 在庫マスタ最適化でエラーが発生しました: {ex.Message}");
            }

            logger.LogInformation("========== 在庫マスタ最適化処理終了 ==========");
            
            // ========== アンマッチリスト処理 ==========
            // 注意：アンマッチリスト処理は別途 create-unmatch-list コマンドで実行してください
            // await ExecuteUnmatchListAfterImport(scopedServices, jobDate, logger);
            
            // 処理結果のサマリを表示
            Console.WriteLine("\n=== フォルダ監視取込完了 ===");
            Console.WriteLine($"対象日付: {jobDate:yyyy-MM-dd}");
            Console.WriteLine($"部門: {department}");
            Console.WriteLine($"処理ファイル数: {sortedFiles.Count}");
            
            if (processedCounts.Any())
            {
                Console.WriteLine("\n処理実績:");
                foreach (var kvp in processedCounts)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}件");
                }
            }
            
            if (errorCount > 0)
            {
                Console.WriteLine($"\n⚠️ {errorCount}件のファイルでエラーが発生しました。");
            }
            
            Console.WriteLine("========================\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ エラー: {ex.Message}");
            logger.LogError(ex, "フォルダ監視取込でエラーが発生しました");
        }
    }
}

/// <summary>
/// 指定日付の在庫マスタ最適化を実行
/// </summary>
private static async Task<(int ProcessedCount, int InsertedCount, int UpdatedCount)> 
    ExecuteInventoryOptimizationForDate(
        SqlConnection connection, 
        DateTime jobDate, 
        string dataSetId,
        ILogger logger)
{
    const string mergeSql = @"
        MERGE InventoryMaster AS target
        USING (
            SELECT DISTINCT
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName
            FROM (
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                FROM SalesVouchers
                WHERE CONVERT(date, JobDate) = @jobDate
                UNION
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                FROM PurchaseVouchers
                WHERE CONVERT(date, JobDate) = @jobDate
                UNION
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                FROM InventoryAdjustments
                WHERE CONVERT(date, JobDate) = @jobDate
            ) AS products
        ) AS source
        ON target.ProductCode = source.ProductCode
            AND target.GradeCode = source.GradeCode
            AND target.ClassCode = source.ClassCode
            AND target.ShippingMarkCode = source.ShippingMarkCode
            AND target.ShippingMarkName = source.ShippingMarkName
        WHEN MATCHED AND target.JobDate <> @jobDate THEN
            UPDATE SET 
                JobDate = @jobDate,
                UpdatedDate = GETDATE(),
                DataSetId = @dataSetId
        WHEN NOT MATCHED THEN
            INSERT (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount, DailyFlag,
                DataSetId, DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
                PreviousMonthQuantity, PreviousMonthAmount
            )
            VALUES (
                source.ProductCode,
                source.GradeCode,
                source.ClassCode,
                source.ShippingMarkCode,
                source.ShippingMarkName,
                '商品名未設定',
                'PCS',
                0,
                '',
                '',
                @jobDate,
                GETDATE(),
                GETDATE(),
                0, 0, 0, 0, '9',
                @dataSetId,
                0, 0, 0, 0,
                0, 0  -- PreviousMonthQuantity, PreviousMonthAmount
            )
        OUTPUT $action AS Action;";
    
    var results = await connection.QueryAsync<dynamic>(
        mergeSql,
        new { jobDate, dataSetId },
        commandTimeout: 300);
    
    var resultList = results.ToList();
    var insertedCount = resultList.Count(r => r.Action == "INSERT");
    var updatedCount = resultList.Count(r => r.Action == "UPDATE");
    var processedCount = insertedCount + updatedCount;
    
    logger.LogDebug(
        "MERGE完了 - JobDate: {JobDate}, Inserted: {Inserted}, Updated: {Updated}",
        jobDate, insertedCount, updatedCount);
    
    return (processedCount, insertedCount, updatedCount);
}

/// <summary>
/// JobDateに基づいて既存の伝票データを削除
/// </summary>
static async Task ClearExistingVoucherData(IServiceProvider services, DateTime jobDate, string department)
{
    var salesRepo = services.GetRequiredService<ISalesVoucherRepository>();
    var purchaseRepo = services.GetRequiredService<IPurchaseVoucherRepository>();
    var adjustmentRepo = services.GetRequiredService<IInventoryAdjustmentRepository>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("既存データをクリア中（JobDate: {JobDate}, 部門: {Department}）...", jobDate, department);
    
    try
    {
        // JobDateベースで既存データを削除
        var salesDeleted = await salesRepo.DeleteByJobDateAsync(jobDate);
        var purchaseDeleted = await purchaseRepo.DeleteByJobDateAsync(jobDate);
        var adjustmentDeleted = await adjustmentRepo.DeleteByJobDateAsync(jobDate);
        
        logger.LogInformation("既存データ削除完了: 売上 {SalesCount}件, 仕入 {PurchaseCount}件, 調整 {AdjustmentCount}件", 
            salesDeleted, purchaseDeleted, adjustmentDeleted);
        
        Console.WriteLine($"  - 売上伝票: {salesDeleted}件削除");
        Console.WriteLine($"  - 仕入伝票: {purchaseDeleted}件削除");
        Console.WriteLine($"  - 在庫調整: {adjustmentDeleted}件削除");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "既存データクリア中にエラーが発生しました");
        Console.WriteLine($"⚠️ 既存データクリア中にエラー: {ex.Message}");
        // エラーが発生しても処理を継続
    }
}


/// <summary>
/// インポート処理後のアンマッチリスト処理を実行
/// </summary>
private static async Task ExecuteUnmatchListAfterImport(IServiceProvider services, DateTime jobDate, ILogger<Program> logger)
{
    try
    {
        logger.LogInformation("アンマッチリスト処理を開始します");
        Console.WriteLine("\n=== アンマッチリスト処理開始 ===");
        
        var unmatchListService = services.GetRequiredService<IUnmatchListService>();
        var reportService = services.GetRequiredService<IUnmatchListReportService>();
        var fileManagementService = services.GetRequiredService<IFileManagementService>();
        
        // アンマッチリスト処理実行
        var result = await unmatchListService.ProcessUnmatchListAsync(jobDate);
        
        if (result.Success)
        {
            logger.LogInformation("アンマッチリスト処理が完了しました - アンマッチ件数: {Count}件", result.UnmatchCount);
            Console.WriteLine($"✅ アンマッチリスト処理完了 - {result.UnmatchCount}件のアンマッチを検出");
            
            // PDF出力（0件でも生成）
            try
            {
                var pdfBytes = reportService.GenerateUnmatchListReport(result.UnmatchItems, jobDate);
                
                if (pdfBytes != null && pdfBytes.Length > 0)
                {
                    // FileManagementServiceを使用してレポートパスを取得
                    var pdfPath = await fileManagementService.GetReportOutputPathAsync("UnmatchList", jobDate, "pdf");
                    
                    await File.WriteAllBytesAsync(pdfPath, pdfBytes);
                    
                    logger.LogInformation("アンマッチリストPDFを保存しました: {Path}", pdfPath);
                    Console.WriteLine($"  - PDFファイル: {Path.GetFileName(pdfPath)}");
                }
            }
            catch (Exception pdfEx)
            {
                logger.LogError(pdfEx, "アンマッチリストPDF生成中にエラーが発生しました");
                Console.WriteLine($"⚠️ PDF生成エラー: {pdfEx.Message}");
            }
        }
        else
        {
            logger.LogError("アンマッチリスト処理が失敗しました: {ErrorMessage}", result.ErrorMessage);
            Console.WriteLine($"❌ アンマッチリスト処理失敗: {result.ErrorMessage}");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "アンマッチリスト処理中にエラーが発生しました");
        Console.WriteLine($"⚠️ アンマッチリスト処理でエラーが発生しました: {ex.Message}");
        // エラーが発生してもインポート処理全体は成功とする
    }
}

/// <summary>
/// 日次終了処理の事前確認を実行
/// </summary>
private static async Task ExecuteCheckDailyCloseAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var dailyCloseService = scopedServices.GetRequiredService<IDailyCloseService>();
        
        // ジョブ日付を取得
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
        
        try
        {
            Console.WriteLine("=== 日次終了処理 事前確認 ===");
            Console.WriteLine($"対象日付: {jobDate:yyyy-MM-dd}");
            Console.WriteLine($"現在時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            
            // 確認情報を取得
            var confirmation = await dailyCloseService.GetConfirmationInfo(jobDate);
            
            // 商品日報情報
            if (confirmation.DailyReport != null)
            {
                Console.WriteLine("【商品日報情報】");
                Console.WriteLine($"  作成時刻: {confirmation.DailyReport.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  作成者: {confirmation.DailyReport.CreatedBy}");
                Console.WriteLine($"  DatasetId: {confirmation.DailyReport.DatasetId}");
                Console.WriteLine();
            }
            
            // 最新CSV取込情報
            if (confirmation.LatestCsvImport != null)
            {
                Console.WriteLine("【最新CSV取込情報】");
                Console.WriteLine($"  取込時刻: {confirmation.LatestCsvImport.ImportedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  取込者: {confirmation.LatestCsvImport.ImportedBy}");
                Console.WriteLine($"  ファイル: {confirmation.LatestCsvImport.FileNames}");
                Console.WriteLine();
            }
            
            // データ件数サマリー
            Console.WriteLine("【データ件数】");
            Console.WriteLine($"  売上伝票: {confirmation.DataCounts.SalesCount:N0}件");
            Console.WriteLine($"  仕入伝票: {confirmation.DataCounts.PurchaseCount:N0}件");
            Console.WriteLine($"  在庫調整: {confirmation.DataCounts.AdjustmentCount:N0}件");
            Console.WriteLine($"  CP在庫: {confirmation.DataCounts.CpInventoryCount:N0}件");
            Console.WriteLine();
            
            // 金額サマリー
            Console.WriteLine("【金額サマリー】");
            Console.WriteLine($"  売上総額: {confirmation.Amounts.SalesAmount:C}");
            Console.WriteLine($"  仕入総額: {confirmation.Amounts.PurchaseAmount:C}");
            Console.WriteLine($"  推定粗利: {confirmation.Amounts.EstimatedGrossProfit:C}");
            Console.WriteLine();
            
            // 検証結果
            if (confirmation.ValidationResults.Any())
            {
                Console.WriteLine("【検証結果】");
                foreach (var validation in confirmation.ValidationResults.OrderBy(v => v.Level))
                {
                    var prefix = validation.Level switch
                    {
                        ValidationLevel.Error => "❌ エラー",
                        ValidationLevel.Warning => "⚠️  警告",
                        ValidationLevel.Info => "ℹ️  情報",
                        _ => "   "
                    };
                    
                    Console.WriteLine($"{prefix}: {validation.Message}");
                    if (!string.IsNullOrEmpty(validation.Detail))
                    {
                        Console.WriteLine($"         {validation.Detail}");
                    }
                }
                Console.WriteLine();
            }
            
            // 処理可否
            Console.WriteLine("【処理可否判定】");
            if (confirmation.CanProcess)
            {
                Console.WriteLine("✅ 日次終了処理を実行可能です");
                Console.WriteLine();
                Console.WriteLine("実行するには以下のコマンドを使用してください:");
                Console.WriteLine($"  dotnet run daily-close {jobDate:yyyy-MM-dd}");
            }
            else
            {
                Console.WriteLine("❌ 日次終了処理を実行できません");
                Console.WriteLine("上記のエラーを解決してから再度実行してください。");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "日次終了処理の事前確認でエラーが発生しました");
            Console.WriteLine($"エラー: {ex.Message}");
        }
    }
}

/// <summary>
/// CP在庫マスタ作成コマンドを実行
/// </summary>
private static async Task ExecuteCreateCpInventoryAsync(IServiceProvider services, string[] args)
{
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var cpInventoryCreationService = scopedServices.GetRequiredService<ICpInventoryCreationService>();
        
        // ジョブ日付を取得
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

        // データセットIDを生成
        var dataSetId = $"CP_INVENTORY_{DateTime.Now:yyyyMMdd_HHmmss}";
        
        try
        {
            Console.WriteLine("=== CP在庫マスタ作成 ===");
            Console.WriteLine($"処理日付: {jobDate:yyyy-MM-dd}");
            Console.WriteLine($"データセットID: {dataSetId}");
            Console.WriteLine();
            
            // CP在庫マスタ作成実行
            var result = await cpInventoryCreationService.CreateCpInventoryFromInventoryMasterAsync(jobDate, dataSetId);
            
            if (result.Success)
            {
                Console.WriteLine("=== 処理結果 ===");
                Console.WriteLine($"削除された既存レコード: {result.DeletedCount}件");
                Console.WriteLine($"在庫マスタからコピー: {result.CopiedCount}件");
                Console.WriteLine();
                
                if (result.Warnings.Any())
                {
                    Console.WriteLine("⚠️ 警告:");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"  {warning}");
                    }
                    Console.WriteLine();
                    
                    // 未登録商品の詳細表示
                    var missingResult = await cpInventoryCreationService.DetectMissingProductsAsync(jobDate);
                    if (missingResult.MissingProducts.Any())
                    {
                        Console.WriteLine("未登録商品の詳細（最初の10件）:");
                        foreach (var missing in missingResult.MissingProducts.Take(10))
                        {
                            Console.WriteLine($"  商品コード:{missing.ProductCode}, 等級:{missing.GradeCode}, 階級:{missing.ClassCode}, " +
                                           $"荷印:{missing.ShippingMarkCode}, 荷印名:{missing.ShippingMarkName}, " +
                                           $"検出元:{missing.FoundInVoucherType}");
                        }
                        if (missingResult.MissingProducts.Count > 10)
                        {
                            Console.WriteLine($"  他{missingResult.MissingProducts.Count - 10}件...");
                        }
                    }
                }
                
                Console.WriteLine("✅ CP在庫マスタ作成が正常に完了しました");
            }
            else
            {
                Console.WriteLine("❌ CP在庫マスタ作成に失敗しました");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    Console.WriteLine($"エラー: {result.ErrorMessage}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CP在庫マスタ作成でエラーが発生しました");
            Console.WriteLine($"エラー: {ex.Message}");
        }
    }
}

} // Program クラスの終了


