using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Data.Repositories.Masters;
using InventorySystem.Import.Services.Masters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Import.Services;

/// <summary>
/// インポートサービスのDI登録拡張メソッド
/// </summary>
public static class ImportServiceExtensions
{
    /// <summary>
    /// すべてのインポートサービスとリポジトリをDIコンテナに登録
    /// </summary>
    public static IServiceCollection AddImportServices(this IServiceCollection services, string connectionString)
    {
        // ========== リポジトリ登録 ==========
        
        // 単位マスタ
        services.AddScoped<IUnitMasterRepository>(provider =>
            new UnitMasterRepository(connectionString, provider.GetRequiredService<ILogger<UnitMasterRepository>>()));

        // 分類マスタ（ジェネリックリポジトリ）
        services.AddScoped<ICategoryMasterRepository<ProductCategory1Master>>(provider =>
            new CategoryMasterRepository<ProductCategory1Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<ProductCategory1Master>>>()));
        services.AddScoped<ICategoryMasterRepository<ProductCategory2Master>>(provider =>
            new CategoryMasterRepository<ProductCategory2Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<ProductCategory2Master>>>()));
        services.AddScoped<ICategoryMasterRepository<ProductCategory3Master>>(provider =>
            new CategoryMasterRepository<ProductCategory3Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<ProductCategory3Master>>>()));
        
        services.AddScoped<ICategoryMasterRepository<CustomerCategory1Master>>(provider =>
            new CategoryMasterRepository<CustomerCategory1Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<CustomerCategory1Master>>>()));
        services.AddScoped<ICategoryMasterRepository<CustomerCategory2Master>>(provider =>
            new CategoryMasterRepository<CustomerCategory2Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<CustomerCategory2Master>>>()));
        services.AddScoped<ICategoryMasterRepository<CustomerCategory3Master>>(provider =>
            new CategoryMasterRepository<CustomerCategory3Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<CustomerCategory3Master>>>()));
        services.AddScoped<ICategoryMasterRepository<CustomerCategory4Master>>(provider =>
            new CategoryMasterRepository<CustomerCategory4Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<CustomerCategory4Master>>>()));
        services.AddScoped<ICategoryMasterRepository<CustomerCategory5Master>>(provider =>
            new CategoryMasterRepository<CustomerCategory5Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<CustomerCategory5Master>>>()));
        
        services.AddScoped<ICategoryMasterRepository<SupplierCategory1Master>>(provider =>
            new CategoryMasterRepository<SupplierCategory1Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<SupplierCategory1Master>>>()));
        services.AddScoped<ICategoryMasterRepository<SupplierCategory2Master>>(provider =>
            new CategoryMasterRepository<SupplierCategory2Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<SupplierCategory2Master>>>()));
        services.AddScoped<ICategoryMasterRepository<SupplierCategory3Master>>(provider =>
            new CategoryMasterRepository<SupplierCategory3Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<SupplierCategory3Master>>>()));
        
        services.AddScoped<ICategoryMasterRepository<StaffCategory1Master>>(provider =>
            new CategoryMasterRepository<StaffCategory1Master>(connectionString, provider.GetRequiredService<ILogger<CategoryMasterRepository<StaffCategory1Master>>>()));

        // 担当者マスタ
        services.AddScoped<IStaffMasterRepository>(provider =>
            new StaffMasterRepository(connectionString, provider.GetRequiredService<ILogger<StaffMasterRepository>>()));

        // ========== インポートサービス登録 ==========
        
        // 単位マスタ
        services.AddScoped<IImportService, UnitMasterImportService>();
        
        // 商品分類マスタ
        services.AddScoped<IImportService, ProductCategory1ImportService>();
        services.AddScoped<IImportService, ProductCategory2ImportService>();
        services.AddScoped<IImportService, ProductCategory3ImportService>();
        
        // 得意先分類マスタ
        services.AddScoped<IImportService, CustomerCategory1ImportService>();
        services.AddScoped<IImportService, CustomerCategory2ImportService>();
        services.AddScoped<IImportService, CustomerCategory3ImportService>();
        services.AddScoped<IImportService, CustomerCategory4ImportService>();
        services.AddScoped<IImportService, CustomerCategory5ImportService>();
        
        // 仕入先分類マスタ
        services.AddScoped<IImportService, SupplierCategory1ImportService>();
        services.AddScoped<IImportService, SupplierCategory2ImportService>();
        services.AddScoped<IImportService, SupplierCategory3ImportService>();
        
        // 担当者マスタ
        services.AddScoped<IImportService, StaffMasterImportService>();
        services.AddScoped<IImportService, StaffCategory1ImportService>();
        
        // 入金・支払伝票
        services.AddScoped<IImportService, ReceiptVoucherImportService>();
        services.AddScoped<IImportService, PaymentVoucherImportService>();

        return services;
    }
    
    /// <summary>
    /// インポートサービスの一覧を取得（デバッグ用）
    /// </summary>
    public static void LogImportServices(this IServiceProvider serviceProvider)
    {
        var services = serviceProvider.GetServices<IImportService>();
        var logger = serviceProvider.GetRequiredService<ILogger<object>>();
        
        logger.LogInformation("=== 登録済みインポートサービス一覧 ===");
        foreach (var service in services.OrderBy(s => s.ProcessOrder))
        {
            logger.LogInformation("Order: {Order}, Service: {ServiceName}, Type: {Type}", 
                service.ProcessOrder, service.ServiceName, service.GetType().Name);
        }
        logger.LogInformation("=== 合計: {Count}種類のサービス ===", services.Count());
    }
}