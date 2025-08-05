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
                // 現金・小切手・手形入金（PaymentType 1,2,4）
                await AggregateField(connection, transaction, jobDate, "DailyCashReceipt", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(rv.Amount) AS TotalAmount
                    FROM ReceiptVouchers rv
                    LEFT JOIN CustomerMaster c ON rv.CustomerCode = c.CustomerCode
                    WHERE rv.JobDate = @JobDate
                      AND rv.PaymentType IN (1,2,4)
                    GROUP BY c.CustomerCategory1");

                // 振込入金（PaymentType 3）
                await AggregateField(connection, transaction, jobDate, "DailyBankReceipt", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(rv.Amount) AS TotalAmount
                    FROM ReceiptVouchers rv
                    LEFT JOIN CustomerMaster c ON rv.CustomerCode = c.CustomerCode
                    WHERE rv.JobDate = @JobDate
                      AND rv.PaymentType = 3
                    GROUP BY c.CustomerCategory1");

                // その他入金（PaymentType 5,6,7,8,9）
                await AggregateField(connection, transaction, jobDate, "DailyOtherReceipt", @"
                    SELECT 
                        COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                        SUM(rv.Amount) AS TotalAmount
                    FROM ReceiptVouchers rv
                    LEFT JOIN CustomerMaster c ON rv.CustomerCode = c.CustomerCode
                    WHERE rv.JobDate = @JobDate
                      AND rv.PaymentType IN (5,6,7,8,9)
                    GROUP BY c.CustomerCategory1");

                _logger.LogInformation("入金伝票データの集計が完了しました: {JobDate}", jobDate);
            });
        }

        public async Task AggregatePaymentDataAsync(DateTime jobDate)
        {
            await ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // 現金・小切手・手形支払（PaymentType 1,2,4）
                await AggregateField(connection, transaction, jobDate, "DailyCashPayment", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PaymentVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.PaymentType IN (1,2,4)
                    GROUP BY s.SupplierCategory1");

                // 振込支払（PaymentType 3）
                await AggregateField(connection, transaction, jobDate, "DailyBankPayment", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PaymentVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.PaymentType = 3
                    GROUP BY s.SupplierCategory1");

                // その他支払（PaymentType 5,6,7,8,9）
                await AggregateField(connection, transaction, jobDate, "DailyOtherPayment", @"
                    SELECT 
                        COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                        SUM(pv.Amount) AS TotalAmount
                    FROM PaymentVouchers pv
                    LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                    WHERE pv.JobDate = @JobDate
                      AND pv.PaymentType IN (5,6,7,8,9)
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

        public async Task<List<BusinessDailyReportItem>> GetMonthlyDataAsync(DateTime jobDate)
        {
            // 月初から前日までの累計を取得
            var startDate = new DateTime(jobDate.Year, jobDate.Month, 1);
            var endDate = jobDate.AddDays(-1);

            const string sql = @"
                -- 分類別に月計を集計
                SELECT 
                    COALESCE(c.CustomerCategory1, s.SupplierCategory1, '999') AS ClassificationCode,
                    COALESCE(cc.CategoryName, sc.CategoryName, '') AS CustomerClassName,
                    COALESCE(sc.CategoryName, cc.CategoryName, '') AS SupplierClassName,
                    
                    -- 売上関連（現金売上は含まない - 現金売上は入金に含まれるため）
                    0 AS MonthlyCashSales,
                    SUM(CASE WHEN sv.VoucherType = 52 AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS MonthlyCashSalesTax,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType IN (1,2) THEN sv.Amount ELSE 0 END) AS MonthlyCreditSales,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType IN (3,4) THEN sv.Amount ELSE 0 END) AS MonthlySalesDiscount,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS MonthlyCreditSalesTax,
                    
                    -- 仕入関連（現金仕入は含まない - 現金仕入は支払に含まれるため）
                    0 AS MonthlyCashPurchase,
                    SUM(CASE WHEN pv.VoucherType = 12 AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS MonthlyCashPurchaseTax,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType IN (1,2) THEN pv.Amount ELSE 0 END) AS MonthlyCreditPurchase,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType IN (3,4) THEN pv.Amount ELSE 0 END) AS MonthlyPurchaseDiscount,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS MonthlyCreditPurchaseTax,
                    
                    -- 入金関連（現金売上を含む）
                    (SUM(CASE WHEN rv.PaymentType IN (1,2,4) THEN rv.Amount ELSE 0 END) + 
                     SUM(CASE WHEN sv.VoucherType = 52 AND sv.DetailType IN (1,2,3,4) THEN sv.Amount ELSE 0 END)) AS MonthlyCashReceipt,
                    SUM(CASE WHEN rv.PaymentType = 3 THEN rv.Amount ELSE 0 END) AS MonthlyBankReceipt,
                    SUM(CASE WHEN rv.PaymentType IN (5,6,7,8,9) THEN rv.Amount ELSE 0 END) AS MonthlyOtherReceipt,
                    
                    -- 支払関連（現金仕入を含む）
                    (SUM(CASE WHEN payv.PaymentType IN (1,2,4) THEN payv.Amount ELSE 0 END) + 
                     SUM(CASE WHEN pv.VoucherType = 12 AND pv.DetailType = 1 THEN pv.Amount ELSE 0 END)) AS MonthlyCashPayment,
                    SUM(CASE WHEN payv.PaymentType = 3 THEN payv.Amount ELSE 0 END) AS MonthlyBankPayment,
                    SUM(CASE WHEN payv.PaymentType IN (5,6,7,8,9) THEN payv.Amount ELSE 0 END) AS MonthlyOtherPayment
                    
                FROM (
                    SELECT DISTINCT COALESCE(CustomerCategory1, '999') AS Category FROM CustomerMaster
                    UNION
                    SELECT DISTINCT COALESCE(SupplierCategory1, '999') AS Category FROM SupplierMaster
                ) AS categories
                LEFT JOIN CustomerMaster c ON categories.Category = c.CustomerCategory1
                LEFT JOIN SupplierMaster s ON categories.Category = s.SupplierCategory1
                LEFT JOIN CustomerCategory1Master cc ON categories.Category = CAST(cc.CategoryCode AS NVARCHAR(3))
                LEFT JOIN SupplierCategory1Master sc ON categories.Category = CAST(sc.CategoryCode AS NVARCHAR(3))
                LEFT JOIN SalesVouchers sv ON (c.CustomerCategory1 = categories.Category) 
                    AND sv.JobDate BETWEEN @StartDate AND @EndDate AND sv.IsActive = 1
                LEFT JOIN PurchaseVouchers pv ON (s.SupplierCategory1 = categories.Category) 
                    AND pv.JobDate BETWEEN @StartDate AND @EndDate AND pv.IsActive = 1
                LEFT JOIN ReceiptVouchers rv ON (c.CustomerCategory1 = categories.Category) 
                    AND rv.JobDate BETWEEN @StartDate AND @EndDate
                LEFT JOIN PaymentVouchers payv ON (s.SupplierCategory1 = categories.Category) 
                    AND payv.JobDate BETWEEN @StartDate AND @EndDate
                GROUP BY 
                    COALESCE(c.CustomerCategory1, s.SupplierCategory1, '999'),
                    COALESCE(cc.CategoryName, sc.CategoryName, ''),
                    COALESCE(sc.CategoryName, cc.CategoryName, '')
                
                UNION ALL
                
                -- 合計行（000）
                SELECT 
                    '000' AS ClassificationCode,
                    '合計' AS CustomerClassName,
                    '合計' AS SupplierClassName,
                    0 AS MonthlyCashSales,
                    SUM(CASE WHEN sv.VoucherType = 52 AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS MonthlyCashSalesTax,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType IN (1,2) THEN sv.Amount ELSE 0 END) AS MonthlyCreditSales,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType IN (3,4) THEN sv.Amount ELSE 0 END) AS MonthlySalesDiscount,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS MonthlyCreditSalesTax,
                    0 AS MonthlyCashPurchase,
                    SUM(CASE WHEN pv.VoucherType = 12 AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS MonthlyCashPurchaseTax,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType IN (1,2) THEN pv.Amount ELSE 0 END) AS MonthlyCreditPurchase,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType IN (3,4) THEN pv.Amount ELSE 0 END) AS MonthlyPurchaseDiscount,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS MonthlyCreditPurchaseTax,
                    (SUM(CASE WHEN rv.PaymentType IN (1,2,4) THEN rv.Amount ELSE 0 END) + 
                     SUM(CASE WHEN sv.VoucherType = 52 AND sv.DetailType IN (1,2,3,4) THEN sv.Amount ELSE 0 END)) AS MonthlyCashReceipt,
                    SUM(CASE WHEN rv.PaymentType = 3 THEN rv.Amount ELSE 0 END) AS MonthlyBankReceipt,
                    SUM(CASE WHEN rv.PaymentType IN (5,6,7,8,9) THEN rv.Amount ELSE 0 END) AS MonthlyOtherReceipt,
                    (SUM(CASE WHEN payv.PaymentType IN (1,2,4) THEN payv.Amount ELSE 0 END) + 
                     SUM(CASE WHEN pv.VoucherType = 12 AND pv.DetailType = 1 THEN pv.Amount ELSE 0 END)) AS MonthlyCashPayment,
                    SUM(CASE WHEN payv.PaymentType = 3 THEN payv.Amount ELSE 0 END) AS MonthlyBankPayment,
                    SUM(CASE WHEN payv.PaymentType IN (5,6,7,8,9) THEN payv.Amount ELSE 0 END) AS MonthlyOtherPayment
                FROM SalesVouchers sv
                FULL OUTER JOIN PurchaseVouchers pv ON 1=1
                FULL OUTER JOIN ReceiptVouchers rv ON 1=1
                FULL OUTER JOIN PaymentVouchers payv ON 1=1
                WHERE (sv.JobDate BETWEEN @StartDate AND @EndDate AND sv.IsActive = 1)
                   OR (pv.JobDate BETWEEN @StartDate AND @EndDate AND pv.IsActive = 1)
                   OR (rv.JobDate BETWEEN @StartDate AND @EndDate)
                   OR (payv.JobDate BETWEEN @StartDate AND @EndDate)
                
                ORDER BY ClassificationCode";

            using var connection = CreateConnection();
            var result = await connection.QueryAsync<BusinessDailyReportItem>(sql, new { StartDate = startDate, EndDate = endDate });
            return result.ToList();
        }

        public async Task<List<BusinessDailyReportItem>> GetYearlyDataAsync(DateTime jobDate)
        {
            // 年度初めから前日までの累計を取得
            // 年計は4項目のみ（売上、売上消費税、仕入、仕入消費税）
            var startDate = new DateTime(jobDate.Year, 4, 1); // 4月開始の会計年度
            if (jobDate.Month < 4)
            {
                startDate = new DateTime(jobDate.Year - 1, 4, 1);
            }
            var endDate = jobDate.AddDays(-1);

            const string sql = @"
                -- 分類別に年計を集計（4項目のみ）
                SELECT 
                    COALESCE(c.CustomerCategory1, s.SupplierCategory1, '999') AS ClassificationCode,
                    COALESCE(cc.CategoryName, sc.CategoryName, '') AS CustomerClassName,
                    COALESCE(sc.CategoryName, cc.CategoryName, '') AS SupplierClassName,
                    
                    -- 年計は4項目のみ
                    SUM(CASE WHEN sv.VoucherType IN (51, 52) AND sv.DetailType IN (1,2,3,4) THEN sv.Amount ELSE 0 END) AS YearlySales,
                    SUM(CASE WHEN sv.VoucherType IN (51, 52) AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS YearlySalesTax,
                    SUM(CASE WHEN pv.VoucherType IN (11, 12) AND pv.DetailType IN (1,2,3,4) THEN pv.Amount ELSE 0 END) AS YearlyPurchase,
                    SUM(CASE WHEN pv.VoucherType IN (11, 12) AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS YearlyPurchaseTax,
                    
                    -- その他の項目は0
                    0 AS YearlyCashSales,
                    0 AS YearlyCashSalesTax,
                    0 AS YearlyCreditSales,
                    0 AS YearlySalesDiscount,
                    0 AS YearlyCreditSalesTax,
                    0 AS YearlyCashPurchase,
                    0 AS YearlyCashPurchaseTax,
                    0 AS YearlyCreditPurchase,
                    0 AS YearlyPurchaseDiscount,
                    0 AS YearlyCreditPurchaseTax,
                    0 AS YearlyCashReceipt,
                    0 AS YearlyBankReceipt,
                    0 AS YearlyOtherReceipt,
                    0 AS YearlyCashPayment,
                    0 AS YearlyBankPayment,
                    0 AS YearlyOtherPayment
                    
                FROM (
                    SELECT DISTINCT COALESCE(CustomerCategory1, '999') AS Category FROM CustomerMaster
                    UNION
                    SELECT DISTINCT COALESCE(SupplierCategory1, '999') AS Category FROM SupplierMaster
                ) AS categories
                LEFT JOIN CustomerMaster c ON categories.Category = c.CustomerCategory1
                LEFT JOIN SupplierMaster s ON categories.Category = s.SupplierCategory1
                LEFT JOIN CustomerCategory1Master cc ON categories.Category = CAST(cc.CategoryCode AS NVARCHAR(3))
                LEFT JOIN SupplierCategory1Master sc ON categories.Category = CAST(sc.CategoryCode AS NVARCHAR(3))
                LEFT JOIN SalesVouchers sv ON (c.CustomerCategory1 = categories.Category) 
                    AND sv.JobDate BETWEEN @StartDate AND @EndDate AND sv.IsActive = 1
                LEFT JOIN PurchaseVouchers pv ON (s.SupplierCategory1 = categories.Category) 
                    AND pv.JobDate BETWEEN @StartDate AND @EndDate AND pv.IsActive = 1
                GROUP BY 
                    COALESCE(c.CustomerCategory1, s.SupplierCategory1, '999'),
                    COALESCE(cc.CategoryName, sc.CategoryName, ''),
                    COALESCE(sc.CategoryName, cc.CategoryName, '')
                
                UNION ALL
                
                -- 合計行（000）- 年計4項目のみ
                SELECT 
                    '000' AS ClassificationCode,
                    '合計' AS CustomerClassName,
                    '合計' AS SupplierClassName,
                    SUM(CASE WHEN sv.VoucherType IN (51, 52) AND sv.DetailType IN (1,2,3,4) THEN sv.Amount ELSE 0 END) AS YearlySales,
                    SUM(CASE WHEN sv.VoucherType IN (51, 52) AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS YearlySalesTax,
                    SUM(CASE WHEN pv.VoucherType IN (11, 12) AND pv.DetailType IN (1,2,3,4) THEN pv.Amount ELSE 0 END) AS YearlyPurchase,
                    SUM(CASE WHEN pv.VoucherType IN (11, 12) AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS YearlyPurchaseTax,
                    0 AS YearlyCashSales,
                    0 AS YearlyCashSalesTax,
                    0 AS YearlyCreditSales,
                    0 AS YearlySalesDiscount,
                    0 AS YearlyCreditSalesTax,
                    0 AS YearlyCashPurchase,
                    0 AS YearlyCashPurchaseTax,
                    0 AS YearlyCreditPurchase,
                    0 AS YearlyPurchaseDiscount,
                    0 AS YearlyCreditPurchaseTax,
                    0 AS YearlyCashReceipt,
                    0 AS YearlyBankReceipt,
                    0 AS YearlyOtherReceipt,
                    0 AS YearlyCashPayment,
                    0 AS YearlyBankPayment,
                    0 AS YearlyOtherPayment
                FROM SalesVouchers sv
                FULL OUTER JOIN PurchaseVouchers pv ON 1=1
                WHERE (sv.JobDate BETWEEN @StartDate AND @EndDate AND sv.IsActive = 1)
                   OR (pv.JobDate BETWEEN @StartDate AND @EndDate AND pv.IsActive = 1)
                
                ORDER BY ClassificationCode";

            using var connection = CreateConnection();
            var result = await connection.QueryAsync<BusinessDailyReportItem>(sql, new { StartDate = startDate, EndDate = endDate });
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