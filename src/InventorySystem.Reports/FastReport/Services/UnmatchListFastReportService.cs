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
                
                // パラメータを設定
                report.SetParameterValue("JobDate", jobDate);
                
                // レポートを準備（スクリプトは使用しない）
                _logger.LogInformation("レポートを生成しています...");
                report.Prepare();
                
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