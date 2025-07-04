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
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDir, "Reports", "Templates", "DailyReport.frx");
            
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
                
                // データセットの準備
                var dataSet = PrepareDataSet(items, subtotals, total);
                
                // データソースの登録
                report.RegisterData(dataSet);
                
                // パラメータの設定
                report.SetParameterValue("ReportDate", reportDate);
                
                // 小計・合計の動的設定
                ConfigureSubtotalsAndTotals(report, subtotals, total);
                
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
        
        private System.Data.DataSet PrepareDataSet(
            List<DailyReportItem> items, 
            List<DailyReportSubtotal> subtotals, 
            DailyReportTotal total)
        {
            var dataSet = new System.Data.DataSet("DailyReport");
            
            // メインデータテーブル
            var mainTable = new DataTable("DailyReportData");
            
            // 列定義
            mainTable.Columns.Add("ProductCategory", typeof(string));
            mainTable.Columns.Add("ProductCode", typeof(string));
            mainTable.Columns.Add("ProductName", typeof(string));
            mainTable.Columns.Add("DailySalesQuantity", typeof(decimal));
            mainTable.Columns.Add("DailySalesAmount", typeof(decimal));
            mainTable.Columns.Add("PurchaseDiscount", typeof(decimal));
            mainTable.Columns.Add("StockAdjustment", typeof(decimal));
            mainTable.Columns.Add("ProcessingCost", typeof(decimal));
            mainTable.Columns.Add("Transfer", typeof(decimal));
            mainTable.Columns.Add("Incentive", typeof(decimal));
            mainTable.Columns.Add("DailyGrossProfit1", typeof(decimal));
            mainTable.Columns.Add("GrossProfitRate1", typeof(decimal));
            mainTable.Columns.Add("DailyGrossProfit2", typeof(decimal));
            mainTable.Columns.Add("GrossProfitRate2", typeof(decimal));
            mainTable.Columns.Add("MonthlyAmount", typeof(decimal));
            mainTable.Columns.Add("MonthlyGross1", typeof(decimal));
            mainTable.Columns.Add("MonthlyRate1", typeof(decimal));
            mainTable.Columns.Add("MonthlyGross2", typeof(decimal));
            mainTable.Columns.Add("MonthlyGross2Display", typeof(string));
            mainTable.Columns.Add("MonthlyRate2", typeof(decimal));
            
            // データ行の追加（ゼロ明細を除外）
            foreach (var item in items.Where(IsNotZeroItem))
            {
                var row = mainTable.NewRow();
                
                // 基本項目
                row["ProductCategory"] = item.ProductCategory1 ?? "";
                row["ProductCode"] = item.ProductCode ?? "";
                row["ProductName"] = item.ProductName ?? "";
                
                // 日計項目
                row["DailySalesQuantity"] = item.DailySalesQuantity;
                row["DailySalesAmount"] = item.DailySalesAmount;
                row["PurchaseDiscount"] = item.DailyPurchaseDiscount;
                row["StockAdjustment"] = item.DailyInventoryAdjustment;
                row["ProcessingCost"] = item.DailyProcessingCost;
                row["Transfer"] = item.DailyTransfer;
                row["Incentive"] = item.DailyIncentive;
                row["DailyGrossProfit1"] = item.DailyGrossProfit1;
                row["GrossProfitRate1"] = CalculateRate(item.DailyGrossProfit1, item.DailySalesAmount);
                row["DailyGrossProfit2"] = item.DailyGrossProfit2;
                row["GrossProfitRate2"] = CalculateRate(item.DailyGrossProfit2, item.DailySalesAmount);
                
                // 月計項目
                var monthlyAmount = item.DailySalesAmount + item.MonthlySalesAmount;
                var monthlyGross1 = item.DailyGrossProfit1 + item.MonthlyGrossProfit1;
                var monthlyGross2 = item.DailyGrossProfit2 + item.MonthlyGrossProfit1 - item.DailyDiscountAmount;
                
                row["MonthlyAmount"] = monthlyAmount;
                row["MonthlyGross1"] = monthlyGross1;
                row["MonthlyRate1"] = CalculateRate(monthlyGross1, monthlyAmount);
                row["MonthlyGross2"] = Math.Abs(monthlyGross2);
                row["MonthlyGross2Display"] = FormatMonthlyGross2(monthlyGross2);
                row["MonthlyRate2"] = CalculateRate(monthlyGross2, monthlyAmount);
                
                mainTable.Rows.Add(row);
            }
            
            dataSet.Tables.Add(mainTable);
            return dataSet;
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
        
        private void ConfigureSubtotalsAndTotals(Report report, List<DailyReportSubtotal> subtotals, DailyReportTotal total)
        {
            // グループフッター（小計）の設定
            var groupFooter = report.FindObject("GroupFooter1") as FR.GroupFooterBand;
            if (groupFooter != null)
            {
                // C#側で小計データを管理
                var subtotalDict = subtotals.ToDictionary(s => s.ProductCategory1);
                
                // データ更新時のイベントハンドラー
                var databand = report.FindObject("Data1") as FR.DataBand;
                if (databand != null)
                {
                    string currentCategory = "";
                    var categoryData = new List<System.Data.DataRow>();
                    
                    databand.BeforePrint += (sender, e) =>
                    {
                        var category = report.GetColumnValue("DailyReportData.ProductCategory")?.ToString() ?? "";
                        if (category != currentCategory && !string.IsNullOrEmpty(currentCategory))
                        {
                            // カテゴリが変わったら小計を設定
                            UpdateSubtotal(report, currentCategory, categoryData);
                            categoryData.Clear();
                        }
                        currentCategory = category;
                    };
                }
            }
            
            // 合計の設定
            UpdateTotal(report, total);
        }
        
        private void UpdateSubtotal(Report report, string category, List<System.Data.DataRow> categoryData)
        {
            if (categoryData.Count == 0) return;
            
            // 小計を計算
            var subtotal = new
            {
                SalesQty = categoryData.Sum(r => (decimal)r["DailySalesQuantity"]),
                SalesAmount = categoryData.Sum(r => (decimal)r["DailySalesAmount"]),
                PurchaseDiscount = categoryData.Sum(r => (decimal)r["PurchaseDiscount"]),
                StockAdjustment = categoryData.Sum(r => (decimal)r["StockAdjustment"]),
                ProcessingCost = categoryData.Sum(r => (decimal)r["ProcessingCost"]),
                Transfer = categoryData.Sum(r => (decimal)r["Transfer"]),
                Incentive = categoryData.Sum(r => (decimal)r["Incentive"]),
                Gross1 = categoryData.Sum(r => (decimal)r["DailyGrossProfit1"]),
                Gross2 = categoryData.Sum(r => (decimal)r["DailyGrossProfit2"]),
                MonthlyAmount = categoryData.Sum(r => (decimal)r["MonthlyAmount"]),
                MonthlyGross1 = categoryData.Sum(r => (decimal)r["MonthlyGross1"]),
                MonthlyGross2 = categoryData.Sum(r => (decimal)r["MonthlyGross2"])
            };
            
            // 小計オブジェクトに値を設定
            SetTextObjectValue(report, "SubtotalSalesQty", subtotal.SalesQty.ToString("N2"));
            SetTextObjectValue(report, "SubtotalSalesAmount", subtotal.SalesAmount.ToString("N0"));
            SetTextObjectValue(report, "SubtotalPurchaseDiscount", subtotal.PurchaseDiscount.ToString("N0"));
            SetTextObjectValue(report, "SubtotalStockAdjust", subtotal.StockAdjustment.ToString("N0"));
            SetTextObjectValue(report, "SubtotalProcessing", subtotal.ProcessingCost.ToString("N0"));
            SetTextObjectValue(report, "SubtotalTransfer", subtotal.Transfer.ToString("N0"));
            SetTextObjectValue(report, "SubtotalIncentive", subtotal.Incentive.ToString("N0"));
            SetTextObjectValue(report, "SubtotalGross1", subtotal.Gross1.ToString("N0"));
            SetTextObjectValue(report, "SubtotalRate1", CalculateRate(subtotal.Gross1, subtotal.SalesAmount).ToString("N2") + "%");
            SetTextObjectValue(report, "SubtotalGross2", subtotal.Gross2.ToString("N0"));
            SetTextObjectValue(report, "SubtotalRate2", CalculateRate(subtotal.Gross2, subtotal.SalesAmount).ToString("N2") + "%");
            SetTextObjectValue(report, "SubtotalMonthlyAmount", subtotal.MonthlyAmount.ToString("N0"));
            SetTextObjectValue(report, "SubtotalMonthlyGross1", subtotal.MonthlyGross1.ToString("N0"));
            SetTextObjectValue(report, "SubtotalMonthlyRate1", CalculateRate(subtotal.MonthlyGross1, subtotal.MonthlyAmount).ToString("N2") + "%");
            SetTextObjectValue(report, "SubtotalMonthlyGross2", FormatMonthlyGross2(subtotal.MonthlyGross2));
            SetTextObjectValue(report, "SubtotalMonthlyRate2", CalculateRate(subtotal.MonthlyGross2, subtotal.MonthlyAmount).ToString("N2") + "%");
        }
        
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