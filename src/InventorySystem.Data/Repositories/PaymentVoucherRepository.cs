using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 支払伝票リポジトリ実装
/// </summary>
public class PaymentVoucherRepository : BaseRepository, IPaymentVoucherRepository
{
    public PaymentVoucherRepository(string connectionString, ILogger<PaymentVoucherRepository> logger)
        : base(connectionString, logger)
    {
    }

    public async Task<IEnumerable<PaymentVoucher>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT 
                Id, VoucherDate, VoucherNumber, SupplierCode, SupplierName,
                PayeeCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, TransferFeeBearer,
                CorporateBankCode, TransferBankCode, TransferBranchCode,
                TransferAccountType, TransferAccountNumber, TransferDesignation,
                Remarks, DataSetId, CreatedAt, UpdatedAt
            FROM PaymentVouchers 
            WHERE DataSetId = @DataSetId
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<PaymentVoucher>(sql, new { DataSetId = dataSetId });
            LogDebug($"Retrieved {vouchers.Count()} payment vouchers for DataSetId {dataSetId}");
            return vouchers;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByDataSetIdAsync), new { DataSetId = dataSetId });
            throw;
        }
    }

    public async Task<IEnumerable<PaymentVoucher>> GetByJobDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT 
                Id, VoucherDate, VoucherNumber, SupplierCode, SupplierName,
                PayeeCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, TransferFeeBearer,
                CorporateBankCode, TransferBankCode, TransferBranchCode,
                TransferAccountType, TransferAccountNumber, TransferDesignation,
                Remarks, DataSetId, CreatedAt, UpdatedAt
            FROM PaymentVouchers 
            WHERE JobDate >= @StartDate AND JobDate <= @EndDate
            ORDER BY JobDate, VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<PaymentVoucher>(sql, new { StartDate = startDate, EndDate = endDate });
            LogDebug($"Retrieved {vouchers.Count()} payment vouchers for date range {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            return vouchers;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByJobDateRangeAsync), new { StartDate = startDate, EndDate = endDate });
            throw;
        }
    }

    public async Task<IEnumerable<PaymentVoucher>> GetBySupplierCodeAsync(string supplierCode)
    {
        const string sql = @"
            SELECT 
                Id, VoucherDate, VoucherNumber, SupplierCode, SupplierName,
                PayeeCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, TransferFeeBearer,
                CorporateBankCode, TransferBankCode, TransferBranchCode,
                TransferAccountType, TransferAccountNumber, TransferDesignation,
                Remarks, DataSetId, CreatedAt, UpdatedAt
            FROM PaymentVouchers 
            WHERE SupplierCode = @SupplierCode
            ORDER BY JobDate DESC, VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<PaymentVoucher>(sql, new { SupplierCode = supplierCode });
            LogDebug($"Retrieved {vouchers.Count()} payment vouchers for supplier {supplierCode}");
            return vouchers;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetBySupplierCodeAsync), new { SupplierCode = supplierCode });
            throw;
        }
    }

    public async Task<PaymentVoucher?> GetByVoucherNumberAsync(string voucherNumber)
    {
        const string sql = @"
            SELECT 
                Id, VoucherDate, VoucherNumber, SupplierCode, SupplierName,
                PayeeCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, TransferFeeBearer,
                CorporateBankCode, TransferBankCode, TransferBranchCode,
                TransferAccountType, TransferAccountNumber, TransferDesignation,
                Remarks, DataSetId, CreatedAt, UpdatedAt
            FROM PaymentVouchers 
            WHERE VoucherNumber = @VoucherNumber";

        try
        {
            using var connection = CreateConnection();
            var voucher = await connection.QuerySingleOrDefaultAsync<PaymentVoucher>(sql, new { VoucherNumber = voucherNumber });
            LogDebug($"Retrieved payment voucher for number {voucherNumber}: {(voucher != null ? "Found" : "Not found")}");
            return voucher;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByVoucherNumberAsync), new { VoucherNumber = voucherNumber });
            throw;
        }
    }

    public async Task<int> InsertBulkAsync(IEnumerable<PaymentVoucher> vouchers)
    {
        var vouchersList = vouchers.ToList();
        if (!vouchersList.Any()) return 0;

        const string sql = @"
            INSERT INTO PaymentVouchers (
                VoucherDate, VoucherNumber, SupplierCode, SupplierName,
                PayeeCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, TransferFeeBearer,
                CorporateBankCode, TransferBankCode, TransferBranchCode,
                TransferAccountType, TransferAccountNumber, TransferDesignation,
                Remarks, DataSetId, CreatedAt, UpdatedAt
            ) VALUES (
                @VoucherDate, @VoucherNumber, @SupplierCode, @SupplierName,
                @PayeeCode, @JobDate, @LineNumber, @PaymentType, @OffsetCode,
                @Amount, @BillDueDate, @BillNumber, @TransferFeeBearer,
                @CorporateBankCode, @TransferBankCode, @TransferBranchCode,
                @TransferAccountType, @TransferAccountNumber, @TransferDesignation,
                @Remarks, @DataSetId, @CreatedAt, @UpdatedAt
            )";

        try
        {
            return await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                var affected = await connection.ExecuteAsync(sql, vouchersList, transaction);
                LogInfo($"Bulk inserted {affected} payment vouchers");
                return affected;
            });
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(InsertBulkAsync), new { Count = vouchersList.Count });
            throw;
        }
    }

    public async Task<int> DeleteByJobDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            DELETE FROM PaymentVouchers 
            WHERE JobDate >= @StartDate AND JobDate <= @EndDate";

        try
        {
            using var connection = CreateConnection();
            var affected = await connection.ExecuteAsync(sql, new { StartDate = startDate, EndDate = endDate });
            LogInfo($"Deleted {affected} payment vouchers for date range {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            return affected;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeleteByJobDateRangeAsync), new { StartDate = startDate, EndDate = endDate });
            throw;
        }
    }

    public async Task<int> DeleteByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            DELETE FROM PaymentVouchers 
            WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = CreateConnection();
            var affected = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
            LogInfo($"Deleted {affected} payment vouchers for DataSetId {dataSetId}");
            return affected;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeleteByDataSetIdAsync), new { DataSetId = dataSetId });
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string voucherNumber, int lineNumber)
    {
        const string sql = @"
            SELECT COUNT(1) 
            FROM PaymentVouchers 
            WHERE VoucherNumber = @VoucherNumber AND LineNumber = @LineNumber";

        try
        {
            using var connection = CreateConnection();
            var count = await connection.QuerySingleAsync<int>(sql, new { VoucherNumber = voucherNumber, LineNumber = lineNumber });
            var exists = count > 0;
            LogDebug($"Payment voucher exists check for {voucherNumber}-{lineNumber}: {exists}");
            return exists;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(ExistsAsync), new { VoucherNumber = voucherNumber, LineNumber = lineNumber });
            throw;
        }
    }

    public async Task<decimal> GetTotalAmountByPeriodAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT ISNULL(SUM(Amount), 0) 
            FROM PaymentVouchers 
            WHERE JobDate >= @StartDate AND JobDate <= @EndDate";

        try
        {
            using var connection = CreateConnection();
            var total = await connection.QuerySingleAsync<decimal>(sql, new { StartDate = startDate, EndDate = endDate });
            LogDebug($"Total amount for period {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}: {total:C}");
            return total;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetTotalAmountByPeriodAsync), new { StartDate = startDate, EndDate = endDate });
            throw;
        }
    }

    public async Task<Dictionary<string, decimal>> GetTotalAmountBySupplierAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT SupplierCode, SUM(Amount) as TotalAmount
            FROM PaymentVouchers 
            WHERE JobDate >= @StartDate AND JobDate <= @EndDate
            GROUP BY SupplierCode";

        try
        {
            using var connection = CreateConnection();
            var results = await connection.QueryAsync<dynamic>(sql, new { StartDate = startDate, EndDate = endDate });
            var totals = results.ToDictionary(r => (string)r.SupplierCode, r => (decimal)r.TotalAmount);
            LogDebug($"Retrieved total amounts by supplier for period {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}: {totals.Count} suppliers");
            return totals;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetTotalAmountBySupplierAsync), new { StartDate = startDate, EndDate = endDate });
            throw;
        }
    }
}