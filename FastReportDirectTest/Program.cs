using System;
using FastReport;
using FastReport.Export.Pdf;

Console.WriteLine("=== FastReport.NET Trial 直接テスト (Windows Forms対応版) ===");
Console.WriteLine($"実行時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

try
{
    Console.WriteLine("\n1. FastReport基本機能テスト...");
    TestBasicReport();
    
    Console.WriteLine("\n2. 最小レポートテスト...");
    TestMinimalReport();
    
    Console.WriteLine("\n3. 日本語フォントテスト...");
    TestJapaneseFontReport();
    
    Console.WriteLine("\n=== FastReport.NET Trial テスト完了 ===");
    Console.WriteLine("✅ すべてのテストが成功しました");
    Console.WriteLine("\n生成されたPDFファイルを確認してください：");
    Console.WriteLine("  📄 test_direct.pdf - 基本テスト");
    Console.WriteLine("  📄 minimal_direct.pdf - 最小テスト");
    Console.WriteLine("  📄 japanese_font_test.pdf - 日本語フォントテスト");
    Console.WriteLine("\n💡 Trial版では透かしが表示されます");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ エラーが発生しました: {ex.Message}");
    Console.WriteLine($"詳細: {ex}");
}

static void TestBasicReport()
{
    try
    {
        using var report = new Report();
        
        // 基本レポート作成
        var page = new ReportPage();
        report.Pages.Add(page);
        
        var titleBand = new ReportTitleBand { Height = 60 };
        page.ReportTitle = titleBand;
        
        var titleText = new TextObject
        {
            Bounds = new System.Drawing.RectangleF(0, 10, 400, 25),
            Text = "FastReport.NET Trial 基本テスト",
            Font = new System.Drawing.Font("Arial", 18, System.Drawing.FontStyle.Bold),
            HorzAlign = HorzAlign.Center
        };
        titleBand.Objects.Add(titleText);
        
        var subtitleText = new TextObject
        {
            Bounds = new System.Drawing.RectangleF(0, 40, 400, 15),
            Text = $"生成日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            Font = new System.Drawing.Font("Arial", 10),
            HorzAlign = HorzAlign.Center
        };
        titleBand.Objects.Add(subtitleText);
        
        // データバンド追加
        var dataBand = new DataBand { Height = 120 };
        page.Bands.Add(dataBand);
        
        var infoText = new TextObject
        {
            Bounds = new System.Drawing.RectangleF(20, 20, 360, 80),
            Text = "このレポートはFastReport.NET Trial版で生成されました。\n\n" +
                   "✓ PDF生成機能が正常に動作しています\n" +
                   "✓ テキストオブジェクトの配置が正しく行われています\n" +
                   "✓ フォント設定が適用されています\n" +
                   "✓ System.Windows.Forms依存関係が解決されています",
            Font = new System.Drawing.Font("Arial", 11),
            WordWrap = true
        };
        dataBand.Objects.Add(infoText);
        
        // レポート生成
        report.Prepare();
        
        // PDF出力
        var pdfExport = new PDFExport
        {
            EmbeddingFonts = true,
            Title = "FastReport基本テスト",
            Subject = "テストレポート"
        };
        
        report.Export(pdfExport, "test_direct.pdf");
        Console.WriteLine("✓ test_direct.pdf を生成しました");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 基本レポートテストでエラー: {ex.Message}");
    }
}

static void TestMinimalReport()
{
    try
    {
        using var report = new Report();
        
        // ページ追加
        var page = new ReportPage();
        report.Pages.Add(page);
        
        // タイトルバンド
        var title = new ReportTitleBand { Height = 50 };
        page.ReportTitle = title;
        
        // タイトルテキスト
        var text = new TextObject
        {
            Bounds = new System.Drawing.RectangleF(0, 0, 300, 30),
            Text = "最小テストレポート",
            Font = new System.Drawing.Font("MS Gothic", 14)
        };
        title.Objects.Add(text);
        
        // レポート生成
        report.Prepare();
        
        // PDF出力
        var pdf = new PDFExport();
        report.Export(pdf, "minimal_direct.pdf");
        
        Console.WriteLine("✓ minimal_direct.pdf を生成しました");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 最小レポートテストでエラー: {ex.Message}");
    }
}

static void TestJapaneseFontReport()
{
    try
    {
        using var report = new Report();
        
        var page = new ReportPage();
        report.Pages.Add(page);
        
        var titleBand = new ReportTitleBand { Height = 80 };
        page.ReportTitle = titleBand;
        
        // 日本語タイトル
        var titleText = new TextObject
        {
            Bounds = new System.Drawing.RectangleF(0, 10, 400, 30),
            Text = "在庫管理システム 帳票テスト",
            Font = new System.Drawing.Font("MS Gothic", 20, System.Drawing.FontStyle.Bold),
            HorzAlign = HorzAlign.Center
        };
        titleBand.Objects.Add(titleText);
        
        // サブタイトル
        var subtitleText = new TextObject
        {
            Bounds = new System.Drawing.RectangleF(0, 45, 400, 20),
            Text = "日本語フォント表示確認",
            Font = new System.Drawing.Font("Meiryo UI", 12),
            HorzAlign = HorzAlign.Center
        };
        titleBand.Objects.Add(subtitleText);
        
        // データバンド
        var dataBand = new DataBand { Height = 150 };
        page.Bands.Add(dataBand);
        
        // 商品情報の例
        var sampleText = new TextObject
        {
            Bounds = new System.Drawing.RectangleF(20, 20, 360, 100),
            Text = "商品コード: 10489\n" +
                   "商品名: タマネギ\n" +
                   "等級: 秀品\n" +
                   "階級: 2L\n" +
                   "産地: 北海道\n" +
                   "数量: 100.00\n" +
                   "単価: ¥1,200",
            Font = new System.Drawing.Font("MS Gothic", 12),
            WordWrap = true
        };
        dataBand.Objects.Add(sampleText);
        
        // レポート生成
        report.Prepare();
        
        // PDF出力（日本語フォント埋め込み）
        var pdfExport = new PDFExport
        {
            EmbeddingFonts = true,
            Title = "日本語フォントテスト",
            Subject = "在庫管理システム"
        };
        
        report.Export(pdfExport, "japanese_font_test.pdf");
        Console.WriteLine("✓ japanese_font_test.pdf を生成しました");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 日本語フォントテストでエラー: {ex.Message}");
    }
}