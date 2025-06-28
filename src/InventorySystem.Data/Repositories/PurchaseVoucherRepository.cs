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
                ShippingMarkName,
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
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, UnitPrice, Amount, DataSetId, VoucherType,
                SupplierCode, DetailType
            ) VALUES (
                @VoucherNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
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
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, UnitPrice, Amount, DataSetId, VoucherType,
                SupplierCode, DetailType
            ) VALUES (
                @VoucherNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
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
            ShippingMarkName = row.ShippingMarkName?.ToString() ?? string.Empty,
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
            voucher.ShippingMarkName,
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
}