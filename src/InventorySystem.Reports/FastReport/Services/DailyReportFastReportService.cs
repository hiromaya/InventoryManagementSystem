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
            
            // テンプレートファイルのパス設定（アンマッチリストと同じパス構成）
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
                
                // デバッグ用ログ
                _logger.LogDebug("テンプレート読み込み完了。スクリプト言語: {ScriptLanguage}", 
                    report.GetType().GetProperty("ScriptLanguage")?.GetValue(report));
                
                // データを手動でTextObjectに設定
                _logger.LogDebug("データバインディング開始。アイテム数: {Count}", items.Count);
                BindDataManually(report, items, subtotals, total, reportDate);
                
                // レポートの準備（データソース登録なし）
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
        
        private void BindDataManually(Report report, List<DailyReportItem> items, 
            List<DailyReportSubtotal> subtotals, DailyReportTotal total, DateTime reportDate)
        {
            // ヘッダー情報の設定
            SetTextObjectValue(report, "ReportDateText", reportDate.ToString("yyyy年MM月dd日"));
            SetTextObjectValue(report, "PageInfo", "1 / 1");
            
            // データ行の設定（ゼロ明細を除外）
            int rowIndex = 0;
            foreach (var item in items.Where(IsNotZeroItem))
            {
                if (rowIndex >= 50) break; // 最大50行
                
                // 商品分類
                SetTextObjectValue(report, $"ProductCategory_{rowIndex}", item.ProductCategory1 ?? "");
                // 商品コード
                SetTextObjectValue(report, $"ProductCode_{rowIndex}", item.ProductCode ?? "");
                // 商品名
                SetTextObjectValue(report, $"ProductName_{rowIndex}", item.ProductName ?? "");
                
                // 日計項目
                SetTextObjectValue(report, $"DailySalesQuantity_{rowIndex}", 
                    item.DailySalesQuantity.ToString("N2"));
                SetTextObjectValue(report, $"DailySalesAmount_{rowIndex}", 
                    item.DailySalesAmount.ToString("N0"));
                SetTextObjectValue(report, $"PurchaseDiscount_{rowIndex}", 
                    FormatNumberWithTriangle(item.DailyPurchaseDiscount));
                SetTextObjectValue(report, $"StockAdjustment_{rowIndex}", 
                    FormatNumberWithTriangle(item.DailyInventoryAdjustment));
                SetTextObjectValue(report, $"ProcessingCost_{rowIndex}", 
                    FormatNumberWithTriangle(item.DailyProcessingCost));
                SetTextObjectValue(report, $"Transfer_{rowIndex}", 
                    FormatNumberWithTriangle(item.DailyTransfer));
                SetTextObjectValue(report, $"Incentive_{rowIndex}", 
                    FormatNumberWithTriangle(item.DailyIncentive));
                SetTextObjectValue(report, $"DailyGrossProfit1_{rowIndex}", 
                    item.DailyGrossProfit1.ToString("N0"));
                SetTextObjectValue(report, $"GrossProfitRate1_{rowIndex}", 
                    CalculateRate(item.DailyGrossProfit1, item.DailySalesAmount).ToString("N2") + "%");
                SetTextObjectValue(report, $"DailyGrossProfit2_{rowIndex}", 
                    FormatMonthlyGross2(item.DailyGrossProfit2));
                SetTextObjectValue(report, $"GrossProfitRate2_{rowIndex}", 
                    CalculateRate(item.DailyGrossProfit2, item.DailySalesAmount).ToString("N2") + "%");
                
                // 月計項目
                var monthlyAmount = item.DailySalesAmount + item.MonthlySalesAmount;
                var monthlyGross1 = item.DailyGrossProfit1 + item.MonthlyGrossProfit1;
                var monthlyGross2 = item.DailyGrossProfit2 + item.MonthlyGrossProfit1 - item.DailyDiscountAmount;
                
                SetTextObjectValue(report, $"MonthlyAmount_{rowIndex}", monthlyAmount.ToString("N0"));
                SetTextObjectValue(report, $"MonthlyGross1_{rowIndex}", monthlyGross1.ToString("N0"));
                SetTextObjectValue(report, $"MonthlyRate1_{rowIndex}", 
                    CalculateRate(monthlyGross1, monthlyAmount).ToString("N2") + "%");
                SetTextObjectValue(report, $"MonthlyGross2_{rowIndex}", 
                    FormatMonthlyGross2(monthlyGross2));
                SetTextObjectValue(report, $"MonthlyRate2_{rowIndex}", 
                    CalculateRate(monthlyGross2, monthlyAmount).ToString("N2") + "%");
                    
                rowIndex++;
            }
            
            // 合計の設定
            UpdateTotal(report, total);
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
        
        private string FormatMonthlyGross2(decimal value)
        {
            if (value < 0)
            {
                return $"▲{Math.Abs(value):N0}";
            }
            return value.ToString("N0");
        }
        
        private string FormatNumberWithTriangle(decimal value)
        {
            if (value < 0)
            {
                return "▲" + Math.Abs(value).ToString("N0");
            }
            return value.ToString("N0");
        }
        
        // このメソッドは不要になったので削除
        
        // このメソッドは不要になったので削除
        
        private void UpdateTotal(Report report, DailyReportTotal total)
        {
            // 合計値を設定
            SetTextObjectValue(report, "TotalSalesQty", total.GrandTotalDailySalesQuantity.ToString("N2"));
            SetTextObjectValue(report, "TotalSalesAmount", total.GrandTotalDailySalesAmount.ToString("N0"));
            SetTextObjectValue(report, "TotalPurchaseDiscount", total.GrandTotalDailyPurchaseDiscount.ToString("N0"));
            SetTextObjectValue(report, "TotalStockAdjust", total.GrandTotalDailyInventoryAdjustment.ToString("N0"));
            SetTextObjectValue(report, "TotalProcessing", total.GrandTotalDailyProcessingCost.ToString("N0"));
            SetTextObjectValue(report, "TotalTransfer", total.GrandTotalDailyTransfer.ToString("N0"));
            SetTextObjectValue(report, "TotalIncentive", total.GrandTotalDailyIncentive.ToString("N0"));
            SetTextObjectValue(report, "TotalGross1", total.GrandTotalDailyGrossProfit1.ToString("N0"));
            SetTextObjectValue(report, "TotalRate1", CalculateRate(total.GrandTotalDailyGrossProfit1, total.GrandTotalDailySalesAmount).ToString("N2") + "%");
            SetTextObjectValue(report, "TotalGross2", total.GrandTotalDailyGrossProfit2.ToString("N0"));
            SetTextObjectValue(report, "TotalRate2", CalculateRate(total.GrandTotalDailyGrossProfit2, total.GrandTotalDailySalesAmount).ToString("N2") + "%");
            SetTextObjectValue(report, "TotalMonthlyAmount", total.GrandTotalMonthlySalesAmount.ToString("N0"));
            SetTextObjectValue(report, "TotalMonthlyGross1", total.GrandTotalMonthlyGrossProfit1.ToString("N0"));
            SetTextObjectValue(report, "TotalMonthlyRate1", CalculateRate(total.GrandTotalMonthlyGrossProfit1, total.GrandTotalMonthlySalesAmount).ToString("N2") + "%");
            SetTextObjectValue(report, "TotalMonthlyGross2", FormatMonthlyGross2(total.GrandTotalMonthlyGrossProfit2));
            SetTextObjectValue(report, "TotalMonthlyRate2", CalculateRate(total.GrandTotalMonthlyGrossProfit2, total.GrandTotalMonthlySalesAmount).ToString("N2") + "%");
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