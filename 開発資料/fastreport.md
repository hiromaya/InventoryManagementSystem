FastReportの開発仕様について調査します。FastReportについて詳しい情報を調査します。# FastReport .NET 開発仕様書

## 📋 概要

FastReportは、.NET環境向けの高機能な帳票生成ツールです。在庫管理システムの帳票開発において、QuestPDFの代替として採用されました。

## 🔧 技術仕様

### バージョン情報
- **最新バージョン**: 2024.2（2024年4月リリース）
- **対応フレームワーク**: 
  - .NET 6以上（推奨）
  - .NET Framework 4.6.2以上
  - .NET 8、.NET 9対応

### 主要パッケージ
```xml
<!-- NuGetパッケージ -->
<PackageReference Include="FastReport.Core" Version="2024.2.*" />
<PackageReference Include="FastReport.Core.Skia" Version="2024.2.*" />
<PackageReference Include="FastReport.Data.MsSql" Version="2024.2.*" />
```

## 🏗️ アーキテクチャ

### 帳票の構成要素

#### 1. バンド構造（Band-Oriented）
FastReportは13種類のバンドをサポート：
- **Report Title** - レポートタイトル
- **Report Summary** - レポートサマリー
- **Page Header** - ページヘッダー
- **Page Footer** - ページフッター
- **Column Header** - カラムヘッダー
- **Column Footer** - カラムフッター
- **Data Header** - データヘッダー
- **Data** - データバンド
- **Data Footer** - データフッター
- **Group Header** - グループヘッダー
- **Group Footer** - グループフッター
- **Child** - 子バンド
- **Overlay** - オーバーレイ

#### 2. レポートオブジェクト
- **TextObject** - テキスト表示
- **PictureObject** - 画像
- **LineObject** - 線
- **ShapeObject** - 図形
- **BarcodeObject** - バーコード
- **MatrixObject** - マトリックス
- **TableObject** - テーブル
- **CheckboxObject** - チェックボックス

## 💻 基本的な実装方法

### 1. レポートの作成（コードから）

```csharp
using FastReport;
using FastReport.Data;
using FastReport.Export.Pdf;

public class DailyReportService
{
    public void GenerateReport(DataSet dataSet)
    {
        // レポートインスタンスの作成
        Report report = new Report();
        
        // データソースの登録
        report.RegisterData(dataSet.Tables["DailyReport"], "DailyReport");
        report.GetDataSource("DailyReport").Enabled = true;
        
        // ページの作成
        ReportPage page = new ReportPage();
        page.Name = "Page1";
        page.PaperWidth = 297f; // A4横
        page.PaperHeight = 210f;
        page.LeftMargin = 10f;
        page.RightMargin = 10f;
        page.TopMargin = 10f;
        page.BottomMargin = 10f;
        report.Pages.Add(page);
        
        // レポートタイトルバンドの追加
        ReportTitleBand title = new ReportTitleBand();
        title.Name = "ReportTitle1";
        title.Height = Units.Centimeters * 2;
        page.ReportTitle = title;
        
        // タイトルテキスト
        TextObject titleText = new TextObject();
        titleText.Name = "TitleText";
        titleText.Bounds = new RectangleF(0, 0, Units.Centimeters * 27, Units.Centimeters * 1);
        titleText.Text = "商品日報";
        titleText.Font = new Font("MS Gothic", 16, FontStyle.Bold);
        titleText.HorzAlign = HorzAlign.Center;
        title.Objects.Add(titleText);
        
        // データバンドの追加
        DataBand data = new DataBand();
        data.Name = "Data1";
        data.DataSource = report.GetDataSource("DailyReport");
        data.Height = Units.Centimeters * 0.8f;
        page.Bands.Add(data);
        
        // データフィールドの追加
        TextObject productCode = new TextObject();
        productCode.Name = "ProductCode";
        productCode.Bounds = new RectangleF(0, 0, Units.Centimeters * 3, Units.Centimeters * 0.6f);
        productCode.Text = "[DailyReport.ProductCode]";
        productCode.Font = new Font("MS Gothic", 9);
        data.Objects.Add(productCode);
        
        // レポートの準備と表示
        report.Prepare();
        
        // PDF出力
        PDFExport pdfExport = new PDFExport();
        report.Export(pdfExport, "daily_report.pdf");
    }
}
```

### 2. テンプレートファイル（.frx）の使用

```csharp
public class ReportGenerator
{
    private readonly string _templatePath;
    
    public ReportGenerator(string templatePath)
    {
        _templatePath = templatePath;
    }
    
    public void GenerateFromTemplate(DataSet dataSet, string outputPath)
    {
        using (Report report = new Report())
        {
            // テンプレートの読み込み
            report.Load(_templatePath);
            
            // データソースの登録
            report.RegisterData(dataSet);
            
            // パラメータの設定
            report.SetParameterValue("ReportDate", DateTime.Now);
            report.SetParameterValue("Department", "営業部");
            
            // レポートの準備
            report.Prepare();
            
            // PDF出力（日本語フォント対応）
            PDFExport export = new PDFExport();
            export.EmbeddingFonts = true; // フォント埋め込み
            export.UseFileCache = true;   // 大量データ対応
            
            report.Export(export, outputPath);
        }
    }
}
```

### 3. 日本語対応の設定

```csharp
public class JapaneseReportConfig
{
    public static void ConfigureJapaneseSupport(Report report)
    {
        // 日本語フォントの設定
        foreach (ReportComponentBase component in report.AllObjects)
        {
            if (component is TextObject textObject)
            {
                // MS ゴシックまたはメイリオを使用
                textObject.Font = new Font("MS Gothic", textObject.Font.Size, textObject.Font.Style);
            }
        }
        
        // PDF出力時の設定
        report.ExportEventHandler += (sender, e) =>
        {
            if (e.Export is PDFExport pdfExport)
            {
                pdfExport.EmbeddingFonts = true;
                pdfExport.PdfCompliance = PDFExport.PdfStandard.PdfA_2a;
            }
        };
    }
}
```

## 📊 在庫管理システムでの活用例

### 1. 商品日報の実装

```csharp
public class DailyProductReport
{
    private readonly Report _report;
    
    public DailyProductReport()
    {
        _report = new Report();
        InitializeReport();
    }
    
    private void InitializeReport()
    {
        // A4横向き設定
        ReportPage page = new ReportPage();
        page.Landscape = true;
        _report.Pages.Add(page);
        
        // ヘッダーバンド
        PageHeaderBand header = new PageHeaderBand();
        header.Height = Units.Centimeters * 3;
        page.PageHeader = header;
        
        // 列ヘッダーの作成
        CreateColumnHeaders(header);
        
        // グループバンド（商品分類別）
        GroupHeaderBand groupHeader = new GroupHeaderBand();
        groupHeader.Height = Units.Centimeters * 1;
        groupHeader.Condition = "[DailyReport.ProductCategory]";
        groupHeader.SortOrder = FastReport.SortOrder.Ascending;
        page.Bands.Add(groupHeader);
        
        // グループフッター（小計）
        GroupFooterBand groupFooter = new GroupFooterBand();
        groupFooter.Height = Units.Centimeters * 1;
        AddSubtotalFields(groupFooter);
        groupHeader.GroupFooter = groupFooter;
    }
    
    private void CreateColumnHeaders(PageHeaderBand header)
    {
        string[] columns = { "商品コード", "商品名", "等級", "階級", "荷印", 
                           "当日売上数量", "当日売上金額", "粗利益", "粗利率" };
        float xPos = 0;
        
        foreach (string column in columns)
        {
            TextObject colHeader = new TextObject();
            colHeader.Bounds = new RectangleF(xPos, 50, 80, 20);
            colHeader.Text = column;
            colHeader.Font = new Font("MS Gothic", 9, FontStyle.Bold);
            colHeader.Border.Lines = BorderLines.All;
            colHeader.FillColor = Color.LightGray;
            header.Objects.Add(colHeader);
            xPos += 80;
        }
    }
}
```

### 2. データベース接続

```csharp
public class FastReportDataConfig
{
    public static void ConfigureDataConnection(Report report, string connectionString)
    {
        // SQL Server接続の設定
        MsSqlDataConnection connection = new MsSqlDataConnection();
        connection.ConnectionString = connectionString;
        report.Dictionary.Connections.Add(connection);
        
        // テーブルの追加
        TableDataSource table = new TableDataSource();
        table.TableName = "CP_InventoryMaster";
        table.Name = "Inventory";
        table.SelectCommand = @"
            SELECT 
                ProductCode,
                ProductName,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                DailySalesQuantity,
                DailySalesAmount,
                DailyGrossProfit,
                CASE 
                    WHEN DailySalesAmount > 0 
                    THEN (DailyGrossProfit / DailySalesAmount) * 100 
                    ELSE 0 
                END AS GrossProfitRate
            FROM CP_InventoryMaster
            WHERE ProcessDate = @ProcessDate
            ORDER BY ProductCategory, ProductCode";
        
        // パラメータの追加
        table.Parameters.Add(new CommandParameter("ProcessDate", DateTime.Today));
        
        connection.Tables.Add(table);
    }
}
```

## 🎨 高度な機能

### 1. 条件付き書式

```csharp
// 粗利率がマイナスの場合は赤字表示
TextObject grossProfitRate = new TextObject();
grossProfitRate.Text = "[Inventory.GrossProfitRate]";
grossProfitRate.Format = new NumberFormat();
grossProfitRate.Format.DecimalDigits = 2;

// 条件付き書式の追加
grossProfitRate.BeforePrint += (sender, e) =>
{
    if (grossProfitRate.Value != null && Convert.ToDecimal(grossProfitRate.Value) < 0)
    {
        grossProfitRate.TextColor = Color.Red;
    }
    else
    {
        grossProfitRate.TextColor = Color.Black;
    }
};
```

### 2. 集計機能

```csharp
// 合計行の追加
ReportSummaryBand summary = new ReportSummaryBand();
summary.Height = Units.Centimeters * 2;
page.ReportSummary = summary;

// 合計フィールド
TextObject totalSales = new TextObject();
totalSales.Text = "合計売上: [Sum([Inventory.DailySalesAmount])]";
totalSales.Font = new Font("MS Gothic", 11, FontStyle.Bold);
summary.Objects.Add(totalSales);
```

### 3. エクスポート機能

```csharp
public class ReportExporter
{
    public void ExportReport(Report report, string format, string outputPath)
    {
        report.Prepare();
        
        switch (format.ToLower())
        {
            case "pdf":
                PDFExport pdfExport = new PDFExport();
                pdfExport.EmbeddingFonts = true;
                report.Export(pdfExport, outputPath);
                break;
                
            case "excel":
                Excel2007Export excelExport = new Excel2007Export();
                excelExport.OpenAfterExport = false;
                report.Export(excelExport, outputPath);
                break;
                
            case "html":
                HTMLExport htmlExport = new HTMLExport();
                htmlExport.SinglePage = true;
                htmlExport.Navigator = true;
                report.Export(htmlExport, outputPath);
                break;
        }
    }
}
```

## ⚡ パフォーマンス最適化

### 1. 大量データの処理

```csharp
// ページキャッシュの有効化
report.UseFileCache = true;
report.MaxPages = 0; // 無制限

// バッチ処理
DataBand dataBand = report.FindObject("Data1") as DataBand;
dataBand.MaxRows = 1000; // 1ページあたりの最大行数
```

### 2. メモリ管理

```csharp
// 処理後のリソース解放
report.Dispose();
GC.Collect();
GC.WaitForPendingFinalizers();
```

## 🔒 ライセンスと制限事項

### オープンソース版の制限
- PDF出力にはプラグインが必要
- デザイナーはCommunity Edition（機能制限あり）
- 一部の高度な機能は使用不可

### 商用版の利点
- フル機能のPDF出力（暗号化、電子署名対応）
- 完全なデザイナー
- 優先サポート
- 追加のエクスポート形式

## 📝 開発時の注意点

1. **日本語フォント**: 必ずMS GothicやMeiryoなど、日本語対応フォントを指定
2. **文字コード**: UTF-8を使用
3. **改行コード**: Windows環境ではCRLF
4. **数値フォーマット**: 日本の会計基準に合わせた書式設定
5. **メモリ管理**: 大量データ処理時はUseFileCacheを有効化

## 🔗 参考リソース

- [公式ドキュメント](https://www.fast-report.com/public_download/docs/FRNet/online/en/ProgrammerManual/)
- [GitHub（オープンソース版）](https://github.com/FastReports/FastReport)
- [NuGetパッケージ](https://www.nuget.org/packages/FastReport.Core/)
- [日本語フォーラム](https://www.fast-report.com/forum/)