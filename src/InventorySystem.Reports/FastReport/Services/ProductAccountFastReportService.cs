#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using FastReport;
using FastReport.Export.Pdf;
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.Models;
using Microsoft.Extensions.Logging;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    public class ProductAccountFastReportService : IProductAccountReportService
    {
        private readonly ILogger<ProductAccountFastReportService> _logger;
        private readonly string _templatePath;
        
        public ProductAccountFastReportService(ILogger<ProductAccountFastReportService> logger)
        {
            _logger = logger;
            
            // テンプレートファイルのパス設定
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "ProductAccount.frx");
            
            _logger.LogInformation("商品勘定テンプレートパス: {Path}", _templatePath);
        }
        
        public byte[] GenerateProductAccountReport(DateTime jobDate, string? departmentCode = null)
        {
            try
            {
                // ===== FastReport診断情報 開始 =====
                _logger.LogInformation("=== 商品勘定帳票 FastReport Service Diagnostics ===");
                _logger.LogInformation("商品勘定帳票 service is being executed");
                _logger.LogInformation($"Job date: {jobDate:yyyy-MM-dd}");
                _logger.LogInformation($"Department code: {departmentCode ?? "全部門"}");

                // FastReportのバージョン情報を取得
                try
                {
                    var fastReportAssembly = typeof(FR.Report).Assembly;
                    _logger.LogInformation($"FastReport Version: {fastReportAssembly.GetName().Version}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get FastReport version");
                }
                // ===== FastReport診断情報 終了 =====
                
                // テンプレートファイルの存在確認
                if (!File.Exists(_templatePath))
                {
                    var errorMessage = $"商品勘定レポートテンプレートが見つかりません: {_templatePath}";
                    _logger.LogError(errorMessage);
                    throw new FileNotFoundException(errorMessage, _templatePath);
                }
                
                // 商品勘定データを取得（一時的にダミーデータで実装）
                var reportData = GetProductAccountData(jobDate, departmentCode);
                
                using var report = new FR.Report();
                
                // FastReportの設定（アンマッチリストと同じ）
                report.ReportResourceString = "";  // リソース文字列をクリア
                report.FileName = _templatePath;   // ファイル名を設定
                
                // テンプレートファイルを読み込む
                _logger.LogInformation("商品勘定レポートテンプレートを読み込んでいます...");
                report.Load(_templatePath);
                
                // .NET 8対応: ScriptLanguageを強制的にNoneに設定（アンマッチリストと同じ方法）
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
                            var noneValue = Enum.GetValues(scriptLanguageType).Cast<object>().FirstOrDefault(v => v.ToString() == "None");
                            if (noneValue != null)
                            {
                                scriptLanguageProperty.SetValue(report, noneValue);
                                _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
                    // エラーが発生しても処理を継続
                }
                
                // PDF生成処理
                var pdfBytes = GeneratePdfReport(reportData, jobDate);
                
                _logger.LogInformation("商品勘定帳票PDF生成完了。サイズ: {Size} bytes", pdfBytes.Length);
                
                return pdfBytes;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "商品勘定テンプレートファイルが見つかりません");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "商品勘定帳票の生成中にエラーが発生しました");
                throw new InvalidOperationException("商品勘定帳票PDFの生成に失敗しました", ex);
            }
        }
        
        /// <summary>
        /// 商品勘定データの取得（一時的な実装）
        /// </summary>
        private IList<ProductAccountReportModel> GetProductAccountData(DateTime jobDate, string? departmentCode)
        {
            // 実際の実装では、ストアドプロシージャsp_CreateProductLedgerDataから取得
            // ここではテスト用のダミーデータを返す
            var data = new List<ProductAccountReportModel>();
            
            // サンプルデータを3件作成
            for (int i = 1; i <= 3; i++)
            {
                var item = new ProductAccountReportModel
                {
                    ProductCode = $"P{i:D4}",
                    ProductName = $"テスト商品{i}",
                    ShippingMarkCode = $"S{i:D3}",
                    ShippingMarkName = $"テスト荷印{i}",
                    ManualShippingMark = $"MAN{i:D3}  ",
                    GradeCode = $"G{i:D2}",
                    GradeName = $"等級{i}",
                    ClassCode = $"C{i:D2}",
                    ClassName = $"階級{i}",
                    VoucherNumber = $"V{i:D6}",
                    VoucherCategory = i % 2 == 0 ? "51" : "11",
                    RecordType = i == 1 ? "Previous" : (i % 2 == 0 ? "Sales" : "Purchase"),
                    TransactionDate = jobDate,
                    PurchaseQuantity = i % 2 == 1 ? 100.50m : 0,
                    SalesQuantity = i % 2 == 0 ? 80.25m : 0,
                    RemainingQuantity = 120.75m - (i * 10),
                    UnitPrice = 1500.50m + (i * 100),
                    Amount = 180750m + (i * 10000),
                    GrossProfit = i % 3 == 0 ? -15000m : 25000m + (i * 5000),
                    CustomerSupplierName = $"取引先{i}株式会社",
                    GroupKey = $"P{i:D4}_S{i:D3}_G{i:D2}_C{i:D2}"
                };
                
                // DisplayCategoryを設定
                item.DisplayCategory = GetDisplayCategory(item.VoucherCategory, item.RecordType);
                
                data.Add(item);
            }
            
            _logger.LogInformation("商品勘定テストデータを生成しました。件数: {Count}", data.Count);
            
            return data;
        }
        
        /// <summary>
        /// PDF生成処理（アンマッチリストのパターンを踏襲）
        /// </summary>
        private byte[] GeneratePdfReport(IEnumerable<ProductAccountReportModel> reportData, DateTime jobDate)
        {
            using var report = new FR.Report();
            
            // FastReportの設定（アンマッチリストと同じ）
            report.ReportResourceString = "";
            report.FileName = _templatePath;
            
            // テンプレート読込
            report.Load(_templatePath);
            
            // スクリプト無効化（アンマッチリストと完全に同じ方法）
            try
            {
                var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                if (scriptLanguageProperty != null)
                {
                    var scriptLanguageType = scriptLanguageProperty.PropertyType;
                    if (scriptLanguageType.IsEnum)
                    {
                        var noneValue = Enum.GetValues(scriptLanguageType).Cast<object>()
                            .FirstOrDefault(v => v.ToString() == "None");
                        if (noneValue != null)
                        {
                            scriptLanguageProperty.SetValue(report, noneValue);
                            _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
                // エラーが発生しても処理を継続
            }
            
            // データ設定
            var dataTable = CreateDataTable(reportData);
            report.RegisterData(dataTable, "ProductAccount");
            
            // データソース検証
            var registeredDataSource = report.GetDataSource("ProductAccount");
            if (registeredDataSource != null)
            {
                _logger.LogInformation("データソース登録確認 OK: {Name}", registeredDataSource.Name);
                _logger.LogInformation("データソース行数確認: {Count}", registeredDataSource.RowCount);
                registeredDataSource.Enabled = true;
            }
            else
            {
                _logger.LogError("データソース登録失敗: 'ProductAccount' が見つかりません");
                throw new InvalidOperationException("データソースの登録に失敗しました");
            }
            
            // レポートパラメータを設定
            _logger.LogInformation("レポートパラメータを設定しています...");
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
            report.SetParameterValue("TotalCount", dataTable.Rows.Count.ToString("0000"));
            
            // レポートを準備
            _logger.LogInformation("レポートを生成しています...");
            report.Prepare();
            
            // PDF出力設定（アンマッチリストと同じ設定）
            using var pdfExport = new PDFExport
            {
                // 日本語フォントの埋め込み（重要）
                EmbeddingFonts = true,
                
                // PDFのメタデータ
                Title = $"商品勘定_{jobDate:yyyyMMdd}",
                Subject = "商品勘定帳票",
                Creator = "在庫管理システム",
                Author = "在庫管理システム",
                
                // 文字エンコーディング設定
                TextInCurves = false,  // テキストをパスに変換しない
                
                // 画質設定
                JpegQuality = 95,
                
                // セキュリティ設定なし（内部文書のため）
                OpenAfterExport = false
            };
            
            // PDFをメモリストリームに出力
            using var stream = new MemoryStream();
            report.Export(pdfExport, stream);
            
            return stream.ToArray();
        }
        
        /// <summary>
        /// DataTable作成（文字列フィールドとして処理、フォーマット済み）
        /// </summary>
        private DataTable CreateDataTable(IEnumerable<ProductAccountReportModel> reportData)
        {
            var table = new DataTable("ProductAccount");
            
            // すべて文字列型で定義（フォーマット済み）
            table.Columns.Add("ProductName", typeof(string));
            table.Columns.Add("ShippingMarkName", typeof(string));
            table.Columns.Add("ManualShippingMark", typeof(string));
            table.Columns.Add("GradeName", typeof(string));
            table.Columns.Add("ClassName", typeof(string));
            table.Columns.Add("VoucherNumber", typeof(string));
            table.Columns.Add("DisplayCategory", typeof(string));
            table.Columns.Add("MonthDay", typeof(string));
            table.Columns.Add("PurchaseQuantity", typeof(string));
            table.Columns.Add("SalesQuantity", typeof(string));
            table.Columns.Add("RemainingQuantity", typeof(string));
            table.Columns.Add("UnitPrice", typeof(string));
            table.Columns.Add("Amount", typeof(string));
            table.Columns.Add("GrossProfit", typeof(string));
            table.Columns.Add("CustomerSupplierName", typeof(string));
            table.Columns.Add("GroupKey", typeof(string));  // グループ化用
            
            // データ追加時のフォーマット処理
            foreach (var item in reportData)
            {
                var row = table.NewRow();
                
                // 文字列フィールドはそのまま
                row["ProductName"] = item.ProductName;
                row["ShippingMarkName"] = item.ShippingMarkName;
                row["ManualShippingMark"] = item.ManualShippingMark;
                row["GradeName"] = item.GradeName;
                row["ClassName"] = item.ClassName;
                row["VoucherNumber"] = item.VoucherNumber;
                row["DisplayCategory"] = item.DisplayCategory;
                row["MonthDay"] = item.TransactionDate.ToString("MM/dd");
                row["CustomerSupplierName"] = item.CustomerSupplierName;
                row["GroupKey"] = item.GroupKey;
                
                // 数値フィールドのフォーマット
                row["PurchaseQuantity"] = FormatQuantity(item.PurchaseQuantity);
                row["SalesQuantity"] = FormatQuantity(item.SalesQuantity);
                row["RemainingQuantity"] = FormatQuantity(item.RemainingQuantity);
                row["UnitPrice"] = FormatUnitPrice(item.UnitPrice);
                row["Amount"] = FormatAmount(item.Amount);
                row["GrossProfit"] = FormatGrossProfit(item.GrossProfit);  // ▲処理含む
                
                table.Rows.Add(row);
            }
            
            return table;
        }
        
        /// <summary>
        /// 数量フォーマット（小数2桁、0の場合は空文字）
        /// </summary>
        private string FormatQuantity(decimal value)
        {
            return value == 0 ? "" : value.ToString("#,##0.00");
        }
        
        /// <summary>
        /// 単価フォーマット（小数2桁）
        /// </summary>
        private string FormatUnitPrice(decimal value)
        {
            return value == 0 ? "" : value.ToString("#,##0.00");
        }
        
        /// <summary>
        /// 金額フォーマット（整数、カンマ区切り）
        /// </summary>
        private string FormatAmount(decimal value)
        {
            return value == 0 ? "" : value.ToString("#,##0");
        }
        
        /// <summary>
        /// 粗利益フォーマット（負の値は▲記号）
        /// </summary>
        private string FormatGrossProfit(decimal value)
        {
            if (value == 0) return "";
            if (value < 0)
            {
                // 負の値は絶対値に▲を付ける
                return Math.Abs(value).ToString("#,##0") + "▲";
            }
            return value.ToString("#,##0");
        }
        
        /// <summary>
        /// 区分表示ルール実装
        /// </summary>
        private string GetDisplayCategory(string voucherCategory, string recordType)
        {
            // 前日残高
            if (recordType == "Previous") return "前残";
            
            // 伝票区分による表示
            return voucherCategory switch
            {
                "11" => "掛仕",
                "12" => "現仕",
                "51" => "掛売",
                "52" => "現売",
                "71" => GetAdjustmentDisplay(recordType),
                _ => voucherCategory
            };
        }
        
        /// <summary>
        /// 在庫調整の表示区分取得
        /// </summary>
        private string GetAdjustmentDisplay(string recordType)
        {
            return recordType switch
            {
                "Loss" => "ロス",
                "Spoilage" => "腐り", 
                "Transfer" => "振替",
                "Processing" => "加工",
                "Adjustment" => "調整",
                _ => "調整"
            };
        }
    }
}
#else
namespace InventorySystem.Reports.FastReport.Services
{
    // Linux環境用のプレースホルダークラス
    public class ProductAccountFastReportService
    {
        public ProductAccountFastReportService(object logger) { }
    }
}
#endif