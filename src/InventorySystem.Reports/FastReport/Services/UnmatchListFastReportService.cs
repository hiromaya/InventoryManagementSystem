#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastReport;
using FastReport.Export.Pdf;
using InventorySystem.Core.Entities;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;

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
                
                using var report = new Report();
                
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
                
                // データを適切な形式に変換（UnmatchItemをそのまま使用）
                var reportData = unmatchList.Select(item => new
                {
                    Category = GetCategoryName(item.Category),
                    CustomerCode = item.CustomerCode ?? "",
                    CustomerName = item.CustomerName ?? "",
                    ProductCode = item.Key.ProductCode ?? "",
                    ProductName = item.ProductName ?? "",
                    ShippingMarkCode = item.Key.ShippingMarkCode ?? "",
                    ShippingMarkName = item.Key.ShippingMarkName ?? "",
                    GradeCode = item.Key.GradeCode ?? "",
                    GradeName = item.GradeName ?? "",
                    ClassCode = item.Key.ClassCode ?? "",
                    ClassName = item.ClassName ?? "",
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount,
                    VoucherNumber = item.VoucherNumber ?? "",
                    AlertType = item.AlertType ?? ""
                }).ToList();
                
                _logger.LogInformation("データソースを登録しています。件数: {Count}", reportData.Count);
                
                // データソースを登録
                report.RegisterData(reportData, "UnmatchItems");
                
                // レポートを準備（スクリプトは使用しない）
                _logger.LogInformation("レポートを生成しています...");
                report.Prepare();
                
                // 準備後にプレースホルダーを更新
                _logger.LogInformation("レポート情報を更新しています...");
                UpdateReportPlaceholders(report, jobDate, reportData.Count);
                
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
        private void UpdateReportPlaceholders(Report report, DateTime jobDate, int totalCount)
        {
            var createDateText = DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒");
            var jobDateText = jobDate.ToString("yyyy年MM月dd日");
            var totalCountText = totalCount.ToString("0000");
            
            // 準備されたページを処理
            int pageCount = report.PreparedPages.Count;
            
            for (int i = 0; i < pageCount; i++)
            {
                // PreparedPagesからページを取得
                if (!(report.PreparedPages.GetPage(i) is FastReport.ReportPage page)) continue;
                
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
        private void UpdateTextObject(ReportPage page, string objectName, string newText)
        {
            // AllObjectsを使用してオブジェクトを検索
            foreach (var obj in page.AllObjects)
            {
                if (obj.Name == objectName && obj is FastReport.TextObject textObject)
                {
                    textObject.Text = newText;
                    break;
                }
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