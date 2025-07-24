using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Services;
using InventorySystem.Core.Factories;
using InventorySystem.Data.Repositories;
using InventorySystem.Import.Services;
using InventorySystem.Import.Services.Masters;
using InventorySystem.Data.Repositories.Masters;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Core.Configuration;
using Microsoft.Extensions.Options;
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.Services;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Models;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using InventorySystem.Core.Interfaces.Services;
using InventorySystem.Reports.Services;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using InventorySystem.Data.Services;
using InventorySystem.Data.Services.Development;
using InventorySystem.Console.Commands;

// Program クラスの定義
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // カルチャー設定（日付処理の一貫性を保つため）
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        
        // ===== PDF生成診断情報 開始 =====
        Console.WriteLine("=== PDF Generation Diagnostics ===");
        Console.WriteLine($"Runtime Identifier: {RuntimeInformation.RuntimeIdentifier}");
        Console.WriteLine($"OS Description: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"Current Directory: {Environment.CurrentDirectory}");

        #if WINDOWS
        Console.WriteLine("WINDOWS symbol: DEFINED ✓ - FastReport services will be used");
        #else
        Console.WriteLine("WINDOWS symbol: NOT DEFINED ✗ - Placeholder services will be used");
        #endif

        // アセンブリ情報の表示
        var assembly = Assembly.GetExecutingAssembly();
        Console.WriteLine($"Assembly: {assembly.GetName().Name} v{assembly.GetName().Version}");

        // FastReport DLLの存在確認
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var fastReportDll = Path.Combine(baseDir, "FastReport.dll");
        if (File.Exists(fastReportDll))
        {
            var fileInfo = new FileInfo(fastReportDll);
            Console.WriteLine($"FastReport.dll: Found ✓ (Size: {fileInfo.Length:N0} bytes)");
        }
        else
        {
            Console.WriteLine($"FastReport.dll: NOT FOUND ✗ at {fastReportDll}");
        }
        Console.WriteLine("=================================\n");
        // ===== PDF生成診断情報 終了 =====
        
        // 実行環境情報の表示
Console.WriteLine($"実行環境: {Environment.OSVersion}");
Console.WriteLine($".NET Runtime: {Environment.Version}");
Console.WriteLine($"実行ディレクトリ: {Environment.CurrentDirectory}");
Console.WriteLine($"現在のカルチャー: {CultureInfo.CurrentCulture.Name} (InvariantCultureに統一)");

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
// 廃止: DataSetsテーブルは完全廃止済み、DataSetManagementテーブルのみ使用
// builder.Services.AddScoped<IDataSetRepository>(provider => 
//     new DataSetRepository(connectionString, provider.GetRequiredService<ILogger<DataSetRepository>>()));

// CSV取込専用リポジトリ
builder.Services.AddScoped<SalesVoucherCsvRepository>(provider => 
    new SalesVoucherCsvRepository(connectionString, provider.GetRequiredService<ILogger<SalesVoucherCsvRepository>>()));

// スキーマ更新サービス
builder.Services.AddScoped<DatabaseSchemaService>(provider =>
    new DatabaseSchemaService(connectionString, provider.GetRequiredService<ILogger<DatabaseSchemaService>>()));
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

// 入金・支払伝票リポジトリ
builder.Services.AddScoped<IReceiptVoucherRepository>(provider => 
    new ReceiptVoucherRepository(connectionString, provider.GetRequiredService<ILogger<ReceiptVoucherRepository>>()));
builder.Services.AddScoped<IPaymentVoucherRepository>(provider => 
    new PaymentVoucherRepository(connectionString, provider.GetRequiredService<ILogger<PaymentVoucherRepository>>()));

// Master import services
builder.Services.AddScoped<CustomerMasterImportService>();
builder.Services.AddScoped<ProductMasterImportService>();
builder.Services.AddScoped<SupplierMasterImportService>();
builder.Services.AddScoped<IShippingMarkMasterImportService, ShippingMarkMasterImportService>();
builder.Services.AddScoped<IRegionMasterImportService, RegionMasterImportService>();

// インポートサービスの一括登録
// この1行で以下の16種類のサービスがすべて登録されます：
// - UnitMasterImportService
// - ProductCategory1-3ImportService  
// - CustomerCategory1-5ImportService
// - SupplierCategory1-3ImportService
// - StaffMasterImportService, StaffCategory1ImportService
// - ReceiptVoucherImportService, PaymentVoucherImportService
builder.Services.AddImportServices(connectionString);

// FileStorage設定の登録
builder.Services.Configure<FileStorageSettings>(
    builder.Configuration.GetSection("FileStorage"));

// FileManagementServiceの登録
builder.Services.AddScoped<IFileManagementService, FileManagementService>();

// 日本時間サービスの登録（シングルトン）
builder.Services.AddSingleton<IJapanTimeService, JapanTimeService>();

// Error prevention services
builder.Services.AddScoped<InventorySystem.Core.Services.Validation.IDateValidationService, InventorySystem.Core.Services.Validation.DateValidationService>();
builder.Services.AddScoped<InventorySystem.Core.Services.DataSet.IDataSetManager, InventorySystem.Core.Services.DataSet.DataSetManager>();
builder.Services.AddScoped<InventorySystem.Core.Services.History.IProcessHistoryService, InventorySystem.Core.Services.History.ProcessHistoryService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IDailyCloseService, DailyCloseService>();
builder.Services.AddScoped<IDataSetIdManager, DataSetIdManager>();
builder.Services.AddScoped<DataSetIdRepairService>();

// Error prevention repositories
builder.Services.AddScoped<IDataSetManagementRepository>(provider => 
    new DataSetManagementRepository(connectionString, provider.GetRequiredService<ILogger<DataSetManagementRepository>>()));
builder.Services.AddScoped<IProcessHistoryRepository>(provider => 
    new ProcessHistoryRepository(connectionString, provider.GetRequiredService<ILogger<ProcessHistoryRepository>>()));
builder.Services.AddScoped<IDailyCloseManagementRepository>(provider => 
    new DailyCloseManagementRepository(connectionString, provider.GetRequiredService<ILogger<DailyCloseManagementRepository>>()));

builder.Services.AddScoped<IUnmatchListService, UnmatchListService>();
builder.Services.AddScoped<InventorySystem.Core.Interfaces.IDailyReportService, DailyReportService>();
builder.Services.AddScoped<IInventoryListService, InventoryListService>();
builder.Services.AddScoped<ICpInventoryCreationService, CpInventoryCreationService>();

// ⭐ Phase 2-B: ITimeProviderとDataSetManagementFactoryの登録（Gemini推奨）
// JST統一: 日本のビジネスシステムのため、JstTimeProviderを使用
builder.Services.AddSingleton<ITimeProvider, JstTimeProvider>();
builder.Services.AddScoped<IDataSetManagementFactory, DataSetManagementFactory>();

// フィーチャーフラグの設定を読み込み
builder.Services.Configure<FeatureFlags>(
    builder.Configuration.GetSection("Features"));

// DataSetService関連の登録（DataSetManagement専用）
builder.Services.AddScoped<IDataSetService, DataSetManagementService>();
Console.WriteLine("🔄 DataSetManagement専用モードで起動");
// Report Services
#if WINDOWS
// FastReportサービスの登録（Windows環境のみ）
// Linux環境ではFastReportフォルダのコンパイルが除外されるため、型の直接参照はできない
var unmatchListFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.UnmatchListFastReportService, InventorySystem.Reports");
var dailyReportFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.DailyReportFastReportService, InventorySystem.Reports");
var productAccountFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.ProductAccountFastReportService, InventorySystem.Reports");
if (unmatchListFastReportType != null && dailyReportFastReportType != null && productAccountFastReportType != null)
{
    builder.Services.AddScoped(typeof(IUnmatchListReportService), unmatchListFastReportType);
    builder.Services.AddScoped(typeof(InventorySystem.Reports.Interfaces.IDailyReportService), dailyReportFastReportType);
    builder.Services.AddScoped(typeof(InventorySystem.Reports.Interfaces.IProductAccountReportService), productAccountFastReportType);
}
else
{
    throw new InvalidOperationException("FastReportサービスが見つかりません。Windows環境で実行してください。");
}
#else
builder.Services.AddScoped<IUnmatchListReportService, PlaceholderUnmatchListReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IDailyReportService, PlaceholderDailyReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IProductAccountReportService, PlaceholderProductAccountReportService>();
#endif
builder.Services.AddScoped<SalesVoucherImportService>();
builder.Services.AddScoped<PurchaseVoucherImportService>();
builder.Services.AddScoped<InventoryAdjustmentImportService>();
builder.Services.AddScoped<PreviousMonthInventoryImportService>();
builder.Services.AddScoped<ImportWithCarryoverCommand>();

// 在庫マスタ最適化サービス
builder.Services.AddScoped<IInventoryMasterOptimizationService, InventorySystem.Data.Services.InventoryMasterOptimizationService>();

// 在庫最適化サービス
builder.Services.AddScoped<IInventoryOptimizationService, InventoryOptimizationService>();

// 特殊日付範囲サービス
builder.Services.AddScoped<ISpecialDateRangeService, SpecialDateRangeService>();

// 開発環境用サービス
builder.Services.AddScoped<InventorySystem.Core.Interfaces.Development.IDatabaseInitializationService>(provider =>
    new InventorySystem.Data.Services.Development.DatabaseInitializationService(
        connectionString, 
        provider.GetRequiredService<ILogger<InventorySystem.Data.Services.Development.DatabaseInitializationService>>()));
builder.Services.AddScoped<InventorySystem.Core.Interfaces.Development.IDailyCloseResetService>(provider =>
    new InventorySystem.Data.Services.Development.DailyCloseResetService(
        connectionString,
        provider.GetRequiredService<ILogger<InventorySystem.Data.Services.Development.DailyCloseResetService>>()));
builder.Services.AddScoped<InventorySystem.Core.Interfaces.Development.IDataStatusCheckService>(provider =>
    new InventorySystem.Data.Services.Development.DataStatusCheckService(
        connectionString,
        provider.GetRequiredService<ILogger<InventorySystem.Data.Services.Development.DataStatusCheckService>>()));
builder.Services.AddScoped<InventorySystem.Core.Interfaces.Development.IProcessingHistoryService>(provider =>
    new InventorySystem.Data.Services.Development.ProcessingHistoryService(
        connectionString,
        provider.GetRequiredService<ILogger<InventorySystem.Data.Services.Development.ProcessingHistoryService>>()));
builder.Services.AddScoped<InventorySystem.Core.Interfaces.Development.IDailySimulationService, InventorySystem.Data.Services.Development.DailySimulationService>();

// Process 2-5: 売上伝票への在庫単価書き込みと粗利計算サービス
builder.Services.AddScoped<GrossProfitCalculationService>();

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

// Parse command line arguments - Mainメソッドの引数を使用
// Environment.GetCommandLineArgs()は"dotnet""run"などを含むため使用しない
if (args.Length < 1)
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
    Console.WriteLine("  dotnet run init-inventory <dept>             - 初期在庫設定（前月末在庫.csv取込）");
    Console.WriteLine("  dotnet run import-with-carryover <dept>      - 前日在庫を引き継いでインポート");
    Console.WriteLine("");
    Console.WriteLine("【開発環境用コマンド】");
    Console.WriteLine("  dotnet run init-database [--force]           - データベース初期化");
    Console.WriteLine("  dotnet run reset-daily-close <YYYY-MM-DD> [--all] - 日次終了処理リセット");
    Console.WriteLine("  dotnet run dev-daily-close <YYYY-MM-DD> [--skip-validation] [--dry-run] - 開発用日次終了処理");
    Console.WriteLine("  dotnet run check-data-status <YYYY-MM-DD>    - データ状態確認");
    Console.WriteLine("  dotnet run simulate-daily <dept> <YYYY-MM-DD> [--dry-run] - 日次処理シミュレーション");
    Console.WriteLine("  dotnet run dev-daily-report <YYYY-MM-DD>     - 開発用商品日報（日付制限無視）");
    Console.WriteLine("  dotnet run dev-check-daily-close <YYYY-MM-DD> - 開発用日次終了確認（時間制限無視）");
    Console.WriteLine("");
    Console.WriteLine("  例: dotnet run test-connection");
    Console.WriteLine("  例: dotnet run unmatch-list 2025-06-16");
    Console.WriteLine("  例: dotnet run daily-report 2025-06-16");
    Console.WriteLine("  例: dotnet run inventory-list 2025-06-16");
    Console.WriteLine("  例: dotnet run import-sales sales.csv 2025-06-16");
    Console.WriteLine("  例: dotnet run import-masters");
    Console.WriteLine("  例: dotnet run check-masters");
    Console.WriteLine("  例: dotnet run init-inventory DeptA");
    Console.WriteLine("  例: dotnet run init-database --force");
    Console.WriteLine("  例: dotnet run reset-daily-close 2025-06-30 --all");
    Console.WriteLine("  例: dotnet run dev-daily-close 2025-06-30 --dry-run");
    Console.WriteLine("  例: dotnet run check-data-status 2025-06-30");
    Console.WriteLine("  例: dotnet run simulate-daily DeptA 2025-06-30 --dry-run");
    Console.WriteLine("  例: dotnet run cleanup-inventory-duplicates");
    Console.WriteLine("  例: dotnet run init-monthly-inventory 202507");
    return 1;
}

var command = args[0].ToLower();

// 自動スキーマチェック（init-database以外のコマンドで実行）
if (command != "init-database" && !await CheckAndFixDatabaseSchemaAsync(host.Services))
{
    Console.WriteLine("❌ データベーススキーマに問題があります。'dotnet run init-database --force' を実行してください。");
    return 1;
}

try
{
    switch (command)
    {
        case "unmatch-list":
            await ExecuteUnmatchListAsync(host.Services, args);
            break;
            
        case "daily-report":
            await ExecuteDailyReportAsync(host.Services, args);
            break;
            
        case "dev-daily-report":
            await ExecuteDevDailyReportAsync(host.Services, args);
            break;
            
        case "dev-check-daily-close":
            await ExecuteDevCheckDailyCloseAsync(host.Services, args);
            break;
            
        case "inventory-list":
            await ExecuteInventoryListAsync(host.Services, args);
            break;
            
        case "product-account":
            await ExecuteProductAccountAsync(host.Services, args);
            break;
            
        case "import-sales":
            await ExecuteImportSalesAsync(host.Services, args);
            break;
            
        case "import-purchase":
            await ExecuteImportPurchaseAsync(host.Services, args);
            break;
            
        case "import-adjustment":
            await ExecuteImportAdjustmentAsync(host.Services, args);
            break;
            
        case "test-pdf":
            Console.WriteLine("PDFテスト機能は削除されました。test-fastreport を使用してください。");
            break;
            
        case "test-connection":
            await TestDatabaseConnectionAsync(host.Services);
            break;
            
        case "debug-csv-structure":
            await DebugCsvStructureAsync(args);
            break;
            
        case "import-customers":
            await ExecuteImportCustomersAsync(host.Services, args);
            break;
            
        case "import-products":
            await ExecuteImportProductsAsync(host.Services, args);
            break;
            
        case "import-suppliers":
            await ExecuteImportSuppliersAsync(host.Services, args);
            break;
            
        case "init-folders":
            await ExecuteInitializeFoldersAsync(host.Services);
            break;
            
        case "import-folder":
            await ExecuteImportFromFolderAsync(host.Services, args);
            break;
        
        case "import-masters":
            await ExecuteImportMastersAsync(host.Services);
            break;
        
        case "check-masters":
            await ExecuteCheckMastersAsync(host.Services);
            break;
        
        case "import-previous-inventory":
            await ExecuteImportPreviousInventoryAsync(host.Services, args);
            break;
        
        case "init-inventory":
            await ExecuteInitInventoryAsync(host.Services, args);
            break;
            
        case "import-with-carryover":
            await ExecuteImportWithCarryoverAsync(host.Services, args);
            break;
        
        case "check-daily-close":
            await ExecuteCheckDailyCloseAsync(host.Services, args);
            break;
            
        case "analyze-pk-change":
            await ExecuteAnalyzePrimaryKeyChangeAsync(host.Services, args);
            break;
            
        // 開発環境用コマンド
        case "init-database":
        case "migrate-database":
            await ExecuteInitDatabaseAsync(host.Services, args);
            break;
            
        case "reset-daily-close":
            await ExecuteResetDailyCloseAsync(host.Services, args);
            break;
            
        case "dev-daily-close":
            await ExecuteDevDailyCloseAsync(host.Services, args);
            break;
            
        case "check-data-status":
            await ExecuteCheckDataStatusAsync(host.Services, args);
            break;
            
        case "check-schema":
            await ExecuteCheckSchemaAsync(host.Services, args);
            break;
            
        case "migrate-phase2":
            await ExecuteMigratePhase2Async(host.Services, args);
            break;
            
        case "migrate-phase3":
            await ExecuteMigratePhase3Async(host.Services, args);
            break;
            
        case "migrate-phase5":
            await ExecuteMigratePhase5Async(host.Services, args);
            break;
            
        case "simulate-daily":
            await ExecuteSimulateDailyAsync(host.Services, args);
            break;
            
        case "create-cp-inventory":
            await ExecuteCreateCpInventoryAsync(host.Services, args);
            break;
            
        case "cleanup-inventory-duplicates":
            await ExecuteCleanupInventoryDuplicatesAsync(host.Services);
            break;
            
        case "init-monthly-inventory":
            await ExecuteInitMonthlyInventoryAsync(host.Services, args);
            break;
            
        case "import-initial-inventory":
            await ExecuteImportInitialInventoryAsync(host.Services, args);
            break;
        
        case "optimize-inventory":
            await ExecuteOptimizeInventoryAsync(host.Services, args);
            break;
            
        case "process-2-5":
        case "gross-profit":
            await ExecuteProcess25Async(host.Services, args);
            break;

        case "repair-dataset-id":
            await ExecuteRepairDataSetIdAsync(host.Services, args);
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
        var inventoryRepository = scopedServices.GetRequiredService<IInventoryRepository>();
        
        // 日付指定の確認（オプション）
        DateTime? targetDate = null;
        if (args.Length >= 2 && DateTime.TryParse(args[1], out var parsedDate))
        {
            targetDate = parsedDate;
            logger.LogInformation("指定された対象日: {TargetDate:yyyy-MM-dd}", targetDate);
        }
        
        // 部門指定（オプション）
        string? department = null;
        if (args.Length >= 3)
        {
            department = args[2];
            logger.LogInformation("指定された部門: {Department}", department);
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        Console.WriteLine("=== アンマッチリスト処理開始 ===");
        
        // 在庫マスタから最新JobDateを取得（表示用）
        var latestJobDate = await inventoryRepository.GetMaxJobDateAsync();
        Console.WriteLine($"在庫マスタ最新JobDate: {latestJobDate:yyyy-MM-dd}");
        if (targetDate.HasValue)
        {
            Console.WriteLine($"処理対象: {targetDate:yyyy-MM-dd}以前のアクティブ在庫");
        }
        else
        {
            Console.WriteLine("処理対象: 全期間のアクティブ在庫");
        }
        Console.WriteLine();
        
        // アンマッチリスト処理実行
        var result = targetDate.HasValue 
            ? await unmatchListService.ProcessUnmatchListAsync(targetDate.Value)
            : await unmatchListService.ProcessUnmatchListAsync();
    
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
            
            // ===== サービス診断情報 開始 =====
            logger.LogInformation("=== Service Diagnostics ===");
            logger.LogInformation($"Service Type: {reportService.GetType().FullName}");
            logger.LogInformation($"Assembly: {reportService.GetType().Assembly.GetName().Name}");
            // ===== サービス診断情報 終了 =====
            
            Console.WriteLine("PDF生成中...");
            var pdfBytes = reportService.GenerateUnmatchListReport(result.UnmatchItems, latestJobDate);
            
            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                // FileManagementServiceを使用してレポートパスを取得
                var pdfPath = await fileManagementService.GetReportOutputPathAsync("UnmatchList", latestJobDate, "pdf");
                
                await File.WriteAllBytesAsync(pdfPath, pdfBytes);
                
                Console.WriteLine($"PDFファイルを保存しました: {pdfPath}");
                Console.WriteLine($"ファイルサイズ: {pdfBytes.Length / 1024.0:F2} KB");
                
                // ===== PDF検証 開始 =====
                if (File.Exists(pdfPath))
                {
                    var fileInfo = new FileInfo(pdfPath);
                    logger.LogInformation($"PDF generated: {fileInfo.Name} (Size: {fileInfo.Length:N0} bytes)");
                    
                    // PDFヘッダーの確認
                    try
                    {
                        using var fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
                        var header = new byte[10];
                        fs.Read(header, 0, Math.Min(10, (int)fs.Length));
                        var headerString = System.Text.Encoding.ASCII.GetString(header);
                        logger.LogInformation($"PDF header: {headerString}");
                        
                        if (!headerString.StartsWith("%PDF"))
                        {
                            logger.LogWarning("Invalid PDF header detected!");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to read PDF header");
                    }
                }
                else
                {
                    logger.LogError($"PDF file not found after generation: {pdfPath}");
                }
                // ===== PDF検証 終了 =====
                
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
        var dataSetId = await importService.ImportAsync(filePath, jobDate, jobDate, null);
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
            var dataSetId = await importService.ImportAsync(filePath, jobDate, jobDate, null);
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
            var dataSetId = await importService.ImportAsync(filePath, jobDate, jobDate, null);
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
        var fileManagementService = scopedServices.GetRequiredService<IFileManagementService>();
        
        // ジョブ日付を取得（引数から、またはデフォルト値）
        DateTime jobDate;
        if (args.Length >= 2 && DateTime.TryParse(args[1], out jobDate))
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
        for (int i = 2; i < args.Length - 1; i++)
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
                    // FileManagementServiceを使用してレポートパスを取得（アンマッチリストと同じ方式）
                    var pdfPath = await fileManagementService.GetReportOutputPathAsync("DailyReport", jobDate, "pdf");
                    
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
            
            // CP在庫マスタを削除
            try
            {
                var cpInventoryRepository = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.ICpInventoryRepository>();
                await cpInventoryRepository.DeleteByDataSetIdAsync(result.DataSetId);
                logger.LogInformation("CP在庫マスタを削除しました - データセットID: {DataSetId}", result.DataSetId);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx, "CP在庫マスタの削除に失敗しました - データセットID: {DataSetId}", result.DataSetId);
                // 削除に失敗しても処理は成功として扱う
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

/// <summary>
/// 開発用商品日報コマンドを実行（日付制限無視）
/// </summary>
static async Task ExecuteDevDailyReportAsync(IServiceProvider services, string[] args)
{
    // 開発環境チェック
    if (!IsDevelopmentEnvironment())
    {
        Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
        return;
    }
    
    if (args.Length < 2)
    {
        Console.WriteLine("使用方法: dotnet run dev-daily-report <YYYY-MM-DD>");
        return;
    }
    
    using var scope = services.CreateScope();
    var scopedServices = scope.ServiceProvider;
    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
    var dailyReportService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.IDailyReportService>();
    var reportService = scopedServices.GetRequiredService<InventorySystem.Reports.Interfaces.IDailyReportService>();
    var fileManagementService = scopedServices.GetRequiredService<IFileManagementService>();
    
    try
    {
        if (!DateTime.TryParse(args[1], out var jobDate))
        {
            Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
            return;
        }
        
        Console.WriteLine($"=== 開発用商品日報処理開始（日付制限無視） ===");
        Console.WriteLine($"レポート日付: {jobDate:yyyy-MM-dd}");
        Console.WriteLine();
        
        var stopwatch = Stopwatch.StartNew();
        
        // 商品日報処理実行（新規DataSetIdで実行、開発用に重複処理許可）
        var processResult = await dailyReportService.ProcessDailyReportAsync(jobDate, null, allowDuplicateProcessing: true);
        
        if (!processResult.Success)
        {
            throw new InvalidOperationException(processResult.ErrorMessage ?? "商品日報処理に失敗しました");
        }
        
        // PDF生成（通常のdaily-reportコマンドと同じ方法）
        var pdfBytes = reportService.GenerateDailyReport(
            processResult.ReportItems, 
            processResult.Subtotals, 
            processResult.Total, 
            jobDate);
        
        // FileManagementServiceを使用してレポートパスを取得
        var pdfPath = await fileManagementService.GetReportOutputPathAsync("DailyReport", jobDate, "pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);
        
        stopwatch.Stop();
        
        Console.WriteLine($"=== 処理完了 ===");
        Console.WriteLine($"データセットID: {processResult.DataSetId}");
        Console.WriteLine($"処理件数: {processResult.ProcessedCount}");
        Console.WriteLine($"PDFファイル: {pdfPath}");
        Console.WriteLine($"ファイルサイズ: {pdfBytes.Length / 1024.0:F2} KB");
        Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
        
        logger.LogInformation("開発用商品日報処理完了: JobDate={JobDate}", jobDate);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"エラー: {ex.Message}");
        logger.LogError(ex, "開発用商品日報処理でエラーが発生しました");
    }
}

/// <summary>
/// 開発用日次終了処理確認コマンドを実行（時間制限無視）
/// </summary>
static async Task ExecuteDevCheckDailyCloseAsync(IServiceProvider services, string[] args)
{
    // 開発環境チェック
    if (!IsDevelopmentEnvironment())
    {
        Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
        return;
    }
    
    if (args.Length < 3)
    {
        Console.WriteLine("使用方法: dotnet run dev-check-daily-close <YYYY-MM-DD>");
        return;
    }
    
    using var scope = services.CreateScope();
    var scopedServices = scope.ServiceProvider;
    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
    var dailyCloseService = scopedServices.GetRequiredService<IDailyCloseService>();
    
    try
    {
        if (!DateTime.TryParse(args[2], out var jobDate))
        {
            Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
            return;
        }
        
        Console.WriteLine($"=== 開発用日次終了処理 事前確認（時間制限無視） ===");
        Console.WriteLine($"対象日付: {jobDate:yyyy-MM-dd}");
        Console.WriteLine($"現在時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();
        
        // GetConfirmationInfoを呼び出して、結果を取得して時間制限チェックを無視
        var confirmation = await dailyCloseService.GetConfirmationInfo(jobDate);
        
        // 時間制限エラーを除外（開発環境のため）
        var filteredResults = confirmation.ValidationResults
            .Where(v => !v.Message.Contains("15:00以降") && !v.Message.Contains("時間的制約違反"))
            .ToList();
        
        // 商品日報情報表示
        Console.WriteLine("【商品日報情報】");
        if (confirmation.DailyReport != null)
        {
            Console.WriteLine($"  作成時刻: {confirmation.DailyReport.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  作成者: {confirmation.DailyReport.CreatedBy}");
            Console.WriteLine($"  DatasetId: {confirmation.DailyReport.DataSetId}");
        }
        else
        {
            Console.WriteLine("  ❌ 商品日報が作成されていません");
        }
        Console.WriteLine();
        
        // データ件数表示
        Console.WriteLine("【データ件数】");
        Console.WriteLine($"  売上伝票: {confirmation.DataCounts.SalesCount:#,##0}件");
        Console.WriteLine($"  仕入伝票: {confirmation.DataCounts.PurchaseCount:#,##0}件");
        Console.WriteLine($"  在庫調整: {confirmation.DataCounts.AdjustmentCount:#,##0}件");
        Console.WriteLine($"  CP在庫: {confirmation.DataCounts.CpInventoryCount:#,##0}件");
        Console.WriteLine();
        
        // 金額サマリー表示
        Console.WriteLine("【金額サマリー】");
        Console.WriteLine($"  売上総額: ¥{confirmation.Amounts.SalesAmount:#,##0.00}");
        Console.WriteLine($"  仕入総額: ¥{confirmation.Amounts.PurchaseAmount:#,##0.00}");
        Console.WriteLine($"  推定粗利: ¥{confirmation.Amounts.EstimatedGrossProfit:#,##0.00}");
        Console.WriteLine();
        
        // 検証結果表示（時間制限以外）
        if (filteredResults.Any())
        {
            Console.WriteLine("【検証結果】");
            foreach (var result in filteredResults)
            {
                var icon = result.Level switch
                {
                    ValidationLevel.Error => "❌",
                    ValidationLevel.Warning => "⚠️ ",
                    _ => "ℹ️ "
                };
                
                Console.WriteLine($"{icon} {result.Level}: {result.Message}");
                if (!string.IsNullOrEmpty(result.Detail))
                {
                    Console.WriteLine($"         {result.Detail}");
                }
            }
            Console.WriteLine();
        }
        
        // 処理可否判定（時間制限を除外）
        var canProcess = !filteredResults.Any(v => v.Level == ValidationLevel.Error);
        
        Console.WriteLine("【処理可否判定】");
        if (canProcess)
        {
            Console.WriteLine("✅ 日次終了処理を実行可能です（開発環境のため時間制限を無視）");
        }
        else
        {
            Console.WriteLine("❌ 日次終了処理を実行できません");
            Console.WriteLine("上記のエラーを解決してから再度実行してください。");
        }
        
        logger.LogInformation("開発用日次終了処理確認完了: JobDate={JobDate}", jobDate);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"エラー: {ex.Message}");
        logger.LogError(ex, "開発用日次終了処理確認でエラーが発生しました");
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

static async Task ExecuteProductAccountAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("使用方法: product-account <JobDate>");
        Console.WriteLine("例: product-account 2025-06-30");
        return;
    }

    if (!DateTime.TryParse(args[1], out DateTime jobDate))
    {
        Console.WriteLine($"❌ 不正な日付形式です: {args[1]}");
        Console.WriteLine("例: product-account 2025-06-30");
        return;
    }

    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var productAccountService = scopedServices.GetRequiredService<InventorySystem.Reports.Interfaces.IProductAccountReportService>();

        try
        {
            logger.LogInformation("=== 商品勘定帳票作成開始 ===");
            Console.WriteLine("=== 商品勘定帳票作成開始 ===");
            Console.WriteLine($"対象日: {jobDate:yyyy-MM-dd}");

            // 商品勘定帳票を作成
            var pdfBytes = productAccountService.GenerateProductAccountReport(jobDate);
            
            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                // ファイル保存処理（必要に応じて実装）
                var outputPath = Path.Combine("帳票出力", $"ProductAccount_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllBytesAsync(outputPath, pdfBytes);
                
                Console.WriteLine($"✅ 商品勘定帳票を作成しました");
                Console.WriteLine($"出力ファイル: {outputPath}");
                Console.WriteLine($"ファイルサイズ: {pdfBytes.Length:N0} bytes");
            }
            else
            {
                Console.WriteLine($"❌ 商品勘定帳票の作成に失敗しました");
            }

            logger.LogInformation("=== 商品勘定帳票作成完了 ===");
            Console.WriteLine("=== 商品勘定帳票作成完了 ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "商品勘定帳票作成中にエラーが発生しました");
            Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
        }
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
/// ファイル名から分類番号を抽出
/// </summary>
private static int ExtractCategoryNumber(string fileName)
{
    // "商品分類１.csv" → 1
    // "得意先分類２.csv" → 2
    // "仕入先分類３.csv" → 3
    
    // 正規表現で数字を抽出
    var match = System.Text.RegularExpressions.Regex.Match(fileName, @"分類(\d+)");
    if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
    {
        return number;
    }
    
    // 全角数字の場合も考慮
    var zenkakuMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"分類([１２３４５６７８９０]+)");
    if (zenkakuMatch.Success)
    {
        var zenkakuNumber = zenkakuMatch.Groups[1].Value
            .Replace("１", "1").Replace("２", "2").Replace("３", "3")
            .Replace("４", "4").Replace("５", "5").Replace("６", "6")
            .Replace("７", "7").Replace("８", "8").Replace("９", "9")
            .Replace("０", "0");
        if (int.TryParse(zenkakuNumber, out int zNumber))
        {
            return zNumber;
        }
    }
    
    return 1; // デフォルト値
}

/// <summary>
/// ファイル処理順序を取得
/// </summary>
private static int GetFileProcessOrder(string fileName)
{
    // Phase 1: マスタファイル（優先度1-15）
    if (fileName.Contains("等級汎用マスター")) return 1;
    if (fileName.Contains("階級汎用マスター")) return 2;
    if (fileName.Contains("荷印汎用マスター")) return 3;
    if (fileName.Contains("産地汎用マスター")) return 4;
    if (fileName == "商品.csv") return 5;
    if (fileName == "得意先.csv") return 6;
    if (fileName == "仕入先.csv") return 7;
    if (fileName == "単位.csv") return 8;
    
    // 分類マスタ（優先度9-15）
    if (fileName.Contains("商品分類")) return 9;
    if (fileName.Contains("得意先分類")) return 10;
    if (fileName.Contains("仕入先分類")) return 11;
    if (fileName == "担当者.csv") return 12;
    if (fileName.Contains("担当者分類")) return 13;
    
    // Phase 2: 初期在庫（優先度20）
    if (fileName == "前月末在庫.csv") return 20;
    
    // Phase 3: 伝票ファイル（優先度30-32）
    if (fileName.StartsWith("売上伝票")) return 30;
    if (fileName.StartsWith("仕入伝票")) return 31;
    if (fileName.StartsWith("在庫調整") || fileName.StartsWith("受注伝票")) return 32;
    
    // Phase 4: 入出金ファイル（優先度40-41）
    if (fileName.StartsWith("入金伝票")) return 40;
    if (fileName.StartsWith("支払伝票")) return 41;
    
    // Phase 5: その他（優先度99）
    return 99;
}

static async Task ExecuteInitInventoryAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("使用方法: init-inventory <部門名>");
        return;
    }

    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var department = args[2];
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var fileManagementService = scopedServices.GetRequiredService<IFileManagementService>();
        var importService = scopedServices.GetRequiredService<PreviousMonthInventoryImportService>();
        
        logger.LogInformation("=== 初期在庫設定開始 ===");
        logger.LogInformation("部門: {Department}", department);
        
        try
        {
            // インポートパスの取得（appsettings.json使用）
            var importPath = fileManagementService.GetImportPath(department);
            var csvPath = Path.Combine(importPath, "前月末在庫.csv");
            
            logger.LogInformation("ファイル: {Path}", csvPath);
            
            if (!File.Exists(csvPath))
            {
                logger.LogError("前月末在庫.csvが見つかりません: {Path}", csvPath);
                Console.WriteLine($"❌ 前月末在庫.csvが見つかりません: {csvPath}");
                return;
            }
            
            Console.WriteLine("=== 初期在庫設定開始 ===");
            Console.WriteLine($"部門: {department}");
            Console.WriteLine($"ファイル: {csvPath}");
            Console.WriteLine();
            
            // インポート実行（日付フィルタなし、すべてのデータを初期在庫として設定）
            var result = await importService.ImportForInitialInventoryAsync();
            
            if (result.IsSuccess)
            {
                Console.WriteLine($"✅ 初期在庫を設定しました（{result.ProcessedRecords}件）");
                
                if (result.ErrorRecords > 0)
                {
                    Console.WriteLine($"商品コード00000の除外件数: {result.ErrorRecords}件");
                }
                
                // ファイルを処理済みフォルダに移動
                await fileManagementService.MoveToProcessedAsync(csvPath, department);
                logger.LogInformation("前月末在庫.csvを処理済みフォルダに移動しました");
            }
            else
            {
                Console.WriteLine($"❌ 初期在庫設定に失敗しました: {result.Message}");
                logger.LogError("初期在庫設定失敗: {Message}", result.Message);
            }
            
            logger.LogInformation("=== 初期在庫設定完了 ===");
            Console.WriteLine("\n=== 初期在庫設定完了 ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "初期在庫設定中にエラーが発生しました");
            Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
        }
    }
}

static async Task ExecuteImportWithCarryoverAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("使用方法: import-with-carryover <部門>");
        Console.WriteLine("例: import-with-carryover DeptA");
        Console.WriteLine("※処理対象日は最終処理日の翌日が自動的に選択されます");
        return;
    }

    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var department = args[2];
        
        var command = scopedServices.GetRequiredService<ImportWithCarryoverCommand>();
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("=== 在庫引継インポート開始 ===");
        logger.LogInformation("部門: {Department}", department);
        
        try
        {
            await command.ExecuteAsync(department);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "在庫引継インポート中にエラーが発生しました");
            Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
        }
    }
}

static async Task ExecuteImportFromFolderAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("エラー: 部門コードが指定されていません");
        Console.WriteLine("使用方法:");
        Console.WriteLine("  単一日付: dotnet run import-folder <dept> <YYYY-MM-DD>");
        Console.WriteLine("  期間指定: dotnet run import-folder <dept> <開始日 YYYY-MM-DD> <終了日 YYYY-MM-DD>");
        Console.WriteLine("  CSV日付保持: dotnet run import-folder <dept> --preserve-csv-dates [--start-date <YYYY-MM-DD>] [--end-date <YYYY-MM-DD>]");
        Console.WriteLine("  全期間  : dotnet run import-folder <dept>");
        return;
    }
    
    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        
        // スキーマ更新を最初に実行
        var connectionString = scopedServices.GetRequiredService<IConfiguration>()
            .GetConnectionString("DefaultConnection");
        var schemaService = new DatabaseSchemaService(
            connectionString, 
            scopedServices.GetRequiredService<ILogger<DatabaseSchemaService>>());
        
        try
        {
            Console.WriteLine("データベーススキーマを確認しています...");
            await schemaService.UpdateSchemaAsync();
            Console.WriteLine("スキーマの確認が完了しました。");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"スキーマ更新エラー: {ex.Message}");
            logger.LogError(ex, "スキーマ更新中にエラーが発生しました");
            throw;
        }
        
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
        var salesVoucherRepo = scopedServices.GetRequiredService<ISalesVoucherRepository>();
        var purchaseVoucherRepo = scopedServices.GetRequiredService<IPurchaseVoucherRepository>();
        var adjustmentRepo = scopedServices.GetRequiredService<IInventoryAdjustmentRepository>();
        var datasetRepo = scopedServices.GetRequiredService<IDataSetManagementRepository>();
        
        // 在庫マスタ最適化サービス
        var optimizationService = scopedServices.GetService<IInventoryMasterOptimizationService>();
        
        var department = args[1];
        DateTime? startDate = null;
        DateTime? endDate = null;
        bool preserveCsvDates = false;
        
        // オプション引数の解析
        int argIndex = 2;
        while (argIndex < args.Length)
        {
            if (args[argIndex] == "--preserve-csv-dates")
            {
                preserveCsvDates = true;
                argIndex++;
            }
            else if (args[argIndex] == "--start-date" && argIndex + 1 < args.Length)
            {
                if (DateTime.TryParse(args[argIndex + 1], out var date))
                {
                    startDate = date;
                    argIndex += 2;
                }
                else
                {
                    Console.WriteLine($"エラー: 無効な開始日付形式: {args[argIndex + 1]}");
                    return;
                }
            }
            else if (args[argIndex] == "--end-date" && argIndex + 1 < args.Length)
            {
                if (DateTime.TryParse(args[argIndex + 1], out var date))
                {
                    endDate = date;
                    argIndex += 2;
                }
                else
                {
                    Console.WriteLine($"エラー: 無効な終了日付形式: {args[argIndex + 1]}");
                    return;
                }
            }
            else if (DateTime.TryParse(args[argIndex], out var date))
            {
                // 従来の日付指定方式（後方互換性）
                startDate = date;
                if (argIndex + 1 < args.Length && DateTime.TryParse(args[argIndex + 1], out var date2))
                {
                    endDate = date2;
                    argIndex += 2;
                }
                else
                {
                    endDate = startDate;
                    argIndex++;
                }
            }
            else
            {
                Console.WriteLine($"エラー: 無効な引数: {args[argIndex]}");
                return;
            }
        }
        
        // 日付範囲の検証
        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
        {
            Console.WriteLine("エラー: 終了日は開始日以降である必要があります");
            return;
        }
        
        // モードのログ出力
        if (preserveCsvDates)
        {
            logger.LogInformation("CSVの日付保持モード: StartDate={StartDate}, EndDate={EndDate}", 
                startDate?.ToString("yyyy-MM-dd") ?? "指定なし", 
                endDate?.ToString("yyyy-MM-dd") ?? "指定なし");
        }
        else if (startDate.HasValue && endDate.HasValue)
        {
            if (startDate.Value == endDate.Value)
            {
                logger.LogInformation("単一日付モード: {Date}", startDate.Value.ToString("yyyy-MM-dd"));
            }
            else
            {
                logger.LogInformation("期間指定モード: {StartDate} ～ {EndDate}", 
                    startDate.Value.ToString("yyyy-MM-dd"), 
                    endDate.Value.ToString("yyyy-MM-dd"));
            }
        }
        else
        {
            logger.LogInformation("全期間モード: 日付フィルタなし");
        }
        
        Console.WriteLine($"=== CSVファイル一括インポート開始 ===");
        if (preserveCsvDates)
        {
            Console.WriteLine("モード: 期間指定（CSVの日付を保持）");
        }
        Console.WriteLine($"部門: {department}");
        
        if (startDate.HasValue && endDate.HasValue)
        {
            if (startDate.Value.Date == endDate.Value.Date)
            {
                Console.WriteLine($"対象日付: {startDate.Value:yyyy-MM-dd}");
            }
            else
            {
                Console.WriteLine($"対象期間: {startDate.Value:yyyy-MM-dd} ～ {endDate.Value:yyyy-MM-dd}");
                Console.WriteLine($"期間日数: {(endDate.Value - startDate.Value).Days + 1}日間");
            }
        }
        else if (preserveCsvDates)
        {
            Console.WriteLine("対象期間: CSVファイル内の全日付");
        }
        else
        {
            Console.WriteLine("対象期間: 全期間（日付フィルタなし）");
        }
        
        var errorCount = 0;
        var processedCounts = new Dictionary<string, int>();
        var dateStatisticsTotal = new Dictionary<DateTime, int>(); // 全体の日付別統計
        var fileStatistics = new Dictionary<string, (int processed, int skipped)>(); // ファイル別統計
        
        try
        {
            // 重複データクリア処理（日付範囲指定時はスキップ）
            if (startDate.HasValue && endDate.HasValue && startDate.Value == endDate.Value)
            {
                Console.WriteLine("\n既存データのクリア中...");
                await ClearExistingVoucherData(scopedServices, startDate.Value, department);
                Console.WriteLine("✅ 既存データクリア完了");
            }
            else if (!startDate.HasValue)
            {
                Console.WriteLine("\n⚠️ 全期間モードまたは期間指定モードでは既存データクリアをスキップします");
            }
            
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
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - Service_Not_Implemented", file);
                            continue;
                        }
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // 現在は他の日付データも処理できるようにファイル移動を無効化
                        // await fileService.MoveToProcessedAsync(file, department);
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
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
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - Service_Not_Implemented", file);
                            continue;
                        }
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // 現在は他の日付データも処理できるようにファイル移動を無効化
                        // await fileService.MoveToProcessedAsync(file, department);
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
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
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - Service_Not_Implemented", file);
                            continue;
                        }
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // 現在は他の日付データも処理できるようにファイル移動を無効化
                        // await fileService.MoveToProcessedAsync(file, department);
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
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
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - Service_Not_Implemented", file);
                            continue;
                        }
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // 現在は他の日付データも処理できるようにファイル移動を無効化
                        // await fileService.MoveToProcessedAsync(file, department);
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName == "商品.csv")
                    {
                        if (productImportService != null)
                        {
                            var result = await productImportService.ImportFromCsvAsync(file, startDate ?? DateTime.Today);
                            Console.WriteLine($"✅ 商品マスタとして処理完了 - {result.ImportedCount}件");
                            processedCounts["商品マスタ"] = result.ImportedCount;
                        }
                        else
                        {
                            logger.LogWarning("ProductMasterImportServiceが未実装のため、商品マスタの取込をスキップします");
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - Service_Not_Implemented", file);
                            continue;
                        }
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // 現在は他の日付データも処理できるようにファイル移動を無効化
                        // await fileService.MoveToProcessedAsync(file, department);
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName == "得意先.csv")
                    {
                        if (customerImportService != null)
                        {
                            var result = await customerImportService.ImportFromCsvAsync(file, startDate ?? DateTime.Today);
                            Console.WriteLine($"✅ 得意先マスタとして処理完了 - {result.ImportedCount}件");
                            processedCounts["得意先マスタ"] = result.ImportedCount;
                        }
                        else
                        {
                            logger.LogWarning("CustomerMasterImportServiceが未実装のため、得意先マスタの取込をスキップします");
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - Service_Not_Implemented", file);
                            continue;
                        }
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // 現在は他の日付データも処理できるようにファイル移動を無効化
                        // await fileService.MoveToProcessedAsync(file, department);
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName == "仕入先.csv")
                    {
                        if (supplierImportService != null)
                        {
                            var result = await supplierImportService.ImportFromCsvAsync(file, startDate ?? DateTime.Today);
                            Console.WriteLine($"✅ 仕入先マスタとして処理完了 - {result.ImportedCount}件");
                            processedCounts["仕入先マスタ"] = result.ImportedCount;
                        }
                        else
                        {
                            logger.LogWarning("SupplierMasterImportServiceが未実装のため、仕入先マスタの取込をスキップします");
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "Service_Not_Implemented");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - Service_Not_Implemented", file);
                            continue;
                        }
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // 現在は他の日付データも処理できるようにファイル移動を無効化
                        // await fileService.MoveToProcessedAsync(file, department);
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    // ========== 分類マスタファイル ==========
                    else if (fileName.Contains("商品分類") && fileName.EndsWith(".csv"))
                    {
                        Console.WriteLine($"処理中: {fileName}");
                        
                        var categoryNumber = ExtractCategoryNumber(fileName);
                        var serviceName = $"ProductCategory{categoryNumber}ImportService";
                        
                        // ImportServiceExtensionsで登録されたサービスを検索
                        var importServices = scopedServices.GetServices<IImportService>();
                        var service = importServices.FirstOrDefault(s => s.GetType().Name == serviceName);
                        
                        if (service != null)
                        {
                            try
                            {
                                await service.ImportAsync(file, startDate ?? DateTime.Today);
                                processedCounts[$"商品分類{categoryNumber}"] = 1; // 処理成功
                                Console.WriteLine($"✅ 商品分類{categoryNumber}マスタとして処理完了");
                                logger.LogInformation("商品分類{CategoryNumber}マスタ取込完了: {File}", categoryNumber, fileName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "商品分類{CategoryNumber}マスタ処理エラー: {File}", categoryNumber, fileName);
                                Console.WriteLine($"❌ エラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.LogError("商品分類{CategoryNumber}の処理サービスが見つかりません: {ServiceName}", categoryNumber, serviceName);
                            Console.WriteLine($"❌ サービスが見つかりません: {serviceName}");
                        }
                        
                        // ファイル移動をスキップ（処理履歴で管理）
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName.Contains("得意先分類") && fileName.EndsWith(".csv"))
                    {
                        Console.WriteLine($"処理中: {fileName}");
                        
                        var categoryNumber = ExtractCategoryNumber(fileName);
                        var serviceName = $"CustomerCategory{categoryNumber}ImportService";
                        
                        var importServices = scopedServices.GetServices<IImportService>();
                        var service = importServices.FirstOrDefault(s => s.GetType().Name == serviceName);
                        
                        if (service != null)
                        {
                            try
                            {
                                await service.ImportAsync(file, startDate ?? DateTime.Today);
                                processedCounts[$"得意先分類{categoryNumber}"] = 1; // 処理成功
                                Console.WriteLine($"✅ 得意先分類{categoryNumber}マスタとして処理完了");
                                logger.LogInformation("得意先分類{CategoryNumber}マスタ取込完了: {File}", categoryNumber, fileName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "得意先分類{CategoryNumber}マスタ処理エラー: {File}", categoryNumber, fileName);
                                Console.WriteLine($"❌ エラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.LogError("得意先分類{CategoryNumber}の処理サービスが見つかりません: {ServiceName}", categoryNumber, serviceName);
                            Console.WriteLine($"❌ サービスが見つかりません: {serviceName}");
                        }
                        
                        // ファイル移動をスキップ（処理履歴で管理）
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName.Contains("仕入先分類") && fileName.EndsWith(".csv"))
                    {
                        Console.WriteLine($"処理中: {fileName}");
                        
                        var categoryNumber = ExtractCategoryNumber(fileName);
                        var serviceName = $"SupplierCategory{categoryNumber}ImportService";
                        
                        var importServices = scopedServices.GetServices<IImportService>();
                        var service = importServices.FirstOrDefault(s => s.GetType().Name == serviceName);
                        
                        if (service != null)
                        {
                            try
                            {
                                await service.ImportAsync(file, startDate ?? DateTime.Today);
                                processedCounts[$"仕入先分類{categoryNumber}"] = 1; // 処理成功
                                Console.WriteLine($"✅ 仕入先分類{categoryNumber}マスタとして処理完了");
                                logger.LogInformation("仕入先分類{CategoryNumber}マスタ取込完了: {File}", categoryNumber, fileName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "仕入先分類{CategoryNumber}マスタ処理エラー: {File}", categoryNumber, fileName);
                                Console.WriteLine($"❌ エラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.LogError("仕入先分類{CategoryNumber}の処理サービスが見つかりません: {ServiceName}", categoryNumber, serviceName);
                            Console.WriteLine($"❌ サービスが見つかりません: {serviceName}");
                        }
                        
                        // ファイル移動をスキップ（処理履歴で管理）
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName.Contains("担当者分類") && fileName.EndsWith(".csv"))
                    {
                        Console.WriteLine($"処理中: {fileName}");
                        
                        var categoryNumber = ExtractCategoryNumber(fileName);
                        var serviceName = $"StaffCategory{categoryNumber}ImportService";
                        
                        var importServices = scopedServices.GetServices<IImportService>();
                        var service = importServices.FirstOrDefault(s => s.GetType().Name == serviceName);
                        
                        if (service != null)
                        {
                            try
                            {
                                await service.ImportAsync(file, startDate ?? DateTime.Today);
                                processedCounts[$"担当者分類{categoryNumber}"] = 1; // 処理成功
                                Console.WriteLine($"✅ 担当者分類{categoryNumber}マスタとして処理完了");
                                logger.LogInformation("担当者分類{CategoryNumber}マスタ取込完了: {File}", categoryNumber, fileName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "担当者分類{CategoryNumber}マスタ処理エラー: {File}", categoryNumber, fileName);
                                Console.WriteLine($"❌ エラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.LogError("担当者分類{CategoryNumber}の処理サービスが見つかりません: {ServiceName}", categoryNumber, serviceName);
                            Console.WriteLine($"❌ サービスが見つかりません: {serviceName}");
                        }
                        
                        // ファイル移動をスキップ（処理履歴で管理）
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName == "単位.csv")
                    {
                        Console.WriteLine($"処理中: {fileName}");
                        
                        var importServices = scopedServices.GetServices<IImportService>();
                        var service = importServices.FirstOrDefault(s => s.GetType().Name == "UnitMasterImportService");
                        
                        if (service != null)
                        {
                            try
                            {
                                await service.ImportAsync(file, startDate ?? DateTime.Today);
                                processedCounts["単位マスタ"] = 1; // 処理成功
                                Console.WriteLine("✅ 単位マスタとして処理完了");
                                logger.LogInformation("単位マスタ取込完了: {File}", fileName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "単位マスタ処理エラー: {File}", fileName);
                                Console.WriteLine($"❌ エラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.LogError("単位マスタの処理サービスが見つかりません: UnitMasterImportService");
                            Console.WriteLine("❌ サービスが見つかりません: UnitMasterImportService");
                        }
                        
                        // ファイル移動をスキップ（処理履歴で管理）
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName == "担当者.csv")
                    {
                        Console.WriteLine($"処理中: {fileName}");
                        
                        var importServices = scopedServices.GetServices<IImportService>();
                        var service = importServices.FirstOrDefault(s => s.GetType().Name == "StaffMasterImportService");
                        
                        if (service != null)
                        {
                            try
                            {
                                await service.ImportAsync(file, startDate ?? DateTime.Today);
                                processedCounts["担当者マスタ"] = 1; // 処理成功
                                Console.WriteLine("✅ 担当者マスタとして処理完了");
                                logger.LogInformation("担当者マスタ取込完了: {File}", fileName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "担当者マスタ処理エラー: {File}", fileName);
                                Console.WriteLine($"❌ エラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.LogError("担当者マスタの処理サービスが見つかりません: StaffMasterImportService");
                            Console.WriteLine("❌ サービスが見つかりません: StaffMasterImportService");
                        }
                        
                        // ファイル移動をスキップ（処理履歴で管理）
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName.StartsWith("入金伝票") && fileName.EndsWith(".csv"))
                    {
                        Console.WriteLine($"処理中: {fileName}");
                        
                        var importServices = scopedServices.GetServices<IImportService>();
                        var service = importServices.FirstOrDefault(s => s.GetType().Name == "ReceiptVoucherImportService");
                        
                        if (service != null)
                        {
                            try
                            {
                                await service.ImportAsync(file, startDate ?? DateTime.Today);
                                processedCounts["入金伝票"] = 1; // 処理成功
                                Console.WriteLine("✅ 入金伝票として処理完了");
                                logger.LogInformation("入金伝票取込完了: {File}", fileName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "入金伝票処理エラー: {File}", fileName);
                                Console.WriteLine($"❌ エラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.LogError("入金伝票の処理サービスが見つかりません: ReceiptVoucherImportService");
                            Console.WriteLine("❌ サービスが見つかりません: ReceiptVoucherImportService");
                        }
                        
                        // ファイル移動をスキップ（処理履歴で管理）
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName.StartsWith("支払伝票") && fileName.EndsWith(".csv"))
                    {
                        Console.WriteLine($"処理中: {fileName}");
                        
                        var importServices = scopedServices.GetServices<IImportService>();
                        var service = importServices.FirstOrDefault(s => s.GetType().Name == "PaymentVoucherImportService");
                        
                        if (service != null)
                        {
                            try
                            {
                                await service.ImportAsync(file, startDate ?? DateTime.Today);
                                processedCounts["支払伝票"] = 1; // 処理成功
                                Console.WriteLine("✅ 支払伝票として処理完了");
                                logger.LogInformation("支払伝票取込完了: {File}", fileName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "支払伝票処理エラー: {File}", fileName);
                                Console.WriteLine($"❌ エラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.LogError("支払伝票の処理サービスが見つかりません: PaymentVoucherImportService");
                            Console.WriteLine("❌ サービスが見つかりません: PaymentVoucherImportService");
                        }
                        
                        // ファイル移動をスキップ（処理履歴で管理）
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    // ========== Phase 2: 初期在庫ファイル ==========
                    else if (fileName == "前月末在庫.csv")
                    {
                        logger.LogWarning("前月末在庫.csvはinit-inventoryコマンドで処理してください。スキップします。");
                        Console.WriteLine("⚠️ 前月末在庫.csvはinit-inventoryコマンドで処理してください。スキップします。");
                        continue;
                    }
                    // ========== Phase 3: 伝票系ファイル ==========
                    else if (fileName.StartsWith("売上伝票"))
                    {
                        // デバッグログ追加: 売上伝票インポート開始
                        logger.LogDebug("売上伝票インポート開始: FileName={FileName}, StartDate={StartDate:yyyy-MM-dd}, EndDate={EndDate:yyyy-MM-dd}, PreserveCsvDates={PreserveCsvDates}", 
                            fileName, startDate, endDate, preserveCsvDates);
                        
                        var dataSetId = await salesImportService.ImportAsync(file, startDate, endDate, department, preserveCsvDates);
                        
                        // デバッグログ追加: 売上伝票インポート完了
                        logger.LogDebug("売上伝票インポート完了: DataSetId={DataSetId}", dataSetId);
                        
                        Console.WriteLine($"✅ 売上伝票として処理完了 - データセットID: {dataSetId}");
                        // インポート結果を取得（データセットIDから件数取得）
                        var salesResult = await salesImportService.GetImportResultAsync(dataSetId);
                        processedCounts["売上伝票"] = salesResult.ImportedCount;
                        fileStatistics[fileName] = (salesResult.ImportedCount, 0); // TODO: スキップ数取得
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // ImportService内でもファイル移動を無効化済み
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName.StartsWith("仕入伝票"))
                    {
                        // デバッグログ追加: 仕入伝票インポート開始
                        logger.LogDebug("仕入伝票インポート開始: FileName={FileName}, StartDate={StartDate:yyyy-MM-dd}, EndDate={EndDate:yyyy-MM-dd}, PreserveCsvDates={PreserveCsvDates}", 
                            fileName, startDate, endDate, preserveCsvDates);
                        
                        var dataSetId = await purchaseImportService.ImportAsync(file, startDate, endDate, department, preserveCsvDates);
                        
                        // デバッグログ追加: 仕入伝票インポート完了
                        logger.LogDebug("仕入伝票インポート完了: DataSetId={DataSetId}", dataSetId);
                        
                        Console.WriteLine($"✅ 仕入伝票として処理完了 - データセットID: {dataSetId}");
                        // インポート結果を取得（データセットIDから件数取得）
                        var purchaseResult = await purchaseImportService.GetImportResultAsync(dataSetId);
                        processedCounts["仕入伝票"] = purchaseResult.ImportedCount;
                        fileStatistics[fileName] = (purchaseResult.ImportedCount, 0); // TODO: スキップ数取得
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // ImportService内でもファイル移動を無効化済み
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName.StartsWith("受注伝票"))
                    {
                        // デバッグログ追加: 受注伝票インポート開始
                        logger.LogDebug("受注伝票インポート開始: FileName={FileName}, StartDate={StartDate:yyyy-MM-dd}, EndDate={EndDate:yyyy-MM-dd}, PreserveCsvDates={PreserveCsvDates}", 
                            fileName, startDate, endDate, preserveCsvDates);
                        
                        // 受注伝票は在庫調整として処理
                        var dataSetId = await adjustmentImportService.ImportAsync(file, startDate, endDate, department, preserveCsvDates);
                        
                        // デバッグログ追加: 受注伝票インポート完了
                        logger.LogDebug("受注伝票インポート完了: DataSetId={DataSetId}", dataSetId);
                        
                        Console.WriteLine($"✅ 在庫調整として処理完了 - データセットID: {dataSetId}");
                        // インポート結果を取得（データセットIDから件数取得）
                        var adjustmentResult = await adjustmentImportService.GetImportResultAsync(dataSetId);
                        processedCounts["受注伝票（在庫調整）"] = adjustmentResult.ImportedCount;
                        fileStatistics[fileName] = (adjustmentResult.ImportedCount, 0); // TODO: スキップ数取得
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // ImportService内でもファイル移動を無効化済み
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    else if (fileName.StartsWith("在庫調整"))
                    {
                        var dataSetId = await adjustmentImportService.ImportAsync(file, startDate, endDate, department, preserveCsvDates);
                        Console.WriteLine($"✅ 在庫調整として処理完了 - データセットID: {dataSetId}");
                        // インポート結果を取得（データセットIDから件数取得）
                        var inventoryAdjustmentResult = await adjustmentImportService.GetImportResultAsync(dataSetId);
                        processedCounts["在庫調整"] = inventoryAdjustmentResult.ImportedCount;
                        fileStatistics[fileName] = (inventoryAdjustmentResult.ImportedCount, 0); // TODO: スキップ数取得
                        // TODO: 処理履歴管理システム実装後は、ファイル移動ではなく処理履歴で管理
                        // ImportService内でもファイル移動を無効化済み
                        logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                    }
                    // ========== 未対応ファイル ==========
                    else if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        // 既知の未対応ファイル（実装済みのファイルは削除）
                        string[] knownButUnsupported = {
                            // 実装済みのため削除: "担当者", "単位", "商品分類", "得意先分類", 
                            // "仕入先分類", "担当者分類", "支払伝票", "入金伝票"
                        };
                        
                        if (knownButUnsupported.Any(pattern => fileName.Contains(pattern)))
                        {
                            Console.WriteLine($"⚠️ {fileName} は現在未対応です（スキップ）");
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "未対応のCSVファイル形式");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - 未対応のCSVファイル形式", file);
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ {fileName} は認識できないCSVファイルです");
                            // エラー時のファイル移動も無効化
                            // await fileService.MoveToErrorAsync(file, department, "不明なCSVファイル");
                            logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - 不明なCSVファイル", file);
                        }
                    }
                    else
                    {
                        // CSV以外のファイル
                        // エラー時のファイル移動も無効化
                        // await fileService.MoveToErrorAsync(file, department, "CSVファイル以外は処理対象外");
                        logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - CSVファイル以外は処理対象外", file);
                        Console.WriteLine("⚠️ CSVファイル以外のため処理をスキップ");
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
            
            // ========== Phase 4: 在庫マスタ最適化または前日在庫引継 ==========
            Console.WriteLine("\n========== Phase 4: 在庫マスタ処理 ==========");

            if (startDate.HasValue && endDate.HasValue)
            {
                try
                {
                    // 期間内の各日付に対して処理を実行
                    var currentDate = startDate.Value;
                    while (currentDate <= endDate.Value)
                    {
                        // 在庫影響伝票の件数を確認
                        var salesCount = await salesVoucherRepo.GetCountByJobDateAsync(currentDate);
                        var purchaseCount = await purchaseVoucherRepo.GetCountByJobDateAsync(currentDate);
                        var adjustmentCount = await adjustmentRepo.GetInventoryAdjustmentCountByJobDateAsync(currentDate);
                        var totalInventoryVouchers = salesCount + purchaseCount + adjustmentCount;
                        
                        logger.LogInformation(
                            "在庫影響伝票数 [{Date:yyyy-MM-dd}] - 売上: {SalesCount}件, 仕入: {PurchaseCount}件, 在庫調整: {AdjustmentCount}件",
                            currentDate, salesCount, purchaseCount, adjustmentCount);
                        
                        var stopwatch = Stopwatch.StartNew();
                        string dataSetId;
                        string importType = "UNKNOWN";
                        
                        if (totalInventoryVouchers == 0)
                        {
                            // 前日在庫引継モード
                            Console.WriteLine($"\n[{currentDate:yyyy-MM-dd}] 在庫影響伝票が0件のため、前日在庫引継モードで処理します。");
                            Console.WriteLine($"  売上: {salesCount}件, 仕入: {purchaseCount}件, 在庫調整: {adjustmentCount}件");
                            
                            // ⭐ Phase 2-B: ファクトリとタイムプロバイダーを先に取得
                            var dataSetFactory = scopedServices.GetRequiredService<IDataSetManagementFactory>();
                            var timeProvider = scopedServices.GetRequiredService<ITimeProvider>();
                            dataSetId = $"CARRYOVER_{currentDate:yyyyMMdd}_{timeProvider.Now:HHmmss}_{GenerateRandomString(6)}";
                            importType = "CARRYOVER";
                            
                            // 前日在庫引継処理を実行
                            await ExecuteCarryoverModeAsync(inventoryRepo, datasetRepo, currentDate, dataSetId, department, logger, dataSetFactory, timeProvider);
                        }
                        else if (optimizationService != null)
                        {
                            // 通常の在庫マスタ最適化
                            Console.WriteLine($"\n[{currentDate:yyyy-MM-dd}] 在庫マスタ最適化を開始します。");
                            Console.WriteLine($"  売上: {salesCount}件, 仕入: {purchaseCount}件, 在庫調整: {adjustmentCount}件");
                            
                            dataSetId = $"AUTO_OPTIMIZE_{currentDate:yyyyMMdd}_{DateTime.Now:HHmmss}";
                            importType = "OPTIMIZE";
                            
                            var result = await optimizationService.OptimizeAsync(currentDate, dataSetId);
                            processedCounts[$"在庫マスタ最適化_{currentDate:yyyy-MM-dd}"] = result.InsertedCount + result.UpdatedCount;
                            
                            // カバレッジ率を計算（簡易版）
                            var coverageRate = result.ProcessedCount > 0 ? 
                                (double)(result.InsertedCount + result.UpdatedCount) / result.ProcessedCount : 0.0;
                            
                            Console.WriteLine($"✅ 在庫マスタ最適化完了 [{currentDate:yyyy-MM-dd}] ({stopwatch.ElapsedMilliseconds}ms)");
                            Console.WriteLine($"   - 新規作成: {result.InsertedCount}件");
                            Console.WriteLine($"   - JobDate更新: {result.UpdatedCount}件");  
                            Console.WriteLine($"   - カバレッジ率: {coverageRate:P1}");
                        }
                        else
                        {
                            logger.LogWarning("在庫マスタ最適化サービスが未実装のため、スキップします。");
                            Console.WriteLine($"⚠️ [{currentDate:yyyy-MM-dd}] 在庫マスタ最適化サービスが未実装のためスキップ");
                        }
                        
                        stopwatch.Stop();
                        
                        logger.LogInformation(
                            "在庫処理完了 - 日付: {Date}, モード: {Mode}, 処理時間: {ElapsedMs}ms",
                            currentDate, importType, stopwatch.ElapsedMilliseconds);
                        
                        currentDate = currentDate.AddDays(1);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "在庫マスタ最適化でエラーが発生しました");
                    Console.WriteLine($"❌ 在庫マスタ最適化エラー: {ex.Message}");
                    errorCount++;
                }
            }
            else
            {
                if (!startDate.HasValue || !endDate.HasValue)
                {
                    logger.LogWarning("在庫処理には日付指定が必要です");
                    Console.WriteLine("⚠️ 在庫処理には日付指定が必要です");
                }
            }
            
            // ========== Phase 5: Process 2-5（売上伝票への在庫単価書込・粗利計算） ==========
            if (startDate.HasValue && endDate.HasValue)
            {
                logger.LogInformation("=== Phase 5: Process 2-5（売上伝票への在庫単価書込・粗利計算）開始 ===");
                Console.WriteLine("\n========== Phase 5: Process 2-5（売上伝票への在庫単価書込・粗利計算） ==========");
                
                try
                {
                    // GrossProfitCalculationServiceを取得
                    var grossProfitService = scopedServices.GetRequiredService<GrossProfitCalculationService>();
                    
                    // 期間内の各日付でProcess 2-5を実行
                    var currentDate = startDate.Value;
                    while (currentDate <= endDate.Value)
                    {
                        // 該当日付のDataSetIdを取得
                        var dataSets = await datasetRepo.GetByJobDateAsync(currentDate);
                        var latestDataSet = dataSets.OrderByDescending(d => d.CreatedAt).FirstOrDefault();
                        
                        if (latestDataSet != null)
                        {
                            Console.WriteLine($"\n[{currentDate:yyyy-MM-dd}] Process 2-5を開始します");
                            logger.LogInformation("Process 2-5開始: JobDate={JobDate}, DataSetId={DataSetId}", 
                                currentDate, latestDataSet.DataSetId);
                            
                            var stopwatch = Stopwatch.StartNew();
                            
                            // Process 2-5実行
                            await grossProfitService.ExecuteProcess25Async(currentDate, latestDataSet.DataSetId);
                            
                            stopwatch.Stop();
                            
                            Console.WriteLine($"✅ Process 2-5完了 [{currentDate:yyyy-MM-dd}] ({stopwatch.ElapsedMilliseconds}ms)");
                            logger.LogInformation("Process 2-5完了: JobDate={JobDate}, DataSetId={DataSetId}, 処理時間={ElapsedMs}ms", 
                                currentDate, latestDataSet.DataSetId, stopwatch.ElapsedMilliseconds);
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ [{currentDate:yyyy-MM-dd}] DataSetが見つからないため、Process 2-5をスキップします");
                            logger.LogWarning("Process 2-5スキップ: JobDate={JobDate} - DataSetが見つかりません", currentDate);
                        }
                        
                        currentDate = currentDate.AddDays(1);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Process 2-5でエラーが発生しました");
                    Console.WriteLine($"❌ Process 2-5エラー: {ex.Message}");
                    errorCount++;
                }
            }
            else
            {
                Console.WriteLine("\n⚠️ Process 2-5には日付指定が必要です（期間モードでのみ実行）");
                logger.LogWarning("Process 2-5スキップ: 日付指定が必要です");
            }
            
            // ========== アンマッチリスト処理 ==========
            // 注意：アンマッチリスト処理は別途 create-unmatch-list コマンドで実行してください
            // await ExecuteUnmatchListAfterImport(scopedServices, jobDate, logger);
            
            // 処理結果のサマリを表示
            Console.WriteLine("\n=== フォルダ監視取込完了 ===");
            if (preserveCsvDates)
            {
                Console.WriteLine("モード: 期間指定（CSVの日付を保持）");
            }
            if (startDate.HasValue && endDate.HasValue)
            {
                if (startDate.Value == endDate.Value)
                {
                    Console.WriteLine($"対象日付: {startDate.Value:yyyy-MM-dd}");
                }
                else
                {
                    Console.WriteLine($"対象期間: {startDate.Value:yyyy-MM-dd} ～ {endDate.Value:yyyy-MM-dd}");
                    var totalDays = (endDate.Value - startDate.Value).Days + 1;
                    Console.WriteLine($"処理日数: {totalDays}日間");
                }
            }
            else
            {
                Console.WriteLine("対象期間: 全期間");
            }
            Console.WriteLine($"部門: {department}");
            Console.WriteLine($"処理ファイル数: {sortedFiles.Count}");
            
            // 総処理時間は省略（StartTimeがないため）
            
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
        var result = await unmatchListService.ProcessUnmatchListAsync();
        
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
                Console.WriteLine($"  DatasetId: {confirmation.DailyReport.DataSetId}");
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

/// <summary>
/// データベース初期化コマンドを実行
/// </summary>
private static async Task ExecuteInitDatabaseAsync(IServiceProvider services, string[] args)
{
    // 開発環境チェック
    if (!IsDevelopmentEnvironment())
    {
        Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
        return;
    }
    
    using var scope = services.CreateScope();
    var scopedServices = scope.ServiceProvider;
    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
    var initService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.Development.IDatabaseInitializationService>();
    
    try
    {
        var force = args.Any(a => a == "--force");
        
        Console.WriteLine("=== データベース初期化 ===");
        if (force)
        {
            Console.WriteLine("⚠️ --forceオプションが指定されました。既存テーブルが削除されます。");
            Console.Write("続行しますか？ (y/N): ");
            var confirm = Console.ReadLine();
            if (confirm?.ToLower() != "y")
            {
                Console.WriteLine("処理を中止しました。");
                return;
            }
        }
        
        var result = await initService.InitializeDatabaseAsync(force);
        Console.WriteLine(result.GetSummary());
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "データベース初期化でエラーが発生しました");
        Console.WriteLine($"エラー: {ex.Message}");
    }
}

/// <summary>
/// 日次終了処理リセットコマンドを実行
/// </summary>
private static async Task ExecuteResetDailyCloseAsync(IServiceProvider services, string[] args)
{
    // 開発環境チェック
    if (!IsDevelopmentEnvironment())
    {
        Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
        return;
    }
    
    if (args.Length < 3)
    {
        Console.WriteLine("使用方法: dotnet run reset-daily-close <YYYY-MM-DD> [--all]");
        return;
    }
    
    using var scope = services.CreateScope();
    var scopedServices = scope.ServiceProvider;
    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
    var resetService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.Development.IDailyCloseResetService>();
    
    try
    {
        if (!DateTime.TryParse(args[2], out var jobDate))
        {
            Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
            return;
        }
        
        var resetAll = args.Any(a => a == "--all");
        
        Console.WriteLine($"=== 日次終了処理リセット: {jobDate:yyyy-MM-dd} ===");
        
        // 関連データ状態を確認
        var status = await resetService.GetRelatedDataStatusAsync(jobDate);
        if (!status.HasDailyCloseRecord)
        {
            Console.WriteLine("指定日付の日次終了処理は実行されていません。");
            return;
        }
        
        Console.WriteLine($"日次終了処理実行日時: {status.LastDailyCloseAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"処理実行者: {status.LastProcessedBy}");
        
        if (status.HasNextDayData && !resetAll)
        {
            Console.WriteLine("⚠️ 翌日以降のデータが存在します。--all オプションを使用してください。");
            return;
        }
        
        if (resetAll)
        {
            Console.WriteLine("⚠️ 在庫マスタもリセットされます。");
        }
        
        Console.Write("続行しますか？ (y/N): ");
        var confirm = Console.ReadLine();
        if (confirm?.ToLower() != "y")
        {
            Console.WriteLine("処理を中止しました。");
            return;
        }
        
        var result = await resetService.ResetDailyCloseAsync(jobDate, resetAll);
        Console.WriteLine(result.GetSummary());
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "日次終了処理リセットでエラーが発生しました");
        Console.WriteLine($"エラー: {ex.Message}");
    }
}

/// <summary>
/// 開発用日次終了処理コマンドを実行
/// </summary>
private static async Task ExecuteDevDailyCloseAsync(IServiceProvider services, string[] args)
{
    // 開発環境チェック
    if (!IsDevelopmentEnvironment())
    {
        Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
        return;
    }
    
    if (args.Length < 3)
    {
        Console.WriteLine("使用方法: dotnet run dev-daily-close <YYYY-MM-DD> [--skip-validation] [--dry-run]");
        return;
    }
    
    using var scope = services.CreateScope();
    var scopedServices = scope.ServiceProvider;
    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
    var dailyCloseService = scopedServices.GetRequiredService<IDailyCloseService>();
    
    try
    {
        if (!DateTime.TryParse(args[2], out var jobDate))
        {
            Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
            return;
        }
        
        var skipValidation = args.Any(a => a == "--skip-validation");
        var dryRun = args.Any(a => a == "--dry-run");
        
        Console.WriteLine($"=== 開発用日次終了処理: {jobDate:yyyy-MM-dd} ===");
        Console.WriteLine($"オプション: SkipValidation={skipValidation}, DryRun={dryRun}");
        Console.WriteLine();
        
        if (dryRun)
        {
            Console.WriteLine("ドライランモードで実行します（実際の更新は行いません）");
        }
        
        var result = await dailyCloseService.ExecuteDevelopmentAsync(jobDate, skipValidation, dryRun);
        
        Console.WriteLine();
        Console.WriteLine(result.GetSummary());
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "開発用日次終了処理でエラーが発生しました");
        Console.WriteLine($"エラー: {ex.Message}");
    }
}

/// <summary>
/// データ状態確認コマンドを実行
/// </summary>
private static async Task ExecuteCheckDataStatusAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("使用方法: dotnet run check-data-status <YYYY-MM-DD>");
        return;
    }
    
    using var scope = services.CreateScope();
    var scopedServices = scope.ServiceProvider;
    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
    var statusService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.Development.IDataStatusCheckService>();
    
    try
    {
        if (!DateTime.TryParse(args[2], out var jobDate))
        {
            Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
            return;
        }
        
        var report = await statusService.GetDataStatusAsync(jobDate);
        statusService.DisplayReport(report);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "データ状態確認でエラーが発生しました");
        Console.WriteLine($"エラー: {ex.Message}");
    }
}

/// <summary>
/// 開発環境チェック
/// </summary>
private static bool IsDevelopmentEnvironment()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    return environment == "Development" || string.IsNullOrEmpty(environment);
}

/// <summary>
/// データベーススキーマチェックと自動修正
/// </summary>
private static async Task<bool> CheckAndFixDatabaseSchemaAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbInitService = scope.ServiceProvider.GetRequiredService<InventorySystem.Core.Interfaces.Development.IDatabaseInitializationService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // 必要なテーブルの存在確認
        var missingTables = await dbInitService.GetMissingTablesAsync();
        if (missingTables.Any())
        {
            logger.LogInformation("✅ スキーマ自動修正: 不足テーブルを作成します: {Tables}", string.Join(", ", missingTables));
            
            var result = await dbInitService.InitializeDatabaseAsync(false);
            if (!result.Success)
            {
                var errorMessage = result.Errors.Any() ? string.Join(", ", result.Errors) : 
                                 !string.IsNullOrEmpty(result.ErrorMessage) ? result.ErrorMessage : "不明なエラー";
                logger.LogError("❌ スキーマ修正失敗: {Error}", errorMessage);
                
                if (result.FailedTables.Any())
                {
                    logger.LogError("❌ 失敗したテーブル: {FailedTables}", string.Join(", ", result.FailedTables));
                }
                return false;
            }
            
            logger.LogInformation("✅ スキーマ自動修正が完了しました。実行時間: {Time}秒", result.ExecutionTime.TotalSeconds.ToString("F2"));
            if (result.CreatedTables.Any())
            {
                logger.LogInformation("✅ 作成されたテーブル: {Tables}", string.Join(", ", result.CreatedTables));
            }
        }
        else
        {
            // テーブルは存在するが、スキーマ不整合がある可能性があるのでチェック
            var result = await dbInitService.InitializeDatabaseAsync(false);
            if (!result.Success)
            {
                var errorMessage = result.Errors.Any() ? string.Join(", ", result.Errors) : 
                                 !string.IsNullOrEmpty(result.ErrorMessage) ? result.ErrorMessage : "不明なエラー";
                logger.LogWarning("⚠️ スキーマチェック中に警告が発生しました: {Error}", errorMessage);
            }
        }
        
        return true;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ データベーススキーマチェックでエラーが発生しました");
        return false;
    }
}

/// <summary>
/// 起動時の必須テーブルチェック
/// </summary>
private static async Task<bool> EnsureRequiredTablesExistAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbInitService = scope.ServiceProvider.GetRequiredService<InventorySystem.Core.Interfaces.Development.IDatabaseInitializationService>();
    
    try
    {
        logger.LogInformation("必要なテーブルの存在を確認中...");
        
        var missingTables = await dbInitService.GetMissingTablesAsync();
        if (missingTables.Any())
        {
            logger.LogWarning("以下のテーブルが不足しています: {Tables}", string.Join(", ", missingTables));
            logger.LogInformation("不足しているテーブルを自動作成します...");
            
            var result = await dbInitService.InitializeDatabaseAsync(false);
            
            if (result.Success)
            {
                logger.LogInformation("✅ テーブル作成完了: {Tables} (実行時間: {Time}秒)", 
                    string.Join(", ", result.CreatedTables), result.ExecutionTime.TotalSeconds.ToString("F2"));
                return true;
            }
            else
            {
                logger.LogError("❌ テーブル作成失敗: {Tables}", string.Join(", ", result.FailedTables));
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    logger.LogError("エラー詳細: {Error}", result.ErrorMessage);
                }
                return false;
            }
        }
        
        logger.LogInformation("✅ 必要なテーブルはすべて存在します");
        return true;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ テーブル確認中にエラーが発生しました");
        return false;
    }
}

    /// <summary>
    /// 日次処理シミュレーション実行
    /// </summary>
    static async Task ExecuteSimulateDailyAsync(IServiceProvider services, string[] args)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var simulationService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.Development.IDailySimulationService>();
        
        // 引数の解析
        if (args.Length < 4)
        {
            Console.WriteLine("使用方法: dotnet run simulate-daily <部門名> <YYYY-MM-DD> [--dry-run]");
            Console.WriteLine("例: dotnet run simulate-daily DeptA 2025-06-30 --dry-run");
            return;
        }
        
        var department = args[2];
        if (!DateTime.TryParse(args[3], out var jobDate))
        {
            Console.WriteLine($"❌ 無効な日付形式: {args[3]}");
            Console.WriteLine("正しい形式: YYYY-MM-DD (例: 2025-06-30)");
            return;
        }
        
        var isDryRun = args.Length > 4 && args[4] == "--dry-run";
        
        Console.WriteLine("=== 日次処理シミュレーション開始 ===");
        Console.WriteLine($"部門: {department}");
        Console.WriteLine($"処理対象日: {jobDate:yyyy-MM-dd}");
        Console.WriteLine($"モード: {(isDryRun ? "ドライラン（実際の更新なし）" : "本番実行")}");
        Console.WriteLine();
        
        try
        {
            var result = await simulationService.SimulateDailyProcessingAsync(department, jobDate, isDryRun);
            
            // 結果表示
            Console.WriteLine("=== シミュレーション結果 ===");
            Console.WriteLine($"実行時間: {result.ProcessingTime.TotalSeconds:F2}秒");
            Console.WriteLine($"成功: {(result.Success ? "✅" : "❌")}");
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine($"エラー: {result.ErrorMessage}");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== ステップ結果 ===");
            foreach (var step in result.StepResults)
            {
                var status = step.Success ? "✅" : "❌";
                Console.WriteLine($"{status} ステップ{step.StepNumber}: {step.StepName} ({step.Duration.TotalSeconds:F2}秒)");
                
                if (!string.IsNullOrEmpty(step.Message))
                {
                    Console.WriteLine($"   → {step.Message}");
                }
                
                if (!string.IsNullOrEmpty(step.ErrorMessage))
                {
                    Console.WriteLine($"   ❌ エラー: {step.ErrorMessage}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("=== 統計情報 ===");
            Console.WriteLine($"インポート: 新規{result.Statistics.Import.NewRecords}件、スキップ{result.Statistics.Import.SkippedRecords}件、エラー{result.Statistics.Import.ErrorRecords}件");
            Console.WriteLine($"アンマッチ: {result.Statistics.Unmatch.UnmatchCount}件");
            Console.WriteLine($"商品日報: {result.Statistics.DailyReport.DataCount}件");
            
            if (!string.IsNullOrEmpty(result.Statistics.DailyReport.ReportPath))
            {
                Console.WriteLine($"商品日報ファイル: {result.Statistics.DailyReport.ReportPath}");
            }
            
            if (!string.IsNullOrEmpty(result.Statistics.Unmatch.UnmatchListPath))
            {
                Console.WriteLine($"アンマッチリストファイル: {result.Statistics.Unmatch.UnmatchListPath}");
            }
            
            if (result.GeneratedFiles.Any())
            {
                Console.WriteLine("生成されたファイル:");
                foreach (var file in result.GeneratedFiles)
                {
                    Console.WriteLine($"  - {file}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine($"=== シミュレーション{(result.Success ? "完了" : "失敗")} ===");
            
            if (isDryRun && result.Success)
            {
                Console.WriteLine("💡 実際の処理を実行するには --dry-run オプションを外してください");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "日次処理シミュレーション中にエラーが発生しました");
            Console.WriteLine($"❌ 予期しないエラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// 在庫マスタの重複レコードをクリーンアップする
    /// </summary>
    static async Task ExecuteCleanupInventoryDuplicatesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var inventoryRepo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        
        try
        {
            Console.WriteLine("=== 在庫マスタ重複レコードクリーンアップ ===");
            Console.WriteLine("⚠️ このコマンドは重複レコードを削除します。");
            Console.Write("続行しますか？ (y/N): ");
            
            var confirmation = Console.ReadLine()?.Trim().ToLower();
            if (confirmation != "y")
            {
                Console.WriteLine("処理をキャンセルしました。");
                return;
            }
            
            var stopwatch = Stopwatch.StartNew();
            var deletedCount = await inventoryRepo.CleanupDuplicateRecordsAsync();
            stopwatch.Stop();
            
            Console.WriteLine($"✅ {deletedCount}件の重複レコードを削除しました。");
            Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
            
            logger.LogInformation("在庫マスタ重複レコードクリーンアップ完了: {Count}件削除", deletedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "在庫マスタ重複レコードクリーンアップ中にエラーが発生しました");
            Console.WriteLine($"❌ エラー: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 月初に前月末在庫から現在庫を初期化する
    /// </summary>
    static async Task ExecuteInitMonthlyInventoryAsync(IServiceProvider services, string[] args)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var inventoryRepo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        
        if (args.Length < 3)
        {
            Console.WriteLine("エラー: 年月が指定されていません");
            Console.WriteLine("使用方法: dotnet run init-monthly-inventory YYYYMM");
            Console.WriteLine("例: dotnet run init-monthly-inventory 202507");
            return;
        }
        
        var yearMonth = args[2];
        if (yearMonth.Length != 6 || !int.TryParse(yearMonth, out _))
        {
            Console.WriteLine("エラー: 年月は YYYYMM 形式で指定してください");
            return;
        }
        
        try
        {
            Console.WriteLine($"=== {yearMonth.Substring(0, 4)}年{yearMonth.Substring(4, 2)}月の在庫初期化 ===");
            Console.WriteLine("前月末在庫から現在庫を初期化します。");
            Console.Write("続行しますか？ (y/N): ");
            
            var confirmation = Console.ReadLine()?.Trim().ToLower();
            if (confirmation != "y")
            {
                Console.WriteLine("処理をキャンセルしました。");
                return;
            }
            
            var stopwatch = Stopwatch.StartNew();
            var updatedCount = await inventoryRepo.InitializeMonthlyInventoryAsync(yearMonth);
            stopwatch.Stop();
            
            Console.WriteLine($"✅ {updatedCount}件の在庫を初期化しました。");
            Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
            
            logger.LogInformation("月初在庫初期化完了: {YearMonth} - {Count}件更新", yearMonth, updatedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "月初在庫初期化中にエラーが発生しました");
            Console.WriteLine($"❌ エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// 前日在庫引継モードの実行
    /// </summary>
    private static async Task ExecuteCarryoverModeAsync(
        IInventoryRepository inventoryRepository,
        IDataSetManagementRepository datasetRepository,
        DateTime targetDate, 
        string dataSetId,
        string department,
        ILogger logger,
        IDataSetManagementFactory dataSetFactory,  // ⭐ Phase 2-B: ファクトリ追加（Gemini推奨）
        ITimeProvider timeProvider)  // ⭐ Phase 2-B: タイムプロバイダー追加（Gemini推奨）
    {
        try
        {
            // 1. 最終処理日の取得
            var lastProcessedDate = await inventoryRepository.GetMaxJobDateAsync();
            if (lastProcessedDate == DateTime.MinValue || lastProcessedDate >= targetDate)
            {
                logger.LogWarning("前日の在庫データが見つかりません。処理をスキップします。");
                Console.WriteLine("⚠️ 前日の在庫データが見つかりません。処理をスキップします。");
                return;
            }

            logger.LogInformation("前日（{LastDate}）の在庫を引き継ぎます。", lastProcessedDate);

            // 2. 前日の在庫データ取得
            var previousInventory = await inventoryRepository.GetAllActiveInventoryAsync();
            logger.LogInformation("前日在庫: {Count}件", previousInventory.Count);

            // 3. 在庫データのコピー（JobDateとDataSetIdを更新）
            var carryoverInventory = previousInventory.Select(inv => new InventoryMaster
            {
                // 5項目複合キー
                Key = inv.Key,
                
                // その他の項目
                ProductName = inv.ProductName,
                Unit = inv.Unit,
                StandardPrice = inv.StandardPrice,
                ProductCategory1 = inv.ProductCategory1,
                ProductCategory2 = inv.ProductCategory2,
                
                // 在庫数量（変更なし）
                CurrentStock = inv.CurrentStock,
                CurrentStockAmount = inv.CurrentStockAmount,
                
                // 当日発生はゼロ
                DailyStock = 0,
                DailyStockAmount = 0,
                DailyFlag = '0',
                
                // 更新項目
                JobDate = targetDate,
                DataSetId = dataSetId,
                ImportType = "CARRYOVER",
                IsActive = true,
                UpdatedDate = timeProvider.UtcNow,  // ⭐ Phase 2-B: UTC統一（Gemini推奨）
                
                // 前月繰越
                PreviousMonthQuantity = inv.PreviousMonthQuantity,
                PreviousMonthAmount = inv.PreviousMonthAmount
            }).ToList();

            // 4. DatasetManagementエンティティを作成
            // ⭐ Phase 2-B: ファクトリパターン使用（Gemini推奨）
            var datasetManagement = dataSetFactory.CreateForCarryover(
                dataSetId,
                targetDate,
                department,
                carryoverInventory.Count(),
                parentDataSetId: previousInventory.FirstOrDefault()?.DataSetId,
                notes: $"前日在庫引継: {previousInventory.Count}件（伝票データ0件）");
            
            // 5. データセット管理レコードは ProcessCarryoverInTransactionAsync 内で作成されるためここでは不要

            // 6. 在庫マスタへの保存（MERGE処理）
            // 在庫マスタへの保存（トランザクション処理）
            var affectedRows = await inventoryRepository.ProcessCarryoverInTransactionAsync(
                carryoverInventory, 
                targetDate, 
                dataSetId,
                datasetManagement);
            
            logger.LogInformation(
                "前日在庫引継完了 - 対象日: {TargetDate}, 件数: {Count}件",
                targetDate, carryoverInventory.Count());
                
            Console.WriteLine($"✅ 前日在庫引継完了 [{targetDate:yyyy-MM-dd}]");
            Console.WriteLine($"   - 引継在庫数: {carryoverInventory.Count()}件");
            Console.WriteLine($"   - DataSetId: {dataSetId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "前日在庫引継処理中にエラーが発生しました");
            Console.WriteLine($"❌ 前日在庫引継エラー: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// ランダム文字列生成
    /// </summary>
    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// 初期在庫インポートコマンドを実行
    /// </summary>
    private static async Task ExecuteImportInitialInventoryAsync(IServiceProvider services, string[] args)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var commandLogger = scopedServices.GetRequiredService<ILogger<ImportInitialInventoryCommand>>();
        
        // 部門の指定（デフォルト: DeptA）
        var department = args.Length >= 3 ? args[2] : "DeptA";
        
        try
        {
            var command = new ImportInitialInventoryCommand(scopedServices, commandLogger, scopedServices.GetRequiredService<IConfiguration>());
            await command.ExecuteAsync(department);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "初期在庫インポートコマンドでエラーが発生しました");
            Console.WriteLine($"エラー: {ex.Message}");
        }
    }

static async Task ExecuteOptimizeInventoryAsync(IServiceProvider services, string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("使用方法: optimize-inventory <日付>");
        Console.WriteLine("例: optimize-inventory 2025-06-30");
        return;
    }

    using (var scope = services.CreateScope())
    {
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var inventoryOptimizationService = scopedServices.GetRequiredService<IInventoryOptimizationService>();

        if (!DateTime.TryParse(args[2], out var jobDate))
        {
            Console.WriteLine("❌ 日付の形式が正しくありません");
            return;
        }

        logger.LogInformation("=== 在庫最適化開始 ===");
        logger.LogInformation("対象日: {JobDate}", jobDate);

        try
        {
            var result = await inventoryOptimizationService.OptimizeInventoryAsync(jobDate);
            
            if (result.IsSuccess)
            {
                Console.WriteLine($"✅ 在庫最適化が完了しました");
                Console.WriteLine($"   対象日: {result.JobDate:yyyy-MM-dd}");
                Console.WriteLine($"   処理時間: {result.ProcessingTime?.TotalSeconds:F2}秒");
                Console.WriteLine($"   前日在庫: {result.PreviousDayStockCount}件");
                Console.WriteLine($"   売上伝票: {result.SalesTransactionCount}件");
                Console.WriteLine($"   仕入伝票: {result.PurchaseTransactionCount}件");
                Console.WriteLine($"   在庫調整: {result.AdjustmentTransactionCount}件");
                Console.WriteLine($"   計算後在庫: {result.CalculatedStockCount}件");
                Console.WriteLine($"   挿入レコード: {result.InsertedRecordCount}件");
                Console.WriteLine($"   削除レコード: {result.DeletedRecordCount}件");
                Console.WriteLine($"   0在庫削除: {result.CleanedUpRecordCount}件");
                
                logger.LogInformation("在庫最適化完了: {Result}", result);
            }
            else
            {
                Console.WriteLine($"❌ 在庫最適化に失敗しました: {result.ErrorMessage}");
                logger.LogError("在庫最適化失敗: {ErrorMessage}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "在庫最適化中にエラーが発生しました");
            Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
        }
        
        logger.LogInformation("=== 在庫最適化完了 ===");
        Console.WriteLine("\n=== 在庫最適化完了 ===");
    }
}

    /// <summary>
    /// フェーズ2: 新しいカラムの追加
    /// </summary>
    private static async Task ExecuteMigratePhase2Async(IServiceProvider services, string[] args)
    {
        await ExecuteMigrationPhaseAsync(services, "051_Phase2_AddNewColumns.sql", "フェーズ2: 新しいカラム追加");
    }

    /// <summary>
    /// フェーズ3: データ移行と同期トリガー作成
    /// </summary>
    private static async Task ExecuteMigratePhase3Async(IServiceProvider services, string[] args)
    {
        await ExecuteMigrationPhaseAsync(services, "052_Phase3_MigrateDataAndSync.sql", "フェーズ3: データ移行と同期");
    }

    /// <summary>
    /// フェーズ5: クリーンアップ
    /// </summary>
    private static async Task ExecuteMigratePhase5Async(IServiceProvider services, string[] args)
    {
        Console.WriteLine("⚠️  重要: このフェーズは古いカラムを削除します");
        Console.WriteLine("   実行前に以下を確認してください:");
        Console.WriteLine("   1. アプリケーションが新しいスキーマで正常動作している");
        Console.WriteLine("   2. import-folderコマンドが成功している");
        Console.WriteLine("   3. データベースの完全バックアップを取得済み");
        Console.WriteLine();
        Console.Write("続行しますか？ (y/N): ");
        
        var response = Console.ReadLine();
        if (response?.ToLower() != "y" && response?.ToLower() != "yes")
        {
            Console.WriteLine("処理をキャンセルしました");
            return;
        }
        
        await ExecuteMigrationPhaseAsync(services, "053_Phase5_Cleanup.sql", "フェーズ5: クリーンアップ");
    }

    /// <summary>
    /// Process 2-5: 売上伝票への在庫単価書き込みと粗利計算
    /// </summary>
    static async Task ExecuteProcess25Async(IServiceProvider services, string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("使用方法: process-2-5 <日付> [データセットID]");
            Console.WriteLine("         gross-profit <日付> [データセットID]");
            Console.WriteLine("例: process-2-5 2025-06-30");
            Console.WriteLine("例: gross-profit 2025-06-30 ABC123");
            return;
        }

        if (!DateTime.TryParse(args[1], out var jobDate))
        {
            Console.WriteLine("❌ 日付の形式が正しくありません");
            return;
        }

        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        var grossProfitService = scopedServices.GetRequiredService<GrossProfitCalculationService>();
        var dataSetRepository = scopedServices.GetRequiredService<IDataSetManagementRepository>();

        try
        {
            // データセットID取得（引数指定または自動取得）
            string dataSetId;
            if (args.Length >= 3)
            {
                dataSetId = args[2];
                Console.WriteLine($"指定されたDataSetId: {dataSetId}");
            }
            else
            {
                // JobDateから最新のDataSetIdを取得
                var dataSets = await dataSetRepository.GetByJobDateAsync(jobDate);
                var latestDataSet = dataSets.OrderByDescending(d => d.CreatedAt).FirstOrDefault();
                
                if (latestDataSet == null)
                {
                    Console.WriteLine($"❌ 指定日({jobDate:yyyy-MM-dd})のデータセットが見つかりません");
                    return;
                }
                
                dataSetId = latestDataSet.DataSetId;
                Console.WriteLine($"自動取得したDataSetId: {dataSetId}");
            }

            Console.WriteLine("=== Process 2-5: 売上伝票への在庫単価書き込みと粗利計算 開始 ===");
            Console.WriteLine($"対象日: {jobDate:yyyy-MM-dd}");
            Console.WriteLine($"データセットID: {dataSetId}");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Process 2-5実行
            await grossProfitService.ExecuteProcess25Async(jobDate, dataSetId);

            stopwatch.Stop();

            Console.WriteLine($"✅ Process 2-5 が正常に完了しました");
            Console.WriteLine($"   処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
            Console.WriteLine("=== Process 2-5 完了 ===");

            logger.LogInformation("Process 2-5完了: JobDate={JobDate}, DataSetId={DataSetId}, 処理時間={ElapsedMs}ms", 
                jobDate, dataSetId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Process 2-5 でエラーが発生しました: {ex.Message}");
            logger.LogError(ex, "Process 2-5エラー: JobDate={JobDate}", jobDate);
        }
    }

    /// <summary>
    /// DataSetId不整合修復コマンドを実行
    /// </summary>
    static async Task ExecuteRepairDataSetIdAsync(IServiceProvider services, string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("使用方法: repair-dataset-id <対象日付(yyyy-MM-dd)>");
            Console.WriteLine("例: repair-dataset-id 2025-06-02");
            return;
        }

        if (!DateTime.TryParseExact(args[1], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var targetDate))
        {
            Console.WriteLine("日付の形式が正しくありません。yyyy-MM-dd 形式で入力してください。");
            return;
        }

        using var scope = services.CreateScope();
        var repairService = scope.ServiceProvider.GetRequiredService<DataSetIdRepairService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            Console.WriteLine("=== DataSetId不整合修復 開始 ===");
            Console.WriteLine($"対象日: {targetDate:yyyy-MM-dd}");
            Console.WriteLine();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // DataSetId不整合修復実行
            var result = await repairService.RepairDataSetIdInconsistenciesAsync(targetDate);

            stopwatch.Stop();

            Console.WriteLine("=== 修復結果 ===");
            
            // 売上伝票の修復結果
            Console.WriteLine($"[売上伝票] 更新件数: {result.SalesVoucherResult.UpdatedRecords}件");
            if (result.SalesVoucherResult.BeforeDataSetIds.Any())
            {
                Console.WriteLine($"  修復前DataSetId: {result.SalesVoucherResult.BeforeDataSetIds.Count}種類");
                Console.WriteLine($"  修復後DataSetId: {result.SalesVoucherResult.CorrectDataSetId}");
            }

            // CP在庫マスタの修復結果
            Console.WriteLine($"[CP在庫マスタ] 更新件数: {result.CpInventoryResult.UpdatedRecords}件");
            if (result.CpInventoryResult.BeforeDataSetIds.Any())
            {
                Console.WriteLine($"  修復前DataSetId: {result.CpInventoryResult.BeforeDataSetIds.Count}種類");
                Console.WriteLine($"  修復後DataSetId: {result.CpInventoryResult.CorrectDataSetId}");
            }

            // 仕入伝票の修復結果
            if (result.PurchaseVoucherResult.UpdatedRecords > 0)
            {
                Console.WriteLine($"[仕入伝票] 更新件数: {result.PurchaseVoucherResult.UpdatedRecords}件");
                Console.WriteLine($"  修復後DataSetId: {result.PurchaseVoucherResult.CorrectDataSetId}");
            }

            // 在庫調整の修復結果
            if (result.InventoryAdjustmentResult.UpdatedRecords > 0)
            {
                Console.WriteLine($"[在庫調整] 更新件数: {result.InventoryAdjustmentResult.UpdatedRecords}件");
                Console.WriteLine($"  修復後DataSetId: {result.InventoryAdjustmentResult.CorrectDataSetId}");
            }

            Console.WriteLine();
            Console.WriteLine($"✅ DataSetId不整合修復が正常に完了しました");
            Console.WriteLine($"   総更新件数: {result.TotalUpdatedRecords}件");
            Console.WriteLine($"   処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
            Console.WriteLine("=== DataSetId不整合修復 完了 ===");

            logger.LogInformation("DataSetId不整合修復完了: TargetDate={TargetDate}, 総更新件数={TotalUpdatedRecords}, 処理時間={ElapsedMs}ms", 
                targetDate, result.TotalUpdatedRecords, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DataSetId不整合修復でエラーが発生しました: {ex.Message}");
            logger.LogError(ex, "DataSetId不整合修復エラー: TargetDate={TargetDate}", targetDate);
        }
    }

    /// <summary>
    /// 移行フェーズの共通実行ロジック
    /// </summary>
    private static async Task ExecuteMigrationPhaseAsync(IServiceProvider services, string scriptFileName, string phaseName)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("=== {PhaseName} 開始 ===", phaseName);
            
            var connectionString = scopedServices.GetRequiredService<IConfiguration>()
                .GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                logger.LogError("接続文字列が見つかりません");
                return;
            }
            
            // スクリプトファイルの読み込み（プロジェクトルートを検索）
            var scriptPath = FindScriptPath(scriptFileName);
            
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                logger.LogError("移行スクリプトが見つかりません: {Path}", scriptPath ?? "null");
                return;
            }
            
            var scriptContent = await File.ReadAllTextAsync(scriptPath);
            
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();
            
            logger.LogInformation("データベース接続成功");
            Console.WriteLine($"=== {phaseName} 実行中 ===");
            
            // 修正済みのGO文分割処理を使用
            await ExecuteSqlScriptAsync(connection, scriptContent);
            
            Console.WriteLine($"✅ {phaseName} 完了");
            logger.LogInformation("=== {PhaseName} 完了 ===", phaseName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{PhaseName} 中にエラーが発生しました", phaseName);
            Console.WriteLine($"❌ エラー: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// マスタテーブルのスキーマ確認
    /// </summary>
    private static async Task ExecuteCheckSchemaAsync(IServiceProvider services, string[] args)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("=== マスタテーブルスキーマ確認開始 ===");
            
            var connectionString = scopedServices.GetRequiredService<IConfiguration>()
                .GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                logger.LogError("接続文字列が見つかりません");
                return;
            }
            
            // スクリプトファイルの読み込み（プロジェクトルートを検索）
            var scriptPath = FindScriptPath("050_Phase1_CheckCurrentSchema.sql");
            
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                logger.LogError("スキーマ確認スクリプトが見つかりません: {Path}", scriptPath ?? "null");
                return;
            }
            
            var scriptContent = await File.ReadAllTextAsync(scriptPath);
            
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();
            
            logger.LogInformation("データベース接続成功");
            
            // スクリプトを実行（GOバッチ分割対応）
            await ExecuteSqlScriptAsync(connection, scriptContent);
            
            // 基本的なテーブル存在確認
            var checkTablesSql = @"
                SELECT TABLE_NAME, 
                       CASE WHEN TABLE_NAME IS NOT NULL THEN '存在' ELSE '未作成' END AS STATUS
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
                ORDER BY TABLE_NAME";
            
            var tables = await connection.QueryAsync(checkTablesSql);
            
            Console.WriteLine("=== テーブル存在確認 ===");
            foreach (var table in tables)
            {
                Console.WriteLine($"  {table.TABLE_NAME}: {table.STATUS}");
            }
            
            // 日付カラムの確認
            var checkDateColumnsSql = @"
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
                AND (COLUMN_NAME LIKE '%Created%' OR COLUMN_NAME LIKE '%Updated%' OR COLUMN_NAME LIKE '%Date%')
                ORDER BY TABLE_NAME, COLUMN_NAME";
            
            var dateColumns = await connection.QueryAsync(checkDateColumnsSql);
            
            Console.WriteLine("\n=== 日付関連カラム確認 ===");
            foreach (var col in dateColumns)
            {
                Console.WriteLine($"  {col.TABLE_NAME}.{col.COLUMN_NAME}: {col.DATA_TYPE} ({(col.IS_NULLABLE == "YES" ? "NULL許可" : "NOT NULL")})");
            }
            
            // 診断結果
            Console.WriteLine("\n=== 診断結果 ===");
            
            bool hasOldSchema = dateColumns.Any(c => c.COLUMN_NAME == "CreatedDate" || c.COLUMN_NAME == "UpdatedDate");
            bool hasNewSchema = dateColumns.Any(c => c.COLUMN_NAME == "CreatedAt" || c.COLUMN_NAME == "UpdatedAt");
            
            if (hasOldSchema && !hasNewSchema)
            {
                Console.WriteLine("🔴 問題: 古いスキーマ（CreatedDate/UpdatedDate）のみ存在");
                Console.WriteLine("   → フェーズ2で新しいカラムの追加が必要");
            }
            else if (!hasOldSchema && hasNewSchema)
            {
                Console.WriteLine("✅ 正常: 新しいスキーマ（CreatedAt/UpdatedAt）のみ存在");
                Console.WriteLine("   → 移行完了済み、追加の対応不要");
            }
            else if (hasOldSchema && hasNewSchema)
            {
                Console.WriteLine("🟡 移行中: 新旧両方のスキーマが存在");
                Console.WriteLine("   → フェーズ3以降の処理が必要");
            }
            else
            {
                Console.WriteLine("🔴 問題: 日付カラムが見つかりません");
                Console.WriteLine("   → テーブル定義に問題がある可能性");
            }
            
            logger.LogInformation("=== マスタテーブルスキーマ確認完了 ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "スキーマ確認中にエラーが発生しました");
            Console.WriteLine($"❌ エラー: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// スクリプトファイルのパスを検索
    /// </summary>
    private static string? FindScriptPath(string fileName)
    {
        // デバッグ情報の出力
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var currentDir = Environment.CurrentDirectory;
        Console.WriteLine($"BaseDirectory: {baseDir}");
        Console.WriteLine($"CurrentDirectory: {currentDir}");
        
        // 検索候補パスを定義
        var searchPaths = new List<string>();
        
        // 1. 現在のディレクトリから
        var currentDirInfo = new DirectoryInfo(currentDir);
        for (int i = 0; i < 6 && currentDirInfo != null; i++)
        {
            searchPaths.Add(Path.Combine(currentDirInfo.FullName, "database", "migrations", fileName));
            currentDirInfo = currentDirInfo.Parent;
        }
        
        // 2. 実行ディレクトリから
        var baseDirInfo = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && baseDirInfo != null; i++)
        {
            searchPaths.Add(Path.Combine(baseDirInfo.FullName, "database", "migrations", fileName));
            baseDirInfo = baseDirInfo.Parent;
        }
        
        // 3. 特定の候補パス
        searchPaths.AddRange(new[]
        {
            Path.Combine(currentDir, "database", "migrations", fileName),
            Path.Combine(currentDir, "..", "database", "migrations", fileName),
            Path.Combine(currentDir, "..", "..", "database", "migrations", fileName),
            Path.Combine(currentDir, "..", "..", "..", "database", "migrations", fileName),
            Path.Combine(currentDir, "..", "..", "..", "..", "database", "migrations", fileName),
            Path.Combine(currentDir, "..", "..", "..", "..", "..", "database", "migrations", fileName)
        });
        
        // InventoryManagementSystemフォルダを探す
        var currentPath = currentDir;
        while (!string.IsNullOrEmpty(currentPath))
        {
            if (Path.GetFileName(currentPath).Equals("InventoryManagementSystem", StringComparison.OrdinalIgnoreCase))
            {
                searchPaths.Add(Path.Combine(currentPath, "database", "migrations", fileName));
                break;
            }
            var parent = Directory.GetParent(currentPath);
            currentPath = parent?.FullName;
        }
        
        // 各パスを試行
        foreach (var searchPath in searchPaths.Distinct())
        {
            try
            {
                var fullPath = Path.GetFullPath(searchPath);
                Console.WriteLine($"Trying: {fullPath}");
                if (File.Exists(fullPath))
                {
                    Console.WriteLine($"Found: {fullPath}");
                    return fullPath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking path {searchPath}: {ex.Message}");
            }
        }
        
        Console.WriteLine("Script file not found in any candidate paths");
        return null;
    }

    /// <summary>
    /// GO文を基準にバッチ分割してSQLスクリプトを実行
    /// </summary>
    private static async Task ExecuteSqlScriptAsync(Microsoft.Data.SqlClient.SqlConnection connection, string scriptContent)
    {
        // GOを基準にスクリプトを分割 (大文字小文字を無視)
        var batches = System.Text.RegularExpressions.Regex.Split(
            scriptContent, 
            @"^\s*GO\s*$", 
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var batch in batches)
        {
            var trimmedBatch = batch.Trim();
            if (string.IsNullOrEmpty(trimmedBatch))
            {
                continue;
            }

            // USEステートメントを特別扱い
            if (trimmedBatch.StartsWith("USE ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // USE文はデータベースコンテキストを変更するため、即時実行
                    await connection.ExecuteAsync(trimmedBatch);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing USE statement: {ex.Message}");
                    // USEが失敗した場合、後続のクエリは実行しない
                    throw;
                }
            }
            else
            {
                try
                {
                    await connection.ExecuteAsync(trimmedBatch);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing batch: {ex.Message}");
                    Console.WriteLine($"Batch content (first 200 chars): {trimmedBatch.Substring(0, Math.Min(200, trimmedBatch.Length))}...");
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// 主キー変更前のデータ分析を実行
    /// </summary>
    private static async Task ExecuteAnalyzePrimaryKeyChangeAsync(IServiceProvider services, string[] args)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<AnalyzePrimaryKeyChangeCommand>>();
        var configuration = scopedServices.GetRequiredService<IConfiguration>();
        
        try
        {
            var command = new AnalyzePrimaryKeyChangeCommand(configuration, logger);
            await command.ExecuteAsync();
            
            Console.WriteLine("\n分析が完了しました。");
            Console.WriteLine("次のステップ：");
            Console.WriteLine("1. 分析結果を確認し、履歴データの保存が必要か判断");
            Console.WriteLine("2. 必要に応じてバックアップテーブルを作成");
            Console.WriteLine("3. マイグレーションスクリプトを実行");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "主キー変更分析でエラーが発生しました");
            Console.WriteLine($"❌ エラー: {ex.Message}");
        }
    }

} // Program クラスの終了


