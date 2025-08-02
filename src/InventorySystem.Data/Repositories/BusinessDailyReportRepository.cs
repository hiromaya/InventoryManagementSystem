using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories
{
    public class BusinessDailyReportRepository : BaseRepository, IBusinessDailyReportRepository
    {
        public BusinessDailyReportRepository(string connectionString, ILogger<BusinessDailyReportRepository> logger) 
            : base(connectionString, logger)
        {
        }

        public async Task ClearDailyAreaAsync()
        {
            const string sql = @"
                UPDATE BusinessDailyReport
                SET 
                    DailyCashSales = 0,
                    DailyCashSalesTax = 0,
                    DailyCreditSales = 0,
                    DailySalesDiscount = 0,
                    DailyCreditSalesTax = 0,
                    DailyCashPurchase = 0,
                    DailyCashPurchaseTax = 0,
                    DailyCreditPurchase = 0,
                    DailyPurchaseDiscount = 0,
                    DailyCreditPurchaseTax = 0,
                    DailyCashReceipt = 0,
                    DailyBankReceipt = 0,
                    DailyOtherReceipt = 0,
                    DailyCashPayment = 0,
                    DailyBankPayment = 0,
                    DailyOtherPayment = 0,
                    UpdatedDate = GETDATE()";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql);
            
            _logger.LogInformation("営業日報の日計エリアをクリアしました");
        }

        public async Task AggregateSalesDataAsync(DateTime jobDate)
        {
            await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // 現金売上（伝票種52、明細種1-4）
                await AggregateField(connection, transaction, jobDate, "DailyCashSales", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(sv.Amount) AS TotalAmount
                    FROM SalesVouchers sv
                    LEFT JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                    WHERE sv.JobDate = @JobDate
                      AND sv.VoucherType = 52
                      AND sv.DetailType IN (1,2,3,4)
                      AND sv.IsActive = 1
                    GROUP BY c.CustomerCategory1");

                // 現売消費税（伝票種52、明細種18）
                await AggregateField(connection, transaction, jobDate, "DailyCashSalesTax", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(sv.Amount) AS TotalAmount
                    FROM SalesVouchers sv
                    LEFT JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                    WHERE sv.JobDate = @JobDate
                      AND sv.VoucherType = 52
                      AND sv.DetailType = 18
                      AND sv.IsActive = 1
                    GROUP BY c.CustomerCategory1");

                // 掛売上（伝票種51、明細種1-2）
                await AggregateField(connection, transaction, jobDate, "DailyCreditSales", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(sv.Amount) AS TotalAmount
                    FROM SalesVouchers sv
                    LEFT JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                    WHERE sv.JobDate = @JobDate
                      AND sv.VoucherType = 51
                      AND sv.DetailType IN (1,2)
                      AND sv.IsActive = 1
                    GROUP BY c.CustomerCategory1");

                // 売上値引（伝票種51、明細種3-4）
                await AggregateField(connection, transaction, jobDate, "DailySalesDiscount", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(sv.Amount) AS TotalAmount
                    FROM SalesVouchers sv
                    LEFT JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                    WHERE sv.JobDate = @JobDate
                      AND sv.VoucherType = 51
                      AND sv.DetailType IN (3,4)
                      AND sv.IsActive = 1
                    GROUP BY c.CustomerCategory1");

                // 掛売消費税（伝票種51、明細種18）
                await AggregateField(connection, transaction, jobDate, "DailyCreditSalesTax", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(sv.Amount) AS TotalAmount
                    FROM SalesVouchers sv
                    LEFT JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                    WHERE sv.JobDate = @JobDate
                      AND sv.VoucherType = 51
                      AND sv.DetailType = 18
                      AND sv.IsActive = 1
                    GROUP BY c.CustomerCategory1");

                _logger.LogInformation("売上伝票データの集計が完了しました: {JobDate}", jobDate);
            });
        }

        public async Task AggregatePurchaseDataAsync(DateTime jobDate)
        {
            await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // 現金仕入（伝票種12、明細種1）
                await AggregateField(connection, transaction, jobDate, "DailyCashPurchase", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PurchaseVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.VoucherType = 12
                      AND pv.DetailType = 1
                      AND pv.IsActive = 1
                    GROUP BY s.SupplierCategory1");

                // 現仕消費税（伝票種12、明細種18）
                await AggregateField(connection, transaction, jobDate, "DailyCashPurchaseTax", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PurchaseVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.VoucherType = 12
                      AND pv.DetailType = 18
                      AND pv.IsActive = 1
                    GROUP BY s.SupplierCategory1");

                // 掛仕入（伝票種11、明細種1-2）
                await AggregateField(connection, transaction, jobDate, "DailyCreditPurchase", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PurchaseVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.VoucherType = 11
                      AND pv.DetailType IN (1,2)
                      AND pv.IsActive = 1
                    GROUP BY s.SupplierCategory1");

                // 仕入値引（伝票種11、明細種3-4）
                await AggregateField(connection, transaction, jobDate, "DailyPurchaseDiscount", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PurchaseVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.VoucherType = 11
                      AND pv.DetailType IN (3,4)
                      AND pv.IsActive = 1
                    GROUP BY s.SupplierCategory1");

                // 掛仕入消費税（伝票種11、明細種18）
                await AggregateField(connection, transaction, jobDate, "DailyCreditPurchaseTax", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PurchaseVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.VoucherType = 11
                      AND pv.DetailType = 18
                      AND pv.IsActive = 1
                    GROUP BY s.SupplierCategory1");

                _logger.LogInformation("仕入伝票データの集計が完了しました: {JobDate}", jobDate);
            });
        }

        public async Task AggregateReceiptDataAsync(DateTime jobDate)
        {
            await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // 現金・小切手・手形入金（伝票種52、明細種12）
                await AggregateField(connection, transaction, jobDate, "DailyCashReceipt", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(rv.Amount) AS TotalAmount
                    FROM ReceiptVouchers rv
                    LEFT JOIN CustomerMaster c ON rv.CustomerCode = c.CustomerCode
                    WHERE rv.JobDate = @JobDate
                      AND rv.VoucherType = 52
                      AND rv.DetailType = 12
                      AND rv.IsActive = 1
                    GROUP BY c.CustomerCategory1");

                // 振込入金（伝票種52、明細種13）
                await AggregateField(connection, transaction, jobDate, "DailyBankReceipt", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(rv.Amount) AS TotalAmount
                    FROM ReceiptVouchers rv
                    LEFT JOIN CustomerMaster c ON rv.CustomerCode = c.CustomerCode
                    WHERE rv.JobDate = @JobDate
                      AND rv.VoucherType = 52
                      AND rv.DetailType = 13
                      AND rv.IsActive = 1
                    GROUP BY c.CustomerCategory1");

                // 入金値引・その他入金（伝票種52、明細種14）
                await AggregateField(connection, transaction, jobDate, "DailyOtherReceipt", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(rv.Amount) AS TotalAmount
                    FROM ReceiptVouchers rv
                    LEFT JOIN CustomerMaster c ON rv.CustomerCode = c.CustomerCode
                    WHERE rv.JobDate = @JobDate
                      AND rv.VoucherType = 52
                      AND rv.DetailType = 14
                      AND rv.IsActive = 1
                    GROUP BY c.CustomerCategory1");

                _logger.LogInformation("入金伝票データの集計が完了しました: {JobDate}", jobDate);
            });
        }

        public async Task AggregatePaymentDataAsync(DateTime jobDate)
        {
            await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // 現金・小切手・手形支払（伝票種52、明細種15）
                await AggregateField(connection, transaction, jobDate, "DailyCashPayment", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PaymentVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.VoucherType = 52
                      AND pv.DetailType = 15
                      AND pv.IsActive = 1
                    GROUP BY s.SupplierCategory1");

                // 振込支払（伝票種52、明細種16）
                await AggregateField(connection, transaction, jobDate, "DailyBankPayment", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PaymentVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.VoucherType = 52
                      AND pv.DetailType = 16
                      AND pv.IsActive = 1
                    GROUP BY s.SupplierCategory1");

                // 支払値引・その他支払（伝票種52、明細種17）
                await AggregateField(connection, transaction, jobDate, "DailyOtherPayment", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PaymentVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.VoucherType = 52
                      AND pv.DetailType = 17
                      AND pv.IsActive = 1
                    GROUP BY s.SupplierCategory1");

                _logger.LogInformation("支払伝票データの集計が完了しました: {JobDate}", jobDate);
            });
        }

        public async Task<List<BusinessDailyReportItem>> GetReportDataAsync()
        {
            const string sql = @"
                SELECT 
                    ClassificationCode,
                    CustomerClassName,
                    SupplierClassName,
                    DailyCashSales,
                    DailyCashSalesTax,
                    DailyCreditSales,
                    DailySalesDiscount,
                    DailyCreditSalesTax,
                    DailyCashPurchase,
                    DailyCashPurchaseTax,
                    DailyCreditPurchase,
                    DailyPurchaseDiscount,
                    DailyCreditPurchaseTax,
                    DailyCashReceipt,
                    DailyBankReceipt,
                    DailyOtherReceipt,
                    DailyCashPayment,
                    DailyBankPayment,
                    DailyOtherPayment
                FROM BusinessDailyReport
                ORDER BY ClassificationCode";

            using var connection = CreateConnection();
            var result = await connection.QueryAsync<BusinessDailyReportItem>(sql);
            return result.ToList();
        }

        public async Task UpdateClassificationNamesAsync()
        {
            await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // 得意先分類1名の更新
                const string updateCustomerSql = @"
                    UPDATE br
                    SET br.CustomerClassName = COALESCE(cc.CategoryName, ''),
                        br.UpdatedDate = GETDATE()
                    FROM BusinessDailyReport br
                    LEFT JOIN CustomerCategory1Master cc ON br.ClassificationCode = CAST(cc.CategoryCode AS NVARCHAR(3))
                    WHERE br.ClassificationCode != '000'";

                await connection.ExecuteAsync(updateCustomerSql, transaction: transaction);

                // 仕入先分類1名の更新
                const string updateSupplierSql = @"
                    UPDATE br
                    SET br.SupplierClassName = COALESCE(sc.CategoryName, ''),
                        br.UpdatedDate = GETDATE()
                    FROM BusinessDailyReport br
                    LEFT JOIN SupplierCategory1Master sc ON br.ClassificationCode = CAST(sc.CategoryCode AS NVARCHAR(3))
                    WHERE br.ClassificationCode != '000'";

                await connection.ExecuteAsync(updateSupplierSql, transaction: transaction);

                _logger.LogInformation("分類名の更新が完了しました");
            });
        }

        private async Task AggregateField(SqlConnection connection, SqlTransaction transaction, DateTime jobDate, string fieldName, string aggregationQuery)
        {
            // 各分類の集計
            var aggregationResults = await connection.QueryAsync<(string ClassificationCode, decimal TotalAmount)>(
                aggregationQuery, 
                new { JobDate = jobDate }, 
                transaction);

            foreach (var result in aggregationResults)
            {
                var updateSql = $@"
                    UPDATE BusinessDailyReport 
                    SET {fieldName} = @Amount, 
                        UpdatedDate = GETDATE() 
                    WHERE ClassificationCode = @ClassificationCode";

                await connection.ExecuteAsync(updateSql, 
                    new { Amount = result.TotalAmount, ClassificationCode = result.ClassificationCode }, 
                    transaction);
            }

            // 合計行の更新
            var totalUpdateSql = $@"
                UPDATE BusinessDailyReport
                SET {fieldName} = (SELECT SUM({fieldName}) FROM BusinessDailyReport WHERE ClassificationCode != '000'),
                    UpdatedDate = GETDATE()
                WHERE ClassificationCode = '000'";

            await connection.ExecuteAsync(totalUpdateSql, transaction: transaction);
        }
    }
}