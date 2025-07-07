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
                
                // データを直接レポートページに設定（正しい項目順序対応）
                _logger.LogDebug("データバインディング開始。アイテム数: {Count}", items.Count);
                PopulateReportData(report, items, subtotals, total);
                
                // レポートの準備
                _logger.LogInformation("レポートを生成しています...");
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
            
            // 小計用変数（正しい項目順序）
            decimal categoryDailySalesQty = 0;
            decimal categoryDailySalesAmount = 0;
            decimal categoryPurchaseDiscount = 0;
            decimal categoryStockAdjust = 0;
            decimal categoryProcessing = 0;
            decimal categoryTransfer = 0;
            decimal categoryIncentive = 0;
            decimal categoryGrossProfit1 = 0;
            decimal categoryGrossProfit2 = 0;
            decimal categoryMonthlySalesAmount = 0;
            decimal categoryMonthlyGrossProfit1 = 0;
            decimal categoryMonthlyGrossProfit2 = 0;
            
            // 有効なデータのみを処理し、商品分類でソート
            var validItems = items.Where(IsNotZeroItem)
                .OrderBy(item => item.ProductCategory1 ?? "")
                .ThenBy(item => item.ProductCode)
                .ToList();
            _logger.LogDebug("有効なデータ数: {Count}", validItems.Count);
            
            foreach (var item in validItems)
            {
                // 商品分類が変わったら小計行を出力
                if (!string.IsNullOrEmpty(currentCategory) && currentCategory != item.ProductCategory1)
                {
                    _logger.LogDebug("商品分類変更検出: {OldCategory} → {NewCategory}", currentCategory, item.ProductCategory1);
                    AddSubtotalRow(dataBand, currentY, currentCategory, 
                        categoryDailySalesQty, categoryDailySalesAmount, categoryPurchaseDiscount, 
                        categoryStockAdjust, categoryProcessing, categoryTransfer, categoryIncentive,
                        categoryGrossProfit1, categoryGrossProfit2, categoryMonthlySalesAmount,
                        categoryMonthlyGrossProfit1, categoryMonthlyGrossProfit2);
                    currentY += 18.9f;
                    
                    // 小計値をリセット
                    categoryDailySalesQty = 0;
                    categoryDailySalesAmount = 0;
                    categoryPurchaseDiscount = 0;
                    categoryStockAdjust = 0;
                    categoryProcessing = 0;
                    categoryTransfer = 0;
                    categoryIncentive = 0;
                    categoryGrossProfit1 = 0;
                    categoryGrossProfit2 = 0;
                    categoryMonthlySalesAmount = 0;
                    categoryMonthlyGrossProfit1 = 0;
                    categoryMonthlyGrossProfit2 = 0;
                }
                
                // 明細行を追加（正しい項目順序）
                AddDetailRow(dataBand, currentY, item);
                currentY += 18.9f;
                
                // 小計に加算
                currentCategory = item.ProductCategory1 ?? "";
                categoryDailySalesQty += item.DailySalesQuantity;
                categoryDailySalesAmount += item.DailySalesAmount;
                categoryPurchaseDiscount += item.DailyPurchaseDiscount;
                categoryStockAdjust += item.DailyInventoryAdjustment;
                categoryProcessing += item.DailyProcessingCost;
                categoryTransfer += item.DailyTransfer;
                categoryIncentive += item.DailyIncentive;
                categoryGrossProfit1 += item.DailyGrossProfit1;
                categoryGrossProfit2 += item.DailyGrossProfit2;
                categoryMonthlySalesAmount += item.MonthlySalesAmount;
                categoryMonthlyGrossProfit1 += item.MonthlyGrossProfit1;
                categoryMonthlyGrossProfit2 += item.MonthlyGrossProfit2;
            }
            
            // 最後の小計
            if (!string.IsNullOrEmpty(currentCategory))
            {
                _logger.LogDebug("最後の商品分類の小計を出力: {Category}", currentCategory);
                AddSubtotalRow(dataBand, currentY, currentCategory,
                    categoryDailySalesQty, categoryDailySalesAmount, categoryPurchaseDiscount, 
                    categoryStockAdjust, categoryProcessing, categoryTransfer, categoryIncentive,
                    categoryGrossProfit1, categoryGrossProfit2, categoryMonthlySalesAmount,
                    categoryMonthlyGrossProfit1, categoryMonthlyGrossProfit2);
                currentY += 18.9f;
            }
            
            // DataBandの高さを調整
            dataBand.Height = currentY;
            
            // 合計値を設定（正しい項目順序対応）
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
                Width = 171.65f,
                Height = 18.9f,
                Text = item.ProductName ?? "",
                Font = new Font("MS Gothic", 8),
                VertAlign = FR.VertAlign.Center
            };
            dataBand.Objects.Add(nameText);
            
            // 日計セクション（11列）
            AddTextObject(dataBand, y, "DailySalesQty", 171.65f, FormatNumber(item.DailySalesQuantity, 2), 75f);  // 数量は小数2桁
            AddTextObject(dataBand, y, "DailySalesAmount", 246.65f, FormatNumber(item.DailySalesAmount), 80f);  // 売上金額
            AddTextObject(dataBand, y, "PurchaseDiscount", 326.65f, FormatNumberWithMinus(item.DailyPurchaseDiscount), 75f);
            AddTextObject(dataBand, y, "StockAdjust", 401.65f, FormatNumberWithMinus(item.DailyInventoryAdjustment), 75f);
            AddTextObject(dataBand, y, "Processing", 476.65f, FormatNumberWithMinus(item.DailyProcessingCost), 75f);
            AddTextObject(dataBand, y, "Transfer", 551.65f, FormatNumberWithMinus(item.DailyTransfer), 75f);
            AddTextObject(dataBand, y, "Incentive", 626.65f, FormatNumberWithMinus(item.DailyIncentive), 75f);
            AddTextObject(dataBand, y, "GrossProfit1", 701.65f, FormatNumber(item.DailyGrossProfit1), 85f);  // 1粗利益
            AddTextObject(dataBand, y, "GrossProfitRate1", 786.65f, FormatRate(CalculateRate(item.DailyGrossProfit1, item.DailySalesAmount)), 60f);
            AddTextObject(dataBand, y, "GrossProfit2", 846.65f, FormatNumberWithTriangle(item.DailyGrossProfit2), 85f);  // 2粗利益、▲記号使用
            AddTextObject(dataBand, y, "GrossProfitRate2", 931.65f, FormatRate(CalculateRate(item.DailyGrossProfit2, item.DailySalesAmount)), 60f);
            
            // 月計セクション（5列）
            AddTextObject(dataBand, y, "MonthlySalesAmount", 991.65f, FormatNumber(item.MonthlySalesAmount), 132.25f);
            AddTextObject(dataBand, y, "MonthlyGrossProfit1", 1123.9f, FormatNumber(item.MonthlyGrossProfit1), 103.9f);
            AddTextObject(dataBand, y, "MonthlyGrossProfitRate1", 1227.8f, FormatRate(CalculateRate(item.MonthlyGrossProfit1, item.MonthlySalesAmount)), 60f);
            AddTextObject(dataBand, y, "MonthlyGrossProfit2", 1287.8f, FormatNumberWithTriangle(item.MonthlyGrossProfit2), 146.7f);  // ▲記号使用
            AddTextObject(dataBand, y, "MonthlyGrossProfitRate2", 1434.5f, FormatRate(CalculateRate(item.MonthlyGrossProfit2, item.MonthlySalesAmount)), 79.45f);
        }
        
        private void AddTextObject(FR.DataBand dataBand, float y, string namePrefix, float left, string text, float width)
        {
            var textObject = new FR.TextObject
            {
                Name = $"{namePrefix}_{y}",
                Left = left,
                Top = y,
                Width = width,
                Height = 18.9f,
                Text = text,
                Font = new Font("MS Gothic", 8),
                HorzAlign = FR.HorzAlign.Right,
                VertAlign = FR.VertAlign.Center
            };
            dataBand.Objects.Add(textObject);
        }
        
        
        private void AddSubtotalRow(FR.DataBand dataBand, float y, string category,
            decimal dailySalesQty, decimal dailySalesAmount, decimal purchaseDiscount, 
            decimal stockAdjust, decimal processing, decimal transfer, decimal incentive,
            decimal grossProfit1, decimal grossProfit2, decimal monthlySalesAmount,
            decimal monthlyGrossProfit1, decimal monthlyGrossProfit2)
        {
            var labelText = new FR.TextObject
            {
                Name = $"Subtotal_{y}",
                Left = 0,
                Top = y,
                Width = 171.65f,
                Height = 18.9f,
                Text = "＊　大分類計　＊",
                Font = new Font("MS Gothic", 8, FontStyle.Bold),
                HorzAlign = FR.HorzAlign.Center,
                VertAlign = FR.VertAlign.Center
            };
            dataBand.Objects.Add(labelText);
            
            // 各小計値を追加
            AddTextObject(dataBand, y, "SubtotalDailySalesQty", 171.65f, FormatNumber(dailySalesQty, 2), 75f);  // 数量は小数2桁
            AddTextObject(dataBand, y, "SubtotalDailySalesAmount", 246.65f, FormatNumber(dailySalesAmount), 80f);
            AddTextObject(dataBand, y, "SubtotalPurchaseDiscount", 326.65f, FormatNumberWithMinus(purchaseDiscount), 75f);
            AddTextObject(dataBand, y, "SubtotalStockAdjust", 401.65f, FormatNumberWithMinus(stockAdjust), 75f);
            AddTextObject(dataBand, y, "SubtotalProcessing", 476.65f, FormatNumberWithMinus(processing), 75f);
            AddTextObject(dataBand, y, "SubtotalTransfer", 551.65f, FormatNumberWithMinus(transfer), 75f);
            AddTextObject(dataBand, y, "SubtotalIncentive", 626.65f, FormatNumberWithMinus(incentive), 75f);
            AddTextObject(dataBand, y, "SubtotalGrossProfit1", 701.65f, FormatNumber(grossProfit1), 85f);
            AddTextObject(dataBand, y, "SubtotalGrossProfitRate1", 786.65f, FormatRate(CalculateRate(grossProfit1, dailySalesAmount)), 60f);
            AddTextObject(dataBand, y, "SubtotalGrossProfit2", 846.65f, FormatNumberWithTriangle(grossProfit2), 85f);  // ▲記号使用
            AddTextObject(dataBand, y, "SubtotalGrossProfitRate2", 931.65f, FormatRate(CalculateRate(grossProfit2, dailySalesAmount)), 60f);
            AddTextObject(dataBand, y, "SubtotalMonthlySalesAmount", 991.65f, FormatNumber(monthlySalesAmount), 132.25f);
            AddTextObject(dataBand, y, "SubtotalMonthlyGrossProfit1", 1123.9f, FormatNumber(monthlyGrossProfit1), 103.9f);
            AddTextObject(dataBand, y, "SubtotalMonthlyGrossProfitRate1", 1227.8f, FormatRate(CalculateRate(monthlyGrossProfit1, monthlySalesAmount)), 60f);
            AddTextObject(dataBand, y, "SubtotalMonthlyGrossProfit2", 1287.8f, FormatNumberWithTriangle(monthlyGrossProfit2), 146.7f);  // ▲記号使用
            AddTextObject(dataBand, y, "SubtotalMonthlyGrossProfitRate2", 1434.5f, FormatRate(CalculateRate(monthlyGrossProfit2, monthlySalesAmount)), 79.45f);
        }
        
        private bool IsNotZeroItem(DailyReportItem item)
        {
            // すべての数値項目が0でない場合のみ表示
            return item.DailySalesQuantity != 0 ||
                   item.DailySalesAmount != 0 ||
                   item.DailyGrossProfit1 != 0 ||
                   item.DailyGrossProfit2 != 0 ||
                   item.MonthlySalesAmount != 0 ||
                   item.MonthlyGrossProfit1 != 0 ||
                   item.MonthlyGrossProfit2 != 0;
        }
        
        private decimal CalculateRate(decimal profit, decimal amount)
        {
            if (amount == 0) return 0;
            return Math.Round((profit * 100) / amount, 2);
        }
        
        private string FormatNumber(decimal value, int decimals = 0)
        {
            if (value == 0) return "";
            
            // 整数表示（小数点なし）、ゼロサプレス
            var format = decimals > 0 ? $"#,##0.{new string('0', decimals)}" : "#,##0";
            
            if (value < 0)
            {
                // マイナス値は末尾に"-"を付ける
                return $"{Math.Abs(value).ToString(format)}-";
            }
            return value.ToString(format);
        }

        private string FormatNumberWithMinus(decimal value)
        {
            if (value == 0) return "";
            
            // 整数表示、マイナスは末尾
            if (value < 0)
                return $"{Math.Abs(value):#,##0}-";
            return value.ToString("#,##0");
        }
        
        private string FormatNumberWithTriangle(decimal value)
        {
            if (value == 0) return "";
            
            // 特定の条件で▲記号を使用（2粗利益など）、整数表示
            if (value < 0)
                return $"{Math.Abs(value):#,##0}▲";
            return value.ToString("#,##0");
        }
        
        private string FormatRate(decimal rate)
        {
            if (rate == 0) return "";
            
            // 率もマイナスの場合は末尾に"-"、小数2桁表示
            if (rate < 0)
                return $"{Math.Abs(rate):0.00}%-";
            
            return rate.ToString("0.00") + "%";
        }
        
        private void SetTotalValues(Report report, DailyReportTotal total)
        {
            // 正しい項目順序に対応した合計値設定
            SetTextObjectValue(report, "TotalDailySalesQty", FormatNumber(total.GrandTotalDailySalesQuantity, 2));  // 数量は小数2桁
            SetTextObjectValue(report, "TotalDailySalesAmount", FormatNumber(total.GrandTotalDailySalesAmount));
            SetTextObjectValue(report, "TotalPurchaseDiscount", FormatNumberWithMinus(total.GrandTotalDailyPurchaseDiscount));
            SetTextObjectValue(report, "TotalStockAdjust", FormatNumberWithMinus(total.GrandTotalDailyInventoryAdjustment));
            SetTextObjectValue(report, "TotalProcessing", FormatNumberWithMinus(total.GrandTotalDailyProcessingCost));
            SetTextObjectValue(report, "TotalTransfer", FormatNumberWithMinus(total.GrandTotalDailyTransfer));
            SetTextObjectValue(report, "TotalIncentive", FormatNumberWithMinus(total.GrandTotalDailyIncentive));
            SetTextObjectValue(report, "TotalGrossProfit1", FormatNumber(total.GrandTotalDailyGrossProfit1));
            SetTextObjectValue(report, "TotalGrossProfitRate1", FormatRate(CalculateRate(total.GrandTotalDailyGrossProfit1, total.GrandTotalDailySalesAmount)));
            SetTextObjectValue(report, "TotalGrossProfit2", FormatNumberWithTriangle(total.GrandTotalDailyGrossProfit2));  // ▲記号使用
            SetTextObjectValue(report, "TotalGrossProfitRate2", FormatRate(CalculateRate(total.GrandTotalDailyGrossProfit2, total.GrandTotalDailySalesAmount)));
            SetTextObjectValue(report, "TotalMonthlySalesAmount", FormatNumber(total.GrandTotalMonthlySalesAmount));
            SetTextObjectValue(report, "TotalMonthlyGrossProfit1", FormatNumber(total.GrandTotalMonthlyGrossProfit1));
            SetTextObjectValue(report, "TotalMonthlyGrossProfitRate1", FormatRate(CalculateRate(total.GrandTotalMonthlyGrossProfit1, total.GrandTotalMonthlySalesAmount)));
            SetTextObjectValue(report, "TotalMonthlyGrossProfit2", FormatNumberWithTriangle(total.GrandTotalMonthlyGrossProfit2));  // ▲記号使用
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