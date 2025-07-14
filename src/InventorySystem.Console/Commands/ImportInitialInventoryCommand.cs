using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Services;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Console.Commands;

/// <summary>
/// 初期在庫インポートコマンド
/// </summary>
public class ImportInitialInventoryCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImportInitialInventoryCommand> _logger;
    private readonly IConfiguration _configuration;

    public ImportInitialInventoryCommand(
        IServiceProvider serviceProvider,
        ILogger<ImportInitialInventoryCommand> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// コマンド実行
    /// </summary>
    public async Task ExecuteAsync(string department = "DeptA")
    {
        try
        {
            _logger.LogInformation("=== 初期在庫インポートコマンド開始 ===");
            _logger.LogInformation("部門: {Department}", department);

            // 設定からパスを取得
            var importPath = _configuration["ImportSettings:ImportPath"]?
                .Replace("{Department}", department) 
                ?? throw new InvalidOperationException("ImportPathが設定されていません");
                
            var processedPath = _configuration["ImportSettings:ProcessedPath"]?
                .Replace("{Department}", department)
                ?? throw new InvalidOperationException("ProcessedPathが設定されていません");
                
            var errorPath = _configuration["ImportSettings:ErrorPath"]?
                .Replace("{Department}", department)
                ?? throw new InvalidOperationException("ErrorPathが設定されていません");

            // サービスの取得と実行
            using var scope = _serviceProvider.CreateScope();
            var inventoryRepository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            var productRepository = scope.ServiceProvider.GetRequiredService<IProductMasterRepository>();
            var dataSetRepository = scope.ServiceProvider.GetRequiredService<IDatasetManagementRepository>();
            var serviceLogger = scope.ServiceProvider.GetRequiredService<ILogger<InitialInventoryImportService>>();

            var service = new InitialInventoryImportService(
                inventoryRepository,
                productRepository,
                dataSetRepository,
                serviceLogger,
                importPath,
                processedPath,
                errorPath);

            var result = await service.ImportAsync(department);

            // 結果表示
            System.Console.WriteLine();
            System.Console.WriteLine("=== 初期在庫インポート結果 ===");
            System.Console.WriteLine($"ステータス: {(result.IsSuccess ? "成功" : "失敗")}");
            System.Console.WriteLine($"メッセージ: {result.Message}");
            System.Console.WriteLine($"DataSetId: {result.DataSetId}");
            System.Console.WriteLine($"成功件数: {result.SuccessCount:N0}");
            System.Console.WriteLine($"エラー件数: {result.ErrorCount:N0}");
            System.Console.WriteLine($"処理時間: {result.Duration.TotalSeconds:F2}秒");

            if (result.Errors.Any())
            {
                System.Console.WriteLine();
                System.Console.WriteLine("エラー詳細:");
                foreach (var error in result.Errors.Take(10))
                {
                    System.Console.WriteLine($"  - {error}");
                }
                if (result.Errors.Count > 10)
                {
                    System.Console.WriteLine($"  ... 他 {result.Errors.Count - 10}件のエラー");
                }
            }

            System.Console.WriteLine();
            System.Console.WriteLine("=== 初期在庫インポートコマンド完了 ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初期在庫インポートコマンドエラー");
            System.Console.WriteLine($"エラー: {ex.Message}");
            throw;
        }
    }
}