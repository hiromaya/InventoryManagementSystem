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
                
                // DataTableを作成
                var dataTable = new DataTable("UnmatchItems");
                dataTable.Columns.Add("Category", typeof(string));
                dataTable.Columns.Add("CustomerCode", typeof(string));
                dataTable.Columns.Add("CustomerName", typeof(string));
                dataTable.Columns.Add("ProductCode", typeof(string));
                dataTable.Columns.Add("ProductName", typeof(string));
                dataTable.Columns.Add("ShippingMarkCode", typeof(string));
                dataTable.Columns.Add("ShippingMarkName", typeof(string));
                dataTable.Columns.Add("GradeCode", typeof(string));
                dataTable.Columns.Add("GradeName", typeof(string));
                dataTable.Columns.Add("ClassCode", typeof(string));
                dataTable.Columns.Add("ClassName", typeof(string));
                dataTable.Columns.Add("Quantity", typeof(decimal));
                dataTable.Columns.Add("UnitPrice", typeof(decimal));
                dataTable.Columns.Add("Amount", typeof(decimal));
                dataTable.Columns.Add("VoucherNumber", typeof(string));
                dataTable.Columns.Add("AlertType", typeof(string));
                
                // データを追加
                foreach (var item in unmatchList)
                {
                    dataTable.Rows.Add(
                        GetCategoryName(item.Category),
                        item.CustomerCode ?? "",
                        item.CustomerName ?? "",
                        item.Key.ProductCode ?? "",
                        item.ProductName ?? "",
                        item.Key.ShippingMarkCode ?? "",
                        item.Key.ShippingMarkName ?? "",
                        item.Key.GradeCode ?? "",
                        item.GradeName ?? "",
                        item.Key.ClassCode ?? "",
                        item.ClassName ?? "",
                        item.Quantity,
                        item.UnitPrice,
                        item.Amount,
                        item.VoucherNumber ?? "",
                        item.AlertType ?? ""
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
                
                // レポートを準備（スクリプトは使用しない）
                _logger.LogInformation("レポートを生成しています...");
                report.Prepare();
                
                // 準備後にプレースホルダーを更新
                _logger.LogInformation("レポート情報を更新しています...");
                UpdateReportPlaceholders(report, jobDate, dataTable.Rows.Count);
                
                // PDF出力設定
                using var pdfExport = new PDFExport
                {
                    // 日本語フォントの埋め込み
                    EmbeddingFonts = true,
                    
                    // PDFのメタデータ
                    Title = $"アンマッチリスト_{jobDate:yyyyMMdd}",
                    Subject = "アンマッチリスト",
                    Creator = "在庫管理システム",
                    Author = "在庫管理システム",
                    
                    // PDF/A準拠（長期保存用）
                    PdfCompliance = PDFExport.PdfStandard.PdfA_2a,
                    
                    // 画質設定
                    JpegQuality = 95,
                    
                    // セキュリティ設定なし（内部文書のため）
                    OpenAfterExport = false
                };
                
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
        /// レポートのプレースホルダーを実際の値に置換
        /// </summary>
        private void UpdateReportPlaceholders(FR.Report report, DateTime jobDate, int totalCount)
        {
            var createDateText = DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒");
            var jobDateText = jobDate.ToString("yyyy年MM月dd日");
            var totalCountText = totalCount.ToString("0000");
            
            // 準備されたページを処理
            int pageCount = report.PreparedPages.Count;
            
            for (int i = 0; i < pageCount; i++)
            {
                // PreparedPagesからページを取得
                var pageObject = report.PreparedPages.GetPage(i);
                if (!(pageObject is FR.ReportPage page)) continue;
                
                // ページ番号テキスト
                var pageNumberText = $"{(i + 1):0000} / {pageCount:0000} 頁";
                
                // 各TextObjectを検索して更新
                UpdateTextObject(page, "CreateDate", $"作成日：{createDateText}");
                UpdateTextObject(page, "PageNumber", pageNumberText);
                UpdateTextObject(page, "Title", $"※　{jobDateText}　アンマッチリスト　※");
                
                // 最終ページのみサマリーを更新
                if (i == pageCount - 1)
                {
                    UpdateTextObject(page, "SummaryText", $"アンマッチ件数＝{totalCountText}");
                }
            }
        }
        
        /// <summary>
        /// TextObjectのテキストを更新
        /// </summary>
        private void UpdateTextObject(FR.ReportPage page, string objectName, string newText)
        {
            var textObject = page.FindObject(objectName) as FR.TextObject;
            if (textObject != null)
            {
                textObject.Text = newText;
            }
            else
            {
                _logger.LogWarning($"TextObject '{objectName}' が見つかりません");
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