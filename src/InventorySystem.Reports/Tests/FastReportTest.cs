#if WINDOWS
using System;
using System.Data;
using FastReport;
using FastReport.Export.Pdf;
#endif

namespace InventorySystem.Reports.Tests
{
    public class FastReportTest
    {
        public static void TestBasicReport()
        {
#if WINDOWS
            Console.WriteLine("FastReport.NET Trial 動作テスト");
            
            try
            {
                using var report = new Report();
                
                // 簡単なレポート作成
                var page = new ReportPage();
                report.Pages.Add(page);
                
                var band = new ReportTitleBand { Height = 50 };
                page.ReportTitle = band;
                
                var text = new TextObject
                {
                    Bounds = new System.Drawing.RectangleF(0, 0, 300, 30),
                    Text = "テストレポート",
#pragma warning disable CA1416 // プラットフォーム互換性
                    Font = new System.Drawing.Font("MS Gothic", 14)
#pragma warning restore CA1416
                };
                band.Objects.Add(text);
                
                report.Prepare();
                
                // PDF出力
                var pdfExport = new PDFExport();
                report.Export(pdfExport, "test.pdf");
                
                Console.WriteLine("テストレポートを生成しました: test.pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー: {ex.Message}");
            }
#else
            Console.WriteLine("FastReport機能は Windows でのみ利用可能です");
#endif
        }
        
        public static void TestMinimalReport()
        {
#if WINDOWS
            try
            {
                using var report = new Report();
                
                // ページ追加
                var page = new ReportPage();
                report.Pages.Add(page);
                
                // タイトルバンド
                var title = new ReportTitleBand { Height = 50 };
                page.ReportTitle = title;
                
                // タイトルテキスト（フォーマットなし）
                var text = new TextObject
                {
                    Bounds = new System.Drawing.RectangleF(0, 0, 300, 30),
                    Text = "テストレポート",
#pragma warning disable CA1416 // プラットフォーム互換性
                    Font = new System.Drawing.Font("MS Gothic", 14)
#pragma warning restore CA1416
                };
                title.Objects.Add(text);
                
                // レポート生成
                report.Prepare();
                
                // PDF出力
                var pdf = new PDFExport();
                report.Export(pdf, "minimal_test.pdf");
                
                Console.WriteLine("成功: minimal_test.pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー: {ex.Message}");
            }
#else
            Console.WriteLine("FastReport機能は Windows でのみ利用可能です");
#endif
        }
    }
}