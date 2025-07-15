using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Services;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;

namespace InventorySystem.Console.Commands;

/// <summary>
/// 初期在庫インポートコマンド
/// </summary>
public class ImportInitialInventoryCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImportInitialInventoryCommand> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _importPath;
    private readonly string _processedPath;
    private readonly string _errorPath;

    public ImportInitialInventoryCommand(
        IServiceProvider serviceProvider,
        ILogger<ImportInitialInventoryCommand> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // パスを直接設定
        _importPath = @"D:\InventoryImport\{Department}\Import";
        _processedPath = @"D:\InventoryImport\{Department}\Processed";
        _errorPath = @"D:\InventoryImport\{Department}\Error";
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

            // 部門コードでプレースホルダーを置換
            var importPath = _importPath.Replace("{Department}", department);
            var processedPath = _processedPath.Replace("{Department}", department);
            var errorPath = _errorPath.Replace("{Department}", department);
            
            // ディレクトリの存在確認
            if (!Directory.Exists(importPath))
            {
                Directory.CreateDirectory(importPath);
                _logger.LogWarning($"Importディレクトリを作成しました: {importPath}");
            }
            
            _logger.LogInformation("インポートパス: {ImportPath}", importPath);
            _logger.LogInformation("処理済みパス: {ProcessedPath}", processedPath);
            _logger.LogInformation("エラーパス: {ErrorPath}", errorPath);

            // サービスの取得と実行
            using var scope = _serviceProvider.CreateScope();
            var inventoryRepository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            var productRepository = scope.ServiceProvider.GetRequiredService<IProductMasterRepository>();
            var dataSetRepository = scope.ServiceProvider.GetRequiredService<IDataSetManagementRepository>();
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