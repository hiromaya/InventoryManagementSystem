#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using FastReport;
using FastReport.Export.Pdf;
using FastReport.Data;
using InventorySystem.Core.Entities;
using FR = FastReport;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Reports.FastReport.Services
{
    public class DailyReportFastReportService : IDailyReportService
    {
        private readonly ILogger<DailyReportFastReportService> _logger;
        private readonly string _templatePath;
        
        public DailyReportFastReportService(ILogger<DailyReportFastReportService> logger)
        {
            _logger = logger;
            
            // テンプレートファイルのパス設定
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "DailyReport.frx");
            
            _logger.LogInformation("テンプレートパス: {Path}", _templatePath);
            
            // テンプレートが存在しない場合のエラーハンドリング
            if (!File.Exists(_templatePath))
            {
                _logger.LogError("商品日報テンプレートファイルが見つかりません: {Path}", _templatePath);
                throw new FileNotFoundException($"商品日報テンプレートファイルが見つかりません: {_templatePath}");
            }
        }
        
        public byte[] GenerateDailyReport(
            List<DailyReportItem> items,
            List<DailyReportSubtotal> subtotals,
            DailyReportTotal total,
            DateTime reportDate)
        {
            try
            {
                _logger.LogInformation("商品日報PDF生成開始: 日付={ReportDate}, 明細数={Count}", 
                    reportDate, items.Count);
                
                using var report = new Report();
                
                // テンプレートを読み込む
                _logger.LogDebug("テンプレートファイルを読み込んでいます: {TemplatePath}", _templatePath);
                report.Load(_templatePath);
                
                // スクリプトを完全に無効化
                SetScriptLanguageToNone(report);
                
                // パラメータ設定
                report.SetParameterValue("ReportDate", reportDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("CreateDateTime", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒"));
                report.SetParameterValue("PageNumber", "1 / 1");
                
                // データを直接レポートページに設定（新しい17列レイアウト対応）
                _logger.LogDebug("データバインディング開始。アイテム数: {Count}", items.Count);
                PopulateReportData(report, items, subtotals, total);
                
                // レポートの準備
                _logger.LogDebug("レポートを準備しています...");
                report.Prepare();
                
                // PDF出力
                return ExportToPdf(report, reportDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "商品日報の生成中にエラーが発生しました");
                throw;
            }
        }
        
        private void SetScriptLanguageToNone(Report report)
        {
            try
            {
                // リフレクションを使用してScriptLanguageプロパティを取得
                var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                if (scriptLanguageProperty != null)
                {
                    var scriptLanguageType = scriptLanguageProperty.PropertyType;
                    if (scriptLanguageType.IsEnum)
                    {
                        // FastReport.ScriptLanguage.None を設定
                        var noneValue = Enum.GetValues(scriptLanguageType)
                            .Cast<object>()
                            .FirstOrDefault(v => v.ToString() == "None");
                        
                        if (noneValue != null)
                        {
                            scriptLanguageProperty.SetValue(report, noneValue);
                            _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }
                
                // Scriptプロパティをnullに設定（追加の安全策）
                var scriptProperty = report.GetType().GetProperty("Script", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
                // エラーが発生しても処理を継続
            }
        }
        
        private void PopulateReportData(Report report, List<DailyReportItem> items, 
            List<DailyReportSubtotal> subtotals, DailyReportTotal total)
        {
            var page = report.FindObject("Page1") as FR.ReportPage;
            if (page == null)
            {
                _logger.LogError("Page1が見つかりません");
                return;
            }
            
            var dataBand = report.FindObject("Data1") as FR.DataBand;
            if (dataBand == null)
            {
                _logger.LogError("Data1が見つかりません");
                return;
            }
            
            float currentY = 0f; // DataBand内の相対位置
            string currentCategory = "";
            
            // 小計用変数（新レイアウト対応）
            decimal categoryPrevStock = 0;
            decimal categoryPurchase = 0;
            decimal categoryPurchaseDiscount = 0;
            decimal categoryStockAdjust = 0;
            decimal categoryProcessing = 0;
            decimal categoryTransfer = 0;
            decimal categoryIncentive = 0;
            decimal categoryGrossProfit1 = 0;
            decimal categoryGrossProfit2 = 0;
            decimal categoryDailySales = 0;
            decimal categoryMonthlySales = 0;
            decimal categoryMonthlyGrossProfit1 = 0;
            decimal categoryMonthlyGrossProfit2 = 0;
            
            // 有効なデータのみを処理
            var validItems = items.Where(IsNotZeroItem).ToList();
            _logger.LogDebug("有効なデータ数: {Count}", validItems.Count);
            
            foreach (var item in validItems)
            {
                // 商品分類が変わったら小計行を出力
                if (!string.IsNullOrEmpty(currentCategory) && currentCategory != item.ProductCategory1)
                {
                    AddSubtotalRow(dataBand, currentY, currentCategory, 
                        categoryPrevStock, categoryPurchase, categoryPurchaseDiscount, categoryStockAdjust,
                        categoryProcessing, categoryTransfer, categoryIncentive, categoryGrossProfit1, 
                        categoryGrossProfit2, categoryDailySales, categoryMonthlySales, 
                        categoryMonthlyGrossProfit1, categoryMonthlyGrossProfit2);
                    currentY += 18.9f;
                    
                    // 小計値をリセット
                    categoryPrevStock = 0;
                    categoryPurchase = 0;
                    categoryPurchaseDiscount = 0;
                    categoryStockAdjust = 0;
                    categoryProcessing = 0;
                    categoryTransfer = 0;
                    categoryIncentive = 0;
                    categoryGrossProfit1 = 0;
                    categoryGrossProfit2 = 0;
                    categoryDailySales = 0;
                    categoryMonthlySales = 0;
                    categoryMonthlyGrossProfit1 = 0;
                    categoryMonthlyGrossProfit2 = 0;
                }
                
                // 明細行を追加（新17列レイアウト）
                AddDetailRow(dataBand, currentY, item);
                currentY += 18.9f;
                
                // 小計に加算（新レイアウト項目）
                currentCategory = item.ProductCategory1 ?? "";
                // 注意：現在のDailyReportItemには存在しない項目があるため、対応するプロパティを使用
                categoryPrevStock += 0; // 前残データは現在未実装
                categoryPurchase += 0; // 当仕入データは現在未実装
                categoryPurchaseDiscount += item.DailyPurchaseDiscount;
                categoryStockAdjust += item.DailyInventoryAdjustment;
                categoryProcessing += item.DailyProcessingCost;
                categoryTransfer += item.DailyTransfer;
                categoryIncentive += item.DailyIncentive;
                categoryGrossProfit1 += item.DailyGrossProfit1;
                categoryGrossProfit2 += item.DailyGrossProfit2;
                categoryDailySales += item.DailySalesQuantity;
                categoryMonthlySales += item.DailySalesAmount + item.MonthlySalesAmount;
                categoryMonthlyGrossProfit1 += item.DailyGrossProfit1 + item.MonthlyGrossProfit1;
                categoryMonthlyGrossProfit2 += item.DailyGrossProfit2 + item.MonthlyGrossProfit1 - item.DailyDiscountAmount;
            }
            
            // 最後の小計
            if (!string.IsNullOrEmpty(currentCategory))
            {
                AddSubtotalRow(dataBand, currentY, currentCategory,
                    categoryPrevStock, categoryPurchase, categoryPurchaseDiscount, categoryStockAdjust,
                    categoryProcessing, categoryTransfer, categoryIncentive, categoryGrossProfit1, 
                    categoryGrossProfit2, categoryDailySales, categoryMonthlySales, 
                    categoryMonthlyGrossProfit1, categoryMonthlyGrossProfit2);
                currentY += 18.9f;
            }
            
            // DataBandの高さを調整
            dataBand.Height = currentY;
            
            // 合計値を設定（新17列レイアウト対応）
            SetTotalValues(report, total);
        }
        
        private void AddDetailRow(FR.DataBand dataBand, float y, DailyReportItem item)
        {
            // 商品名（1列目）
            var nameText = new FR.TextObject
            {
                Name = $"ProductName_{y}",
                Left = 0,
                Top = y,
                Width = 189f,
                Height = 18.9f,
                Text = item.ProductName ?? "",
                Font = new Font("MS Gothic", 8)
            };
            dataBand.Objects.Add(nameText);
            
            // 日計セクション（12列）
            AddDailyColumnData(dataBand, y, item);
            
            // 月計セクション（5列）
            AddMonthlyColumnData(dataBand, y, item);
        }
        
        private void AddDailyColumnData(FR.DataBand dataBand, float y, DailyReportItem item)
        {
            // 2. 前残（現在未実装のため0）
            AddTextObject(dataBand, y, "PrevStock", 189f, FormatNumberZeroSuppress(0, 2));
            
            // 3. 当仕入（現在未実装のため0）
            AddTextObject(dataBand, y, "Purchase", 252f, FormatNumberZeroSuppress(0));
            
            // 4. 仕入値引
            AddTextObject(dataBand, y, "PurchaseDiscount", 315f, FormatNumberWithTriangle(item.DailyPurchaseDiscount));
            
            // 5. 在庫調整
            AddTextObject(dataBand, y, "StockAdjust", 378f, FormatNumberWithTriangle(item.DailyInventoryAdjustment));
            
            // 6. 加工費
            AddTextObject(dataBand, y, "Processing", 441f, FormatNumberWithTriangle(item.DailyProcessingCost));
            
            // 7. 振替
            AddTextObject(dataBand, y, "Transfer", 504f, FormatNumberWithTriangle(item.DailyTransfer));
            
            // 8. 奨励金
            AddTextObject(dataBand, y, "Incentive", 567f, FormatNumberWithTriangle(item.DailyIncentive));
            
            // 9. 1粗利益
            AddTextObject(dataBand, y, "GrossProfit1", 630f, FormatNumberZeroSuppress(item.DailyGrossProfit1));
            
            // 10. 1粗利率
            AddTextObject(dataBand, y, "GrossProfitRate1", 693f, FormatRate(CalculateRate(item.DailyGrossProfit1, item.DailySalesAmount)));
            
            // 11. 2粗利益
            AddTextObject(dataBand, y, "GrossProfit2", 756f, FormatNumberWithTriangle(item.DailyGrossProfit2));
            
            // 12. 2粗利率
            AddTextObject(dataBand, y, "GrossProfitRate2", 819f, FormatRate(CalculateRate(item.DailyGrossProfit2, item.DailySalesAmount)));
            
            // 13. 売上数量
            AddTextObject(dataBand, y, "DailySales", 882f, FormatNumberZeroSuppress(item.DailySalesQuantity, 2));
        }
        
        private void AddMonthlyColumnData(FR.DataBand dataBand, float y, DailyReportItem item)
        {
            // 月計項目の計算
            var monthlyAmount = item.DailySalesAmount + item.MonthlySalesAmount;
            var monthlyGross1 = item.DailyGrossProfit1 + item.MonthlyGrossProfit1;
            var monthlyGross2 = item.DailyGrossProfit2 + item.MonthlyGrossProfit1 - item.DailyDiscountAmount;
            
            // 14. 月計売上金額
            AddTextObject(dataBand, y, "MonthlySales", 945f, FormatNumberZeroSuppress(monthlyAmount));
            
            // 15. 月計1粗利益
            AddTextObject(dataBand, y, "MonthlyGrossProfit1", 1008f, FormatNumberZeroSuppress(monthlyGross1));
            
            // 16. 月計1粗利率
            AddTextObject(dataBand, y, "MonthlyGrossProfitRate1", 1071f, FormatRate(CalculateRate(monthlyGross1, monthlyAmount)));
            
            // 17. 月計2粗利益（▲記号使用）
            AddTextObject(dataBand, y, "MonthlyGrossProfit2", 1134f, FormatNumberWithTriangleSpecial(monthlyGross2));
            
            // 18. 月計2粗利率
            AddTextObject(dataBand, y, "MonthlyGrossProfitRate2", 1197f, FormatRate(CalculateRate(monthlyGross2, monthlyAmount)));
        }
        
        private void AddTextObject(FR.DataBand dataBand, float y, string namePrefix, float left, string text)
        {
            var textObject = new FR.TextObject
            {
                Name = $"{namePrefix}_{y}",
                Left = left,
                Top = y,
                Width = 63f,
                Height = 18.9f,
                Text = text,
                Font = new Font("MS Gothic", 8),
                HorzAlign = FR.HorzAlign.Right
            };
            dataBand.Objects.Add(textObject);
        }
        
        private void AddSubtotalRow(FR.DataBand dataBand, float y, string category,
            decimal prevStock, decimal purchase, decimal purchaseDiscount, decimal stockAdjust,
            decimal processing, decimal transfer, decimal incentive, decimal grossProfit1, 
            decimal grossProfit2, decimal dailySales, decimal monthlySales, 
            decimal monthlyGrossProfit1, decimal monthlyGrossProfit2)
        {
            var labelText = new FR.TextObject
            {
                Name = $"Subtotal_{y}",
                Left = 0,
                Top = y,
                Width = 189f,
                Height = 18.9f,
                Text = $"＊　{category}　計　＊",
                Font = new Font("MS Gothic", 8, FontStyle.Bold),
                HorzAlign = FR.HorzAlign.Center
            };
            dataBand.Objects.Add(labelText);
            
            // 各小計値のTextObjectを追加（必要に応じて実装）
            // 現在は「大分類計」ラベルのみ実装
        }
        
        private bool IsNotZeroItem(DailyReportItem item)
        {
            // すべての数値項目が0でない場合のみ表示
            return item.DailySalesQuantity != 0 ||
                   item.DailySalesAmount != 0 ||
                   item.DailyGrossProfit1 != 0 ||
                   item.DailyGrossProfit2 != 0 ||
                   item.MonthlySalesAmount != 0 ||
                   item.MonthlyGrossProfit1 != 0;
        }
        
        private decimal CalculateRate(decimal profit, decimal amount)
        {
            if (amount == 0) return 0;
            return Math.Round((profit * 100) / amount, 2);
        }
        
        private string FormatNumberZeroSuppress(decimal value, int decimals = 0)
        {
            if (value == 0) return "";
            
            var format = decimals > 0 ? $"#,##0.{new string('0', decimals)}" : "#,##0";
            return value.ToString(format);
        }

        private string FormatNumberWithTriangle(decimal value)
        {
            if (value < 0)
                return $"▲{Math.Abs(value):N0}";
            return value == 0 ? "" : value.ToString("N0");
        }

        private string FormatNumberWithTriangleSpecial(decimal value)
        {
            // 月計2粗利益専用フォーマット（仕様書の▲記号使用）
            if (value < 0)
                return $"▲{Math.Abs(value):N0}";
            return value == 0 ? "" : value.ToString("N0");
        }
        
        private string FormatRate(decimal rate)
        {
            return rate == 0 ? "" : rate.ToString("N2") + "%";
        }
        
        private void SetTotalValues(Report report, DailyReportTotal total)
        {
            // 新17列レイアウトに対応した合計値設定
            // 現在のDailyReportTotalエンティティの項目を使用
            SetTextObjectValue(report, "TotalPrevStock", ""); // 未実装項目
            SetTextObjectValue(report, "TotalPurchase", ""); // 未実装項目
            SetTextObjectValue(report, "TotalPurchaseDiscount", FormatNumberWithTriangle(total.GrandTotalDailyPurchaseDiscount));
            SetTextObjectValue(report, "TotalStockAdjust", FormatNumberWithTriangle(total.GrandTotalDailyInventoryAdjustment));
            SetTextObjectValue(report, "TotalProcessing", FormatNumberWithTriangle(total.GrandTotalDailyProcessingCost));
            SetTextObjectValue(report, "TotalTransfer", FormatNumberWithTriangle(total.GrandTotalDailyTransfer));
            SetTextObjectValue(report, "TotalIncentive", FormatNumberWithTriangle(total.GrandTotalDailyIncentive));
            SetTextObjectValue(report, "TotalGrossProfit1", FormatNumberZeroSuppress(total.GrandTotalDailyGrossProfit1));
            SetTextObjectValue(report, "TotalGrossProfitRate1", FormatRate(CalculateRate(total.GrandTotalDailyGrossProfit1, total.GrandTotalDailySalesAmount)));
            SetTextObjectValue(report, "TotalGrossProfit2", FormatNumberWithTriangle(total.GrandTotalDailyGrossProfit2));
            SetTextObjectValue(report, "TotalGrossProfitRate2", FormatRate(CalculateRate(total.GrandTotalDailyGrossProfit2, total.GrandTotalDailySalesAmount)));
            SetTextObjectValue(report, "TotalDailySales", FormatNumberZeroSuppress(total.GrandTotalDailySalesQuantity, 2));
            SetTextObjectValue(report, "TotalMonthlySales", FormatNumberZeroSuppress(total.GrandTotalMonthlySalesAmount));
            SetTextObjectValue(report, "TotalMonthlyGrossProfit1", FormatNumberZeroSuppress(total.GrandTotalMonthlyGrossProfit1));
            SetTextObjectValue(report, "TotalMonthlyGrossProfitRate1", FormatRate(CalculateRate(total.GrandTotalMonthlyGrossProfit1, total.GrandTotalMonthlySalesAmount)));
            SetTextObjectValue(report, "TotalMonthlyGrossProfit2", FormatNumberWithTriangleSpecial(total.GrandTotalMonthlyGrossProfit2));
            SetTextObjectValue(report, "TotalMonthlyGrossProfitRate2", FormatRate(CalculateRate(total.GrandTotalMonthlyGrossProfit2, total.GrandTotalMonthlySalesAmount)));
        }
        
        private void SetTextObjectValue(Report report, string objectName, string value)
        {
            var textObject = report.FindObject(objectName) as FR.TextObject;
            if (textObject != null)
            {
                textObject.Text = value;
            }
        }
        
        private byte[] ExportToPdf(Report report, DateTime reportDate)
        {
            using var pdfExport = new PDFExport
            {
                EmbeddingFonts = true,
                Title = $"商品日報_{reportDate:yyyyMMdd}",
                Subject = "商品日報",
                Creator = "在庫管理システム",
                OpenAfterExport = false,
                // 日本語フォント対応
                JpegQuality = 95
            };
            
            using var stream = new MemoryStream();
            report.Export(pdfExport, stream);
            
            _logger.LogInformation("商品日報PDF生成完了: サイズ={Size}bytes", stream.Length);
            return stream.ToArray();
        }
    }
}
#else
namespace InventorySystem.Reports.FastReport.Services
{
    // Linux環境用のプレースホルダークラス
    public class DailyReportFastReportService
    {
        public DailyReportFastReportService(object logger) { }
    }
}
#endif