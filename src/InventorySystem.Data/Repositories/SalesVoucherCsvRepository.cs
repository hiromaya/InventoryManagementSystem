using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 売上伝票CSV取込用リポジトリ実装
/// </summary>
public class SalesVoucherCsvRepository : BaseRepository, ISalesVoucherRepository
{
    public SalesVoucherCsvRepository(string connectionString, ILogger<SalesVoucherCsvRepository> logger)
        : base(connectionString, logger)
    {
    }

    /// <summary>
    /// 売上伝票データを一括挿入
    /// </summary>
    public async Task<int> BulkInsertAsync(IEnumerable<SalesVoucher> vouchers)
    {
        const string sql = @"
            INSERT INTO SalesVouchers (
                VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                CustomerCode, CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, UnitPrice, Amount, InventoryUnitPrice, CreatedDate, DataSetId
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherNumber, @VoucherDate, @JobDate, @VoucherType, @DetailType,
                @CustomerCode, @CustomerName, @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @UnitPrice, @Amount, @InventoryUnitPrice, @CreatedDate, @DataSetId
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var now = DateTime.Now;
            
            var parameters = vouchers.Select((voucher, index) => new
            {
                VoucherId = $"{voucher.DataSetId}_{voucher.VoucherNumber}",
                LineNumber = index + 1,
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.JobDate,
                voucher.VoucherType,
                voucher.DetailType,
                voucher.CustomerCode,
                voucher.CustomerName,
                voucher.ProductCode,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ShippingMarkName,
                voucher.Quantity,
                voucher.UnitPrice,
                voucher.Amount,
                InventoryUnitPrice = 0m,  // デフォルト値
                CreatedDate = now,
                voucher.DataSetId
            });

            var insertedCount = await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("売上伝票データ一括挿入完了: {Count}件", insertedCount);
            return insertedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "売上伝票データ一括挿入エラー");
            throw;
        }
    }

    /// <summary>
    /// データセットIDで売上伝票データを取得
    /// </summary>
    public async Task<IEnumerable<SalesVoucher>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType, CustomerCode,
                   CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                   Quantity, UnitPrice, Amount, InventoryUnitPrice, CreatedDate
            FROM SalesVouchers 
            WHERE DataSetId = @DataSetId
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<SalesVoucher>(sql, new { DataSetId = dataSetId });
            
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット別売上伝票データ取得エラー: {DataSetId}", dataSetId);
            throw;
        }
    }

    /// <summary>
    /// ジョブ日付で売上伝票データを取得
    /// </summary>
    public async Task<IEnumerable<SalesVoucher>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType, CustomerCode,
                   CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                   Quantity, UnitPrice, Amount, InventoryUnitPrice, CreatedDate
            FROM SalesVouchers 
            WHERE JobDate = @JobDate
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<SalesVoucher>(sql, new { JobDate = jobDate.Date });
            
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブ日付別売上伝票データ取得エラー: {JobDate}", jobDate);
            throw;
        }
    }

    // 以下は互換性のためのメソッド（旧インターフェース対応）
    public async Task<int> CreateAsync(SalesVoucher voucher)
    {
        return await BulkInsertAsync(new[] { voucher });
    }

    public async Task UpdateAsync(SalesVoucher voucher)
    {
        const string sql = @"
            UPDATE SalesVouchers 
            SET VoucherNumber = @VoucherNumber,
                VoucherDate = @VoucherDate,
                JobDate = @JobDate,
                VoucherType = @VoucherType,
                DetailType = @DetailType,
                CustomerCode = @CustomerCode,
                CustomerName = @CustomerName,
                ProductCode = @ProductCode,
                GradeCode = @GradeCode,
                ClassCode = @ClassCode,
                ShippingMarkCode = @ShippingMarkCode,
                ShippingMarkName = @ShippingMarkName,
                Quantity = @Quantity,
                UnitPrice = @UnitPrice,
                Amount = @Amount,
                InventoryUnitPrice = @InventoryUnitPrice
            WHERE VoucherId = @VoucherId AND LineNumber = @LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var parameters = new
            {
                voucher.VoucherId,
                voucher.LineNumber,
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.JobDate,
                voucher.VoucherType,
                voucher.DetailType,
                voucher.CustomerCode,
                voucher.CustomerName,
                voucher.ProductCode,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ShippingMarkName,
                voucher.Quantity,
                voucher.UnitPrice,
                voucher.Amount,
                voucher.InventoryUnitPrice
            };

            await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("売上伝票データ更新完了: {VoucherId}-{LineNumber}", voucher.VoucherId, voucher.LineNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "売上伝票データ更新エラー: {VoucherId}-{LineNumber}", voucher.VoucherId, voucher.LineNumber);
            throw;
        }
    }

    public async Task DeleteAsync(long id)
    {
        // Note: This method signature uses long id for compatibility, but we need VoucherId and LineNumber
        // This is a limitation of the interface - consider updating interface to accept composite key
        const string sql = "DELETE FROM SalesVouchers WHERE VoucherId = @VoucherId AND LineNumber = @LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, new { Id = id });
            
            _logger.LogInformation("売上伝票データ削除完了: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "売上伝票データ削除エラー: {Id}", id);
            throw;
        }
    }
}