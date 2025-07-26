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
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Reports.Models;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InventorySystem.Core.Models;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    /// <summary>
    /// 商品勘定帳票のFastReportサービス
    /// </summary>
    public class ProductAccountFastReportService : IProductAccountReportService
    {
        private readonly ILogger<ProductAccountFastReportService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISalesVoucherRepository _salesVoucherRepository;
        private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
        private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly ICustomerMasterRepository _customerMasterRepository;
        private readonly IUnmatchCheckValidationService _unmatchCheckValidationService;
        private readonly string _templatePath;
        
        public ProductAccountFastReportService(
            ILogger<ProductAccountFastReportService> logger,
            IConfiguration configuration,
            ISalesVoucherRepository salesVoucherRepository,
            IPurchaseVoucherRepository purchaseVoucherRepository,
            IInventoryAdjustmentRepository inventoryAdjustmentRepository,
            IInventoryRepository inventoryRepository,
            ICustomerMasterRepository customerMasterRepository,
            IUnmatchCheckValidationService unmatchCheckValidationService)
        {
            _logger = logger;
            _configuration = configuration;
            _salesVoucherRepository = salesVoucherRepository;
            _purchaseVoucherRepository = purchaseVoucherRepository;
            _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
            _inventoryRepository = inventoryRepository;
            _customerMasterRepository = customerMasterRepository;
            _unmatchCheckValidationService = unmatchCheckValidationService;
            
            // テンプレートファイルのパス設定
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "ProductAccount.frx");
            
            _logger.LogInformation("商品勘定帳票テンプレートパス: {Path}", _templatePath);
        }
        
        /// <summary>
        /// 商品勘定帳票を生成（旧式 - アンマッチチェックなし）
        /// </summary>
        public byte[] GenerateProductAccountReport(DateTime jobDate, string? departmentCode = null)
        {
            return GenerateProductAccountReportWithValidation(jobDate, departmentCode, null, skipUnmatchCheck: true);
        }

        /// <summary>
        /// 商品勘定帳票を生成（DataSetId指定・アンマッチチェックあり）
        /// </summary>
        public async Task<byte[]> GenerateProductAccountReportAsync(DateTime jobDate, string dataSetId, string? departmentCode = null, bool skipUnmatchCheck = false)
        {
            return await Task.Run(() => GenerateProductAccountReportWithValidation(jobDate, departmentCode, dataSetId, skipUnmatchCheck));
        }

        /// <summary>
        /// 商品勘定帳票を生成（内部実装）
        /// </summary>
        private byte[] GenerateProductAccountReportWithValidation(DateTime jobDate, string? departmentCode, string? dataSetId, bool skipUnmatchCheck)
        {
            try
            {
                _logger.LogInformation("=== 商品勘定帳票生成開始 ===");
                _logger.LogInformation($"対象日: {jobDate:yyyy-MM-dd}");
                _logger.LogInformation($"部門: {departmentCode ?? "全部門"}");
                _logger.LogInformation($"DataSetId: {dataSetId ?? "未指定"}");
                _logger.LogInformation($"アンマッチチェックスキップ: {skipUnmatchCheck}");

                // アンマッチチェック検証（DataSetId指定時のみ）
                if (!string.IsNullOrEmpty(dataSetId) && !skipUnmatchCheck)
                {
                    _logger.LogInformation("アンマッチチェック検証開始 - DataSetId: {DataSetId}", dataSetId);
                    var validation = _unmatchCheckValidationService.ValidateForReportExecutionAsync(
                        dataSetId, ReportType.ProductAccount).GetAwaiter().GetResult();

                    if (!validation.CanExecute)
                    {
                        _logger.LogError("❌ 商品勘定帳票実行不可 - {ErrorMessage}", validation.ErrorMessage);
                        throw new InvalidOperationException($"商品勘定帳票を実行できません。{validation.ErrorMessage}");
                    }

                    _logger.LogInformation("✅ アンマッチチェック検証合格 - 商品勘定帳票実行を継続します");
                }

                // データを取得・計算
                var reportData = PrepareReportData(jobDate, departmentCode);
                _logger.LogInformation($"レポートデータ件数: {reportData.Count()}");

                // FastReportでPDF生成
                return GeneratePdfReport(reportData, jobDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "商品勘定帳票の生成に失敗しました");
                throw;
            }
        }

        /// <summary>
        /// レポート用データを準備（ストアドプロシージャ使用）
        /// Gemini CLI戦略: データ準備とレポート描画の役割分離
        /// </summary>
        private IEnumerable<ProductAccountReportModel> PrepareReportData(DateTime jobDate, string? departmentCode)
        {
            _logger.LogInformation("ストアドプロシージャによるレポートデータ準備開始");

            var reportModels = new List<ProductAccountReportModel>();

            try
            {
                // ストアドプロシージャ実行でデータを取得
                var connectionString = GetConnectionString();
                
                using var connection = new System.Data.SqlClient.SqlConnection(connectionString);
                connection.Open();
                
                using var command = new System.Data.SqlClient.SqlCommand("sp_CreateProductLedgerData", connection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.CommandTimeout = 300; // 5分タイムアウト
                
                // パラメータ設定
                command.Parameters.AddWithValue("@JobDate", jobDate);
                if (!string.IsNullOrEmpty(departmentCode))
                {
                    command.Parameters.AddWithValue("@DepartmentCode", departmentCode);
                }
                else
                {
                    command.Parameters.AddWithValue("@DepartmentCode", DBNull.Value);
                }

                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    var model = new ProductAccountReportModel
                    {
                        ProductCode = reader.GetString("ProductCode"),
                        ProductName = reader.GetString("ProductName"),
                        ShippingMarkCode = reader.GetString("ShippingMarkCode"),
                        ShippingMarkName = reader.GetString("ShippingMarkName"),
                        ManualShippingMark = reader.GetString("ManualShippingMark"),
                        GradeCode = reader.GetString("GradeCode"),
                        GradeName = reader.IsDBNull("GradeName") ? "" : reader.GetString("GradeName"),
                        ClassCode = reader.GetString("ClassCode"),
                        ClassName = reader.IsDBNull("ClassName") ? "" : reader.GetString("ClassName"),
                        VoucherNumber = reader.GetString("VoucherNumber"),
                        DisplayCategory = reader.GetString("DisplayCategory"),
                        TransactionDate = reader.GetDateTime("TransactionDate"),
                        PurchaseQuantity = reader.GetDecimal("PurchaseQuantity"),
                        SalesQuantity = reader.GetDecimal("SalesQuantity"),
                        RemainingQuantity = reader.GetDecimal("RemainingQuantity"),
                        UnitPrice = reader.GetDecimal("UnitPrice"),
                        Amount = reader.GetDecimal("Amount"),
                        GrossProfit = reader.GetDecimal("GrossProfit"),
                        WalkingDiscount = reader.GetDecimal("WalkingDiscount"),
                        CustomerSupplierName = reader.GetString("CustomerSupplierName"),
                        GroupKey = reader.GetString("GroupKey"),
                        ProductCategory1 = reader.IsDBNull("ProductCategory1") ? null : reader.GetString("ProductCategory1"),
                        ProductCategory5 = reader.IsDBNull("ProductCategory5") ? null : reader.GetString("ProductCategory5"),
                        
                        // 集計用データ（ストアドプロシージャで計算済み）
                        PreviousBalanceQuantity = reader.GetDecimal("PreviousBalance"),
                        TotalPurchaseQuantity = reader.GetDecimal("TotalPurchaseQuantity"),
                        TotalSalesQuantity = reader.GetDecimal("TotalSalesQuantity"),
                        CurrentBalanceQuantity = reader.GetDecimal("CurrentBalance"),
                        InventoryUnitPrice = reader.GetDecimal("InventoryUnitPrice"),
                        InventoryAmount = reader.GetDecimal("InventoryAmount"),
                        TotalGrossProfit = reader.GetDecimal("TotalGrossProfit"),
                        GrossProfitRate = reader.GetDecimal("GrossProfitRate")
                    };

                    // 月日表示を設定
                    model.MonthDayDisplay = reader.GetString("MonthDay");
                    
                    reportModels.Add(model);
                }

                _logger.LogInformation($"ストアドプロシージャから{reportModels.Count}件のデータを取得");
                return reportModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ストアドプロシージャの実行に失敗しました");
                throw;
            }
        }

        /// <summary>
        /// 接続文字列を取得
        /// </summary>
        private string GetConnectionString()
        {
            return _configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        /// <summary>
        /// 粗利益・歩引き金を計算（非推奨：ストアドプロシージャで計算済み）
        /// </summary>
        private void CalculateGrossProfitAndDiscount(ProductAccountReportModel model, Dictionary<string, CustomerMaster> customers)
        {
            // 商品分類5=99999の場合は例外処理
            if (model.IsExceptionCase())
            {
                model.GrossProfit = 0;
                model.WalkingDiscount = 0;
                _logger.LogDebug($"商品分類5=99999の例外処理適用: {model.ProductCode}");
                return;
            }

            // 通常の計算処理
            if (model.RecordType == "Sales")
            {
                // 粗利益 = 売上金額 - (売上数量 × 在庫単価)
                var costAmount = model.SalesQuantity * model.InventoryUnitPrice;
                model.GrossProfit = model.Amount - costAmount;

                // 歩引き金 = 売上金額 × 歩引き率
                if (customers.TryGetValue(model.CustomerSupplierName, out var customer) && customer.WalkingRate.HasValue)
                {
                    model.WalkingDiscount = model.Amount * (customer.WalkingRate.Value / 100);
                }
            }
        }

        /// <summary>
        /// レポートモデルを作成
        /// </summary>
        private ProductAccountReportModel CreateReportModel(object record, string recordType, string? voucherCategory, Dictionary<string, CustomerMaster> customers)
        {
            var model = new ProductAccountReportModel();

            switch (record)
            {
                case SalesVoucher sales:
                    model.ProductCode = sales.ProductCode;
                    model.ProductName = sales.ProductName ?? "";
                    model.ShippingMarkCode = sales.ShippingMarkCode;
                    model.ShippingMarkName = sales.ShippingMarkName;
                    model.ManualShippingMark = sales.ShippingMarkName.PadRight(8).Substring(0, 8);
                    model.GradeCode = sales.GradeCode;
                    model.ClassCode = sales.ClassCode;
                    model.VoucherNumber = sales.VoucherNumber;
                    model.VoucherCategory = sales.VoucherType;
                    model.TransactionDate = sales.VoucherDate;
                    model.SalesQuantity = sales.Quantity;
                    model.UnitPrice = sales.UnitPrice;
                    model.Amount = sales.Amount;
                    model.CustomerSupplierName = sales.CustomerName ?? "";
                    model.ProductCategory1 = sales.ProductCategory1;
                    model.ProductCategory5 = sales.ProductCategory5;
                    model.InventoryUnitPrice = sales.InventoryUnitPrice;
                    model.RecordType = "Sales";
                    break;

                case PurchaseVoucher purchase:
                    model.ProductCode = purchase.ProductCode;
                    model.ProductName = purchase.ProductName ?? "";
                    model.ShippingMarkCode = purchase.ShippingMarkCode;
                    model.ShippingMarkName = purchase.ShippingMarkName;
                    model.ManualShippingMark = purchase.ShippingMarkName.PadRight(8).Substring(0, 8);
                    model.GradeCode = purchase.GradeCode;
                    model.ClassCode = purchase.ClassCode;
                    model.VoucherNumber = purchase.VoucherNumber;
                    model.VoucherCategory = purchase.VoucherType;
                    model.TransactionDate = purchase.VoucherDate;
                    model.PurchaseQuantity = purchase.Quantity;
                    model.UnitPrice = purchase.UnitPrice;
                    model.Amount = purchase.Amount;
                    model.CustomerSupplierName = purchase.SupplierName ?? "";
                    model.RecordType = "Purchase";
                    break;

                case InventoryAdjustment adjustment:
                    model.ProductCode = adjustment.ProductCode;
                    model.ProductName = adjustment.ProductName ?? "";
                    model.ShippingMarkCode = adjustment.ShippingMarkCode;
                    model.ShippingMarkName = adjustment.ShippingMarkName;
                    model.ManualShippingMark = adjustment.ShippingMarkName.PadRight(8).Substring(0, 8);
                    model.GradeCode = adjustment.GradeCode;
                    model.ClassCode = adjustment.ClassCode;
                    model.VoucherNumber = adjustment.VoucherNumber;
                    model.VoucherCategory = "71";
                    model.TransactionDate = adjustment.VoucherDate;
                    model.UnitPrice = adjustment.UnitPrice;
                    model.Amount = adjustment.Amount;
                    model.RecordType = GetAdjustmentType(adjustment);
                    
                    // 在庫調整の数量は調整区分により売上・仕入のどちらかに設定
                    if (adjustment.AdjustmentCategory == "1" || adjustment.AdjustmentCategory == "6") // ロス・調整
                    {
                        model.SalesQuantity = Math.Abs(adjustment.Quantity); // 出庫として扱う
                    }
                    else
                    {
                        model.PurchaseQuantity = adjustment.Quantity; // 入庫として扱う
                    }
                    break;
            }

            model.GenerateGroupKey();
            model.GenerateSortKey();
            model.DisplayCategory = model.GetDisplayCategory();

            return model;
        }

        /// <summary>
        /// PDFレポートを生成
        /// </summary>
        private byte[] GeneratePdfReport(IEnumerable<ProductAccountReportModel> reportData, DateTime jobDate)
        {
            var report = new FR.Report();
            
            try
            {
                // テンプレートファイルの存在確認
                if (!File.Exists(_templatePath))
                {
                    throw new FileNotFoundException($"テンプレートファイルが見つかりません: {_templatePath}");
                }

                // アンマッチリストと同じ初期化処理
                report.ReportResourceString = "";
                report.FileName = _templatePath;
                
                _logger.LogInformation("レポートテンプレートを読み込んでいます...");
                report.Load(_templatePath);
                
                // .NET 8対応: ScriptLanguageを強制的にNoneに設定（アンマッチリストと同じパターン）
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

                // データテーブル作成
                var dataTable = CreateDataTable(reportData);
                report.RegisterData(dataTable, "ProductAccount");
                
                // データソースを明示的に取得して設定
                var dataSource = report.GetDataSource("ProductAccount");
                if (dataSource != null)
                {
                    dataSource.Enabled = true;
                    _logger.LogInformation("データソースを有効化しました");
                }
                else
                {
                    _logger.LogWarning("データソース 'ProductAccount' が見つかりません");
                }

                // レポートパラメータ設定
                _logger.LogInformation("レポートパラメータを設定しています...");
                report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("GeneratedAt", DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分ss秒"));

                // レポート準備・生成
                _logger.LogInformation("レポートを生成しています...");
                report.Prepare();

                // PDF出力設定（アンマッチリストと同じパターン）
                using var pdfExport = new PDFExport
                {
                    // 日本語フォントの埋め込み（重要）
                    EmbeddingFonts = true,
                    
                    // PDFのメタデータ
                    Title = $"商品勘定帳票_{jobDate:yyyyMMdd}",
                    Subject = "商品勘定帳票",
                    Creator = "在庫管理システム",
                    Author = "在庫管理システム",
                    
                    // 文字エンコーディング設定
                    TextInCurves = false,  // テキストをパスに変換しない
                    
                    // 画質設定
                    JpegQuality = 95,
                    
                    // セキュリティ設定なし
                    OpenAfterExport = false
                };
                
                using var stream = new MemoryStream();
                pdfExport.Export(report, stream);
                
                _logger.LogInformation($"商品勘定帳票PDF生成完了: {stream.Length} bytes");
                return stream.ToArray();
            }
            finally
            {
                report?.Dispose();
            }
        }

        /// <summary>
        /// レポート用データテーブルを作成
        /// </summary>
        private DataTable CreateDataTable(IEnumerable<ProductAccountReportModel> reportData)
        {
            var table = new DataTable("ProductAccount");

            // カラム定義
            table.Columns.Add("ProductCode", typeof(string));
            table.Columns.Add("ProductName", typeof(string));
            table.Columns.Add("ShippingMarkCode", typeof(string));
            table.Columns.Add("ShippingMarkName", typeof(string));
            table.Columns.Add("ManualShippingMark", typeof(string));
            table.Columns.Add("GradeCode", typeof(string));
            table.Columns.Add("GradeName", typeof(string));
            table.Columns.Add("ClassCode", typeof(string));
            table.Columns.Add("ClassName", typeof(string));
            table.Columns.Add("VoucherNumber", typeof(string));
            table.Columns.Add("DisplayCategory", typeof(string));
            table.Columns.Add("MonthDay", typeof(string));
            table.Columns.Add("PurchaseQuantity", typeof(decimal));
            table.Columns.Add("SalesQuantity", typeof(decimal));
            table.Columns.Add("RemainingQuantity", typeof(decimal));
            table.Columns.Add("UnitPrice", typeof(decimal));
            table.Columns.Add("Amount", typeof(decimal));
            table.Columns.Add("GrossProfit", typeof(decimal));
            table.Columns.Add("WalkingDiscount", typeof(decimal));
            table.Columns.Add("CustomerSupplierName", typeof(string));
            table.Columns.Add("GroupKey", typeof(string));
            
            // 集計用カラム追加（仕様書対応）
            table.Columns.Add("PreviousBalance", typeof(decimal));
            table.Columns.Add("TotalPurchaseQuantity", typeof(decimal));
            table.Columns.Add("TotalSalesQuantity", typeof(decimal));
            table.Columns.Add("CurrentBalance", typeof(decimal));
            table.Columns.Add("InventoryUnitPrice", typeof(decimal));
            table.Columns.Add("InventoryAmount", typeof(decimal));
            table.Columns.Add("TotalGrossProfit", typeof(decimal));
            table.Columns.Add("GrossProfitRate", typeof(decimal));

            // データ行追加
            foreach (var item in reportData)
            {
                var row = table.NewRow();
                row["ProductCode"] = item.ProductCode;
                row["ProductName"] = item.ProductName;
                row["ShippingMarkCode"] = item.ShippingMarkCode;
                row["ShippingMarkName"] = item.ShippingMarkName;
                row["ManualShippingMark"] = item.ManualShippingMark;
                row["GradeCode"] = item.GradeCode;
                row["GradeName"] = item.GradeName;
                row["ClassCode"] = item.ClassCode;
                row["ClassName"] = item.ClassName;
                row["VoucherNumber"] = item.VoucherNumber;
                row["DisplayCategory"] = item.DisplayCategory;
                row["MonthDay"] = item.MonthDayDisplay;
                row["PurchaseQuantity"] = item.PurchaseQuantity;
                row["SalesQuantity"] = item.SalesQuantity;
                row["RemainingQuantity"] = item.RemainingQuantity;
                row["UnitPrice"] = item.UnitPrice;
                row["Amount"] = item.Amount;
                row["GrossProfit"] = item.GrossProfit;
                row["WalkingDiscount"] = item.WalkingDiscount;
                row["CustomerSupplierName"] = item.CustomerSupplierName;
                row["GroupKey"] = item.GroupKey;
                
                // 集計用データ（仕様書対応）
                row["PreviousBalance"] = item.PreviousBalanceQuantity;
                row["TotalPurchaseQuantity"] = item.TotalPurchaseQuantity;
                row["TotalSalesQuantity"] = item.TotalSalesQuantity;
                row["CurrentBalance"] = item.CurrentBalanceQuantity;
                row["InventoryUnitPrice"] = item.InventoryUnitPrice;
                row["InventoryAmount"] = item.InventoryAmount;
                row["TotalGrossProfit"] = item.TotalGrossProfit;
                row["GrossProfitRate"] = item.GrossProfitRate;
                
                table.Rows.Add(row);
            }

            _logger.LogInformation($"DataTable作成完了: {table.Rows.Count}行");
            return table;
        }

        // ヘルパーメソッド
        private string GenerateGroupKey(string productCode, string shippingMarkCode, string gradeCode, string classCode)
        {
            return $"{productCode}_{shippingMarkCode}_{gradeCode}_{classCode}";
        }

        private InventoryKey GetInventoryKeyFromRecord(object record)
        {
            return record switch
            {
                SalesVoucher s => s.GetInventoryKey(),
                PurchaseVoucher p => p.GetInventoryKey(),
                InventoryAdjustment a => a.GetInventoryKey(),
                _ => throw new ArgumentException("Unsupported record type")
            };
        }

        private DateTime GetTransactionDate(object record)
        {
            return record switch
            {
                SalesVoucher s => s.VoucherDate,
                PurchaseVoucher p => p.VoucherDate,
                InventoryAdjustment a => a.VoucherDate,
                _ => DateTime.MinValue
            };
        }

        private (decimal quantity, decimal amount) GetPreviousBalance(InventoryKey key, DateTime jobDate)
        {
            // 在庫マスタから前日残高を取得（実装詳細は省略）
            // 実際の実装では InventoryRepository を使用
            return (0, 0);
        }

        private string GetAdjustmentType(InventoryAdjustment adjustment)
        {
            return adjustment.AdjustmentCategory switch
            {
                "1" => "Loss",
                "4" => "Transfer", 
                "6" => "Adjustment",
                _ => "Other"
            };
        }

        private void CalculateRunningBalances(List<ProductAccountReportModel> models)
        {
            // グループ別の累積残高計算（実装詳細は省略）
            // 各グループで前残高から開始して取引ごとに残高を更新
        }

    }
}
#endif
#pragma warning restore CA1416