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
            
            _logger.LogInformation("営業日報テンプレートパス: {Path}", _templatePath);
        }

        public byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            try
            {
                _logger.LogCritical("===== 営業日報デバッグ開始 =====");
                _logger.LogInformation("営業日報PDF生成を開始します: JobDate={JobDate}", jobDate);

                // 入力データ検証
                var itemsList = items.ToList();
                _logger.LogCritical("入力データ件数: {Count}", itemsList.Count);
                
                foreach (var item in itemsList)
                {
                    _logger.LogCritical("分類コード: {Code}, 現金売上: {Sales}, 掛売上: {Credit}, 得意先分類: {Customer}, 仕入先分類: {Supplier}", 
                        item.ClassificationCode, 
                        item.DailyCashSales, 
                        item.DailyCreditSales,
                        item.CustomerClassName,
                        item.SupplierClassName);
                }

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
                var flatData = CreateFlatReportData(itemsList, jobDate);
                
                // DataTableの作成
                var dataTable = CreateDataTable(flatData);
                report.RegisterData(dataTable, "BusinessDailyReport");
                
                // FastReport登録確認
                _logger.LogCritical("===== FastReport登録確認 =====");
                var dataSource = report.GetDataSource("BusinessDailyReport");
                if (dataSource != null)
                {
                    dataSource.Enabled = true;
                    _logger.LogCritical("DataSource: Name={Name}, Enabled={Enabled}, RowCount={Count}", 
                        dataSource.Name, dataSource.Enabled, dataSource.RowCount);
                }
                else
                {
                    _logger.LogCritical("エラー: DataSource 'BusinessDailyReport' が登録されていません");
                    throw new InvalidOperationException("データソースの登録に失敗しました");
                }

                // 登録されているすべてのデータソースを確認
                _logger.LogCritical("登録済みDataSource一覧:");
                foreach (var ds in report.Dictionary.DataSources)
                {
                    // DataSourceの型によって適切にキャストして情報を取得
                    var name = ds.GetType().GetProperty("Name")?.GetValue(ds)?.ToString() ?? "Unknown";
                    var enabled = ds.GetType().GetProperty("Enabled")?.GetValue(ds)?.ToString() ?? "Unknown";
                    _logger.LogCritical("  - {Name} (Type: {Type}, Enabled: {Enabled})", 
                        name, ds.GetType().Name, enabled);
                }
                
                // 分類名をパラメータとして設定
                SetClassificationNames(report, items);
                
                // パラメータ設定
                report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分"));
                
                _logger.LogInformation("レポートを準備中...");
                
                // デバッグモード: レポート定義を保存
#if DEBUG
                try
                {
                    var debugReportPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, 
                        $"debug_business_daily_report_{DateTime.Now:yyyyMMddHHmmss}.frx");
                    report.Save(debugReportPath);
                    _logger.LogCritical("デバッグ: レポート定義を保存しました: {Path}", debugReportPath);
                }
                catch (Exception debugEx)
                {
                    _logger.LogCritical("デバッグ: レポート定義保存エラー: {Message}", debugEx.Message);
                }
                
                // デバッグモード: プレビューを有効化（Windows環境のみ）
                try
                {
                    var previewProperty = report.GetType().GetProperty("ShowPreparedReport");
                    if (previewProperty != null)
                    {
                        previewProperty.SetValue(report, true);
                        _logger.LogCritical("デバッグ: ShowPreparedReport を有効化しました");
                    }
                    else
                    {
                        _logger.LogCritical("デバッグ: ShowPreparedReport プロパティが見つかりません");
                    }
                }
                catch (Exception previewEx)
                {
                    _logger.LogCritical("デバッグ: Preview設定エラー: {Message}", previewEx.Message);
                }
#endif
                
                report.Prepare();
                
                // デバッグ: Prepare後のレポート状態確認
                _logger.LogCritical("===== Prepare後のレポート状態 =====");
                _logger.LogCritical("Pages.Count: {Count}", report.Pages.Count);
                _logger.LogCritical("PreparedPages.Count: {Count}", report.PreparedPages.Count);
                
                if (report.PreparedPages.Count == 0)
                {
                    _logger.LogCritical("エラー: PreparedPages が0件です。データが正しく処理されていません。");
                }
                
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
            
            // デバッグ: 入力データの確認
            _logger.LogInformation("入力データ確認: BusinessDailyReportItem件数={Count}", itemsList.Count);
            foreach (var item in itemsList.Take(3))
            {
                _logger.LogDebug("入力データ: ClassificationCode={ClassCode}, CustomerClass={CustomerName}, SupplierClass={SupplierName}, DailyCashSales={DailyCashSales}", 
                    item.ClassificationCode, item.CustomerClassName, item.SupplierClassName, item.DailyCashSales);
            }
            
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
            
            // デバッグ: フラットデータ生成結果の確認
            _logger.LogCritical("===== フラットデータ生成結果 =====");
            _logger.LogCritical("生成された行数: {Count}", flatData.Count);
            foreach (var row in flatData)
            {
                _logger.LogCritical("行{No}: {ItemName} => Total:{Total}, Class01:{C1}, Class02:{C2}", 
                    row.RowNumber, row.ItemName, row.Total, row.Class01, row.Class02);
            }
            
            return flatData;
        }

        private void SetClassificationNames(FR.Report report, IEnumerable<BusinessDailyReportItem> items)
        {
            var itemsList = items.ToList();
            
            // 分類名を取得してパラメータに設定
            _logger.LogCritical("===== パラメータ設定確認 =====");
            for (int i = 1; i <= 8; i++)
            {
                var classCode = i.ToString("000");
                var item = itemsList.FirstOrDefault(x => x.ClassificationCode == classCode);
                
                var customerName = item?.CustomerClassName ?? $"分類{i:00}";
                var supplierName = item?.SupplierClassName ?? $"分類{i:00}";
                
                report.SetParameterValue($"CustomerClass{i:00}", customerName);
                report.SetParameterValue($"SupplierClass{i:00}", supplierName);
                
                _logger.LogCritical("分類{i}: 得意先={Customer}, 仕入先={Supplier}", 
                    i, customerName, supplierName);
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
            
            // デバッグ: DataTable作成結果の確認
            _logger.LogCritical("===== DataTable生成結果 =====");
            _logger.LogCritical("行数: {Rows}, 列数: {Columns}", table.Rows.Count, table.Columns.Count);

            // カラム名の確認
            var columnNames = string.Join(", ", table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            _logger.LogCritical("カラム名: {Columns}", columnNames);

            // データの確認
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                _logger.LogCritical("Row[{i}]: ItemName={ItemName}, Total={Total}, Class01={Class01}", 
                    i, row["ItemName"], row["Total"], row["Class01"]);
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
        public string ItemName { get; set; } = string.Empty;
        public string Total { get; set; } = string.Empty;
        public string Class01 { get; set; } = string.Empty;
        public string Class02 { get; set; } = string.Empty;
        public string Class03 { get; set; } = string.Empty;
        public string Class04 { get; set; } = string.Empty;
        public string Class05 { get; set; } = string.Empty;
        public string Class06 { get; set; } = string.Empty;
        public string Class07 { get; set; } = string.Empty;
        public string Class08 { get; set; } = string.Empty;
    }
}
#endif
#pragma warning restore CA1416