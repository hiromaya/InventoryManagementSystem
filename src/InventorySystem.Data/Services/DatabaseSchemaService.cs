using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Dapper;

namespace InventorySystem.Data.Services;

/// <summary>
/// データベーススキーマの自動更新サービス
/// </summary>
public class DatabaseSchemaService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseSchemaService> _logger;

    public DatabaseSchemaService(string connectionString, ILogger<DatabaseSchemaService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// スキーマを最新版に更新
    /// </summary>
    public async Task UpdateSchemaAsync()
    {
        _logger.LogInformation("データベーススキーマの確認を開始します...");

        try
        {
            await AddGrossProfitColumnsAsync();
            await UpdateVoucherIdSizeAsync();
            await AddProductNameColumnAsync();
            
            _logger.LogInformation("データベーススキーマの更新が完了しました。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データベーススキーマの更新中にエラーが発生しました。");
            throw;
        }
    }

    /// <summary>
    /// GrossProfitカラムの追加
    /// </summary>
    private async Task AddGrossProfitColumnsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        // SalesVouchersテーブル
        var salesGrossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
            AND name = 'GrossProfit'") > 0;

        if (!salesGrossProfitExists)
        {
            _logger.LogInformation("SalesVouchersテーブルにGrossProfitカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[SalesVouchers]
                ADD [GrossProfit] DECIMAL(16,4) NULL");
            _logger.LogInformation("GrossProfitカラムを追加しました。");
        }

        // PurchaseVouchersテーブル
        var purchaseGrossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVouchers]') 
            AND name = 'GrossProfit'") > 0;

        if (!purchaseGrossProfitExists)
        {
            _logger.LogInformation("PurchaseVouchersテーブルにGrossProfitカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[PurchaseVouchers]
                ADD [GrossProfit] DECIMAL(16,4) NULL");
        }

        // InventoryAdjustmentsテーブル
        var adjustmentGrossProfitExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') 
            AND name = 'GrossProfit'") > 0;

        if (!adjustmentGrossProfitExists)
        {
            _logger.LogInformation("InventoryAdjustmentsテーブルにGrossProfitカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[InventoryAdjustments]
                ADD [GrossProfit] DECIMAL(16,4) NULL");
        }
    }

    /// <summary>
    /// VoucherIdカラムのサイズ更新
    /// </summary>
    private async Task UpdateVoucherIdSizeAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        // 現在のVoucherIdのサイズを確認
        var currentSize = await connection.ExecuteScalarAsync<int>(@"
            SELECT max_length 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
            AND name = 'VoucherId'");

        // NVARCHAR(50) = 100バイト、NVARCHAR(100) = 200バイト
        if (currentSize == 100) // 現在50文字
        {
            // データが存在するか確認
            var hasData = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM SalesVouchers") > 0;

            if (!hasData)
            {
                _logger.LogInformation("VoucherIdカラムのサイズを100文字に拡張します...");
                
                // 主キー制約を一時的に削除
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] 
                    DROP CONSTRAINT PK_SalesVouchers");

                // カラムサイズを変更
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] 
                    ALTER COLUMN VoucherId NVARCHAR(100) NOT NULL");

                // 主キー制約を再作成
                await connection.ExecuteAsync(@"
                    ALTER TABLE [dbo].[SalesVouchers] 
                    ADD CONSTRAINT PK_SalesVouchers PRIMARY KEY (VoucherId, LineNumber)");

                _logger.LogInformation("VoucherIdカラムのサイズを拡張しました。");
            }
            else
            {
                _logger.LogWarning("SalesVouchersテーブルにデータが存在するため、VoucherIdのサイズ変更をスキップします。");
            }
        }
    }

    /// <summary>
    /// ProductNameカラムの追加
    /// </summary>
    private async Task AddProductNameColumnAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        
        var productNameExists = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) 
            FROM sys.columns 
            WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
            AND name = 'ProductName'") > 0;

        if (!productNameExists)
        {
            _logger.LogInformation("SalesVouchersテーブルにProductNameカラムを追加します...");
            await connection.ExecuteAsync(@"
                ALTER TABLE [dbo].[SalesVouchers]
                ADD [ProductName] NVARCHAR(100) NULL");
            _logger.LogInformation("ProductNameカラムを追加しました。");
        }
    }
}