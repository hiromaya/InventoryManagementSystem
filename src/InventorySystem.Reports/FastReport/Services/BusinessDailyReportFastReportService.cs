#pragma warning disable CA1416
#if WINDOWS

using FastReport;
using FastReport.Export.Pdf;
using FastReport.Utils;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Drawing;
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

                // .NET 8対応: スクリプト機能を完全に無効化
                DisableFastReportScripting(report);

                // テンプレートを静的レイアウトに変換
                _logger.LogInformation("テンプレートを静的レイアウトに変換しています...");
                ConvertToStaticLayout(report);

                // データを直接設定（16行×9列）
                _logger.LogInformation("データを16行×9列で設定しています...");
                var dataTable = CreateStaticReportData(items);
                SetReportData(report, dataTable);

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

        private void DisableFastReportScripting(Report report)
        {
            try
            {
                _logger.LogInformation("FastReportのスクリプト機能を無効化します");
                
                // 0. ScriptLanguageを強制的にNoneに設定（最重要）
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
                        else
                        {
                            _logger.LogWarning("ScriptLanguage.Noneが見つかりません");
                        }
                    }
                }
                
                // 1. ScriptTextプロパティをクリア
                var scriptTextProperty = report.GetType().GetProperty("ScriptText");
                if (scriptTextProperty != null)
                {
                    scriptTextProperty.SetValue(report, string.Empty);
                    _logger.LogInformation("ScriptTextをクリアしました");
                }
                
                // 2. 非公開のScriptプロパティをnullに設定（最重要）
                var scriptProperty = report.GetType().GetProperty("Script", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                    _logger.LogInformation("Scriptプロパティをnullに設定しました");
                }
                
                // 3. ReportScriptプロパティをnullに設定
                var reportScriptProperty = report.GetType().GetProperty("ReportScript", 
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (reportScriptProperty != null)
                {
                    reportScriptProperty.SetValue(report, null);
                    _logger.LogInformation("ReportScriptをnullに設定しました");
                }
                
                // 4. ScriptRestrictions プロパティを設定（存在する場合）
                var scriptRestrictionsProperty = report.GetType().GetProperty("ScriptRestrictions");
                if (scriptRestrictionsProperty != null)
                {
                    try
                    {
                        // DontRunという値を設定（列挙型の場合）
                        var restrictionsType = scriptRestrictionsProperty.PropertyType;
                        if (restrictionsType.IsEnum)
                        {
                            var dontRunValue = Enum.GetValues(restrictionsType)
                                .Cast<object>()
                                .FirstOrDefault(v => v.ToString().Contains("DontRun"));
                            if (dontRunValue != null)
                            {
                                scriptRestrictionsProperty.SetValue(report, dontRunValue);
                                _logger.LogInformation("ScriptRestrictionsをDontRunに設定しました");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"ScriptRestrictions設定時の例外: {ex.Message}");
                    }
                }
                
                // 5. AllowExpressions プロパティを false に設定
                var allowExpressionsProperty = report.GetType().GetProperty("AllowExpressions");
                if (allowExpressionsProperty != null)
                {
                    allowExpressionsProperty.SetValue(report, false);
                    _logger.LogInformation("AllowExpressionsをfalseに設定しました");
                }
                
                _logger.LogInformation("FastReportのスクリプト機能の無効化が完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"スクリプト無効化時の警告: {ex.Message}");
                // エラーが発生しても処理を継続
            }
        }

        private void ConvertToStaticLayout(Report report)
        {
            try
            {
                _logger.LogInformation("テンプレートの静的レイアウト変換を開始します");
                
                // ReportPageを取得
                var page = report.Pages[0] as ReportPage;
                if (page != null)
                {
                    // DataBandを検出して削除
                    var dataBands = page.AllObjects.OfType<DataBand>().ToList();
                    foreach (var dataBand in dataBands)
                    {
                        _logger.LogInformation($"DataBandを削除しました: {dataBand.Name}");
                        // DataBandを削除（親から取り除く）
                        dataBand.Dispose();
                    }
                    
                    // DataBand削除後、すべてのTextObjectから式を削除
                    var textObjects = page.AllObjects.OfType<TextObject>().ToList();
                    foreach (var textObj in textObjects)
                    {
                        if (textObj.Text != null && textObj.Text.Contains("["))
                        {
                            _logger.LogDebug($"TextObject '{textObj.Name}' の式をクリアします: {textObj.Text}");
                            // 式を削除して、静的なテキストまたは空にする
                            textObj.Text = "";
                        }
                    }
                    
                    // ReportSummaryBandがない場合は作成
                    var summaryBand = page.AllObjects.OfType<ReportSummaryBand>().FirstOrDefault();
                    if (summaryBand == null)
                    {
                        summaryBand = new ReportSummaryBand();
                        summaryBand.Name = "ReportSummary1";
                        summaryBand.Height = 302.4f; // 16行×18.9mm
                        summaryBand.Parent = page;
                        _logger.LogInformation("ReportSummaryBandを作成しました");
                    }
                    
                    // 16行×9列のTextObjectを作成
                    CreateStaticTextObjects(summaryBand);
                }
                
                _logger.LogInformation("テンプレートの静的レイアウト変換が完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"静的レイアウト変換時の警告: {ex.Message}");
            }
        }

        private void CreateStaticTextObjects(ReportSummaryBand summaryBand)
        {
            var itemNames = new string[]
            {
                "【日計】 現金売上", "現売消費税", "掛売上＋売上返品", "売上値引",
                "掛売消費税", "現金仕入", "現仕消費税", "掛仕入＋仕入返品",
                "仕入値引", "掛仕入消費税", "現金・小切手・手形入金", "振込入金",
                "入金値引・その他入金", "現金・小切手・手形支払", "振込支払", "支払値引・その他支払"
            };
            
            float rowHeight = 18.9f; // 1行の高さ（mm単位）
            float colWidth = 20f;    // 1列の幅（mm単位）
            float labelWidth = 40f;  // 項目名の幅（mm単位）
            
            for (int row = 0; row < 16; row++)
            {
                float top = row * rowHeight;
                string rowNo = (row + 1).ToString("00");
                
                // 項目名
                var labelObj = new TextObject
                {
                    Name = $"Row{rowNo}_Label",
                    Left = 0,
                    Top = top,
                    Width = labelWidth,
                    Height = rowHeight,
                    Text = itemNames[row],  // 静的な値
                    Font = new Font("ＭＳ ゴシック", 9),
                    // 式評価を無効化
                    AllowExpressions = false,
                    Brackets = ""
                };
                labelObj.Parent = summaryBand;
                
                // 合計
                var totalObj = new TextObject
                {
                    Name = $"Row{rowNo}_Total",
                    Left = labelWidth,
                    Top = top,
                    Width = colWidth,
                    Height = rowHeight,
                    Text = "0",  // 静的な値
                    Font = new Font("ＭＳ ゴシック", 9),
                    HorzAlign = HorzAlign.Right,
                    // 式評価を無効化
                    AllowExpressions = false,
                    Brackets = ""
                };
                totalObj.Border.Lines = BorderLines.All;
                totalObj.Border.Width = 0.5f;
                totalObj.Parent = summaryBand;
                
                // 8分類
                for (int col = 1; col <= 8; col++)
                {
                    var classObj = new TextObject
                    {
                        Name = $"Row{rowNo}_Class{col:00}",
                        Left = labelWidth + col * colWidth,
                        Top = top,
                        Width = colWidth,
                        Height = rowHeight,
                        Text = "0",  // 静的な値
                        Font = new Font("ＭＳ ゴシック", 9),
                        HorzAlign = HorzAlign.Right,
                        // 式評価を無効化
                        AllowExpressions = false,
                        Brackets = ""
                    };
                    classObj.Border.Lines = BorderLines.All;
                    classObj.Border.Width = 0.5f;
                    classObj.Parent = summaryBand;
                }
            }
            
            _logger.LogInformation($"静的TextObjectを作成しました: {16 * 9}個");
        }

        private DataTable CreateStaticReportData(IEnumerable<BusinessDailyReportItem> items)
        {
            var dt = new DataTable("BusinessDailyReport");
            
            // カラム定義
            dt.Columns.Add("RowNo", typeof(int));
            dt.Columns.Add("ItemName", typeof(string));
            dt.Columns.Add("Total", typeof(decimal));
            for (int i = 1; i <= 8; i++)
            {
                dt.Columns.Add($"Class{i:00}", typeof(decimal));
            }
            
            var itemNames = new string[]
            {
                "【日計】 現金売上", "現売消費税", "掛売上＋売上返品", "売上値引",
                "掛売消費税", "現金仕入", "現仕消費税", "掛仕入＋仕入返品",
                "仕入値引", "掛仕入消費税", "現金・小切手・手形入金", "振込入金",
                "入金値引・その他入金", "現金・小切手・手形支払", "振込支払", "支払値引・その他支払"
            };
            
            // 分類別に集計
            var itemsByClass = items.Where(x => !string.IsNullOrEmpty(x.ClassificationCode))
                                   .GroupBy(x => x.ClassificationCode)
                                   .ToDictionary(g => g.Key, g => g.First());
            
            // 合計を計算
            var totalItem = items.FirstOrDefault(x => x.ClassificationCode == "000") ?? new BusinessDailyReportItem
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
            
            // 16行のデータを作成
            for (int row = 0; row < 16; row++)
            {
                var dr = dt.NewRow();
                dr["RowNo"] = row + 1;
                dr["ItemName"] = itemNames[row];
                dr["Total"] = GetValueByRowIndex(totalItem, row);
                
                // 8分類のデータ
                for (int col = 1; col <= 8; col++)
                {
                    var classCode = col.ToString("D3");
                    if (itemsByClass.ContainsKey(classCode))
                    {
                        dr[$"Class{col:00}"] = GetValueByRowIndex(itemsByClass[classCode], row);
                    }
                    else
                    {
                        dr[$"Class{col:00}"] = 0m;
                    }
                }
                
                dt.Rows.Add(dr);
            }
            
            _logger.LogInformation($"静的レポートデータを作成しました: {dt.Rows.Count}行");
            return dt;
        }

        private void SetReportData(Report report, DataTable dataTable)
        {
            try
            {
                var page = report.Pages[0] as ReportPage;
                if (page == null) return;
                
                _logger.LogInformation("レポートデータを設定しています...");
                
                // 16行分のデータを設定
                for (int row = 0; row < 16; row++)
                {
                    var dataRow = dataTable.Rows[row];
                    var rowNo = (row + 1).ToString("00");
                    
                    // 合計
                    var totalObj = FindTextObject(page, $"Row{rowNo}_Total");
                    if (totalObj != null)
                    {
                        totalObj.Text = FormatNumber((decimal)dataRow["Total"]);
                    }
                    
                    // 8分類
                    for (int col = 1; col <= 8; col++)
                    {
                        var classObj = FindTextObject(page, $"Row{rowNo}_Class{col:00}");
                        if (classObj != null)
                        {
                            classObj.Text = FormatNumber((decimal)dataRow[$"Class{col:00}"]);
                        }
                    }
                }
                
                _logger.LogInformation("レポートデータの設定が完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"レポートデータ設定時の警告: {ex.Message}");
            }
        }

        private TextObject FindTextObject(ReportPage page, string objectName)
        {
            var textObj = page.FindObject(objectName) as TextObject;
            if (textObj == null)
            {
                // ReportSummaryBand内も検索
                var summaryBand = page.AllObjects.OfType<ReportSummaryBand>().FirstOrDefault();
                if (summaryBand != null)
                {
                    textObj = summaryBand.AllObjects
                        .OfType<TextObject>()
                        .FirstOrDefault(t => t.Name == objectName);
                }
            }

            if (textObj == null)
            {
                _logger.LogDebug($"TextObject '{objectName}' が見つかりません");
            }
            
            return textObj;
        }

        private string FormatNumber(decimal value)
        {
            if (value == 0) return "0";
            if (value < 0) return $"▲{Math.Abs(value):N0}";
            return value.ToString("N0");
        }
    }
}

#endif
#pragma warning restore CA1416