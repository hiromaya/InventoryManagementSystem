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
                
                // パラメータ設定
                report.SetParameterValue("ReportDate", reportDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("CreateDateTime", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒"));
                report.SetParameterValue("PageNumber", "1 / 1");
                
                // データを直接レポートページに設定（DataBandを使わない）
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
            
            // 小計用変数
            decimal categoryDailySales = 0;
            decimal categoryDailyGross1 = 0;
            decimal categoryDailyGross2 = 0;
            decimal categoryMonthlySales = 0;
            decimal categoryMonthlyGross1 = 0;
            decimal categoryMonthlyGross2 = 0;
            
            // 有効なデータのみを処理
            var validItems = items.Where(IsNotZeroItem).ToList();
            _logger.LogDebug("有効なデータ数: {Count}", validItems.Count);
            
            foreach (var item in validItems)
            {
                // 商品分類が変わったら小計行を出力
                if (!string.IsNullOrEmpty(currentCategory) && currentCategory != item.ProductCategory1)
                {
                    AddSubtotalRow(dataBand, currentY, currentCategory, 
                        categoryDailySales, categoryDailyGross1, categoryDailyGross2,
                        categoryMonthlySales, categoryMonthlyGross1, categoryMonthlyGross2);
                    currentY += 18.9f;
                    
                    // 小計値をリセット
                    categoryDailySales = 0;
                    categoryDailyGross1 = 0;
                    categoryDailyGross2 = 0;
                    categoryMonthlySales = 0;
                    categoryMonthlyGross1 = 0;
                    categoryMonthlyGross2 = 0;
                }
                
                // 明細行を追加
                AddDetailRow(dataBand, currentY, item);
                currentY += 18.9f;
                
                // 小計に加算
                currentCategory = item.ProductCategory1 ?? "";
                categoryDailySales += item.DailySalesAmount;
                categoryDailyGross1 += item.DailyGrossProfit1;
                categoryDailyGross2 += item.DailyGrossProfit2;
                categoryMonthlySales += item.DailySalesAmount + item.MonthlySalesAmount;
                categoryMonthlyGross1 += item.DailyGrossProfit1 + item.MonthlyGrossProfit1;
                categoryMonthlyGross2 += item.DailyGrossProfit2 + item.MonthlyGrossProfit1 - item.DailyDiscountAmount;
            }
            
            // 最後の小計
            if (!string.IsNullOrEmpty(currentCategory))
            {
                AddSubtotalRow(dataBand, currentY, currentCategory,
                    categoryDailySales, categoryDailyGross1, categoryDailyGross2,
                    categoryMonthlySales, categoryMonthlyGross1, categoryMonthlyGross2);
                currentY += 18.9f;
            }
            
            // DataBandの高さを調整
            dataBand.Height = currentY;
            
            // 合計値を設定
            SetTotalValues(report, total);
        }
        
        private void AddDetailRow(FR.DataBand dataBand, float y, DailyReportItem item)
        {
            // 商品コード
            var codeText = new FR.TextObject
            {
                Name = $"Code_{y}",
                Left = 0,
                Top = y,
                Width = 56.7f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = item.ProductCode ?? "",
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Center,
                Fill = { Color = Color.White }
            };
            dataBand.Objects.Add(codeText);
            
            // 商品名
            var nameText = new FR.TextObject
            {
                Name = $"Name_{y}",
                Left = 56.7f,
                Top = y,
                Width = 170.1f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = item.ProductName ?? "",
                Font = new Font("MS Gothic", 9),
                Fill = { Color = Color.White },
                Padding = new FR.Padding(2, 0, 2, 0)
            };
            dataBand.Objects.Add(nameText);
            
            // 売上数量（ゼロサプレス形式）
            var qtyText = new FR.TextObject
            {
                Name = $"Qty_{y}",
                Left = 226.8f,
                Top = y,
                Width = 56.7f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberZeroSuppress(item.DailySalesQuantity, 2),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(qtyText);
            
            // 売上金額
            var amountText = new FR.TextObject
            {
                Name = $"Amount_{y}",
                Left = 283.5f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberZeroSuppress(item.DailySalesAmount),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(amountText);
            
            // 仕入値引
            var discountText = new FR.TextObject
            {
                Name = $"Discount_{y}",
                Left = 359.1f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberWithTriangle(item.DailyPurchaseDiscount),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(discountText);
            
            // 在庫調整
            var adjustText = new FR.TextObject
            {
                Name = $"Adjust_{y}",
                Left = 434.7f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberWithTriangle(item.DailyInventoryAdjustment),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(adjustText);
            
            // 加工費
            var processText = new FR.TextObject
            {
                Name = $"Process_{y}",
                Left = 510.3f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberWithTriangle(item.DailyProcessingCost),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(processText);
            
            // 振替
            var transferText = new FR.TextObject
            {
                Name = $"Transfer_{y}",
                Left = 585.9f,
                Top = y,
                Width = 56.7f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberWithTriangle(item.DailyTransfer),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(transferText);
            
            // 奨励金
            var incentiveText = new FR.TextObject
            {
                Name = $"Incentive_{y}",
                Left = 642.6f,
                Top = y,
                Width = 56.7f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberWithTriangle(item.DailyIncentive),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(incentiveText);
            
            // 粗利益１
            var gross1Text = new FR.TextObject
            {
                Name = $"Gross1_{y}",
                Left = 699.3f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberZeroSuppress(item.DailyGrossProfit1),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(gross1Text);
            
            // 率１
            var rate1Text = new FR.TextObject
            {
                Name = $"Rate1_{y}",
                Left = 774.9f,
                Top = y,
                Width = 56.7f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatRate(CalculateRate(item.DailyGrossProfit1, item.DailySalesAmount)),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(rate1Text);
            
            // 粗利益２
            var gross2Text = new FR.TextObject
            {
                Name = $"Gross2_{y}",
                Left = 831.6f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatMonthlyGross2(item.DailyGrossProfit2),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(gross2Text);
            
            // 率２
            var rate2Text = new FR.TextObject
            {
                Name = $"Rate2_{y}",
                Left = 907.2f,
                Top = y,
                Width = 56.7f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatRate(CalculateRate(item.DailyGrossProfit2, item.DailySalesAmount)),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(rate2Text);
            
            // 月計項目
            var monthlyAmount = item.DailySalesAmount + item.MonthlySalesAmount;
            var monthlyGross1 = item.DailyGrossProfit1 + item.MonthlyGrossProfit1;
            var monthlyGross2 = item.DailyGrossProfit2 + item.MonthlyGrossProfit1 - item.DailyDiscountAmount;
            
            // 月計売上金額
            var monthAmountText = new FR.TextObject
            {
                Name = $"MonthAmount_{y}",
                Left = 963.9f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberZeroSuppress(monthlyAmount),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(monthAmountText);
            
            // 月計粗利益１
            var monthGross1Text = new FR.TextObject
            {
                Name = $"MonthGross1_{y}",
                Left = 1039.5f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatNumberZeroSuppress(monthlyGross1),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(monthGross1Text);
            
            // 月計率１
            var monthRate1Text = new FR.TextObject
            {
                Name = $"MonthRate1_{y}",
                Left = 1115.1f,
                Top = y,
                Width = 56.7f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatRate(CalculateRate(monthlyGross1, monthlyAmount)),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(monthRate1Text);
            
            // 月計粗利益２
            var monthGross2Text = new FR.TextObject
            {
                Name = $"MonthGross2_{y}",
                Left = 1171.8f,
                Top = y,
                Width = 75.6f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatMonthlyGross2(monthlyGross2),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(monthGross2Text);
            
            // 月計率２
            var monthRate2Text = new FR.TextObject
            {
                Name = $"MonthRate2_{y}",
                Left = 1247.4f,
                Top = y,
                Width = 56.7f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = FormatRate(CalculateRate(monthlyGross2, monthlyAmount)),
                Font = new Font("MS Gothic", 9),
                HorzAlign = FR.HorzAlign.Right,
                Fill = { Color = Color.White },
                Padding = new FR.Padding(0, 0, 2, 0)
            };
            dataBand.Objects.Add(monthRate2Text);
        }
        
        private void AddSubtotalRow(FR.DataBand dataBand, float y, string category,
            decimal dailySales, decimal dailyGross1, decimal dailyGross2,
            decimal monthlySales, decimal monthlyGross1, decimal monthlyGross2)
        {
            var labelText = new FR.TextObject
            {
                Name = $"Subtotal_{y}",
                Left = 0,
                Top = y,
                Width = 226.8f,
                Height = 18.9f,
                Border = { Lines = FR.BorderLines.All },
                Text = $"＊　{category}　計　＊",
                Font = new Font("MS Gothic", 9, FontStyle.Bold),
                HorzAlign = FR.HorzAlign.Center,
                Fill = { Color = Color.LightGray }
            };
            dataBand.Objects.Add(labelText);
            
            // 各小計値のTextObjectを追加（必要に応じて）
            // 仕様により小計行の内容を決定
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

        private string FormatMonthlyGross2(decimal value)
        {
            if (value < 0)
                return $"({Math.Abs(value):N0})";
            return value == 0 ? "" : value.ToString("N0");
        }
        
        private string FormatRate(decimal rate)
        {
            return rate == 0 ? "" : rate.ToString("N2") + "%";
        }
        
        private void SetTotalValues(Report report, DailyReportTotal total)
        {
            // 合計値を設定
            SetTextObjectValue(report, "TotalSalesQty", FormatNumberZeroSuppress(total.GrandTotalDailySalesQuantity, 2));
            SetTextObjectValue(report, "TotalSalesAmount", FormatNumberZeroSuppress(total.GrandTotalDailySalesAmount));
            SetTextObjectValue(report, "TotalPurchaseDiscount", FormatNumberWithTriangle(total.GrandTotalDailyPurchaseDiscount));
            SetTextObjectValue(report, "TotalStockAdjust", FormatNumberWithTriangle(total.GrandTotalDailyInventoryAdjustment));
            SetTextObjectValue(report, "TotalProcessing", FormatNumberWithTriangle(total.GrandTotalDailyProcessingCost));
            SetTextObjectValue(report, "TotalTransfer", FormatNumberWithTriangle(total.GrandTotalDailyTransfer));
            SetTextObjectValue(report, "TotalIncentive", FormatNumberWithTriangle(total.GrandTotalDailyIncentive));
            SetTextObjectValue(report, "TotalGross1", FormatNumberZeroSuppress(total.GrandTotalDailyGrossProfit1));
            SetTextObjectValue(report, "TotalRate1", FormatRate(CalculateRate(total.GrandTotalDailyGrossProfit1, total.GrandTotalDailySalesAmount)));
            SetTextObjectValue(report, "TotalGross2", FormatMonthlyGross2(total.GrandTotalDailyGrossProfit2));
            SetTextObjectValue(report, "TotalRate2", FormatRate(CalculateRate(total.GrandTotalDailyGrossProfit2, total.GrandTotalDailySalesAmount)));
            SetTextObjectValue(report, "TotalMonthlyAmount", FormatNumberZeroSuppress(total.GrandTotalMonthlySalesAmount));
            SetTextObjectValue(report, "TotalMonthlyGross1", FormatNumberZeroSuppress(total.GrandTotalMonthlyGrossProfit1));
            SetTextObjectValue(report, "TotalMonthlyRate1", FormatRate(CalculateRate(total.GrandTotalMonthlyGrossProfit1, total.GrandTotalMonthlySalesAmount)));
            SetTextObjectValue(report, "TotalMonthlyGross2", FormatMonthlyGross2(total.GrandTotalMonthlyGrossProfit2));
            SetTextObjectValue(report, "TotalMonthlyRate2", FormatRate(CalculateRate(total.GrandTotalMonthlyGrossProfit2, total.GrandTotalMonthlySalesAmount)));
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