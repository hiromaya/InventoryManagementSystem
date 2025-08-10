#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FastReport;
using FastReport.Export.Pdf;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.Models;
using Microsoft.Extensions.Logging;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    /// <summary>
    /// 営業日報FastReportサービス - 完全パラメータ方式（スクリプトレス）
    /// 4ページ固定レイアウト（1ページ目:合計+分類01-08、2-4ページ目:分類09-17,18-26,27-35）
    /// </summary>
    public class BusinessDailyReportFastReportService : 
        InventorySystem.Reports.Interfaces.IBusinessDailyReportService, 
        InventorySystem.Core.Interfaces.IBusinessDailyReportReportService
    {
        private readonly ILogger<BusinessDailyReportFastReportService> _logger;
        private readonly IBusinessDailyReportRepository _repository;
        private readonly string _templatePath;

        public BusinessDailyReportFastReportService(
            ILogger<BusinessDailyReportFastReportService> logger,
            IBusinessDailyReportRepository repository)
        {
            _logger = logger;
            _repository = repository;
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "BusinessDailyReport.frx");
            
            _logger.LogInformation("営業日報テンプレートパス: {Path}", _templatePath);
        }

        public async Task<byte[]> GenerateBusinessDailyReportAsync(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            try
            {
                _logger.LogInformation("営業日報PDF生成を開始します: JobDate={JobDate}", jobDate);

                // 月計・年計データの取得
                var monthlyData = await _repository.GetMonthlyDataAsync(jobDate);
                var yearlyData = await _repository.GetYearlyDataAsync(jobDate);
                
                // パラメータ方式でPDF生成
                return GenerateParameterBasedPdf(items, monthlyData, yearlyData, jobDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "営業日報PDF生成中にエラーが発生しました");
                throw;
            }
        }

        // 同期版（既存インターフェース対応）
        public byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            return GenerateBusinessDailyReportAsync(items, jobDate).GetAwaiter().GetResult();
        }

        public byte[] GenerateBusinessDailyReport(IEnumerable<object> businessDailyReportItems, DateTime jobDate)
        {
            var items = businessDailyReportItems.Cast<BusinessDailyReportItem>();
            return GenerateBusinessDailyReport(items, jobDate);
        }

        /// <summary>
        /// パラメータ方式でPDFを生成
        /// </summary>
        private byte[] GenerateParameterBasedPdf(
            IEnumerable<BusinessDailyReportItem> dailyItems,
            IEnumerable<BusinessDailyReportItem> monthlyItems,
            IEnumerable<BusinessDailyReportItem> yearlyItems,
            DateTime jobDate)
        {
            if (!File.Exists(_templatePath))
            {
                throw new FileNotFoundException($"営業日報テンプレートが見つかりません: {_templatePath}");
            }

            using var report = new FR.Report();
            report.Load(_templatePath);
            SetScriptLanguageToNone(report);

            var dailyList = dailyItems.ToList();
            var monthlyList = monthlyItems.ToList();
            var yearlyList = yearlyItems.ToList();

            // 基本パラメータ設定
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));

            // 分類名設定（35分類分）
            SetClassificationNames(report, dailyList);

            // 4ページ分のデータパラメータを設定
            SetPage1Parameters(report, dailyList, monthlyList, yearlyList); // 合計+分類01-08
            SetPage2Parameters(report, dailyList, monthlyList, yearlyList); // 分類09-17
            SetPage3Parameters(report, dailyList, monthlyList, yearlyList); // 分類18-26
            SetPage4Parameters(report, dailyList, monthlyList, yearlyList); // 分類27-35

            _logger.LogInformation("レポートを準備中...");
            report.Prepare();

            return ExportToPdf(report, jobDate);
        }

        /// <summary>
        /// ページ1のパラメータを設定（合計 + 分類01～08の9列）
        /// </summary>
        private void SetPage1Parameters(
            FR.Report report, 
            List<BusinessDailyReportItem> dailyList,
            List<BusinessDailyReportItem> monthlyList,
            List<BusinessDailyReportItem> yearlyList)
        {
            // 合計データ取得
            var totalDaily = dailyList.FirstOrDefault(x => x.ClassificationCode == "000");
            var totalMonthly = monthlyList.FirstOrDefault(x => x.ClassificationCode == "000");
            var totalYearly = yearlyList.FirstOrDefault(x => x.ClassificationCode == "000");

            // 日計データのパラメータ設定
            SetDailyParameters(report, "P1", totalDaily, dailyList, 1, 8, true);
            
            // 月計データのパラメータ設定
            SetMonthlyParameters(report, "P1", totalMonthly, monthlyList, 1, 8, true);
            
            // 年計データのパラメータ設定
            SetYearlyParameters(report, "P1", totalYearly, yearlyList, 1, 8, true);
        }

        /// <summary>
        /// ページ2のパラメータを設定（分類09～17の9列）
        /// </summary>
        private void SetPage2Parameters(
            FR.Report report,
            List<BusinessDailyReportItem> dailyList,
            List<BusinessDailyReportItem> monthlyList,
            List<BusinessDailyReportItem> yearlyList)
        {
            SetDailyParameters(report, "P2", null, dailyList, 9, 17, false);
            SetMonthlyParameters(report, "P2", null, monthlyList, 9, 17, false);
            SetYearlyParameters(report, "P2", null, yearlyList, 9, 17, false);
        }

        /// <summary>
        /// ページ3のパラメータを設定（分類18～26の9列）
        /// </summary>
        private void SetPage3Parameters(
            FR.Report report,
            List<BusinessDailyReportItem> dailyList,
            List<BusinessDailyReportItem> monthlyList,
            List<BusinessDailyReportItem> yearlyList)
        {
            SetDailyParameters(report, "P3", null, dailyList, 18, 26, false);
            SetMonthlyParameters(report, "P3", null, monthlyList, 18, 26, false);
            SetYearlyParameters(report, "P3", null, yearlyList, 18, 26, false);
        }

        /// <summary>
        /// ページ4のパラメータを設定（分類27～35の9列）
        /// </summary>
        private void SetPage4Parameters(
            FR.Report report,
            List<BusinessDailyReportItem> dailyList,
            List<BusinessDailyReportItem> monthlyList,
            List<BusinessDailyReportItem> yearlyList)
        {
            SetDailyParameters(report, "P4", null, dailyList, 27, 35, false);
            SetMonthlyParameters(report, "P4", null, monthlyList, 27, 35, false);
            SetYearlyParameters(report, "P4", null, yearlyList, 27, 35, false);
        }

        /// <summary>
        /// 日計パラメータを設定（18項目）
        /// </summary>
        private void SetDailyParameters(
            FR.Report report, 
            string pagePrefix, 
            BusinessDailyReportItem totalData,
            List<BusinessDailyReportItem> allData,
            int startClass, 
            int endClass, 
            bool includeTotal)
        {
            // 18項目の日計データ設定
            var items = new[]
            {
                new { Name = "CashSales", Property = nameof(BusinessDailyReportItem.DailyCashSales), IsSum = false },
                new { Name = "CashSalesTax", Property = nameof(BusinessDailyReportItem.DailyCashSalesTax), IsSum = false },
                new { Name = "CreditSales", Property = nameof(BusinessDailyReportItem.DailyCreditSales), IsSum = false },
                new { Name = "SalesDiscount", Property = nameof(BusinessDailyReportItem.DailySalesDiscount), IsSum = false },
                new { Name = "CreditSalesTax", Property = nameof(BusinessDailyReportItem.DailyCreditSalesTax), IsSum = false },
                new { Name = "SalesSum", Property = "", IsSum = true }, // 計算項目
                new { Name = "CashPurchase", Property = nameof(BusinessDailyReportItem.DailyCashPurchase), IsSum = false },
                new { Name = "CashPurchaseTax", Property = nameof(BusinessDailyReportItem.DailyCashPurchaseTax), IsSum = false },
                new { Name = "CreditPurchase", Property = nameof(BusinessDailyReportItem.DailyCreditPurchase), IsSum = false },
                new { Name = "PurchaseDiscount", Property = nameof(BusinessDailyReportItem.DailyPurchaseDiscount), IsSum = false },
                new { Name = "CreditPurchaseTax", Property = nameof(BusinessDailyReportItem.DailyCreditPurchaseTax), IsSum = false },
                new { Name = "PurchaseSum", Property = "", IsSum = true }, // 計算項目
                new { Name = "CashReceipt", Property = nameof(BusinessDailyReportItem.DailyCashReceipt), IsSum = false },
                new { Name = "BankReceipt", Property = nameof(BusinessDailyReportItem.DailyBankReceipt), IsSum = false },
                new { Name = "OtherReceipt", Property = nameof(BusinessDailyReportItem.DailyOtherReceipt), IsSum = false },
                new { Name = "ReceiptSum", Property = "", IsSum = true }, // 計算項目
                new { Name = "CashPayment", Property = nameof(BusinessDailyReportItem.DailyCashPayment), IsSum = false },
                new { Name = "BankPayment", Property = nameof(BusinessDailyReportItem.DailyBankPayment), IsSum = false },
                new { Name = "OtherPayment", Property = nameof(BusinessDailyReportItem.DailyOtherPayment), IsSum = false },
                new { Name = "PaymentSum", Property = "", IsSum = true } // 計算項目
            };

            foreach (var item in items)
            {
                // 合計列（ページ1のみ）
                if (includeTotal)
                {
                    var totalValue = item.IsSum 
                        ? CalculateSummaryValue(totalData, item.Name)
                        : GetPropertyValue(totalData, item.Property);
                    report.SetParameterValue($"{pagePrefix}_Daily_{item.Name}_Total", FormatNumber(totalValue));
                }

                // 各分類列
                for (int classNum = startClass; classNum <= endClass; classNum++)
                {
                    var classCode = classNum.ToString("D3");
                    var classData = allData.FirstOrDefault(x => x.ClassificationCode == classCode);
                    
                    var classValue = item.IsSum 
                        ? CalculateSummaryValue(classData, item.Name)
                        : GetPropertyValue(classData, item.Property);
                        
                    report.SetParameterValue($"{pagePrefix}_Daily_{item.Name}_C{classNum:D2}", FormatNumber(classValue));
                }
            }
        }

        /// <summary>
        /// 月計パラメータを設定（18項目）
        /// </summary>
        private void SetMonthlyParameters(
            FR.Report report,
            string pagePrefix,
            BusinessDailyReportItem totalData,
            List<BusinessDailyReportItem> allData,
            int startClass,
            int endClass,
            bool includeTotal)
        {
            // 月計は日計と同じ18項目構成
            var items = new[]
            {
                new { Name = "CashSales", Property = nameof(BusinessDailyReportItem.MonthlyCashSales), IsSum = false },
                new { Name = "CashSalesTax", Property = nameof(BusinessDailyReportItem.MonthlyCashSalesTax), IsSum = false },
                new { Name = "CreditSales", Property = nameof(BusinessDailyReportItem.MonthlyCreditSales), IsSum = false },
                new { Name = "SalesDiscount", Property = nameof(BusinessDailyReportItem.MonthlySalesDiscount), IsSum = false },
                new { Name = "CreditSalesTax", Property = nameof(BusinessDailyReportItem.MonthlyCreditSalesTax), IsSum = false },
                new { Name = "SalesSum", Property = "", IsSum = true },
                new { Name = "CashPurchase", Property = nameof(BusinessDailyReportItem.MonthlyCashPurchase), IsSum = false },
                new { Name = "CashPurchaseTax", Property = nameof(BusinessDailyReportItem.MonthlyCashPurchaseTax), IsSum = false },
                new { Name = "CreditPurchase", Property = nameof(BusinessDailyReportItem.MonthlyCreditPurchase), IsSum = false },
                new { Name = "PurchaseDiscount", Property = nameof(BusinessDailyReportItem.MonthlyPurchaseDiscount), IsSum = false },
                new { Name = "CreditPurchaseTax", Property = nameof(BusinessDailyReportItem.MonthlyCreditPurchaseTax), IsSum = false },
                new { Name = "PurchaseSum", Property = "", IsSum = true },
                new { Name = "CashReceipt", Property = nameof(BusinessDailyReportItem.MonthlyCashReceipt), IsSum = false },
                new { Name = "BankReceipt", Property = nameof(BusinessDailyReportItem.MonthlyBankReceipt), IsSum = false },
                new { Name = "OtherReceipt", Property = nameof(BusinessDailyReportItem.MonthlyOtherReceipt), IsSum = false },
                new { Name = "ReceiptSum", Property = "", IsSum = true },
                new { Name = "CashPayment", Property = nameof(BusinessDailyReportItem.MonthlyCashPayment), IsSum = false },
                new { Name = "BankPayment", Property = nameof(BusinessDailyReportItem.MonthlyBankPayment), IsSum = false },
                new { Name = "OtherPayment", Property = nameof(BusinessDailyReportItem.MonthlyOtherPayment), IsSum = false },
                new { Name = "PaymentSum", Property = "", IsSum = true }
            };

            foreach (var item in items)
            {
                // 合計列（ページ1のみ）
                if (includeTotal)
                {
                    var totalValue = item.IsSum 
                        ? CalculateMonthlySummaryValue(totalData, item.Name)
                        : GetPropertyValue(totalData, item.Property);
                    report.SetParameterValue($"{pagePrefix}_Monthly_{item.Name}_Total", FormatNumber(totalValue));
                }

                // 各分類列
                for (int classNum = startClass; classNum <= endClass; classNum++)
                {
                    var classCode = classNum.ToString("D3");
                    var classData = allData.FirstOrDefault(x => x.ClassificationCode == classCode);
                    
                    var classValue = item.IsSum 
                        ? CalculateMonthlySummaryValue(classData, item.Name)
                        : GetPropertyValue(classData, item.Property);
                        
                    report.SetParameterValue($"{pagePrefix}_Monthly_{item.Name}_C{classNum:D2}", FormatNumber(classValue));
                }
            }
        }

        /// <summary>
        /// 年計パラメータを設定（4項目のみ）
        /// </summary>
        private void SetYearlyParameters(
            FR.Report report,
            string pagePrefix,
            BusinessDailyReportItem totalData,
            List<BusinessDailyReportItem> allData,
            int startClass,
            int endClass,
            bool includeTotal)
        {
            // 年計は4項目のみ
            var items = new[]
            {
                new { Name = "Sales", Property = nameof(BusinessDailyReportItem.YearlyCashSales) },
                new { Name = "SalesTax", Property = nameof(BusinessDailyReportItem.YearlyCashSalesTax) },
                new { Name = "Purchase", Property = nameof(BusinessDailyReportItem.YearlyCashPurchase) },
                new { Name = "PurchaseTax", Property = nameof(BusinessDailyReportItem.YearlyCashPurchaseTax) }
            };

            foreach (var item in items)
            {
                // 合計列（ページ1のみ）
                if (includeTotal)
                {
                    var totalValue = GetPropertyValue(totalData, item.Property);
                    report.SetParameterValue($"{pagePrefix}_Yearly_{item.Name}_Total", FormatNumber(totalValue));
                }

                // 各分類列
                for (int classNum = startClass; classNum <= endClass; classNum++)
                {
                    var classCode = classNum.ToString("D3");
                    var classData = allData.FirstOrDefault(x => x.ClassificationCode == classCode);
                    
                    var classValue = GetPropertyValue(classData, item.Property);
                    report.SetParameterValue($"{pagePrefix}_Yearly_{item.Name}_C{classNum:D2}", FormatNumber(classValue));
                }
            }
        }

        /// <summary>
        /// 日計の合計値を計算
        /// </summary>
        private decimal CalculateSummaryValue(BusinessDailyReportItem item, string summaryType)
        {
            if (item == null) return 0;

            return summaryType switch
            {
                "SalesSum" => item.DailyCashSales + item.DailyCashSalesTax + 
                              item.DailyCreditSales + item.DailySalesDiscount + item.DailyCreditSalesTax,
                "PurchaseSum" => item.DailyCashPurchase + item.DailyCashPurchaseTax + 
                                 item.DailyCreditPurchase + item.DailyPurchaseDiscount + item.DailyCreditPurchaseTax,
                "ReceiptSum" => item.DailyCashReceipt + item.DailyBankReceipt + item.DailyOtherReceipt,
                "PaymentSum" => item.DailyCashPayment + item.DailyBankPayment + item.DailyOtherPayment,
                _ => 0
            };
        }

        /// <summary>
        /// 月計の合計値を計算
        /// </summary>
        private decimal CalculateMonthlySummaryValue(BusinessDailyReportItem item, string summaryType)
        {
            if (item == null) return 0;

            return summaryType switch
            {
                "SalesSum" => item.MonthlyCashSales + item.MonthlyCashSalesTax + 
                              item.MonthlyCreditSales + item.MonthlySalesDiscount + item.MonthlyCreditSalesTax,
                "PurchaseSum" => item.MonthlyCashPurchase + item.MonthlyCashPurchaseTax + 
                                 item.MonthlyCreditPurchase + item.MonthlyPurchaseDiscount + item.MonthlyCreditPurchaseTax,
                "ReceiptSum" => item.MonthlyCashReceipt + item.MonthlyBankReceipt + item.MonthlyOtherReceipt,
                "PaymentSum" => item.MonthlyCashPayment + item.MonthlyBankPayment + item.MonthlyOtherPayment,
                _ => 0
            };
        }

        /// <summary>
        /// 分類名をレポートパラメータに設定
        /// </summary>
        private void SetClassificationNames(FR.Report report, List<BusinessDailyReportItem> itemList)
        {
            // 分類01～35の分類名を設定
            for (int i = 1; i <= 35; i++)
            {
                var classCode = i.ToString("D3");
                var item = itemList.FirstOrDefault(x => x.ClassificationCode == classCode);
                
                var customerName = TruncateToLength(item?.CustomerClassName ?? "", 6);
                var supplierName = TruncateToLength(item?.SupplierClassName ?? "", 6);
                
                report.SetParameterValue($"CustomerName{i:D2}", customerName);
                report.SetParameterValue($"SupplierName{i:D2}", supplierName);
            }
        }

        /// <summary>
        /// プロパティ値を取得
        /// </summary>
        private decimal GetPropertyValue(BusinessDailyReportItem item, string propertyName)
        {
            if (item == null || string.IsNullOrEmpty(propertyName))
                return 0;

            var property = typeof(BusinessDailyReportItem).GetProperty(propertyName);
            var value = property?.GetValue(item);
            
            return value is decimal decValue ? decValue : 0;
        }

        /// <summary>
        /// 数値フォーマット（ゼロ→空文字、マイナス→▲記号、カンマ区切り）
        /// </summary>
        private string FormatNumber(decimal value)
        {
            if (value == 0) return "";
            return value < 0 ? $"▲{Math.Abs(value):N0}" : value.ToString("N0");
        }

        /// <summary>
        /// 文字列を指定長に切り詰め
        /// </summary>
        private string TruncateToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }

        /// <summary>
        /// ScriptLanguageをNoneに設定
        /// </summary>
        private void SetScriptLanguageToNone(FR.Report report)
        {
            try
            {
                var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                if (scriptLanguageProperty != null)
                {
                    var scriptLanguageType = scriptLanguageProperty.PropertyType;
                    if (scriptLanguageType.IsEnum)
                    {
                        var noneValue = Enum.GetValues(scriptLanguageType)
                            .Cast<object>()
                            .FirstOrDefault(v => v.ToString() == "None");

                        if (noneValue != null)
                        {
                            scriptLanguageProperty.SetValue(report, noneValue);
                            _logger.LogDebug("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }

                // Scriptプロパティもnullに設定
                var scriptProperty = report.GetType().GetProperty("Script", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScriptLanguage設定時の警告");
            }
        }

        /// <summary>
        /// PDFエクスポート
        /// </summary>
        private byte[] ExportToPdf(FR.Report report, DateTime jobDate)
        {
            using var pdfExport = new PDFExport
            {
                EmbeddingFonts = true,
                Title = $"営業日報_{jobDate:yyyyMMdd}",
                Subject = "営業日報",
                Creator = "在庫管理システム",
                Author = "在庫管理システム",
                TextInCurves = false,
                JpegQuality = 95,
                OpenAfterExport = false
            };

            using var stream = new MemoryStream();
            report.Export(pdfExport, stream);

            var result = stream.ToArray();
            _logger.LogInformation("営業日報PDF生成完了: サイズ={Size}bytes", result.Length);

            return result;
        }
    }
}
#endif
#pragma warning restore CA1416