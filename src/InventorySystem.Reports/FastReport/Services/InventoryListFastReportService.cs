using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FastReport;
using FastReport.Export.PdfSimple;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Entities;

namespace InventorySystem.Reports.FastReport.Services
{
    /// <summary>
    /// 在庫表PDF生成サービス（Phase 1: 最小限実装）
    /// 目的：まずPDFが生成できることを確認する
    /// </summary>
    public class InventoryListFastReportService
    {
        private readonly ILogger<InventoryListFastReportService> _logger;
        private readonly ICpInventoryRepository _cpInventoryRepository;
        
        public InventoryListFastReportService(
            ILogger<InventoryListFastReportService> logger,
            ICpInventoryRepository cpInventoryRepository)
        {
            _logger = logger;
            _cpInventoryRepository = cpInventoryRepository;
        }

        /// <summary>
        /// メインメソッド：在庫表PDFを生成
        /// Phase 1では単純なPDF生成のみ実装
        /// </summary>
        public async Task<byte[]> GenerateInventoryListAsync(DateTime jobDate, string? dataSetId = null)
        {
            _logger.LogInformation("=== Phase 1: 最小限実装開始 ===");
            _logger.LogInformation("対象日: {JobDate}", jobDate.ToString("yyyy-MM-dd"));

            try
            {
                // Step 1: シンプルなテストデータ作成
                var testDataTable = CreateSimpleTestDataTable();
                _logger.LogInformation("テストデータ作成完了: {RowCount}行", testDataTable.Rows.Count);

                // Step 2: PDF生成（最もシンプルな方法）
                var pdfBytes = GenerateSimplePdf(testDataTable);
                _logger.LogInformation("PDF生成完了: {Size}バイト", pdfBytes.Length);

                if (pdfBytes.Length == 0)
                {
                    throw new InvalidOperationException("PDFが0バイトです");
                }

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Phase 1 PDF生成エラー");
                throw;
            }
        }

        /// <summary>
        /// テスト用の単純なDataTable作成
        /// </summary>
        private DataTable CreateSimpleTestDataTable()
        {
            var dt = new DataTable("InventoryData");
            
            // 最小限の列定義
            dt.Columns.Add("Col1", typeof(string));
            dt.Columns.Add("Col2", typeof(string));
            dt.Columns.Add("Col3", typeof(string));
            
            // テストデータ追加（5行程度）
            dt.Rows.Add("商品001", "等級A", "在庫10");
            dt.Rows.Add("商品002", "等級B", "在庫20");
            dt.Rows.Add("商品003", "等級A", "在庫15");
            dt.Rows.Add("商品004", "等級C", "在庫5");
            dt.Rows.Add("商品005", "等級B", "在庫30");
            
            return dt;
        }

        /// <summary>
        /// 最もシンプルなPDF生成
        /// </summary>
        private byte[] GenerateSimplePdf(DataTable dataTable)
        {
            using var report = new Report();
            
            // テンプレートパス取得
            var templatePath = GetTemplatePath();
            _logger.LogInformation("テンプレート: {Path}", templatePath);
            
            // テンプレート読み込み
            report.Load(templatePath);
            
            // データ登録（最重要）
            report.RegisterData(dataTable, "InventoryData");
            
            // 準備と出力
            report.Prepare();
            
            // PDF出力
            using var pdfExport = new PDFSimpleExport();
            using var stream = new MemoryStream();
            report.Export(pdfExport, stream);
            
            return stream.ToArray();
        }
        
        /// <summary>
        /// テンプレートパス取得
        /// </summary>
        private string GetTemplatePath()
        {
            // 実行ディレクトリからの相対パス
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(basePath, "FastReport", "Templates", "InventoryList.frx");
        }
    }
}

