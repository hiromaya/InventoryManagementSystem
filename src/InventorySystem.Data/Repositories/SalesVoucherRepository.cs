using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

public class SalesVoucherRepository : BaseRepository, ISalesVoucherRepository
{
    public SalesVoucherRepository(string connectionString, ILogger<SalesVoucherRepository> logger)
        : base(connectionString, logger)
    {
    }

    public async Task<IEnumerable<SalesVoucher>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                CustomerCode,
                CustomerName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                Quantity,
                UnitPrice as SalesUnitPrice,
                Amount as SalesAmount,
                InventoryUnitPrice,
                JobDate,
                DetailType,
                DataSetId
            FROM SalesVouchers
            WHERE JobDate = @jobDate
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { jobDate });
            
            return vouchers.Select(MapToSalesVoucher);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByJobDateAsync), new { jobDate });
            throw;
        }
    }

    public async Task<int> CreateAsync(SalesVoucher voucher)
    {
        const string sql = @"
            INSERT INTO SalesVouchers (
                VoucherNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, UnitPrice, Amount, DataSetId, VoucherType,
                CustomerCode, DetailType
            ) VALUES (
                @VoucherNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @UnitPrice, @Amount, @DataSetId, @VoucherType,
                @CustomerCode, @DetailType
            )";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, MapFromSalesVoucher(voucher));
            
            LogInfo($"Created sales voucher record", new { voucher.VoucherId });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(CreateAsync), voucher);
            throw;
        }
    }

    public async Task<int> BulkInsertAsync(IEnumerable<SalesVoucher> vouchers)
    {
        const string sql = @"
            INSERT INTO SalesVouchers (
                VoucherNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, UnitPrice, Amount, DataSetId, VoucherType,
                CustomerCode, DetailType
            ) VALUES (
                @VoucherNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @UnitPrice, @Amount, @DataSetId, @VoucherType,
                @CustomerCode, @DetailType
            )";

        try
        {
            using var connection = CreateConnection();
            var parameters = vouchers.Select(MapFromSalesVoucher);
            var result = await connection.ExecuteAsync(sql, parameters);
            
            LogInfo($"Bulk inserted {result} sales voucher records");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(BulkInsertAsync), new { Count = vouchers.Count() });
            throw;
        }
    }

    private static SalesVoucher MapToSalesVoucher(dynamic row)
    {
        return new SalesVoucher
        {
            VoucherId = row.VoucherId?.ToString() ?? string.Empty,
            LineNumber = row.LineNumber ?? 0,
            VoucherNumber = row.VoucherNumber?.ToString() ?? string.Empty,
            VoucherDate = row.VoucherDate,
            JobDate = row.JobDate,
            VoucherType = row.VoucherType?.ToString() ?? string.Empty,
            DetailType = row.DetailType?.ToString() ?? string.Empty,
            CustomerCode = row.CustomerCode?.ToString(),
            CustomerName = row.CustomerName?.ToString(),
            ProductCode = row.ProductCode?.ToString() ?? string.Empty,
            GradeCode = row.GradeCode?.ToString() ?? string.Empty,
            ClassCode = row.ClassCode?.ToString() ?? string.Empty,
            ShippingMarkCode = row.ShippingMarkCode?.ToString() ?? string.Empty,
            ShippingMarkName = row.ShippingMarkName?.ToString() ?? string.Empty,
            Quantity = row.Quantity ?? 0m,
            UnitPrice = row.SalesUnitPrice ?? 0m,
            Amount = row.SalesAmount ?? 0m,
            InventoryUnitPrice = row.InventoryUnitPrice ?? 0m,
            DataSetId = row.DataSetId?.ToString() ?? string.Empty
        };
    }

    private static object MapFromSalesVoucher(SalesVoucher voucher)
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
            voucher.ShippingMarkName,
            voucher.Quantity,
            UnitPrice = voucher.UnitPrice,
            Amount = voucher.Amount,
            voucher.DataSetId,
            voucher.VoucherType,
            voucher.CustomerCode,
            voucher.DetailType
        };
    }

    public async Task<int> DeleteByJobDateAsync(DateTime jobDate)
    {
        const string sql = "DELETE FROM SalesVouchers WHERE JobDate = @JobDate";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            LogInfo($"Deleted {result} sales voucher records", new { jobDate });
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
        const string sql = "SELECT COUNT(*) FROM SalesVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    public async Task<int> GetCountByJobDateAsync(DateTime jobDate)
    {
        const string sql = "SELECT COUNT(*) FROM SalesVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    public async Task<decimal> GetTotalAmountAsync(DateTime jobDate)
    {
        const string sql = "SELECT ISNULL(SUM(Amount), 0) FROM SalesVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(sql, new { jobDate });
    }
    
    public async Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM SalesVouchers 
            WHERE JobDate = @jobDate 
            AND CreatedDate > @modifiedAfter";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate, modifiedAfter });
    }
    
    public async Task<IEnumerable<SalesVoucher>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                CustomerCode,
                CustomerName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                Quantity,
                UnitPrice as SalesUnitPrice,
                Amount as SalesAmount,
                InventoryUnitPrice,
                JobDate,
                DetailType,
                DataSetId
            FROM SalesVouchers
            WHERE DataSetId = @dataSetId
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { dataSetId });
            
            return vouchers.Select(MapToSalesVoucher);
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
            FROM SalesVouchers 
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

    public async Task<IEnumerable<SalesVoucher>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                CustomerCode,
                CustomerName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                Quantity,
                UnitPrice as SalesUnitPrice,
                Amount as SalesAmount,
                InventoryUnitPrice,
                JobDate,
                DetailType,
                DataSetId
            FROM SalesVouchers
            ORDER BY JobDate DESC, VoucherDate DESC, VoucherId DESC";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql);
            
            var results = vouchers.Select(MapToSalesVoucher).ToList();
            LogInfo($"Retrieved {results.Count} sales vouchers (all records)");
            return results;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetAllAsync));
            throw;
        }
    }
}