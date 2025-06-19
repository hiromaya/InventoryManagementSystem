using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InventorySystem.Core.Entities;

namespace InventorySystem.Reports.Services;

/// <summary>
/// 在庫表PDF生成サービス
/// </summary>
public class InventoryListReportService
{
    private static bool _isFontRegistered = false;
    private static string _registeredFontFamily = "";
    
    static InventoryListReportService()
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
    /// 在庫表PDFを生成
    /// </summary>
    public byte[] GenerateInventoryList(List<InventoryListByStaff> staffInventories, 
        InventoryListTotal grandTotal, DateTime reportDate)
    {
        return Document.Create(container =>
        {
            // 担当者別にページ分割
            foreach (var staffInventory in staffInventories)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
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

                    page.Header().Element(header => ComposeHeader(header, reportDate, staffInventory));
                    page.Content().Element(content => ComposeContent(content, staffInventory));
                    page.Footer().Element(ComposeFooter);
                });
            }
            
            // 最後に全体合計ページ
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.5f, Unit.Centimetre);
                
                // フォント設定
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

                page.Header().Element(header => ComposeGrandTotalHeader(header, reportDate));
                page.Content().Element(content => ComposeGrandTotalContent(content, grandTotal));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container, DateTime reportDate, InventoryListByStaff staffInventory)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            
            // 第1行：作成日とページ
            column.Item().Row(row =>
            {
                row.ConstantItem(200).Text($"作成日：{DateTime.Now:yyyy年MM月dd日HH時mm分ss秒}")
                    .FontSize(10);
                row.RelativeItem().Text("");
                row.ConstantItem(80).AlignRight().Text("頁").FontSize(10);
            });
            
            // 第2行：タイトル
            column.Item().AlignCenter().Text($"※　{reportDate:yyyy年MM月dd日}　在　庫　表　※")
                .FontSize(12).Bold();
            
            // 第3行：担当者情報
            column.Item().Text($"担当者コード：{staffInventory.StaffCode}　担当者名：{staffInventory.StaffName}")
                .FontSize(10).Bold();
        });
    }

    private void ComposeGrandTotalHeader(IContainer container, DateTime reportDate)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            
            // 第1行：作成日とページ
            column.Item().Row(row =>
            {
                row.ConstantItem(200).Text($"作成日：{DateTime.Now:yyyy年MM月dd日HH時mm分ss秒}")
                    .FontSize(10);
                row.RelativeItem().Text("");
                row.ConstantItem(80).AlignRight().Text("頁").FontSize(10);
            });
            
            // 第2行：タイトル
            column.Item().AlignCenter().Text($"※　{reportDate:yyyy年MM月dd日}　在　庫　表　（全体合計）　※")
                .FontSize(12).Bold();
        });
    }

    private void ComposeContent(IContainer container, InventoryListByStaff staffInventory)
    {
        if (!staffInventory.Items.Any())
        {
            // データがない場合
            container.Column(column =>
            {
                column.Spacing(20);
                column.Item().AlignCenter().PaddingTop(100).Text("在庫表データなし")
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
                    columns.ConstantColumn(40);   // 商品コード
                    columns.RelativeColumn(20);   // 商品名
                    columns.ConstantColumn(30);   // 荷印コード
                    columns.RelativeColumn(15);   // 荷印名
                    columns.ConstantColumn(25);   // 等級コード
                    columns.ConstantColumn(25);   // 階級コード
                    columns.ConstantColumn(50);   // 現在庫数量
                    columns.ConstantColumn(40);   // 現在庫単価
                    columns.ConstantColumn(60);   // 現在庫金額
                    columns.ConstantColumn(50);   // 前在庫数量
                    columns.ConstantColumn(60);   // 前在庫金額
                    columns.ConstantColumn(15);   // 滞留マーク
                });

                // ヘッダー
                table.Header(header =>
                {
                    header.Cell().Padding(2).AlignRight().Text("商品ｺｰﾄﾞ").FontSize(8).Bold();
                    header.Cell().Padding(2).Text("商　品　名").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("荷印ｺｰﾄﾞ").FontSize(8).Bold();
                    header.Cell().Padding(2).Text("荷　印　名").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("等級ｺｰﾄﾞ").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("階級ｺｰﾄﾞ").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("現在庫数量").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("現在庫単価").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("現在庫金額").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("前在庫数量").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignRight().Text("前在庫金額").FontSize(8).Bold();
                    header.Cell().Padding(2).AlignCenter().Text("滞留").FontSize(8).Bold();
                });

                // データ行
                string currentProductCode = "";
                foreach (var item in staffInventory.Items)
                {
                    // 商品コードが変わったら小計を印字
                    if (currentProductCode != item.ProductCode && !string.IsNullOrEmpty(currentProductCode))
                    {
                        var subtotal = staffInventory.Subtotals.FirstOrDefault(s => s.ProductCode == currentProductCode);
                        if (subtotal != null)
                        {
                            AddSubtotalRow(table, subtotal);
                        }
                    }
                    currentProductCode = item.ProductCode;

                    AddDataRow(table, item);
                }

                // 最後の小計
                if (!string.IsNullOrEmpty(currentProductCode))
                {
                    var lastSubtotal = staffInventory.Subtotals.FirstOrDefault(s => s.ProductCode == currentProductCode);
                    if (lastSubtotal != null)
                    {
                        AddSubtotalRow(table, lastSubtotal);
                    }
                }

                // 担当者合計行
                AddStaffTotalRow(table, staffInventory.Total);
            });
        });
    }

    private void ComposeGrandTotalContent(IContainer container, InventoryListTotal grandTotal)
    {
        container.Column(column =>
        {
            column.Spacing(20);
            
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(50);   // ラベル
                    columns.ConstantColumn(80);   // 数量
                    columns.ConstantColumn(100);  // 金額
                });

                // ヘッダー
                table.Header(header =>
                {
                    header.Cell().Padding(2).Text("項　目").FontSize(10).Bold();
                    header.Cell().Padding(2).AlignRight().Text("合計数量").FontSize(10).Bold();
                    header.Cell().Padding(2).AlignRight().Text("合計金額").FontSize(10).Bold();
                });

                // 合計行
                table.Cell().Padding(2).Text("【　全　体　合　計　】").FontSize(10).Bold();
                table.Cell().Padding(2).AlignRight().Text(FormatQuantity(grandTotal.GrandTotalQuantity)).FontSize(10).Bold();
                table.Cell().Padding(2).AlignRight().Text(FormatAmount(grandTotal.GrandTotalAmount)).FontSize(10).Bold();
            });
        });
    }

    private void AddDataRow(TableDescriptor table, InventoryListItem item)
    {
        table.Cell().Padding(1).AlignRight().Text(FormatCode(item.ProductCode, 5)).FontSize(8);
        table.Cell().Padding(1).Text(TruncateText(item.ProductName, 15)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatCode(item.ShippingMarkCode, 5)).FontSize(8);
        table.Cell().Padding(1).Text(TruncateText(item.ShippingMarkName, 10)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatCode(item.GradeCode, 3)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatCode(item.ClassCode, 3)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(item.CurrentStockQuantity)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatUnitPrice(item.CurrentStockUnitPrice)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.CurrentStockAmount)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(item.PreviousStockQuantity)).FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(item.PreviousStockAmount)).FontSize(8);
        table.Cell().Padding(1).AlignCenter().Text(item.StagnationMark).FontSize(8).Bold();
    }

    private void AddSubtotalRow(TableDescriptor table, InventoryListSubtotal subtotal)
    {
        table.Cell().Padding(1).Text($"{subtotal.ProductCode}計").FontSize(8).Bold();
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(subtotal.SubtotalQuantity)).FontSize(8).Bold();
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(subtotal.SubtotalAmount)).FontSize(8).Bold();
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
    }

    private void AddStaffTotalRow(TableDescriptor table, InventoryListTotal total)
    {
        table.Cell().Padding(1).Text("担当者合計").FontSize(8).Bold();
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatQuantity(total.GrandTotalQuantity)).FontSize(8).Bold();
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).AlignRight().Text(FormatAmount(total.GrandTotalAmount)).FontSize(8).Bold();
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
        table.Cell().Padding(1).Text("").FontSize(8);
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

    private string FormatUnitPrice(decimal unitPrice)
    {
        // ZZ,ZZ9.99-形式（仕様書通り）
        if (unitPrice == 0) return "";
        var formattedValue = Math.Abs(unitPrice).ToString("N2", CultureInfo.InvariantCulture);
        if (unitPrice < 0)
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

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        var truncated = text.Substring(0, Math.Min(maxLength, text.Length));
        return truncated + "...";
    }
}