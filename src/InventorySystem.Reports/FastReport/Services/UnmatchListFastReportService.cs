#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
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
                // ===== FastReport診断情報 開始 =====
                _logger.LogInformation("=== FastReport Service Diagnostics ===");
                _logger.LogInformation("FastReport service is being executed");
                var itemsList = unmatchItems?.ToList() ?? new List<UnmatchItem>();
                _logger.LogInformation($"Data count: {itemsList.Count}");
                _logger.LogInformation($"Job date: {jobDate:yyyy-MM-dd}");

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
                
                // スクリプトを完全に無効化
                SetScriptLanguageToNone(report);
                
                // データソースの準備
                var unmatchList = unmatchItems.ToList();
                _logger.LogCritical("===== FastReport データソース診断 =====");
                _logger.LogCritical("入力されたアンマッチ項目数: {Count}", unmatchList.Count);
                
                // 入力データの詳細分析
                if (unmatchList.Count > 0)
                {
                    var categoryBreakdown = unmatchList.GroupBy(x => x.Category).ToList();
                    _logger.LogCritical("カテゴリ別内訳:");
                    foreach (var group in categoryBreakdown)
                    {
                        _logger.LogCritical("  {Category}: {Count}件", group.Key, group.Count());
                    }
                    
                    var alertTypeBreakdown = unmatchList.GroupBy(x => x.AlertType).ToList();
                    _logger.LogCritical("アラート種別内訳:");
                    foreach (var group in alertTypeBreakdown)
                    {
                        _logger.LogCritical("  {AlertType}: {Count}件", group.Key, group.Count());
                    }
                }
                
                _logger.LogDebug("PDF生成: アンマッチ項目数={Count}", unmatchList.Count);
                
                // 最初の5件の文字列状態を確認
                foreach (var (item, index) in unmatchList.Take(5).Select((i, idx) => (i, idx)))
                {
                    _logger.LogDebug("PDF生成 行{Index}: 得意先名='{CustomerName}', 商品名='{ProductName}', 荷印名='{ManualShippingMark}'", 
                        index + 1, item.CustomerName, item.ProductName, item.Key.ManualShippingMark);
                    
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
                dataTable.Columns.Add("ManualShippingMark", typeof(string));
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
                _logger.LogCritical("===== DataTable作成開始 =====");
                int addedRowCount = 0;
                foreach (var (item, index) in unmatchList.Select((i, idx) => (i, idx)))
                {
                    var categoryName = GetCategoryName(item.Category);
                    
                    // カテゴリ変換のログ（最初の5件のみ）
                    if (index < 5)
                    {
                        _logger.LogCritical("カテゴリ変換 {Index}: '{Original}' → '{Converted}'", 
                            index + 1, item.Category, categoryName);
                    }
                    var customerCode = item.CustomerCode ?? "";
                    var customerName = item.CustomerName ?? "";
                    var productCode = item.Key.ProductCode ?? "";
                    var productName = item.ProductName ?? "";
                    var shippingMarkCode = item.Key.ShippingMarkCode ?? "";
                    var shippingMarkName = item.Key.ManualShippingMark ?? "";
                    
                    // デバッグログ追加（文字化け調査用）
                    _logger.LogDebug("DataTable追加前: カテゴリ={Category}, 商品名={ProductName}, 荷印名={ManualShippingMark}", 
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
                    
                    // 等級名の設定（コード0の場合は空白）
                    var gradeName = IsZeroCode(item.Key.GradeCode) ? "" : (item.GradeName ?? "");
                    
                    // 階級名の設定（コード0の場合は空白）
                    var className = IsZeroCode(item.Key.ClassCode) ? "" : (item.ClassName ?? "");
                    
                    // 荷印名の設定（コード0の場合は空白）
                    var displayManualShippingMark = IsZeroCode(item.Key.ShippingMarkCode) ? "" : shippingMarkName;
                    
                    dataTable.Rows.Add(
                        categoryName,
                        customerCode,
                        customerName,
                        productCode,
                        productName,
                        shippingMarkCode,
                        item.ShippingMarkName ?? "",
                        displayManualShippingMark,
                        item.Key.GradeCode ?? "",
                        gradeName,
                        item.Key.ClassCode ?? "",
                        className,
                        item.Quantity,
                        item.UnitPrice,
                        item.Amount,
                        item.VoucherNumber ?? "",
                        item.AlertType ?? "",
                        item.AlertType2 ?? ""
                    );
                    
                    addedRowCount++;
                }
                
                _logger.LogCritical("===== DataTable作成完了 =====");
                _logger.LogCritical("追加した行数: {AddedCount} / 入力データ数: {InputCount}", 
                    addedRowCount, unmatchList.Count);
                _logger.LogCritical("DataTable実際の行数: {ActualRows}", dataTable.Rows.Count);
                
                _logger.LogInformation("データソースを登録しています。件数: {Count}", dataTable.Rows.Count);
                
                // DataTableとして登録
                _logger.LogCritical("===== FastReport データソース登録 =====");
                report.RegisterData(dataTable, "UnmatchItems");
                _logger.LogCritical("report.RegisterData() 完了");
                
                // データソース検証
                var registeredDataSource = report.GetDataSource("UnmatchItems");
                if (registeredDataSource != null)
                {
                    _logger.LogCritical("データソース登録確認 OK: {Name}", registeredDataSource.Name);
                    _logger.LogCritical("データソース行数確認: {Count}", registeredDataSource.RowCount);
                    _logger.LogCritical("データソース有効状態: {Enabled}", registeredDataSource.Enabled);
                    
                    // データソース詳細検証
                    ValidateDataSource(registeredDataSource, unmatchList.Count);
                }
                else
                {
                    _logger.LogError("データソース登録失敗: 'UnmatchItems' が見つかりません");
                    throw new InvalidOperationException("データソースの登録に失敗しました");
                }
                
                // 0件時のヘッダー制御
                if (unmatchList.Count == 0)
                {
                    _logger.LogInformation("アンマッチ0件のため、ヘッダーを非表示にします");
                    
                    // PageHeaderBandを取得
                    var pageHeader = report.FindObject("PageHeader1") as FR.PageHeaderBand;
                    if (pageHeader != null)
                    {
                        // ヘッダーオブジェクトを非表示（Header1～Header18）
                        for (int i = 1; i <= 18; i++)
                        {
                            var header = report.FindObject($"Header{i}") as FR.TextObject;
                            if (header != null)
                            {
                                header.Visible = false;
                                _logger.LogDebug("Header{Index}を非表示にしました", i);
                            }
                        }
                        
                        // ページ番号も非表示
                        var pageNumber = report.FindObject("PageNumber") as FR.TextObject;
                        if (pageNumber != null)
                        {
                            pageNumber.Visible = false;
                            _logger.LogDebug("PageNumberを非表示にしました");
                        }
                        
                        // CreateDateとTitleは表示したままにする
                        _logger.LogDebug("CreateDateとTitleは表示を維持します");
                    }
                    else
                    {
                        _logger.LogWarning("PageHeader1が見つかりません");
                    }
                }
                
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
                _logger.LogCritical("===== FastReport Prepare() 開始 =====");
                _logger.LogInformation("レポートを生成しています...");
                
                // Prepare前のデータソース状態確認
                var preDataSource = report.GetDataSource("UnmatchItems");
                if (preDataSource != null)
                {
                    _logger.LogCritical("Prepare前データソース行数: {Count}", preDataSource.RowCount);
                }
                
                report.Prepare();
                
                // Prepare後のデータソース状態確認
                var postDataSource = report.GetDataSource("UnmatchItems");
                if (postDataSource != null)
                {
                    _logger.LogCritical("Prepare後データソース行数: {Count}", postDataSource.RowCount);
                }
                
                _logger.LogCritical("FastReport Prepare() 完了");
                
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
                    
                    // 画質設定
                    JpegQuality = 95,
                    
                    // セキュリティ設定なし（内部文書のため）
                    OpenAfterExport = false
                };
                
                _logger.LogDebug("PDFExport設定: EmbeddingFonts={EmbeddingFonts}, TextInCurves={TextInCurves}",
                    pdfExport.EmbeddingFonts, pdfExport.TextInCurves);
                
                // PDFをメモリストリームに出力
                _logger.LogCritical("===== PDF Export 開始 =====");
                using var stream = new MemoryStream();
                report.Export(pdfExport, stream);
                
                var pdfBytes = stream.ToArray();
                _logger.LogCritical("===== PDF Export 完了 =====");
                _logger.LogInformation("PDF生成完了。サイズ: {Size} bytes", pdfBytes.Length);
                
                // 最終結果サマリー
                _logger.LogCritical("===== 最終結果サマリー =====");
                _logger.LogCritical("入力データ数: {Input}", unmatchList.Count);
                _logger.LogCritical("DataTable行数: {DataTable}", dataTable.Rows.Count);
                _logger.LogCritical("PDF出力サイズ: {Size} bytes", pdfBytes.Length);
                
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
        /// FastReportのスクリプト機能を完全に無効化する
        /// </summary>
        /// <param name="report">対象のレポートオブジェクト</param>
        private void SetScriptLanguageToNone(FR.Report report)
        {
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
                        var noneValue = Enum.GetValues(scriptLanguageType)
                            .Cast<object>()
                            .FirstOrDefault(v => v.ToString() == "None");
                        
                        if (noneValue != null)
                        {
                            scriptLanguageProperty.SetValue(report, noneValue);
                            _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }
                
                // Scriptプロパティをnullに設定（追加の安全策）
                var scriptProperty = report.GetType().GetProperty("Script", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                    _logger.LogInformation("Scriptプロパティをnullに設定しました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
                // エラーが発生しても処理を継続
            }
        }

        /// <summary>
        /// コードが0（オール0）かどうかを判定
        /// </summary>
        private static bool IsZeroCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            
            var cleanCode = code.Trim().Trim('"');
            
            if (string.IsNullOrEmpty(cleanCode))
                return false;
            
            // 数値として0かチェック
            if (decimal.TryParse(cleanCode, out var numValue) && numValue == 0)
                return true;
            
            // 文字列として全て0かチェック（例：0, 00, 000, 0000）
            if (cleanCode.All(c => c == '0'))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// データソースの詳細検証
        /// </summary>
        private void ValidateDataSource(object dataSource, int expectedCount)
        {
            try
            {
                _logger.LogCritical("===== データソース詳細検証 =====");
                
                // リフレクションでRowCountプロパティを取得
                var rowCountProperty = dataSource.GetType().GetProperty("RowCount");
                if (rowCountProperty != null)
                {
                    var actualRowCount = (int)rowCountProperty.GetValue(dataSource);
                    _logger.LogCritical("期待行数: {Expected} / 実際行数: {Actual}", expectedCount, actualRowCount);
                    
                    if (actualRowCount != expectedCount)
                    {
                        _logger.LogError("❌ 行数不一致を検出しました！");
                        _logger.LogError("この差異が411→16の原因の可能性があります");
                    }
                    else
                    {
                        _logger.LogCritical("✅ 行数一致確認 OK");
                    }
                }
                
                // カラム数の検証
                var columnsProperty = dataSource.GetType().GetProperty("Columns");
                if (columnsProperty != null)
                {
                    var columns = columnsProperty.GetValue(dataSource);
                    if (columns != null)
                    {
                        var countProperty = columns.GetType().GetProperty("Count");
                        if (countProperty != null)
                        {
                            var columnCount = (int)countProperty.GetValue(columns);
                            _logger.LogCritical("カラム数: {ColumnCount}", columnCount);
                        }
                    }
                }
                
                _logger.LogCritical("データソース検証完了");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "データソース検証中にエラーが発生しましたが、処理を継続します");
            }
        }
        
        /// <summary>
        /// カテゴリコードを日本語名に変換
        /// </summary>
        private string GetCategoryName(string category)
        {
            return category switch
            {
                "51" => "掛売上",     // 修正: 11 → 51
                "52" => "現金売上",   // 修正: 12 → 52
                "11" => "掛仕入",     // 修正: 21 → 11
                "12" => "現金仕入",   // 修正: 22 → 12
                "71" => "在庫調整",
                "振替" => "振替",     // 修正: "04" → "振替"
                "加工費" => "加工費", // 修正: "05" → "加工費"
                "在庫調整" => "在庫調整", // 追加: 既存の文字列マッピング
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