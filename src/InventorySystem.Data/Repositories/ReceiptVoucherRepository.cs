using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 入金伝票リポジトリ実装
/// </summary>
public class ReceiptVoucherRepository : BaseRepository, IReceiptVoucherRepository
{
    public ReceiptVoucherRepository(string connectionString, ILogger<ReceiptVoucherRepository> logger)
        : base(connectionString, logger)
    {
    }

    public async Task<IEnumerable<ReceiptVoucher>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT 
                Id, VoucherDate, VoucherNumber, CustomerCode, CustomerName,
                BillingCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, CorporateBankCode,
                DepositAccountNumber, RemitterName, Remarks, DataSetId,
                CreatedAt, UpdatedAt
            FROM ReceiptVouchers 
            WHERE DataSetId = @DataSetId
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<ReceiptVoucher>(sql, new { DataSetId = dataSetId });
            LogDebug($"Retrieved {vouchers.Count()} receipt vouchers for DataSetId {dataSetId}");
            return vouchers;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByDataSetIdAsync), new { DataSetId = dataSetId });
            throw;
        }
    }

    public async Task<IEnumerable<ReceiptVoucher>> GetByJobDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT 
                Id, VoucherDate, VoucherNumber, CustomerCode, CustomerName,
                BillingCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, CorporateBankCode,
                DepositAccountNumber, RemitterName, Remarks, DataSetId,
                CreatedAt, UpdatedAt
            FROM ReceiptVouchers 
            WHERE JobDate >= @StartDate AND JobDate <= @EndDate
            ORDER BY JobDate, VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<ReceiptVoucher>(sql, new { StartDate = startDate, EndDate = endDate });
            LogDebug($"Retrieved {vouchers.Count()} receipt vouchers for date range {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            return vouchers;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByJobDateRangeAsync), new { StartDate = startDate, EndDate = endDate });
            throw;
        }
    }

    public async Task<IEnumerable<ReceiptVoucher>> GetByCustomerCodeAsync(string customerCode)
    {
        const string sql = @"
            SELECT 
                Id, VoucherDate, VoucherNumber, CustomerCode, CustomerName,
                BillingCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, CorporateBankCode,
                DepositAccountNumber, RemitterName, Remarks, DataSetId,
                CreatedAt, UpdatedAt
            FROM ReceiptVouchers 
            WHERE CustomerCode = @CustomerCode
            ORDER BY JobDate DESC, VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<ReceiptVoucher>(sql, new { CustomerCode = customerCode });
            LogDebug($"Retrieved {vouchers.Count()} receipt vouchers for customer {customerCode}");
            return vouchers;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByCustomerCodeAsync), new { CustomerCode = customerCode });
            throw;
        }
    }

    public async Task<ReceiptVoucher?> GetByVoucherNumberAsync(string voucherNumber)
    {
        const string sql = @"
            SELECT 
                Id, VoucherDate, VoucherNumber, CustomerCode, CustomerName,
                BillingCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, CorporateBankCode,
                DepositAccountNumber, RemitterName, Remarks, DataSetId,
                CreatedAt, UpdatedAt
            FROM ReceiptVouchers 
            WHERE VoucherNumber = @VoucherNumber";

        try
        {
            using var connection = CreateConnection();
            var voucher = await connection.QuerySingleOrDefaultAsync<ReceiptVoucher>(sql, new { VoucherNumber = voucherNumber });
            LogDebug($"Retrieved receipt voucher for number {voucherNumber}: {(voucher != null ? "Found" : "Not found")}");
            return voucher;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByVoucherNumberAsync), new { VoucherNumber = voucherNumber });
            throw;
        }
    }

    public async Task<int> InsertBulkAsync(IEnumerable<ReceiptVoucher> vouchers)
    {
        var vouchersList = vouchers.ToList();
        if (!vouchersList.Any()) return 0;

        const string sql = @"
            INSERT INTO ReceiptVouchers (
                VoucherDate, VoucherNumber, CustomerCode, CustomerName,
                BillingCode, JobDate, LineNumber, PaymentType, OffsetCode,
                Amount, BillDueDate, BillNumber, CorporateBankCode,
                DepositAccountNumber, RemitterName, Remarks, DataSetId,
                CreatedAt, UpdatedAt
            ) VALUES (
                @VoucherDate, @VoucherNumber, @CustomerCode, @CustomerName,
                @BillingCode, @JobDate, @LineNumber, @PaymentType, @OffsetCode,
                @Amount, @BillDueDate, @BillNumber, @CorporateBankCode,
                @DepositAccountNumber, @RemitterName, @Remarks, @DataSetId,
                @CreatedAt, @UpdatedAt
            )";

        try
        {
            return await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                var affected = await connection.ExecuteAsync(sql, vouchersList, transaction);
                LogInfo($"Bulk inserted {affected} receipt vouchers");
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
            DELETE FROM ReceiptVouchers 
            WHERE JobDate >= @StartDate AND JobDate <= @EndDate";

        try
        {
            using var connection = CreateConnection();
            var affected = await connection.ExecuteAsync(sql, new { StartDate = startDate, EndDate = endDate });
            LogInfo($"Deleted {affected} receipt vouchers for date range {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
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
            DELETE FROM ReceiptVouchers 
            WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = CreateConnection();
            var affected = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
            LogInfo($"Deleted {affected} receipt vouchers for DataSetId {dataSetId}");
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
            FROM ReceiptVouchers 
            WHERE VoucherNumber = @VoucherNumber AND LineNumber = @LineNumber";

        try
        {
            using var connection = CreateConnection();
            var count = await connection.QuerySingleAsync<int>(sql, new { VoucherNumber = voucherNumber, LineNumber = lineNumber });
            var exists = count > 0;
            LogDebug($"Receipt voucher exists check for {voucherNumber}-{lineNumber}: {exists}");
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
            FROM ReceiptVouchers 
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

    public async Task<Dictionary<string, decimal>> GetTotalAmountByCustomerAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT CustomerCode, SUM(Amount) as TotalAmount
            FROM ReceiptVouchers 
            WHERE JobDate >= @StartDate AND JobDate <= @EndDate
            GROUP BY CustomerCode";

        try
        {
            using var connection = CreateConnection();
            var results = await connection.QueryAsync<dynamic>(sql, new { StartDate = startDate, EndDate = endDate });
            var totals = results.ToDictionary(r => (string)r.CustomerCode, r => (decimal)r.TotalAmount);
            LogDebug($"Retrieved total amounts by customer for period {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}: {totals.Count} customers");
            return totals;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetTotalAmountByCustomerAsync), new { StartDate = startDate, EndDate = endDate });
            throw;
        }
    }
}