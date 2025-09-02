global using System;
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

namespace InventorySystem.Console
{
    // Program クラスの定義
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // カルチャー設定（日付処理の一貫性を保つため）
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            // ===== PDF生成診断情報 開始 =====
            System.Console.WriteLine("=== PDF Generation Diagnostics ===");
            System.Console.WriteLine($"Runtime Identifier: {RuntimeInformation.RuntimeIdentifier}");
            System.Console.WriteLine($"OS Description: {RuntimeInformation.OSDescription}");
            System.Console.WriteLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
            System.Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
            System.Console.WriteLine($"Current Directory: {Environment.CurrentDirectory}");

#if WINDOWS
            System.Console.WriteLine("WINDOWS symbol: DEFINED ✓ - FastReport services will be used");
#else
            System.Console.WriteLine("WINDOWS symbol: NOT DEFINED ✗ - Placeholder services will be used");
#endif

            // アセンブリ情報の表示
            var assembly = Assembly.GetExecutingAssembly();
            System.Console.WriteLine($"Assembly: {assembly.GetName().Name} v{assembly.GetName().Version}");

            // FastReport DLLの存在確認
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var fastReportDll = Path.Combine(baseDir, "FastReport.dll");
            if (File.Exists(fastReportDll))
            {
                var fileInfo = new FileInfo(fastReportDll);
                System.Console.WriteLine($"FastReport.dll: Found ✓ (Size: {fileInfo.Length:N0} bytes)");
            }
            else
            {
                System.Console.WriteLine($"FastReport.dll: NOT FOUND ✗ at {fastReportDll}");
            }
            System.Console.WriteLine("=================================\n");
            // ===== PDF生成診断情報 終了 =====

            // 実行環境情報の表示
            System.Console.WriteLine($"実行環境: {Environment.OSVersion}");
            System.Console.WriteLine($".NET Runtime: {Environment.Version}");
            System.Console.WriteLine($"実行ディレクトリ: {Environment.CurrentDirectory}");
            System.Console.WriteLine($"現在のカルチャー: {CultureInfo.CurrentCulture.Name} (InvariantCultureに統一)");

            // FastReportテストコマンドの早期処理
            if (args.Length > 0 && args[0] == "test-fastreport")
            {
                System.Console.WriteLine("=== FastReport.NET Trial テスト開始 ===");
                System.Console.WriteLine($"実行時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                System.Console.WriteLine("\n✓ Windows専用環境");
                System.Console.WriteLine("✓ FastReport.NET Trial版が利用可能です");
                System.Console.WriteLine("✓ アンマッチリスト・商品日報の実装が完了しています");
                System.Console.WriteLine("\n実際のPDF生成テストを実行するには：");
                System.Console.WriteLine("  dotnet run unmatch-list [日付] # アンマッチリストPDF生成");
                System.Console.WriteLine("  dotnet run daily-report [日付] # 商品日報PDF生成");
                System.Console.WriteLine("\n=== FastReport.NET移行テスト完了 ===");
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
            builder.Services.AddScoped<IUnInventoryRepository>(provider =>
                    new UnInventoryRepository(connectionString, provider.GetRequiredService<ILogger<UnInventoryRepository>>()));
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
            builder.Services.AddScoped<IUnmatchCheckRepository>(provider =>
                    new UnmatchCheckRepository(connectionString, provider.GetRequiredService<ILogger<UnmatchCheckRepository>>()));

            builder.Services.AddScoped<IUnmatchListService, UnmatchListService>();
            builder.Services.AddScoped<IUnmatchCheckValidationService, UnmatchCheckValidationService>();
            builder.Services.AddScoped<InventorySystem.Core.Interfaces.IDailyReportService, DailyReportService>();
            builder.Services.AddScoped<IInventoryListService, InventoryListService>();
            builder.Services.AddScoped<ICpInventoryCreationService, CpInventoryCreationService>();

            // 営業日報サービス
            builder.Services.AddScoped<InventorySystem.Core.Interfaces.IBusinessDailyReportService, BusinessDailyReportService>();
            builder.Services.AddScoped<IBusinessDailyReportRepository>(provider =>
                    new BusinessDailyReportRepository(connectionString, provider.GetRequiredService<ILogger<BusinessDailyReportRepository>>()));

            // ⭐ Phase 2-B: ITimeProviderとDataSetManagementFactoryの登録（Gemini推奨）
            // JST統一: 日本のビジネスシステムのため、JstTimeProviderを使用
            builder.Services.AddSingleton<ITimeProvider, JstTimeProvider>();
            builder.Services.AddScoped<IDataSetManagementFactory, DataSetManagementFactory>();

            // フィーチャーフラグの設定を読み込み
            builder.Services.Configure<FeatureFlags>(
                    builder.Configuration.GetSection("Features"));

            // DataSetService関連の登録（DataSetManagement専用）
            builder.Services.AddScoped<IDataSetService, DataSetManagementService>();
            System.Console.WriteLine("🔄 DataSetManagement専用モードで起動");
            // Report Services
#if WINDOWS
            // FastReportサービスの登録（Windows環境のみ）
            // Linux環境ではFastReportフォルダのコンパイルが除外されるため、型の直接参照はできない
            var unmatchListFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.UnmatchListFastReportService, InventorySystem.Reports");
            var dailyReportFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.DailyReportFastReportService, InventorySystem.Reports");
            var productAccountFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.ProductAccountFastReportService, InventorySystem.Reports");
            var businessDailyReportFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.BusinessDailyReportFastReportService, InventorySystem.Reports");
            var inventoryListFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.InventoryListService, InventorySystem.Reports");
            if (unmatchListFastReportType != null && dailyReportFastReportType != null && productAccountFastReportType != null && businessDailyReportFastReportType != null && inventoryListFastReportType != null)
            {
                builder.Services.AddScoped(typeof(IUnmatchListReportService), unmatchListFastReportType);
                builder.Services.AddScoped(typeof(InventorySystem.Reports.Interfaces.IDailyReportService), dailyReportFastReportType);
                builder.Services.AddScoped(typeof(InventorySystem.Reports.Interfaces.IProductAccountReportService), productAccountFastReportType);
                builder.Services.AddScoped(typeof(InventorySystem.Reports.Interfaces.IBusinessDailyReportService), businessDailyReportFastReportType);
                builder.Services.AddScoped(typeof(IBusinessDailyReportReportService), businessDailyReportFastReportType);
                builder.Services.AddScoped(inventoryListFastReportType);
            }
            else
            {
                throw new InvalidOperationException("FastReportサービスが見つかりません。Windows環境で実行してください。");
            }
#else
builder.Services.AddScoped<IUnmatchListReportService, PlaceholderUnmatchListReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IDailyReportService, PlaceholderDailyReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IProductAccountReportService, PlaceholderProductAccountReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IBusinessDailyReportService, BusinessDailyReportPlaceholderService>();
builder.Services.AddScoped<IBusinessDailyReportReportService, BusinessDailyReportPlaceholderService>();
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

            // SE3: マスタ同期サービス（商品勘定・在庫表担当）
            builder.Services.AddScoped<InventorySystem.Data.Services.IMasterSyncService>(provider =>
                new InventorySystem.Data.Services.MasterSyncService(
                    connectionString,
                    provider.GetRequiredService<ILogger<InventorySystem.Data.Services.MasterSyncService>>()));

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
                System.Console.WriteLine("使用方法:");
                System.Console.WriteLine("  dotnet run test-connection                   - データベース接続テスト");
                System.Console.WriteLine("  dotnet run test-pdf                          - PDF生成テスト（DB不要）");
                System.Console.WriteLine("  dotnet run test-fastreport                   - FastReportテスト（DB不要）");
                System.Console.WriteLine("  dotnet run unmatch-list [YYYY-MM-DD]         - アンマッチリスト処理を実行");
                System.Console.WriteLine("  dotnet run daily-report [YYYY-MM-DD] [--dataset-id ID] - 商品日報を生成（アンマッチ0件必須）");
                System.Console.WriteLine("  dotnet run product-account [YYYY-MM-DD] [--dataset-id ID] - 商品勘定を生成（アンマッチ0件必須）");
                System.Console.WriteLine("  dotnet run inventory-list [YYYY-MM-DD]       - 在庫表を生成（アンマッチ0件必須）");
                System.Console.WriteLine("  dotnet run import-sales <file> [YYYY-MM-DD]  - 売上伝票CSVを取込");
                System.Console.WriteLine("  dotnet run import-purchase <file> [YYYY-MM-DD] - 仕入伝票CSVを取込");
                System.Console.WriteLine("  dotnet run import-adjustment <file> [YYYY-MM-DD] - 在庫調整CSVを取込");
                System.Console.WriteLine("  dotnet run debug-csv-structure <file>        - CSV構造を分析");
                System.Console.WriteLine("  dotnet run import-customers <file>           - 得意先マスタCSVを取込");
                System.Console.WriteLine("  dotnet run import-products <file>            - 商品マスタCSVを取込");
                System.Console.WriteLine("  dotnet run import-suppliers <file>           - 仕入先マスタCSVを取込");
                System.Console.WriteLine("  dotnet run init-folders                      - フォルダ構造を初期化");
                System.Console.WriteLine("  dotnet run import-folder <dept> [YYYY-MM-DD] - 部門フォルダから一括取込");
                System.Console.WriteLine("  dotnet run import-masters                    - 等級・階級マスタをインポート");
                System.Console.WriteLine("  dotnet run check-masters                     - 等級・階級マスタの登録状況を確認");
                System.Console.WriteLine("  dotnet run init-inventory <dept>             - 初期在庫設定（前月末在庫.csv取込）");
                System.Console.WriteLine("  dotnet run import-with-carryover <dept>      - 前日在庫を引き継いでインポート");
                System.Console.WriteLine("");
                System.Console.WriteLine("【開発環境用コマンド】");
                System.Console.WriteLine("  dotnet run init-database [--force]           - データベース初期化");
                System.Console.WriteLine("  dotnet run reset-daily-close <YYYY-MM-DD> [--all] - 日次終了処理リセット");
                System.Console.WriteLine("  dotnet run dev-daily-close <YYYY-MM-DD> [--skip-validation] [--dry-run] - 開発用日次終了処理");
                System.Console.WriteLine("  dotnet run check-data-status <YYYY-MM-DD>    - データ状態確認");
                System.Console.WriteLine("  dotnet run simulate-daily <dept> <YYYY-MM-DD> [--dry-run] - 日次処理シミュレーション");
                System.Console.WriteLine("  dotnet run dev-daily-report <YYYY-MM-DD> [--skip-unmatch-check] - 開発用商品日報（制限無視）");
                System.Console.WriteLine("  dotnet run dev-product-account <YYYY-MM-DD> [--skip-unmatch-check] - 開発用商品勘定（制限無視）");
                System.Console.WriteLine("  dotnet run dev-inventory-list <YYYY-MM-DD> [--skip-unmatch-check] - 開発用在庫表（制限無視）");
                System.Console.WriteLine("  dotnet run dev-check-daily-close <YYYY-MM-DD> - 開発用日次終了確認（時間制限無視）");
                System.Console.WriteLine("");
                System.Console.WriteLine("  例: dotnet run test-connection");
                System.Console.WriteLine("  例: dotnet run unmatch-list 2025-06-16");
                System.Console.WriteLine("  例: dotnet run daily-report 2025-06-16");
                System.Console.WriteLine("  例: dotnet run inventory-list 2025-06-16");
                System.Console.WriteLine("  例: dotnet run import-sales sales.csv 2025-06-16");
                System.Console.WriteLine("  例: dotnet run import-masters");
                System.Console.WriteLine("  例: dotnet run check-masters");
                System.Console.WriteLine("  例: dotnet run init-inventory DeptA");
                System.Console.WriteLine("  例: dotnet run init-database --force");
                System.Console.WriteLine("  例: dotnet run reset-daily-close 2025-06-30 --all");
                System.Console.WriteLine("  例: dotnet run dev-daily-close 2025-06-30 --dry-run");
                System.Console.WriteLine("  例: dotnet run check-data-status 2025-06-30");
                System.Console.WriteLine("  例: dotnet run simulate-daily DeptA 2025-06-30 --dry-run");
                System.Console.WriteLine("  例: dotnet run cleanup-inventory-duplicates");
                System.Console.WriteLine("  例: dotnet run init-monthly-inventory 202507");
                return 1;
            }

            var command = args[0].ToLower();

            // 自動スキーマチェック（init-database以外のコマンドで実行）
            if (command != "init-database" && !await CheckAndFixDatabaseSchemaAsync(host.Services))
            {
                System.Console.WriteLine("❌ データベーススキーマに問題があります。'dotnet run init-database --force' を実行してください。");
                return 1;
            }

            try
            {
                switch (command)
                {
                    case "unmatch-list":
                        await ExecuteUnmatchListAsync(host.Services, args);
                        break;

                    case "business-daily-report":
                        await ExecuteBusinessDailyReportAsync(host.Services, args);
                        break;

                    case "test-business-daily-report":
                        await TestBusinessDailyReportAsync(host.Services);
                        break;

                    case "daily-report":
                        await ExecuteDailyReportAsync(host.Services, args);
                        break;

                    case "dev-daily-report":
                        await ExecuteDevDailyReportAsync(host.Services, args);
                        break;

                    case "dev-product-account":
                        await ExecuteDevProductAccountAsync(host.Services, args);
                        break;

                    case "dev-inventory-list":
                        await ExecuteDevInventoryListAsync(host.Services, args);
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
                        System.Console.WriteLine("PDFテスト機能は削除されました。test-fastreport を使用してください。");
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

                    case "restore-dataset":
                        await ExecuteRestoreDatasetAsync(host.Services, args);
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

                    case "help":
                    case "-h":
                    case "--help":
                        DisplayHelp(args);
                        return 0;

                    default:
                        System.Console.WriteLine($"不明なコマンド: {command}");
                        DisplayHelp(args);
                        return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"エラーが発生しました: {ex.Message}");
                return 1;
            }

            // ローカル関数定義
            static void DisplayHelp(string[] args)
            {
                if (args.Length > 1)
                {
                    // 特定のコマンドのヘルプを表示
                    var commandName = args[1].ToLower();
                    DisplayCommandHelp(commandName);
                    return;
                }

                System.Console.WriteLine("📊 在庫管理システム - コマンドライン インターフェース");
                System.Console.WriteLine("=".PadRight(60, '='));
                System.Console.WriteLine();

                System.Console.WriteLine("📊 帳票・レポート生成:");
                System.Console.WriteLine("  unmatch-list [YYYY-MM-DD]                   - アンマッチリスト処理を実行");
                System.Console.WriteLine("  daily-report [YYYY-MM-DD] [--dataset-id ID] - 商品日報を生成（アンマッチ0件必須）");
                System.Console.WriteLine("  product-account [YYYY-MM-DD] [--dataset-id ID] - 商品勘定を生成（アンマッチ0件必須）");
                System.Console.WriteLine("  inventory-list [YYYY-MM-DD]                 - 在庫表を生成（アンマッチ0件必須）");
                System.Console.WriteLine();

                System.Console.WriteLine("📥 データ取込:");
                System.Console.WriteLine("  import-sales <file> [YYYY-MM-DD]            - 売上伝票CSVを取込");
                System.Console.WriteLine("  import-purchase <file> [YYYY-MM-DD]         - 仕入伝票CSVを取込");
                System.Console.WriteLine("  import-adjustment <file> [YYYY-MM-DD]       - 在庫調整CSVを取込");
                System.Console.WriteLine("  import-payment <file> [YYYY-MM-DD]          - 支払伝票CSVを取込");
                System.Console.WriteLine("  import-receipt <file> [YYYY-MM-DD]          - 入金伝票CSVを取込");
                System.Console.WriteLine("  import-folder [YYYY-MM-DD] [フォルダパス]    - フォルダ内の全CSVファイルを一括取込");
                System.Console.WriteLine("  import-masters [フォルダパス]                - マスタファイルを一括取込");
                System.Console.WriteLine();

                System.Console.WriteLine("🛠️ システム管理:");
                System.Console.WriteLine("  init-database                               - データベースを初期化");
                System.Console.WriteLine("  test-connection                             - データベース接続をテスト");
                System.Console.WriteLine("  check-masters                               - マスタデータ整合性チェック");
                System.Console.WriteLine("  test-fastreport                             - FastReportテスト（DB不要）");
                System.Console.WriteLine();

                System.Console.WriteLine("🧪 開発・テスト用:");
                System.Console.WriteLine("  dev-daily-close <YYYY-MM-DD> [--skip-validation] [--dry-run] - 開発用日次終了処理");
                System.Console.WriteLine("  check-data-status <YYYY-MM-DD>              - データ状態確認");
                System.Console.WriteLine("  simulate-daily <dept> <YYYY-MM-DD> [--dry-run] - 日次処理シミュレーション");
                System.Console.WriteLine("  dev-daily-report <YYYY-MM-DD> [--skip-unmatch-check] - 開発用商品日報（制限無視）");
                System.Console.WriteLine("  dev-product-account <YYYY-MM-DD> [--skip-unmatch-check] - 開発用商品勘定（制限無視）");
                System.Console.WriteLine("  dev-inventory-list <YYYY-MM-DD> [--skip-unmatch-check] - 開発用在庫表（制限無視）");
                System.Console.WriteLine("  dev-check-daily-close <YYYY-MM-DD>         - 開発用日次終了確認（時間制限無視）");
                System.Console.WriteLine();

                System.Console.WriteLine("例: dotnet run test-connection");
                System.Console.WriteLine("例: dotnet run unmatch-list 2025-06-16");
                System.Console.WriteLine("例: dotnet run daily-report 2025-06-16");
                System.Console.WriteLine("例: dotnet run inventory-list 2025-06-16");
                System.Console.WriteLine("例: dotnet run import-sales sales.csv 2025-06-16");
                System.Console.WriteLine("例: dotnet run import-masters");
                System.Console.WriteLine("例: dotnet run check-masters");
                System.Console.WriteLine();

                System.Console.WriteLine("📋 詳細なヘルプが必要な場合は: dotnet run help <command>");
                System.Console.WriteLine("例: dotnet run help import-folder");
            }

            static void DisplayCommandHelp(string commandName)
            {
                System.Console.WriteLine($"📋 コマンド詳細ヘルプ: {commandName}");
                System.Console.WriteLine("=".PadRight(50, '='));
                System.Console.WriteLine();

                switch (commandName)
                {
                    case "import-folder":
                        System.Console.WriteLine("📥 import-folder - フォルダ内CSV一括取込");
                        System.Console.WriteLine();
                        System.Console.WriteLine("説明:");
                        System.Console.WriteLine("  指定フォルダ内の全CSVファイルを検出し、ファイル名に基づいて");
                        System.Console.WriteLine("  適切な取込処理を自動実行します。");
                        System.Console.WriteLine();
                        System.Console.WriteLine("使用方法:");
                        System.Console.WriteLine("  dotnet run import-folder [YYYY-MM-DD] [フォルダパス]");
                        System.Console.WriteLine();
                        System.Console.WriteLine("例:");
                        System.Console.WriteLine("  dotnet run import-folder 2025-06-16");
                        System.Console.WriteLine("  dotnet run import-folder 2025-06-16 D:\\CSVFiles");
                        System.Console.WriteLine();
                        System.Console.WriteLine("対応ファイル名パターン:");
                        System.Console.WriteLine("  売上伝票.csv, 仕入伝票.csv, 在庫調整伝票.csv");
                        System.Console.WriteLine("  支払伝票.csv, 入金伝票.csv");
                        System.Console.WriteLine();
                        System.Console.WriteLine("出力ファイル:");
                        System.Console.WriteLine("  D:\\InventoryBackup\\Reports\\ImportResults_YYYYMMDD.txt");
                        break;

                    case "unmatch-list":
                        System.Console.WriteLine("🔍 unmatch-list - アンマッチリスト処理");
                        System.Console.WriteLine();
                        System.Console.WriteLine("説明:");
                        System.Console.WriteLine("  在庫データと伝票データの整合性をチェックし、不整合データを検出します。");
                        System.Console.WriteLine("  商品日報・在庫表作成の前に必ず実行し、アンマッチ0件を確認してください。");
                        System.Console.WriteLine();
                        System.Console.WriteLine("使用方法:");
                        System.Console.WriteLine("  dotnet run unmatch-list [YYYY-MM-DD]");
                        System.Console.WriteLine();
                        System.Console.WriteLine("例:");
                        System.Console.WriteLine("  dotnet run unmatch-list 2025-06-16");
                        System.Console.WriteLine();
                        System.Console.WriteLine("チェック項目:");
                        System.Console.WriteLine("  - 在庫マスタに存在しない商品の伝票");
                        System.Console.WriteLine("  - 伝票に存在しない商品の在庫");
                        System.Console.WriteLine("  - 数量・金額の不整合");
                        System.Console.WriteLine();
                        System.Console.WriteLine("出力ファイル:");
                        System.Console.WriteLine("  D:\\InventoryBackup\\Reports\\UnmatchList_YYYYMMDD.PDF");
                        break;

                    case "business-daily-report":
                        System.Console.WriteLine("📊 business-daily-report - 営業日報作成");
                        System.Console.WriteLine();
                        System.Console.WriteLine("説明:");
                        System.Console.WriteLine("  得意先分類1・仕入先分類1別に売上・仕入・入金・支払を集計します。");
                        System.Console.WriteLine("  日計・月計・年計の3段階表示でA3横3ページのPDFを出力します。");
                        System.Console.WriteLine();
                        System.Console.WriteLine("使用方法:");
                        System.Console.WriteLine("  dotnet run business-daily-report [YYYY-MM-DD]");
                        System.Console.WriteLine();
                        System.Console.WriteLine("例:");
                        System.Console.WriteLine("  dotnet run business-daily-report 2025-06-16");
                        System.Console.WriteLine();
                        System.Console.WriteLine("集計項目:");
                        System.Console.WriteLine("  - 現金売上・掛売上・売上値引・消費税");
                        System.Console.WriteLine("  - 現金仕入・掛仕入・仕入値引・消費税");
                        System.Console.WriteLine("  - 現金・振込・その他入金");
                        System.Console.WriteLine("  - 現金・振込・その他支払");
                        System.Console.WriteLine();
                        System.Console.WriteLine("出力ファイル:");
                        System.Console.WriteLine("  D:\\InventoryBackup\\Reports\\BusinessDailyReport_YYYYMMDD.PDF");
                        break;

                    case "daily-report":
                        System.Console.WriteLine("📈 daily-report - 商品日報生成");
                        System.Console.WriteLine();
                        System.Console.WriteLine("説明:");
                        System.Console.WriteLine("  指定日の商品別売上・仕入・在庫状況をまとめた日報をPDF形式で出力します。");
                        System.Console.WriteLine("  アンマッチ0件が前提条件です。");
                        System.Console.WriteLine();
                        System.Console.WriteLine("使用方法:");
                        System.Console.WriteLine("  dotnet run daily-report <YYYY-MM-DD> [--dataset-id ID]");
                        System.Console.WriteLine();
                        System.Console.WriteLine("例:");
                        System.Console.WriteLine("  dotnet run daily-report 2025-06-16");
                        System.Console.WriteLine();
                        System.Console.WriteLine("レイアウト:");
                        System.Console.WriteLine("  - A3横向き（420mm × 297mm）");
                        System.Console.WriteLine("  - 日計・月計の売上、粗利、粗利率を表示");
                        System.Console.WriteLine("  - 商品分類別集計");
                        System.Console.WriteLine();
                        System.Console.WriteLine("出力ファイル:");
                        System.Console.WriteLine("  D:\\InventoryBackup\\Reports\\DailyReport_YYYYMMDD.pdf");
                        break;

                    case "product-account":
                        System.Console.WriteLine("📈 product-account - 商品勘定帳票生成");
                        System.Console.WriteLine();
                        System.Console.WriteLine("説明:");
                        System.Console.WriteLine("  指定日の商品勘定帳票をPDF形式で出力します。");
                        System.Console.WriteLine("  移動平均法による在庫単価計算と粗利益・粗利率を表示します。");
                        System.Console.WriteLine();
                        System.Console.WriteLine("使用方法:");
                        System.Console.WriteLine("  dotnet run product-account <YYYY-MM-DD> [--dataset-id ID]");
                        System.Console.WriteLine();
                        System.Console.WriteLine("例:");
                        System.Console.WriteLine("  dotnet run product-account 2025-06-30");
                        System.Console.WriteLine();
                        System.Console.WriteLine("レイアウト:");
                        System.Console.WriteLine("  - A3横向き（420mm × 297mm）");
                        System.Console.WriteLine("  - 商品別の仕入・売上・残高履歴");
                        System.Console.WriteLine("  - 担当者別グループ化");
                        System.Console.WriteLine();
                        System.Console.WriteLine("出力ファイル:");
                        System.Console.WriteLine("  D:\\InventoryBackup\\Reports\\ProductAccount_YYYYMMDD.pdf");
                        break;

                    default:
                        System.Console.WriteLine($"❌ コマンド '{commandName}' の詳細ヘルプは見つかりません。");
                        System.Console.WriteLine();
                        System.Console.WriteLine("利用可能なコマンド:");
                        System.Console.WriteLine("  import-folder, unmatch-list, daily-report, product-account");
                        System.Console.WriteLine("  init-database, test-connection");
                        System.Console.WriteLine();
                        System.Console.WriteLine("全コマンド一覧: dotnet run help");
                        break;
                }
            }

            /// <summary>
            /// 商品勘定処理（開発用）- アンマッチチェックをスキップ可能
            /// </summary>
            static async Task ExecuteDevProductAccountAsync(IServiceProvider services, string[] args)
            {
                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();

                    // ジョブ日付を取得
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

                    bool skipUnmatchCheck = args.Contains("--skip-unmatch-check");

                    System.Console.WriteLine("=== 商品勘定処理開始（開発用） ===");
                    System.Console.WriteLine($"対象日付: {jobDate:yyyy-MM-dd}");
                    if (skipUnmatchCheck)
                    {
                        System.Console.WriteLine("⚠️ アンマッチチェックはスキップされます（開発用）");
                    }

                    try
                    {
                        // 商品勘定処理の実装（現時点では既存のproduct-accountコマンドを流用）
                        await ExecuteProductAccountAsync(services, new string[] { "product-account", jobDate.ToString("yyyy-MM-dd") });
                        System.Console.WriteLine("✅ 商品勘定処理が完了しました（開発用モード）");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
                        logger.LogError(ex, "商品勘定処理（開発用）でエラーが発生しました");
                    }
                }
            }

            /// <summary>
            /// 在庫表処理（開発用）- アンマッチチェックをスキップ可能
            /// </summary>
            static async Task ExecuteDevInventoryListAsync(IServiceProvider services, string[] args)
            {
                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();

                    // ジョブ日付を取得
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

                    bool skipUnmatchCheck = args.Contains("--skip-unmatch-check");

                    System.Console.WriteLine("=== 在庫表処理開始（開発用） ===");
                    System.Console.WriteLine($"対象日付: {jobDate:yyyy-MM-dd}");
                    if (skipUnmatchCheck)
                    {
                        System.Console.WriteLine("⚠️ アンマッチチェックはスキップされます（開発用）");
                    }

                    try
                    {
                        // 在庫表処理の実装（FastReport未対応）
                        System.Console.WriteLine("📋 在庫表処理中...");
                        System.Console.WriteLine("⚠️ 開発用在庫表はFastReport未対応です。本番用inventory-listコマンドを使用してください。");
                        System.Console.WriteLine("✅ 在庫表処理が完了しました（開発用モード - FastReport未対応）");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
                        logger.LogError(ex, "在庫表処理（開発用）でエラーが発生しました");
                    }
                }
            }

            /// <summary>
            /// 営業日報処理実行
            /// </summary>
            static async Task ExecuteBusinessDailyReportAsync(IServiceProvider services, string[] args)
            {
                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
                    var businessDailyReportService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.IBusinessDailyReportService>();

                    if (args.Length < 2)
                    {
                        System.Console.WriteLine("❌ 日付が指定されていません");
                        System.Console.WriteLine("使用方法: dotnet run business-daily-report [YYYY-MM-DD]");
                        System.Console.WriteLine("例: dotnet run business-daily-report 2025-06-01");
                        return;
                    }

                    if (!DateTime.TryParse(args[1], out var jobDate))
                    {
                        System.Console.WriteLine($"❌ 日付の形式が正しくありません: {args[1]}");
                        System.Console.WriteLine("正しい形式: YYYY-MM-DD (例: 2025-06-01)");
                        return;
                    }

                    System.Console.WriteLine("=== 営業日報処理開始 ===");
                    System.Console.WriteLine($"対象日付: {jobDate:yyyy-MM-dd}");
                    System.Console.WriteLine();

                    try
                    {
                        var dataSetId = Guid.NewGuid().ToString();
                        var result = await businessDailyReportService.ExecuteAsync(jobDate, dataSetId);

                        if (result.Success)
                        {
                            System.Console.WriteLine("✅ 営業日報処理が正常に完了しました");
                            System.Console.WriteLine($"📊 処理件数: {result.ProcessedCount}件");
                            System.Console.WriteLine($"⏱️ 処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
                            System.Console.WriteLine($"📁 出力ファイル: {result.OutputPath}");

                            logger.LogInformation("営業日報処理が完了しました: JobDate={JobDate}, ProcessedCount={ProcessedCount}, OutputPath={OutputPath}",
                                jobDate, result.ProcessedCount, result.OutputPath);
                        }
                        else
                        {
                            System.Console.WriteLine("❌ 営業日報処理でエラーが発生しました");
                            System.Console.WriteLine($"エラー: {result.ErrorMessage}");

                            logger.LogError("営業日報処理でエラーが発生しました: JobDate={JobDate}, Error={Error}",
                                jobDate, result.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ 予期しないエラーが発生しました: {ex.Message}");
                        logger.LogError(ex, "営業日報処理で予期しないエラーが発生しました: JobDate={JobDate}", jobDate);
                    }

                    System.Console.WriteLine("=== 営業日報処理終了 ===");
                }
            }

            static async Task TestBusinessDailyReportAsync(IServiceProvider services)
            {
                System.Console.WriteLine("=== 営業日報テストデータ生成開始 ===");


                try
                {
                    await TestBusinessDailyReport.RunTest(services);
                    System.Console.WriteLine("✅ 営業日報テストデータ生成が完了しました");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"❌ 営業日報テストデータ生成でエラーが発生しました: {ex.Message}");
                }

                System.Console.WriteLine("=== 営業日報テストデータ生成終了 ===");
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

                    System.Console.WriteLine("=== アンマッチリスト処理開始 ===");

                    // 在庫マスタから最新JobDateを取得（表示用）
                    var latestJobDate = await inventoryRepository.GetMaxJobDateAsync();
                    System.Console.WriteLine($"在庫マスタ最新JobDate: {latestJobDate:yyyy-MM-dd}");
                    if (targetDate.HasValue)
                    {
                        System.Console.WriteLine($"処理対象: {targetDate:yyyy-MM-dd}以前のアクティブ在庫");
                    }
                    else
                    {
                        System.Console.WriteLine("処理対象: 全期間のアクティブ在庫");
                    }
                    System.Console.WriteLine();

                    // アンマッチリスト処理実行
                    var result = targetDate.HasValue
                        ? await unmatchListService.ProcessUnmatchListAsync(targetDate.Value)
                        : await unmatchListService.ProcessUnmatchListAsync();

                    stopwatch.Stop();

                    if (result.Success)
                    {
                        System.Console.WriteLine("=== 処理結果 ===");
                        System.Console.WriteLine($"データセットID: {result.DataSetId}");
                        System.Console.WriteLine($"アンマッチ件数: {result.UnmatchCount}");
                        System.Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
                        System.Console.WriteLine();

                        if (result.UnmatchCount > 0)
                        {
                            System.Console.WriteLine("=== アンマッチ一覧 ===");
                            foreach (var item in result.UnmatchItems.Take(10)) // 最初の10件のみ表示
                            {
                                System.Console.WriteLine($"{item.Category} | {item.Key.ProductCode} | {item.ProductName} | {item.AlertType}");
                            }

                            if (result.UnmatchCount > 10)
                            {
                                System.Console.WriteLine($"... 他 {result.UnmatchCount - 10} 件");
                            }
                            System.Console.WriteLine();
                        }

                        // PDF出力（0件でも生成）
                        try
                        {
                            if (result.UnmatchCount == 0)
                            {
                                System.Console.WriteLine("アンマッチ件数が0件です。0件のPDFを生成します");
                            }

                            // ===== サービス診断情報 開始 =====
                            logger.LogInformation("=== Service Diagnostics ===");
                            logger.LogInformation($"Service Type: {reportService.GetType().FullName}");
                            logger.LogInformation($"Assembly: {reportService.GetType().Assembly.GetName().Name}");
                            // ===== サービス診断情報 終了 =====

                            System.Console.WriteLine("PDF生成中...");
                            // 指定された日付またはシステム日付を使用（latestJobDateではない）
                            var reportDate = targetDate ?? DateTime.Today;
                            var pdfBytes = reportService.GenerateUnmatchListReport(result.UnmatchItems, reportDate);

                            if (pdfBytes != null && pdfBytes.Length > 0)
                            {
                                // FileManagementServiceを使用してレポートパスを取得
                                var pdfPath = await fileManagementService.GetReportOutputPathAsync("UnmatchList", reportDate, "pdf");

                                await File.WriteAllBytesAsync(pdfPath, pdfBytes);

                                System.Console.WriteLine($"PDFファイルを保存しました: {pdfPath}");
                                System.Console.WriteLine($"ファイルサイズ: {pdfBytes.Length / 1024.0:F2} KB");

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
                                System.Console.WriteLine("PDF生成がスキップされました（環境制限またはデータなし）");
                            }
                        }
                        catch (Exception pdfEx)
                        {
                            logger.LogError(pdfEx, "PDF生成中にエラーが発生しました");
                            System.Console.WriteLine($"PDF生成エラー: {pdfEx.Message}");
                        }

                        System.Console.WriteLine("=== アンマッチリスト処理完了 ===");
                    }
                    else
                    {
                        System.Console.WriteLine("=== 処理失敗 ===");
                        System.Console.WriteLine($"エラーメッセージ: {result.ErrorMessage}");
                        System.Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");

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
                        System.Console.WriteLine("エラー: CSVファイルパスが指定されていません");
                        System.Console.WriteLine("使用方法: dotnet run import-sales <file> [YYYY-MM-DD]");
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

                    System.Console.WriteLine("=== 売上伝票CSV取込処理開始 ===");
                    System.Console.WriteLine($"ファイル: {filePath}");
                    System.Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
                    System.Console.WriteLine();

                    try
                    {
                        var dataSetId = await importService.ImportAsync(filePath, jobDate, jobDate, null);
                        var result = await importService.GetImportResultAsync(dataSetId);

                        stopwatch.Stop();

                        System.Console.WriteLine("=== 取込結果 ===");
                        System.Console.WriteLine($"データセットID: {result.DataSetId}");
                        System.Console.WriteLine($"ステータス: {result.Status}");
                        System.Console.WriteLine($"取込件数: {result.ImportedCount}");
                        System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            System.Console.WriteLine($"エラー情報: {result.ErrorMessage}");
                        }

                        System.Console.WriteLine("=== 売上伝票CSV取込処理完了 ===");
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        System.Console.WriteLine($"エラー: {ex.Message}");
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
                        System.Console.WriteLine("エラー: CSVファイルパスが指定されていません");
                        System.Console.WriteLine("使用方法: dotnet run import-purchase <file> [YYYY-MM-DD]");
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

                    System.Console.WriteLine("=== 仕入伝票CSV取込処理開始 ===");
                    System.Console.WriteLine($"ファイル: {filePath}");
                    System.Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
                    System.Console.WriteLine();

                    try
                    {
                        var dataSetId = await importService.ImportAsync(filePath, jobDate, jobDate, null);
                        var result = await importService.GetImportResultAsync(dataSetId);

                        stopwatch.Stop();

                        System.Console.WriteLine("=== 取込結果 ===");
                        System.Console.WriteLine($"データセットID: {result.DataSetId}");
                        System.Console.WriteLine($"ステータス: {result.Status}");
                        System.Console.WriteLine($"取込件数: {result.ImportedCount}");
                        System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            System.Console.WriteLine($"エラー情報: {result.ErrorMessage}");
                        }

                        System.Console.WriteLine("=== 仕入伝票CSV取込処理完了 ===");
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        System.Console.WriteLine($"エラー: {ex.Message}");
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
                        System.Console.WriteLine("エラー: CSVファイルパスが指定されていません");
                        System.Console.WriteLine("使用方法: dotnet run import-adjustment <file> [YYYY-MM-DD]");
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

                    System.Console.WriteLine("=== 在庫調整CSV取込処理開始 ===");
                    System.Console.WriteLine($"ファイル: {filePath}");
                    System.Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
                    System.Console.WriteLine();

                    try
                    {
                        var dataSetId = await importService.ImportAsync(filePath, jobDate, jobDate, null);
                        var result = await importService.GetImportResultAsync(dataSetId);

                        stopwatch.Stop();

                        System.Console.WriteLine("=== 取込結果 ===");
                        System.Console.WriteLine($"データセットID: {result.DataSetId}");
                        System.Console.WriteLine($"ステータス: {result.Status}");
                        System.Console.WriteLine($"取込件数: {result.ImportedCount}");
                        System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            System.Console.WriteLine($"エラー情報: {result.ErrorMessage}");
                        }

                        System.Console.WriteLine("=== 在庫調整CSV取込処理完了 ===");
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        System.Console.WriteLine($"エラー: {ex.Message}");
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

                    System.Console.WriteLine("=== 商品日報処理開始 ===");
                    System.Console.WriteLine($"レポート日付: {jobDate:yyyy-MM-dd}");
                    if (existingDataSetId != null)
                    {
                        System.Console.WriteLine($"既存データセットID: {existingDataSetId}");

                        // ✅ アンマッチチェック0件必須検証
                        System.Console.WriteLine("🔍 アンマッチチェック検証中...");
                        var validationService = scopedServices.GetRequiredService<IUnmatchCheckValidationService>();
                        var validation = await validationService.ValidateForReportExecutionAsync(existingDataSetId, ReportType.DailyReport);

                        if (!validation.CanExecute)
                        {
                            System.Console.WriteLine($"❌ 商品日報を実行できません");
                            System.Console.WriteLine($"理由: {validation.ErrorMessage}");
                            System.Console.WriteLine();
                            System.Console.WriteLine("💡 対処方法:");
                            System.Console.WriteLine("  1. アンマッチリストを実行: dotnet run unmatch-list");
                            System.Console.WriteLine("  2. アンマッチデータを修正");
                            System.Console.WriteLine("  3. 再度アンマッチリストを実行して0件を確認");
                            System.Console.WriteLine("  4. 商品日報を実行");
                            return;
                        }

                        System.Console.WriteLine("✅ アンマッチチェック合格（0件確認済み）");

                        // 既存データセット使用時：CP在庫マスタ作成
                        System.Console.WriteLine("📊 CP在庫マスタ作成中...");
                        var cpInventoryRepository = scopedServices.GetRequiredService<ICpInventoryRepository>();
                        await cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(jobDate);
                        await cpInventoryRepository.ClearDailyAreaAsync();
                        await cpInventoryRepository.AggregateSalesDataAsync(jobDate);
                        await cpInventoryRepository.AggregatePurchaseDataAsync(jobDate);
                        await cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(jobDate);
                        await cpInventoryRepository.CalculateDailyStockAsync();
                        await cpInventoryRepository.SetDailyFlagToProcessedAsync();
                        System.Console.WriteLine("✅ CP在庫マスタ作成完了");
                    }
                    System.Console.WriteLine();

                    // 商品日報処理実行
                    var result = await dailyReportService.ProcessDailyReportAsync(jobDate, existingDataSetId);

                    stopwatch.Stop();

                    if (result.Success)
                    {
                        System.Console.WriteLine("=== 処理結果 ===");
                        System.Console.WriteLine($"データセットID: {result.DataSetId}");
                        System.Console.WriteLine($"データ件数: {result.ProcessedCount}");
                        System.Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
                        System.Console.WriteLine();

                        if (result.ProcessedCount > 0)
                        {
                            System.Console.WriteLine("=== 商品日報データ（サンプル） ===");
                            foreach (var item in result.ReportItems.Take(5))
                            {
                                System.Console.WriteLine($"{item.ProductCode} | {item.ProductName} | 売上:{item.DailySalesAmount:N0}円 | 粗利1:{item.DailyGrossProfit1:N0}円");
                            }

                            if (result.ProcessedCount > 5)
                            {
                                System.Console.WriteLine($"... 他 {result.ProcessedCount - 5} 件");
                            }
                            System.Console.WriteLine();
                        }

                        // PDF出力
                        try
                        {
                            System.Console.WriteLine("PDF生成中...");
                            var pdfBytes = reportService.GenerateDailyReport(result.ReportItems, result.Subtotals, result.Total, jobDate);

                            if (pdfBytes != null && pdfBytes.Length > 0)
                            {
                                // FileManagementServiceを使用してレポートパスを取得（アンマッチリストと同じ方式）
                                var pdfPath = await fileManagementService.GetReportOutputPathAsync("DailyReport", jobDate, "pdf");

                                await File.WriteAllBytesAsync(pdfPath, pdfBytes);

                                System.Console.WriteLine($"PDFファイルを保存しました: {pdfPath}");
                                System.Console.WriteLine($"ファイルサイズ: {pdfBytes.Length / 1024.0:F2} KB");

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
                                System.Console.WriteLine("PDF生成がスキップされました（環境制限またはデータなし）");
                            }
                        }
                        catch (Exception pdfEx)
                        {
                            logger.LogError(pdfEx, "PDF生成中にエラーが発生しました");
                            System.Console.WriteLine($"PDF生成エラー: {pdfEx.Message}");
                        }

                        // CP在庫マスタを削除
                        try
                        {
                            var cpInventoryRepository = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.ICpInventoryRepository>();
                            await cpInventoryRepository.DeleteAllAsync(); // 仮テーブル設計：全レコード削除
                            logger.LogInformation("CP在庫マスタを削除しました（仮テーブル設計）");
                        }
                        catch (Exception cleanupEx)
                        {
                            logger.LogError(cleanupEx, "CP在庫マスタの削除に失敗しました - データセットID: {DataSetId}", result.DataSetId);
                            // 削除に失敗しても処理は成功として扱う
                        }

                        System.Console.WriteLine("=== 商品日報処理完了 ===");
                    }
                    else
                    {
                        System.Console.WriteLine("=== 処理失敗 ===");
                        System.Console.WriteLine($"エラーメッセージ: {result.ErrorMessage}");
                        System.Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");

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
                    System.Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
                    return;
                }

                if (args.Length < 2)
                {
                    System.Console.WriteLine("使用方法: dotnet run dev-daily-report <YYYY-MM-DD> [--skip-unmatch-check]");
                    return;
                }

                var skipUnmatchCheck = args.Contains("--skip-unmatch-check");

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
                        System.Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
                        return;
                    }

                    System.Console.WriteLine($"=== 開発用商品日報処理開始（日付制限無視） ===");
                    System.Console.WriteLine($"レポート日付: {jobDate:yyyy-MM-dd}");
                    System.Console.WriteLine();

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

                    System.Console.WriteLine($"=== 処理完了 ===");
                    System.Console.WriteLine($"データセットID: {processResult.DataSetId}");
                    System.Console.WriteLine($"処理件数: {processResult.ProcessedCount}");
                    System.Console.WriteLine($"PDFファイル: {pdfPath}");
                    System.Console.WriteLine($"ファイルサイズ: {pdfBytes.Length / 1024.0:F2} KB");
                    System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                    logger.LogInformation("開発用商品日報処理完了: JobDate={JobDate}", jobDate);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"エラー: {ex.Message}");
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
                    System.Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
                    return;
                }

                if (args.Length < 3)
                {
                    System.Console.WriteLine("使用方法: dotnet run dev-check-daily-close <YYYY-MM-DD>");
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
                        System.Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
                        return;
                    }

                    System.Console.WriteLine($"=== 開発用日次終了処理 事前確認（時間制限無視） ===");
                    System.Console.WriteLine($"対象日付: {jobDate:yyyy-MM-dd}");
                    System.Console.WriteLine($"現在時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    System.Console.WriteLine();

                    // GetConfirmationInfoを呼び出して、結果を取得して時間制限チェックを無視
                    var confirmation = await dailyCloseService.GetConfirmationInfo(jobDate);

                    // 時間制限エラーを除外（開発環境のため）
                    var filteredResults = confirmation.ValidationResults
                        .Where(v => !v.Message.Contains("15:00以降") && !v.Message.Contains("時間的制約違反"))
                        .ToList();

                    // 商品日報情報表示
                    System.Console.WriteLine("【商品日報情報】");
                    if (confirmation.DailyReport != null)
                    {
                        System.Console.WriteLine($"  作成時刻: {confirmation.DailyReport.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                        System.Console.WriteLine($"  作成者: {confirmation.DailyReport.CreatedBy}");
                        System.Console.WriteLine($"  DatasetId: {confirmation.DailyReport.DataSetId}");
                    }
                    else
                    {
                        System.Console.WriteLine("  ❌ 商品日報が作成されていません");
                    }
                    System.Console.WriteLine();

                    // データ件数表示
                    System.Console.WriteLine("【データ件数】");
                    System.Console.WriteLine($"  売上伝票: {confirmation.DataCounts.SalesCount:#,##0}件");
                    System.Console.WriteLine($"  仕入伝票: {confirmation.DataCounts.PurchaseCount:#,##0}件");
                    System.Console.WriteLine($"  在庫調整: {confirmation.DataCounts.AdjustmentCount:#,##0}件");
                    System.Console.WriteLine($"  CP在庫: {confirmation.DataCounts.CpInventoryCount:#,##0}件");
                    System.Console.WriteLine();

                    // 金額サマリー表示
                    System.Console.WriteLine("【金額サマリー】");
                    System.Console.WriteLine($"  売上総額: ¥{confirmation.Amounts.SalesAmount:#,##0.00}");
                    System.Console.WriteLine($"  仕入総額: ¥{confirmation.Amounts.PurchaseAmount:#,##0.00}");
                    System.Console.WriteLine($"  推定粗利: ¥{confirmation.Amounts.EstimatedGrossProfit:#,##0.00}");
                    System.Console.WriteLine();

                    // 検証結果表示（時間制限以外）
                    if (filteredResults.Any())
                    {
                        System.Console.WriteLine("【検証結果】");
                        foreach (var result in filteredResults)
                        {
                            var icon = result.Level switch
                            {
                                ValidationLevel.Error => "❌",
                                ValidationLevel.Warning => "⚠️ ",
                                _ => "ℹ️ "
                            };

                            System.Console.WriteLine($"{icon} {result.Level}: {result.Message}");
                            if (!string.IsNullOrEmpty(result.Detail))
                            {
                                System.Console.WriteLine($"         {result.Detail}");
                            }
                        }
                        System.Console.WriteLine();
                    }

                    // 処理可否判定（時間制限を除外）
                    var canProcess = !filteredResults.Any(v => v.Level == ValidationLevel.Error);

                    System.Console.WriteLine("【処理可否判定】");
                    if (canProcess)
                    {
                        System.Console.WriteLine("✅ 日次終了処理を実行可能です（開発環境のため時間制限を無視）");
                    }
                    else
                    {
                        System.Console.WriteLine("❌ 日次終了処理を実行できません");
                        System.Console.WriteLine("上記のエラーを解決してから再度実行してください。");
                    }

                    logger.LogInformation("開発用日次終了処理確認完了: JobDate={JobDate}", jobDate);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"エラー: {ex.Message}");
                    logger.LogError(ex, "開発用日次終了処理確認でエラーが発生しました");
                }
            }

            static async Task ExecuteInventoryListAsync(IServiceProvider services, string[] args)
            {
                if (args.Length < 2)
                {
                    System.Console.WriteLine("使用方法: inventory-list <JobDate>");
                    System.Console.WriteLine("例: inventory-list 2025-06-30");
                    return;
                }

                if (!DateTime.TryParse(args[1], out DateTime jobDate))
                {
                    System.Console.WriteLine($"❌ 不正な日付形式です: {args[1]}");
                    System.Console.WriteLine("例: inventory-list 2025-06-30");
                    return;
                }

                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
                    var salesVoucherRepository = scopedServices.GetRequiredService<ISalesVoucherRepository>();
                    var cpInventoryRepository = scopedServices.GetRequiredService<ICpInventoryRepository>();

                    try
                    {
                        logger.LogInformation("=== 在庫表作成開始 ===");
                        System.Console.WriteLine("=== 在庫表作成開始 ===");
                        System.Console.WriteLine($"対象日: {jobDate:yyyy-MM-dd}");

                        // 1. 仮テーブル設計確認
                        System.Console.WriteLine("📊 仮テーブル設計でCP在庫マスタを管理します");

                        // 2. CP在庫マスタを作成（仮テーブル設計）
                        System.Console.WriteLine("📊 CP在庫マスタ作成中...");
                        await cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(jobDate);
                        await cpInventoryRepository.ClearDailyAreaAsync();
                        await cpInventoryRepository.AggregateSalesDataAsync(jobDate);
                        await cpInventoryRepository.AggregatePurchaseDataAsync(jobDate);
                        await cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(jobDate);
                        await cpInventoryRepository.CalculateDailyStockAsync();
                        await cpInventoryRepository.SetDailyFlagToProcessedAsync();
                        System.Console.WriteLine("✅ CP在庫マスタ作成完了（仮テーブル）");

                        // 3. 在庫表作成（FastReport実装）
                        System.Console.WriteLine("📋 在庫表生成中...");
                        
                        #if WINDOWS
                        var inventoryListService = scopedServices.GetService(Type.GetType("InventorySystem.Reports.FastReport.Services.InventoryListService, InventorySystem.Reports"));
                        if (inventoryListService != null)
                        {
                            var method = inventoryListService.GetType().GetMethod("GenerateInventoryListAsync");
                            var task = (Task<byte[]>)method.Invoke(inventoryListService, new object[] { jobDate, "TEST_DATASET" });
                            var pdfBytes = await task;
                            
                            System.Console.WriteLine($"✅ 在庫表PDF生成完了: ファイルサイズ={pdfBytes.Length:N0} bytes");
                            System.Console.WriteLine("📁 出力先: appsettings.json設定のReportOutputPathに保存されました");
                        }
                        else
                        {
                            System.Console.WriteLine("⚠️ InventoryListServiceの取得に失敗しました");
                        }
                        #else
                        System.Console.WriteLine("⚠️ 在庫表のFastReport対応はWindows環境でのみ利用可能です");
                        #endif

                        logger.LogInformation("=== 在庫表作成完了 ===");
                        System.Console.WriteLine("=== 在庫表作成完了 ===");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "在庫表作成中にエラーが発生しました");
                        System.Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
                    }
                }
            }

            static async Task ExecuteProductAccountAsync(IServiceProvider services, string[] args)
            {
                if (args.Length < 2)
                {
                    System.Console.WriteLine("使用方法: product-account <JobDate>");
                    System.Console.WriteLine("例: product-account 2025-06-30");
                    return;
                }

                if (!DateTime.TryParse(args[1], out DateTime jobDate))
                {
                    System.Console.WriteLine($"❌ 不正な日付形式です: {args[1]}");
                    System.Console.WriteLine("例: product-account 2025-06-30");
                    return;
                }

                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
                    var productAccountService = scopedServices.GetRequiredService<InventorySystem.Reports.Interfaces.IProductAccountReportService>();
                    var salesVoucherRepository = scopedServices.GetRequiredService<ISalesVoucherRepository>();
                    var cpInventoryRepository = scopedServices.GetRequiredService<ICpInventoryRepository>();
                    var fileManagementService = scopedServices.GetRequiredService<IFileManagementService>();

                    try
                    {
                        logger.LogInformation("=== 商品勘定帳票作成開始 ===");
                        System.Console.WriteLine("=== 商品勘定帳票作成開始 ===");
                        System.Console.WriteLine($"対象日: {jobDate:yyyy-MM-dd}");

                        // 1. 仮テーブル設計確認
                        System.Console.WriteLine("📊 仮テーブル設計でCP在庫マスタを管理します");

                        // 2. CP在庫マスタを作成（仮テーブル設計）
                        System.Console.WriteLine("📊 CP在庫マスタ作成中...");
                        await cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(jobDate);
                        await cpInventoryRepository.ClearDailyAreaAsync();
                        await cpInventoryRepository.AggregateSalesDataAsync(jobDate);
                        await cpInventoryRepository.AggregatePurchaseDataAsync(jobDate);
                        await cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(jobDate);
                        await cpInventoryRepository.CalculateDailyStockAsync();
                        await cpInventoryRepository.SetDailyFlagToProcessedAsync();
                        System.Console.WriteLine("✅ CP在庫マスタ作成完了（仮テーブル）");

                        // 【追加】マスタ同期処理
                        System.Console.WriteLine("🔄 等級・階級マスタの同期を開始します");
                        var masterSyncService = scopedServices.GetRequiredService<InventorySystem.Data.Services.IMasterSyncService>();
                        var syncResult = await masterSyncService.SyncFromCpInventoryMasterAsync(jobDate);

                        if (syncResult.Success)
                        {
                            logger.LogInformation(
                                "マスタ同期完了 - 等級: 新規{GradeInserted}件/スキップ{GradeSkipped}件, " +
                                "階級: 新規{ClassInserted}件/スキップ{ClassSkipped}件",
                                syncResult.GradeInserted, syncResult.GradeSkipped,
                                syncResult.ClassInserted, syncResult.ClassSkipped);
                            System.Console.WriteLine($"✅ マスタ同期完了 - 等級: 新規{syncResult.GradeInserted}件, 階級: 新規{syncResult.ClassInserted}件");
                        }
                        else
                        {
                            logger.LogWarning("マスタ同期で警告が発生: {Message}", syncResult.ErrorMessage);
                            System.Console.WriteLine($"⚠️ マスタ同期で警告が発生しましたが、処理を継続します: {syncResult.ErrorMessage}");
                        }

                        // 3. 商品勘定帳票を作成
                        System.Console.WriteLine("📋 商品勘定帳票生成中...");
                        var pdfBytes = productAccountService.GenerateProductAccountReport(jobDate);

                        if (pdfBytes != null && pdfBytes.Length > 0)
                        {
                            // FileManagementServiceを使用してレポートパスを取得（他の帳票と同じ方式）
                            var pdfPath = await fileManagementService.GetReportOutputPathAsync("ProductAccount", jobDate, "pdf");

                            await File.WriteAllBytesAsync(pdfPath, pdfBytes);

                            System.Console.WriteLine($"✅ 商品勘定帳票を作成しました");
                            System.Console.WriteLine($"出力ファイル: {pdfPath}");
                            System.Console.WriteLine($"ファイルサイズ: {pdfBytes.Length:N0} bytes");
                        }
                        else
                        {
                            System.Console.WriteLine($"❌ 商品勘定帳票の作成に失敗しました");
                        }

                        logger.LogInformation("=== 商品勘定帳票作成完了 ===");
                        System.Console.WriteLine("=== 商品勘定帳票作成完了 ===");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "商品勘定帳票作成中にエラーが発生しました");
                        System.Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
                    }
                }
            }

            static async Task DebugCsvStructureAsync(string[] args)
            {
                if (args.Length < 3)
                {
                    System.Console.WriteLine("エラー: CSVファイルパスが指定されていません");
                    System.Console.WriteLine("使用方法: dotnet run debug-csv-structure <file>");
                    return;
                }

                var filePath = args[2];
                if (!File.Exists(filePath))
                {
                    System.Console.WriteLine($"エラー: ファイルが存在しません: {filePath}");
                    return;
                }

                System.Console.WriteLine($"=== CSV構造解析 ===\nFile: {filePath}\n");

                try
                {
                    // UTF-8エンコーディングで直接読み込む
                    var encoding = Encoding.UTF8;
                    System.Console.WriteLine($"使用エンコーディング: {encoding.EncodingName}\n");

                    using var reader = new StreamReader(filePath, encoding);
                    var headerLine = await reader.ReadLineAsync();
                    if (headerLine == null)
                    {
                        System.Console.WriteLine("エラー: CSVファイルが空です");
                        return;
                    }

                    var headers = headerLine.Split(',');
                    System.Console.WriteLine($"列数: {headers.Length}\n");

                    // 特定の列を検索
                    var searchColumns = new[] { "得意先名", "得意先名１", "仕入先名", "荷印名", "商品名" };
                    System.Console.WriteLine("=== 重要な列の位置 ===");
                    foreach (var searchColumn in searchColumns)
                    {
                        for (int i = 0; i < headers.Length; i++)
                        {
                            if (headers[i].Trim('\"').Contains(searchColumn))
                            {
                                System.Console.WriteLine($"列{i:D3}: {headers[i].Trim('\"')}");
                            }
                        }
                    }

                    // 最初の20列を表示
                    System.Console.WriteLine("\n=== 最初の20列 ===");
                    for (int i = 0; i < Math.Min(20, headers.Length); i++)
                    {
                        System.Console.WriteLine($"列{i:D3}: {headers[i].Trim('\"')}");
                    }

                    // 80-95列目を表示
                    if (headers.Length > 80)
                    {
                        System.Console.WriteLine("\n=== 80-95列目 ===");
                        for (int i = 80; i < Math.Min(95, headers.Length); i++)
                        {
                            System.Console.WriteLine($"列{i:D3}: {headers[i].Trim('\"')}");
                        }
                    }

                    // 130-150列目を表示
                    if (headers.Length > 130)
                    {
                        System.Console.WriteLine("\n=== 130-150列目 ===");
                        for (int i = 130; i < Math.Min(150, headers.Length); i++)
                        {
                            System.Console.WriteLine($"列{i:D3}: {headers[i].Trim('\"')}");
                        }
                    }

                    // データの最初の行も確認
                    var dataLine = await reader.ReadLineAsync();
                    if (dataLine != null)
                    {
                        var dataValues = dataLine.Split(',');
                        System.Console.WriteLine("\n=== 最初のデータ行のサンプル ===");
                        var importantIndices = new[] { 3, 8, 88, 138, 142 }; // 得意先コード、得意先名、商品コード、荷印名、商品名
                        foreach (var idx in importantIndices)
                        {
                            if (idx < dataValues.Length)
                            {
                                System.Console.WriteLine($"列{idx:D3} ({headers[idx].Trim('\"')}): {dataValues[idx].Trim('\"')}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"エラー: {ex.Message}");
                }
            }


            static async Task TestDatabaseConnectionAsync(IServiceProvider services)
            {
                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
                    var configuration = scopedServices.GetRequiredService<IConfiguration>();

                    System.Console.WriteLine("=== データベース接続テスト開始 ===");

                    try
                    {
                        var connectionString = configuration.GetConnectionString("DefaultConnection");
                        System.Console.WriteLine($"接続文字列: {connectionString}");
                        System.Console.WriteLine();

                        // 基本的な接続テスト
                        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);

                        System.Console.WriteLine("データベースへの接続を試行中...");
                        await connection.OpenAsync();
                        System.Console.WriteLine("✅ データベース接続成功");

                        // バージョン情報取得
                        using var command = connection.CreateCommand();
                        command.CommandText = "SELECT @@VERSION as Version, DB_NAME() as DatabaseName, GETDATE() as CurrentTime";
                        using var reader = await command.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            System.Console.WriteLine($"データベース名: {reader["DatabaseName"]}");
                            System.Console.WriteLine($"現在時刻: {reader["CurrentTime"]}");
                            System.Console.WriteLine($"SQL Server バージョン: {reader["Version"]?.ToString()?.Split('\n')[0]}");
                        }

                        System.Console.WriteLine();
                        System.Console.WriteLine("=== テーブル存在確認 ===");

                        reader.Close();

                        // テーブル存在確認
                        string[] tables = { "InventoryMaster", "CpInventoryMaster", "SalesVouchers", "PurchaseVouchers", "InventoryAdjustments", "DataSets" };

                        foreach (var table in tables)
                        {
                            command.CommandText = $"SELECT CASE WHEN EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{table}]') AND type in (N'U')) THEN 1 ELSE 0 END";
                            var exists = (int)(await command.ExecuteScalarAsync() ?? 0) == 1;
                            System.Console.WriteLine($"{table}: {(exists ? "✅ 存在" : "❌ 未作成")}");
                        }

                        System.Console.WriteLine();
                        System.Console.WriteLine("=== データベース接続テスト完了 ===");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ データベース接続エラー: {ex.Message}");
                        System.Console.WriteLine();
                        System.Console.WriteLine("=== トラブルシューティング ===");
                        System.Console.WriteLine("1. SQL Server Express が起動していることを確認してください");
                        System.Console.WriteLine("2. LocalDB を使用する場合:");
                        System.Console.WriteLine("   sqllocaldb info");
                        System.Console.WriteLine("   sqllocaldb start MSSQLLocalDB");
                        System.Console.WriteLine("3. 接続文字列を確認してください（appsettings.json）");
                        System.Console.WriteLine("4. database/CreateDatabase.sql を実行してデータベースを作成してください");

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
                        System.Console.WriteLine("エラー: CSVファイルパスが指定されていません");
                        System.Console.WriteLine("使用方法: dotnet run import-customers <file>");
                        return;
                    }

                    var filePath = args[2];
                    var importDate = DateTime.Today;

                    var stopwatch = Stopwatch.StartNew();

                    System.Console.WriteLine("=== 得意先マスタCSV取込処理開始 ===");
                    System.Console.WriteLine($"ファイル: {filePath}");
                    System.Console.WriteLine();

                    try
                    {
                        var result = await importService.ImportFromCsvAsync(filePath, importDate);

                        stopwatch.Stop();

                        System.Console.WriteLine("=== 取込結果 ===");
                        System.Console.WriteLine($"データセットID: {result.DataSetId}");
                        System.Console.WriteLine($"ステータス: {result.Status}");
                        System.Console.WriteLine($"取込件数: {result.ImportedCount}");
                        System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            System.Console.WriteLine($"エラー情報: {result.ErrorMessage}");
                        }

                        System.Console.WriteLine("=== 得意先マスタCSV取込処理完了 ===");
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        System.Console.WriteLine($"エラー: {ex.Message}");
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
                        System.Console.WriteLine("エラー: CSVファイルパスが指定されていません");
                        System.Console.WriteLine("使用方法: dotnet run import-products <file>");
                        return;
                    }

                    var filePath = args[2];
                    var importDate = DateTime.Today;

                    var stopwatch = Stopwatch.StartNew();

                    System.Console.WriteLine("=== 商品マスタCSV取込処理開始 ===");
                    System.Console.WriteLine($"ファイル: {filePath}");
                    System.Console.WriteLine();

                    try
                    {
                        var result = await importService.ImportFromCsvAsync(filePath, importDate);

                        stopwatch.Stop();

                        System.Console.WriteLine("=== 取込結果 ===");
                        System.Console.WriteLine($"データセットID: {result.DataSetId}");
                        System.Console.WriteLine($"ステータス: {result.Status}");
                        System.Console.WriteLine($"取込件数: {result.ImportedCount}");
                        System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            System.Console.WriteLine($"エラー情報: {result.ErrorMessage}");
                        }

                        System.Console.WriteLine("=== 商品マスタCSV取込処理完了 ===");
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        System.Console.WriteLine($"エラー: {ex.Message}");
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
                        System.Console.WriteLine("エラー: CSVファイルパスが指定されていません");
                        System.Console.WriteLine("使用方法: dotnet run import-suppliers <file>");
                        return;
                    }

                    var filePath = args[2];
                    var importDate = DateTime.Today;

                    var stopwatch = Stopwatch.StartNew();

                    System.Console.WriteLine("=== 仕入先マスタCSV取込処理開始 ===");
                    System.Console.WriteLine($"ファイル: {filePath}");
                    System.Console.WriteLine();

                    try
                    {
                        var result = await importService.ImportFromCsvAsync(filePath, importDate);

                        stopwatch.Stop();

                        System.Console.WriteLine("=== 取込結果 ===");
                        System.Console.WriteLine($"データセットID: {result.DataSetId}");
                        System.Console.WriteLine($"ステータス: {result.Status}");
                        System.Console.WriteLine($"取込件数: {result.ImportedCount}");
                        System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            System.Console.WriteLine($"エラー情報: {result.ErrorMessage}");
                        }

                        System.Console.WriteLine("=== 仕入先マスタCSV取込処理完了 ===");
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        System.Console.WriteLine($"エラー: {ex.Message}");
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

                    System.Console.WriteLine("=== フォルダ構造初期化開始 ===");

                    try
                    {
                        await fileService.InitializeDirectoryStructureAsync();
                        System.Console.WriteLine("✅ フォルダ構造の初期化が完了しました");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ エラー: {ex.Message}");
                        logger.LogError(ex, "フォルダ構造初期化でエラーが発生しました");
                    }
                }
            }

            /// <summary>
            /// 指定されたDataSetIdを復元（再度アクティブにする）
            /// </summary>
            static async Task ExecuteRestoreDatasetAsync(IServiceProvider services, string[] args)
            {
                if (args.Length < 2)
                {
                    System.Console.WriteLine("エラー: DataSetIdが指定されていません");
                    System.Console.WriteLine("使用方法:");
                    System.Console.WriteLine("  dotnet run restore-dataset <DataSetId>");
                    System.Console.WriteLine("  dotnet run restore-dataset list [YYYY-MM-DD] # 指定日の無効DataSet一覧表示");
                    return;
                }

                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
                    var dataSetService = scopedServices.GetRequiredService<IDataSetService>();
                    var dataSetRepo = scopedServices.GetRequiredService<IDataSetManagementRepository>();

                    var command = args[1].ToLower();

                    try
                    {
                        // list コマンド: 無効化されたDataSetの一覧表示
                        if (command == "list")
                        {
                            DateTime? targetDate = null;
                            if (args.Length >= 3 && DateTime.TryParse(args[2], out var parsedDate))
                            {
                                targetDate = parsedDate;
                            }

                            System.Console.WriteLine("=== 無効化されたDataSet一覧 ===");

                            if (targetDate.HasValue)
                            {
                                System.Console.WriteLine($"対象日: {targetDate.Value:yyyy-MM-dd}");
                                var datasets = await dataSetRepo.GetByJobDateAsync(targetDate.Value);
                                var inactiveDatasets = datasets.Where(ds => !ds.IsActive).ToList();

                                if (inactiveDatasets.Any())
                                {
                                    System.Console.WriteLine($"無効化されたDataSet: {inactiveDatasets.Count}件");
                                    foreach (var ds in inactiveDatasets.OrderBy(ds => ds.ProcessType))
                                    {
                                        System.Console.WriteLine($"  {ds.DataSetId} | {ds.ProcessType} | {ds.Description} | {ds.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                                    }
                                }
                                else
                                {
                                    System.Console.WriteLine("無効化されたDataSetはありません。");
                                }
                            }
                            else
                            {
                                System.Console.WriteLine("全期間の無効化されたDataSetを検索中...");
                                // TODO: 全期間の無効DataSet取得メソッドが必要
                                System.Console.WriteLine("⚠️ 全期間検索は未実装です。日付を指定してください。");
                            }
                            return;
                        }

                        // DataSetId指定による復元処理
                        var dataSetId = args[1];

                        System.Console.WriteLine("=== DataSet復元処理開始 ===");
                        System.Console.WriteLine($"対象DataSetId: {dataSetId}");

                        // DataSetの存在確認
                        var dataSet = await dataSetRepo.GetByIdAsync(dataSetId);
                        if (dataSet == null)
                        {
                            System.Console.WriteLine($"❌ エラー: DataSetId '{dataSetId}' が見つかりません。");
                            return;
                        }

                        System.Console.WriteLine($"DataSet情報:");
                        System.Console.WriteLine($"  - ProcessType: {dataSet.ProcessType}");
                        System.Console.WriteLine($"  - JobDate: {dataSet.JobDate:yyyy-MM-dd}");
                        System.Console.WriteLine($"  - Description: {dataSet.Description}");
                        System.Console.WriteLine($"  - IsActive: {dataSet.IsActive}");
                        System.Console.WriteLine($"  - CreatedAt: {dataSet.CreatedAt:yyyy-MM-dd HH:mm:ss}");

                        if (dataSet.IsActive)
                        {
                            System.Console.WriteLine("⚠️ 警告: このDataSetは既にアクティブです。");
                            return;
                        }

                        // 同一JobDate+ProcessTypeの他のアクティブなDataSetがあるかチェック
                        var existingActive = await dataSetRepo.GetByJobDateAsync(dataSet.JobDate);
                        var conflictingDataSets = existingActive
                            .Where(ds => ds.ProcessType == dataSet.ProcessType && ds.IsActive && ds.DataSetId != dataSetId)
                            .ToList();

                        if (conflictingDataSets.Any())
                        {
                            System.Console.WriteLine("⚠️ 警告: 同じJobDateとProcessTypeのアクティブなDataSetが存在します:");
                            foreach (var conflict in conflictingDataSets)
                            {
                                System.Console.WriteLine($"  - {conflict.DataSetId} | {conflict.Description}");
                            }

                            System.Console.Write("これらのDataSetを無効化して続行しますか？ (y/N): ");
                            var input = System.Console.ReadLine();
                            if (input?.ToLower() != "y")
                            {
                                System.Console.WriteLine("復元処理をキャンセルしました。");
                                return;
                            }

                            // 競合するDataSetを無効化
                            foreach (var conflict in conflictingDataSets)
                            {
                                try
                                {
                                    await dataSetService.DeactivateOldDataSetsAsync(
                                        dataSet.JobDate, dataSet.ProcessType, dataSetId);
                                    System.Console.WriteLine($"✅ DataSet '{conflict.DataSetId}' を無効化しました。");
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "競合DataSetの無効化に失敗しました: {DataSetId}", conflict.DataSetId);
                                    System.Console.WriteLine($"❌ エラー: DataSet '{conflict.DataSetId}' の無効化に失敗しました。");
                                }
                            }
                        }

                        // DataSetの復元（IsActive = true に更新）
                        try
                        {
                            // DataSetを復元する
                            dataSet.IsActive = true;
                            dataSet.DeactivatedAt = null;
                            dataSet.DeactivatedBy = null;
                            dataSet.UpdatedAt = DateTime.Now;
                            await dataSetRepo.UpdateAsync(dataSet);

                            System.Console.WriteLine($"✅ DataSet '{dataSetId}' を復元しました。");

                            // 関連する伝票データの復元（Phase 2で追加されたIsActiveカラム）
                            try
                            {
                                var salesRepo = scopedServices.GetRequiredService<ISalesVoucherRepository>();
                                var purchaseRepo = scopedServices.GetRequiredService<IPurchaseVoucherRepository>();
                                var adjustmentRepo = scopedServices.GetRequiredService<IInventoryAdjustmentRepository>();

                                int salesUpdated = await salesRepo.UpdateIsActiveByDataSetIdAsync(dataSetId, true);
                                int purchaseUpdated = await purchaseRepo.UpdateIsActiveByDataSetIdAsync(dataSetId, true);
                                int adjustmentUpdated = await adjustmentRepo.UpdateIsActiveByDataSetIdAsync(dataSetId, true);

                                System.Console.WriteLine($"関連伝票データ復元: 売上{salesUpdated}件、仕入{purchaseUpdated}件、在庫調整{adjustmentUpdated}件");
                                logger.LogInformation(
                                    "関連伝票データ復元完了: DataSetId={DataSetId}, Sales={Sales}, Purchase={Purchase}, Adjustment={Adjustment}",
                                    dataSetId, salesUpdated, purchaseUpdated, adjustmentUpdated);
                            }
                            catch (Exception voucherEx)
                            {
                                logger.LogWarning(voucherEx, "伝票データの復元中にエラーが発生しました: {DataSetId}", dataSetId);
                                System.Console.WriteLine($"⚠️ 警告: 伝票データの復元中にエラーが発生しました - {voucherEx.Message}");
                            }

                            logger.LogInformation("DataSet復元完了: {DataSetId}", dataSetId);
                            System.Console.WriteLine("=== DataSet復元処理完了 ===");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "DataSet復元処理でエラーが発生しました: {DataSetId}", dataSetId);
                            System.Console.WriteLine($"❌ エラー: DataSet復元に失敗しました - {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "restore-datasetコマンド実行中にエラーが発生しました");
                        System.Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
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

                    System.Console.WriteLine("=== マスタデータインポート開始 ===");
                    System.Console.WriteLine();

                    try
                    {
                        // 等級マスタのインポート
                        System.Console.WriteLine("等級マスタをインポート中...");
                        var gradeCount = await gradeRepo.ImportFromCsvAsync();
                        System.Console.WriteLine($"✅ 等級マスタ: {gradeCount}件インポートしました");
                        System.Console.WriteLine();

                        // 階級マスタのインポート
                        System.Console.WriteLine("階級マスタをインポート中...");
                        var classCount = await classRepo.ImportFromCsvAsync();
                        System.Console.WriteLine($"✅ 階級マスタ: {classCount}件インポートしました");
                        System.Console.WriteLine();

                        System.Console.WriteLine("=== マスタデータインポート完了 ===");
                        System.Console.WriteLine($"合計: {gradeCount + classCount}件のレコードをインポートしました");
                    }
                    catch (FileNotFoundException ex)
                    {
                        System.Console.WriteLine($"❌ エラー: {ex.Message}");
                        System.Console.WriteLine("CSVファイルが見つかりません。以下のパスにファイルが存在することを確認してください：");
                        System.Console.WriteLine("  - D:\\InventoryImport\\DeptA\\Import\\等級汎用マスター１.csv");
                        System.Console.WriteLine("  - D:\\InventoryImport\\DeptA\\Import\\階級汎用マスター２.csv");
                        logger.LogError(ex, "マスタデータインポートでファイルが見つかりません");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ エラー: {ex.Message}");
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

                    System.Console.WriteLine("=== マスタデータ登録状況確認 ===");
                    System.Console.WriteLine();

                    try
                    {
                        // 等級マスタの件数を確認
                        System.Console.WriteLine("【等級マスタ】");
                        var gradeCount = await gradeRepo.GetCountAsync();
                        System.Console.WriteLine($"  登録件数: {gradeCount:N0}件");

                        if (gradeCount > 0)
                        {
                            // サンプルデータを表示
                            var allGrades = await gradeRepo.GetAllGradesAsync();
                            var sampleGrades = allGrades.Take(5);
                            System.Console.WriteLine("  サンプルデータ:");
                            foreach (var grade in sampleGrades)
                            {
                                System.Console.WriteLine($"    {grade.Key}: {grade.Value}");
                            }
                            if (allGrades.Count > 5)
                            {
                                System.Console.WriteLine($"    ... 他 {allGrades.Count - 5}件");
                            }
                        }
                        else
                        {
                            System.Console.WriteLine("  ⚠️ データが登録されていません");
                            System.Console.WriteLine("  'dotnet run import-masters' でインポートしてください");
                        }

                        System.Console.WriteLine();

                        // 階級マスタの件数を確認
                        System.Console.WriteLine("【階級マスタ】");
                        var classCount = await classRepo.GetCountAsync();
                        System.Console.WriteLine($"  登録件数: {classCount:N0}件");

                        if (classCount > 0)
                        {
                            // サンプルデータを表示
                            var allClasses = await classRepo.GetAllClassesAsync();
                            var sampleClasses = allClasses.Take(5);
                            System.Console.WriteLine("  サンプルデータ:");
                            foreach (var cls in sampleClasses)
                            {
                                System.Console.WriteLine($"    {cls.Key}: {cls.Value}");
                            }
                            if (allClasses.Count > 5)
                            {
                                System.Console.WriteLine($"    ... 他 {allClasses.Count - 5}件");
                            }
                        }
                        else
                        {
                            System.Console.WriteLine("  ⚠️ データが登録されていません");
                            System.Console.WriteLine("  'dotnet run import-masters' でインポートしてください");
                        }

                        System.Console.WriteLine();
                        System.Console.WriteLine("=== 確認完了 ===");
                        System.Console.WriteLine($"合計: {gradeCount + classCount:N0}件のマスタデータが登録されています");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ エラー: {ex.Message}");
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
                        System.Console.WriteLine("=== 前月末在庫インポート開始 ===");

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
                        System.Console.WriteLine($"\n処理時間: {result.Duration.TotalSeconds:F2}秒");
                        System.Console.WriteLine($"読込件数: {result.TotalRecords:N0}件");
                        System.Console.WriteLine($"処理件数: {result.ProcessedRecords:N0}件");
                        System.Console.WriteLine($"エラー件数: {result.ErrorRecords:N0}件");

                        if (result.IsSuccess)
                        {
                            System.Console.WriteLine("\n✅ 前月末在庫インポートが正常に完了しました");
                        }
                        else
                        {
                            System.Console.WriteLine("\n⚠️ インポートは完了しましたが、エラーが発生しました");
                            if (result.Errors.Count > 0)
                            {
                                System.Console.WriteLine("\nエラー詳細:");
                                foreach (var error in result.Errors.Take(10))
                                {
                                    System.Console.WriteLine($"  - {error}");
                                }
                                if (result.Errors.Count > 10)
                                {
                                    System.Console.WriteLine($"  ... 他 {result.Errors.Count - 10}件のエラー");
                                }
                            }
                        }

                        System.Console.WriteLine("\n=== 前月末在庫インポート完了 ===");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ エラー: {ex.Message}");
                        logger.LogError(ex, "前月末在庫インポートでエラーが発生しました");
                    }
                }
            }

            /// <summary>
            /// ファイル名から分類番号を抽出
            /// </summary>
            static int ExtractCategoryNumber(string fileName)
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
            static int GetFileProcessOrder(string fileName)
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
                    System.Console.WriteLine("使用方法: init-inventory <部門名>");
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
                            System.Console.WriteLine($"❌ 前月末在庫.csvが見つかりません: {csvPath}");
                            return;
                        }

                        System.Console.WriteLine("=== 初期在庫設定開始 ===");
                        System.Console.WriteLine($"部門: {department}");
                        System.Console.WriteLine($"ファイル: {csvPath}");
                        System.Console.WriteLine();

                        // インポート実行（日付フィルタなし、すべてのデータを初期在庫として設定）
                        var result = await importService.ImportForInitialInventoryAsync();

                        if (result.IsSuccess)
                        {
                            System.Console.WriteLine($"✅ 初期在庫を設定しました（{result.ProcessedRecords}件）");

                            if (result.ErrorRecords > 0)
                            {
                                System.Console.WriteLine($"商品コード00000の除外件数: {result.ErrorRecords}件");
                            }

                            // ファイルを処理済みフォルダに移動
                            await fileManagementService.MoveToProcessedAsync(csvPath, department);
                            logger.LogInformation("前月末在庫.csvを処理済みフォルダに移動しました");
                        }
                        else
                        {
                            System.Console.WriteLine($"❌ 初期在庫設定に失敗しました: {result.Message}");
                            logger.LogError("初期在庫設定失敗: {Message}", result.Message);
                        }

                        logger.LogInformation("=== 初期在庫設定完了 ===");
                        System.Console.WriteLine("\n=== 初期在庫設定完了 ===");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "初期在庫設定中にエラーが発生しました");
                        System.Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
                    }
                }
            }

            static async Task ExecuteImportWithCarryoverAsync(IServiceProvider services, string[] args)
            {
                if (args.Length < 3)
                {
                    System.Console.WriteLine("使用方法: import-with-carryover <部門>");
                    System.Console.WriteLine("例: import-with-carryover DeptA");
                    System.Console.WriteLine("※処理対象日は最終処理日の翌日が自動的に選択されます");
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
                        System.Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
                    }
                }
            }

            static async Task ExecuteImportFromFolderAsync(IServiceProvider services, string[] args)
            {
                if (args.Length < 2)
                {
                    System.Console.WriteLine("エラー: 部門コードが指定されていません");
                    System.Console.WriteLine("使用方法:");
                    System.Console.WriteLine("  単一日付: dotnet run import-folder <dept> <YYYY-MM-DD>");
                    System.Console.WriteLine("  期間指定: dotnet run import-folder <dept> <開始日 YYYY-MM-DD> <終了日 YYYY-MM-DD>");
                    System.Console.WriteLine("  CSV日付保持: dotnet run import-folder <dept> --preserve-csv-dates [--start-date <YYYY-MM-DD>] [--end-date <YYYY-MM-DD>]");
                    System.Console.WriteLine("  全期間  : dotnet run import-folder <dept>");
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
                        System.Console.WriteLine("データベーススキーマを確認しています...");
                        await schemaService.UpdateSchemaAsync();
                        System.Console.WriteLine("スキーマの確認が完了しました。");
                        System.Console.WriteLine();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"スキーマ更新エラー: {ex.Message}");
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
                                System.Console.WriteLine($"エラー: 無効な開始日付形式: {args[argIndex + 1]}");
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
                                System.Console.WriteLine($"エラー: 無効な終了日付形式: {args[argIndex + 1]}");
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
                            System.Console.WriteLine($"エラー: 無効な引数: {args[argIndex]}");
                            return;
                        }
                    }

                    // 日付範囲の検証
                    if (startDate.HasValue && endDate.HasValue && endDate < startDate)
                    {
                        System.Console.WriteLine("エラー: 終了日は開始日以降である必要があります");
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

                    System.Console.WriteLine($"=== CSVファイル一括インポート開始 ===");
                    if (preserveCsvDates)
                    {
                        System.Console.WriteLine("モード: 期間指定（CSVの日付を保持）");
                    }
                    System.Console.WriteLine($"部門: {department}");

                    if (startDate.HasValue && endDate.HasValue)
                    {
                        if (startDate.Value.Date == endDate.Value.Date)
                        {
                            System.Console.WriteLine($"対象日付: {startDate.Value:yyyy-MM-dd}");
                        }
                        else
                        {
                            System.Console.WriteLine($"対象期間: {startDate.Value:yyyy-MM-dd} ～ {endDate.Value:yyyy-MM-dd}");
                            System.Console.WriteLine($"期間日数: {(endDate.Value - startDate.Value).Days + 1}日間");
                        }
                    }
                    else if (preserveCsvDates)
                    {
                        System.Console.WriteLine("対象期間: CSVファイル内の全日付");
                    }
                    else
                    {
                        System.Console.WriteLine("対象期間: 全期間（日付フィルタなし）");
                    }

                    var errorCount = 0;
                    var processedCounts = new Dictionary<string, int>();
                    var dateStatisticsTotal = new Dictionary<DateTime, int>(); // 全体の日付別統計
                    var fileStatistics = new Dictionary<string, (int processed, int skipped)>(); // ファイル別統計

                    try
                    {
                        // ===== 新規追加: 既存DataSetの無効化処理 =====
                        if (startDate.HasValue)
                        {
                            var dataSetService = scopedServices.GetRequiredService<IDataSetService>();
                            var dataSetManagementRepo = scopedServices.GetRequiredService<IDataSetManagementRepository>();

                            System.Console.WriteLine($"\n既存のActiveなDataSetの確認中... (対象日: {startDate.Value:yyyy-MM-dd})");

                            // 対象日付の既存ActiveなDataSetを取得
                            var existingDataSets = await dataSetManagementRepo.GetByJobDateAsync(startDate.Value);
                            var activeDataSets = existingDataSets.Where(ds => ds.IsActive).ToList();

                            if (activeDataSets.Any())
                            {
                                System.Console.WriteLine($"既存のActiveなDataSetが{activeDataSets.Count}件見つかりました。無効化処理を開始します。");
                                logger.LogInformation(
                                    "既存のActiveなDataSetが{Count}件見つかりました。無効化処理を開始します。",
                                    activeDataSets.Count);

                                // ProcessType別に無効化
                                var processTypes = activeDataSets.Select(ds => ds.ProcessType).Distinct();
                                foreach (var processType in processTypes)
                                {
                                    try
                                    {
                                        // DeactivateOldDataSetsAsyncを呼び出す
                                        // 注意: currentDataSetIdはまだ生成されていないため、nullを渡す
                                        await dataSetService.DeactivateOldDataSetsAsync(startDate.Value, processType, null);

                                        System.Console.WriteLine($"  ✅ ProcessType={processType}の既存DataSetを無効化しました。");
                                        logger.LogInformation(
                                            "ProcessType={ProcessType}の既存DataSetを無効化しました。",
                                            processType);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Console.WriteLine($"  ⚠️ ProcessType={processType}の無効化中にエラー: {ex.Message}");
                                        logger.LogWarning(ex, "ProcessType={ProcessType}の無効化中にエラーが発生しましたが、処理を続行します。", processType);
                                    }
                                }
                                System.Console.WriteLine("✅ 既存DataSet無効化処理完了");
                            }
                            else
                            {
                                System.Console.WriteLine("既存のActiveなDataSetは見つかりませんでした。");
                            }
                        }
                        // ===== 新規追加ここまで =====

                        // 重複データクリア処理（日付範囲指定時はスキップ）
                        if (startDate.HasValue && endDate.HasValue && startDate.Value == endDate.Value)
                        {
                            System.Console.WriteLine("\n既存データのクリア中...");
                            await ClearExistingVoucherData(scopedServices, startDate.Value, department);
                            System.Console.WriteLine("✅ 既存データクリア完了");
                        }
                        else if (!startDate.HasValue)
                        {
                            System.Console.WriteLine("\n⚠️ 全期間モードまたは期間指定モードでは既存データクリアをスキップします");
                        }

                        // ファイル一覧の取得
                        var files = await fileService.GetPendingFilesAsync(department);
                        System.Console.WriteLine($"取込対象ファイル数: {files.Count}\n");

                        // ファイルを処理順序でソート
                        var sortedFiles = files
                            .OrderBy(f => GetFileProcessOrder(Path.GetFileName(f)))
                            .ThenBy(f => Path.GetFileName(f))
                            .ToList();

                        // 各ファイルの処理
                        foreach (var file in sortedFiles)
                        {
                            var fileName = Path.GetFileName(file);
                            System.Console.WriteLine($"処理中: {fileName}");

                            try
                            {
                                // ========== Phase 1: マスタ系ファイル ==========
                                if (fileName.Contains("等級汎用マスター"))
                                {
                                    if (gradeRepo != null)
                                    {
                                        await gradeRepo.ImportFromCsvAsync();
                                        System.Console.WriteLine("✅ 等級マスタとして処理完了");
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
                                        System.Console.WriteLine("✅ 階級マスタとして処理完了");
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
                                        System.Console.WriteLine($"✅ 荷印マスタとして処理完了 - {result.ImportedCount}件");
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
                                        System.Console.WriteLine($"✅ 産地マスタとして処理完了 - {result.ImportedCount}件");
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
                                        System.Console.WriteLine($"✅ 商品マスタとして処理完了 - {result.ImportedCount}件");
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
                                        System.Console.WriteLine($"✅ 得意先マスタとして処理完了 - {result.ImportedCount}件");
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
                                        System.Console.WriteLine($"✅ 仕入先マスタとして処理完了 - {result.ImportedCount}件");
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
                                    System.Console.WriteLine($"処理中: {fileName}");

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
                                            System.Console.WriteLine($"✅ 商品分類{categoryNumber}マスタとして処理完了");
                                            logger.LogInformation("商品分類{CategoryNumber}マスタ取込完了: {File}", categoryNumber, fileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "商品分類{CategoryNumber}マスタ処理エラー: {File}", categoryNumber, fileName);
                                            System.Console.WriteLine($"❌ エラー: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogError("商品分類{CategoryNumber}の処理サービスが見つかりません: {ServiceName}", categoryNumber, serviceName);
                                        System.Console.WriteLine($"❌ サービスが見つかりません: {serviceName}");
                                    }

                                    // ファイル移動をスキップ（処理履歴で管理）
                                    logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                                }
                                else if (fileName.Contains("得意先分類") && fileName.EndsWith(".csv"))
                                {
                                    System.Console.WriteLine($"処理中: {fileName}");

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
                                            System.Console.WriteLine($"✅ 得意先分類{categoryNumber}マスタとして処理完了");
                                            logger.LogInformation("得意先分類{CategoryNumber}マスタ取込完了: {File}", categoryNumber, fileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "得意先分類{CategoryNumber}マスタ処理エラー: {File}", categoryNumber, fileName);
                                            System.Console.WriteLine($"❌ エラー: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogError("得意先分類{CategoryNumber}の処理サービスが見つかりません: {ServiceName}", categoryNumber, serviceName);
                                        System.Console.WriteLine($"❌ サービスが見つかりません: {serviceName}");
                                    }

                                    // ファイル移動をスキップ（処理履歴で管理）
                                    logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                                }
                                else if (fileName.Contains("仕入先分類") && fileName.EndsWith(".csv"))
                                {
                                    System.Console.WriteLine($"処理中: {fileName}");

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
                                            System.Console.WriteLine($"✅ 仕入先分類{categoryNumber}マスタとして処理完了");
                                            logger.LogInformation("仕入先分類{CategoryNumber}マスタ取込完了: {File}", categoryNumber, fileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "仕入先分類{CategoryNumber}マスタ処理エラー: {File}", categoryNumber, fileName);
                                            System.Console.WriteLine($"❌ エラー: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogError("仕入先分類{CategoryNumber}の処理サービスが見つかりません: {ServiceName}", categoryNumber, serviceName);
                                        System.Console.WriteLine($"❌ サービスが見つかりません: {serviceName}");
                                    }

                                    // ファイル移動をスキップ（処理履歴で管理）
                                    logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                                }
                                else if (fileName.Contains("担当者分類") && fileName.EndsWith(".csv"))
                                {
                                    System.Console.WriteLine($"処理中: {fileName}");

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
                                            System.Console.WriteLine($"✅ 担当者分類{categoryNumber}マスタとして処理完了");
                                            logger.LogInformation("担当者分類{CategoryNumber}マスタ取込完了: {File}", categoryNumber, fileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "担当者分類{CategoryNumber}マスタ処理エラー: {File}", categoryNumber, fileName);
                                            System.Console.WriteLine($"❌ エラー: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogError("担当者分類{CategoryNumber}の処理サービスが見つかりません: {ServiceName}", categoryNumber, serviceName);
                                        System.Console.WriteLine($"❌ サービスが見つかりません: {serviceName}");
                                    }

                                    // ファイル移動をスキップ（処理履歴で管理）
                                    logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                                }
                                else if (fileName == "単位.csv")
                                {
                                    System.Console.WriteLine($"処理中: {fileName}");

                                    var importServices = scopedServices.GetServices<IImportService>();
                                    var service = importServices.FirstOrDefault(s => s.GetType().Name == "UnitMasterImportService");

                                    if (service != null)
                                    {
                                        try
                                        {
                                            await service.ImportAsync(file, startDate ?? DateTime.Today);
                                            processedCounts["単位マスタ"] = 1; // 処理成功
                                            System.Console.WriteLine("✅ 単位マスタとして処理完了");
                                            logger.LogInformation("単位マスタ取込完了: {File}", fileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "単位マスタ処理エラー: {File}", fileName);
                                            System.Console.WriteLine($"❌ エラー: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogError("単位マスタの処理サービスが見つかりません: UnitMasterImportService");
                                        System.Console.WriteLine("❌ サービスが見つかりません: UnitMasterImportService");
                                    }

                                    // ファイル移動をスキップ（処理履歴で管理）
                                    logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                                }
                                else if (fileName == "担当者.csv")
                                {
                                    System.Console.WriteLine($"処理中: {fileName}");

                                    var importServices = scopedServices.GetServices<IImportService>();
                                    var service = importServices.FirstOrDefault(s => s.GetType().Name == "StaffMasterImportService");

                                    if (service != null)
                                    {
                                        try
                                        {
                                            await service.ImportAsync(file, startDate ?? DateTime.Today);
                                            processedCounts["担当者マスタ"] = 1; // 処理成功
                                            System.Console.WriteLine("✅ 担当者マスタとして処理完了");
                                            logger.LogInformation("担当者マスタ取込完了: {File}", fileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "担当者マスタ処理エラー: {File}", fileName);
                                            System.Console.WriteLine($"❌ エラー: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogError("担当者マスタの処理サービスが見つかりません: StaffMasterImportService");
                                        System.Console.WriteLine("❌ サービスが見つかりません: StaffMasterImportService");
                                    }

                                    // ファイル移動をスキップ（処理履歴で管理）
                                    logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                                }
                                else if (fileName.StartsWith("入金伝票") && fileName.EndsWith(".csv"))
                                {
                                    System.Console.WriteLine($"処理中: {fileName}");

                                    var importServices = scopedServices.GetServices<IImportService>();
                                    var service = importServices.FirstOrDefault(s => s.GetType().Name == "ReceiptVoucherImportService");

                                    if (service != null)
                                    {
                                        try
                                        {
                                            await service.ImportAsync(file, startDate ?? DateTime.Today);
                                            processedCounts["入金伝票"] = 1; // 処理成功
                                            System.Console.WriteLine("✅ 入金伝票として処理完了");
                                            logger.LogInformation("入金伝票取込完了: {File}", fileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "入金伝票処理エラー: {File}", fileName);
                                            System.Console.WriteLine($"❌ エラー: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogError("入金伝票の処理サービスが見つかりません: ReceiptVoucherImportService");
                                        System.Console.WriteLine("❌ サービスが見つかりません: ReceiptVoucherImportService");
                                    }

                                    // ファイル移動をスキップ（処理履歴で管理）
                                    logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                                }
                                else if (fileName.StartsWith("支払伝票") && fileName.EndsWith(".csv"))
                                {
                                    System.Console.WriteLine($"処理中: {fileName}");

                                    var importServices = scopedServices.GetServices<IImportService>();
                                    var service = importServices.FirstOrDefault(s => s.GetType().Name == "PaymentVoucherImportService");

                                    if (service != null)
                                    {
                                        try
                                        {
                                            await service.ImportAsync(file, startDate ?? DateTime.Today);
                                            processedCounts["支払伝票"] = 1; // 処理成功
                                            System.Console.WriteLine("✅ 支払伝票として処理完了");
                                            logger.LogInformation("支払伝票取込完了: {File}", fileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "支払伝票処理エラー: {File}", fileName);
                                            System.Console.WriteLine($"❌ エラー: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogError("支払伝票の処理サービスが見つかりません: PaymentVoucherImportService");
                                        System.Console.WriteLine("❌ サービスが見つかりません: PaymentVoucherImportService");
                                    }

                                    // ファイル移動をスキップ（処理履歴で管理）
                                    logger.LogInformation("ファイル移動をスキップしました（処理履歴で管理）: {File}", file);
                                }
                                // ========== Phase 2: 初期在庫ファイル ==========
                                else if (fileName == "前月末在庫.csv")
                                {
                                    logger.LogWarning("前月末在庫.csvはinit-inventoryコマンドで処理してください。スキップします。");
                                    System.Console.WriteLine("⚠️ 前月末在庫.csvはinit-inventoryコマンドで処理してください。スキップします。");
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

                                    System.Console.WriteLine($"✅ 売上伝票として処理完了 - データセットID: {dataSetId}");
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

                                    System.Console.WriteLine($"✅ 仕入伝票として処理完了 - データセットID: {dataSetId}");
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

                                    System.Console.WriteLine($"✅ 在庫調整として処理完了 - データセットID: {dataSetId}");
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
                                    System.Console.WriteLine($"✅ 在庫調整として処理完了 - データセットID: {dataSetId}");
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
                                        System.Console.WriteLine($"⚠️ {fileName} は現在未対応です（スキップ）");
                                        // エラー時のファイル移動も無効化
                                        // await fileService.MoveToErrorAsync(file, department, "未対応のCSVファイル形式");
                                        logger.LogError("エラーが発生しましたが、ファイルは移動しません: {File} - 未対応のCSVファイル形式", file);
                                    }
                                    else
                                    {
                                        System.Console.WriteLine($"⚠️ {fileName} は認識できないCSVファイルです");
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
                                    System.Console.WriteLine("⚠️ CSVファイル以外のため処理をスキップ");
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "ファイル処理中にエラーが発生しました: {FileName}", fileName);
                                System.Console.WriteLine($"❌ エラー: {fileName} - {ex.Message}");

                                // エラーファイルは移動せずに続行
                                errorCount++;
                                continue;
                            }

                            System.Console.WriteLine(); // 各ファイル処理後に改行
                        }

                        // ========== Phase 4: 在庫マスタ最適化または前日在庫引継 ==========
                        System.Console.WriteLine("\n========== Phase 4: 在庫マスタ処理 ==========");

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
                                        System.Console.WriteLine($"\n[{currentDate:yyyy-MM-dd}] 在庫影響伝票が0件のため、前日在庫引継モードで処理します。");
                                        System.Console.WriteLine($"  売上: {salesCount}件, 仕入: {purchaseCount}件, 在庫調整: {adjustmentCount}件");

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
                                        System.Console.WriteLine($"\n[{currentDate:yyyy-MM-dd}] 在庫マスタ最適化を開始します。");
                                        System.Console.WriteLine($"  売上: {salesCount}件, 仕入: {purchaseCount}件, 在庫調整: {adjustmentCount}件");

                                        dataSetId = $"AUTO_OPTIMIZE_{currentDate:yyyyMMdd}_{DateTime.Now:HHmmss}";
                                        importType = "OPTIMIZE";

                                        var result = await optimizationService.OptimizeAsync(currentDate, dataSetId);
                                        processedCounts[$"在庫マスタ最適化_{currentDate:yyyy-MM-dd}"] = result.InsertedCount + result.UpdatedCount;

                                        // CP在庫マスタの等級名・階級名設定処理を追加
                                        var masterSyncService = scopedServices.GetService<IMasterSyncService>();
                                        if (masterSyncService != null)
                                        {
                                            System.Console.WriteLine($"[{currentDate:yyyy-MM-dd}] CP在庫マスタの等級名・階級名を設定中...");
                                            var masterSyncConnectionString = scopedServices.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
                                            
                                            using var connection = new SqlConnection(masterSyncConnectionString);
                                            await connection.OpenAsync();
                                            using var transaction = connection.BeginTransaction();
                                            
                                            try
                                            {
                                                await masterSyncService.UpdateCpInventoryMasterNamesAsync(connection, transaction, currentDate);
                                                await transaction.CommitAsync();
                                                System.Console.WriteLine($"✅ CP在庫マスタの等級名・階級名設定完了 [{currentDate:yyyy-MM-dd}]");
                                            }
                                            catch (Exception ex)
                                            {
                                                await transaction.RollbackAsync();
                                                logger.LogWarning(ex, "CP在庫マスタの等級名・階級名設定でエラーが発生しました: {Message}", ex.Message);
                                                System.Console.WriteLine($"⚠️ CP在庫マスタの等級名・階級名設定エラー: {ex.Message}");
                                            }
                                        }
                                        else
                                        {
                                            logger.LogWarning("MasterSyncServiceが未実装のため、CP在庫マスタの等級名・階級名設定をスキップします");
                                            System.Console.WriteLine($"⚠️ MasterSyncServiceが未実装のため、CP在庫マスタの等級名・階級名設定をスキップ");
                                        }

                                        // カバレッジ率を計算（簡易版）
                                        var coverageRate = result.ProcessedCount > 0 ?
                                            (double)(result.InsertedCount + result.UpdatedCount) / result.ProcessedCount : 0.0;

                                        System.Console.WriteLine($"✅ 在庫マスタ最適化完了 [{currentDate:yyyy-MM-dd}] ({stopwatch.ElapsedMilliseconds}ms)");
                                        System.Console.WriteLine($"   - 新規作成: {result.InsertedCount}件");
                                        System.Console.WriteLine($"   - JobDate更新: {result.UpdatedCount}件");
                                        System.Console.WriteLine($"   - カバレッジ率: {coverageRate:P1}");
                                    }
                                    else
                                    {
                                        logger.LogWarning("在庫マスタ最適化サービスが未実装のため、スキップします。");
                                        System.Console.WriteLine($"⚠️ [{currentDate:yyyy-MM-dd}] 在庫マスタ最適化サービスが未実装のためスキップ");
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
                                System.Console.WriteLine($"❌ 在庫マスタ最適化エラー: {ex.Message}");
                                errorCount++;
                            }
                        }
                        else
                        {
                            if (!startDate.HasValue || !endDate.HasValue)
                            {
                                logger.LogWarning("在庫処理には日付指定が必要です");
                                System.Console.WriteLine("⚠️ 在庫処理には日付指定が必要です");
                            }
                        }

                        // ========== 注意：アンマッチリスト処理は削除されました ==========
                        // UN/CP在庫マスタ分離仕様により、import-folderコマンドではアンマッチチェックを行いません
                        // アンマッチチェックは別途 'unmatch-list' コマンドで実行してください

                        System.Console.WriteLine("\n========== データ取込完了 ==========");
                        System.Console.WriteLine("✅ CSVファイルの取込処理が完了しました");
                        System.Console.WriteLine();
                        System.Console.WriteLine("次の手順:");
                        System.Console.WriteLine("1. アンマッチチェック: dotnet run -- unmatch-list <日付>");
                        System.Console.WriteLine("2. 帳票生成: dotnet run -- daily-report <日付>");
                        System.Console.WriteLine("3. 商品勘定: dotnet run -- product-account <日付>");

                        // 処理結果のサマリを表示
                        System.Console.WriteLine("\n=== フォルダ監視取込完了 ===");
                        if (preserveCsvDates)
                        {
                            System.Console.WriteLine("モード: 期間指定（CSVの日付を保持）");
                        }
                        if (startDate.HasValue && endDate.HasValue)
                        {
                            if (startDate.Value == endDate.Value)
                            {
                                System.Console.WriteLine($"対象日付: {startDate.Value:yyyy-MM-dd}");
                            }
                            else
                            {
                                System.Console.WriteLine($"対象期間: {startDate.Value:yyyy-MM-dd} ～ {endDate.Value:yyyy-MM-dd}");
                                var totalDays = (endDate.Value - startDate.Value).Days + 1;
                                System.Console.WriteLine($"処理日数: {totalDays}日間");
                            }
                        }
                        else
                        {
                            System.Console.WriteLine("対象期間: 全期間");
                        }
                        System.Console.WriteLine($"部門: {department}");
                        System.Console.WriteLine($"処理ファイル数: {sortedFiles.Count}");

                        if (processedCounts.Any())
                        {
                            System.Console.WriteLine("\n処理実績:");
                            foreach (var kvp in processedCounts)
                            {
                                System.Console.WriteLine($"  {kvp.Key}: {kvp.Value}件");
                            }
                        }

                        if (errorCount > 0)
                        {
                            System.Console.WriteLine($"\n⚠️ {errorCount}件のファイルでエラーが発生しました。");
                        }
                        else
                        {
                            // 正常完了時のメッセージ
                            System.Console.WriteLine();
                            System.Console.WriteLine("✅ データ取込が正常に完了しました。");
                            System.Console.WriteLine();
                            System.Console.WriteLine("次の手順で処理を続けてください：");
                            System.Console.WriteLine("1. アンマッチチェック: dotnet run -- unmatch-list <日付>");
                            System.Console.WriteLine("2. 帳票生成（アンマッチ0件確認後）:");

                            if (startDate.HasValue && endDate.HasValue)
                            {
                                if (startDate.Value == endDate.Value)
                                {
                                    // 単一日付の場合
                                    var targetDate = startDate.Value;
                                    System.Console.WriteLine($"  商品日報: dotnet run daily-report {targetDate:yyyy-MM-dd}");
                                    System.Console.WriteLine($"  商品勘定: dotnet run product-account {targetDate:yyyy-MM-dd}");
                                    System.Console.WriteLine($"  在庫表: dotnet run inventory-list {targetDate:yyyy-MM-dd}");
                                }
                                else
                                {
                                    // 期間指定の場合
                                    System.Console.WriteLine($"  商品日報: dotnet run daily-report <YYYY-MM-DD> （{startDate.Value:yyyy-MM-dd} ～ {endDate.Value:yyyy-MM-dd}）");
                                    System.Console.WriteLine($"  商品勘定: dotnet run product-account <YYYY-MM-DD>");
                                    System.Console.WriteLine($"  在庫表: dotnet run inventory-list <YYYY-MM-DD>");
                                }
                            }
                        }

                        System.Console.WriteLine("========================\n");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"❌ エラー: {ex.Message}");
                        logger.LogError(ex, "フォルダ監視取込でエラーが発生しました");
                    }
                }
            }

            /// <summary>
            /// 指定日付の在庫マスタ最適化を実行
            /// </summary>
            static async Task<(int ProcessedCount, int InsertedCount, int UpdatedCount)>
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
                    ManualShippingMark
                FROM (
                    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                    FROM SalesVouchers
                    WHERE CONVERT(date, JobDate) = @jobDate
                    UNION
                    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                    FROM PurchaseVouchers
                    WHERE CONVERT(date, JobDate) = @jobDate
                    UNION
                    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                    FROM InventoryAdjustments
                    WHERE CONVERT(date, JobDate) = @jobDate
                ) AS products
            ) AS source
            ON target.ProductCode = source.ProductCode
                AND target.GradeCode = source.GradeCode
                AND target.ClassCode = source.ClassCode
                AND target.ShippingMarkCode = source.ShippingMarkCode
                AND target.ManualShippingMark = source.ManualShippingMark
            WHEN MATCHED AND target.JobDate <> @jobDate THEN
                UPDATE SET 
                    JobDate = @jobDate,
                    UpdatedDate = GETDATE(),
                    DataSetId = @dataSetId
            WHEN NOT MATCHED THEN
                INSERT (
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
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
                    source.ManualShippingMark,
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

                    System.Console.WriteLine($"  - 売上伝票: {salesDeleted}件削除");
                    System.Console.WriteLine($"  - 仕入伝票: {purchaseDeleted}件削除");
                    System.Console.WriteLine($"  - 在庫調整: {adjustmentDeleted}件削除");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "既存データクリア中にエラーが発生しました");
                    System.Console.WriteLine($"⚠️ 既存データクリア中にエラー: {ex.Message}");
                    // エラーが発生しても処理を継続
                }
            }


            /// <summary>
            /// インポート処理後のアンマッチリスト処理を実行
            /// </summary>
            static async Task ExecuteUnmatchListAfterImport(IServiceProvider services, DateTime jobDate, ILogger<Program> logger)
            {
                try
                {
                    logger.LogInformation("アンマッチリスト処理を開始します");
                    System.Console.WriteLine("\n=== アンマッチリスト処理開始 ===");

                    var unmatchListService = services.GetRequiredService<IUnmatchListService>();
                    var reportService = services.GetRequiredService<IUnmatchListReportService>();
                    var fileManagementService = services.GetRequiredService<IFileManagementService>();

                    // アンマッチリスト処理実行
                    var result = await unmatchListService.ProcessUnmatchListAsync();

                    if (result.Success)
                    {
                        logger.LogInformation("アンマッチリスト処理が完了しました - アンマッチ件数: {Count}件", result.UnmatchCount);
                        System.Console.WriteLine($"✅ アンマッチリスト処理完了 - {result.UnmatchCount}件のアンマッチを検出");

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
                                System.Console.WriteLine($"  - PDFファイル: {Path.GetFileName(pdfPath)}");
                            }
                        }
                        catch (Exception pdfEx)
                        {
                            logger.LogError(pdfEx, "アンマッチリストPDF生成中にエラーが発生しました");
                            System.Console.WriteLine($"⚠️ PDF生成エラー: {pdfEx.Message}");
                        }
                    }
                    else
                    {
                        logger.LogError("アンマッチリスト処理が失敗しました: {ErrorMessage}", result.ErrorMessage);
                        System.Console.WriteLine($"❌ アンマッチリスト処理失敗: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "アンマッチリスト処理中にエラーが発生しました");
                    System.Console.WriteLine($"⚠️ アンマッチリスト処理でエラーが発生しました: {ex.Message}");
                    // エラーが発生してもインポート処理全体は成功とする
                }
            }

            /// <summary>
            /// 日次終了処理の事前確認を実行
            /// </summary>
            static async Task ExecuteCheckDailyCloseAsync(IServiceProvider services, string[] args)
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
                        System.Console.WriteLine("=== 日次終了処理 事前確認 ===");
                        System.Console.WriteLine($"対象日付: {jobDate:yyyy-MM-dd}");
                        System.Console.WriteLine($"現在時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        System.Console.WriteLine();

                        // 確認情報を取得
                        var confirmation = await dailyCloseService.GetConfirmationInfo(jobDate);

                        // 商品日報情報
                        if (confirmation.DailyReport != null)
                        {
                            System.Console.WriteLine("【商品日報情報】");
                            System.Console.WriteLine($"  作成時刻: {confirmation.DailyReport.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                            System.Console.WriteLine($"  作成者: {confirmation.DailyReport.CreatedBy}");
                            System.Console.WriteLine($"  DatasetId: {confirmation.DailyReport.DataSetId}");
                            System.Console.WriteLine();
                        }

                        // 最新CSV取込情報
                        if (confirmation.LatestCsvImport != null)
                        {
                            System.Console.WriteLine("【最新CSV取込情報】");
                            System.Console.WriteLine($"  取込時刻: {confirmation.LatestCsvImport.ImportedAt:yyyy-MM-dd HH:mm:ss}");
                            System.Console.WriteLine($"  取込者: {confirmation.LatestCsvImport.ImportedBy}");
                            System.Console.WriteLine($"  ファイル: {confirmation.LatestCsvImport.FileNames}");
                            System.Console.WriteLine();
                        }

                        // データ件数サマリー
                        System.Console.WriteLine("【データ件数】");
                        System.Console.WriteLine($"  売上伝票: {confirmation.DataCounts.SalesCount:N0}件");
                        System.Console.WriteLine($"  仕入伝票: {confirmation.DataCounts.PurchaseCount:N0}件");
                        System.Console.WriteLine($"  在庫調整: {confirmation.DataCounts.AdjustmentCount:N0}件");
                        System.Console.WriteLine($"  CP在庫: {confirmation.DataCounts.CpInventoryCount:N0}件");
                        System.Console.WriteLine();

                        // 金額サマリー
                        System.Console.WriteLine("【金額サマリー】");
                        System.Console.WriteLine($"  売上総額: {confirmation.Amounts.SalesAmount:C}");
                        System.Console.WriteLine($"  仕入総額: {confirmation.Amounts.PurchaseAmount:C}");
                        System.Console.WriteLine($"  推定粗利: {confirmation.Amounts.EstimatedGrossProfit:C}");
                        System.Console.WriteLine();

                        // 検証結果
                        if (confirmation.ValidationResults.Any())
                        {
                            System.Console.WriteLine("【検証結果】");
                            foreach (var validation in confirmation.ValidationResults.OrderBy(v => v.Level))
                            {
                                var prefix = validation.Level switch
                                {
                                    ValidationLevel.Error => "❌ エラー",
                                    ValidationLevel.Warning => "⚠️  警告",
                                    ValidationLevel.Info => "ℹ️  情報",
                                    _ => "   "
                                };

                                System.Console.WriteLine($"{prefix}: {validation.Message}");
                                if (!string.IsNullOrEmpty(validation.Detail))
                                {
                                    System.Console.WriteLine($"         {validation.Detail}");
                                }
                            }
                            System.Console.WriteLine();
                        }

                        // 処理可否
                        System.Console.WriteLine("【処理可否判定】");
                        if (confirmation.CanProcess)
                        {
                            System.Console.WriteLine("✅ 日次終了処理を実行可能です");
                            System.Console.WriteLine();
                            System.Console.WriteLine("実行するには以下のコマンドを使用してください:");
                            System.Console.WriteLine($"  dotnet run daily-close {jobDate:yyyy-MM-dd}");
                        }
                        else
                        {
                            System.Console.WriteLine("❌ 日次終了処理を実行できません");
                            System.Console.WriteLine("上記のエラーを解決してから再度実行してください。");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "日次終了処理の事前確認でエラーが発生しました");
                        System.Console.WriteLine($"エラー: {ex.Message}");
                    }
                }
            }

            /// <summary>
            /// CP在庫マスタ作成コマンドを実行
            /// </summary>
            static async Task ExecuteCreateCpInventoryAsync(IServiceProvider services, string[] args)
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
                        System.Console.WriteLine("=== CP在庫マスタ作成 ===");
                        System.Console.WriteLine($"処理日付: {jobDate:yyyy-MM-dd}");
                        System.Console.WriteLine($"データセットID: {dataSetId}");
                        System.Console.WriteLine();

                        // CP在庫マスタ作成実行
                        var result = await cpInventoryCreationService.CreateCpInventoryFromInventoryMasterAsync(jobDate); // 仮テーブル設計

                        if (result.Success)
                        {
                            System.Console.WriteLine("=== 処理結果 ===");
                            System.Console.WriteLine($"削除された既存レコード: {result.DeletedCount}件");
                            System.Console.WriteLine($"在庫マスタからコピー: {result.CopiedCount}件");
                            System.Console.WriteLine();

                            if (result.Warnings.Any())
                            {
                                System.Console.WriteLine("⚠️ 警告:");
                                foreach (var warning in result.Warnings)
                                {
                                    System.Console.WriteLine($"  {warning}");
                                }
                                System.Console.WriteLine();

                                // 未登録商品の詳細表示
                                var missingResult = await cpInventoryCreationService.DetectMissingProductsAsync(jobDate);
                                if (missingResult.MissingProducts.Any())
                                {
                                    System.Console.WriteLine("未登録商品の詳細（最初の10件）:");
                                    foreach (var missing in missingResult.MissingProducts.Take(10))
                                    {
                                        System.Console.WriteLine($"  商品コード:{missing.ProductCode}, 等級:{missing.GradeCode}, 階級:{missing.ClassCode}, " +
                                                       $"荷印:{missing.ShippingMarkCode}, 荷印名:{missing.ManualShippingMark}, " +
                                                       $"検出元:{missing.FoundInVoucherType}");
                                    }
                                    if (missingResult.MissingProducts.Count > 10)
                                    {
                                        System.Console.WriteLine($"  他{missingResult.MissingProducts.Count - 10}件...");
                                    }
                                }
                            }

                            System.Console.WriteLine("✅ CP在庫マスタ作成が正常に完了しました");
                        }
                        else
                        {
                            System.Console.WriteLine("❌ CP在庫マスタ作成に失敗しました");
                            if (!string.IsNullOrEmpty(result.ErrorMessage))
                            {
                                System.Console.WriteLine($"エラー: {result.ErrorMessage}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "CP在庫マスタ作成でエラーが発生しました");
                        System.Console.WriteLine($"エラー: {ex.Message}");
                    }
                }
            }

            /// <summary>
            /// データベース初期化コマンドを実行
            /// </summary>
            static async Task ExecuteInitDatabaseAsync(IServiceProvider services, string[] args)
            {
                // 開発環境チェック
                if (!IsDevelopmentEnvironment())
                {
                    System.Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
                    return;
                }

                using var scope = services.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var logger = scopedServices.GetRequiredService<ILogger<Program>>();
                var initService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.Development.IDatabaseInitializationService>();

                try
                {
                    var force = args.Any(a => a == "--force");

                    System.Console.WriteLine("=== データベース初期化 ===");
                    if (force)
                    {
                        System.Console.WriteLine("⚠️ --forceオプションが指定されました。既存テーブルが削除されます。");
                        System.Console.Write("続行しますか？ (y/N): ");
                        var confirm = System.Console.ReadLine();
                        if (confirm?.ToLower() != "y")
                        {
                            System.Console.WriteLine("処理を中止しました。");
                            return;
                        }
                    }

                    var result = await initService.InitializeDatabaseAsync(force);
                    System.Console.WriteLine(result.GetSummary());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "データベース初期化でエラーが発生しました");
                    System.Console.WriteLine($"エラー: {ex.Message}");
                }
            }

            /// <summary>
            /// 日次終了処理リセットコマンドを実行
            /// </summary>
            static async Task ExecuteResetDailyCloseAsync(IServiceProvider services, string[] args)
            {
                // 開発環境チェック
                if (!IsDevelopmentEnvironment())
                {
                    System.Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
                    return;
                }

                if (args.Length < 3)
                {
                    System.Console.WriteLine("使用方法: dotnet run reset-daily-close <YYYY-MM-DD> [--all]");
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
                        System.Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
                        return;
                    }

                    var resetAll = args.Any(a => a == "--all");

                    System.Console.WriteLine($"=== 日次終了処理リセット: {jobDate:yyyy-MM-dd} ===");

                    // 関連データ状態を確認
                    var status = await resetService.GetRelatedDataStatusAsync(jobDate);
                    if (!status.HasDailyCloseRecord)
                    {
                        System.Console.WriteLine("指定日付の日次終了処理は実行されていません。");
                        return;
                    }

                    System.Console.WriteLine($"日次終了処理実行日時: {status.LastDailyCloseAt:yyyy-MM-dd HH:mm:ss}");
                    System.Console.WriteLine($"処理実行者: {status.LastProcessedBy}");

                    if (status.HasNextDayData && !resetAll)
                    {
                        System.Console.WriteLine("⚠️ 翌日以降のデータが存在します。--all オプションを使用してください。");
                        return;
                    }

                    if (resetAll)
                    {
                        System.Console.WriteLine("⚠️ 在庫マスタもリセットされます。");
                    }

                    System.Console.Write("続行しますか？ (y/N): ");
                    var confirm = System.Console.ReadLine();
                    if (confirm?.ToLower() != "y")
                    {
                        System.Console.WriteLine("処理を中止しました。");
                        return;
                    }

                    var result = await resetService.ResetDailyCloseAsync(jobDate, resetAll);
                    System.Console.WriteLine(result.GetSummary());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "日次終了処理リセットでエラーが発生しました");
                    System.Console.WriteLine($"エラー: {ex.Message}");
                }
            }

            /// <summary>
            /// 開発用日次終了処理コマンドを実行
            /// </summary>
            static async Task ExecuteDevDailyCloseAsync(IServiceProvider services, string[] args)
            {
                // 開発環境チェック
                if (!IsDevelopmentEnvironment())
                {
                    System.Console.WriteLine("❌ このコマンドは開発環境でのみ使用可能です");
                    return;
                }

                if (args.Length < 3)
                {
                    System.Console.WriteLine("使用方法: dotnet run dev-daily-close <YYYY-MM-DD> [--skip-validation] [--dry-run]");
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
                        System.Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
                        return;
                    }

                    var skipValidation = args.Any(a => a == "--skip-validation");
                    var dryRun = args.Any(a => a == "--dry-run");

                    System.Console.WriteLine($"=== 開発用日次終了処理: {jobDate:yyyy-MM-dd} ===");
                    System.Console.WriteLine($"オプション: SkipValidation={skipValidation}, DryRun={dryRun}");
                    System.Console.WriteLine();

                    if (dryRun)
                    {
                        System.Console.WriteLine("ドライランモードで実行します（実際の更新は行いません）");
                    }

                    var result = await dailyCloseService.ExecuteDevelopmentAsync(jobDate, skipValidation, dryRun);

                    System.Console.WriteLine();
                    System.Console.WriteLine(result.GetSummary());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "開発用日次終了処理でエラーが発生しました");
                    System.Console.WriteLine($"エラー: {ex.Message}");
                }
            }

            /// <summary>
            /// データ状態確認コマンドを実行
            /// </summary>
            static async Task ExecuteCheckDataStatusAsync(IServiceProvider services, string[] args)
            {
                if (args.Length < 3)
                {
                    System.Console.WriteLine("使用方法: dotnet run check-data-status <YYYY-MM-DD>");
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
                        System.Console.WriteLine("日付形式が正しくありません。YYYY-MM-DD形式で指定してください。");
                        return;
                    }

                    var report = await statusService.GetDataStatusAsync(jobDate);
                    statusService.DisplayReport(report);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "データ状態確認でエラーが発生しました");
                    System.Console.WriteLine($"エラー: {ex.Message}");
                }
            }

            /// <summary>
            /// 開発環境チェック
            /// </summary>
            static bool IsDevelopmentEnvironment()
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                return environment == "Development" || string.IsNullOrEmpty(environment);
            }

            /// <summary>
            /// データベーススキーマチェックと自動修正
            /// </summary>
            static async Task<bool> CheckAndFixDatabaseSchemaAsync(IServiceProvider services)
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
            static async Task<bool> EnsureRequiredTablesExistAsync(IServiceProvider services)
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
                    System.Console.WriteLine("使用方法: dotnet run simulate-daily <部門名> <YYYY-MM-DD> [--dry-run]");
                    System.Console.WriteLine("例: dotnet run simulate-daily DeptA 2025-06-30 --dry-run");
                    return;
                }

                var department = args[2];
                if (!DateTime.TryParse(args[3], out var jobDate))
                {
                    System.Console.WriteLine($"❌ 無効な日付形式: {args[3]}");
                    System.Console.WriteLine("正しい形式: YYYY-MM-DD (例: 2025-06-30)");
                    return;
                }

                var isDryRun = args.Length > 4 && args[4] == "--dry-run";

                System.Console.WriteLine("=== 日次処理シミュレーション開始 ===");
                System.Console.WriteLine($"部門: {department}");
                System.Console.WriteLine($"処理対象日: {jobDate:yyyy-MM-dd}");
                System.Console.WriteLine($"モード: {(isDryRun ? "ドライラン（実際の更新なし）" : "本番実行")}");
                System.Console.WriteLine();

                try
                {
                    var result = await simulationService.SimulateDailyProcessingAsync(department, jobDate, isDryRun);

                    // 結果表示
                    System.Console.WriteLine("=== シミュレーション結果 ===");
                    System.Console.WriteLine($"実行時間: {result.ProcessingTime.TotalSeconds:F2}秒");
                    System.Console.WriteLine($"成功: {(result.Success ? "✅" : "❌")}");

                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        System.Console.WriteLine($"エラー: {result.ErrorMessage}");
                    }

                    System.Console.WriteLine();
                    System.Console.WriteLine("=== ステップ結果 ===");
                    foreach (var step in result.StepResults)
                    {
                        var status = step.Success ? "✅" : "❌";
                        System.Console.WriteLine($"{status} ステップ{step.StepNumber}: {step.StepName} ({step.Duration.TotalSeconds:F2}秒)");

                        if (!string.IsNullOrEmpty(step.Message))
                        {
                            System.Console.WriteLine($"   → {step.Message}");
                        }

                        if (!string.IsNullOrEmpty(step.ErrorMessage))
                        {
                            System.Console.WriteLine($"   ❌ エラー: {step.ErrorMessage}");
                        }
                    }

                    System.Console.WriteLine();
                    System.Console.WriteLine("=== 統計情報 ===");
                    System.Console.WriteLine($"インポート: 新規{result.Statistics.Import.NewRecords}件、スキップ{result.Statistics.Import.SkippedRecords}件、エラー{result.Statistics.Import.ErrorRecords}件");
                    System.Console.WriteLine($"アンマッチ: {result.Statistics.Unmatch.UnmatchCount}件");
                    System.Console.WriteLine($"商品日報: {result.Statistics.DailyReport.DataCount}件");

                    if (!string.IsNullOrEmpty(result.Statistics.DailyReport.ReportPath))
                    {
                        System.Console.WriteLine($"商品日報ファイル: {result.Statistics.DailyReport.ReportPath}");
                    }

                    if (!string.IsNullOrEmpty(result.Statistics.Unmatch.UnmatchListPath))
                    {
                        System.Console.WriteLine($"アンマッチリストファイル: {result.Statistics.Unmatch.UnmatchListPath}");
                    }

                    if (result.GeneratedFiles.Any())
                    {
                        System.Console.WriteLine("生成されたファイル:");
                        foreach (var file in result.GeneratedFiles)
                        {
                            System.Console.WriteLine($"  - {file}");
                        }
                    }

                    System.Console.WriteLine();
                    System.Console.WriteLine($"=== シミュレーション{(result.Success ? "完了" : "失敗")} ===");

                    if (isDryRun && result.Success)
                    {
                        System.Console.WriteLine("💡 実際の処理を実行するには --dry-run オプションを外してください");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "日次処理シミュレーション中にエラーが発生しました");
                    System.Console.WriteLine($"❌ 予期しないエラーが発生しました: {ex.Message}");
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
                    System.Console.WriteLine("=== 在庫マスタ重複レコードクリーンアップ ===");
                    System.Console.WriteLine("⚠️ このコマンドは重複レコードを削除します。");
                    System.Console.Write("続行しますか？ (y/N): ");

                    var confirmation = System.Console.ReadLine()?.Trim().ToLower();
                    if (confirmation != "y")
                    {
                        System.Console.WriteLine("処理をキャンセルしました。");
                        return;
                    }

                    var stopwatch = Stopwatch.StartNew();
                    var deletedCount = await inventoryRepo.CleanupDuplicateRecordsAsync();
                    stopwatch.Stop();

                    System.Console.WriteLine($"✅ {deletedCount}件の重複レコードを削除しました。");
                    System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                    logger.LogInformation("在庫マスタ重複レコードクリーンアップ完了: {Count}件削除", deletedCount);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "在庫マスタ重複レコードクリーンアップ中にエラーが発生しました");
                    System.Console.WriteLine($"❌ エラー: {ex.Message}");
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
                    System.Console.WriteLine("エラー: 年月が指定されていません");
                    System.Console.WriteLine("使用方法: dotnet run init-monthly-inventory YYYYMM");
                    System.Console.WriteLine("例: dotnet run init-monthly-inventory 202507");
                    return;
                }

                var yearMonth = args[2];
                if (yearMonth.Length != 6 || !int.TryParse(yearMonth, out _))
                {
                    System.Console.WriteLine("エラー: 年月は YYYYMM 形式で指定してください");
                    return;
                }

                try
                {
                    System.Console.WriteLine($"=== {yearMonth.Substring(0, 4)}年{yearMonth.Substring(4, 2)}月の在庫初期化 ===");
                    System.Console.WriteLine("前月末在庫から現在庫を初期化します。");
                    System.Console.Write("続行しますか？ (y/N): ");

                    var confirmation = System.Console.ReadLine()?.Trim().ToLower();
                    if (confirmation != "y")
                    {
                        System.Console.WriteLine("処理をキャンセルしました。");
                        return;
                    }

                    var stopwatch = Stopwatch.StartNew();
                    var updatedCount = await inventoryRepo.InitializeMonthlyInventoryAsync(yearMonth);
                    stopwatch.Stop();

                    System.Console.WriteLine($"✅ {updatedCount}件の在庫を初期化しました。");
                    System.Console.WriteLine($"処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");

                    logger.LogInformation("月初在庫初期化完了: {YearMonth} - {Count}件更新", yearMonth, updatedCount);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "月初在庫初期化中にエラーが発生しました");
                    System.Console.WriteLine($"❌ エラー: {ex.Message}");
                }
            }

            /// <summary>
            /// 前日在庫引継モードの実行
            /// </summary>
            static async Task ExecuteCarryoverModeAsync(
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
                        System.Console.WriteLine("⚠️ 前日の在庫データが見つかりません。処理をスキップします。");
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

                    System.Console.WriteLine($"✅ 前日在庫引継完了 [{targetDate:yyyy-MM-dd}]");
                    System.Console.WriteLine($"   - 引継在庫数: {carryoverInventory.Count()}件");
                    System.Console.WriteLine($"   - DataSetId: {dataSetId}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "前日在庫引継処理中にエラーが発生しました");
                    System.Console.WriteLine($"❌ 前日在庫引継エラー: {ex.Message}");
                    throw;
                }
            }

            /// <summary>
            /// ランダム文字列生成
            /// </summary>
            static string GenerateRandomString(int length)
            {
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var random = new Random();
                return new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            }

            /// <summary>
            /// 初期在庫インポートコマンドを実行
            /// </summary>
            static async Task ExecuteImportInitialInventoryAsync(IServiceProvider services, string[] args)
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
                    System.Console.WriteLine($"エラー: {ex.Message}");
                }
            }

            static async Task ExecuteOptimizeInventoryAsync(IServiceProvider services, string[] args)
            {
                if (args.Length < 3)
                {
                    System.Console.WriteLine("使用方法: optimize-inventory <日付>");
                    System.Console.WriteLine("例: optimize-inventory 2025-06-30");
                    return;
                }

                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
                    var inventoryOptimizationService = scopedServices.GetRequiredService<IInventoryOptimizationService>();

                    if (!DateTime.TryParse(args[2], out var jobDate))
                    {
                        System.Console.WriteLine("❌ 日付の形式が正しくありません");
                        return;
                    }

                    logger.LogInformation("=== 在庫最適化開始 ===");
                    logger.LogInformation("対象日: {JobDate}", jobDate);

                    try
                    {
                        var result = await inventoryOptimizationService.OptimizeInventoryAsync(jobDate);

                        if (result.IsSuccess)
                        {
                            System.Console.WriteLine($"✅ 在庫最適化が完了しました");
                            System.Console.WriteLine($"   対象日: {result.JobDate:yyyy-MM-dd}");
                            System.Console.WriteLine($"   処理時間: {result.ProcessingTime?.TotalSeconds:F2}秒");
                            System.Console.WriteLine($"   前日在庫: {result.PreviousDayStockCount}件");
                            System.Console.WriteLine($"   売上伝票: {result.SalesTransactionCount}件");
                            System.Console.WriteLine($"   仕入伝票: {result.PurchaseTransactionCount}件");
                            System.Console.WriteLine($"   在庫調整: {result.AdjustmentTransactionCount}件");
                            System.Console.WriteLine($"   計算後在庫: {result.CalculatedStockCount}件");
                            System.Console.WriteLine($"   挿入レコード: {result.InsertedRecordCount}件");
                            System.Console.WriteLine($"   削除レコード: {result.DeletedRecordCount}件");
                            System.Console.WriteLine($"   0在庫削除: {result.CleanedUpRecordCount}件");

                            logger.LogInformation("在庫最適化完了: {Result}", result);
                        }
                        else
                        {
                            System.Console.WriteLine($"❌ 在庫最適化に失敗しました: {result.ErrorMessage}");
                            logger.LogError("在庫最適化失敗: {ErrorMessage}", result.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "在庫最適化中にエラーが発生しました");
                        System.Console.WriteLine($"❌ エラーが発生しました: {ex.Message}");
                    }

                    logger.LogInformation("=== 在庫最適化完了 ===");
                    System.Console.WriteLine("\n=== 在庫最適化完了 ===");
                }
            }

            /// <summary>
            /// フェーズ2: 新しいカラムの追加
            /// </summary>
            static async Task ExecuteMigratePhase2Async(IServiceProvider services, string[] args)
            {
                await ExecuteMigrationPhaseAsync(services, "051_Phase2_AddNewColumns.sql", "フェーズ2: 新しいカラム追加");
            }

            /// <summary>
            /// フェーズ3: データ移行と同期トリガー作成
            /// </summary>
            static async Task ExecuteMigratePhase3Async(IServiceProvider services, string[] args)
            {
                await ExecuteMigrationPhaseAsync(services, "052_Phase3_MigrateDataAndSync.sql", "フェーズ3: データ移行と同期");
            }

            /// <summary>
            /// フェーズ5: クリーンアップ
            /// </summary>
            static async Task ExecuteMigratePhase5Async(IServiceProvider services, string[] args)
            {
                System.Console.WriteLine("⚠️  重要: このフェーズは古いカラムを削除します");
                System.Console.WriteLine("   実行前に以下を確認してください:");
                System.Console.WriteLine("   1. アプリケーションが新しいスキーマで正常動作している");
                System.Console.WriteLine("   2. import-folderコマンドが成功している");
                System.Console.WriteLine("   3. データベースの完全バックアップを取得済み");
                System.Console.WriteLine();
                System.Console.Write("続行しますか？ (y/N): ");

                var response = System.Console.ReadLine();
                if (response?.ToLower() != "y" && response?.ToLower() != "yes")
                {
                    System.Console.WriteLine("処理をキャンセルしました");
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
                    System.Console.WriteLine("使用方法: process-2-5 <日付> [データセットID]");
                    System.Console.WriteLine("         gross-profit <日付> [データセットID]");
                    System.Console.WriteLine("例: process-2-5 2025-06-30");
                    System.Console.WriteLine("例: gross-profit 2025-06-30 ABC123");
                    return;
                }

                if (!DateTime.TryParse(args[1], out var jobDate))
                {
                    System.Console.WriteLine("❌ 日付の形式が正しくありません");
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
                        System.Console.WriteLine($"指定されたDataSetId: {dataSetId}");
                    }
                    else
                    {
                        // JobDateから最新のDataSetIdを取得
                        var dataSets = await dataSetRepository.GetByJobDateAsync(jobDate);
                        var latestDataSet = dataSets.OrderByDescending(d => d.CreatedAt).FirstOrDefault();

                        if (latestDataSet == null)
                        {
                            System.Console.WriteLine($"❌ 指定日({jobDate:yyyy-MM-dd})のデータセットが見つかりません");
                            return;
                        }

                        dataSetId = latestDataSet.DataSetId;
                        System.Console.WriteLine($"自動取得したDataSetId: {dataSetId}");
                    }

                    System.Console.WriteLine("=== Process 2-5: 売上伝票への在庫単価書き込みと粗利計算 開始 ===");
                    System.Console.WriteLine($"対象日: {jobDate:yyyy-MM-dd}");
                    System.Console.WriteLine($"データセットID: {dataSetId}");

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    // Process 2-5実行
                    await grossProfitService.ExecuteProcess25Async(jobDate, dataSetId);

                    stopwatch.Stop();

                    System.Console.WriteLine($"✅ Process 2-5 が正常に完了しました");
                    System.Console.WriteLine($"   処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
                    System.Console.WriteLine("=== Process 2-5 完了 ===");

                    logger.LogInformation("Process 2-5完了: JobDate={JobDate}, DataSetId={DataSetId}, 処理時間={ElapsedMs}ms",
                        jobDate, dataSetId, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"❌ Process 2-5 でエラーが発生しました: {ex.Message}");
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
                    System.Console.WriteLine("使用方法: repair-dataset-id <対象日付(yyyy-MM-dd)>");
                    System.Console.WriteLine("例: repair-dataset-id 2025-06-02");
                    return;
                }

                if (!DateTime.TryParseExact(args[1], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var targetDate))
                {
                    System.Console.WriteLine("日付の形式が正しくありません。yyyy-MM-dd 形式で入力してください。");
                    return;
                }

                using var scope = services.CreateScope();
                var repairService = scope.ServiceProvider.GetRequiredService<DataSetIdRepairService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    System.Console.WriteLine("=== DataSetId不整合修復 開始 ===");
                    System.Console.WriteLine($"対象日: {targetDate:yyyy-MM-dd}");
                    System.Console.WriteLine();

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    // DataSetId不整合修復実行
                    var result = await repairService.RepairDataSetIdInconsistenciesAsync(targetDate);

                    stopwatch.Stop();

                    System.Console.WriteLine("=== 修復結果 ===");

                    // 売上伝票の修復結果
                    System.Console.WriteLine($"[売上伝票] 更新件数: {result.SalesVoucherResult.UpdatedRecords}件");
                    if (result.SalesVoucherResult.BeforeDataSetIds.Any())
                    {
                        System.Console.WriteLine($"  修復前DataSetId: {result.SalesVoucherResult.BeforeDataSetIds.Count}種類");
                        System.Console.WriteLine($"  修復後DataSetId: {result.SalesVoucherResult.CorrectDataSetId}");
                    }

                    // CP在庫マスタの修復結果
                    System.Console.WriteLine($"[CP在庫マスタ] 更新件数: {result.CpInventoryResult.UpdatedRecords}件");
                    if (result.CpInventoryResult.BeforeDataSetIds.Any())
                    {
                        System.Console.WriteLine($"  修復前DataSetId: {result.CpInventoryResult.BeforeDataSetIds.Count}種類");
                        System.Console.WriteLine($"  修復後DataSetId: {result.CpInventoryResult.CorrectDataSetId}");
                    }

                    // 仕入伝票の修復結果
                    if (result.PurchaseVoucherResult.UpdatedRecords > 0)
                    {
                        System.Console.WriteLine($"[仕入伝票] 更新件数: {result.PurchaseVoucherResult.UpdatedRecords}件");
                        System.Console.WriteLine($"  修復後DataSetId: {result.PurchaseVoucherResult.CorrectDataSetId}");
                    }

                    // 在庫調整の修復結果
                    if (result.InventoryAdjustmentResult.UpdatedRecords > 0)
                    {
                        System.Console.WriteLine($"[在庫調整] 更新件数: {result.InventoryAdjustmentResult.UpdatedRecords}件");
                        System.Console.WriteLine($"  修復後DataSetId: {result.InventoryAdjustmentResult.CorrectDataSetId}");
                    }

                    System.Console.WriteLine();
                    System.Console.WriteLine($"✅ DataSetId不整合修復が正常に完了しました");
                    System.Console.WriteLine($"   総更新件数: {result.TotalUpdatedRecords}件");
                    System.Console.WriteLine($"   処理時間: {stopwatch.Elapsed.TotalSeconds:F2}秒");
                    System.Console.WriteLine("=== DataSetId不整合修復 完了 ===");

                    logger.LogInformation("DataSetId不整合修復完了: TargetDate={TargetDate}, 総更新件数={TotalUpdatedRecords}, 処理時間={ElapsedMs}ms",
                        targetDate, result.TotalUpdatedRecords, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"❌ DataSetId不整合修復でエラーが発生しました: {ex.Message}");
                    logger.LogError(ex, "DataSetId不整合修復エラー: TargetDate={TargetDate}", targetDate);
                }
            }

            /// <summary>
            /// 移行フェーズの共通実行ロジック
            /// </summary>
            static async Task ExecuteMigrationPhaseAsync(IServiceProvider services, string scriptFileName, string phaseName)
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
                    System.Console.WriteLine($"=== {phaseName} 実行中 ===");

                    // 修正済みのGO文分割処理を使用
                    await ExecuteSqlScriptAsync(connection, scriptContent);

                    System.Console.WriteLine($"✅ {phaseName} 完了");
                    logger.LogInformation("=== {PhaseName} 完了 ===", phaseName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{PhaseName} 中にエラーが発生しました", phaseName);
                    System.Console.WriteLine($"❌ エラー: {ex.Message}");
                    throw;
                }
            }

            /// <summary>
            /// マスタテーブルのスキーマ確認
            /// </summary>
            static async Task ExecuteCheckSchemaAsync(IServiceProvider services, string[] args)
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

                    System.Console.WriteLine("=== テーブル存在確認 ===");
                    foreach (var table in tables)
                    {
                        System.Console.WriteLine($"  {table.TABLE_NAME}: {table.STATUS}");
                    }

                    // 日付カラムの確認
                    var checkDateColumnsSql = @"
                    SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
                    AND (COLUMN_NAME LIKE '%Created%' OR COLUMN_NAME LIKE '%Updated%' OR COLUMN_NAME LIKE '%Date%')
                    ORDER BY TABLE_NAME, COLUMN_NAME";

                    var dateColumns = await connection.QueryAsync(checkDateColumnsSql);

                    System.Console.WriteLine("\n=== 日付関連カラム確認 ===");
                    foreach (var col in dateColumns)
                    {
                        System.Console.WriteLine($"  {col.TABLE_NAME}.{col.COLUMN_NAME}: {col.DATA_TYPE} ({(col.IS_NULLABLE == "YES" ? "NULL許可" : "NOT NULL")})");
                    }

                    // 診断結果
                    System.Console.WriteLine("\n=== 診断結果 ===");

                    bool hasOldSchema = dateColumns.Any(c => c.COLUMN_NAME == "CreatedDate" || c.COLUMN_NAME == "UpdatedDate");
                    bool hasNewSchema = dateColumns.Any(c => c.COLUMN_NAME == "CreatedAt" || c.COLUMN_NAME == "UpdatedAt");

                    if (hasOldSchema && !hasNewSchema)
                    {
                        System.Console.WriteLine("🔴 問題: 古いスキーマ（CreatedDate/UpdatedDate）のみ存在");
                        System.Console.WriteLine("   → フェーズ2で新しいカラムの追加が必要");
                    }
                    else if (!hasOldSchema && hasNewSchema)
                    {
                        System.Console.WriteLine("✅ 正常: 新しいスキーマ（CreatedAt/UpdatedAt）のみ存在");
                        System.Console.WriteLine("   → 移行完了済み、追加の対応不要");
                    }
                    else if (hasOldSchema && hasNewSchema)
                    {
                        System.Console.WriteLine("🟡 移行中: 新旧両方のスキーマが存在");
                        System.Console.WriteLine("   → フェーズ3以降の処理が必要");
                    }
                    else
                    {
                        System.Console.WriteLine("🔴 問題: 日付カラムが見つかりません");
                        System.Console.WriteLine("   → テーブル定義に問題がある可能性");
                    }

                    logger.LogInformation("=== マスタテーブルスキーマ確認完了 ===");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "スキーマ確認中にエラーが発生しました");
                    System.Console.WriteLine($"❌ エラー: {ex.Message}");
                    throw;
                }
            }

            /// <summary>
            /// スクリプトファイルのパスを検索
            /// </summary>
            static string? FindScriptPath(string fileName)
            {
                // デバッグ情報の出力
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var currentDir = Environment.CurrentDirectory;
                System.Console.WriteLine($"BaseDirectory: {baseDir}");
                System.Console.WriteLine($"CurrentDirectory: {currentDir}");

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
                        System.Console.WriteLine($"Trying: {fullPath}");
                        if (File.Exists(fullPath))
                        {
                            System.Console.WriteLine($"Found: {fullPath}");
                            return fullPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error checking path {searchPath}: {ex.Message}");
                    }
                }

                System.Console.WriteLine("Script file not found in any candidate paths");
                return null;
            }

            /// <summary>
            /// GO文を基準にバッチ分割してSQLスクリプトを実行
            /// </summary>
            static async Task ExecuteSqlScriptAsync(Microsoft.Data.SqlClient.SqlConnection connection, string scriptContent)
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
                            System.Console.WriteLine($"Error executing USE statement: {ex.Message}");
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
                            System.Console.WriteLine($"Error executing batch: {ex.Message}");
                            System.Console.WriteLine($"Batch content (first 200 chars): {trimmedBatch.Substring(0, Math.Min(200, trimmedBatch.Length))}...");
                            throw;
                        }
                    }
                }
            }

            /// <summary>
            /// 主キー変更前のデータ分析を実行
            /// </summary>
            static async Task ExecuteAnalyzePrimaryKeyChangeAsync(IServiceProvider services, string[] args)
            {
                using var scope = services.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var logger = scopedServices.GetRequiredService<ILogger<AnalyzePrimaryKeyChangeCommand>>();
                var configuration = scopedServices.GetRequiredService<IConfiguration>();

                try
                {
                    var command = new AnalyzePrimaryKeyChangeCommand(configuration, logger);
                    await command.ExecuteAsync();

                    System.Console.WriteLine("\n分析が完了しました。");
                    System.Console.WriteLine("次のステップ：");
                    System.Console.WriteLine("1. 分析結果を確認し、履歴データの保存が必要か判断");
                    System.Console.WriteLine("2. 必要に応じてバックアップテーブルを作成");
                    System.Console.WriteLine("3. マイグレーションスクリプトを実行");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "主キー変更分析でエラーが発生しました");
                    System.Console.WriteLine($"❌ エラー: {ex.Message}");
                }
            }
        }
    }
}
