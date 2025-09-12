using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Console
{
    public static class TestBusinessDailyReport
    {
        public static async Task RunTest(IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            var reportService = services.GetRequiredService<IBusinessDailyReportReportService>();

            logger.LogCritical("===== 営業日報テストデータ生成開始 =====");

            // テストデータ作成
            var testItems = new List<BusinessDailyReportItem>
            {
                new BusinessDailyReportItem
                {
                    ClassificationCode = "000",
                    CustomerClassName = "合計",
                    SupplierClassName = "合計",
                    DailyCashSales = 100000,
                    DailyCashSalesTax = 10000,
                    DailyCreditSales = 200000,
                    DailySalesDiscount = 5000,
                    DailyCreditSalesTax = 20000,
                    DailyCashPurchase = 50000,
                    DailyCashPurchaseTax = 5000,
                    DailyCreditPurchase = 80000,
                    DailyPurchaseDiscount = 2000,
                    DailyCreditPurchaseTax = 8000,
                    DailyCashReceipt = 30000,
                    DailyBankReceipt = 40000,
                    DailyOtherReceipt = 5000,
                    DailyCashPayment = 25000,
                    DailyBankPayment = 35000,
                    DailyOtherPayment = 3000
                },
                new BusinessDailyReportItem
                {
                    ClassificationCode = "001",
                    CustomerClassName = "テスト得意先01",
                    SupplierClassName = "テスト仕入先01",
                    DailyCashSales = 10000,
                    DailyCashSalesTax = 1000,
                    DailyCreditSales = 20000,
                    DailySalesDiscount = 500,
                    DailyCreditSalesTax = 2000,
                    DailyCashPurchase = 5000,
                    DailyCashPurchaseTax = 500,
                    DailyCreditPurchase = 8000,
                    DailyPurchaseDiscount = 200,
                    DailyCreditPurchaseTax = 800,
                    DailyCashReceipt = 3000,
                    DailyBankReceipt = 4000,
                    DailyOtherReceipt = 500,
                    DailyCashPayment = 2500,
                    DailyBankPayment = 3500,
                    DailyOtherPayment = 300
                },
                new BusinessDailyReportItem
                {
                    ClassificationCode = "002",
                    CustomerClassName = "テスト得意先02",
                    SupplierClassName = "テスト仕入先02",
                    DailyCashSales = 15000,
                    DailyCashSalesTax = 1500,
                    DailyCreditSales = 25000,
                    DailySalesDiscount = 750,
                    DailyCreditSalesTax = 2500,
                    DailyCashPurchase = 7500,
                    DailyCashPurchaseTax = 750,
                    DailyCreditPurchase = 12000,
                    DailyPurchaseDiscount = 300,
                    DailyCreditPurchaseTax = 1200,
                    DailyCashReceipt = 4500,
                    DailyBankReceipt = 6000,
                    DailyOtherReceipt = 750,
                    DailyCashPayment = 3750,
                    DailyBankPayment = 5250,
                    DailyOtherPayment = 450
                }
            };

            logger.LogCritical("テストデータ作成完了: {Count}件", testItems.Count);
            foreach (var item in testItems)
            {
                logger.LogCritical("テストデータ: {Code} - {Customer} / {Supplier} - 現金売上: {Cash}",
                    item.ClassificationCode, item.CustomerClassName, item.SupplierClassName, item.DailyCashSales);
            }

            try
            {
                var testDate = DateTime.Today;
                logger.LogCritical("PDF生成開始: JobDate={Date}", testDate);

                var pdfBytes = reportService.GenerateBusinessDailyReport(testItems, testDate);

                var outputPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    $"test_business_daily_report_{DateTime.Now:yyyyMMddHHmmss}.pdf");

                await File.WriteAllBytesAsync(outputPath, pdfBytes);

                logger.LogCritical("テスト営業日報生成完了: {Path}, サイズ: {Size}bytes", outputPath, pdfBytes.Length);

                // ファイルの存在確認
                if (File.Exists(outputPath))
                {
                    var fileInfo = new FileInfo(outputPath);
                    logger.LogCritical("ファイル確認: 存在={Exists}, サイズ={Size}bytes", fileInfo.Exists, fileInfo.Length);
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "テスト営業日報生成エラー: {Message}", ex.Message);
                throw;
            }

            logger.LogCritical("===== 営業日報テストデータ生成終了 =====");
        }
    }
}