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

                _logger.LogDebug("テンプレートファイルを読み込んでいます: {TemplatePath}", _templatePath);
                report.Load(_templatePath);

                // スクリプトを完全に無効化
                SetScriptLanguageToNone(report);

                // パラメータ設定
                report.SetParameterValue("ReportDate", reportDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("CreateDateTime", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒"));

                // DataTable方式でデータ登録
                var dataTable = CreateDailyReportDataTable(items, subtotals);
                _logger.LogInformation("DataTable作成完了 - 行数: {Count}", dataTable.Rows.Count);

                report.RegisterData(dataTable, "DailyReport");

                var dataSource = report.GetDataSource("DailyReport");
                if (dataSource == null)
                {
                    throw new InvalidOperationException("データソース 'DailyReport' の登録に失敗しました");
                }

                dataSource.Enabled = true;

                var dataBand = report.FindObject("Data1") as DataBand;
                if (dataBand != null)
                {
                    dataBand.DataSource = dataSource;
                    _logger.LogDebug("DataBandにデータソースを関連付けました");
                }
                else
                {
                    _logger.LogWarning("DataBand 'Data1' が見つかりません");
                }

                // 合計行の設定（ReportSummaryBand）
                SetTotalValues(report, total);

                _logger.LogInformation("レポートを生成しています...");
                report.Prepare();

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
        
        private const string RowTypeItem = "ITEM";
        private const string RowTypeSubtotal = "SUBTOTAL";
        private const string RowTypeSpacer = "SPACER";

        private DataTable CreateDailyReportDataTable(
            List<DailyReportItem> items,
            List<DailyReportSubtotal> subtotals)
        {
            var table = new DataTable("DailyReport");

            // 表示用カラム
            table.Columns.Add("ProductName", typeof(string));
            table.Columns.Add("DailySalesQty", typeof(string));
            table.Columns.Add("DailySalesAmount", typeof(string));
            table.Columns.Add("PurchaseDiscount", typeof(string));
            table.Columns.Add("StockAdjust", typeof(string));
            table.Columns.Add("Processing", typeof(string));
            table.Columns.Add("Transfer", typeof(string));
            table.Columns.Add("Incentive", typeof(string));
            table.Columns.Add("GrossProfit1", typeof(string));
            table.Columns.Add("GrossProfitRate1", typeof(string));
            table.Columns.Add("GrossProfit2", typeof(string));
            table.Columns.Add("GrossProfitRate2", typeof(string));
            table.Columns.Add("MonthlySalesAmount", typeof(string));
            table.Columns.Add("MonthlyGrossProfit1", typeof(string));
            table.Columns.Add("MonthlyGrossProfitRate1", typeof(string));
            table.Columns.Add("MonthlyGrossProfit2", typeof(string));
            table.Columns.Add("MonthlyGrossProfitRate2", typeof(string));

            // 制御用カラム
            table.Columns.Add("RowType", typeof(string));
            table.Columns.Add("IsSubtotal", typeof(bool));
            table.Columns.Add("ProductCategory1", typeof(string));

            var subtotalLookup = subtotals
                .GroupBy(s => s.ProductCategory1 ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.First());

            string currentCategory = string.Empty;

            foreach (var item in items.Where(IsNotZeroItem)
                .OrderBy(i => i.ProductCategory1 ?? string.Empty)
                .ThenBy(i => i.ProductCode))
            {
                var itemCategory = item.ProductCategory1 ?? string.Empty;

                if (!string.IsNullOrEmpty(currentCategory) && currentCategory != itemCategory)
                {
                    if (subtotalLookup.TryGetValue(currentCategory, out var subtotal))
                    {
                        AddSpacerRow(table);
                        AddSubtotalDataRow(table, subtotal);
                        AddSpacerRow(table);
                    }
                }

                AddItemDataRow(table, item);
                currentCategory = itemCategory;
            }

            if (!string.IsNullOrEmpty(currentCategory) &&
                subtotalLookup.TryGetValue(currentCategory, out var lastSubtotal))
            {
                AddSpacerRow(table);
                AddSubtotalDataRow(table, lastSubtotal);
                AddSpacerRow(table);
            }

            return table;
        }

        private void AddItemDataRow(DataTable table, DailyReportItem item)
        {
            var row = table.NewRow();

            row["ProductName"] = item.ProductName ?? item.ProductCode ?? string.Empty;
            row["DailySalesQty"] = FormatNumber(item.DailySalesQuantity, 2);
            row["DailySalesAmount"] = FormatNumber(item.DailySalesAmount);
            row["PurchaseDiscount"] = FormatNumberWithMinus(item.DailyPurchaseDiscount);
            row["StockAdjust"] = FormatNumberWithMinus(item.DailyInventoryAdjustment);
            row["Processing"] = FormatNumberWithMinus(item.DailyProcessingCost);
            row["Transfer"] = FormatNumberWithMinus(item.DailyTransfer);
            row["Incentive"] = FormatNumberWithMinus(item.DailyIncentive);
            row["GrossProfit1"] = FormatNumberWithTriangle(item.DailyGrossProfit1);
            row["GrossProfitRate1"] = FormatRateWithTriangle(CalculateRate(item.DailyGrossProfit1, item.DailySalesAmount));
            row["GrossProfit2"] = FormatNumberWithTriangle(item.DailyGrossProfit2);
            row["GrossProfitRate2"] = FormatRate(CalculateRate(item.DailyGrossProfit2, item.DailySalesAmount));
            row["MonthlySalesAmount"] = FormatNumber(item.MonthlySalesAmount);
            row["MonthlyGrossProfit1"] = FormatNumberWithTriangle(item.MonthlyGrossProfit1);
            row["MonthlyGrossProfitRate1"] = FormatRateWithTriangle(CalculateRate(item.MonthlyGrossProfit1, item.MonthlySalesAmount));
            row["MonthlyGrossProfit2"] = FormatNumberWithTriangle(item.MonthlyGrossProfit2);
            row["MonthlyGrossProfitRate2"] = FormatRate(CalculateRate(item.MonthlyGrossProfit2, item.MonthlySalesAmount));

            row["RowType"] = RowTypeItem;
            row["IsSubtotal"] = false;
            row["ProductCategory1"] = item.ProductCategory1 ?? string.Empty;

            table.Rows.Add(row);
        }

        private void AddSubtotalDataRow(DataTable table, DailyReportSubtotal subtotal)
        {
            var row = table.NewRow();

            row["ProductName"] = subtotal.SubtotalName;
            row["DailySalesQty"] = FormatNumber(subtotal.TotalDailySalesQuantity, 2);
            row["DailySalesAmount"] = FormatNumber(subtotal.TotalDailySalesAmount);
            row["PurchaseDiscount"] = FormatNumberWithMinus(subtotal.TotalDailyPurchaseDiscount);
            row["StockAdjust"] = FormatNumberWithMinus(subtotal.TotalDailyInventoryAdjustment);
            row["Processing"] = FormatNumberWithMinus(subtotal.TotalDailyProcessingCost);
            row["Transfer"] = FormatNumberWithMinus(subtotal.TotalDailyTransfer);
            row["Incentive"] = FormatNumberWithMinus(subtotal.TotalDailyIncentive);
            row["GrossProfit1"] = FormatNumberWithTriangle(subtotal.TotalDailyGrossProfit1);
            row["GrossProfitRate1"] = FormatRateWithTriangle(subtotal.TotalDailyGrossProfitRate1);
            row["GrossProfit2"] = FormatNumberWithTriangle(subtotal.TotalDailyGrossProfit2);
            row["GrossProfitRate2"] = FormatRate(subtotal.TotalDailyGrossProfitRate2);
            row["MonthlySalesAmount"] = FormatNumber(subtotal.TotalMonthlySalesAmount);
            row["MonthlyGrossProfit1"] = FormatNumberWithTriangle(subtotal.TotalMonthlyGrossProfit1);
            row["MonthlyGrossProfitRate1"] = FormatRateWithTriangle(subtotal.TotalMonthlyGrossProfitRate1);
            row["MonthlyGrossProfit2"] = FormatNumberWithTriangle(subtotal.TotalMonthlyGrossProfit2);
            row["MonthlyGrossProfitRate2"] = FormatRate(subtotal.TotalMonthlyGrossProfitRate2);

            row["RowType"] = RowTypeSubtotal;
            row["IsSubtotal"] = true;
            row["ProductCategory1"] = subtotal.ProductCategory1 ?? string.Empty;

            table.Rows.Add(row);
        }

        private void AddSpacerRow(DataTable table)
        {
            var row = table.NewRow();
            row["ProductName"] = string.Empty;
            row["DailySalesQty"] = string.Empty;
            row["DailySalesAmount"] = string.Empty;
            row["PurchaseDiscount"] = string.Empty;
            row["StockAdjust"] = string.Empty;
            row["Processing"] = string.Empty;
            row["Transfer"] = string.Empty;
            row["Incentive"] = string.Empty;
            row["GrossProfit1"] = string.Empty;
            row["GrossProfitRate1"] = string.Empty;
            row["GrossProfit2"] = string.Empty;
            row["GrossProfitRate2"] = string.Empty;
            row["MonthlySalesAmount"] = string.Empty;
            row["MonthlyGrossProfit1"] = string.Empty;
            row["MonthlyGrossProfitRate1"] = string.Empty;
            row["MonthlyGrossProfit2"] = string.Empty;
            row["MonthlyGrossProfitRate2"] = string.Empty;
            row["RowType"] = RowTypeSpacer;
            row["IsSubtotal"] = false;
            row["ProductCategory1"] = string.Empty;
            table.Rows.Add(row);
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
            return Math.Round((profit * 100) / amount, 2, MidpointRounding.AwayFromZero);
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
            if (rate < 0)
            {
                // マイナス率は末尾を"-"にする
                return $"{Math.Abs(rate):0.00}%-";
            }

            return $"{rate:0.00}%";
        }

        private string FormatRateWithTriangle(decimal rate)
        {
            if (rate == 0)
            {
                return string.Empty;
            }

            if (rate < 0)
            {
                return $"{Math.Abs(rate):0.00}▲%";
            }

            return $"{rate:0.00}%";
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
            SetTextObjectValue(report, "TotalGrossProfit1", FormatNumberWithTriangle(total.GrandTotalDailyGrossProfit1));
            SetTextObjectValue(report, "TotalGrossProfitRate1", FormatRateWithTriangle(total.GrandTotalDailyGrossProfitRate1));
            SetTextObjectValue(report, "TotalGrossProfit2", FormatNumberWithTriangle(total.GrandTotalDailyGrossProfit2));  // ▲記号使用
            SetTextObjectValue(report, "TotalGrossProfitRate2", FormatRate(total.GrandTotalDailyGrossProfitRate2));
            SetTextObjectValue(report, "TotalMonthlySalesAmount", FormatNumber(total.GrandTotalMonthlySalesAmount));
            SetTextObjectValue(report, "TotalMonthlyGrossProfit1", FormatNumberWithTriangle(total.GrandTotalMonthlyGrossProfit1));
            SetTextObjectValue(report, "TotalMonthlyGrossProfitRate1", FormatRateWithTriangle(total.GrandTotalMonthlyGrossProfitRate1));
            SetTextObjectValue(report, "TotalMonthlyGrossProfit2", FormatNumberWithTriangle(total.GrandTotalMonthlyGrossProfit2));  // ▲記号使用
            SetTextObjectValue(report, "TotalMonthlyGrossProfitRate2", FormatRate(total.GrandTotalMonthlyGrossProfitRate2));
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
