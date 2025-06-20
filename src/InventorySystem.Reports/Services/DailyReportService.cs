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
        Console.WriteLine("Column widths total: 389mm");
        
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A3.Landscape());
                page.Margin(0.5f, Unit.Centimetre);
                
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
                column.Item().AlignCenter().Text($"※　　　{reportDate:yyyy年MM月dd日}　　　商　　　品　　　日　　　報　　　※")
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

        // データがある場合
        container.Column(column =>
        {
            column.Spacing(10);
            
            // ヘッダー行
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    // A3横向きレイアウト用固定幅（合計: 389mm）
                    columns.ConstantColumn(25, Unit.Millimetre);   // 商品コード（短縮）
                    columns.ConstantColumn(60, Unit.Millimetre);   // 商品名（短縮）
                    columns.ConstantColumn(20, Unit.Millimetre);   // 売上数量
                    columns.ConstantColumn(28, Unit.Millimetre);   // 売上金額
                    columns.ConstantColumn(20, Unit.Millimetre);   // 仕入値引
                    columns.ConstantColumn(20, Unit.Millimetre);   // 在庫調整
                    columns.ConstantColumn(18, Unit.Millimetre);   // 加工費
                    columns.ConstantColumn(18, Unit.Millimetre);   // 振替
                    columns.ConstantColumn(18, Unit.Millimetre);   // 奨励金
                    columns.ConstantColumn(25, Unit.Millimetre);   // １粗利益
                    columns.ConstantColumn(18, Unit.Millimetre);   // １粗利率
                    columns.ConstantColumn(25, Unit.Millimetre);   // ２粗利益
                    columns.ConstantColumn(18, Unit.Millimetre);   // ２粗利率
                    // 月計項目
                    columns.ConstantColumn(28, Unit.Millimetre);   // 月売上金額
                    columns.ConstantColumn(25, Unit.Millimetre);   // 月１粗利益
                    columns.ConstantColumn(18, Unit.Millimetre);   // 月１粗利率
                    columns.ConstantColumn(25, Unit.Millimetre);   // 月２粗利益
                    columns.ConstantColumn(18, Unit.Millimetre);   // 月２粗利率
                });

                // シンプルなヘッダー（枠線なし、ColumnSpanなし）
                table.Header(header =>
                {
                    // 項目名のみ（日計・月計の区別はコメントで示す）
                    header.Cell().Padding(1).AlignCenter().Text("商品ｺｰﾄﾞ").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("商　品　名").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("売上数量").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("売上金額").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("仕入値引").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("在庫調整").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("加工費").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("振替").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("奨励金").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("１粗利益").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("１粗利率").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("２粗利益").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("２粗利率").FontSize(9).Bold();
                    // 月計項目
                    header.Cell().Padding(1).AlignCenter().Text("月売上金額").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("月１粗利益").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("月１粗利率").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("月２粗利益").FontSize(9).Bold();
                    header.Cell().Padding(1).AlignCenter().Text("月２粗利率").FontSize(9).Bold();
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
        // 商品コード列（左寄せ）
        table.Cell().Padding(1).AlignLeft().Text(item.ProductCode).FontSize(9);
        // 商品名列（左寄せ）
        table.Cell().Padding(1).AlignLeft().Text(TruncateText(item.ProductName, 15)).FontSize(9);
        // 数値列（右寄せ）
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(item.DailySalesQuantity)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailySalesAmount)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyPurchaseDiscount)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyInventoryAdjustment)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyProcessingCost)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyTransfer)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyIncentive)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyGrossProfit1)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.DailyGrossProfitRate1)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyGrossProfit2)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.DailyGrossProfitRate2)).FontSize(9);
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(item.MonthlySalesAmount)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.MonthlyGrossProfit1)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.MonthlyGrossProfitRate1)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.MonthlyGrossProfit2)).FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.MonthlyGrossProfitRate2)).FontSize(9);
    }

    private void AddSubtotalRow(TableDescriptor table, DailyReportSubtotal subtotal)
    {
        table.Cell().Padding(1).Text(subtotal.SubtotalName).FontSize(9).Bold();
        table.Cell().Padding(1).Text("").FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(subtotal.TotalDailySalesQuantity)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailySalesAmount)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyPurchaseDiscount)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyInventoryAdjustment)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyProcessingCost)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyTransfer)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyIncentive)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyGrossProfit1)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalDailyGrossProfitRate1)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyGrossProfit2)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalDailyGrossProfitRate2)).FontSize(9).Bold();
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(subtotal.TotalMonthlySalesAmount)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalMonthlyGrossProfit1)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalMonthlyGrossProfitRate1)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalMonthlyGrossProfit2)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalMonthlyGrossProfitRate2)).FontSize(9).Bold();
    }

    private void AddTotalRow(TableDescriptor table, DailyReportTotal total)
    {
        table.Cell().Padding(1).Text(total.TotalName).FontSize(9).Bold();
        table.Cell().Padding(1).Text("").FontSize(9);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(total.GrandTotalDailySalesQuantity)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailySalesAmount)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyPurchaseDiscount)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyInventoryAdjustment)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyProcessingCost)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyTransfer)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyIncentive)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyGrossProfit1)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalDailyGrossProfitRate1)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyGrossProfit2)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalDailyGrossProfitRate2)).FontSize(9).Bold();
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(total.GrandTotalMonthlySalesAmount)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalMonthlyGrossProfit1)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalMonthlyGrossProfitRate1)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalMonthlyGrossProfit2)).FontSize(9).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalMonthlyGrossProfitRate2)).FontSize(9).Bold();
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