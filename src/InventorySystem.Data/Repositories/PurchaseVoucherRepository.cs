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
                VoucherId, LineNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, PurchaseUnitPrice, PurchaseAmount, DataSetId
            FROM PurchaseVouchers 
            WHERE JobDate = @JobDate
            ORDER BY VoucherId, LineNumber";

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
                VoucherId, LineNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, PurchaseUnitPrice, PurchaseAmount, DataSetId
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @PurchaseUnitPrice, @PurchaseAmount, @DataSetId
            )";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, MapFromPurchaseVoucher(voucher));
            
            LogInfo($"Created purchase voucher record", new { voucher.VoucherId, voucher.LineNumber });
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
                VoucherId, LineNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, PurchaseUnitPrice, PurchaseAmount, DataSetId
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @PurchaseUnitPrice, @PurchaseAmount, @DataSetId
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
            VoucherId = row.VoucherId,
            LineNumber = row.LineNumber,
            VoucherDate = row.VoucherDate,
            JobDate = row.JobDate,
            InventoryKey = new InventoryKey
            {
                ProductCode = row.ProductCode,
                GradeCode = row.GradeCode,
                ClassCode = row.ClassCode,
                ShippingMarkCode = row.ShippingMarkCode,
                ShippingMarkName = row.ShippingMarkName
            },
            Quantity = row.Quantity,
            UnitPrice = row.UnitPrice,
            Amount = row.Amount,
            DataSetId = row.DataSetId
        };
    }

    private static object MapFromPurchaseVoucher(PurchaseVoucher voucher)
    {
        return new
        {
            voucher.VoucherId,
            voucher.LineNumber,
            voucher.VoucherDate,
            voucher.JobDate,
            ProductCode = voucher.InventoryKey.ProductCode,
            GradeCode = voucher.InventoryKey.GradeCode,
            ClassCode = voucher.InventoryKey.ClassCode,
            ShippingMarkCode = voucher.InventoryKey.ShippingMarkCode,
            ShippingMarkName = voucher.InventoryKey.ShippingMarkName,
            voucher.Quantity,
            voucher.UnitPrice,
            voucher.Amount,
            voucher.DataSetId
        };
    }
}