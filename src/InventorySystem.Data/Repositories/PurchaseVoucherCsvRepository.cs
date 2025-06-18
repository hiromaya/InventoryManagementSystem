using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 仕入伝票CSV取込用リポジトリ実装
/// </summary>
public class PurchaseVoucherCsvRepository : BaseRepository, IPurchaseVoucherRepository
{
    public PurchaseVoucherCsvRepository(string connectionString, ILogger<PurchaseVoucherCsvRepository> logger)
        : base(connectionString, logger)
    {
    }

    /// <summary>
    /// 仕入伝票データを一括挿入
    /// </summary>
    public async Task<int> BulkInsertAsync(IEnumerable<PurchaseVoucher> vouchers)
    {
        const string sql = @"
            INSERT INTO PurchaseVouchers (
                DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType, SupplierCode,
                SupplierName, ProductCode, ProductName, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, UnitPrice, Amount, ProductCategory1, ProductCategory2, ProductCategory3,
                IsExcluded, ExcludeReason, ImportedAt, CreatedAt, UpdatedAt
            ) VALUES (
                @DataSetId, @VoucherNumber, @VoucherDate, @JobDate, @VoucherType, @DetailType, @SupplierCode,
                @SupplierName, @ProductCode, @ProductName, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @UnitPrice, @Amount, @ProductCategory1, @ProductCategory2, @ProductCategory3,
                @IsExcluded, @ExcludeReason, @ImportedAt, @CreatedAt, @UpdatedAt
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var now = DateTime.Now;
            
            var parameters = vouchers.Select(voucher => new
            {
                voucher.DataSetId,
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.JobDate,
                voucher.VoucherType,
                voucher.DetailType,
                voucher.SupplierCode,
                voucher.SupplierName,
                voucher.ProductCode,
                voucher.ProductName,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ShippingMarkName,
                voucher.Quantity,
                voucher.UnitPrice,
                voucher.Amount,
                voucher.ProductCategory1,
                voucher.ProductCategory2,
                voucher.ProductCategory3,
                voucher.IsExcluded,
                voucher.ExcludeReason,
                ImportedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });

            var insertedCount = await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("仕入伝票データ一括挿入完了: {Count}件", insertedCount);
            return insertedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仕入伝票データ一括挿入エラー");
            throw;
        }
    }

    /// <summary>
    /// データセットIDで仕入伝票データを取得
    /// </summary>
    public async Task<IEnumerable<PurchaseVoucher>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT Id, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType, SupplierCode,
                   SupplierName, ProductCode, ProductName, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                   Quantity, UnitPrice, Amount, ProductCategory1, ProductCategory2, ProductCategory3,
                   IsExcluded, ExcludeReason, ImportedAt, CreatedAt, UpdatedAt
            FROM PurchaseVouchers 
            WHERE DataSetId = @DataSetId
            ORDER BY VoucherNumber, ProductCode";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<PurchaseVoucher>(sql, new { DataSetId = dataSetId });
            
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット別仕入伝票データ取得エラー: {DataSetId}", dataSetId);
            throw;
        }
    }

    /// <summary>
    /// ジョブ日付で仕入伝票データを取得
    /// </summary>
    public async Task<IEnumerable<PurchaseVoucher>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT Id, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType, SupplierCode,
                   SupplierName, ProductCode, ProductName, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                   Quantity, UnitPrice, Amount, ProductCategory1, ProductCategory2, ProductCategory3,
                   IsExcluded, ExcludeReason, ImportedAt, CreatedAt, UpdatedAt
            FROM PurchaseVouchers 
            WHERE JobDate = @JobDate
            ORDER BY VoucherNumber, ProductCode";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<PurchaseVoucher>(sql, new { JobDate = jobDate.Date });
            
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブ日付別仕入伝票データ取得エラー: {JobDate}", jobDate);
            throw;
        }
    }

    // 以下は互換性のためのメソッド（旧インターフェース対応）
    public async Task<int> CreateAsync(PurchaseVoucher voucher)
    {
        return await BulkInsertAsync(new[] { voucher });
    }

    public async Task UpdateAsync(PurchaseVoucher voucher)
    {
        const string sql = @"
            UPDATE PurchaseVouchers 
            SET VoucherNumber = @VoucherNumber,
                VoucherDate = @VoucherDate,
                JobDate = @JobDate,
                VoucherType = @VoucherType,
                DetailType = @DetailType,
                SupplierCode = @SupplierCode,
                SupplierName = @SupplierName,
                ProductCode = @ProductCode,
                ProductName = @ProductName,
                GradeCode = @GradeCode,
                ClassCode = @ClassCode,
                ShippingMarkCode = @ShippingMarkCode,
                ShippingMarkName = @ShippingMarkName,
                Quantity = @Quantity,
                UnitPrice = @UnitPrice,
                Amount = @Amount,
                ProductCategory1 = @ProductCategory1,
                ProductCategory2 = @ProductCategory2,
                ProductCategory3 = @ProductCategory3,
                IsExcluded = @IsExcluded,
                ExcludeReason = @ExcludeReason,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var parameters = new
            {
                voucher.Id,
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.JobDate,
                voucher.VoucherType,
                voucher.DetailType,
                voucher.SupplierCode,
                voucher.SupplierName,
                voucher.ProductCode,
                voucher.ProductName,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ShippingMarkName,
                voucher.Quantity,
                voucher.UnitPrice,
                voucher.Amount,
                voucher.ProductCategory1,
                voucher.ProductCategory2,
                voucher.ProductCategory3,
                voucher.IsExcluded,
                voucher.ExcludeReason,
                UpdatedAt = DateTime.Now
            };

            await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("仕入伝票データ更新完了: {Id}", voucher.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仕入伝票データ更新エラー: {Id}", voucher.Id);
            throw;
        }
    }

    public async Task DeleteAsync(long id)
    {
        const string sql = "DELETE FROM PurchaseVouchers WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, new { Id = id });
            
            _logger.LogInformation("仕入伝票データ削除完了: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仕入伝票データ削除エラー: {Id}", id);
            throw;
        }
    }
}