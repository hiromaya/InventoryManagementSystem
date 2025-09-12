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
                VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                SupplierCode, SupplierName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                Quantity, UnitPrice, Amount, CreatedDate, DataSetId
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherNumber, @VoucherDate, @JobDate, @VoucherType, @DetailType,
                @SupplierCode, @SupplierName, @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ManualShippingMark,
                @Quantity, @UnitPrice, @Amount, @CreatedDate, @DataSetId
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var now = DateTime.Now;
            
            // LineNumberはすでにToEntityで設定されているため、そのまま使用
            var parameters = vouchers.Select(voucher => new
            {
                voucher.VoucherId,  // すでに正しく設定されている
                voucher.LineNumber, // すでに正しく設定されている
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.JobDate,
                voucher.VoucherType,
                voucher.DetailType,
                voucher.SupplierCode,
                voucher.SupplierName,
                voucher.ProductCode,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ManualShippingMark,
                voucher.Quantity,
                voucher.UnitPrice,
                voucher.Amount,
                CreatedDate = now,
                voucher.DataSetId
            }).ToList();

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
            SELECT VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   SupplierCode, SupplierName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate, DataSetId
            FROM PurchaseVouchers 
            WHERE DataSetId = @DataSetId
            ORDER BY VoucherNumber, LineNumber";

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
            SELECT VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   SupplierCode, SupplierName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate, DataSetId
            FROM PurchaseVouchers 
            WHERE JobDate = @JobDate
            ORDER BY VoucherNumber, LineNumber";

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
                GradeCode = @GradeCode,
                ClassCode = @ClassCode,
                ShippingMarkCode = @ShippingMarkCode,
                ManualShippingMark = @ManualShippingMark,
                Quantity = @Quantity,
                UnitPrice = @UnitPrice,
                Amount = @Amount
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
                voucher.SupplierCode,
                voucher.SupplierName,
                voucher.ProductCode,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ManualShippingMark,
                voucher.Quantity,
                voucher.UnitPrice,
                voucher.Amount
            };

            await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("仕入伝票データ更新完了: {VoucherId}-{LineNumber}", voucher.VoucherId, voucher.LineNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仕入伝票データ更新エラー: {VoucherId}-{LineNumber}", voucher.VoucherId, voucher.LineNumber);
            throw;
        }
    }

    public async Task DeleteAsync(long id)
    {
        // Note: This method signature uses long id for compatibility, but we need VoucherId and LineNumber
        // This is a limitation of the interface - consider updating interface to accept composite key
        const string sql = "DELETE FROM PurchaseVouchers WHERE VoucherId = @VoucherId AND LineNumber = @LineNumber";

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

    public async Task<int> DeleteByJobDateAsync(DateTime jobDate)
    {
        const string sql = "DELETE FROM PurchaseVouchers WHERE JobDate = @JobDate";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            LogInfo($"Deleted {result} purchase voucher records", new { jobDate });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeleteByJobDateAsync), new { jobDate });
            throw;
        }
    }
    
    public async Task<int> GetCountAsync(DateTime jobDate)
    {
        const string sql = "SELECT COUNT(*) FROM PurchaseVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    public async Task<int> GetCountByJobDateAsync(DateTime jobDate)
    {
        const string sql = "SELECT COUNT(*) FROM PurchaseVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    public async Task<decimal> GetTotalAmountAsync(DateTime jobDate)
    {
        const string sql = "SELECT ISNULL(SUM(Amount), 0) FROM PurchaseVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(sql, new { jobDate });
    }
    
    public async Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM PurchaseVouchers 
            WHERE JobDate = @jobDate 
            AND CreatedDate > @modifiedAfter";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate, modifiedAfter });
    }

    public async Task<string?> GetDataSetIdByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT TOP 1 DataSetId 
            FROM PurchaseVouchers 
            WHERE JobDate = @jobDate 
            AND DataSetId IS NOT NULL";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { jobDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブ日付でDataSetId取得エラー: {JobDate}", jobDate);
            throw;
        }
    }
    
    /// <summary>
    /// すべての仕入伝票データを取得
    /// </summary>
    public async Task<IEnumerable<PurchaseVoucher>> GetAllAsync()
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   SupplierCode, SupplierName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate, DataSetId
            FROM PurchaseVouchers
            ORDER BY JobDate DESC, VoucherDate DESC, VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<PurchaseVoucher>(sql);
            
            _logger.LogInformation("すべての仕入伝票データを取得しました: {Count}件", vouchers.Count());
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "すべての仕入伝票データの取得中にエラーが発生しました");
            throw;
        }
    }

    /// <summary>
    /// 指定されたDataSetIdの伝票データのIsActiveフラグを更新
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="isActive">アクティブフラグの値</param>
    /// <returns>更新件数</returns>
    public async Task<int> UpdateIsActiveByDataSetIdAsync(string dataSetId, bool isActive)
    {
        const string sql = @"
            UPDATE PurchaseVouchers 
            SET IsActive = @IsActive, 
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, IsActive = isActive });
            
            _logger.LogInformation("Updated {Count} purchase vouchers IsActive flag (CSV repository)", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSVリポジトリでの仕入伝票IsActiveフラグ更新エラー: DataSetId={DataSetId}, IsActive={IsActive}", dataSetId, isActive);
            throw;
        }
    }

    /// <summary>
    /// アクティブな伝票のみを取得（IsActive = true）
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>アクティブな仕入伝票一覧</returns>
    public async Task<IEnumerable<PurchaseVoucher>> GetActiveByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   SupplierCode, SupplierName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate, DataSetId, IsActive
            FROM PurchaseVouchers 
            WHERE JobDate = @jobDate AND IsActive = 1
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<PurchaseVoucher>(sql, new { jobDate = jobDate.Date });
            
            var results = vouchers.ToList();
            _logger.LogInformation("Retrieved {Count} active purchase vouchers (CSV repository)", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSVリポジトリでのアクティブ仕入伝票データ取得エラー: JobDate={JobDate}", jobDate);
            throw;
        }
    }

    /// <summary>
    /// 指定されたJobDateの伝票データを無効化
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <param name="excludeDataSetId">除外するDataSetId（nullの場合は除外しない）</param>
    /// <returns>無効化件数</returns>
    public async Task<int> DeactivateByJobDateAsync(DateTime jobDate, string? excludeDataSetId = null)
    {
        string sql = @"
            UPDATE PurchaseVouchers 
            SET IsActive = 0, 
                UpdatedDate = GETDATE()
            WHERE JobDate = @JobDate";

        object parameters;
        
        if (!string.IsNullOrEmpty(excludeDataSetId))
        {
            sql += " AND DataSetId != @ExcludeDataSetId";
            parameters = new { JobDate = jobDate, ExcludeDataSetId = excludeDataSetId };
        }
        else
        {
            parameters = new { JobDate = jobDate };
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("Deactivated {Count} purchase vouchers (CSV repository)", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSVリポジトリでの仕入伝票無効化エラー: JobDate={JobDate}, ExcludeDataSetId={ExcludeDataSetId}", jobDate, excludeDataSetId);
            throw;
        }
    }
}