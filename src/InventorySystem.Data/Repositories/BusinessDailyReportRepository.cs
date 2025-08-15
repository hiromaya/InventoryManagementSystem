using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        // ================ 個別集計メソッド（月次用） ================

        /// <summary>
        /// 月次売上データを分類別に集計
        /// </summary>
        private async Task<Dictionary<string, BusinessDailyReportItem>> GetMonthlySalesDataAsync(
            DateTime startDate, DateTime endDate)
        {
            using var connection = CreateConnection();
            
            const string sql = @"
                SELECT 
                    COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                    SUM(CASE WHEN sv.VoucherType = 52 AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS MonthlyCashSalesTax,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType IN (1,2) THEN sv.Amount ELSE 0 END) AS MonthlyCreditSales,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType IN (3,4) THEN sv.Amount ELSE 0 END) AS MonthlySalesDiscount,
                    SUM(CASE WHEN sv.VoucherType = 51 AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS MonthlyCreditSalesTax
                FROM SalesVouchers sv
                LEFT JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                WHERE sv.JobDate BETWEEN @StartDate AND @EndDate 
                  AND sv.IsActive = 1
                GROUP BY c.CustomerCategory1";

            var result = await connection.QueryAsync<dynamic>(sql, new { StartDate = startDate, EndDate = endDate });
            
            var dictionary = new Dictionary<string, BusinessDailyReportItem>();
            foreach (var row in result)
            {
                var item = new BusinessDailyReportItem
                {
                    ClassificationCode = row.ClassificationCode,
                    MonthlyCashSalesTax = row.MonthlyCashSalesTax ?? 0,
                    MonthlyCreditSales = row.MonthlyCreditSales ?? 0,
                    MonthlySalesDiscount = row.MonthlySalesDiscount ?? 0,
                    MonthlyCreditSalesTax = row.MonthlyCreditSalesTax ?? 0
                };
                dictionary[item.ClassificationCode] = item;
            }
            
            return dictionary;
        }

        /// <summary>
        /// 月次仕入データを分類別に集計
        /// </summary>
        private async Task<Dictionary<string, BusinessDailyReportItem>> GetMonthlyPurchaseDataAsync(
            DateTime startDate, DateTime endDate)
        {
            using var connection = CreateConnection();
            
            const string sql = @"
                SELECT 
                    COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                    SUM(CASE WHEN pv.VoucherType = 12 AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS MonthlyCashPurchaseTax,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType IN (1,2) THEN pv.Amount ELSE 0 END) AS MonthlyCreditPurchase,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType IN (3,4) THEN pv.Amount ELSE 0 END) AS MonthlyPurchaseDiscount,
                    SUM(CASE WHEN pv.VoucherType = 11 AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS MonthlyCreditPurchaseTax
                FROM PurchaseVouchers pv
                LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                WHERE pv.JobDate BETWEEN @StartDate AND @EndDate 
                  AND pv.IsActive = 1
                GROUP BY s.SupplierCategory1";

            var result = await connection.QueryAsync<dynamic>(sql, new { StartDate = startDate, EndDate = endDate });
            
            var dictionary = new Dictionary<string, BusinessDailyReportItem>();
            foreach (var row in result)
            {
                var item = new BusinessDailyReportItem
                {
                    ClassificationCode = row.ClassificationCode,
                    MonthlyCashPurchaseTax = row.MonthlyCashPurchaseTax ?? 0,
                    MonthlyCreditPurchase = row.MonthlyCreditPurchase ?? 0,
                    MonthlyPurchaseDiscount = row.MonthlyPurchaseDiscount ?? 0,
                    MonthlyCreditPurchaseTax = row.MonthlyCreditPurchaseTax ?? 0
                };
                dictionary[item.ClassificationCode] = item;
            }
            
            return dictionary;
        }

        /// <summary>
        /// 月次入金データを分類別に集計
        /// </summary>
        private async Task<Dictionary<string, BusinessDailyReportItem>> GetMonthlyReceiptDataAsync(
            DateTime startDate, DateTime endDate)
        {
            using var connection = CreateConnection();
            
            const string sql = @"
                SELECT 
                    COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                    SUM(CASE WHEN rv.PaymentType IN (1,2,4) THEN rv.Amount ELSE 0 END) AS MonthlyCashReceipt,
                    SUM(CASE WHEN rv.PaymentType = 3 THEN rv.Amount ELSE 0 END) AS MonthlyBankReceipt,
                    SUM(CASE WHEN rv.PaymentType IN (5,6,7,8,9) THEN rv.Amount ELSE 0 END) AS MonthlyOtherReceipt
                FROM ReceiptVouchers rv
                LEFT JOIN CustomerMaster c ON rv.CustomerCode = c.CustomerCode
                WHERE rv.JobDate BETWEEN @StartDate AND @EndDate
                GROUP BY c.CustomerCategory1";

            var result = await connection.QueryAsync<dynamic>(sql, new { StartDate = startDate, EndDate = endDate });
            
            var dictionary = new Dictionary<string, BusinessDailyReportItem>();
            foreach (var row in result)
            {
                var item = new BusinessDailyReportItem
                {
                    ClassificationCode = row.ClassificationCode,
                    MonthlyCashReceipt = row.MonthlyCashReceipt ?? 0,
                    MonthlyBankReceipt = row.MonthlyBankReceipt ?? 0,
                    MonthlyOtherReceipt = row.MonthlyOtherReceipt ?? 0
                };
                dictionary[item.ClassificationCode] = item;
            }
            
            return dictionary;
        }

        /// <summary>
        /// 月次支払データを分類別に集計
        /// </summary>
        private async Task<Dictionary<string, BusinessDailyReportItem>> GetMonthlyPaymentDataAsync(
            DateTime startDate, DateTime endDate)
        {
            using var connection = CreateConnection();
            
            const string sql = @"
                SELECT 
                    COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                    SUM(CASE WHEN pv.PaymentType IN (1,2,4) THEN pv.Amount ELSE 0 END) AS MonthlyCashPayment,
                    SUM(CASE WHEN pv.PaymentType = 3 THEN pv.Amount ELSE 0 END) AS MonthlyBankPayment,
                    SUM(CASE WHEN pv.PaymentType IN (5,6,7,8,9) THEN pv.Amount ELSE 0 END) AS MonthlyOtherPayment
                FROM PaymentVouchers pv
                LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                WHERE pv.JobDate BETWEEN @StartDate AND @EndDate
                GROUP BY s.SupplierCategory1";

            var result = await connection.QueryAsync<dynamic>(sql, new { StartDate = startDate, EndDate = endDate });
            
            var dictionary = new Dictionary<string, BusinessDailyReportItem>();
            foreach (var row in result)
            {
                var item = new BusinessDailyReportItem
                {
                    ClassificationCode = row.ClassificationCode,
                    MonthlyCashPayment = row.MonthlyCashPayment ?? 0,
                    MonthlyBankPayment = row.MonthlyBankPayment ?? 0,
                    MonthlyOtherPayment = row.MonthlyOtherPayment ?? 0
                };
                dictionary[item.ClassificationCode] = item;
            }
            
            return dictionary;
        }

        /// <summary>
        /// 月次データをマージして完全なリストを作成
        /// </summary>
        private List<BusinessDailyReportItem> MergeMonthlyData(
            Dictionary<string, BusinessDailyReportItem> salesData,
            Dictionary<string, BusinessDailyReportItem> purchaseData,
            Dictionary<string, BusinessDailyReportItem> receiptData,
            Dictionary<string, BusinessDailyReportItem> paymentData)
        {
            // すべての分類コードを収集
            var allCodes = new HashSet<string>();
            allCodes.UnionWith(salesData.Keys);
            allCodes.UnionWith(purchaseData.Keys);
            allCodes.UnionWith(receiptData.Keys);
            allCodes.UnionWith(paymentData.Keys);
            
            var result = new List<BusinessDailyReportItem>();
            
            foreach (var code in allCodes.OrderBy(c => c))
            {
                var item = new BusinessDailyReportItem
                {
                    ClassificationCode = code,
                    CustomerClassName = "",  // 後で別途取得
                    SupplierClassName = "",  // 後で別途取得
                };
                
                // 売上データをマージ
                if (salesData.TryGetValue(code, out var sales))
                {
                    item.MonthlyCashSalesTax = sales.MonthlyCashSalesTax;
                    item.MonthlyCreditSales = sales.MonthlyCreditSales;
                    item.MonthlySalesDiscount = sales.MonthlySalesDiscount;
                    item.MonthlyCreditSalesTax = sales.MonthlyCreditSalesTax;
                }
                
                // 仕入データをマージ
                if (purchaseData.TryGetValue(code, out var purchase))
                {
                    item.MonthlyCashPurchaseTax = purchase.MonthlyCashPurchaseTax;
                    item.MonthlyCreditPurchase = purchase.MonthlyCreditPurchase;
                    item.MonthlyPurchaseDiscount = purchase.MonthlyPurchaseDiscount;
                    item.MonthlyCreditPurchaseTax = purchase.MonthlyCreditPurchaseTax;
                }
                
                // 入金データをマージ
                if (receiptData.TryGetValue(code, out var receipt))
                {
                    item.MonthlyCashReceipt = receipt.MonthlyCashReceipt;
                    item.MonthlyBankReceipt = receipt.MonthlyBankReceipt;
                    item.MonthlyOtherReceipt = receipt.MonthlyOtherReceipt;
                }
                
                // 支払データをマージ
                if (paymentData.TryGetValue(code, out var payment))
                {
                    item.MonthlyCashPayment = payment.MonthlyCashPayment;
                    item.MonthlyBankPayment = payment.MonthlyBankPayment;
                    item.MonthlyOtherPayment = payment.MonthlyOtherPayment;
                }
                
                result.Add(item);
            }
            
            // 合計行（000）を追加
            var totalItem = new BusinessDailyReportItem
            {
                ClassificationCode = "000",
                CustomerClassName = "合計",
                SupplierClassName = "合計",
                MonthlyCashSalesTax = result.Sum(r => r.MonthlyCashSalesTax ?? 0),
                MonthlyCreditSales = result.Sum(r => r.MonthlyCreditSales ?? 0),
                MonthlySalesDiscount = result.Sum(r => r.MonthlySalesDiscount ?? 0),
                MonthlyCreditSalesTax = result.Sum(r => r.MonthlyCreditSalesTax ?? 0),
                MonthlyCashPurchaseTax = result.Sum(r => r.MonthlyCashPurchaseTax ?? 0),
                MonthlyCreditPurchase = result.Sum(r => r.MonthlyCreditPurchase ?? 0),
                MonthlyPurchaseDiscount = result.Sum(r => r.MonthlyPurchaseDiscount ?? 0),
                MonthlyCreditPurchaseTax = result.Sum(r => r.MonthlyCreditPurchaseTax ?? 0),
                MonthlyCashReceipt = result.Sum(r => r.MonthlyCashReceipt ?? 0),
                MonthlyBankReceipt = result.Sum(r => r.MonthlyBankReceipt ?? 0),
                MonthlyOtherReceipt = result.Sum(r => r.MonthlyOtherReceipt ?? 0),
                MonthlyCashPayment = result.Sum(r => r.MonthlyCashPayment ?? 0),
                MonthlyBankPayment = result.Sum(r => r.MonthlyBankPayment ?? 0),
                MonthlyOtherPayment = result.Sum(r => r.MonthlyOtherPayment ?? 0)
            };
            
            result.Insert(0, totalItem);
            
            return result;
        }

        /// <summary>
        /// 分類名を取得して設定
        /// </summary>
        private async Task SetClassificationNamesAsync(
            SqlConnection connection, List<BusinessDailyReportItem> items)
        {
            // 得意先分類名を取得
            const string customerSql = @"
                SELECT CategoryCode, CategoryName 
                FROM CustomerCategory1Master";
            
            var customerCategories = await connection.QueryAsync<dynamic>(customerSql);
            var customerDict = customerCategories.ToDictionary(
                c => c.CategoryCode.ToString().PadLeft(3, '0'),
                c => (string)c.CategoryName);
            
            // 仕入先分類名を取得
            const string supplierSql = @"
                SELECT CategoryCode, CategoryName 
                FROM SupplierCategory1Master";
            
            var supplierCategories = await connection.QueryAsync<dynamic>(supplierSql);
            var supplierDict = supplierCategories.ToDictionary(
                s => s.CategoryCode.ToString().PadLeft(3, '0'),
                s => (string)s.CategoryName);
            
            // 分類名を設定
            foreach (var item in items)
            {
                if (item.ClassificationCode == "000")
                {
                    item.CustomerClassName = "合計";
                    item.SupplierClassName = "合計";
                }
                else
                {
                    customerDict.TryGetValue(item.ClassificationCode, out var customerName);
                    supplierDict.TryGetValue(item.ClassificationCode, out var supplierName);
                    item.CustomerClassName = customerName ?? "";
                    item.SupplierClassName = supplierName ?? "";
                }
            }
        }

        /// <summary>
        /// BusinessDailyReportテーブルの分類名をデータベースに保存
        /// </summary>
        public async Task UpdateClassificationNamesInDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("データベースの分類名更新を開始");
                
                using var connection = CreateConnection();
                
                // 得意先分類名を更新
                const string updateCustomerSql = @"
                    UPDATE bdr
                    SET CustomerClassName = cc.CategoryName
                    FROM BusinessDailyReport bdr
                    INNER JOIN CustomerCategory1Master cc 
                        ON bdr.ClassificationCode = RIGHT('000' + CAST(cc.CategoryCode AS NVARCHAR), 3)
                    WHERE bdr.ClassificationCode != '000'
                      AND bdr.ClassificationCode != '999'";
                
                var customerUpdated = await connection.ExecuteAsync(updateCustomerSql);
                _logger.LogInformation("得意先分類名更新完了: {UpdatedRows}件", customerUpdated);
                
                // 仕入先分類名を更新
                const string updateSupplierSql = @"
                    UPDATE bdr
                    SET SupplierClassName = sc.CategoryName
                    FROM BusinessDailyReport bdr
                    INNER JOIN SupplierCategory1Master sc 
                        ON bdr.ClassificationCode = RIGHT('000' + CAST(sc.CategoryCode AS NVARCHAR), 3)
                    WHERE bdr.ClassificationCode != '000'
                      AND bdr.ClassificationCode != '999'";
                
                var supplierUpdated = await connection.ExecuteAsync(updateSupplierSql);
                _logger.LogInformation("仕入先分類名更新完了: {UpdatedRows}件", supplierUpdated);
                
                // 合計行の分類名を設定
                const string updateTotalsSql = @"
                    UPDATE BusinessDailyReport
                    SET CustomerClassName = '合計',
                        SupplierClassName = '合計'
                    WHERE ClassificationCode = '000'";
                
                var totalsUpdated = await connection.ExecuteAsync(updateTotalsSql);
                _logger.LogInformation("合計行分類名更新完了: {UpdatedRows}件", totalsUpdated);
                
                _logger.LogInformation("全分類名更新完了: 得意先{Customer}件, 仕入先{Supplier}件, 合計{Total}件", 
                    customerUpdated, supplierUpdated, totalsUpdated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分類名のデータベース更新でエラーが発生しました");
                throw;
            }
        }

        public async Task<List<BusinessDailyReportItem>> GetMonthlyDataAsync(DateTime jobDate)
        {
            try
            {
                _logger.LogInformation("月次データ集計を開始: JobDate={JobDate}", jobDate);
                
                var startDate = new DateTime(jobDate.Year, jobDate.Month, 1);
                var endDate = jobDate.AddDays(-1);
                
                // 前日までのデータがない場合は空リストを返す
                if (endDate < startDate)
                {
                    _logger.LogInformation("月初のため月次データなし");
                    return new List<BusinessDailyReportItem>();
                }
                
                // 各テーブルを個別に集計（並列実行）
                var salesTask = GetMonthlySalesDataAsync(startDate, endDate);
                var purchaseTask = GetMonthlyPurchaseDataAsync(startDate, endDate);
                var receiptTask = GetMonthlyReceiptDataAsync(startDate, endDate);
                var paymentTask = GetMonthlyPaymentDataAsync(startDate, endDate);
                
                // すべての集計を待機
                await Task.WhenAll(salesTask, purchaseTask, receiptTask, paymentTask);
                
                _logger.LogInformation("個別集計完了 - 売上:{SalesCount}, 仕入:{PurchaseCount}, 入金:{ReceiptCount}, 支払:{PaymentCount}",
                    salesTask.Result.Count, purchaseTask.Result.Count, 
                    receiptTask.Result.Count, paymentTask.Result.Count);
                
                // データをマージ
                var result = MergeMonthlyData(
                    salesTask.Result,
                    purchaseTask.Result,
                    receiptTask.Result,
                    paymentTask.Result
                );
                
                // 分類名を設定（新しいconnectionを作成）
                using var connection = CreateConnection();
                await SetClassificationNamesAsync(connection, result);
                
                _logger.LogInformation("月次データ集計完了: {Count}件", result.Count);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "月次データ集計でエラーが発生しました");
                throw;
            }
        }

        // ================ 個別集計メソッド（年次用） ================

        /// <summary>
        /// 年次売上データを分類別に集計（、4項目のみ）
        /// </summary>
        private async Task<Dictionary<string, BusinessDailyReportItem>> GetYearlySalesDataAsync(
            DateTime startDate, DateTime endDate)
        {
            using var connection = CreateConnection();
            
            const string sql = @"
                SELECT 
                    COALESCE(c.CustomerCategory1, '999') AS ClassificationCode,
                    SUM(CASE WHEN sv.VoucherType IN (51, 52) AND sv.DetailType IN (1,2,3,4) THEN sv.Amount ELSE 0 END) AS YearlyCashSales,
                    SUM(CASE WHEN sv.VoucherType IN (51, 52) AND sv.DetailType = 18 THEN sv.Amount ELSE 0 END) AS YearlyCashSalesTax
                FROM SalesVouchers sv
                LEFT JOIN CustomerMaster c ON sv.CustomerCode = c.CustomerCode
                WHERE sv.JobDate BETWEEN @StartDate AND @EndDate 
                  AND sv.IsActive = 1
                GROUP BY c.CustomerCategory1";

            var result = await connection.QueryAsync<dynamic>(sql, new { StartDate = startDate, EndDate = endDate });
            
            var dictionary = new Dictionary<string, BusinessDailyReportItem>();
            foreach (var row in result)
            {
                var item = new BusinessDailyReportItem
                {
                    ClassificationCode = row.ClassificationCode,
                    YearlyCashSales = row.YearlyCashSales ?? 0,
                    YearlyCashSalesTax = row.YearlyCashSalesTax ?? 0
                };
                dictionary[item.ClassificationCode] = item;
            }
            
            return dictionary;
        }

        /// <summary>
        /// 年次仕入データを分類別に集計（、2項目のみ）
        /// </summary>
        private async Task<Dictionary<string, BusinessDailyReportItem>> GetYearlyPurchaseDataAsync(
            DateTime startDate, DateTime endDate)
        {
            using var connection = CreateConnection();
            
            const string sql = @"
                SELECT 
                    COALESCE(s.SupplierCategory1, '999') AS ClassificationCode,
                    SUM(CASE WHEN pv.VoucherType IN (11, 12) AND pv.DetailType IN (1,2,3,4) THEN pv.Amount ELSE 0 END) AS YearlyCashPurchase,
                    SUM(CASE WHEN pv.VoucherType IN (11, 12) AND pv.DetailType = 18 THEN pv.Amount ELSE 0 END) AS YearlyCashPurchaseTax
                FROM PurchaseVouchers pv
                LEFT JOIN SupplierMaster s ON pv.SupplierCode = s.SupplierCode
                WHERE pv.JobDate BETWEEN @StartDate AND @EndDate 
                  AND pv.IsActive = 1
                GROUP BY s.SupplierCategory1";

            var result = await connection.QueryAsync<dynamic>(sql, new { StartDate = startDate, EndDate = endDate });
            
            var dictionary = new Dictionary<string, BusinessDailyReportItem>();
            foreach (var row in result)
            {
                var item = new BusinessDailyReportItem
                {
                    ClassificationCode = row.ClassificationCode,
                    YearlyCashPurchase = row.YearlyCashPurchase ?? 0,
                    YearlyCashPurchaseTax = row.YearlyCashPurchaseTax ?? 0
                };
                dictionary[item.ClassificationCode] = item;
            }
            
            return dictionary;
        }

        /// <summary>
        /// 年次データをマージして完全なリストを作成
        /// </summary>
        private List<BusinessDailyReportItem> MergeYearlyData(
            Dictionary<string, BusinessDailyReportItem> salesData,
            Dictionary<string, BusinessDailyReportItem> purchaseData)
        {
            // すべての分類コードを収集
            var allCodes = new HashSet<string>();
            allCodes.UnionWith(salesData.Keys);
            allCodes.UnionWith(purchaseData.Keys);
            
            var result = new List<BusinessDailyReportItem>();
            
            foreach (var code in allCodes.OrderBy(c => c))
            {
                var item = new BusinessDailyReportItem
                {
                    ClassificationCode = code,
                    CustomerClassName = "",  // 後で別途取得
                    SupplierClassName = "",  // 後で別途取得
                };
                
                // 売上データをマージ
                if (salesData.TryGetValue(code, out var sales))
                {
                    item.YearlyCashSales = sales.YearlyCashSales;
                    item.YearlyCashSalesTax = sales.YearlyCashSalesTax;
                }
                
                // 仕入データをマージ
                if (purchaseData.TryGetValue(code, out var purchase))
                {
                    item.YearlyCashPurchase = purchase.YearlyCashPurchase;
                    item.YearlyCashPurchaseTax = purchase.YearlyCashPurchaseTax;
                }
                
                result.Add(item);
            }
            
            // 合計行（000）を追加
            var totalItem = new BusinessDailyReportItem
            {
                ClassificationCode = "000",
                CustomerClassName = "合計",
                SupplierClassName = "合計",
                YearlyCashSales = result.Sum(r => r.YearlyCashSales ?? 0),
                YearlyCashSalesTax = result.Sum(r => r.YearlyCashSalesTax ?? 0),
                YearlyCashPurchase = result.Sum(r => r.YearlyCashPurchase ?? 0),
                YearlyCashPurchaseTax = result.Sum(r => r.YearlyCashPurchaseTax ?? 0)
            };
            
            result.Insert(0, totalItem);
            
            return result;
        }

        public async Task<List<BusinessDailyReportItem>> GetYearlyDataAsync(DateTime jobDate)
        {
            try
            {
                _logger.LogInformation("年次データ集計を開始: JobDate={JobDate}", jobDate);
                
                // 年度初めから前日までの累計を取得
                var startDate = new DateTime(jobDate.Year, 4, 1); // 4月開始の会計年度
                if (jobDate.Month < 4)
                {
                    startDate = new DateTime(jobDate.Year - 1, 4, 1);
                }
                var endDate = jobDate.AddDays(-1);
                
                // 前日までのデータがない場合は空リストを返す
                if (endDate < startDate)
                {
                    _logger.LogInformation("年度初のため年次データなし");
                    return new List<BusinessDailyReportItem>();
                }
                
                // 売上と仕入を個別に集計（年計は4項目のみ）
                var salesTask = GetYearlySalesDataAsync(startDate, endDate);
                var purchaseTask = GetYearlyPurchaseDataAsync(startDate, endDate);
                
                // すべての集計を待機
                await Task.WhenAll(salesTask, purchaseTask);
                
                _logger.LogInformation("個別集計完了 - 売上:{SalesCount}, 仕入:{PurchaseCount}",
                    salesTask.Result.Count, purchaseTask.Result.Count);
                
                // データをマージ
                var result = MergeYearlyData(salesTask.Result, purchaseTask.Result);
                
                // 分類名を設定（新しいconnectionを作成）
                using var connection = CreateConnection();
                await SetClassificationNamesAsync(connection, result);
                
                _logger.LogInformation("年次データ集計完了: {Count}件", result.Count);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "年次データ集計でエラーが発生しました");
                throw;
            }
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