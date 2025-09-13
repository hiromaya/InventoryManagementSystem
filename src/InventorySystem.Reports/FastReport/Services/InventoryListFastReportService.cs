using System;
using System.Data;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FastReport;
using FR = global::FastReport;
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
            _logger.LogInformation("=== Phase 1.5: CP在庫マスタ連携実装 ===");
            _logger.LogInformation("対象日: {JobDate}", jobDate.ToString("yyyy-MM-dd"));

            try
            {
                // Step 1: CP在庫マスタから在庫表用DataTableを作成
                var inventoryDataTable = await CreateInventoryDataTableAsync(jobDate);
                _logger.LogInformation("在庫データ取得完了: {RowCount}行", inventoryDataTable.Rows.Count);

                // Step 2: PDF生成
                var pdfBytes = GenerateSimplePdf(inventoryDataTable, jobDate);
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
        private async Task<DataTable> CreateInventoryDataTableAsync(DateTime jobDate)
        {
            var dt = new DataTable("InventoryData");

            // === 制御情報列（4列）===
            dt.Columns.Add("RowType", typeof(string));
            dt.Columns.Add("IsPageBreak", typeof(string));
            dt.Columns.Add("IsBold", typeof(string));
            dt.Columns.Add("IsGrayBackground", typeof(string));

            // === 担当者情報列（2列）===
            dt.Columns.Add("StaffCode", typeof(string));
            dt.Columns.Add("StaffName", typeof(string));

            // === 表示データ列（9列＋手入力）===
            dt.Columns.Add("Col1", typeof(string));
            dt.Columns.Add("Col2", typeof(string));
            dt.Columns.Add("ColManual", typeof(string));
            dt.Columns.Add("Col3", typeof(string));
            dt.Columns.Add("Col4", typeof(string));
            dt.Columns.Add("Col5", typeof(string));
            dt.Columns.Add("Col6", typeof(string));
            dt.Columns.Add("Col7", typeof(string));
            dt.Columns.Add("Col8", typeof(string));
            dt.Columns.Add("Col9", typeof(string));

            // === ページ情報列（2列）===
            dt.Columns.Add("CurrentPage", typeof(string));
            dt.Columns.Add("TotalPages", typeof(string));

            // === CP在庫マスタからデータ取得 ===
            var cpInventoryData = await _cpInventoryRepository.GetByJobDateAsync(jobDate);

            if (cpInventoryData == null || !cpInventoryData.Any())
            {
                _logger.LogWarning("CP在庫マスタにデータがありません。JobDate: {JobDate}", jobDate);
                return dt;
            }

            _logger.LogInformation("CP在庫マスタから{Count}件取得", cpInventoryData.Count());

            // 除外条件・ソート
            // Current系の同義としてDaily系を使用（CpInventoryMasterの現行プロパティ名に準拠）
            var filtered = cpInventoryData
                // 当日在庫数量/金額が共に0の行を除外（CurrentStock/Amount の同義）
                .Where(x => !(x.DailyStock == 0 && x.DailyStockAmount == 0))
                // 前日在庫0を除外（PreviousStock の同義）
                .Where(x => x.PreviousDayStock != 0)
                .OrderBy(x => string.IsNullOrEmpty(x.ProductCategory1) ? "000" : x.ProductCategory1)
                .ThenBy(x => x.Key.ProductCode)
                .ThenBy(x => x.Key.ShippingMarkCode)
                .ThenBy(x => x.Key.ManualShippingMark)
                .ThenBy(x => x.Key.GradeCode)
                .ThenBy(x => x.Key.ClassCode)
                .ToList();

            _logger.LogInformation("除外条件適用後: {Count}件", filtered.Count);

            foreach (var item in filtered)
            {
                var staffCode = string.IsNullOrEmpty(item.ProductCategory1) ? "000" : item.ProductCategory1;
                var staffName = GetStaffName(staffCode);
                var stagnation = CalculateStagnationMark(item.LastReceiptDate, jobDate);

                // 表示値の準備（指示に基づくマッピング）
                var col1_ProductName = item.ProductName ?? string.Empty; // 商品名のみ
                var col2_Shipping = !string.IsNullOrWhiteSpace(item.ShippingMarkName)
                    ? item.ShippingMarkName
                    : (item.Key.ShippingMarkCode ?? string.Empty); // 荷印名が空ならコードを表示
                var colManual = (item.ManualShippingMark ?? string.Empty).Trim();

                // Current系の同義としてDaily系を使用（現行エンティティ準拠）
                var currentStock = item.DailyStock;
                var currentUnitPrice = item.DailyUnitPrice;
                var currentAmount = item.DailyStockAmount;

                var col5_Quantity = FormatQuantity(currentStock);
                var col6_UnitPrice = FormatUnitPrice(currentUnitPrice);
                var col7_Amount = FormatAmount(currentAmount);
                var col8_LastReceipt = item.LastReceiptDate.HasValue
                    ? item.LastReceiptDate.Value.ToString("(yy-MM-dd)")
                    : string.Empty;

                dt.Rows.Add(
                    "DETAIL",     // RowType
                    "0",          // IsPageBreak
                    "0",          // IsBold
                    "0",          // IsGrayBackground
                    staffCode,     // StaffCode
                    staffName,     // StaffName
                    col1_ProductName,                                                           // Col1
                    col2_Shipping,                                                              // Col2
                    colManual,                                                                  // ColManual
                    item.GradeName ?? string.Empty,                                              // Col3
                    item.ClassName ?? string.Empty,                                              // Col4
                    col5_Quantity,                                                               // Col5（当日在庫数量）
                    col6_UnitPrice,                                                              // Col6（当日在庫単価）
                    col7_Amount,                                                                 // Col7（当日在庫金額）
                    col8_LastReceipt,                                                            // Col8（最終入荷日）
                    stagnation,                                                                  // Col9（滞留マーク）
                    "1", "1"   // CurrentPage, TotalPages（仮）
                );
            }

            _logger.LogInformation("DataTable作成完了: {RowCount}行", dt.Rows.Count);
            return dt;
        }

        /// <summary>
        /// 最もシンプルなPDF生成
        /// </summary>
        private byte[] GenerateSimplePdf(DataTable dataTable, DateTime jobDate)
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

            // テンプレート内の潜在的な式/複雑表現を最小化（Phase1安全策）
            TryClearPotentialExpressions(report);
            
            // パラメータ（最小限）
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy/MM/dd"));
            report.SetParameterValue("TotalCount", dataTable.Rows.Count.ToString());

            // 直前でスクリプト無効化を再度強制
            try
            {
                var slProp = report.GetType().GetProperty("ScriptLanguage");
                if (slProp != null && slProp.PropertyType.IsEnum)
                {
                    var none = Enum.GetValues(slProp.PropertyType).Cast<object>().FirstOrDefault(v => v.ToString() == "None");
                    if (none != null) slProp.SetValue(report, none);
                }
                var scriptProp = report.GetType().GetProperty("Script",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                scriptProp?.SetValue(report, null);
            }
            catch { /* ignore */ }

            // 表現式の無害化（万一のCodeDOM回避）
            TryNeutralizeExpressions(report);

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

                // 反映結果をログ
                var currentLang = scriptLanguageProperty?.GetValue(report)?.ToString() ?? "(unknown)";
                var scriptValue = scriptProperty?.GetValue(report);
                _logger.LogInformation("InventoryList: ScriptLanguage現値={Lang}, Script is null={IsNull}", currentLang, scriptValue == null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InventoryList: ScriptLanguage設定時の警告: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// テンプレ内の潜在的なスクリプト/式由来のコンパイルを避けるため、式を簡略化
        /// </summary>
        private void TryClearPotentialExpressions(Report report)
        {
            try
            {
                // Phase1.5: StaffLabelも簡略化してCodeDOM経路を避ける
                var staffLabel = report.FindObject("StaffLabel") as FR.TextObject;
                if (staffLabel != null)
                {
                    staffLabel.Text = "[IIF([InventoryData.StaffCode] != \"\", \"担当者コード：\" + [InventoryData.StaffCode], \"\")]";
                }

                // PageInfo の TotalPages# 参照を簡略化（Page# のみ）
                var pageInfo = report.FindObject("PageInfo") as FR.TextObject;
                // PageInfoの簡略化は維持（TotalPages#依存を避ける）
                if (pageInfo != null) pageInfo.Text = "[Page#] 頁";

                // DataBand の StartNewPageExpression をクリア
                var dataBand = report.FindObject("Data1");
                if (dataBand != null)
                {
                    var prop = dataBand.GetType().GetProperty("StartNewPageExpression");
                    prop?.SetValue(dataBand, "");
                    _logger.LogInformation("InventoryList: DataBand.StartNewPageExpressionをクリアしました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InventoryList: 式クリア中の警告: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// バンド/オブジェクトの式や条件プロパティを汎用的に空にする（安全策）
        /// </summary>
        private void TryNeutralizeExpressions(Report report)
        {
            try
            {
                for (int i = 0; i < report.Pages.Count; i++)
                {
                    if (report.Pages[i] is FR.ReportPage page)
                    {
                        foreach (var obj in page.AllObjects)
                        {
                            var t = obj.GetType();
                            var cond = t.GetProperty("Condition");
                            cond?.SetValue(obj, "");
                            var expr = t.GetProperty("Expression");
                            expr?.SetValue(obj, "");
                            var filter = t.GetProperty("Filter");
                            filter?.SetValue(obj, null);
                            var dataFilter = t.GetProperty("FilterExpression");
                            dataFilter?.SetValue(obj, "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InventoryList: TryNeutralizeExpressions 警告: {Message}", ex.Message);
            }
        }

        // 数値フォーマット（商品勘定の仕様に準拠）
        private string FormatQuantity(decimal? value)
        {
            if (value == null || value.Value == 0m) return string.Empty;
            if (value.Value < 0m) return $"{Math.Abs(value.Value):N2}▲";
            return value.Value.ToString("N2");
        }

        private string FormatAmount(decimal? value)
        {
            if (value == null || value.Value == 0m) return string.Empty;
            if (value.Value < 0m) return $"{Math.Abs(value.Value):N0}▲";
            return value.Value.ToString("N0");
        }

        private string FormatUnitPrice(decimal? value)
        {
            if (value == null || value.Value == 0m) return string.Empty;
            if (value.Value < 0m) return $"{Math.Abs(value.Value):N0}▲";
            return value.Value.ToString("N0");
        }

        /// <summary>
        /// 滞留マークを算出（11/21/31日で !/!!/!!!）
        /// </summary>
        private string CalculateStagnationMark(DateTime? lastReceiptDate, DateTime jobDate)
        {
            if (!lastReceiptDate.HasValue || lastReceiptDate.Value == DateTime.MinValue)
            {
                return string.Empty;
            }
            var days = (jobDate.Date - lastReceiptDate.Value.Date).Days;
            if (days >= 31) return "!!!";
            if (days >= 21) return "!!";
            if (days >= 11) return "!";
            return string.Empty;
        }

        /// <summary>
        /// 担当者名（フェーズ1.5は仮実装）
        /// </summary>
        private string GetStaffName(string? staffCode)
        {
            if (string.IsNullOrWhiteSpace(staffCode) || staffCode == "000")
            {
                return "未設定";
            }
            return $"担当者{staffCode}";
        }
    }
}
