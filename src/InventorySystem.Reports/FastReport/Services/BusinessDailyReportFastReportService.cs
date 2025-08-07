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
    /// <summary>
    /// 営業日報動的ページ生成サービス（データがある分類のみ表示、1ページ8分類）
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
                _logger.LogInformation("営業日報PDF生成を開始します（動的ページ生成版）: JobDate={JobDate}", jobDate);

                // 1. データがある分類のみを抽出
                var activeClassifications = GetActiveClassifications(items);
                _logger.LogInformation("有効な分類数: {Count}", activeClassifications.Count);
                
                // 2. ページ数を計算（1ページ8分類）
                var totalPages = Math.Max(1, (int)Math.Ceiling(activeClassifications.Count / 8.0));
                _logger.LogInformation("動的ページ数: {TotalPages}", totalPages);
                
                // 3. 月計・年計データの取得
                var monthlyData = await _repository.GetMonthlyDataAsync(jobDate);
                var yearlyData = await _repository.GetYearlyDataAsync(jobDate);
                
                // 4. 動的ページ生成でPDF作成
                return await GenerateDynamicMultiPagePdf(items, monthlyData, yearlyData, activeClassifications, totalPages, jobDate);
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

        /// <summary>
        /// データがある分類のみを抽出（000は除外）
        /// </summary>
        private List<BusinessDailyReportItem> GetActiveClassifications(IEnumerable<BusinessDailyReportItem> items)
        {
            return items
                .Where(x => x.ClassificationCode != "000" && // 合計以外
                           !string.IsNullOrEmpty(x.ClassificationCode) &&
                           HasAnyData(x)) // 実際にデータがある分類のみ
                .OrderBy(x => x.ClassificationCode)
                .ToList();
        }
        
        /// <summary>
        /// 分類に何らかのデータがあるかをチェック
        /// </summary>
        private bool HasAnyData(BusinessDailyReportItem item)
        {
            return item.DailyCashSales != 0 ||
                   item.DailyCashSalesTax != 0 ||
                   item.DailyCreditSales != 0 ||
                   item.DailySalesDiscount != 0 ||
                   item.DailyCreditSalesTax != 0 ||
                   item.DailyCashPurchase != 0 ||
                   item.DailyCashPurchaseTax != 0 ||
                   item.DailyCreditPurchase != 0 ||
                   item.DailyPurchaseDiscount != 0 ||
                   item.DailyCreditPurchaseTax != 0 ||
                   item.DailyCashReceipt != 0 ||
                   item.DailyBankReceipt != 0 ||
                   item.DailyOtherReceipt != 0 ||
                   item.DailyCashPayment != 0 ||
                   item.DailyBankPayment != 0 ||
                   item.DailyOtherPayment != 0;
        }

        /// <summary>
        /// 動的ページ生成でPDFを作成
        /// </summary>
        private async Task<byte[]> GenerateDynamicMultiPagePdf(
            IEnumerable<BusinessDailyReportItem> dailyItems,
            IEnumerable<BusinessDailyReportItem> monthlyItems,
            IEnumerable<BusinessDailyReportItem> yearlyItems,
            List<BusinessDailyReportItem> activeClassifications,
            int totalPages,
            DateTime jobDate)
        {
            if (!File.Exists(_templatePath))
            {
                throw new FileNotFoundException($"営業日報テンプレートが見つかりません: {_templatePath}");
            }

            using var report = new FR.Report();
            report.Load(_templatePath);
            SetScriptLanguageToNone(report);

            // 元のページをテンプレートとして取得
            var templatePage = report.Pages[0] as FR.ReportPage;
            if (templatePage == null)
            {
                throw new InvalidOperationException("テンプレートページが見つかりません");
            }

            // 追加ページが必要な場合はページを複製
            for (int pageIndex = 1; pageIndex < totalPages; pageIndex++)
            {
                var newPage = ClonePage(templatePage, report);
                report.Pages.Add(newPage);
                _logger.LogDebug("ページ{PageIndex}を追加しました", pageIndex + 1);
            }

            var allDailyItems = dailyItems.ToList();
            var allMonthlyItems = monthlyItems.ToList();
            var allYearlyItems = yearlyItems.ToList();

            // 各ページにデータを設定
            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                var currentPage = report.Pages[pageIndex] as FR.ReportPage;
                if (currentPage != null)
                {
                    SetPageData(currentPage, allDailyItems, allMonthlyItems, allYearlyItems, 
                              activeClassifications, pageIndex, totalPages, jobDate);
                }
            }

            _logger.LogInformation("レポートを準備中...");
            report.Prepare();

            return ExportToPdf(report, jobDate);
        }

        /// <summary>
        /// ページを複製（FastReportページクローン）
        /// </summary>
        private FR.ReportPage ClonePage(FR.ReportPage templatePage, FR.Report report)
        {
            // 新しいページを作成
            var newPage = new FR.ReportPage();
            
            // 基本プロパティをコピー
            newPage.Name = $"Page{report.Pages.Count + 1}";
            newPage.PaperWidth = templatePage.PaperWidth;
            newPage.PaperHeight = templatePage.PaperHeight;
            newPage.Landscape = templatePage.Landscape;
            newPage.LeftMargin = templatePage.LeftMargin;
            newPage.TopMargin = templatePage.TopMargin;
            newPage.RightMargin = templatePage.RightMargin;
            newPage.BottomMargin = templatePage.BottomMargin;

            // バンドをコピー
            foreach (FR.BandBase band in templatePage.AllObjects)
            {
                if (band is FR.BandBase)
                {
                    // バンドの複製を作成
                    var newBand = CloneBand(band);
                    newPage.Bands.Add(newBand);
                }
            }

            return newPage;
        }

        /// <summary>
        /// バンドを複製
        /// </summary>
        private FR.BandBase CloneBand(FR.BandBase originalBand)
        {
            // バンドタイプに応じて新しいバンドを作成
            FR.BandBase newBand = originalBand switch
            {
                FR.PageHeaderBand => new FR.PageHeaderBand(),
                FR.DataBand => new FR.DataBand(),
                FR.PageFooterBand => new FR.PageFooterBand(),
                _ => throw new NotSupportedException($"バンドタイプ {originalBand.GetType().Name} は対応していません")
            };

            // 基本プロパティをコピー
            newBand.Name = originalBand.Name + "_Clone";
            newBand.Height = originalBand.Height;
            newBand.Top = originalBand.Top;

            // 子オブジェクトをコピー
            foreach (FR.ReportComponentBase child in originalBand.Objects)
            {
                var clonedChild = CloneReportObject(child);
                newBand.Objects.Add(clonedChild);
            }

            return newBand;
        }

        /// <summary>
        /// レポートオブジェクトを複製
        /// </summary>
        private FR.ReportComponentBase CloneReportObject(FR.ReportComponentBase original)
        {
            if (original is FR.TextObject textObj)
            {
                var newTextObj = new FR.TextObject();
                newTextObj.Name = textObj.Name + "_Clone";
                newTextObj.Left = textObj.Left;
                newTextObj.Top = textObj.Top;
                newTextObj.Width = textObj.Width;
                newTextObj.Height = textObj.Height;
                newTextObj.Text = textObj.Text;
                newTextObj.HorzAlign = textObj.HorzAlign;
                newTextObj.VertAlign = textObj.VertAlign;
                newTextObj.Font = textObj.Font;
                newTextObj.Border = textObj.Border;
                return newTextObj;
            }

            throw new NotSupportedException($"オブジェクトタイプ {original.GetType().Name} は対応していません");
        }

        /// <summary>
        /// 各ページにデータパラメータを設定
        /// </summary>
        private void SetPageData(
            FR.ReportPage page,
            List<BusinessDailyReportItem> dailyItems,
            List<BusinessDailyReportItem> monthlyItems,
            List<BusinessDailyReportItem> yearlyItems,
            List<BusinessDailyReportItem> activeClassifications,
            int pageIndex,
            int totalPages,
            DateTime jobDate)
        {
            // 基本パラメータ
            var report = page.Report;
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
            report.SetParameterValue("PageNumber", ToKanjiNumber(pageIndex + 1));

            // このページの分類を取得（8個ずつ）
            var startIndex = pageIndex * 8;
            var pageClassifications = activeClassifications
                .Skip(startIndex)
                .Take(8)
                .ToList();

            // 合計データ取得
            var totalDailyData = dailyItems.FirstOrDefault(x => x.ClassificationCode == "000");
            var totalMonthlyData = monthlyItems.FirstOrDefault(x => x.ClassificationCode == "000");
            var totalYearlyData = yearlyItems.FirstOrDefault(x => x.ClassificationCode == "000");

            // データテーブル作成
            var dataTable = CreateDynamicPageDataTable(
                totalDailyData, totalMonthlyData, totalYearlyData,
                pageClassifications, dailyItems, monthlyItems, yearlyItems);

            report.RegisterData(dataTable, $"PageData");

            // 分類名パラメータ設定（8個分）
            for (int i = 0; i < 8; i++)
            {
                if (i < pageClassifications.Count)
                {
                    var item = pageClassifications[i];
                    report.SetParameterValue($"CustomerName{i + 1}", TruncateToLength(item.CustomerClassName ?? "", 6));
                    report.SetParameterValue($"SupplierName{i + 1}", TruncateToLength(item.SupplierClassName ?? "", 6));
                }
                else
                {
                    // 空の分類
                    report.SetParameterValue($"CustomerName{i + 1}", "");
                    report.SetParameterValue($"SupplierName{i + 1}", "");
                }
            }
        }

        /// <summary>
        /// 動的ページ用のDataTable作成（40行固定）
        /// </summary>
        private DataTable CreateDynamicPageDataTable(
            BusinessDailyReportItem totalDaily,
            BusinessDailyReportItem totalMonthly,
            BusinessDailyReportItem totalYearly,
            List<BusinessDailyReportItem> pageClassifications,
            List<BusinessDailyReportItem> allDaily,
            List<BusinessDailyReportItem> allMonthly,
            List<BusinessDailyReportItem> allYearly)
        {
            var table = new DataTable("PageData");
            
            // カラム定義
            table.Columns.Add("SectionName", typeof(string));
            table.Columns.Add("ItemName", typeof(string));
            table.Columns.Add("Total", typeof(string));
            for (int i = 1; i <= 8; i++)
            {
                table.Columns.Add($"Class{i:00}", typeof(string));
            }
            table.Columns.Add("IsSummaryRow", typeof(bool));

            // 40行のデータを作成（18+18+4）
            AddDailySection(table, totalDaily, pageClassifications, allDaily);
            AddMonthlySection(table, totalMonthly, pageClassifications, allMonthly);
            AddYearlySection(table, totalYearly, pageClassifications, allYearly);

            return table;
        }

        /// <summary>
        /// 日計セクション（18行）を追加
        /// </summary>
        private void AddDailySection(DataTable table, BusinessDailyReportItem totalData, 
            List<BusinessDailyReportItem> pageClassifications, List<BusinessDailyReportItem> allData)
        {
            // 日計18行の定義（仕様書準拠の正確な項目名）
            var dailyRows = new[]
            {
                new { Section = "【日計】", Item = "現金売上", Property = nameof(BusinessDailyReportItem.DailyCashSales), IsSum = false },
                new { Section = "", Item = "現売消費税", Property = nameof(BusinessDailyReportItem.DailyCashSalesTax), IsSum = false },
                new { Section = "", Item = "掛売上と返品", Property = nameof(BusinessDailyReportItem.DailyCreditSales), IsSum = false },
                new { Section = "", Item = "売上値引", Property = nameof(BusinessDailyReportItem.DailySalesDiscount), IsSum = false },
                new { Section = "", Item = "掛売消費税", Property = nameof(BusinessDailyReportItem.DailyCreditSalesTax), IsSum = false },
                new { Section = "", Item = "＊売上計＊", Property = "", IsSum = true },
                new { Section = "", Item = "現金仕入", Property = nameof(BusinessDailyReportItem.DailyCashPurchase), IsSum = false },
                new { Section = "", Item = "現仕消費税", Property = nameof(BusinessDailyReportItem.DailyCashPurchaseTax), IsSum = false },
                new { Section = "", Item = "掛仕入と返品", Property = nameof(BusinessDailyReportItem.DailyCreditPurchase), IsSum = false },
                new { Section = "", Item = "仕入値引", Property = nameof(BusinessDailyReportItem.DailyPurchaseDiscount), IsSum = false },
                new { Section = "", Item = "掛仕入消費税", Property = nameof(BusinessDailyReportItem.DailyCreditPurchaseTax), IsSum = false },
                new { Section = "", Item = "＊仕入計＊", Property = "", IsSum = true },
                new { Section = "", Item = "入金と現売", Property = nameof(BusinessDailyReportItem.DailyCashReceipt), IsSum = false },
                new { Section = "", Item = "振込入金", Property = nameof(BusinessDailyReportItem.DailyBankReceipt), IsSum = false },
                new { Section = "", Item = "入金値引・その他", Property = nameof(BusinessDailyReportItem.DailyOtherReceipt), IsSum = false },
                new { Section = "", Item = "＊入金計＊", Property = "", IsSum = true },
                new { Section = "", Item = "支払と現金支払", Property = nameof(BusinessDailyReportItem.DailyCashPayment), IsSum = false },
                new { Section = "", Item = "振込支払", Property = nameof(BusinessDailyReportItem.DailyBankPayment), IsSum = false }
            };

            foreach (var rowDef in dailyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = rowDef.IsSum;

                if (rowDef.IsSum)
                {
                    // 合計行の計算（前5行の合計など）
                    row["Total"] = ""; // 実装時に計算ロジック追加
                    for (int i = 1; i <= 8; i++)
                    {
                        row[$"Class{i:00}"] = "";
                    }
                }
                else
                {
                    // 通常行
                    row["Total"] = GetPropertyValueFormatted(totalData, rowDef.Property);
                    for (int i = 1; i <= 8; i++)
                    {
                        if (i <= pageClassifications.Count)
                        {
                            var classCode = pageClassifications[i - 1].ClassificationCode;
                            var item = allData.FirstOrDefault(x => x.ClassificationCode == classCode);
                            row[$"Class{i:00}"] = GetPropertyValueFormatted(item, rowDef.Property);
                        }
                        else
                        {
                            row[$"Class{i:00}"] = "";
                        }
                    }
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// 月計セクション（18行）を追加
        /// </summary>
        private void AddMonthlySection(DataTable table, BusinessDailyReportItem totalData, 
            List<BusinessDailyReportItem> pageClassifications, List<BusinessDailyReportItem> allData)
        {
            // 月計も日計と同じ18行構造
            var monthlyRows = new[]
            {
                new { Section = "【月計】", Item = "現金売上", Property = nameof(BusinessDailyReportItem.DailyCashSales), IsSum = false },
                new { Section = "", Item = "現売消費税", Property = nameof(BusinessDailyReportItem.DailyCashSalesTax), IsSum = false },
                new { Section = "", Item = "掛売上と返品", Property = nameof(BusinessDailyReportItem.DailyCreditSales), IsSum = false },
                new { Section = "", Item = "売上値引", Property = nameof(BusinessDailyReportItem.DailySalesDiscount), IsSum = false },
                new { Section = "", Item = "掛売消費税", Property = nameof(BusinessDailyReportItem.DailyCreditSalesTax), IsSum = false },
                new { Section = "", Item = "＊売上計＊", Property = "", IsSum = true },
                new { Section = "", Item = "現金仕入", Property = nameof(BusinessDailyReportItem.DailyCashPurchase), IsSum = false },
                new { Section = "", Item = "現仕消費税", Property = nameof(BusinessDailyReportItem.DailyCashPurchaseTax), IsSum = false },
                new { Section = "", Item = "掛仕入と返品", Property = nameof(BusinessDailyReportItem.DailyCreditPurchase), IsSum = false },
                new { Section = "", Item = "仕入値引", Property = nameof(BusinessDailyReportItem.DailyPurchaseDiscount), IsSum = false },
                new { Section = "", Item = "掛仕入消費税", Property = nameof(BusinessDailyReportItem.DailyCreditPurchaseTax), IsSum = false },
                new { Section = "", Item = "＊仕入計＊", Property = "", IsSum = true },
                new { Section = "", Item = "入金と現売", Property = nameof(BusinessDailyReportItem.DailyCashReceipt), IsSum = false },
                new { Section = "", Item = "振込入金", Property = nameof(BusinessDailyReportItem.DailyBankReceipt), IsSum = false },
                new { Section = "", Item = "入金値引・その他", Property = nameof(BusinessDailyReportItem.DailyOtherReceipt), IsSum = false },
                new { Section = "", Item = "＊入金計＊", Property = "", IsSum = true },
                new { Section = "", Item = "支払と現金支払", Property = nameof(BusinessDailyReportItem.DailyCashPayment), IsSum = false },
                new { Section = "", Item = "振込支払", Property = nameof(BusinessDailyReportItem.DailyBankPayment), IsSum = false }
            };

            foreach (var rowDef in monthlyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = rowDef.IsSum;

                if (rowDef.IsSum)
                {
                    // 合計行
                    row["Total"] = "";
                    for (int i = 1; i <= 8; i++)
                    {
                        row[$"Class{i:00}"] = "";
                    }
                }
                else
                {
                    // 通常行（月計データを使用）
                    row["Total"] = GetPropertyValueFormatted(totalData, rowDef.Property);
                    for (int i = 1; i <= 8; i++)
                    {
                        if (i <= pageClassifications.Count)
                        {
                            var classCode = pageClassifications[i - 1].ClassificationCode;
                            var item = allData.FirstOrDefault(x => x.ClassificationCode == classCode);
                            row[$"Class{i:00}"] = GetPropertyValueFormatted(item, rowDef.Property);
                        }
                        else
                        {
                            row[$"Class{i:00}"] = "";
                        }
                    }
                }

                table.Rows.Add(row);
            }
        }

        /// <summary>
        /// 年計セクション（4行）を追加
        /// </summary>
        private void AddYearlySection(DataTable table, BusinessDailyReportItem totalData, 
            List<BusinessDailyReportItem> pageClassifications, List<BusinessDailyReportItem> allData)
        {
            // 年計は4行のみ
            var yearlyRows = new[]
            {
                new { Section = "【年計】", Item = "売上", Property = nameof(BusinessDailyReportItem.DailyCashSales) },
                new { Section = "", Item = "売上消費税", Property = nameof(BusinessDailyReportItem.DailyCashSalesTax) },
                new { Section = "", Item = "仕入", Property = nameof(BusinessDailyReportItem.DailyCashPurchase) },
                new { Section = "", Item = "仕入消費税", Property = nameof(BusinessDailyReportItem.DailyCashPurchaseTax) }
            };

            foreach (var rowDef in yearlyRows)
            {
                var row = table.NewRow();
                row["SectionName"] = rowDef.Section;
                row["ItemName"] = rowDef.Item;
                row["IsSummaryRow"] = false;

                // 年計データを使用
                row["Total"] = GetPropertyValueFormatted(totalData, rowDef.Property);
                for (int i = 1; i <= 8; i++)
                {
                    if (i <= pageClassifications.Count)
                    {
                        var classCode = pageClassifications[i - 1].ClassificationCode;
                        var item = allData.FirstOrDefault(x => x.ClassificationCode == classCode);
                        row[$"Class{i:00}"] = GetPropertyValueFormatted(item, rowDef.Property);
                    }
                    else
                    {
                        row[$"Class{i:00}"] = "";
                    }
                }

                table.Rows.Add(row);
            }
        }

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
                _logger.LogWarning(ex, "ScriptLanguage設定時の警告");
            }
        }

        private string FormatNumber(decimal value)
        {
            if (value == 0) return "";
            return value < 0 ? $"▲{Math.Abs(value):N0}" : value.ToString("N0");
        }

        private string TruncateToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }

        private string ToKanjiNumber(int number)
        {
            return number switch
            {
                1 => "１",
                2 => "２", 
                3 => "３",
                4 => "４",
                5 => "５",
                6 => "６",
                7 => "７",
                8 => "８",
                9 => "９",
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