using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using InventorySystem.Core.Entities;

namespace InventorySystem.Reports.Services;

/// <summary>
/// 商品日報PDF生成サービス
/// </summary>
public class DailyReportPdfService
{
    private static bool _isFontRegistered = false;
    private static string _registeredFontFamily = "";
    
    static DailyReportPdfService()
    {
        try
        {
            // 日本語フォントを動的に検出して登録
            var fontPath = FontHelper.GetJapaneseFontPath();
            
            // フォントファイルを直接読み込んで登録
            var fontBytes = File.ReadAllBytes(fontPath);
            using var fontStream = new MemoryStream(fontBytes);
            FontManager.RegisterFont(fontStream);
            
            // フォントファミリー名を推測（Notoフォントの場合）
            if (fontPath.Contains("NotoSansCJK"))
            {
                _registeredFontFamily = "Noto Sans CJK JP";
            }
            else if (fontPath.Contains("ipag"))
            {
                _registeredFontFamily = "IPAGothic";
            }
            else
            {
                _registeredFontFamily = "Noto Sans";
            }
            
            _isFontRegistered = true;
            
            System.Console.WriteLine($"日本語フォントを登録しました: {fontPath}");
            System.Console.WriteLine($"使用するフォントファミリー: {_registeredFontFamily}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"フォント登録エラー: {ex.Message}");
            _isFontRegistered = false;
        }
    }

    /// <summary>
    /// 商品日報PDFを生成
    /// </summary>
    public byte[] GenerateDailyReport(List<DailyReportItem> reportItems, 
        List<DailyReportSubtotal> subtotals, DailyReportTotal total, DateTime reportDate)
    {
        // QuestPDFデバッグを有効化
        QuestPDF.Settings.EnableDebugging = true;
        
        // デバッグ情報を出力
        var pageSize = PageSizes.A3.Landscape();
        Console.WriteLine($"Page Width: {pageSize.Width}, Page Height: {pageSize.Height}");
        
        // 列幅の合計を確認
        var columnWidths = new[] { 60, 18, 22, 18, 18, 16, 16, 16, 20, 16, 20, 16, 22, 20, 16, 20, 16 };
        var totalWidth = columnWidths.Sum();
        Console.WriteLine($"Total column width: {totalWidth}mm");
        Console.WriteLine($"Available width: ~400mm");
        Console.WriteLine($"Margin: {400 - totalWidth}mm");
        
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A3.Landscape());
                page.Margin(10, Unit.Millimetre);
                
                // フォント設定（登録された日本語フォントを使用）
                if (_isFontRegistered && !string.IsNullOrEmpty(_registeredFontFamily))
                {
                    page.DefaultTextStyle(x => x
                        .FontSize(9)
                        .FontFamily(_registeredFontFamily));
                }
                else
                {
                    page.DefaultTextStyle(x => x
                        .FontSize(9));
                }

                page.Header().Element(header => ComposeHeader(header, reportDate));
                page.Content().Element(content => ComposeContent(content, reportItems, subtotals, total));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container, DateTime reportDate)
    {
        container.Row(row =>
        {
            row.ConstantItem(200).Column(column =>
            {
                column.Item().Text($"作成日：{DateTime.Now:yyyy年MM月dd日HH時mm分ss秒}")
                    .FontSize(8);
            });
            
            row.RelativeItem().Column(column =>
            {
                column.Item().AlignCenter().Text($"※　{reportDate:yyyy年MM月dd日}　商　品　日　報　※")
                    .FontSize(10).Bold();
            });
            
            row.ConstantItem(80).Column(column =>
            {
                column.Item().AlignRight().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" 頁").FontSize(10);
                });
            });
        });
    }

    private void ComposeContent(IContainer container, List<DailyReportItem> reportItems,
        List<DailyReportSubtotal> subtotals, DailyReportTotal total)
    {
        if (!reportItems.Any())
        {
            // データがない場合
            container.Column(column =>
            {
                column.Spacing(20);
                column.Item().AlignCenter().PaddingTop(100).Text("商品日報データなし")
                    .FontSize(16).Bold();
            });
            return;
        }

        // デバッグ情報を追加
        var pageWidth = PageSizes.A3.Landscape().Width;
        var pageWidthMm = pageWidth / 2.83465f; // ポイントをmmに変換
        Console.WriteLine($"Page width in mm: {pageWidthMm:F2}");
        Console.WriteLine($"Usable width (with 10mm margins): {pageWidthMm - 20:F2}");
        
        // データがある場合
        container.Column(column =>
        {
            column.Spacing(10);
            
            // ヘッダー行
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    // 合計を340mm以内に収める（マージン考慮）
                    columns.ConstantColumn(60, Unit.Millimetre);  // 商品名（80→60に縮小）
                    
                    // === 日計セクション（11列）===
                    columns.ConstantColumn(18, Unit.Millimetre);  // 売上数量（20→18）
                    columns.ConstantColumn(22, Unit.Millimetre);  // 売上金額（25→22）
                    columns.ConstantColumn(18, Unit.Millimetre);  // 仕入値引（20→18）
                    columns.ConstantColumn(18, Unit.Millimetre);  // 在庫調整（20→18）
                    columns.ConstantColumn(16, Unit.Millimetre);  // 加工費（18→16）
                    columns.ConstantColumn(16, Unit.Millimetre);  // 振替（18→16）
                    columns.ConstantColumn(16, Unit.Millimetre);  // 奨励金（18→16）
                    columns.ConstantColumn(20, Unit.Millimetre);  // １粗利益（22→20）
                    columns.ConstantColumn(16, Unit.Millimetre);  // １粗利率（18→16）
                    columns.ConstantColumn(20, Unit.Millimetre);  // ２粗利益（22→20）
                    columns.ConstantColumn(16, Unit.Millimetre);  // ２粗利率（18→16）
                    
                    // === 月計セクション（5列）===
                    columns.ConstantColumn(22, Unit.Millimetre);  // 売上金額（25→22）
                    columns.ConstantColumn(20, Unit.Millimetre);  // １粗利益（22→20）
                    columns.ConstantColumn(16, Unit.Millimetre);  // １粗利率（18→16）
                    columns.ConstantColumn(20, Unit.Millimetre);  // ２粗利益（22→20）
                    columns.ConstantColumn(16, Unit.Millimetre);  // ２粗利率（18→16）
                    
                    // 合計: 60 + 186 + 94 = 340mm（安全マージン考慮）
                });

                // 2階層ヘッダー構造の実装（RowSpan問題を解決）
                table.Header(header =>
                {
                    // 第1階層：セクションヘッダー
                    header.Cell().Text("").FontSize(7); // 商品名列（空白セル）
                    
                    // 日計セクション（11列を結合）
                    header.Cell().ColumnSpan(11).AlignCenter()
                        .Padding(1)
                        .Text("日　　　　　　計").FontSize(8).Bold();
                    
                    // 月計セクション（5列を結合）
                    header.Cell().ColumnSpan(5).AlignCenter()
                        .Padding(1)
                        .Text("月　　　　計").FontSize(8).Bold();
                    
                    // 第2階層：項目ヘッダー
                    
                    // 商品名
                    header.Cell().Text("商　品　名").FontSize(7).AlignCenter().Bold();
                    
                    // 日計の項目（11列）
                    header.Cell().Text("売上数量").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("売上金額").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("仕入値引").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("在庫調整").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("加工費").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("振替").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("奨励金").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("１粗利益").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("１粗利率").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("２粗利益").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("２粗利率").FontSize(7).AlignCenter().Bold();
                    
                    // 月計の項目（5列）
                    header.Cell().Text("売上金額").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("１粗利益").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("１粗利率").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("２粗利益").FontSize(7).AlignCenter().Bold();
                    header.Cell().Text("２粗利率").FontSize(7).AlignCenter().Bold();
                });

                // データ行
                string currentCategory = "";
                foreach (var item in reportItems)
                {
                    // 商品分類1が変わったら大分類計を印字
                    if (currentCategory != item.ProductCategory1 && !string.IsNullOrEmpty(currentCategory))
                    {
                        var subtotal = subtotals.FirstOrDefault(s => s.ProductCategory1 == currentCategory);
                        if (subtotal != null)
                        {
                            AddSubtotalRow(table, subtotal);
                        }
                    }
                    currentCategory = item.ProductCategory1;

                    AddDataRow(table, item);
                }

                // 最後の大分類計
                if (!string.IsNullOrEmpty(currentCategory))
                {
                    var lastSubtotal = subtotals.FirstOrDefault(s => s.ProductCategory1 == currentCategory);
                    if (lastSubtotal != null)
                    {
                        AddSubtotalRow(table, lastSubtotal);
                    }
                }

                // 合計行
                AddTotalRow(table, total);
            });
        });
    }

    private void AddDataRow(TableDescriptor table, DailyReportItem item)
    {
        // 商品名列のみ（左寄せ、商品コード列は削除）
        table.Cell().Padding(1).AlignLeft().Text(TruncateText(item.ProductName, 25)).FontSize(7);
        // 数値列（右寄せ）
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(item.DailySalesQuantity)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailySalesAmount)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyPurchaseDiscount)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyInventoryAdjustment)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyProcessingCost)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyTransfer)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyIncentive)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyGrossProfit1)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.DailyGrossProfitRate1)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyGrossProfit2)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.DailyGrossProfitRate2)).FontSize(7);
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(item.MonthlySalesAmount)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.MonthlyGrossProfit1)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.MonthlyGrossProfitRate1)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.MonthlyGrossProfit2)).FontSize(7);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.MonthlyGrossProfitRate2)).FontSize(7);
    }

    private void AddSubtotalRow(TableDescriptor table, DailyReportSubtotal subtotal)
    {
        // 商品名列のみ（商品コード列削除）
        table.Cell().Padding(1).Text(subtotal.SubtotalName).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(subtotal.TotalDailySalesQuantity)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailySalesAmount)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyPurchaseDiscount)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyInventoryAdjustment)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyProcessingCost)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyTransfer)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyIncentive)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyGrossProfit1)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalDailyGrossProfitRate1)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyGrossProfit2)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalDailyGrossProfitRate2)).FontSize(7).Bold();
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(subtotal.TotalMonthlySalesAmount)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalMonthlyGrossProfit1)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalMonthlyGrossProfitRate1)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalMonthlyGrossProfit2)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalMonthlyGrossProfitRate2)).FontSize(7).Bold();
    }

    private void AddTotalRow(TableDescriptor table, DailyReportTotal total)
    {
        // 商品名列のみ（商品コード列削除）
        table.Cell().Padding(1).Text(total.TotalName).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(total.GrandTotalDailySalesQuantity)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailySalesAmount)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyPurchaseDiscount)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyInventoryAdjustment)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyProcessingCost)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyTransfer)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyIncentive)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyGrossProfit1)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalDailyGrossProfitRate1)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyGrossProfit2)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalDailyGrossProfitRate2)).FontSize(7).Bold();
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(total.GrandTotalMonthlySalesAmount)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalMonthlyGrossProfit1)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalMonthlyGrossProfitRate1)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalMonthlyGrossProfit2)).FontSize(7).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalMonthlyGrossProfitRate2)).FontSize(7).Bold();
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.CurrentPageNumber().FontSize(10);
        });
    }

    // フォーマット用メソッド
    private string FormatCode(string code, int width)
    {
        if (string.IsNullOrEmpty(code)) return "";
        if (int.TryParse(code, out var numCode))
        {
            return numCode.ToString($"D{width}");
        }
        return code;
    }

    private string FormatQuantity(decimal quantity)
    {
        // ZZ,ZZ9.99-形式（仕様書通り）
        if (quantity == 0) return "";
        var formattedValue = Math.Abs(quantity).ToString("N2", CultureInfo.InvariantCulture);
        if (quantity < 0)
        {
            return formattedValue + "-";
        }
        return formattedValue;
    }

    private string FormatAmount(decimal amount)
    {
        // ZZ,ZZZ,ZZ9-形式（仕様書通り）
        if (amount == 0) return "";
        var formattedValue = Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture);
        if (amount < 0)
        {
            return formattedValue + "-";
        }
        return formattedValue;
    }

    private string FormatProcessingAmount(decimal amount)
    {
        // Z,ZZZ,ZZ9-形式（仕様書通り）
        if (amount == 0) return "";
        var formattedValue = Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture);
        if (amount < 0)
        {
            return formattedValue + "-";
        }
        return formattedValue;
    }

    private string FormatMonthlyAmount(decimal amount)
    {
        // ZZZ,ZZZ,ZZ9-形式（仕様書通り）
        if (amount == 0) return "";
        var formattedValue = Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture);
        if (amount < 0)
        {
            return formattedValue + "-";
        }
        return formattedValue;
    }

    private string FormatPercentage(decimal percentage)
    {
        // ZZ9.99-%形式（仕様書通り）
        if (percentage == 0) return "";
        var formattedValue = Math.Abs(percentage).ToString("N2", CultureInfo.InvariantCulture);
        if (percentage < 0)
        {
            return formattedValue + "-%";
        }
        return formattedValue + "%";
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        var truncated = text.Substring(0, Math.Min(maxLength, text.Length));
        return truncated + "...";
    }
}