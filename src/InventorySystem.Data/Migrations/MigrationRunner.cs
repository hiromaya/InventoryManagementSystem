using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Dapper;

namespace InventorySystem.Data.Migrations;

/// <summary>
/// データベースマイグレーション実行クラス
/// </summary>
public class MigrationRunner
{
    private readonly string _connectionString;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(string connectionString, ILogger<MigrationRunner> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task RunMigrationsAsync()
    {
        await EnsureMigrationTableExists();
        
        var migrations = await GetPendingMigrations();
        foreach (var migration in migrations)
        {
            await ExecuteMigration(migration);
        }
    }

    private async Task EnsureMigrationTableExists()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[__Migrations]') AND type in (N'U'))
            BEGIN
                CREATE TABLE __Migrations (
                    MigrationId NVARCHAR(150) PRIMARY KEY,
                    AppliedOn DATETIME2 NOT NULL DEFAULT GETDATE()
                )
            END");
    }

    private async Task<List<IMigration>> GetPendingMigrations()
    {
        using var connection = new SqlConnection(_connectionString);
        var appliedMigrations = await connection.QueryAsync<string>(
            "SELECT MigrationId FROM __Migrations");

        var allMigrations = new List<IMigration>
        {
            new Migration001_AddGrossProfit(),
            new Migration002_ExpandVoucherId(),
            new Migration003_AddProductNameColumn(),
            // 将来のマイグレーションをここに追加
        };

        return allMigrations
            .Where(m => !appliedMigrations.Contains(m.Id))
            .OrderBy(m => m.Id)
            .ToList();
    }

    private async Task ExecuteMigration(IMigration migration)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            _logger.LogInformation($"マイグレーション実行中: {migration.Id}");
            
            await migration.UpAsync(connection, transaction);
            
            await connection.ExecuteAsync(
                "INSERT INTO __Migrations (MigrationId) VALUES (@Id)",
                new { migration.Id },
                transaction);
            
            await transaction.CommitAsync();
            _logger.LogInformation($"マイグレーション完了: {migration.Id}");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public interface IMigration
{
    string Id { get; }
    Task UpAsync(SqlConnection connection, SqlTransaction transaction);
}

public class Migration001_AddGrossProfit : IMigration
{
    public string Id => "001_AddGrossProfit";

    public async Task UpAsync(SqlConnection connection, SqlTransaction transaction)
    {
        // SalesVouchersにGrossProfitカラムを追加
        var grossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
            AND name = 'GrossProfit'", transaction: transaction) > 0;

        if (!grossProfitExists)
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[SalesVouchers]
                ADD [GrossProfit] DECIMAL(16,4) NULL", transaction: transaction);
        }

        // PurchaseVouchersテーブルにも追加
        var purchaseGrossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVouchers]') 
            AND name = 'GrossProfit'", transaction: transaction) > 0;

        if (!purchaseGrossProfitExists)
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[PurchaseVouchers]
                ADD [GrossProfit] DECIMAL(16,4) NULL", transaction: transaction);
        }

        // InventoryAdjustmentsテーブルにも追加
        var adjustmentGrossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') 
            AND name = 'GrossProfit'", transaction: transaction) > 0;

        if (!adjustmentGrossProfitExists)
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[InventoryAdjustments]
                ADD [GrossProfit] DECIMAL(16,4) NULL", transaction: transaction);
        }
    }
}

public class Migration002_ExpandVoucherId : IMigration
{
    public string Id => "002_ExpandVoucherId";

    public async Task UpAsync(SqlConnection connection, SqlTransaction transaction)
    {
        // データが存在しない場合のみVoucherIdを拡張
        var hasData = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SalesVouchers", transaction: transaction) > 0;

        if (!hasData)
        {
            var currentSize = await connection.ExecuteScalarAsync<int>(@"
                SELECT max_length FROM sys.columns 
                WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
                AND name = 'VoucherId'", transaction: transaction);

            if (currentSize == 100) // 50文字の場合
            {
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] DROP CONSTRAINT PK_SalesVouchers", 
                    transaction: transaction);
                
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] ALTER COLUMN VoucherId NVARCHAR(100) NOT NULL", 
                    transaction: transaction);
                
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] ADD CONSTRAINT PK_SalesVouchers PRIMARY KEY (VoucherId, LineNumber)",
                    transaction: transaction);
            }
        }
    }
}

public class Migration003_AddProductNameColumn : IMigration
{
    public string Id => "003_AddProductNameColumn";

    public async Task UpAsync(SqlConnection connection, SqlTransaction transaction)
    {
        // SalesVouchersにProductNameカラムを追加
        var productNameExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
            AND name = 'ProductName'", transaction: transaction) > 0;

        if (!productNameExists)
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[SalesVouchers]
                ADD [ProductName] NVARCHAR(100) NULL", transaction: transaction);
        }

        // PurchaseVouchersにも追加
        productNameExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVouchers]') 
            AND name = 'ProductName'", transaction: transaction) > 0;

        if (!productNameExists)
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[PurchaseVouchers]
                ADD [ProductName] NVARCHAR(100) NULL", transaction: transaction);
        }

        // InventoryAdjustmentsにも追加
        productNameExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') 
            AND name = 'ProductName'", transaction: transaction) > 0;

        if (!productNameExists)
        {
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[InventoryAdjustments]
                ADD [ProductName] NVARCHAR(100) NULL", transaction: transaction);
        }
    }
}