#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using FastReport;
using FastReport.Export.Pdf;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    public class BusinessDailyReportFastReportService : 
        InventorySystem.Reports.Interfaces.IBusinessDailyReportService, 
        InventorySystem.Core.Interfaces.IBusinessDailyReportReportService
    {
        private readonly ILogger<BusinessDailyReportFastReportService> _logger;
        private readonly string _templatePath;

        public BusinessDailyReportFastReportService(ILogger<BusinessDailyReportFastReportService> logger)
        {
            _logger = logger;
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "BusinessDailyReport.frx");
        }

        public byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            try
            {
                _logger.LogInformation("営業日報PDF生成を開始します: JobDate={JobDate}", jobDate);

                if (!File.Exists(_templatePath))
                {
                    throw new FileNotFoundException($"営業日報テンプレートが見つかりません: {_templatePath}");
                }

                using var report = new FR.Report();
                
                // FastReportの設定（商品勘定と同じ）
                report.ReportResourceString = "";
                report.FileName = _templatePath;
                
                // テンプレート読み込み
                report.Load(_templatePath);
                
                // ScriptLanguageをNoneに設定（最重要）
                SetScriptLanguageToNone(report);
                
                // フラットデータの作成（16行のデータ）
                var flatData = CreateFlatReportData(items, jobDate);
                
                // DataTableの作成
                var dataTable = CreateDataTable(flatData);
                report.RegisterData(dataTable, "BusinessDailyReport");
                
                // データソースの有効化
                var dataSource = report.GetDataSource("BusinessDailyReport");
                if (dataSource != null)
                {
                    dataSource.Enabled = true;
                    _logger.LogInformation("データソース登録確認: {Name}, 行数: {Count}", 
                        dataSource.Name, dataSource.RowCount);
                }
                else
                {
                    _logger.LogError("データソース登録失敗: 'BusinessDailyReport' が見つかりません");
                    throw new InvalidOperationException("データソースの登録に失敗しました");
                }
                
                // 分類名をパラメータとして設定
                SetClassificationNames(report, items);
                
                // パラメータ設定
                report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分"));
                
                _logger.LogInformation("レポートを準備中...");
                report.Prepare();
                
                // PDF出力
                using var pdfExport = new PDFExport
                {
                    EmbeddingFonts = true,
                    Title = $"営業日報_{jobDate:yyyyMMdd}",
                    Subject = "営業日報",
                    Creator = "在庫管理システム",
                    Author = "在庫管理システム",
                    TextInCurves = false,
                    JpegQuality = 95,
                    OpenAfterExport = false
                };
                
                using var stream = new MemoryStream();
                report.Export(pdfExport, stream);
                
                var result = stream.ToArray();
                _logger.LogInformation("営業日報PDF生成完了: サイズ={Size}bytes", result.Length);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "営業日報PDF生成中にエラーが発生しました");
                throw;
            }
        }

        private void SetScriptLanguageToNone(FR.Report report)
        {
            try
            {
                var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                if (scriptLanguageProperty != null)
                {
                    var scriptLanguageType = scriptLanguageProperty.PropertyType;
                    if (scriptLanguageType.IsEnum)
                    {
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
                
                // Scriptプロパティもnullに設定（追加の安全策）
                var scriptProperty = report.GetType().GetProperty("Script", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
            }
        }

        private List<BusinessDailyReportFlatRow> CreateFlatReportData(
            IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            var flatData = new List<BusinessDailyReportFlatRow>();
            var itemsList = items.ToList();
            
            // 16行の固定項目
            string[] rowLabels = new[]
            {
                "【日計】 現金売上", "現売消費税", "掛売上＋売上返品", "売上値引",
                "掛売消費税", "現金仕入", "現仕消費税", "掛仕入＋仕入返品",
                "仕入値引", "掛仕入消費税", "現金・小切手・手形入金", "振込入金",
                "入金値引・その他入金", "現金・小切手・手形支払", "振込支払", "支払値引・その他支払"
            };
            
            // 16行分のデータを作成
            for (int rowIndex = 0; rowIndex < 16; rowIndex++)
            {
                var row = new BusinessDailyReportFlatRow
                {
                    RowNumber = rowIndex + 1,
                    ItemName = rowLabels[rowIndex],
                    Total = FormatNumber(GetValueByRow(itemsList, "000", rowIndex)),
                    Class01 = FormatNumber(GetValueByRow(itemsList, "001", rowIndex)),
                    Class02 = FormatNumber(GetValueByRow(itemsList, "002", rowIndex)),
                    Class03 = FormatNumber(GetValueByRow(itemsList, "003", rowIndex)),
                    Class04 = FormatNumber(GetValueByRow(itemsList, "004", rowIndex)),
                    Class05 = FormatNumber(GetValueByRow(itemsList, "005", rowIndex)),
                    Class06 = FormatNumber(GetValueByRow(itemsList, "006", rowIndex)),
                    Class07 = FormatNumber(GetValueByRow(itemsList, "007", rowIndex)),
                    Class08 = FormatNumber(GetValueByRow(itemsList, "008", rowIndex))
                };
                
                flatData.Add(row);
            }
            
            return flatData;
        }

        private void SetClassificationNames(FR.Report report, IEnumerable<BusinessDailyReportItem> items)
        {
            var itemsList = items.ToList();
            
            // 分類名を取得してパラメータに設定
            for (int i = 1; i <= 8; i++)
            {
                var classCode = i.ToString("000");
                var item = itemsList.FirstOrDefault(x => x.ClassificationCode == classCode);
                
                var customerName = item?.CustomerClassName ?? $"分類{i:00}";
                var supplierName = item?.SupplierClassName ?? $"分類{i:00}";
                
                report.SetParameterValue($"CustomerClass{i:00}", customerName);
                report.SetParameterValue($"SupplierClass{i:00}", supplierName);
            }
        }

        private decimal GetValueByRow(List<BusinessDailyReportItem> items, string classCode, int rowIndex)
        {
            var item = items.FirstOrDefault(x => x.ClassificationCode == classCode);
            if (item == null) return 0m;
            
            return rowIndex switch
            {
                0 => item.DailyCashSales,
                1 => item.DailyCashSalesTax,
                2 => item.DailyCreditSales,
                3 => item.DailySalesDiscount,
                4 => item.DailyCreditSalesTax,
                5 => item.DailyCashPurchase,
                6 => item.DailyCashPurchaseTax,
                7 => item.DailyCreditPurchase,
                8 => item.DailyPurchaseDiscount,
                9 => item.DailyCreditPurchaseTax,
                10 => item.DailyCashReceipt,
                11 => item.DailyBankReceipt,
                12 => item.DailyOtherReceipt,
                13 => item.DailyCashPayment,
                14 => item.DailyBankPayment,
                15 => item.DailyOtherPayment,
                _ => 0m
            };
        }

        private string FormatNumber(decimal value)
        {
            if (value == 0) return "";
            return value < 0 ? $"▲{Math.Abs(value):N0}" : value.ToString("N0");
        }

        private DataTable CreateDataTable(List<BusinessDailyReportFlatRow> flatData)
        {
            var table = new DataTable("BusinessDailyReport");
            
            // カラム定義（すべて文字列型で統一）
            table.Columns.Add("RowNumber", typeof(string));
            table.Columns.Add("ItemName", typeof(string));
            table.Columns.Add("Total", typeof(string));
            table.Columns.Add("Class01", typeof(string));
            table.Columns.Add("Class02", typeof(string));
            table.Columns.Add("Class03", typeof(string));
            table.Columns.Add("Class04", typeof(string));
            table.Columns.Add("Class05", typeof(string));
            table.Columns.Add("Class06", typeof(string));
            table.Columns.Add("Class07", typeof(string));
            table.Columns.Add("Class08", typeof(string));
            
            // データ追加
            foreach (var row in flatData)
            {
                table.Rows.Add(
                    row.RowNumber.ToString(),
                    row.ItemName,
                    row.Total,
                    row.Class01,
                    row.Class02,
                    row.Class03,
                    row.Class04,
                    row.Class05,
                    row.Class06,
                    row.Class07,
                    row.Class08
                );
            }
            
            return table;
        }

        // インターフェース実装用
        public byte[] GenerateBusinessDailyReport(IEnumerable<object> businessDailyReportItems, DateTime jobDate)
        {
            var items = businessDailyReportItems.Cast<BusinessDailyReportItem>();
            return GenerateBusinessDailyReport(items, jobDate);
        }
    }

    // フラットデータ用のクラス
    public class BusinessDailyReportFlatRow
    {
        public int RowNumber { get; set; }
        public string ItemName { get; set; }
        public string Total { get; set; }
        public string Class01 { get; set; }
        public string Class02 { get; set; }
        public string Class03 { get; set; }
        public string Class04 { get; set; }
        public string Class05 { get; set; }
        public string Class06 { get; set; }
        public string Class07 { get; set; }
        public string Class08 { get; set; }
    }
}
#endif
#pragma warning restore CA1416