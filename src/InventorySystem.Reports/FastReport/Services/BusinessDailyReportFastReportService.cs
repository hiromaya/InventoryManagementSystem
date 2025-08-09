#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using FastReport;
using FastReport.Data;
using FastReport.Export.Pdf;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.Models;
using Microsoft.Extensions.Logging;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    /// <summary>
    /// 営業日報FastReportサービス - DataTable方式（スクリプトレス）
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
                
                // DataTable方式でPDF生成
                return GenerateDataTableBasedPdf(items, monthlyData, yearlyData, jobDate);
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
        /// DataTable方式でPDFを生成
        /// </summary>
        private byte[] GenerateDataTableBasedPdf(
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

            // 4ページ分のDataTableを作成
            var page1Data = CreatePage1DataTable(dailyItems, monthlyItems, yearlyItems); // 合計+分類01-08
            var page2Data = CreatePage2DataTable(dailyItems, monthlyItems, yearlyItems); // 分類09-17
            var page3Data = CreatePage3DataTable(dailyItems, monthlyItems, yearlyItems); // 分類18-26
            var page4Data = CreatePage4DataTable(dailyItems, monthlyItems, yearlyItems); // 分類27-35

            // FastReportにデータ登録
            report.RegisterData(page1Data, "Page1Data");
            report.RegisterData(page2Data, "Page2Data");
            report.RegisterData(page3Data, "Page3Data");
            report.RegisterData(page4Data, "Page4Data");

            // 基本パラメータ設定
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));

            // 分類名設定（35分類分）
            SetClassificationNames(report, dailyItems);

            _logger.LogInformation("レポートを準備中...");
            report.Prepare();

            return ExportToPdf(report, jobDate);
        }

        /// <summary>
        /// 1ページ目用DataTable作成（合計列 + 分類01～08の9列）
        /// </summary>
        private DataTable CreatePage1DataTable(
            IEnumerable<BusinessDailyReportItem> dailyItems,
            IEnumerable<BusinessDailyReportItem> monthlyItems,
            IEnumerable<BusinessDailyReportItem> yearlyItems)
        {
            var table = new DataTable("Page1Data");
            
            // カラム定義（9列：合計+分類01～08）
            table.Columns.Add("SectionName", typeof(string));
            table.Columns.Add("ItemName", typeof(string));
            table.Columns.Add("Total", typeof(string));
            for (int i = 1; i <= 8; i++)
            {
                table.Columns.Add($"Class{i:D2}", typeof(string));
            }
            table.Columns.Add("IsSummaryRow", typeof(bool));

            var dailyList = dailyItems.ToList();
            var monthlyList = monthlyItems.ToList();
            var yearlyList = yearlyItems.ToList();

            // 合計データ取得
            var totalDaily = dailyList.FirstOrDefault(x => x.ClassificationCode == "000");
            var totalMonthly = monthlyList.FirstOrDefault(x => x.ClassificationCode == "000");
            var totalYearly = yearlyList.FirstOrDefault(x => x.ClassificationCode == "000");

            // 分類01～08データ取得
            var class01To08 = new List<BusinessDailyReportItem>();
            for (int i = 1; i <= 8; i++)
            {
                var classCode = i.ToString("D3");
                class01To08.Add(dailyList.FirstOrDefault(x => x.ClassificationCode == classCode));
            }

            // 40行のデータ追加（18行日計 + 18行月計 + 4行年計）
            AddDailySectionToTable(table, totalDaily, class01To08, dailyList);
            AddMonthlySectionToTable(table, totalMonthly, class01To08, monthlyList);
            AddYearlySectionToTable(table, totalYearly, class01To08, yearlyList);

            return table;
        }

        /// <summary>
        /// 2ページ目用DataTable作成（分類09～17の9列）
        /// </summary>
        private DataTable CreatePage2DataTable(
            IEnumerable<BusinessDailyReportItem> dailyItems,
            IEnumerable<BusinessDailyReportItem> monthlyItems,
            IEnumerable<BusinessDailyReportItem> yearlyItems)
        {
            var table = new DataTable("Page2Data");
            
            // カラム定義（9列：分類09～17）
            table.Columns.Add("SectionName", typeof(string));
            table.Columns.Add("ItemName", typeof(string));
            for (int i = 9; i <= 17; i++)
            {
                table.Columns.Add($"Class{i:D2}", typeof(string));
            }
            table.Columns.Add("IsSummaryRow", typeof(bool));

            var dailyList = dailyItems.ToList();
            var monthlyList = monthlyItems.ToList();
            var yearlyList = yearlyItems.ToList();

            // 分類09～17データ取得
            var class09To17 = new List<BusinessDailyReportItem>();
            for (int i = 9; i <= 17; i++)
            {
                var classCode = i.ToString("D3");
                class09To17.Add(dailyList.FirstOrDefault(x => x.ClassificationCode == classCode));
            }

            // 40行のデータ追加
            AddDailySectionToPage2And3And4(table, class09To17, dailyList, 9);
            AddMonthlySectionToPage2And3And4(table, class09To17, monthlyList, 9);
            AddYearlySectionToPage2And3And4(table, class09To17, yearlyList, 9);

            return table;
        }

        /// <summary>
        /// 3ページ目用DataTable作成（分類18～26の9列）
        /// </summary>
        private DataTable CreatePage3DataTable(
            IEnumerable<BusinessDailyReportItem> dailyItems,
            IEnumerable<BusinessDailyReportItem> monthlyItems,
            IEnumerable<BusinessDailyReportItem> yearlyItems)
        {
            var table = new DataTable("Page3Data");
            
            // カラム定義（9列：分類18～26）
            table.Columns.Add("SectionName", typeof(string));
            table.Columns.Add("ItemName", typeof(string));
            for (int i = 18; i <= 26; i++)
            {
                table.Columns.Add($"Class{i:D2}", typeof(string));
            }
            table.Columns.Add("IsSummaryRow", typeof(bool));

            var dailyList = dailyItems.ToList();
            var monthlyList = monthlyItems.ToList();
            var yearlyList = yearlyItems.ToList();

            // 分類18～26データ取得
            var class18To26 = new List<BusinessDailyReportItem>();
            for (int i = 18; i <= 26; i++)
            {
                var classCode = i.ToString("D3");
                class18To26.Add(dailyList.FirstOrDefault(x => x.ClassificationCode == classCode));
            }

            // 40行のデータ追加
            AddDailySectionToPage2And3And4(table, class18To26, dailyList, 18);
            AddMonthlySectionToPage2And3And4(table, class18To26, monthlyList, 18);
            AddYearlySectionToPage2And3And4(table, class18To26, yearlyList, 18);

            return table;
        }

        /// <summary>
        /// 4ページ目用DataTable作成（分類27～35の9列）
        /// </summary>
        private DataTable CreatePage4DataTable(
            IEnumerable<BusinessDailyReportItem> dailyItems,
            IEnumerable<BusinessDailyReportItem> monthlyItems,
            IEnumerable<BusinessDailyReportItem> yearlyItems)
        {
            var table = new DataTable("Page4Data");
            
            // カラム定義（9列：分類27～35）
            table.Columns.Add("SectionName", typeof(string));
            table.Columns.Add("ItemName", typeof(string));
            for (int i = 27; i <= 35; i++)
            {
                table.Columns.Add($"Class{i:D2}", typeof(string));
            }
            table.Columns.Add("IsSummaryRow", typeof(bool));

            var dailyList = dailyItems.ToList();
            var monthlyList = monthlyItems.ToList();
            var yearlyList = yearlyItems.ToList();

            // 分類27～35データ取得
            var class27To35 = new List<BusinessDailyReportItem>();
            for (int i = 27; i <= 35; i++)
            {
                var classCode = i.ToString("D3");
                class27To35.Add(dailyList.FirstOrDefault(x => x.ClassificationCode == classCode));
            }

            // 40行のデータ追加
            AddDailySectionToPage2And3And4(table, class27To35, dailyList, 27);
            AddMonthlySectionToPage2And3And4(table, class27To35, monthlyList, 27);
            AddYearlySectionToPage2And3And4(table, class27To35, yearlyList, 27);

            return table;
        }

        /// <summary>
        /// 日計セクション（18行）をテーブルに追加
        /// </summary>
        private void AddDailySectionToTable(DataTable table, BusinessDailyReportItem totalData,
            List<BusinessDailyReportItem> classData, List<BusinessDailyReportItem> allData)
        {
            var dailyRows = GetDailyRowDefinitions();

            foreach (var rowDef in dailyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = rowDef.IsSum;

                if (rowDef.IsSum)
                {
                    // 合計行の計算
                    row["Total"] = CalculateSummaryValue(totalData, rowDef.SummaryType);
                    for (int i = 1; i <= 8; i++)
                    {
                        var classItem = classData[i - 1];
                        row[$"Class{i:D2}"] = CalculateSummaryValue(classItem, rowDef.SummaryType);
                    }
                }
                else
                {
                    // 通常行
                    row["Total"] = GetPropertyValueFormatted(totalData, rowDef.Property);
                    for (int i = 1; i <= 8; i++)
                    {
                        var classItem = classData[i - 1];
                        row[$"Class{i:D2}"] = GetPropertyValueFormatted(classItem, rowDef.Property);
                    }
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// 月計セクション（18行）をテーブルに追加
        /// </summary>
        private void AddMonthlySectionToTable(DataTable table, BusinessDailyReportItem totalData,
            List<BusinessDailyReportItem> classData, List<BusinessDailyReportItem> allData)
        {
            var monthlyRows = GetMonthlyRowDefinitions();

            foreach (var rowDef in monthlyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = rowDef.IsSum;

                if (rowDef.IsSum)
                {
                    // 合計行の計算
                    row["Total"] = CalculateSummaryValue(totalData, rowDef.SummaryType);
                    for (int i = 1; i <= 8; i++)
                    {
                        var classItem = classData[i - 1];
                        row[$"Class{i:D2}"] = CalculateSummaryValue(classItem, rowDef.SummaryType);
                    }
                }
                else
                {
                    // 通常行（月計データを使用）
                    row["Total"] = GetMonthlyPropertyValueFormatted(totalData, rowDef.Property);
                    for (int i = 1; i <= 8; i++)
                    {
                        var classItem = classData[i - 1];
                        row[$"Class{i:D2}"] = GetMonthlyPropertyValueFormatted(classItem, rowDef.Property);
                    }
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// 年計セクション（4行）をテーブルに追加
        /// </summary>
        private void AddYearlySectionToTable(DataTable table, BusinessDailyReportItem totalData,
            List<BusinessDailyReportItem> classData, List<BusinessDailyReportItem> allData)
        {
            var yearlyRows = GetYearlyRowDefinitions();

            foreach (var rowDef in yearlyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = false;

                // 年計データを使用
                row["Total"] = GetYearlyPropertyValueFormatted(totalData, rowDef.Property);
                for (int i = 1; i <= 8; i++)
                {
                    var classItem = classData[i - 1];
                    row[$"Class{i:D2}"] = GetYearlyPropertyValueFormatted(classItem, rowDef.Property);
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// 2-4ページ目用の日計セクション追加
        /// </summary>
        private void AddDailySectionToPage2And3And4(DataTable table, List<BusinessDailyReportItem> classData,
            List<BusinessDailyReportItem> allData, int startClassNum)
        {
            var dailyRows = GetDailyRowDefinitions();

            foreach (var rowDef in dailyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = rowDef.IsSum;

                if (rowDef.IsSum)
                {
                    // 合計行の計算
                    for (int i = 0; i < 9; i++)
                    {
                        var classItem = classData[i];
                        row[$"Class{startClassNum + i:D2}"] = CalculateSummaryValue(classItem, rowDef.SummaryType);
                    }
                }
                else
                {
                    // 通常行
                    for (int i = 0; i < 9; i++)
                    {
                        var classItem = classData[i];
                        row[$"Class{startClassNum + i:D2}"] = GetPropertyValueFormatted(classItem, rowDef.Property);
                    }
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// 2-4ページ目用の月計セクション追加
        /// </summary>
        private void AddMonthlySectionToPage2And3And4(DataTable table, List<BusinessDailyReportItem> classData,
            List<BusinessDailyReportItem> allData, int startClassNum)
        {
            var monthlyRows = GetMonthlyRowDefinitions();

            foreach (var rowDef in monthlyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = rowDef.IsSum;

                if (rowDef.IsSum)
                {
                    // 合計行の計算
                    for (int i = 0; i < 9; i++)
                    {
                        var classItem = classData[i];
                        row[$"Class{startClassNum + i:D2}"] = CalculateSummaryValue(classItem, rowDef.SummaryType);
                    }
                }
                else
                {
                    // 通常行
                    for (int i = 0; i < 9; i++)
                    {
                        var classItem = classData[i];
                        row[$"Class{startClassNum + i:D2}"] = GetMonthlyPropertyValueFormatted(classItem, rowDef.Property);
                    }
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// 2-4ページ目用の年計セクション追加
        /// </summary>
        private void AddYearlySectionToPage2And3And4(DataTable table, List<BusinessDailyReportItem> classData,
            List<BusinessDailyReportItem> allData, int startClassNum)
        {
            var yearlyRows = GetYearlyRowDefinitions();

            foreach (var rowDef in yearlyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = false;

                // 年計データを使用
                for (int i = 0; i < 9; i++)
                {
                    var classItem = classData[i];
                    row[$"Class{startClassNum + i:D2}"] = GetYearlyPropertyValueFormatted(classItem, rowDef.Property);
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// 日計行定義を取得
        /// </summary>
        private IEnumerable<dynamic> GetDailyRowDefinitions()
        {
            return new[]
            {
                new { Section = "【日計】", Item = "現金売上", Property = nameof(BusinessDailyReportItem.DailyCashSales), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "現売消費税", Property = nameof(BusinessDailyReportItem.DailyCashSalesTax), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "掛売上と返品", Property = nameof(BusinessDailyReportItem.DailyCreditSales), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "売上値引", Property = nameof(BusinessDailyReportItem.DailySalesDiscount), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "掛売消費税", Property = nameof(BusinessDailyReportItem.DailyCreditSalesTax), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "＊売上計＊", Property = "", IsSum = true, SummaryType = "Sales" },
                new { Section = "", Item = "現金仕入", Property = nameof(BusinessDailyReportItem.DailyCashPurchase), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "現仕消費税", Property = nameof(BusinessDailyReportItem.DailyCashPurchaseTax), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "掛仕入と返品", Property = nameof(BusinessDailyReportItem.DailyCreditPurchase), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "仕入値引", Property = nameof(BusinessDailyReportItem.DailyPurchaseDiscount), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "掛仕入消費税", Property = nameof(BusinessDailyReportItem.DailyCreditPurchaseTax), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "＊仕入計＊", Property = "", IsSum = true, SummaryType = "Purchase" },
                new { Section = "", Item = "入金と現売", Property = nameof(BusinessDailyReportItem.DailyCashReceipt), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "振込入金", Property = nameof(BusinessDailyReportItem.DailyBankReceipt), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "入金値引・その他", Property = nameof(BusinessDailyReportItem.DailyOtherReceipt), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "＊入金計＊", Property = "", IsSum = true, SummaryType = "Receipt" },
                new { Section = "", Item = "支払と現金支払", Property = nameof(BusinessDailyReportItem.DailyCashPayment), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "振込支払", Property = nameof(BusinessDailyReportItem.DailyBankPayment), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "支払値引・その他", Property = nameof(BusinessDailyReportItem.DailyOtherPayment), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "＊支払計＊", Property = "", IsSum = true, SummaryType = "Payment" }
            };
        }

        /// <summary>
        /// 月計行定義を取得
        /// </summary>
        private IEnumerable<dynamic> GetMonthlyRowDefinitions()
        {
            return new[]
            {
                new { Section = "【月計】", Item = "現金売上", Property = nameof(BusinessDailyReportItem.DailyCashSales), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "現売消費税", Property = nameof(BusinessDailyReportItem.DailyCashSalesTax), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "掛売上と返品", Property = nameof(BusinessDailyReportItem.DailyCreditSales), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "売上値引", Property = nameof(BusinessDailyReportItem.DailySalesDiscount), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "掛売消費税", Property = nameof(BusinessDailyReportItem.DailyCreditSalesTax), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "＊売上計＊", Property = "", IsSum = true, SummaryType = "Sales" },
                new { Section = "", Item = "現金仕入", Property = nameof(BusinessDailyReportItem.DailyCashPurchase), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "現仕消費税", Property = nameof(BusinessDailyReportItem.DailyCashPurchaseTax), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "掛仕入と返品", Property = nameof(BusinessDailyReportItem.DailyCreditPurchase), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "仕入値引", Property = nameof(BusinessDailyReportItem.DailyPurchaseDiscount), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "掛仕入消費税", Property = nameof(BusinessDailyReportItem.DailyCreditPurchaseTax), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "＊仕入計＊", Property = "", IsSum = true, SummaryType = "Purchase" },
                new { Section = "", Item = "入金と現売", Property = nameof(BusinessDailyReportItem.DailyCashReceipt), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "振込入金", Property = nameof(BusinessDailyReportItem.DailyBankReceipt), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "入金値引・その他", Property = nameof(BusinessDailyReportItem.DailyOtherReceipt), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "＊入金計＊", Property = "", IsSum = true, SummaryType = "Receipt" },
                new { Section = "", Item = "支払と現金支払", Property = nameof(BusinessDailyReportItem.DailyCashPayment), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "振込支払", Property = nameof(BusinessDailyReportItem.DailyBankPayment), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "支払値引・その他", Property = nameof(BusinessDailyReportItem.DailyOtherPayment), IsSum = false, SummaryType = "" },
                new { Section = "", Item = "＊支払計＊", Property = "", IsSum = true, SummaryType = "Payment" }
            };
        }

        /// <summary>
        /// 年計行定義を取得
        /// </summary>
        private IEnumerable<dynamic> GetYearlyRowDefinitions()
        {
            return new[]
            {
                new { Section = "【年計】", Item = "売上", Property = nameof(BusinessDailyReportItem.DailyCashSales) },
                new { Section = "", Item = "売上消費税", Property = nameof(BusinessDailyReportItem.DailyCashSalesTax) },
                new { Section = "", Item = "仕入", Property = nameof(BusinessDailyReportItem.DailyCashPurchase) },
                new { Section = "", Item = "仕入消費税", Property = nameof(BusinessDailyReportItem.DailyCashPurchaseTax) }
            };
        }

        /// <summary>
        /// 合計値を計算
        /// </summary>
        private string CalculateSummaryValue(BusinessDailyReportItem item, string summaryType)
        {
            if (item == null || string.IsNullOrEmpty(summaryType))
                return "";

            decimal total = summaryType switch
            {
                "Sales" => item.DailyCashSales + item.DailyCashSalesTax + 
                          item.DailyCreditSales + item.DailySalesDiscount + item.DailyCreditSalesTax,
                "Purchase" => item.DailyCashPurchase + item.DailyCashPurchaseTax + 
                             item.DailyCreditPurchase + item.DailyPurchaseDiscount + item.DailyCreditPurchaseTax,
                "Receipt" => item.DailyCashReceipt + item.DailyBankReceipt + item.DailyOtherReceipt,
                "Payment" => item.DailyCashPayment + item.DailyBankPayment + item.DailyOtherPayment,
                _ => 0
            };

            return FormatNumber(total);
        }

        /// <summary>
        /// 分類名をレポートパラメータに設定
        /// </summary>
        private void SetClassificationNames(FR.Report report, IEnumerable<BusinessDailyReportItem> items)
        {
            var itemList = items.ToList();

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
        /// プロパティ値を取得してフォーマット（日計用）
        /// </summary>
        private string GetPropertyValueFormatted(BusinessDailyReportItem item, string propertyName)
        {
            if (item == null || string.IsNullOrEmpty(propertyName))
                return "";

            var property = typeof(BusinessDailyReportItem).GetProperty(propertyName);
            var value = property?.GetValue(item);
            
            if (value is decimal decValue)
            {
                return FormatNumber(decValue);
            }
            
            return "";
        }

        /// <summary>
        /// プロパティ値を取得してフォーマット（月計用）
        /// </summary>
        private string GetMonthlyPropertyValueFormatted(BusinessDailyReportItem item, string propertyName)
        {
            if (item == null || string.IsNullOrEmpty(propertyName))
                return "";

            // 月計用プロパティ名にマッピング
            var monthlyPropertyName = propertyName.Replace("Daily", "Monthly");
            var property = typeof(BusinessDailyReportItem).GetProperty(monthlyPropertyName);
            var value = property?.GetValue(item);
            
            if (value is decimal decValue)
            {
                return FormatNumber(decValue);
            }
            
            return "";
        }

        /// <summary>
        /// プロパティ値を取得してフォーマット（年計用）
        /// </summary>
        private string GetYearlyPropertyValueFormatted(BusinessDailyReportItem item, string propertyName)
        {
            if (item == null || string.IsNullOrEmpty(propertyName))
                return "";

            // 年計用プロパティ名にマッピング
            var yearlyPropertyName = propertyName.Replace("Daily", "Yearly");
            var property = typeof(BusinessDailyReportItem).GetProperty(yearlyPropertyName);
            var value = property?.GetValue(item);
            
            if (value is decimal decValue)
            {
                return FormatNumber(decValue);
            }
            
            return "";
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