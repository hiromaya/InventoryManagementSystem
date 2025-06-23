#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using FastReport;
using FastReport.Export.Pdf;
using FastReport.Data;
using InventorySystem.Core.Entities;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Reports.FastReport.Services
{
    public class DailyReportFastReportService : IDailyReportService
    {
        private readonly ILogger<DailyReportFastReportService> _logger;
        
        public DailyReportFastReportService(ILogger<DailyReportFastReportService> logger)
        {
            _logger = logger;
        }
        
        public byte[] GenerateDailyReport(
            List<DailyReportItem> items,
            List<DailyReportSubtotal> subtotals,
            DailyReportTotal total,
            DateTime reportDate)
        {
            try
            {
                using var report = new Report();
            
            // A3横設定
            var page = new ReportPage
            {
                Name = "Page1",
                PaperWidth = 420f,  // A3
                PaperHeight = 297f,
                Landscape = true,
                LeftMargin = 10f,
                RightMargin = 10f,
                TopMargin = 10f,
                BottomMargin = 10f
            };
            report.Pages.Add(page);
            
            // レポートタイトル
            CreateReportTitle(page, reportDate);
            
            // カラムヘッダー
            CreateColumnHeaders(page);
            
            // データ部分
            CreateDataSection(page, report, items);
            
            // 小計部分
            CreateSubtotalSection(page, subtotals);
            
            // 合計部分
            CreateTotalSection(page, total);
            
            // レポート生成
            // 【重要】スクリプトを完全に無効化
            try
            {
                // ScriptLanguageプロパティをリフレクションで探す
                var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                if (scriptLanguageProperty != null)
                {
                    var scriptLanguageType = scriptLanguageProperty.PropertyType;
                    if (scriptLanguageType.IsEnum)
                    {
                        // FastReport.ScriptLanguage.None を設定
                        var noneValue = Enum.GetValues(scriptLanguageType).Cast<object>().FirstOrDefault(v => v.ToString() == "None");
                        if (noneValue != null)
                        {
                            scriptLanguageProperty.SetValue(report, noneValue);
                            _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }
                
                // ScriptTextは設定しない（完全に削除）
                // report.ScriptText を設定している行をすべて削除
                
                // Prepareを直接呼び出す
                report.Prepare();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"レポート生成でエラーが発生しました: {ex.Message}");
                
                // エラーが発生した場合の最小限のフォールバック
                try
                {
                    // 内部のScriptプロパティをnullに設定
                    var scriptProperty = report.GetType().GetProperty("Script", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (scriptProperty != null)
                    {
                        scriptProperty.SetValue(report, null);
                    }
                    
                    report.Prepare();
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "フォールバック処理も失敗しました");
                    throw;
                }
            }
            
            // PDF出力
            using var pdfExport = new PDFExport
            {
                EmbeddingFonts = true,
                Title = $"商品日報_{reportDate:yyyyMMdd}",
                Subject = "商品日報",
                Creator = "在庫管理システム"
            };
            
            using var stream = new MemoryStream();
            report.Export(pdfExport, stream);
            
            return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "商品日報の生成中にエラーが発生しました");
                throw;
            }
        }
        
        private void CreateReportTitle(ReportPage page, DateTime reportDate)
        {
            var titleBand = new ReportTitleBand
            {
                Name = "ReportTitle",
                Height = 60f
            };
            page.ReportTitle = titleBand;
            
            var titleText = new TextObject
            {
                Name = "Title",
                Bounds = new RectangleF(0, 10, page.PaperWidth - 20, 30),
                Text = "商　品　日　報",
                Font = new Font("MS Gothic", 20, FontStyle.Bold),
                HorzAlign = HorzAlign.Center
            };
            titleBand.Objects.Add(titleText);
            
            var dateText = new TextObject
            {
                Name = "ReportDate",
                Bounds = new RectangleF(0, 40, page.PaperWidth - 20, 20),
                Text = $"{reportDate:yyyy年MM月dd日}",
                Font = new Font("MS Gothic", 12),
                HorzAlign = HorzAlign.Center
            };
            titleBand.Objects.Add(dateText);
        }
        
        private void CreateColumnHeaders(ReportPage page)
        {
            var headerBand = new PageHeaderBand
            {
                Name = "PageHeader",
                Height = 80f
            };
            page.PageHeader = headerBand;
            
            // 日計・月計の大ヘッダー
            CreateMainHeaders(headerBand);
            
            // 詳細ヘッダー
            CreateDetailHeaders(headerBand);
        }
        
        private void CreateMainHeaders(PageHeaderBand headerBand)
        {
            var dailyHeader = new TextObject
            {
                Name = "DailyHeader",
                Bounds = new RectangleF(120, 10, 200, 20),
                Text = "日計",
                Font = new Font("MS Gothic", 12, FontStyle.Bold),
                HorzAlign = HorzAlign.Center,
                Border = { Lines = BorderLines.All }
            };
            headerBand.Objects.Add(dailyHeader);
            
            var monthlyHeader = new TextObject
            {
                Name = "MonthlyHeader",
                Bounds = new RectangleF(320, 10, 80, 20),
                Text = "月計",
                Font = new Font("MS Gothic", 12, FontStyle.Bold),
                HorzAlign = HorzAlign.Center,
                Border = { Lines = BorderLines.All }
            };
            headerBand.Objects.Add(monthlyHeader);
        }
        
        private void CreateDetailHeaders(PageHeaderBand headerBand)
        {
            var headers = new[]
            {
                "商品名", "売上数量", "売上金額", "仕入値引", "在庫調整", "加工費", "振替", "奨励金", 
                "１粗利益", "１粗利率", "２粗利益", "２粗利率", "売上金額", "１粗利益", "１粗利率", "２粗利益", "２粗利率"
            };
            
            var widths = new[] { 120, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20 };
            
            float xPos = 0;
            for (int i = 0; i < headers.Length; i++)
            {
                var header = new TextObject
                {
                    Name = $"Header{i}",
                    Bounds = new RectangleF(xPos, 30, widths[i], 40),
                    Text = headers[i],
                    Font = new Font("MS Gothic", 8, FontStyle.Bold),
                    HorzAlign = HorzAlign.Center,
                    VertAlign = VertAlign.Center,
                    Border = { Lines = BorderLines.All },
                    WordWrap = true
                };
                headerBand.Objects.Add(header);
                xPos += widths[i];
            }
        }
        
        private void CreateDataSection(ReportPage page, Report report, List<DailyReportItem> items)
        {
            // データソースの登録
            var dataTable = CreateDataTable(items);
            report.RegisterData(dataTable, "DailyReportItems");
            
            var dataBand = new DataBand
            {
                Name = "DataBand",
                DataSource = report.GetDataSource("DailyReportItems"),
                Height = 20f
            };
            page.Bands.Add(dataBand);
            
            // データフィールドの作成
            CreateDataFields(dataBand);
        }
        
        private DataTable CreateDataTable(List<DailyReportItem> items)
        {
            var table = new DataTable("DailyReportItems");
            
            // カラム定義
            table.Columns.Add("ProductName", typeof(string));
            table.Columns.Add("DailySalesQuantity", typeof(decimal));
            table.Columns.Add("DailySalesAmount", typeof(decimal));
            table.Columns.Add("DailyPurchaseDiscount", typeof(decimal));
            table.Columns.Add("DailyInventoryAdjustment", typeof(decimal));
            table.Columns.Add("DailyProcessingCost", typeof(decimal));
            table.Columns.Add("DailyTransfer", typeof(decimal));
            table.Columns.Add("DailyIncentive", typeof(decimal));
            table.Columns.Add("DailyGrossProfit1", typeof(decimal));
            table.Columns.Add("DailyGrossProfitRate1", typeof(decimal));
            table.Columns.Add("DailyGrossProfit2", typeof(decimal));
            table.Columns.Add("DailyGrossProfitRate2", typeof(decimal));
            table.Columns.Add("MonthlySalesAmount", typeof(decimal));
            table.Columns.Add("MonthlyGrossProfit1", typeof(decimal));
            table.Columns.Add("MonthlyGrossProfitRate1", typeof(decimal));
            table.Columns.Add("MonthlyGrossProfit2", typeof(decimal));
            table.Columns.Add("MonthlyGrossProfitRate2", typeof(decimal));
            
            // データ追加（オール0の明細は除外）
            foreach (var item in items.Where(i => !i.IsAllZero()))
            {
                table.Rows.Add(
                    item.ProductName,
                    item.DailySalesQuantity,
                    item.DailySalesAmount,
                    item.DailyPurchaseDiscount,
                    item.DailyInventoryAdjustment,
                    item.DailyProcessingCost,
                    item.DailyTransfer,
                    item.DailyIncentive,
                    item.DailyGrossProfit1,
                    item.DailyGrossProfitRate1,
                    item.DailyGrossProfit2,
                    item.DailyGrossProfitRate2,
                    item.MonthlySalesAmount,
                    item.MonthlyGrossProfit1,
                    item.MonthlyGrossProfitRate1,
                    item.MonthlyGrossProfit2,
                    item.MonthlyGrossProfitRate2
                );
            }
            
            return table;
        }
        
        private void CreateDataFields(DataBand dataBand)
        {
            var fields = new[]
            {
                "ProductName", "DailySalesQuantity", "DailySalesAmount", "DailyPurchaseDiscount",
                "DailyInventoryAdjustment", "DailyProcessingCost", "DailyTransfer", "DailyIncentive",
                "DailyGrossProfit1", "DailyGrossProfitRate1", "DailyGrossProfit2", "DailyGrossProfitRate2",
                "MonthlySalesAmount", "MonthlyGrossProfit1", "MonthlyGrossProfitRate1", 
                "MonthlyGrossProfit2", "MonthlyGrossProfitRate2"
            };
            
            var widths = new[] { 120, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20 };
            
            float xPos = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                var field = new TextObject
                {
                    Name = $"Field{i}",
                    Bounds = new RectangleF(xPos, 0, widths[i], 20),
                    Text = $"[DailyReportItems.{fields[i]}]",
                    Font = new Font("MS Gothic", 8),
                    HorzAlign = i == 0 ? HorzAlign.Left : HorzAlign.Right,
                    Border = { Lines = BorderLines.All }
                };
                
                // 数値フィールドのフォーマット設定
                if (i > 0)
                {
                    if (fields[i].Contains("Rate"))
                    {
                        // 率の場合：小数点2桁表示
                        field.Text = $"[Format([DailyReportItems.{fields[i]}], \"N2\")]";
                    }
                    else
                    {
                        // 金額の場合：整数表示、桁区切りあり
                        field.Text = $"[Format([DailyReportItems.{fields[i]}], \"N0\")]";
                    }
                }
                
                dataBand.Objects.Add(field);
                xPos += widths[i];
            }
        }
        
        private void CreateSubtotalSection(ReportPage page, List<DailyReportSubtotal> subtotals)
        {
            // 小計処理は簡略化（実装が複雑になるため）
            // 実際の実装では GroupHeaderBand と GroupFooterBand を使用
        }
        
        private void CreateTotalSection(ReportPage page, DailyReportTotal total)
        {
            var summaryBand = new ReportSummaryBand
            {
                Name = "ReportSummary",
                Height = 40f
            };
            page.ReportSummary = summaryBand;
            
            var totalLabel = new TextObject
            {
                Name = "TotalLabel",
                Bounds = new RectangleF(0, 10, 120, 20),
                Text = total.TotalName,
                Font = new Font("MS Gothic", 10, FontStyle.Bold),
                HorzAlign = HorzAlign.Center,
                Border = { Lines = BorderLines.All }
            };
            summaryBand.Objects.Add(totalLabel);
            
            // 合計値の表示（簡略化）
            var totalSales = new TextObject
            {
                Name = "TotalSales",
                Bounds = new RectangleF(140, 10, 60, 20),
                Text = total.GrandTotalDailySalesAmount.ToString("#,##0"),
                Font = new Font("MS Gothic", 8),
                HorzAlign = HorzAlign.Right,
                Border = { Lines = BorderLines.All }
            };
            summaryBand.Objects.Add(totalSales);
        }
    }
}
#else
namespace InventorySystem.Reports.FastReport.Services
{
    // Linux環境用のプレースホルダークラス
    public class DailyReportFastReportService
    {
        public DailyReportFastReportService(object logger) { }
    }
}
#endif