using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FastReport;
using FastReport.Export.Pdf;
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
            
            // === 制御情報列（4列）===
            dt.Columns.Add("RowType", typeof(string));            // "DETAIL" 固定
            dt.Columns.Add("IsPageBreak", typeof(string));        // "0" 固定
            dt.Columns.Add("IsBold", typeof(string));             // "0" 固定
            dt.Columns.Add("IsGrayBackground", typeof(string));   // "0" 固定
            
            // === 担当者情報列（2列）===
            dt.Columns.Add("StaffCode", typeof(string));          // "000" 固定
            dt.Columns.Add("StaffName", typeof(string));          // "未設定" 固定
            
            // === 表示データ列（9列）===
            dt.Columns.Add("Col1", typeof(string));  // 商品名等
            dt.Columns.Add("Col2", typeof(string));  // 荷印
            dt.Columns.Add("Col3", typeof(string));  // 等級
            dt.Columns.Add("Col4", typeof(string));  // 階級
            dt.Columns.Add("Col5", typeof(string));  // 前日在庫数量
            dt.Columns.Add("Col6", typeof(string));  // 当日在庫数量
            dt.Columns.Add("Col7", typeof(string));  // 在庫金額
            dt.Columns.Add("Col8", typeof(string));  // 滞留マーク
            dt.Columns.Add("Col9", typeof(string));  // 備考
            
            // === ページ情報列（2列）===
            dt.Columns.Add("CurrentPage", typeof(string));        // "1" 固定
            dt.Columns.Add("TotalPages", typeof(string));         // "1" 固定
            
            // === テストデータ追加（5行）===
            // すべての列を埋める（フェーズ1では最小限の値）
            dt.Rows.Add(
                "DETAIL", "0", "0", "0",   // 制御
                "000", "未設定",             // 担当者
                "商品001", "荷印A", "等級A", "", "", "", "", "", "", // 表示
                "1", "1"                     // ページ
            );
            dt.Rows.Add(
                "DETAIL", "0", "0", "0",
                "000", "未設定",
                "商品002", "荷印B", "等級B", "", "", "", "", "", "",
                "1", "1"
            );
            dt.Rows.Add(
                "DETAIL", "0", "0", "0",
                "000", "未設定",
                "商品003", "荷印A", "等級A", "", "", "", "", "", "",
                "1", "1"
            );
            dt.Rows.Add(
                "DETAIL", "0", "0", "0",
                "000", "未設定",
                "商品004", "荷印C", "等級C", "", "", "", "", "", "",
                "1", "1"
            );
            dt.Rows.Add(
                "DETAIL", "0", "0", "0",
                "000", "未設定",
                "商品005", "荷印B", "等級B", "", "", "", "", "", "",
                "1", "1"
            );
            
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
            report.ReportResourceString = string.Empty; // ProductAccountと同様にクリア
            report.FileName = templatePath;             // 参照ファイル名を設定
            report.Load(templatePath);
            
            // スクリプトを完全に無効化（ProductAccountと同等の対策）
            SetScriptLanguageToNone(report);
            
            // データ登録（最重要）
            report.RegisterData(dataTable, "InventoryData");
            var ds = report.GetDataSource("InventoryData");
            if (ds != null)
            {
                ds.Enabled = true;
                _logger.LogInformation("データソース有効化: InventoryData");
                _logger.LogInformation("DataTable登録完了: {RowCount}行, {ColumnCount}列", dataTable.Rows.Count, dataTable.Columns.Count);
            }
            
            // パラメータ（最小限）
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            report.SetParameterValue("JobDate", DateTime.Now.ToString("yyyy/MM/dd"));
            report.SetParameterValue("TotalCount", dataTable.Rows.Count.ToString());

            // 準備と出力
            _logger.LogInformation("レポート準備開始");
            report.Prepare();
            _logger.LogInformation("レポート準備完了");
            
            // PDF出力
            using var pdfExport = new PDFExport();
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

        /// <summary>
        /// FastReportのスクリプトを完全に無効化する（CodeDOMコンパイル抑止）
        /// ProductAccountFastReportService と同等の実装
        /// </summary>
        private void SetScriptLanguageToNone(Report report)
        {
            try
            {
                // ScriptLanguage を None に設定
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
                            _logger.LogInformation("InventoryList: ScriptLanguageをNoneに設定しました");
                        }
                    }
                }

                // 非公開 Script プロパティを null に設定（念のため）
                var scriptProperty = report.GetType().GetProperty(
                    "Script",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                    _logger.LogInformation("InventoryList: Scriptプロパティをnullに設定しました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InventoryList: ScriptLanguage設定時の警告: {Message}", ex.Message);
            }
        }
    }
}
