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
                VoucherId, LineNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, SalesUnitPrice, SalesAmount, InventoryUnitPrice, DataSetId
            FROM SalesVoucher 
            WHERE JobDate = @JobDate
            ORDER BY VoucherId, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { JobDate = jobDate });
            
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
            INSERT INTO SalesVoucher (
                VoucherId, LineNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, SalesUnitPrice, SalesAmount, InventoryUnitPrice, DataSetId
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @SalesUnitPrice, @SalesAmount, @InventoryUnitPrice, @DataSetId
            )";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, MapFromSalesVoucher(voucher));
            
            LogInfo($"Created sales voucher record", new { voucher.VoucherId, voucher.LineNumber });
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
            INSERT INTO SalesVoucher (
                VoucherId, LineNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, SalesUnitPrice, SalesAmount, InventoryUnitPrice, DataSetId
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @SalesUnitPrice, @SalesAmount, @InventoryUnitPrice, @DataSetId
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
            InventoryUnitPrice = row.InventoryUnitPrice,
            DataSetId = row.DataSetId
        };
    }

    private static object MapFromSalesVoucher(SalesVoucher voucher)
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
            voucher.InventoryUnitPrice,
            voucher.DataSetId
        };
    }
}