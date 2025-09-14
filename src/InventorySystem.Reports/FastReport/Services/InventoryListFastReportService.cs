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
        /// 在庫表PDFを生成（CP在庫マスタ連携版）
        /// </summary>
        public async Task<byte[]> GenerateInventoryListAsync(DateTime jobDate, string? dataSetId = null)
        {
            _logger.LogInformation("在庫表生成開始: JobDate={JobDate}", jobDate);

            try
            {
                // Step 1: CP在庫マスタからデータ取得→DataTable作成
                var dataTable = await CreateInventoryDataTableAsync(jobDate);
                _logger.LogInformation("在庫データ行数: {RowCount}", dataTable.Rows.Count);

                // Step 2: PDF生成
                var pdfBytes = GeneratePdf(dataTable, jobDate);
                _logger.LogInformation("PDF生成完了: {Size}バイト", pdfBytes.Length);

                if (pdfBytes.Length == 0)
                {
                    throw new InvalidOperationException("PDFが0バイトです");
                }

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "在庫表生成エラー");
                throw;
            }
        }

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
            // 指示のGetInventoryForReportAsyncが未定義のため、現行のGetByJobDateAsyncを使用
            var cpInventoryData = await _cpInventoryRepository.GetByJobDateAsync(jobDate);

            if (cpInventoryData == null || !cpInventoryData.Any())
            {
                _logger.LogWarning("CP在庫マスタにデータがありません。JobDate: {JobDate}", jobDate);
                return dt;
            }

            var totalCount = cpInventoryData.Count();
            _logger.LogInformation("CP在庫マスタから{Count}件取得", totalCount);

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

            var excluded = totalCount - filtered.Count;
            _logger.LogInformation("除外件数: {Excluded}件, 対象件数: {Count}件", excluded, filtered.Count);

            string? previousProductCode = null;
            decimal subtotalQuantity = 0m;
            decimal subtotalAmount = 0m;

            foreach (var item in filtered)
            {
                var staffCode = string.IsNullOrEmpty(item.ProductCategory1) ? "000" : item.ProductCategory1;
                var staffName = GetStaffName(staffCode);
                var stagnation = CalculateStagnationMark(item.LastReceiptDate, jobDate);

                // 商品コードが変わったら小計行を追加
                var currentProductCode = item.Key.ProductCode ?? string.Empty;
                if (!string.IsNullOrEmpty(previousProductCode) && previousProductCode != currentProductCode)
                {
                    AddSubtotalRow(dt, subtotalQuantity, subtotalAmount);
                    subtotalQuantity = 0m;
                    subtotalAmount = 0m;
                }

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

                // 小計累積
                subtotalQuantity += item.DailyStock;
                subtotalAmount += item.DailyStockAmount;
                previousProductCode = currentProductCode;
            }

            // 最後の商品の小計
            if (!string.IsNullOrEmpty(previousProductCode))
            {
                AddSubtotalRow(dt, subtotalQuantity, subtotalAmount);
            }

            _logger.LogInformation("DataTable作成完了: {RowCount}行", dt.Rows.Count);
            return dt;
        }

        /// <summary>
        /// PDF生成
        /// </summary>

        private byte[] GeneratePdf(DataTable dataTable, DateTime jobDate)
        {
            using var report = new Report();
            
            // テンプレートパス取得
            var templatePath = GetTemplatePath();
            _logger.LogInformation("テンプレート: {Path}", templatePath);
            
            // テンプレート読み込み
            report.Load(templatePath);
            
            // ★ Load直後のScriptLanguage値を確認
            _logger.LogInformation("Load直後: ScriptLanguage={0}", report.ScriptLanguage);
            
            // スクリプトを完全に無効化
            SetScriptLanguageToNone(report);
            
            // ★ SetScriptLanguageToNone後の値を確認
            _logger.LogInformation("設定後: ScriptLanguage={0}", report.ScriptLanguage);
            
            // ★ 重要：Compileを明示的にスキップ
            try
            {
                // リフレクションでScriptRestrictionsプロパティを設定
                var scriptRestrictionsProperty = report.GetType().GetProperty("ScriptRestrictions");
                if (scriptRestrictionsProperty != null)
                {
                    scriptRestrictionsProperty.SetValue(report, true);
                    _logger.LogInformation("ScriptRestrictionsをtrueに設定");
                }
                
                // リフレクションでNeedCompileプロパティをfalseに設定
                var needCompileProperty = report.GetType().GetProperty("NeedCompile", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (needCompileProperty != null)
                {
                    needCompileProperty.SetValue(report, false);
                    _logger.LogInformation("NeedCompileをfalseに設定");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("追加設定の警告: {Message}", ex.Message);
            }
            
            // データ登録（最重要）
            report.RegisterData(dataTable, "InventoryData");
            var ds = report.GetDataSource("InventoryData");
            if (ds != null)
            {
                ds.Enabled = true;
                _logger.LogInformation("データソース有効化: InventoryData");
            }

            // DataBand のデータソースを明示的に設定
            if (report.Pages.Count > 0 && report.Pages[0] is FR.ReportPage rp)
            {
                foreach (var band in rp.Bands)
                {
                    if (band is FR.DataBand db && db.Name == "Data1")
                    {
                        db.DataSource = report.GetDataSource("InventoryData");
                        _logger.LogInformation("DataBand設定完了");
                        break;
                    }
                }
            }
            
            // パラメータ（最小限）
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy/MM/dd"));
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

        private void AddSubtotalRow(DataTable dataTable, decimal quantity, decimal amount)
        {
            var row = dataTable.NewRow();
            row["RowType"] = "PRODUCT_SUBTOTAL";
            row["IsPageBreak"] = "0";
            row["IsBold"] = "1";
            row["IsGrayBackground"] = "0";
            row["Col1"] = "＊　小　　計　＊";
            row["Col5"] = FormatQuantity(quantity);
            row["Col7"] = FormatAmount(amount);
            row["CurrentPage"] = "1";
            row["TotalPages"] = "1";
            dataTable.Rows.Add(row);
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
                // デバッグ: 利用可能な列挙値を確認
                var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                if (scriptLanguageProperty != null)
                {
                    var scriptLanguageType = scriptLanguageProperty.PropertyType;
                    _logger.LogInformation("ScriptLanguageType: {TypeName}", scriptLanguageType.FullName);
                    
                    if (scriptLanguageType.IsEnum)
                    {
                        // 利用可能な全ての値をログ出力
                        var enumValues = Enum.GetValues(scriptLanguageType);
                        _logger.LogInformation("利用可能な値: {Values}", 
                            string.Join(", ", enumValues.Cast<object>().Select(v => v.ToString())));
                        
                        // 方法1: 数値インデックスで設定（Noneは通常0）
                        var noneValue = enumValues.GetValue(0); // 最初の値（通常None）
                        if (noneValue != null)
                        {
                            scriptLanguageProperty.SetValue(report, noneValue);
                            _logger.LogInformation("ScriptLanguageを{Value}に設定しました（インデックス0）", noneValue);
                            
                            // 確認
                            var newValue = scriptLanguageProperty.GetValue(report);
                            _logger.LogInformation("設定後の値: {Value}", newValue);
                        }
                    }
                }
                else
                {
                    _logger.LogError("ScriptLanguageプロパティが見つかりません");
                }

                // Scriptプロパティをnullに設定
                var scriptProperty = report.GetType().GetProperty("Script", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scriptProperty != null && scriptProperty.CanWrite)
                {
                    scriptProperty.SetValue(report, null);
                    _logger.LogInformation("Scriptプロパティをnullに設定しました");
                }
                
                // ReportScriptプロパティも試す
                var reportScriptProperty = report.GetType().GetProperty("ReportScript", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (reportScriptProperty != null && reportScriptProperty.CanWrite)
                {
                    reportScriptProperty.SetValue(report, string.Empty);
                    _logger.LogInformation("ReportScriptプロパティを空に設定しました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetScriptLanguageToNoneでエラー");
            }
        }
        // TryClearPotentialExpressions / TryNeutralizeExpressions は使用しない

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
