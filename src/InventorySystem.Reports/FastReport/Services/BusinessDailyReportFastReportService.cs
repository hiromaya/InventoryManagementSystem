#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
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

                // 1. 月計・年計データの取得
                var monthlyData = await _repository.GetMonthlyDataAsync(jobDate);
                var yearlyData = await _repository.GetYearlyDataAsync(jobDate);
                
                // 2. 4ページ分のデータ構造を作成
                var pages = CreatePageData(items, monthlyData, yearlyData, jobDate);
                
                // 3. FastReportで4ページ分を生成
                return GenerateMultiPagePdf(pages, jobDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "営業日報PDF生成中にエラーが発生しました");
                throw;
            }
        }

        // 同期版も残しておく（既存インターフェース対応）
        public byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            return GenerateBusinessDailyReportAsync(items, jobDate).GetAwaiter().GetResult();
        }

        private List<BusinessDailyReportPage> CreatePageData(
            IEnumerable<BusinessDailyReportItem> dailyItems,
            IEnumerable<BusinessDailyReportItem> monthlyItems,
            IEnumerable<BusinessDailyReportItem> yearlyItems,
            DateTime jobDate)
        {
            var pages = new List<BusinessDailyReportPage>();
            var dailyList = dailyItems.ToList();
            var monthlyList = monthlyItems.ToList();
            var yearlyList = yearlyItems.ToList();
            
            _logger.LogInformation("4ページ分のデータ構造を作成中...");
            
            // 4ページ分のループ
            for (int pageNo = 1; pageNo <= 4; pageNo++)
            {
                var page = new BusinessDailyReportPage
                {
                    PageNumber = pageNo,
                    PageTitle = $"営業日報（{GetKanjiNumber(pageNo)}）"
                };
                
                // 該当ページの分類範囲を計算
                int startClass = (pageNo - 1) * 9 + 1;
                int endClass = Math.Min(pageNo * 9, 35);
                
                // 分類名を設定
                for (int i = startClass; i <= endClass; i++)
                {
                    var classCode = $"{i:000}";
                    var dailyItem = dailyList.FirstOrDefault(x => x.ClassificationCode == classCode);
                    page.CustomerClassNames.Add(dailyItem?.CustomerClassName ?? $"得意分類{i:00}");
                    page.SupplierClassNames.Add(dailyItem?.SupplierClassName ?? $"仕入分類{i:00}");
                }
                
                // 【日計】セクション（16項目 + 合計行4行）
                AddDailySection(page, dailyList, startClass, endClass);
                
                // 【月計】セクション（16項目 + 合計行4行）
                AddMonthlySection(page, monthlyList, startClass, endClass);
                
                // 【年計】セクション（4項目のみ）
                AddYearlySection(page, yearlyList, startClass, endClass);
                
                pages.Add(page);
            }
            
            return pages;
        }

        private void AddDailySection(
            BusinessDailyReportPage page, 
            IEnumerable<BusinessDailyReportItem> items,
            int startClass, int endClass)
        {
            var rows = new List<BusinessDailyReportRow>();
            
            // 売上関連5項目
            rows.Add(CreateDataRow("【日計】", "現金売上", items, startClass, endClass, 
                item => item.DailyCashSales));
            rows.Add(CreateDataRow("", "現売消費税", items, startClass, endClass, 
                item => item.DailyCashSalesTax));
            rows.Add(CreateDataRow("", "掛売上と返品", items, startClass, endClass, 
                item => item.DailyCreditSales));
            rows.Add(CreateDataRow("", "売上値引", items, startClass, endClass, 
                item => item.DailySalesDiscount));
            rows.Add(CreateDataRow("", "掛売消費税", items, startClass, endClass, 
                item => item.DailyCreditSalesTax));
            
            // 売上計（合計行）
            rows.Add(CreateSummaryRow("", "＊売上計＊", rows.TakeLast(5)));
            
            // 仕入関連5項目
            rows.Add(CreateDataRow("", "現金仕入", items, startClass, endClass, 
                item => item.DailyCashPurchase));
            rows.Add(CreateDataRow("", "現仕消費税", items, startClass, endClass, 
                item => item.DailyCashPurchaseTax));
            rows.Add(CreateDataRow("", "掛仕入と返品", items, startClass, endClass, 
                item => item.DailyCreditPurchase));
            rows.Add(CreateDataRow("", "仕入値引", items, startClass, endClass, 
                item => item.DailyPurchaseDiscount));
            rows.Add(CreateDataRow("", "掛仕入消費税", items, startClass, endClass, 
                item => item.DailyCreditPurchaseTax));
            
            // 仕入計（合計行）
            rows.Add(CreateSummaryRow("", "＊仕入計＊", rows.Skip(6).Take(5)));
            
            // 入金関連3項目
            rows.Add(CreateDataRow("", "入金と現売", items, startClass, endClass, 
                item => item.DailyCashReceipt + item.DailyCashSales));  // 現金売上を加算
            rows.Add(CreateDataRow("", "入金値引・他", items, startClass, endClass, 
                item => item.DailyOtherReceipt));
            
            // 入金計（合計行）
            rows.Add(CreateSummaryRow("", "＊入金計＊", rows.Skip(12).Take(2)));
            
            // 支払関連3項目
            rows.Add(CreateDataRow("", "支払と現金支払", items, startClass, endClass, 
                item => item.DailyCashPayment + item.DailyCashPurchase));  // 現金仕入を加算
            rows.Add(CreateDataRow("", "支払値引・他", items, startClass, endClass, 
                item => item.DailyOtherPayment));
            
            // 支払計（合計行）
            rows.Add(CreateSummaryRow("", "＊支払計＊", rows.Skip(15).Take(2)));
            
            page.Rows.AddRange(rows);
        }

        private void AddMonthlySection(
            BusinessDailyReportPage page,
            IEnumerable<BusinessDailyReportItem> items,
            int startClass, int endClass)
        {
            var rows = new List<BusinessDailyReportRow>();
            
            // 月計は日計と同じ構造だが、項目名に【月計】を付ける
            rows.Add(CreateDataRow("【月計】", "現金売上", items, startClass, endClass, 
                item => item.DailyCashSales));  // 月計データの場合、Dailyではなく適切なプロパティを使用
            rows.Add(CreateDataRow("", "現売消費税", items, startClass, endClass, 
                item => item.DailyCashSalesTax));
            rows.Add(CreateDataRow("", "掛売上と返品", items, startClass, endClass, 
                item => item.DailyCreditSales));
            rows.Add(CreateDataRow("", "売上値引", items, startClass, endClass, 
                item => item.DailySalesDiscount));
            rows.Add(CreateDataRow("", "掛売消費税", items, startClass, endClass, 
                item => item.DailyCreditSalesTax));
            
            // 売上計（合計行）
            rows.Add(CreateSummaryRow("", "＊売上計＊", rows.TakeLast(5)));
            
            // 仕入関連5項目
            rows.Add(CreateDataRow("", "現金仕入", items, startClass, endClass, 
                item => item.DailyCashPurchase));
            rows.Add(CreateDataRow("", "現仕消費税", items, startClass, endClass, 
                item => item.DailyCashPurchaseTax));
            rows.Add(CreateDataRow("", "掛仕入と返品", items, startClass, endClass, 
                item => item.DailyCreditPurchase));
            rows.Add(CreateDataRow("", "仕入値引", items, startClass, endClass, 
                item => item.DailyPurchaseDiscount));
            rows.Add(CreateDataRow("", "掛仕入消費税", items, startClass, endClass, 
                item => item.DailyCreditPurchaseTax));
            
            // 仕入計（合計行）
            rows.Add(CreateSummaryRow("", "＊仕入計＊", rows.Skip(6).Take(5)));
            
            // 入金関連3項目
            rows.Add(CreateDataRow("", "入金と現売", items, startClass, endClass, 
                item => item.DailyCashReceipt + item.DailyCashSales));
            rows.Add(CreateDataRow("", "入金値引・他", items, startClass, endClass, 
                item => item.DailyOtherReceipt));
            
            // 入金計（合計行）
            rows.Add(CreateSummaryRow("", "＊入金計＊", rows.Skip(12).Take(2)));
            
            // 支払関連3項目
            rows.Add(CreateDataRow("", "支払と現金支払", items, startClass, endClass, 
                item => item.DailyCashPayment + item.DailyCashPurchase));
            rows.Add(CreateDataRow("", "支払値引・他", items, startClass, endClass, 
                item => item.DailyOtherPayment));
            
            // 支払計（合計行）
            rows.Add(CreateSummaryRow("", "＊支払計＊", rows.Skip(15).Take(2)));
            
            page.Rows.AddRange(rows);
        }

        private void AddYearlySection(
            BusinessDailyReportPage page,
            IEnumerable<BusinessDailyReportItem> items,
            int startClass, int endClass)
        {
            var rows = new List<BusinessDailyReportRow>();
            
            // 年計は4項目のみ
            rows.Add(CreateDataRow("【年計】", "売上", items, startClass, endClass, 
                item => item.DailyCashSales + item.DailyCreditSales));  // 売上合計
            rows.Add(CreateDataRow("", "売上消費税", items, startClass, endClass, 
                item => item.DailyCashSalesTax + item.DailyCreditSalesTax));  // 売上消費税合計
            rows.Add(CreateDataRow("", "仕入", items, startClass, endClass, 
                item => item.DailyCashPurchase + item.DailyCreditPurchase));  // 仕入合計
            rows.Add(CreateDataRow("", "仕入消費税", items, startClass, endClass, 
                item => item.DailyCashPurchaseTax + item.DailyCreditPurchaseTax));  // 仕入消費税合計
            
            page.Rows.AddRange(rows);
        }

        private BusinessDailyReportRow CreateDataRow(
            string sectionName,
            string itemName,
            IEnumerable<BusinessDailyReportItem> items,
            int startClass,
            int endClass,
            Func<BusinessDailyReportItem, decimal> valueSelector)
        {
            var row = new BusinessDailyReportRow
            {
                SectionName = sectionName,
                ItemName = itemName,
                IsSummaryRow = false
            };
            
            decimal total = 0;
            
            // 合計（000）の値
            var totalItem = items.FirstOrDefault(x => x.ClassificationCode == "000");
            if (totalItem != null)
            {
                total = valueSelector(totalItem);
                row.Total = FormatNumber(total);
            }
            
            // 各分類の値を設定（該当ページの分類のみ）
            for (int i = startClass; i <= endClass; i++)
            {
                var classCode = $"{i:000}";
                var item = items.FirstOrDefault(x => x.ClassificationCode == classCode);
                decimal value = item != null ? valueSelector(item) : 0m;
                
                // リフレクションで動的にプロパティ設定
                var propName = $"Class{(i - startClass + 1):00}";
                var prop = row.GetType().GetProperty(propName);
                prop?.SetValue(row, FormatNumber(value));
            }
            
            return row;
        }

        private BusinessDailyReportRow CreateSummaryRow(
            string sectionName,
            string itemName,
            IEnumerable<BusinessDailyReportRow> sourceRows)
        {
            var row = new BusinessDailyReportRow
            {
                SectionName = sectionName,
                ItemName = itemName,
                IsSummaryRow = true
            };
            
            // 各列の合計を計算
            row.Total = FormatNumber(SumColumn(sourceRows, "Total"));
            
            for (int i = 1; i <= 9; i++)
            {
                var propName = $"Class{i:00}";
                var sum = SumColumn(sourceRows, propName);
                var prop = row.GetType().GetProperty(propName);
                prop?.SetValue(row, FormatNumber(sum));
            }
            
            return row;
        }

        private decimal SumColumn(IEnumerable<BusinessDailyReportRow> rows, string columnName)
        {
            decimal sum = 0;
            foreach (var row in rows)
            {
                var prop = row.GetType().GetProperty(columnName);
                var value = prop?.GetValue(row)?.ToString() ?? "";
                if (!string.IsNullOrEmpty(value))
                {
                    // ▲記号の処理
                    bool isNegative = value.StartsWith("▲");
                    var numStr = value.Replace("▲", "").Replace(",", "");
                    if (decimal.TryParse(numStr, out var num))
                    {
                        sum += isNegative ? -num : num;
                    }
                }
            }
            return sum;
        }

        private byte[] GenerateMultiPagePdf(List<BusinessDailyReportPage> pages, DateTime jobDate)
        {
            if (!File.Exists(_templatePath))
            {
                throw new FileNotFoundException($"営業日報テンプレートが見つかりません: {_templatePath}");
            }

            using var report = new FR.Report();
            
            // FastReportの設定
            report.ReportResourceString = "";
            report.FileName = _templatePath;
            
            // テンプレート読み込み
            report.Load(_templatePath);
            
            // ScriptLanguageをNoneに設定（最重要）
            SetScriptLanguageToNone(report);
            
            // 4ページ分のDataTableを作成・登録
            for (int pageNo = 1; pageNo <= 4; pageNo++)
            {
                var page = pages.FirstOrDefault(p => p.PageNumber == pageNo);
                if (page != null)
                {
                    var dataTable = CreatePageDataTable(page, pageNo);
                    report.RegisterData(dataTable, $"Page{pageNo}Data");
                }
            }
            
            // パラメータ設定
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分"));
            
            // 各ページの分類名パラメータ設定
            SetPagesParameters(report, pages);
            
            _logger.LogInformation("レポートを準備中...");
            report.Prepare();
            
            // PDF出力
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

        private DataTable CreatePageDataTable(BusinessDailyReportPage page, int pageNo)
        {
            var table = new DataTable($"Page{pageNo}Data");
            
            // カラム定義
            table.Columns.Add("SectionName", typeof(string));
            table.Columns.Add("ItemName", typeof(string));
            table.Columns.Add("Total", typeof(string));
            for (int i = 1; i <= 9; i++)
            {
                table.Columns.Add($"Class{i:00}", typeof(string));
            }
            table.Columns.Add("IsSummaryRow", typeof(bool));
            
            // データ追加
            foreach (var row in page.Rows)
            {
                var dataRow = table.NewRow();
                dataRow["SectionName"] = row.SectionName;
                dataRow["ItemName"] = row.ItemName;
                dataRow["Total"] = row.Total;
                
                for (int i = 1; i <= 9; i++)
                {
                    var propName = $"Class{i:00}";
                    var prop = row.GetType().GetProperty(propName);
                    dataRow[propName] = prop?.GetValue(row) ?? "";
                }
                
                dataRow["IsSummaryRow"] = row.IsSummaryRow;
                table.Rows.Add(dataRow);
            }
            
            return table;
        }

        private void SetPagesParameters(FR.Report report, List<BusinessDailyReportPage> pages)
        {
            foreach (var page in pages)
            {
                // ページタイトル
                report.SetParameterValue($"Page{page.PageNumber}Title", page.PageTitle);
                
                // 分類名（9個×2種類）
                for (int i = 0; i < 9 && i < page.CustomerClassNames.Count; i++)
                {
                    report.SetParameterValue($"Page{page.PageNumber}CustomerClass{(i + 1):00}", page.CustomerClassNames[i]);
                }
                
                for (int i = 0; i < 9 && i < page.SupplierClassNames.Count; i++)
                {
                    report.SetParameterValue($"Page{page.PageNumber}SupplierClass{(i + 1):00}", page.SupplierClassNames[i]);
                }
            }
        }

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
                            _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }
                
                // Scriptプロパティもnullに設定（追加の安全策）
                var scriptProperty = report.GetType().GetProperty("Script", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
            }
        }

        private string FormatNumber(decimal value)
        {
            if (value == 0) return "";
            return value < 0 ? $"▲{Math.Abs(value):N0}" : value.ToString("N0");
        }

        private string GetKanjiNumber(int number)
        {
            return number switch
            {
                1 => "１",
                2 => "２", 
                3 => "３",
                4 => "４",
                _ => number.ToString()
            };
        }

        // インターフェース実装用
        public byte[] GenerateBusinessDailyReport(IEnumerable<object> businessDailyReportItems, DateTime jobDate)
        {
            var items = businessDailyReportItems.Cast<BusinessDailyReportItem>();
            return GenerateBusinessDailyReport(items, jobDate);
        }
    }
}
#endif
#pragma warning restore CA1416