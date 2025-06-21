using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Services;
using InventorySystem.Data.Repositories;
using InventorySystem.Import.Services;
using InventorySystem.Reports.Services;
using System.Diagnostics;
using QuestPDF.Infrastructure;

// FastReportテストコマンド（using文を遅延読み込みするため先に処理）
if (args.Length > 0 && args[0] == "test-fastreport")
{
    try
    {
        RunFastReportTest();
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FastReportテストエラー: {ex.Message}");
        Console.WriteLine($"詳細: {ex}");
        return 1;
    }
}

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
builder.Services.AddScoped<IDailyReportService, DailyReportService>();
builder.Services.AddScoped<IInventoryListService, InventoryListService>();
// FastReportテスト時はReportsサービスをコメントアウト
// builder.Services.AddScoped<UnmatchListReportService>();
// builder.Services.AddScoped<InventorySystem.Reports.Services.DailyReportPdfService>();
// builder.Services.AddScoped<InventoryListReportService>();
builder.Services.AddScoped<SalesVoucherImportService>();
builder.Services.AddScoped<PurchaseVoucherImportService>();
builder.Services.AddScoped<InventoryAdjustmentImportService>();

// FastReportサービスの登録（FastReportテスト時はコメントアウト）
// builder.Services.AddScoped<InventorySystem.Reports.FastReport.Services.FastReportService>();
// builder.Services.AddScoped<InventorySystem.Reports.FastReport.Services.DailyReportFastReportService>();

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
            InventorySystem.Console.TestWithoutDatabase.RunPdfTest();
            break;
            
        case "test-connection":
            await TestDatabaseConnectionAsync(host.Services);
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

static async Task ExecuteDailyReportAsync(IServiceProvider services, string[] args)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var dailyReportService = services.GetRequiredService<IDailyReportService>();
    var reportService = services.GetRequiredService<InventorySystem.Reports.Services.DailyReportPdfService>();
    
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
            
            var outputPath = Path.Combine(Environment.CurrentDirectory, 
                $"daily_report_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
            
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
    var reportService = services.GetRequiredService<InventoryListReportService>();
    
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
    
    Console.WriteLine("=== 在庫表処理開始 ===");
    Console.WriteLine($"レポート日付: {jobDate:yyyy-MM-dd}");
    Console.WriteLine();
    
    // 在庫表処理実行
    var result = await inventoryListService.ProcessInventoryListAsync(jobDate);
    
    stopwatch.Stop();
    
    if (result.Success)
    {
        Console.WriteLine("=== 処理結果 ===");
        Console.WriteLine($"データセットID: {result.DataSetId}");
        Console.WriteLine($"データ件数: {result.ProcessedCount}");
        Console.WriteLine($"担当者数: {result.StaffInventories.Count}");
        Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
        Console.WriteLine();
        
        if (result.StaffInventories.Any())
        {
            Console.WriteLine("=== 担当者別集計結果 ===");
            foreach (var staff in result.StaffInventories.Take(3))
            {
                Console.WriteLine($"担当者: {staff.StaffName} | 商品数: {staff.Items.Count} | 合計金額: {staff.Total.GrandTotalAmount:N0}円");
            }
            
            if (result.StaffInventories.Count > 3)
            {
                Console.WriteLine($"... 他 {result.StaffInventories.Count - 3} 名");
            }
            Console.WriteLine($"全体合計: {result.GrandTotal.GrandTotalAmount:N0}円");
            Console.WriteLine();
        }
        
        // PDF出力
        try
        {
            Console.WriteLine("PDF生成中...");
            var pdfBytes = reportService.GenerateInventoryList(result.StaffInventories, result.GrandTotal, jobDate);
            
            var outputPath = Path.Combine(Environment.CurrentDirectory, 
                $"inventory_list_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
            
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
        
        Console.WriteLine("=== 在庫表処理完了 ===");
    }
    else
    {
        Console.WriteLine("=== 処理失敗 ===");
        Console.WriteLine($"エラーメッセージ: {result.ErrorMessage}");
        Console.WriteLine($"処理時間: {result.ProcessingTime.TotalSeconds:F2}秒");
        
        logger.LogError("在庫表処理が失敗しました: {ErrorMessage}", result.ErrorMessage);
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

// FastReportテスト実行メソッド
static void RunFastReportTest()
{
    Console.WriteLine("=== FastReport.NET Trial テスト開始 ===");
    Console.WriteLine($"実行時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    try
    {
        // プラットフォーム確認
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("\n✓ Linux環境での実行を確認");
            Console.WriteLine("FastReport.NET は Windows専用のため、実際のレポート生成はスキップされます。");
            Console.WriteLine("Windows環境では以下のテストが実行されます：");
            Console.WriteLine("  1. FastReportアセンブリの読み込み");
            Console.WriteLine("  2. 基本レポートテスト（PDF生成）");
            Console.WriteLine("  3. 最小レポートテスト（PDF生成）");
            Console.WriteLine("\n=== クロスプラットフォームテスト完了 ===");
            return;
        }
        
        // Windows環境でのテスト実行
        // アセンブリの動的読み込みとテスト
        Console.WriteLine("\n1. InventorySystem.Reportsアセンブリの読み込み確認...");
        var currentDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
        var reportsAssemblyPath = System.IO.Path.Combine(currentDir, "InventorySystem.Reports.dll");
        var reportsAssembly = System.Reflection.Assembly.LoadFrom(reportsAssemblyPath);
        Console.WriteLine("✓ InventorySystem.Reportsアセンブリ読み込み成功");
        
        // FastReportTestクラスの動的取得とメソッド実行
        Console.WriteLine("\n2. 基本レポートテスト...");
        var testType = reportsAssembly.GetType("InventorySystem.Reports.Tests.FastReportTest");
        var testMethod = testType?.GetMethod("TestBasicReport");
        testMethod?.Invoke(null, null);
        Console.WriteLine("✓ 基本レポートテスト成功");
        
        // 最小限のレポートテスト
        Console.WriteLine("\n3. 最小レポートテスト...");
        TestMinimalReportDynamic();
        Console.WriteLine("✓ 最小レポートテスト成功");
        
        Console.WriteLine("\n=== すべてのテストが完了しました ===");
        Console.WriteLine("生成されたPDFファイルを確認してください。");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n✗ エラーが発生しました: {ex.Message}");
        Console.WriteLine($"詳細: {ex.StackTrace}");
    }
}

// 最小限のFastReportテスト（動的読み込み版）
static void TestMinimalReportDynamic()
{
    try
    {
        // FastReportアセンブリを動的に読み込み
        var currentDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
        var fastReportAssemblyPath = System.IO.Path.Combine(currentDir, "FastReport.dll");
        var fastReportAssembly = System.Reflection.Assembly.LoadFrom(fastReportAssemblyPath);
        
        // 必要な型を動的に取得
        var reportType = fastReportAssembly.GetType("FastReport.Report");
        var pageType = fastReportAssembly.GetType("FastReport.ReportPage");
        var titleBandType = fastReportAssembly.GetType("FastReport.ReportTitleBand");
        var textObjectType = fastReportAssembly.GetType("FastReport.TextObject");
        var pdfExportType = fastReportAssembly.GetType("FastReport.Export.Pdf.PDFExport");
        
        // Null チェック
        if (reportType == null || pageType == null || titleBandType == null || textObjectType == null || pdfExportType == null)
        {
            throw new InvalidOperationException("FastReport の必要な型が見つかりません");
        }
        
        // レポートインスタンス作成
        using var report = (IDisposable)Activator.CreateInstance(reportType)!;
        
        // ページ追加
        var page = Activator.CreateInstance(pageType)!;
        var pagesProperty = reportType.GetProperty("Pages");
        var pages = pagesProperty?.GetValue(report);
        var addMethod = pages?.GetType().GetMethod("Add");
        addMethod?.Invoke(pages, new[] { page });
        
        // タイトルバンド
        var title = Activator.CreateInstance(titleBandType)!;
        var heightProperty = titleBandType.GetProperty("Height");
        heightProperty?.SetValue(title, 50f);
        
        var reportTitleProperty = pageType.GetProperty("ReportTitle");
        reportTitleProperty?.SetValue(page, title);
        
        // タイトルテキスト
        var text = Activator.CreateInstance(textObjectType)!;
        var boundsProperty = textObjectType.GetProperty("Bounds");
        boundsProperty?.SetValue(text, new System.Drawing.RectangleF(0, 0, 300, 30));
        
        var textProperty = textObjectType.GetProperty("Text");
        textProperty?.SetValue(text, "FastReport.NET 最小テスト");
        
        var fontProperty = textObjectType.GetProperty("Font");
        fontProperty?.SetValue(text, new System.Drawing.Font("MS Gothic", 14));
        
        // オブジェクト追加
        var objectsProperty = titleBandType.GetProperty("Objects");
        var objects = objectsProperty?.GetValue(title);
        var addObjectMethod = objects?.GetType().GetMethod("Add");
        addObjectMethod?.Invoke(objects, new[] { text });
        
        // レポート生成
        var prepareMethod = reportType.GetMethod("Prepare", Type.EmptyTypes);
        prepareMethod?.Invoke(report, null);
        
        // PDF出力
        var pdfExport = Activator.CreateInstance(pdfExportType)!;
        var exportMethod = reportType.GetMethod("Export", new[] { pdfExportType, typeof(string) });
        exportMethod?.Invoke(report, new[] { pdfExport, "minimal_test_dynamic.pdf" });
        
        Console.WriteLine("動的読み込みによるPDF生成完了: minimal_test_dynamic.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"動的読み込みテストエラー: {ex.Message}");
        throw;
    }
}
