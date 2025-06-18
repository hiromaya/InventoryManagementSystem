using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Services;
using InventorySystem.Data.Repositories;
using InventorySystem.Reports.Services;
using InventorySystem.Import.Services;
using System.Diagnostics;
using QuestPDF.Infrastructure;

// QuestPDF ライセンス設定（Community License）
QuestPDF.Settings.License = LicenseType.Community;

var builder = Host.CreateApplicationBuilder();

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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

builder.Services.AddScoped<IUnmatchListService, UnmatchListService>();
builder.Services.AddScoped<UnmatchListReportService>();
builder.Services.AddScoped<SalesVoucherImportService>();
builder.Services.AddScoped<PurchaseVoucherImportService>();
builder.Services.AddScoped<InventoryAdjustmentImportService>();

var host = builder.Build();

// Parse command line arguments
var commandArgs = Environment.GetCommandLineArgs();

if (commandArgs.Length < 2)
{
    Console.WriteLine("使用方法:");
    Console.WriteLine("  dotnet run unmatch-list [YYYY-MM-DD]         - アンマッチリスト処理を実行");
    Console.WriteLine("  dotnet run import-sales <file> [YYYY-MM-DD]  - 売上伝票CSVを取込");
    Console.WriteLine("  dotnet run import-purchase <file> [YYYY-MM-DD] - 仕入伝票CSVを取込");
    Console.WriteLine("  dotnet run import-adjustment <file> [YYYY-MM-DD] - 在庫調整CSVを取込");
    Console.WriteLine("  例: dotnet run unmatch-list 2025-06-16");
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
            InventorySystem.Console.TestWithoutDatabase.RunPdfTest();
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
    var reportService = services.GetRequiredService<UnmatchListReportService>();
    
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
            Console.WriteLine("PDF生成中...");
            var pdfBytes = reportService.GenerateUnmatchListReport(result.UnmatchItems, jobDate);
            
            var outputPath = Path.Combine(Environment.CurrentDirectory, 
                $"unmatch_list_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
            
            await File.WriteAllBytesAsync(outputPath, pdfBytes);
            Console.WriteLine($"PDF出力完了: {outputPath}");
            
            // PDFを開く（Windows）
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
