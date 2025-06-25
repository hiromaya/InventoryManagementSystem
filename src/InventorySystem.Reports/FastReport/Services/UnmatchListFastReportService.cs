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
using InventorySystem.Core.Entities;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    public class UnmatchListFastReportService : IUnmatchListReportService
    {
        private readonly ILogger<UnmatchListFastReportService> _logger;
        private readonly string _templatePath;
        
        public UnmatchListFastReportService(ILogger<UnmatchListFastReportService> logger)
        {
            _logger = logger;
            
            // テンプレートファイルのパス設定
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "UnmatchListReport.frx");
            
            _logger.LogInformation("テンプレートパス: {Path}", _templatePath);
        }
        
        public byte[] GenerateUnmatchListReport(IEnumerable<UnmatchItem> unmatchItems, DateTime jobDate)
        {
            try
            {
                // テンプレートファイルの存在確認
                if (!File.Exists(_templatePath))
                {
                    var errorMessage = $"レポートテンプレートが見つかりません: {_templatePath}";
                    _logger.LogError(errorMessage);
                    throw new FileNotFoundException(errorMessage, _templatePath);
                }
                
                using var report = new FR.Report();
                
                // FastReportの設定
                report.ReportResourceString = "";  // リソース文字列をクリア
                report.FileName = _templatePath;   // ファイル名を設定
                
                // テンプレートファイルを読み込む
                _logger.LogInformation("レポートテンプレートを読み込んでいます...");
                report.Load(_templatePath);
                
                // .NET 8対応: ScriptLanguageを強制的にNoneに設定
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
                
                // データソースの準備
                var unmatchList = unmatchItems.ToList();
                _logger.LogDebug("PDF生成: アンマッチ項目数={Count}", unmatchList.Count);
                
                // 最初の5件の文字列状態を確認
                foreach (var (item, index) in unmatchList.Take(5).Select((i, idx) => (i, idx)))
                {
                    _logger.LogDebug("PDF生成 行{Index}: 得意先名='{CustomerName}', 商品名='{ProductName}', 荷印名='{ShippingMarkName}'", 
                        index + 1, item.CustomerName, item.ProductName, item.Key.ShippingMarkName);
                    
                    if (!string.IsNullOrEmpty(item.CustomerName))
                    {
                        _logger.LogDebug("PDF生成 得意先名バイト列: {Bytes}", BitConverter.ToString(Encoding.UTF8.GetBytes(item.CustomerName)));
                    }
                }
                
                // DataTableを作成
                var dataTable = new DataTable("UnmatchItems");
                dataTable.Columns.Add("Category", typeof(string));
                dataTable.Columns.Add("CustomerCode", typeof(string));
                dataTable.Columns.Add("CustomerName", typeof(string));
                dataTable.Columns.Add("ProductCode", typeof(string));
                dataTable.Columns.Add("ProductName", typeof(string));
                dataTable.Columns.Add("ShippingMarkCode", typeof(string));
                dataTable.Columns.Add("ShippingMarkName", typeof(string));
                dataTable.Columns.Add("ManualInput", typeof(string));
                dataTable.Columns.Add("GradeCode", typeof(string));
                dataTable.Columns.Add("GradeName", typeof(string));
                dataTable.Columns.Add("ClassCode", typeof(string));
                dataTable.Columns.Add("ClassName", typeof(string));
                dataTable.Columns.Add("Quantity", typeof(decimal));
                dataTable.Columns.Add("UnitPrice", typeof(decimal));
                dataTable.Columns.Add("Amount", typeof(decimal));
                dataTable.Columns.Add("VoucherNumber", typeof(string));
                dataTable.Columns.Add("AlertType", typeof(string));
                dataTable.Columns.Add("AlertType2", typeof(string));
                
                // データを追加
                foreach (var (item, index) in unmatchList.Select((i, idx) => (i, idx)))
                {
                    var categoryName = GetCategoryName(item.Category);
                    var customerCode = item.CustomerCode ?? "";
                    var customerName = item.CustomerName ?? "";
                    var productCode = item.Key.ProductCode ?? "";
                    var productName = item.ProductName ?? "";
                    var shippingMarkCode = item.Key.ShippingMarkCode ?? "";
                    var shippingMarkName = item.Key.ShippingMarkName ?? "";
                    
                    // デバッグログ追加（文字化け調査用）
                    _logger.LogDebug("DataTable追加前: カテゴリ={Category}, 商品名={ProductName}, 荷印名={ShippingMarkName}", 
                        categoryName, 
                        productName ?? "(null)", 
                        shippingMarkName ?? "(null)");
                    
                    // 文字列のバイト表現を確認（文字化け調査用）
                    if (!string.IsNullOrEmpty(productName))
                    {
                        var bytes = Encoding.UTF8.GetBytes(productName);
                        _logger.LogDebug("商品名バイト列: {Bytes}", BitConverter.ToString(bytes));
                    }
                    
                    if (!string.IsNullOrEmpty(customerName))
                    {
                        var bytes = Encoding.UTF8.GetBytes(customerName);
                        _logger.LogDebug("得意先名バイト列: {Bytes}", BitConverter.ToString(bytes));
                    }
                    
                    if (!string.IsNullOrEmpty(shippingMarkName))
                    {
                        var bytes = Encoding.UTF8.GetBytes(shippingMarkName);
                        _logger.LogDebug("荷印名バイト列: {Bytes}", BitConverter.ToString(bytes));
                    }
                    
                    dataTable.Rows.Add(
                        categoryName,
                        customerCode,
                        customerName,
                        productCode,
                        productName,
                        shippingMarkCode,
                        shippingMarkName,
                        shippingMarkName,  // ManualInput - 荷印名と同じ値
                        item.Key.GradeCode ?? "",
                        item.GradeName ?? "",
                        item.Key.ClassCode ?? "",
                        item.ClassName ?? "",
                        item.Quantity,
                        item.UnitPrice,
                        item.Amount,
                        item.VoucherNumber ?? "",
                        item.AlertType ?? "",
                        item.AlertType2 ?? ""
                    );
                }
                
                _logger.LogInformation("データソースを登録しています。件数: {Count}", dataTable.Rows.Count);
                
                // DataTableとして登録
                report.RegisterData(dataTable, "UnmatchItems");
                
                // データソースを明示的に取得して設定
                var dataSource = report.GetDataSource("UnmatchItems");
                if (dataSource != null)
                {
                    dataSource.Enabled = true;
                    _logger.LogInformation("データソースを有効化しました");
                }
                else
                {
                    _logger.LogWarning("データソース 'UnmatchItems' が見つかりません");
                }
                
                // レポートパラメータを設定
                _logger.LogInformation("レポートパラメータを設定しています...");
                report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒"));
                report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("TotalCount", dataTable.Rows.Count.ToString("0000"));
                
                // デバッグ: フォント情報とデータ確認
                _logger.LogDebug("使用フォント: {FontName}", "ＭＳ ゴシック");
                
                // データ確認（最初の3件）
                foreach (var (item, idx) in unmatchList.Take(3).Select((i, index) => (i, index)))
                {
                    _logger.LogDebug("PDF用データ確認 {Index} - 商品名: {Name}, 長さ: {Length}, バイト: {Bytes}", 
                        idx + 1,
                        item.ProductName,
                        item.ProductName?.Length ?? 0,
                        item.ProductName != null ? BitConverter.ToString(Encoding.UTF8.GetBytes(item.ProductName)) : "null");
                }
                
                // レポートを準備（スクリプトは使用しない）
                _logger.LogInformation("レポートを生成しています...");
                report.Prepare();
                
                // PDF出力設定
                using var pdfExport = new PDFExport
                {
                    // 日本語フォントの埋め込み（重要）
                    EmbeddingFonts = true,
                    
                    // PDFのメタデータ（エンコーディング確認）
                    Title = $"アンマッチリスト_{jobDate:yyyyMMdd}",
                    Subject = "アンマッチリスト",
                    Creator = "在庫管理システム",
                    Author = "在庫管理システム",
                    
                    // 文字エンコーディング設定
                    TextInCurves = false,  // テキストをパスに変換しない
                    
                    // TrueTypeフォントを使用
                    UseFileCache = true,
                    
                    // 画質設定
                    JpegQuality = 95,
                    
                    // セキュリティ設定なし（内部文書のため）
                    OpenAfterExport = false
                };
                
                _logger.LogDebug("PDFExport設定: EmbeddingFonts={EmbeddingFonts}, TextInCurves={TextInCurves}, UseFileCache={UseFileCache}",
                    pdfExport.EmbeddingFonts, pdfExport.TextInCurves, pdfExport.UseFileCache);
                
                // PDFをメモリストリームに出力
                using var stream = new MemoryStream();
                report.Export(pdfExport, stream);
                
                var pdfBytes = stream.ToArray();
                _logger.LogInformation("PDF生成完了。サイズ: {Size} bytes", pdfBytes.Length);
                
                return pdfBytes;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "テンプレートファイルが見つかりません");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "アンマッチリストの生成中にエラーが発生しました");
                throw new InvalidOperationException("アンマッチリストPDFの生成に失敗しました", ex);
            }
        }
        
        
        /// <summary>
        /// カテゴリコードを日本語名に変換
        /// </summary>
        private string GetCategoryName(string category)
        {
            return category switch
            {
                "11" => "掛売上",
                "12" => "現金売上",
                "21" => "掛仕入",
                "22" => "現金仕入",
                "71" => "在庫調整",
                "04" => "振替",
                "05" => "加工費",
                _ => category
            };
        }
    }
}
#else
namespace InventorySystem.Reports.FastReport.Services
{
    // Linux環境用のプレースホルダークラス
    public class UnmatchListFastReportService
    {
        public UnmatchListFastReportService(object logger) { }
    }
}
#endif