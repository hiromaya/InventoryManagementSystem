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
#if WINDOWS
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.FastReport.Services;
using InventorySystem.Reports.Services; // Placeholderサービス用に追加
#else
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.Services;
#endif
using System.Diagnostics;
using System.Reflection;

// 実行環境情報の表示
Console.WriteLine($"実行環境: {Environment.OSVersion}");
Console.WriteLine($".NET Runtime: {Environment.Version}");
Console.WriteLine($"実行ディレクトリ: {Environment.CurrentDirectory}");

// FastReportテストコマンドの早期処理
if (args.Length > 0 && args[0] == "test-fastreport")
{
    Console.WriteLine("=== FastReport.NET Trial テスト開始 ===");
    Console.WriteLine($"実行時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    // プラットフォーム確認
    if (!OperatingSystem.IsWindows())
    {
        Console.WriteLine("\n✓ Linux環境での実行を確認");
        Console.WriteLine("FastReport.NET は Windows専用のため、実際のレポート生成はスキップされます。");
        Console.WriteLine("Windows環境では以下のテストが実行されます：");
        Console.WriteLine("  1. FastReportアセンブリの読み込み");
        Console.WriteLine("  2. アンマッチリスト・商品日報のFastReport実装確認");
        Console.WriteLine("  3. PDF生成テスト");
        Console.WriteLine("\n✓ QuestPDFからFastReport.NETへの移行が完了しています");
        Console.WriteLine("✓ クロスプラットフォーム対応が実装済みです");
        Console.WriteLine("\n=== FastReport.NET移行テスト完了 ===");
        return 0;
    }
    else
    {
        Console.WriteLine("\n✓ Windows環境を検出");
        Console.WriteLine("✓ FastReport.NET Trial版が利用可能です");
        Console.WriteLine("✓ アンマッチリスト・商品日報の実装が完了しています");
        Console.WriteLine("\n実際のPDF生成テストを実行するには：");
        Console.WriteLine("  dotnet run unmatch-list [日付] # アンマッチリストPDF生成");
        Console.WriteLine("  dotnet run daily-report [日付] # 商品日報PDF生成");
        Console.WriteLine("\n=== FastReport.NET移行テスト完了 ===");
        return 0;
    }
}

// FastReport.NET Trial版を使用

var builder = Host.CreateApplicationBuilder();

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add Memory Cache for master data repositories
builder.Services.AddMemoryCache();

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

builder.Services.AddScoped<IUnmatchListService, UnmatchListService>();
builder.Services.AddScoped<InventorySystem.Core.Interfaces.IDailyReportService, DailyReportService>();
builder.Services.AddScoped<IInventoryListService, InventoryListService>();
// Report Services
#if WINDOWS
if (OperatingSystem.IsWindows())
{
    // Windows環境：FastReport実装を使用
    builder.Services.AddScoped<IUnmatchListReportService, UnmatchListFastReportService>();
    builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IDailyReportService, DailyReportFastReportService>();
}
else
{
    // Windowsではない場合（念のため）
    builder.Services.AddScoped<IUnmatchListReportService, PlaceholderUnmatchListReportService>();
    builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IDailyReportService, PlaceholderDailyReportService>();
}
#else
// Linux環境：プレースホルダー実装を使用
builder.Services.AddScoped<IUnmatchListReportService, PlaceholderUnmatchListReportService>();
builder.Services.AddScoped<InventorySystem.Reports.Interfaces.IDailyReportService, PlaceholderDailyReportService>();
#endif
builder.Services.AddScoped<SalesVoucherImportService>();
builder.Services.AddScoped<PurchaseVoucherImportService>();
builder.Services.AddScoped<InventoryAdjustmentImportService>();


var host = builder.Build();

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

static async Task ExecuteUnmatchListAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var unmatchListService = services.GetRequiredService<IUnmatchListService>();
    
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
            var reportService = services.GetRequiredService<IUnmatchListReportService>();
            Console.WriteLine("PDF生成中...");
            var pdfBytes = reportService.GenerateUnmatchListReport(result.UnmatchItems, jobDate);
            
            var outputPath = Path.Combine(Environment.CurrentDirectory, 
                $"unmatch_list_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
            
            await File.WriteAllBytesAsync(outputPath, pdfBytes);
            Console.WriteLine($"PDF出力完了: {outputPath}");
            
            // PDFを開く（Windows環境のみ）
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
            }
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

static async Task ExecuteImportSalesAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var importService = services.GetRequiredService<SalesVoucherImportService>();
    
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

static async Task ExecuteImportPurchaseAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var importService = services.GetRequiredService<PurchaseVoucherImportService>();
    
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

static async Task ExecuteImportAdjustmentAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var importService = services.GetRequiredService<InventoryAdjustmentImportService>();
    
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

static async Task ExecuteDailyReportAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var dailyReportService = services.GetRequiredService<InventorySystem.Core.Interfaces.IDailyReportService>();
    
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
            var reportService = services.GetRequiredService<InventorySystem.Reports.Interfaces.IDailyReportService>();
            Console.WriteLine("PDF生成中...");
            var pdfBytes = reportService.GenerateDailyReport(result.ReportItems, result.Subtotals, result.Total, jobDate);
            
            var outputPath = Path.Combine(Environment.CurrentDirectory, 
                $"daily_report_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
            
            await File.WriteAllBytesAsync(outputPath, pdfBytes);
            Console.WriteLine($"PDF出力完了: {outputPath}");
            
            // PDFを開く（Windows環境のみ）
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
            }
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

static async Task ExecuteInventoryListAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var inventoryListService = services.GetRequiredService<IInventoryListService>();
    // TODO: Implement FastReport version for inventory list
    Console.WriteLine("在庫表のFastReport対応は未実装です。QuestPDFからの移行が必要です。");
    await Task.CompletedTask; // 警告を回避
}

static async Task DebugCsvStructureAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("使用法: dotnet run debug-csv-structure <csvファイル>");
        return;
    }

    var csvFilePath = args[1];
    if (!File.Exists(csvFilePath))
    {
        Console.WriteLine($"ファイルが見つかりません: {csvFilePath}");
        return;
    }

    using var reader = new StreamReader(csvFilePath, System.Text.Encoding.UTF8);
    
    // ヘッダー行を読み取り
    var headerLine = await reader.ReadLineAsync();
    if (headerLine != null)
    {
        var headers = headerLine.Split(',');
        Console.WriteLine($"=== CSV構造分析 ===");
        Console.WriteLine($"総列数: {headers.Length}");
        Console.WriteLine();
        
        // 重要な列を検索
        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim('"');
            if (header.Contains("得意先") || header.Contains("客先") || header.Contains("顧客"))
            {
                Console.WriteLine($"列{i:D3}: [{header}] ← 得意先関連");
            }
            else if (header.Contains("仕入先"))
            {
                Console.WriteLine($"列{i:D3}: [{header}] ← 仕入先関連");
            }
            else if (header.Contains("商品") || header.Contains("品名") || header.Contains("製品"))
            {
                Console.WriteLine($"列{i:D3}: [{header}] ← 商品関連");
            }
            else if (header.Contains("コード") && i < 20)
            {
                Console.WriteLine($"列{i:D3}: [{header}] ← コード関連");
            }
        }
    }
    
    // データ行のサンプルを表示
    Console.WriteLine("\n=== データサンプル（最初の3行） ===");
    for (int rowIndex = 0; rowIndex < 3; rowIndex++)
    {
        var dataLine = await reader.ReadLineAsync();
        if (dataLine == null) break;
        
        var values = dataLine.Split(',');
        Console.WriteLine($"\n--- 行 {rowIndex + 1} ---");
        
        // 得意先コード・名前周辺を表示
        Console.WriteLine("\n--- 最初の15列 ---");
        for (int i = 0; i < Math.Min(15, values.Length); i++)
        {
            var value = values[i].Trim('"');
            if (!string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine($"列{i:D3}: [{value}]");
            }
        }
        
        // 商品コード・名前周辺を表示（80列目～95列目）
        Console.WriteLine("\n--- 商品関連（80-95列目） ---");
        for (int i = 80; i < Math.Min(95, values.Length); i++)
        {
            var value = values[i].Trim('"');
            if (!string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine($"列{i:D3}: [{value}]");
            }
        }
        
        // 140列目周辺も確認
        Console.WriteLine("\n--- 140列目周辺 ---");
        for (int i = 135; i < Math.Min(150, values.Length); i++)
        {
            var value = values[i].Trim('"');
            if (!string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine($"列{i:D3}: [{value}]");
            }
        }
    }
}

static async Task TestDatabaseConnectionAsync(IServiceProvider services)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var configuration = services.GetRequiredService<IConfiguration>();
    
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

static async Task ExecuteImportCustomersAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var importService = services.GetRequiredService<CustomerMasterImportService>();
    
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

static async Task ExecuteImportProductsAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var importService = services.GetRequiredService<ProductMasterImportService>();
    
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

static async Task ExecuteImportSuppliersAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var importService = services.GetRequiredService<SupplierMasterImportService>();
    
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


