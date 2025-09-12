using System;
using FastReport;
using FastReport.Export.Pdf;

Console.WriteLine("=== FastReport.NET Trial 直接テスト (Windows) ===");
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
    Console.WriteLine("\n🔧 トラブルシューティング:");
    Console.WriteLine("  1. FastReport.NET Trial がインストールされているか確認");
    Console.WriteLine("  2. C:\\Program Files (x86)\\FastReports\\FastReport .NET Trial\\ にDLLが存在するか確認");
    Console.WriteLine("  3. Windows環境で実行しているか確認");
}

static void TestBasicReport()
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
               "✓ フォント設定が適用されています\n\n" +
               "本番環境では正式ライセンスの購入をご検討ください。",
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
        Subject = "Trial版動作確認",
        Creator = "在庫管理システム"
    };
    report.Export(pdfExport, "test_direct.pdf");
    
    Console.WriteLine("✅ 基本レポート生成完了: test_direct.pdf");
}

static void TestMinimalReport()
{
    using var report = new Report();
    
    // A4サイズ設定
    var page = new ReportPage
    {
        PaperWidth = 210f, // A4
        PaperHeight = 297f,
        LeftMargin = 15f,
        RightMargin = 15f,
        TopMargin = 15f,
        BottomMargin = 15f
    };
    report.Pages.Add(page);
    
    // タイトルバンド
    var title = new ReportTitleBand { Height = 50 };
    page.ReportTitle = title;
    
    var text = new TextObject
    {
        Bounds = new System.Drawing.RectangleF(0, 15, 180, 20),
        Text = "最小レポートテスト - Trial版透かし確認",
        Font = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold),
        HorzAlign = HorzAlign.Center
    };
    title.Objects.Add(text);
    
    // データバンド（内容）
    var dataBand = new DataBand { Height = 150 };
    page.Bands.Add(dataBand);
    
    var contentText = new TextObject
    {
        Bounds = new System.Drawing.RectangleF(10, 20, 160, 120),
        Text = "📋 最小構成でのPDF生成テスト\n\n" +
               "🔍 確認項目:\n" +
               "• Trial版透かしの表示\n" +
               "• PDF形式での出力\n" +
               "• テキストの正常表示\n" +
               "• ページレイアウトの確認\n\n" +
               "⏰ テスト実行時刻:\n" + 
               DateTime.Now.ToString("yyyy年MM月dd日 HH:mm:ss") + "\n\n" +
               "✨ FastReport.NET Trial版による生成",
        Font = new System.Drawing.Font("Arial", 10),
        WordWrap = true
    };
    dataBand.Objects.Add(contentText);
    
    // フッターバンド
    var footer = new PageFooterBand { Height = 30 };
    page.PageFooter = footer;
    
    var footerText = new TextObject
    {
        Bounds = new System.Drawing.RectangleF(0, 10, 180, 15),
        Text = "Generated by InventoryManagementSystem - FastReport.NET Trial",
        Font = new System.Drawing.Font("Arial", 8),
        HorzAlign = HorzAlign.Center
    };
    footer.Objects.Add(footerText);
    
    // レポート生成
    report.Prepare();
    
    // PDF出力
    var pdf = new PDFExport
    {
        EmbeddingFonts = true,
        Title = "FastReport最小テスト",
        Subject = "Trial版テスト",
        Creator = "在庫管理システム",
        Keywords = "FastReport, Trial, Test, PDF"
    };
    report.Export(pdf, "minimal_direct.pdf");
    
    Console.WriteLine("✅ 最小レポート生成完了: minimal_direct.pdf");
}

static void TestJapaneseFontReport()
{
    using var report = new Report();
    
    // A4縦設定
    var page = new ReportPage
    {
        PaperWidth = 210f,
        PaperHeight = 297f,
        LeftMargin = 20f,
        RightMargin = 20f,
        TopMargin = 20f,
        BottomMargin = 20f
    };
    report.Pages.Add(page);
    
    // タイトルバンド
    var title = new ReportTitleBand { Height = 60 };
    page.ReportTitle = title;
    
    var titleText = new TextObject
    {
        Bounds = new System.Drawing.RectangleF(0, 10, 170, 25),
        Text = "日本語フォントテスト",
        Font = new System.Drawing.Font("MS Gothic", 16, System.Drawing.FontStyle.Bold),
        HorzAlign = HorzAlign.Center
    };
    title.Objects.Add(titleText);
    
    var subtitleText = new TextObject
    {
        Bounds = new System.Drawing.RectangleF(0, 40, 170, 15),
        Text = "在庫管理システム - FastReport.NET Trial版",
        Font = new System.Drawing.Font("MS Gothic", 10),
        HorzAlign = HorzAlign.Center
    };
    title.Objects.Add(subtitleText);
    
    // データバンド
    var dataBand = new DataBand { Height = 180 };
    page.Bands.Add(dataBand);
    
    var japaneseContent = new TextObject
    {
        Bounds = new System.Drawing.RectangleF(10, 10, 150, 160),
        Text = "📊 商品日報サンプル\n\n" +
               "商品名：りんご（青森県産）\n" +
               "商品コード：APPLE-001-AOM\n" +
               "等級：特級\n" +
               "階級：L\n" +
               "荷印：山田農園\n\n" +
               "📈 売上実績\n" +
               "売上数量：150 箱\n" +
               "売上金額：¥75,000\n" +
               "粗利益１：¥22,500\n" +
               "粗利率１：30.0%\n\n" +
               "📅 処理日時：" + DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分") + "\n\n" +
               "※ このレポートはTrial版で生成されているため、\n" +
               "透かしが表示されます。",
        Font = new System.Drawing.Font("MS Gothic", 9),
        WordWrap = true
    };
    dataBand.Objects.Add(japaneseContent);
    
    // レポート生成
    report.Prepare();
    
    // PDF出力
    var pdf = new PDFExport
    {
        EmbeddingFonts = true,
        Title = "日本語フォントテスト",
        Subject = "商品日報サンプル",
        Creator = "在庫管理システム",
        Keywords = "日本語, フォント, 商品日報, FastReport"
    };
    report.Export(pdf, "japanese_font_test.pdf");
    
    Console.WriteLine("✅ 日本語フォントテスト完了: japanese_font_test.pdf");
}