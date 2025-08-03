#pragma warning disable CA1416
#if WINDOWS

using FastReport;
using FastReport.Export.Pdf;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace InventorySystem.Reports.FastReport.Services
{
    public class BusinessDailyReportFastReportService : InventorySystem.Reports.Interfaces.IBusinessDailyReportService, InventorySystem.Core.Interfaces.IBusinessDailyReportReportService
    {
        private readonly ILogger<BusinessDailyReportFastReportService> _logger;

        public BusinessDailyReportFastReportService(ILogger<BusinessDailyReportFastReportService> logger)
        {
            _logger = logger;
        }

        public byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            try
            {
                _logger.LogInformation("営業日報PDF生成を開始します: JobDate={JobDate}", jobDate);

                using var report = new Report();
                
                // テンプレートファイルのパス
                var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    "FastReport", "Templates", "BusinessDailyReport.frx");

                if (!File.Exists(templatePath))
                {
                    throw new FileNotFoundException($"営業日報テンプレートファイルが見つかりません: {templatePath}");
                }

                // テンプレート読み込み
                report.Load(templatePath);

                // .NET 8対応: ScriptLanguageを強制的にNoneに設定
                try
                {
                    _logger.LogInformation("ScriptLanguage設定を開始します");
                    
                    // リフレクションを使用してScriptLanguageプロパティを取得
                    var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                    if (scriptLanguageProperty != null)
                    {
                        _logger.LogInformation($"ScriptLanguageプロパティが見つかりました: {scriptLanguageProperty.PropertyType}");
                        
                        var scriptLanguageType = scriptLanguageProperty.PropertyType;
                        if (scriptLanguageType.IsEnum)
                        {
                            // 現在の値をログ出力
                            var currentValue = scriptLanguageProperty.GetValue(report);
                            _logger.LogInformation($"現在のScriptLanguage値: {currentValue}");
                            
                            // FastReport.ScriptLanguage.None を設定
                            var noneValue = Enum.GetValues(scriptLanguageType)
                                .Cast<object>()
                                .FirstOrDefault(v => v.ToString() == "None");
                            
                            if (noneValue != null)
                            {
                                scriptLanguageProperty.SetValue(report, noneValue);
                                var newValue = scriptLanguageProperty.GetValue(report);
                                _logger.LogInformation($"ScriptLanguageをNoneに設定しました: {currentValue} → {newValue}");
                            }
                            else
                            {
                                _logger.LogWarning("ScriptLanguage.Noneが見つかりませんでした");
                                var enumValues = Enum.GetValues(scriptLanguageType).Cast<object>().Select(v => v.ToString()).ToArray();
                                _logger.LogInformation($"利用可能な値: {string.Join(", ", enumValues)}");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"ScriptLanguageプロパティがEnum型ではありません: {scriptLanguageType}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("ScriptLanguageプロパティが見つかりませんでした");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
                    _logger.LogWarning($"スタックトレース: {ex.StackTrace}");
                    // エラーが発生しても処理を継続
                }

                // 追加の.NET 8対応: スクリプトテキストをクリア
                try
                {
                    var scriptTextProperty = report.GetType().GetProperty("ScriptText");
                    if (scriptTextProperty != null && scriptTextProperty.CanWrite)
                    {
                        scriptTextProperty.SetValue(report, "");
                        _logger.LogInformation("ScriptTextをクリアしました");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"ScriptTextクリア時の警告: {ex.Message}");
                }

                // データソース作成（16行に展開）
                _logger.LogInformation("データを16行に展開しています...");
                var dataTable = CreateExpandedDataTable(items);
                report.RegisterData(dataTable, "BusinessDailyReportExpanded");

                // パラメータ設定
                report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分"));

                // レポート準備
                report.Prepare();

                // PDF出力
                using var pdfExport = new PDFExport();
                using var stream = new MemoryStream();
                
                // PDF設定
                pdfExport.ShowProgress = false;
                pdfExport.Subject = $"営業日報 {jobDate:yyyy年MM月dd日}";
                pdfExport.Title = "営業日報";
                pdfExport.Author = "在庫管理システム";
                pdfExport.Creator = "FastReport.NET";

                report.Export(pdfExport, stream);
                
                var result = stream.ToArray();
                
                _logger.LogInformation("営業日報PDF生成が完了しました: サイズ={Size}bytes", result.Length);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "営業日報PDF生成中にエラーが発生しました: JobDate={JobDate}", jobDate);
                throw;
            }
        }

        private DataTable CreateDataTable(IEnumerable<BusinessDailyReportItem> items)
        {
            var dataTable = new DataTable("BusinessDailyReportItems");

            // カラム定義
            dataTable.Columns.Add("ClassificationCode", typeof(string));
            dataTable.Columns.Add("CustomerClassName", typeof(string));
            dataTable.Columns.Add("SupplierClassName", typeof(string));
            
            // 日計項目（16項目）
            dataTable.Columns.Add("DailyCashSales", typeof(decimal));
            dataTable.Columns.Add("DailyCashSalesTax", typeof(decimal));
            dataTable.Columns.Add("DailyCreditSales", typeof(decimal));
            dataTable.Columns.Add("DailySalesDiscount", typeof(decimal));
            dataTable.Columns.Add("DailyCreditSalesTax", typeof(decimal));
            dataTable.Columns.Add("DailyCashPurchase", typeof(decimal));
            dataTable.Columns.Add("DailyCashPurchaseTax", typeof(decimal));
            dataTable.Columns.Add("DailyCreditPurchase", typeof(decimal));
            dataTable.Columns.Add("DailyPurchaseDiscount", typeof(decimal));
            dataTable.Columns.Add("DailyCreditPurchaseTax", typeof(decimal));
            dataTable.Columns.Add("DailyCashReceipt", typeof(decimal));
            dataTable.Columns.Add("DailyBankReceipt", typeof(decimal));
            dataTable.Columns.Add("DailyOtherReceipt", typeof(decimal));
            dataTable.Columns.Add("DailyCashPayment", typeof(decimal));
            dataTable.Columns.Add("DailyBankPayment", typeof(decimal));
            dataTable.Columns.Add("DailyOtherPayment", typeof(decimal));
            
            // 計算項目
            dataTable.Columns.Add("DailySalesTotal", typeof(decimal));
            dataTable.Columns.Add("DailyPurchaseTotal", typeof(decimal));

            // データ追加（分類000～035の36行）
            var itemList = items.ToList();
            
            // 36行分のデータを確保（不足分は空行で補完）
            for (int i = 0; i < 36; i++)
            {
                var classificationCode = i == 0 ? "000" : i.ToString("D3");
                var item = itemList.FirstOrDefault(x => x.ClassificationCode == classificationCode);

                if (item != null)
                {
                    dataTable.Rows.Add(
                        item.ClassificationCode,
                        item.CustomerClassName ?? "",
                        item.SupplierClassName ?? "",
                        item.DailyCashSales,
                        item.DailyCashSalesTax,
                        item.DailyCreditSales,
                        item.DailySalesDiscount,
                        item.DailyCreditSalesTax,
                        item.DailyCashPurchase,
                        item.DailyCashPurchaseTax,
                        item.DailyCreditPurchase,
                        item.DailyPurchaseDiscount,
                        item.DailyCreditPurchaseTax,
                        item.DailyCashReceipt,
                        item.DailyBankReceipt,
                        item.DailyOtherReceipt,
                        item.DailyCashPayment,
                        item.DailyBankPayment,
                        item.DailyOtherPayment,
                        item.DailySalesTotal,
                        item.DailyPurchaseTotal
                    );
                }
                else
                {
                    // 空行
                    dataTable.Rows.Add(
                        classificationCode,
                        "",
                        "",
                        0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m,
                        0m, 0m
                    );
                }
            }

            return dataTable;
        }

        private DataTable CreateExpandedDataTable(IEnumerable<BusinessDailyReportItem> items)
        {
            var table = new DataTable("BusinessDailyReportExpanded");
            
            // カラム定義
            table.Columns.Add("RowNumber", typeof(int));
            table.Columns.Add("ItemName", typeof(string));
            table.Columns.Add("Col1_Total", typeof(decimal));
            table.Columns.Add("Col2_Class1", typeof(decimal));
            table.Columns.Add("Col3_Class2", typeof(decimal));
            table.Columns.Add("Col4_Class3", typeof(decimal));
            table.Columns.Add("Col5_Class4", typeof(decimal));
            table.Columns.Add("Col6_Class5", typeof(decimal));
            table.Columns.Add("Col7_Class6", typeof(decimal));
            table.Columns.Add("Col8_Class7", typeof(decimal));
            table.Columns.Add("Col9_Class8", typeof(decimal));
            
            // 16行の項目定義
            var rowDefinitions = new[]
            {
                "【日計】 現金売上",
                "現売消費税",
                "掛売上＋売上返品",
                "売上値引",
                "掛売消費税",
                "現金仕入",
                "現仕消費税",
                "掛仕入＋仕入返品",
                "仕入値引",
                "掛仕入消費税",
                "現金・小切手・手形入金",
                "振込入金",
                "入金値引・その他入金",
                "現金・小切手・手形支払",
                "振込支払",
                "支払値引・その他支払"
            };
            
            // 分類別に集計（分類コード001～008）
            var itemsByClass = items.Where(x => !string.IsNullOrEmpty(x.ClassificationCode))
                                   .GroupBy(x => x.ClassificationCode)
                                   .ToDictionary(g => g.Key, g => g.First());
            
            // 合計を計算（分類000または全体合計）
            var totalItem = items.FirstOrDefault(x => x.ClassificationCode == "000");
            if (totalItem == null)
            {
                // 合計がない場合は各分類の合計を計算
                totalItem = new BusinessDailyReportItem
                {
                    ClassificationCode = "000",
                    DailyCashSales = items.Sum(x => x.DailyCashSales),
                    DailyCashSalesTax = items.Sum(x => x.DailyCashSalesTax),
                    DailyCreditSales = items.Sum(x => x.DailyCreditSales),
                    DailySalesDiscount = items.Sum(x => x.DailySalesDiscount),
                    DailyCreditSalesTax = items.Sum(x => x.DailyCreditSalesTax),
                    DailyCashPurchase = items.Sum(x => x.DailyCashPurchase),
                    DailyCashPurchaseTax = items.Sum(x => x.DailyCashPurchaseTax),
                    DailyCreditPurchase = items.Sum(x => x.DailyCreditPurchase),
                    DailyPurchaseDiscount = items.Sum(x => x.DailyPurchaseDiscount),
                    DailyCreditPurchaseTax = items.Sum(x => x.DailyCreditPurchaseTax),
                    DailyCashReceipt = items.Sum(x => x.DailyCashReceipt),
                    DailyBankReceipt = items.Sum(x => x.DailyBankReceipt),
                    DailyOtherReceipt = items.Sum(x => x.DailyOtherReceipt),
                    DailyCashPayment = items.Sum(x => x.DailyCashPayment),
                    DailyBankPayment = items.Sum(x => x.DailyBankPayment),
                    DailyOtherPayment = items.Sum(x => x.DailyOtherPayment)
                };
            }
            
            // 16行のデータを作成
            for (int i = 0; i < 16; i++)
            {
                var row = table.NewRow();
                row["RowNumber"] = i + 1;
                row["ItemName"] = rowDefinitions[i];
                
                // 合計列
                row["Col1_Total"] = GetValueByRowIndex(totalItem, i);
                
                // 分類001～008のデータ
                for (int classIndex = 1; classIndex <= 8; classIndex++)
                {
                    var classCode = classIndex.ToString("D3");  // 001, 002, ... 008
                    if (itemsByClass.ContainsKey(classCode))
                    {
                        row[$"Col{classIndex + 1}_Class{classIndex}"] = GetValueByRowIndex(itemsByClass[classCode], i);
                    }
                    else
                    {
                        row[$"Col{classIndex + 1}_Class{classIndex}"] = 0m;
                    }
                }
                
                table.Rows.Add(row);
            }
            
            _logger.LogInformation($"データ展開完了: {table.Rows.Count}行のデータを作成しました");
            return table;
        }

        private decimal GetValueByRowIndex(BusinessDailyReportItem item, int rowIndex)
        {
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
    }
}

#endif
#pragma warning restore CA1416