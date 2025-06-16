using InventorySystem.Core.Entities;
using InventorySystem.Reports.Services;
using QuestPDF.Infrastructure;

namespace InventorySystem.Console;

public class TestWithoutDatabase
{
    public static void RunPdfTest()
    {
        // QuestPDF ライセンス設定
        QuestPDF.Settings.License = LicenseType.Community;
        
        // テスト用のアンマッチデータ作成
        var testUnmatchItems = new List<UnmatchItem>
        {
            new UnmatchItem
            {
                Category = "掛売上",
                CustomerCode = "C001",
                CustomerName = "Test Customer A",
                Key = new InventoryKey 
                { 
                    ProductCode = "00008",
                    GradeCode = "001",
                    ClassCode = "001",
                    ShippingMarkCode = "1008",
                    ShippingMarkName = "テスト荷印8"
                },
                ProductName = "テスト商品H",
                GradeName = "特級",
                ClassName = "大",
                Quantity = -5.00m,
                UnitPrice = 1300.00m,
                Amount = -6500.00m,
                VoucherNumber = "S0003",
                AlertType = "在庫0",
                ProductCategory1 = "2"
            },
            new UnmatchItem
            {
                Category = "掛売上",
                CustomerCode = "C004",
                CustomerName = "テスト得意先D",
                Key = new InventoryKey 
                { 
                    ProductCode = "99999",
                    GradeCode = "999",
                    ClassCode = "999",
                    ShippingMarkCode = "9999",
                    ShippingMarkName = "存在しない荷印"
                },
                ProductName = "",
                GradeName = "",
                ClassName = "",
                Quantity = -8.00m,
                UnitPrice = 1000.00m,
                Amount = -8000.00m,
                VoucherNumber = "S0004",
                AlertType = "該当無",
                ProductCategory1 = ""
            },
            new UnmatchItem
            {
                Category = "掛買",
                CustomerCode = "S003",
                CustomerName = "テスト仕入先C",
                Key = new InventoryKey 
                { 
                    ProductCode = "88888",
                    GradeCode = "888",
                    ClassCode = "888",
                    ShippingMarkCode = "8888",
                    ShippingMarkName = "存在しない商品"
                },
                ProductName = "",
                GradeName = "",
                ClassName = "",
                Quantity = 25.00m,
                UnitPrice = 1200.00m,
                Amount = 30000.00m,
                VoucherNumber = "P0003",
                AlertType = "該当無",
                ProductCategory1 = ""
            }
        };
        
        try
        {
            System.Console.WriteLine("=== PDF生成テスト開始 ===");
            System.Console.WriteLine($"テストデータ: {testUnmatchItems.Count}件");
            
            // PDF生成
            var reportService = new UnmatchListReportService();
            var pdfBytes = reportService.GenerateUnmatchListReport(testUnmatchItems, DateTime.Now);
            
            // ファイル保存
            var outputPath = Path.Combine(Environment.CurrentDirectory, 
                $"test_unmatch_list_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            
            File.WriteAllBytes(outputPath, pdfBytes);
            System.Console.WriteLine($"PDF生成成功: {outputPath}");
            
            // PDFを開く
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
            }
            
            System.Console.WriteLine("=== PDF生成テスト完了 ===");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"エラー: {ex.Message}");
        }
    }
}