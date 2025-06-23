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
    public class UnmatchListFastReportService : IUnmatchListReportService
    {
        private readonly ILogger<UnmatchListFastReportService> _logger;
        
        public UnmatchListFastReportService(ILogger<UnmatchListFastReportService> logger)
        {
            _logger = logger;
        }
        
        public byte[] GenerateUnmatchListReport(IEnumerable<UnmatchItem> unmatchItems, DateTime jobDate)
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
                CreateReportTitle(page, jobDate);
                
                // カラムヘッダー
                CreateColumnHeaders(page);
                
                // データ準備
                var dataTable = ConvertToDataTable(unmatchItems);
                report.RegisterData(dataTable, "UnmatchData");
                
                // データバンド
                CreateDataBand(page, report);
                
                // 集計行
                CreateSummary(page, unmatchItems.Count());
                
                // レポート生成
                // スクリプトのコンパイルを無効化（.NET 8.0対応）
                report.ScriptLanguage = global::FastReport.ScriptLanguage.None;
                report.Prepare();
                
                // PDF出力
                using var pdfExport = new PDFExport
                {
                    EmbeddingFonts = true,
                    Title = $"アンマッチリスト_{jobDate:yyyyMMdd}",
                    Subject = "アンマッチリスト",
                    Creator = "在庫管理システム"
                };
                
                using var stream = new MemoryStream();
                report.Export(pdfExport, stream);
                
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "アンマッチリストの生成中にエラーが発生しました");
                throw;
            }
        }
        
        private void CreateReportTitle(ReportPage page, DateTime jobDate)
        {
            var titleBand = new ReportTitleBand { Height = 50 };
            page.ReportTitle = titleBand;
            
            var titleText = new TextObject
            {
                Bounds = new RectangleF(0, 10, 400, 25),
                Text = "アンマッチリスト",
                Font = new Font("MS Gothic", 20, FontStyle.Bold),
                HorzAlign = HorzAlign.Center
            };
            titleBand.Objects.Add(titleText);
            
            var dateText = new TextObject
            {
                Bounds = new RectangleF(0, 35, 400, 15),
                Text = $"処理日付: {jobDate:yyyy年MM月dd日}",
                Font = new Font("MS Gothic", 10),
                HorzAlign = HorzAlign.Center
            };
            titleBand.Objects.Add(dateText);
        }
        
        private void CreateColumnHeaders(ReportPage page)
        {
            var headerBand = new PageHeaderBand { Height = 25 };
            page.PageHeader = headerBand;
            
            // ヘッダー列の定義
            var headers = new[]
            {
                new { Text = "種別", X = 0f, Width = 40f },
                new { Text = "得意先CD", X = 40f, Width = 60f },
                new { Text = "得意先名", X = 100f, Width = 80f },
                new { Text = "商品CD", X = 180f, Width = 50f },
                new { Text = "商品名", X = 230f, Width = 80f },
                new { Text = "数量", X = 310f, Width = 40f },
                new { Text = "伝票番号", X = 350f, Width = 50f },
                new { Text = "エラー", X = 400f, Width = 40f }
            };
            
            foreach (var header in headers)
            {
                var text = new TextObject
                {
                    Bounds = new RectangleF(header.X, 5, header.Width, 20),
                    Text = header.Text,
                    Font = new Font("MS Gothic", 10, FontStyle.Bold),
                    HorzAlign = HorzAlign.Center,
                    Border = new Border { Lines = BorderLines.All }
                };
                headerBand.Objects.Add(text);
            }
        }
        
        private DataTable ConvertToDataTable(IEnumerable<UnmatchItem> items)
        {
            var dataTable = new DataTable("UnmatchData");
            
            // カラム定義
            dataTable.Columns.Add("Category", typeof(string));
            dataTable.Columns.Add("CustomerCode", typeof(string));
            dataTable.Columns.Add("CustomerName", typeof(string));
            dataTable.Columns.Add("ProductCode", typeof(string));
            dataTable.Columns.Add("ProductName", typeof(string));
            dataTable.Columns.Add("Quantity", typeof(decimal));
            dataTable.Columns.Add("VoucherNumber", typeof(string));
            dataTable.Columns.Add("AlertType", typeof(string));
            
            // データ追加
            foreach (var item in items)
            {
                dataTable.Rows.Add(
                    item.Category,
                    item.CustomerCode,
                    item.CustomerName,
                    item.Key.ProductCode,
                    item.ProductName,
                    item.Quantity,
                    item.VoucherNumber,
                    item.AlertType
                );
            }
            
            return dataTable;
        }
        
        private void CreateDataBand(ReportPage page, Report report)
        {
            var dataBand = new DataBand
            {
                Name = "Data1",
                DataSource = report.GetDataSource("UnmatchData"),
                Height = 20
            };
            page.Bands.Add(dataBand);
            
            // データ列の定義（ヘッダーと同じ位置）
            var columns = new[]
            {
                new { Field = "[UnmatchData.Category]", X = 0f, Width = 40f, Align = HorzAlign.Left },
                new { Field = "[UnmatchData.CustomerCode]", X = 40f, Width = 60f, Align = HorzAlign.Left },
                new { Field = "[UnmatchData.CustomerName]", X = 100f, Width = 80f, Align = HorzAlign.Left },
                new { Field = "[UnmatchData.ProductCode]", X = 180f, Width = 50f, Align = HorzAlign.Center },
                new { Field = "[UnmatchData.ProductName]", X = 230f, Width = 80f, Align = HorzAlign.Left },
                new { Field = "[UnmatchData.Quantity]", X = 310f, Width = 40f, Align = HorzAlign.Right },
                new { Field = "[UnmatchData.VoucherNumber]", X = 350f, Width = 50f, Align = HorzAlign.Center },
                new { Field = "[UnmatchData.AlertType]", X = 400f, Width = 40f, Align = HorzAlign.Center }
            };
            
            foreach (var col in columns)
            {
                var text = new TextObject
                {
                    Bounds = new RectangleF(col.X, 0, col.Width, 20),
                    Text = col.Field,
                    Font = new Font("MS Gothic", 9),
                    HorzAlign = col.Align,
                    Border = new Border { Lines = BorderLines.All }
                };
                
                // 数量フィールドの場合はフォーマットを設定
                if (col.Field.Contains("Quantity"))
                {
                    text.Text = $"[Format([UnmatchData.Quantity], \"N2\")]";
                }
                
                dataBand.Objects.Add(text);
            }
        }
        
        private void CreateSummary(ReportPage page, int totalCount)
        {
            var summaryBand = new ReportSummaryBand { Height = 30 };
            page.ReportSummary = summaryBand;
            
            var summaryText = new TextObject
            {
                Bounds = new RectangleF(0, 5, 440, 20),
                Text = totalCount > 0 
                    ? $"アンマッチ件数: {totalCount} 件" 
                    : "アンマッチデータなし",
                Font = new Font("MS Gothic", 12, FontStyle.Bold),
                HorzAlign = HorzAlign.Center,
                Border = new Border { Lines = BorderLines.All }
            };
            summaryBand.Objects.Add(summaryText);
        }
    }
}
#else
namespace InventorySystem.Reports.FastReport.Services
{
    // Linux環境用のプレースホルダークラス
    public class UnmatchListFastReportService
    {
        public UnmatchListFastReportService(object logger) { }
    }
}
#endif