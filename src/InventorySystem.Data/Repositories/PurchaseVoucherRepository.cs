using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

public class PurchaseVoucherRepository : BaseRepository, IPurchaseVoucherRepository
{
    public PurchaseVoucherRepository(string connectionString, ILogger<PurchaseVoucherRepository> logger)
        : base(connectionString, logger)
    {
    }

    public async Task<IEnumerable<PurchaseVoucher>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                SupplierCode,
                SupplierName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ManualShippingMark,
                Quantity,
                UnitPrice as PurchaseUnitPrice,
                Amount as PurchaseAmount,
                JobDate,
                DetailType,
                DataSetId
            FROM PurchaseVouchers 
            WHERE JobDate = @JobDate
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { JobDate = jobDate });
            
            return vouchers.Select(MapToPurchaseVoucher);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByJobDateAsync), new { jobDate });
            throw;
        }
    }

    public async Task<int> CreateAsync(PurchaseVoucher voucher)
    {
        const string sql = @"
            INSERT INTO PurchaseVouchers (
                VoucherNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                Quantity, UnitPrice, Amount, DataSetId, VoucherType,
                SupplierCode, DetailType
            ) VALUES (
                @VoucherNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ManualShippingMark,
                @Quantity, @UnitPrice, @Amount, @DataSetId, @VoucherType,
                @SupplierCode, @DetailType
            )";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, MapFromPurchaseVoucher(voucher));
            
            LogInfo($"Created purchase voucher record", new { voucher.VoucherId });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(CreateAsync), voucher);
            throw;
        }
    }

    public async Task<int> BulkInsertAsync(IEnumerable<PurchaseVoucher> vouchers)
    {
        const string sql = @"
            INSERT INTO PurchaseVouchers (
                VoucherNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                Quantity, UnitPrice, Amount, DataSetId, VoucherType,
                SupplierCode, DetailType
            ) VALUES (
                @VoucherNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ManualShippingMark,
                @Quantity, @UnitPrice, @Amount, @DataSetId, @VoucherType,
                @SupplierCode, @DetailType
            )";

        try
        {
            using var connection = CreateConnection();
            var parameters = vouchers.Select(MapFromPurchaseVoucher);
            var result = await connection.ExecuteAsync(sql, parameters);
            
            LogInfo($"Bulk inserted {result} purchase voucher records");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(BulkInsertAsync), new { Count = vouchers.Count() });
            throw;
        }
    }

    private static PurchaseVoucher MapToPurchaseVoucher(dynamic row)
    {
        return new PurchaseVoucher
        {
            VoucherId = row.VoucherId?.ToString() ?? string.Empty,
            LineNumber = row.LineNumber ?? 0,
            VoucherNumber = row.VoucherNumber?.ToString() ?? string.Empty,
            VoucherDate = row.VoucherDate,
            JobDate = row.JobDate,
            VoucherType = row.VoucherType?.ToString() ?? string.Empty,
            DetailType = row.DetailType?.ToString() ?? string.Empty,
            SupplierCode = row.SupplierCode?.ToString(),
            SupplierName = row.SupplierName?.ToString(),
            ProductCode = row.ProductCode?.ToString() ?? string.Empty,
            GradeCode = row.GradeCode?.ToString() ?? string.Empty,
            ClassCode = row.ClassCode?.ToString() ?? string.Empty,
            ShippingMarkCode = row.ShippingMarkCode?.ToString() ?? string.Empty,
            ManualShippingMark = row.ManualShippingMark?.ToString() ?? string.Empty,
            Quantity = row.Quantity ?? 0m,
            UnitPrice = row.PurchaseUnitPrice ?? 0m,
            Amount = row.PurchaseAmount ?? 0m,
            DataSetId = row.DataSetId?.ToString() ?? string.Empty
        };
    }

    private static object MapFromPurchaseVoucher(PurchaseVoucher voucher)
    {
        return new
        {
            VoucherNumber = voucher.VoucherId,
            voucher.VoucherDate,
            voucher.JobDate,
            voucher.ProductCode,
            voucher.GradeCode,
            voucher.ClassCode,
            voucher.ShippingMarkCode,
            voucher.ManualShippingMark,
            voucher.Quantity,
            UnitPrice = voucher.UnitPrice,
            Amount = voucher.Amount,
            voucher.DataSetId,
            voucher.VoucherType,
            voucher.SupplierCode,
            voucher.DetailType
        };
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
    
    public async Task<IEnumerable<PurchaseVoucher>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                SupplierCode,
                SupplierName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ManualShippingMark,
                Quantity,
                UnitPrice as PurchaseUnitPrice,
                Amount as PurchaseAmount,
                JobDate,
                DetailType,
                DataSetId
            FROM PurchaseVouchers 
            WHERE DataSetId = @dataSetId
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { dataSetId });
            
            return vouchers.Select(MapToPurchaseVoucher);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByDataSetIdAsync), new { dataSetId });
            throw;
        }
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
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { jobDate });
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetDataSetIdByJobDateAsync), new { jobDate });
            throw;
        }
    }

    public async Task<IEnumerable<PurchaseVoucher>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                SupplierCode,
                SupplierName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ManualShippingMark,
                Quantity,
                UnitPrice as PurchaseUnitPrice,
                Amount as PurchaseAmount,
                JobDate,
                DetailType,
                DataSetId
            FROM PurchaseVouchers 
            ORDER BY JobDate DESC, VoucherDate DESC, VoucherId DESC";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql);
            
            var results = vouchers.Select(MapToPurchaseVoucher).ToList();
            LogInfo($"Retrieved {results.Count} purchase vouchers (all records)");
            return results;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetAllAsync));
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
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, IsActive = isActive });
            
            LogInfo($"Updated {result} purchase vouchers IsActive flag", new { dataSetId, isActive });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateIsActiveByDataSetIdAsync), new { dataSetId, isActive });
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
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                SupplierCode,
                SupplierName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ManualShippingMark,
                Quantity,
                UnitPrice as PurchaseUnitPrice,
                Amount as PurchaseAmount,
                JobDate,
                DetailType,
                DataSetId,
                IsActive
            FROM PurchaseVouchers 
            WHERE JobDate = @jobDate AND IsActive = 1
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { jobDate });
            
            var results = vouchers.Select(MapToPurchaseVoucher).ToList();
            LogInfo($"Retrieved {results.Count} active purchase vouchers", new { jobDate });
            return results;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetActiveByJobDateAsync), new { jobDate });
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
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, parameters);
            
            LogInfo($"Deactivated {result} purchase vouchers", new { jobDate, excludeDataSetId });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeactivateByJobDateAsync), new { jobDate, excludeDataSetId });
            throw;
        }
    }
}