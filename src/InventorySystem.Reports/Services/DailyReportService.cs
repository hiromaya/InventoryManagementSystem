using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InventorySystem.Core.Entities;

namespace InventorySystem.Reports.Services;

/// <summary>
/// 商品日報PDF生成サービス
/// </summary>
public class DailyReportService
{
    private static bool _isFontRegistered = false;
    private static string _registeredFontFamily = "";
    
    static DailyReportService()
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
                        .FontSize(8)
                        .FontFamily(_registeredFontFamily));
                }
                else
                {
                    page.DefaultTextStyle(x => x
                        .FontSize(8));
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
                    .FontSize(10);
            });
            
            row.RelativeItem().Column(column =>
            {
                column.Item().AlignCenter().Text($"※　{reportDate:yyyy年MM月dd日}　商　品　日　報　※")
                    .FontSize(12).Bold();
            });
            
            row.ConstantItem(80).Column(column =>
            {
                column.Item().AlignRight().Text("頁").FontSize(10);
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
                    columns.ConstantColumn(50);   // 商品コード
                    columns.RelativeColumn(15);   // 商品名
                    columns.ConstantColumn(60);   // 売上数量
                    columns.ConstantColumn(80);   // 売上金額
                    columns.ConstantColumn(70);   // 仕入値引
                    columns.ConstantColumn(70);   // 在庫調整
                    columns.ConstantColumn(60);   // 加工費
                    columns.ConstantColumn(60);   // 振替
                    columns.ConstantColumn(60);   // 奨励金
                    columns.ConstantColumn(70);   // １粗利益
                    columns.ConstantColumn(60);   // １粗利率
                    columns.ConstantColumn(70);   // ２粗利益
                    columns.ConstantColumn(60);   // ２粗利率
                    // 月計項目
                    columns.ConstantColumn(80);   // 月売上金額
                    columns.ConstantColumn(70);   // 月１粗利益
                    columns.ConstantColumn(60);   // 月１粗利率
                    columns.ConstantColumn(70);   // 月２粗利益
                    columns.ConstantColumn(60);   // 月２粗利率
                });

                // ヘッダー
                table.Header(header =>
                {
                    // 日計ヘッダー
                    header.Cell().Padding(2).AlignRight().Text("商品ｺｰﾄﾞ").FontSize(8).Bold();
                    header.Cell().Padding(2).Text("商　品　名").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("売上数量").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("売上金額").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("仕入値引").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("在庫調整").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("加工費").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("振替").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("奨励金").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("１粗利益").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("１粗利率").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("２粗利益").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("２粗利率").FontSize(8).Bold();
                    
                    // 月計ヘッダー
                    header.Cell().Padding(2).AlignRight().Text("月売上金額").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("月１粗利益").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("月１粗利率").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("月２粗利益").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("月２粗利率").FontSize(8).Bold();
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
        table.Cell().Padding(1).AlignRight().Text(FormatCode(item.ProductCode, 5)).FontSize(8);
        table.Cell().Padding(1).Text(TruncateText(item.ProductName, 15)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(item.DailySalesQuantity)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailySalesAmount)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyPurchaseDiscount)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyInventoryAdjustment)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyProcessingCost)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyTransfer)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(item.DailyIncentive)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyGrossProfit1)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.DailyGrossProfitRate1)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.DailyGrossProfit2)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.DailyGrossProfitRate2)).FontSize(8);
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(item.MonthlySalesAmount)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.MonthlyGrossProfit1)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.MonthlyGrossProfitRate1)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.MonthlyGrossProfit2)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(item.MonthlyGrossProfitRate2)).FontSize(8);
    }

    private void AddSubtotalRow(TableDescriptor table, DailyReportSubtotal subtotal)
    {
        table.Cell().Padding(1).Text(subtotal.SubtotalName).FontSize(8).Bold();
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(subtotal.TotalDailySalesQuantity)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailySalesAmount)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyPurchaseDiscount)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyInventoryAdjustment)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyProcessingCost)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyTransfer)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(subtotal.TotalDailyIncentive)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyGrossProfit1)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalDailyGrossProfitRate1)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalDailyGrossProfit2)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalDailyGrossProfitRate2)).FontSize(8).Bold();
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(subtotal.TotalMonthlySalesAmount)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalMonthlyGrossProfit1)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalMonthlyGrossProfitRate1)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.TotalMonthlyGrossProfit2)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(subtotal.TotalMonthlyGrossProfitRate2)).FontSize(8).Bold();
    }

    private void AddTotalRow(TableDescriptor table, DailyReportTotal total)
    {
        table.Cell().Padding(1).Text(total.TotalName).FontSize(8).Bold();
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(total.GrandTotalDailySalesQuantity)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailySalesAmount)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyPurchaseDiscount)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyInventoryAdjustment)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyProcessingCost)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyTransfer)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatProcessingAmount(total.GrandTotalDailyIncentive)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyGrossProfit1)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalDailyGrossProfitRate1)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalDailyGrossProfit2)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalDailyGrossProfitRate2)).FontSize(8).Bold();
        
        // 月計
        table.Cell().Padding(1).AlignRight().Text(FormatMonthlyAmount(total.GrandTotalMonthlySalesAmount)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalMonthlyGrossProfit1)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalMonthlyGrossProfitRate1)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalMonthlyGrossProfit2)).FontSize(8).Bold();
        table.Cell().Padding(1).AlignRight().Text(FormatPercentage(total.GrandTotalMonthlyGrossProfitRate2)).FontSize(8).Bold();
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