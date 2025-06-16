using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InventorySystem.Core.Entities;
using SkiaSharp;

namespace InventorySystem.Reports.Services;

public class UnmatchListReportService
{
    static UnmatchListReportService()
    {
        // QuestPDFのフォント設定
        QuestPDF.Settings.DocumentLayoutExceptionThreshold = 250;
    }
    public byte[] GenerateUnmatchListReport(IEnumerable<UnmatchItem> unmatchItems, DateTime jobDate)
    {
        var items = unmatchItems.ToList();
        
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A3.Landscape());
                page.Margin(1, Unit.Centimetre);
                // フォント設定
                page.DefaultTextStyle(x => x
                    .FontSize(9)
                    .FontFamily("DejaVu Sans"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(content => ComposeContent(content, items, jobDate));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
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
                column.Item().AlignCenter().Text($"※　{DateTime.Now:yyyy年MM月dd日}　アンマッチリスト　※")
                    .FontSize(12).Bold();
            });
            
            row.ConstantItem(50).Column(column =>
            {
                column.Item().AlignRight().Text("頁").FontSize(10);
            });
        });
    }

    private void ComposeContent(IContainer container, List<UnmatchItem> items, DateTime jobDate)
    {
        if (!items.Any())
        {
            // アンマッチデータがない場合
            container.Column(column =>
            {
                column.Spacing(20);
                column.Item().AlignCenter().PaddingTop(100).Text("アンマッチデータなし")
                    .FontSize(16).Bold();
                column.Item().AlignCenter().PaddingTop(50).Text($"アンマッチ件数＝{items.Count:0000}")
                    .FontSize(12);
            });
            return;
        }

        // アンマッチデータがある場合
        container.Column(column =>
        {
            column.Spacing(10);
            
            // ヘッダー行
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(8);  // 区分
                    columns.ConstantColumn(40); // コード（取引先）
                    columns.RelativeColumn(12); // 取引先名
                    columns.ConstantColumn(40); // コード（商品）
                    columns.RelativeColumn(15); // 商品名
                    columns.ConstantColumn(30); // コード（荷印）
                    columns.RelativeColumn(10); // 荷印
                    columns.RelativeColumn(10); // 手入力
                    columns.ConstantColumn(25); // コード（等級）
                    columns.RelativeColumn(8);  // 等級
                    columns.ConstantColumn(25); // コード（階級）
                    columns.RelativeColumn(8);  // 階級
                    columns.ConstantColumn(60); // 数量
                    columns.ConstantColumn(50); // 単価
                    columns.ConstantColumn(70); // 金額
                    columns.ConstantColumn(50); // 伝票番号
                    columns.RelativeColumn(8);  // アラート
                });

                // ヘッダー
                table.Header(header =>
                {
                    header.Cell().Border(1).Padding(2).Text("区分").Bold();
                    header.Cell().Border(1).Padding(2).Text("ｺｰﾄﾞ").Bold();
                    header.Cell().Border(1).Padding(2).Text("取引先名").Bold();
                    header.Cell().Border(1).Padding(2).Text("ｺｰﾄﾞ").Bold();
                    header.Cell().Border(1).Padding(2).Text("商　品　名").Bold();
                    header.Cell().Border(1).Padding(2).Text("ｺｰﾄﾞ").Bold();
                    header.Cell().Border(1).Padding(2).Text("荷　印").Bold();
                    header.Cell().Border(1).Padding(2).Text("手入力").Bold();
                    header.Cell().Border(1).Padding(2).Text("ｺｰﾄﾞ").Bold();
                    header.Cell().Border(1).Padding(2).Text("等　級").Bold();
                    header.Cell().Border(1).Padding(2).Text("ｺｰﾄﾞ").Bold();
                    header.Cell().Border(1).Padding(2).Text("階　級").Bold();
                    header.Cell().Border(1).Padding(2).Text("数　量").Bold();
                    header.Cell().Border(1).Padding(2).Text("単　価").Bold();
                    header.Cell().Border(1).Padding(2).Text("金　額").Bold();
                    header.Cell().Border(1).Padding(2).Text("伝票番号").Bold();
                    header.Cell().Border(1).Padding(2).Text("ｱﾗｰﾄ").Bold();
                });

                // データ行
                foreach (var item in items)
                {
                    table.Cell().Border(1).Padding(2).Text(item.Category);
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatCode(item.CustomerCode, 5));
                    table.Cell().Border(1).Padding(2).Text(TruncateText(item.CustomerName, 12));
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatCode(item.Key.ProductCode, 5));
                    table.Cell().Border(1).Padding(2).Text(TruncateText(item.ProductName, 15));
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatCode(item.Key.ShippingMarkCode, 4));
                    table.Cell().Border(1).Padding(2).Text(TruncateText(item.Key.ShippingMarkName, 10));
                    table.Cell().Border(1).Padding(2).Text(TruncateText(item.Key.ShippingMarkName, 10)); // 手入力も荷印名を使用
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatCode(item.Key.GradeCode, 3));
                    table.Cell().Border(1).Padding(2).Text(TruncateText(item.GradeName, 8));
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatCode(item.Key.ClassCode, 3));
                    table.Cell().Border(1).Padding(2).Text(TruncateText(item.ClassName, 8));
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatQuantity(item.Quantity));
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatUnitPrice(item.UnitPrice));
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatAmount(item.Amount));
                    table.Cell().Border(1).Padding(2).AlignRight().Text(FormatCode(item.VoucherNumber, 6));
                    table.Cell().Border(1).Padding(2).Text(item.AlertType);
                }
            });

            // フッター
            column.Item().PaddingTop(20).AlignCenter()
                .Text($"アンマッチ件数＝{items.Count:0000}")
                .FontSize(12).Bold();
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.CurrentPageNumber().FontSize(10);
        });
    }

    private string FormatCode(string code, int width)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        
        // 数値として扱える場合は右詰め、そうでなければそのまま
        if (int.TryParse(code, out var numCode))
        {
            return numCode.ToString($"D{width}");
        }
        return code.PadLeft(width);
    }

    private string FormatQuantity(decimal quantity)
    {
        // フォーマット: ZZ,ZZ9.99-
        if (quantity == 0) return string.Empty;
        
        var formattedValue = Math.Abs(quantity).ToString("N2", CultureInfo.InvariantCulture);
        if (quantity < 0)
        {
            return formattedValue + "-";
        }
        return formattedValue;
    }

    private string FormatUnitPrice(decimal unitPrice)
    {
        // フォーマット: ZZZ,ZZ9
        if (unitPrice == 0) return string.Empty;
        return unitPrice.ToString("N0", CultureInfo.InvariantCulture);
    }

    private string FormatAmount(decimal amount)
    {
        // フォーマット: ZZ,ZZZ,ZZ9-
        if (amount == 0) return string.Empty;
        
        var formattedValue = Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture);
        if (amount < 0)
        {
            return formattedValue + "-";
        }
        return formattedValue;
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        if (text.Length <= maxLength) return text;
        
        // 日本語文字を考慮した切り詰め（簡易実装）
        var truncated = text.Substring(0, Math.Min(maxLength, text.Length));
        return truncated + "...";
    }
}