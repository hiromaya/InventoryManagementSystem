#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using FastReport;
using FastReport.Export.Pdf;
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Dapper;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    public class ProductAccountFastReportService : IProductAccountReportService
    {
        private readonly ILogger<ProductAccountFastReportService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _templatePath;
        
        public ProductAccountFastReportService(
            ILogger<ProductAccountFastReportService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
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
                
                // 商品勘定データを取得
                var reportData = PrepareReportData(jobDate, departmentCode);
                
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
        /// 商品勘定データの準備
        /// </summary>
        private IEnumerable<ProductAccountReportModel> PrepareReportData(DateTime jobDate, string? departmentCode)
        {
            _logger.LogInformation("商品勘定データ準備開始 - JobDate: {JobDate}, Department: {Dept}", 
                jobDate, departmentCode ?? "全部門");

            var reportModels = new List<ProductAccountReportModel>();

            try
            {
                // まずストアドプロシージャの実行を試行
                try
                {
                    return ExecuteStoredProcedure(jobDate, departmentCode);
                }
                catch (SqlException sqlEx) when (sqlEx.Number == 2812)
                {
                    _logger.LogWarning("ストアドプロシージャ sp_CreateProductLedgerData が見つかりません。直接データ取得にフォールバックします。");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ストアドプロシージャ実行エラー。直接データ取得にフォールバックします。");
                }

                // 直接データ取得にフォールバック
                return GetReportDataDirectly(jobDate, departmentCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "商品勘定データ準備中にエラーが発生しました");
                throw;
            }
        }

        /// <summary>
        /// ストアドプロシージャによるデータ取得
        /// </summary>
        private IEnumerable<ProductAccountReportModel> ExecuteStoredProcedure(DateTime jobDate, string? departmentCode)
        {
            var reportModels = new List<ProductAccountReportModel>();
            
            using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            connection.Open();
            
            using var command = new SqlCommand("sp_CreateProductLedgerData", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 300; // 5分タイムアウト
            
            // パラメータ設定
            command.Parameters.AddWithValue("@JobDate", jobDate);
            command.Parameters.AddWithValue("@DepartmentCode", 
                string.IsNullOrEmpty(departmentCode) ? DBNull.Value : departmentCode);

            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                var model = new ProductAccountReportModel
                {
                    // 基本情報
                    ProductCode = reader["ProductCode"]?.ToString() ?? "",
                    ProductName = reader["ProductName"]?.ToString() ?? "",
                    ShippingMarkCode = reader["ShippingMarkCode"]?.ToString() ?? "",
                    ShippingMarkName = reader["ShippingMarkName"]?.ToString() ?? "",
                    ManualShippingMark = reader["ManualShippingMark"]?.ToString() ?? "",
                    GradeCode = reader["GradeCode"]?.ToString() ?? "",
                    GradeName = reader["GradeName"]?.ToString() ?? "",
                    ClassCode = reader["ClassCode"]?.ToString() ?? "",
                    ClassName = reader["ClassName"]?.ToString() ?? "",
                    
                    // 担当者情報（重要）
                    ProductCategory1 = reader["ProductCategory1"]?.ToString() ?? "",
                    
                    // 伝票情報
                    VoucherNumber = reader["VoucherNumber"]?.ToString() ?? "",
                    VoucherType = reader["VoucherType"]?.ToString() ?? "",
                    DisplayCategory = reader["DisplayCategory"]?.ToString() ?? "",
                    TransactionDate = reader.IsDBNull(reader.GetOrdinal("TransactionDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("TransactionDate")),
                    
                    // 数量・金額
                    PurchaseQuantity = reader.IsDBNull(reader.GetOrdinal("PurchaseQuantity")) ? 0 : reader.GetDecimal(reader.GetOrdinal("PurchaseQuantity")),
                    SalesQuantity = reader.IsDBNull(reader.GetOrdinal("SalesQuantity")) ? 0 : reader.GetDecimal(reader.GetOrdinal("SalesQuantity")),
                    RemainingQuantity = reader.IsDBNull(reader.GetOrdinal("RemainingQuantity")) ? 0 : reader.GetDecimal(reader.GetOrdinal("RemainingQuantity")),
                    UnitPrice = reader.IsDBNull(reader.GetOrdinal("UnitPrice")) ? 0 : reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                    Amount = reader.IsDBNull(reader.GetOrdinal("Amount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Amount")),
                    GrossProfit = reader.IsDBNull(reader.GetOrdinal("GrossProfit")) ? 0 : reader.GetDecimal(reader.GetOrdinal("GrossProfit")),
                    
                    // 取引先情報
                    CustomerSupplierName = reader["CustomerSupplierName"]?.ToString() ?? "",
                    
                    // その他
                    RecordType = reader["RecordType"]?.ToString() ?? "",
                    GroupKey = reader["GroupKey"]?.ToString() ?? "",
                    SortKey = reader["SortKey"]?.ToString() ?? ""
                };
                
                // 担当者名を取得（商品分類1マスタから）
                if (!string.IsNullOrEmpty(model.ProductCategory1))
                {
                    var staffName = GetStaffName(model.ProductCategory1);
                    model.SetAdditionalInfo("ProductCategory1Name", staffName);
                }
                
                reportModels.Add(model);
            }
            
            _logger.LogInformation("ストアドプロシージャから{Count}件のデータを取得", reportModels.Count);
            return reportModels;
        }

        /// <summary>
        /// 直接データ取得メソッド（フォールバック用）
        /// </summary>
        private IEnumerable<ProductAccountReportModel> GetReportDataDirectly(DateTime jobDate, string? departmentCode)
        {
            _logger.LogInformation("直接データ取得モードで商品勘定データを準備します");
            
            var reportModels = new List<ProductAccountReportModel>();
            
            try
            {
                // 簡易的な実装 - 販売伝票データから取得
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                
                var sql = @"
                    WITH ProductAccount AS (
                        -- 売上伝票データ
                        SELECT 
                            s.ProductCode,
                            s.ProductName,  -- JOINは不要、直接カラムを使用
                            '' as ProductCategory1,  -- 商品マスタからの取得が必要な場合は後で対応
                            s.ShippingMarkCode,
                            s.ShippingMarkName,
                            s.ShippingMarkName as ManualShippingMark,
                            s.GradeCode,
                            '' as GradeName,
                            s.ClassCode,
                            '' as ClassName,
                            s.VoucherNumber,
                            s.VoucherType,  -- VoucherCategoryではない
                            CASE s.VoucherType
                                WHEN '51' THEN '掛売'
                                WHEN '52' THEN '現売'
                                ELSE s.VoucherType
                            END as DisplayCategory,
                            s.VoucherDate as TransactionDate,
                            0 as PurchaseQuantity,
                            s.Quantity as SalesQuantity,
                            0 as RemainingQuantity,
                            s.UnitPrice,
                            s.Amount,
                            0 as GrossProfit,
                            s.CustomerName as CustomerSupplierName,  -- JOINは不要
                            'Sales' as RecordType
                        FROM SalesVouchers s  -- 複数形
                        WHERE s.JobDate = @JobDate
                          AND s.DetailType = '1'
                          AND s.IsActive = 1  -- アクティブなレコードのみ
                        
                        UNION ALL
                        
                        -- 仕入伝票データ
                        SELECT 
                            p.ProductCode,
                            '' as ProductName,  -- PurchaseVouchersにはProductNameがない
                            '' as ProductCategory1,
                            p.ShippingMarkCode,
                            p.ShippingMarkName,
                            p.ShippingMarkName as ManualShippingMark,
                            p.GradeCode,
                            '' as GradeName,
                            p.ClassCode,
                            '' as ClassName,
                            p.VoucherNumber,
                            p.VoucherType,  -- VoucherCategoryではない
                            CASE p.VoucherType
                                WHEN '11' THEN '掛仕'
                                WHEN '12' THEN '現仕'
                                ELSE p.VoucherType
                            END as DisplayCategory,
                            p.VoucherDate as TransactionDate,
                            p.Quantity as PurchaseQuantity,
                            0 as SalesQuantity,
                            0 as RemainingQuantity,
                            p.UnitPrice,
                            p.Amount,
                            0 as GrossProfit,
                            p.SupplierName as CustomerSupplierName,  -- JOINは不要
                            'Purchase' as RecordType
                        FROM PurchaseVouchers p  -- 複数形
                        WHERE p.JobDate = @JobDate
                          AND p.DetailType = '1'
                          AND p.IsActive = 1  -- アクティブなレコードのみ
                    )
                    SELECT * FROM ProductAccount
                    ORDER BY ProductCategory1, ProductCode, ShippingMarkCode, ManualShippingMark, 
                             GradeCode, ClassCode, TransactionDate, VoucherNumber";
                
                var results = connection.Query<ProductAccountReportModel>(sql, new { 
                    JobDate = jobDate, 
                    DepartmentCode = departmentCode 
                });
                
                foreach (var model in results)
                {
                    // 担当者名を取得
                    if (!string.IsNullOrEmpty(model.ProductCategory1))
                    {
                        var staffName = GetStaffName(model.ProductCategory1);
                        model.SetAdditionalInfo("ProductCategory1Name", staffName);
                    }
                    
                    // GroupKeyの生成
                    model.GroupKey = $"{model.ProductCode}_{model.ShippingMarkCode}_{model.GradeCode}_{model.ClassCode}";
                    
                    reportModels.Add(model);
                }
                
                _logger.LogInformation("直接取得で{Count}件のデータを準備しました", reportModels.Count);
                
                return reportModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "直接データ取得中にエラーが発生しました");
                throw;
            }
        }

        /// <summary>
        /// 担当者名取得メソッド
        /// </summary>
        private string GetStaffName(string staffCode)
        {
            try
            {
                var sql = @"
                    SELECT Name 
                    FROM ProductClassification1 
                    WHERE Code = @Code";
                    
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                var name = connection.QueryFirstOrDefault<string>(sql, new { Code = staffCode });
                
                return name ?? $"担当者{staffCode}";
            }
            catch
            {
                return $"担当者{staffCode}";
            }
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
            
            // データソース検証と有効化
            var registeredDataSource = report.GetDataSource("ProductAccount");
            if (registeredDataSource != null)
            {
                // データソースを有効化
                registeredDataSource.Enabled = true;
                
                _logger.LogInformation("データソース登録確認 OK: {Name}", registeredDataSource.Name);
                _logger.LogInformation("データソース行数確認: {Count}", registeredDataSource.RowCount);
                _logger.LogInformation("データソース有効状態: {Enabled}", registeredDataSource.Enabled);
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
            table.Columns.Add("ProductCategory1", typeof(string));       // 担当者コード
            table.Columns.Add("ProductCategory1Name", typeof(string));   // 担当者名
            table.Columns.Add("ProductCode", typeof(string));
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
                
                // 担当者情報
                row["ProductCategory1"] = item.ProductCategory1 ?? "";
                row["ProductCategory1Name"] = item.GetAdditionalInfo("ProductCategory1Name") ?? "";
                
                // 文字列フィールドはそのまま
                row["ProductCode"] = item.ProductCode;
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
            
            // デバッグログを追加
            _logger.LogInformation("CreateDataTable完了: {Count}件のデータを追加", table.Rows.Count);
            
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
        private string GetDisplayCategory(string voucherType, string recordType)
        {
            // 前日残高
            if (recordType == "Previous") return "前残";
            
            // 伝票区分による表示
            return voucherType switch
            {
                "11" => "掛仕",
                "12" => "現仕",
                "51" => "掛売",
                "52" => "現売",
                "71" => GetAdjustmentDisplay(recordType),
                _ => voucherType
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