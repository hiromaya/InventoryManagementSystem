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
                伝票番号 as VoucherId,
                伝票日付 as VoucherDate,
                伝票区分 as VoucherType,
                得意先コード as CustomerCode,
                商品コード as ProductCode,
                等級コード as GradeCode,
                階級コード as ClassCode,
                荷印コード as ShippingMarkCode,
                荷印名 as ShippingMarkName,
                数量 as Quantity,
                単価 as SalesUnitPrice,
                金額 as SalesAmount,
                ジョブデート as JobDate,
                明細種 as DetailType,
                明細行 as LineNumber,
                データセットID as DataSetId
            FROM SalesVouchers
            WHERE ジョブデート = @jobDate
            ORDER BY VoucherId, LineNumber";

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
                伝票番号, 明細行, 伝票日付, ジョブデート,
                商品コード, 等級コード, 階級コード, 荷印コード, 荷印名,
                数量, 単価, 金額, データセットID, 伝票区分,
                得意先コード, 明細種
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @SalesUnitPrice, @SalesAmount, @DataSetId, @VoucherType,
                @CustomerCode, @DetailType
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
            INSERT INTO SalesVouchers (
                伝票番号, 明細行, 伝票日付, ジョブデート,
                商品コード, 等級コード, 階級コード, 荷印コード, 荷印名,
                数量, 単価, 金額, データセットID, 伝票区分,
                得意先コード, 明細種
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @SalesUnitPrice, @SalesAmount, @DataSetId, @VoucherType,
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
            VoucherNumber = row.VoucherId?.ToString() ?? string.Empty,
            LineNumber = row.LineNumber ?? 0,
            VoucherDate = row.VoucherDate,
            JobDate = row.JobDate,
            VoucherType = row.VoucherType?.ToString() ?? string.Empty,
            DetailType = row.DetailType?.ToString() ?? string.Empty,
            CustomerCode = row.CustomerCode?.ToString(),
            ProductCode = row.ProductCode?.ToString() ?? string.Empty,
            GradeCode = row.GradeCode?.ToString() ?? string.Empty,
            ClassCode = row.ClassCode?.ToString() ?? string.Empty,
            ShippingMarkCode = row.ShippingMarkCode?.ToString() ?? string.Empty,
            ShippingMarkName = row.ShippingMarkName?.ToString() ?? string.Empty,
            Quantity = row.Quantity ?? 0m,
            UnitPrice = row.SalesUnitPrice ?? 0m,
            Amount = row.SalesAmount ?? 0m,
            DataSetId = row.DataSetId?.ToString() ?? string.Empty
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
            ProductCode = voucher.ProductCode,
            GradeCode = voucher.GradeCode,
            ClassCode = voucher.ClassCode,
            ShippingMarkCode = voucher.ShippingMarkCode,
            ShippingMarkName = voucher.ShippingMarkName,
            voucher.Quantity,
            SalesUnitPrice = voucher.UnitPrice,
            SalesAmount = voucher.Amount,
            voucher.DataSetId,
            voucher.VoucherType,
            voucher.CustomerCode,
            voucher.DetailType
        };
    }
}