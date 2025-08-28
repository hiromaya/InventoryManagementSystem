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
                
                // 商品勘定フラットデータを生成（C#側完全制御）
                var flatData = GenerateFlatData(jobDate, departmentCode);
                
                if (!flatData.Any())
                {
                    _logger.LogWarning("商品勘定フラットデータが0件です");
                    // 空のPDFを返す or 例外を投げる
                    throw new InvalidOperationException("商品勘定データが存在しません");
                }
                
                // PDF生成処理
                var pdfBytes = GeneratePdfReportFromFlatData(flatData, jobDate);
                
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
        /// 商品勘定フラットデータの生成（C#側完全制御）
        /// </summary>
        private List<ProductAccountFlatRow> GenerateFlatData(DateTime jobDate, string? departmentCode)
        {
            _logger.LogInformation("商品勘定フラットデータ生成開始 - JobDate: {JobDate}", jobDate);
            
            // 元データを取得
            var sourceData = PrepareReportData(jobDate, departmentCode).ToList();
            
            if (!sourceData.Any())
            {
                _logger.LogWarning("商品勘定の元データが0件です");
                return new List<ProductAccountFlatRow>();
            }
            
            var flatRows = new List<ProductAccountFlatRow>();
            int sequence = 0;
            
            // 担当者でグループ化（ProductCategory1）
            // 重要：先にソートしてからグループ化（ORDER BY句がGroupBy後に効果を失う問題を解決）
            var staffGroups = sourceData
                .OrderBy(x => x.ProductCategory1 ?? "")  // 先にソート
                .ThenBy(x => x.ProductCode)
                .GroupBy(x => new 
                { 
                    StaffCode = x.ProductCategory1 ?? "", 
                    StaffName = x.GetAdditionalInfo("ProductCategory1Name") ?? ""
                });
            
            bool isFirstStaff = true;
            
            foreach (var staffGroup in staffGroups)
            {
                var staffCode = staffGroup.Key.StaffCode;
                var staffName = staffGroup.Key.StaffName;
                
                // 空の担当者コードの場合のログを調整
                if (string.IsNullOrWhiteSpace(staffCode))
                {
                    _logger.LogInformation("担当者コードなしのグループ処理中 ({Count}件)", staffGroup.Count());
                }
                else
                {
                    _logger.LogInformation("担当者処理中: {StaffCode} {StaffName} ({Count}件)", 
                        staffCode, staffName, staffGroup.Count());
                }
                
                // 改ページ制御（最初の担当者以外）
                if (!isFirstStaff)
                {
                    flatRows.Add(CreatePageBreakRow(staffCode, staffName, sequence++));
                }
                isFirstStaff = false;
                
                // 商品でグループ化（5項目複合キー）
                var productGroups = staffGroup
                    .GroupBy(x => new 
                    { 
                        x.ProductCode, 
                        x.ShippingMarkCode,
                        x.ManualShippingMark,
                        x.GradeCode,
                        x.ClassCode
                    })
                    .OrderBy(g => g.Key.ProductCode)
                    .ThenBy(g => g.Key.ShippingMarkCode)
                    .ThenBy(g => g.Key.ManualShippingMark)
                    .ThenBy(g => g.Key.GradeCode)
                    .ThenBy(g => g.Key.ClassCode);
                
                foreach (var productGroup in productGroups)
                {
                    // 商品グループヘッダーは削除（不要）
                    
                    // 前残行の作成（必要に応じて）
                    var previousBalance = CreatePreviousBalanceIfNeeded(productGroup.Key, sequence);
                    if (previousBalance != null)
                    {
                        flatRows.Add(previousBalance);
                        sequence++;
                    }
                    
                    // 明細行（日付・伝票番号順）
                    var details = productGroup
                        .Where(x => x.RecordType != "Previous") // 前残は別途処理
                        .OrderBy(x => x.TransactionDate)
                        .ThenBy(x => x.VoucherNumber);
                    
                    // 集計用変数
                    decimal subtotalPreviousBalance = 0;  // 追加
                    decimal subtotalPurchase = 0;
                    decimal subtotalSales = 0;
                    decimal subtotalCurrentBalance = 0;   // 追加
                    decimal subtotalInventoryUnitPrice = 0; // 追加
                    decimal subtotalInventoryAmount = 0;
                    decimal subtotalGrossProfit = 0;
                    
                    // CP在庫マスタから前日残と在庫単価を取得
                    var cpInventoryData = GetCpInventoryData(productGroup.Key, jobDate);
                    if (cpInventoryData != null)
                    {
                        subtotalPreviousBalance = cpInventoryData.PreviousDayStock ?? 0;
                        subtotalInventoryUnitPrice = cpInventoryData.DailyUnitPrice ?? 0;  // 修正
                        subtotalInventoryAmount = cpInventoryData.DailyStockAmount ?? 0;   // 修正
                    }
                    
                    foreach (var detail in details)
                    {
                        // 各明細行に商品情報をすべて含める（担当者情報も含む）
                        flatRows.Add(CreateDetailRowWithProductInfo(detail, productGroup.Key, staffCode, staffName, sequence++));
                        
                        // 小計集計
                        subtotalPurchase += detail.PurchaseQuantity;
                        subtotalSales += detail.SalesQuantity;
                        subtotalGrossProfit += detail.GrossProfit;
                    }
                    
                    // 当日残を計算
                    subtotalCurrentBalance = subtotalPreviousBalance + subtotalPurchase - subtotalSales;
                    
                    // 粗利率を計算
                    decimal grossProfitRate = 0;
                    if (subtotalSales != 0 && subtotalInventoryUnitPrice != 0)
                    {
                        decimal salesAmount = subtotalSales * subtotalInventoryUnitPrice;
                        if (salesAmount != 0)
                        {
                            grossProfitRate = Math.Round((subtotalGrossProfit / salesAmount) * 100, 2);
                        }
                    }
                    
                    // 商品別小計（2行構成）
                    if (subtotalPurchase != 0 || subtotalSales != 0 || subtotalInventoryAmount != 0)
                    {
                        // 見出し行
                        flatRows.Add(CreateProductSubtotalHeader(sequence++));
                        
                        // 数値行
                        flatRows.Add(CreateProductSubtotal(
                            subtotalPreviousBalance,
                            subtotalPurchase, 
                            subtotalSales,
                            subtotalCurrentBalance,
                            subtotalInventoryUnitPrice,
                            subtotalInventoryAmount,
                            subtotalGrossProfit,
                            grossProfitRate,
                            sequence++));
                    }
                    
                    // 空行
                    flatRows.Add(CreateBlankRow(sequence++));
                }
                
            }
            
            _logger.LogInformation("フラットデータ生成完了: {Count}行", flatRows.Count);
            return flatRows;
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
                _logger.LogCritical("=== GetReportDataDirectly メソッド使用（修正版） ===");
                // 直接データ取得を優先使用（等級名・階級名修正版）
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
            _logger.LogCritical("=== ExecuteStoredProcedure メソッド実行中（非推奨） ===");
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
                    SortKey = ""
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
            
            // デバッグログ: ストアドプロシージャデータ確認
            _logger.LogCritical("=== ストアドプロシージャデータ確認 ===");
            _logger.LogCritical($"取得件数: {reportModels.Count}");
            if (reportModels.Count > 0)
            {
                var firstItem = reportModels.First();
                _logger.LogCritical($"1件目のデータ確認:");
                _logger.LogCritical($"  ProductCode: {firstItem.ProductCode}");
                _logger.LogCritical($"  GradeCode: {firstItem.GradeCode}");
                _logger.LogCritical($"  GradeName: '{firstItem.GradeName}'");
                _logger.LogCritical($"  ClassCode: {firstItem.ClassCode}");
                _logger.LogCritical($"  ClassName: '{firstItem.ClassName}'");
            }
            
            return reportModels;
        }

        /// <summary>
        /// 直接データ取得メソッド（フォールバック用）
        /// </summary>
        private IEnumerable<ProductAccountReportModel> GetReportDataDirectly(DateTime jobDate, string? departmentCode)
        {
            // === デバッグ開始ログ ===
            _logger.LogCritical("=== 商品勘定 荷印名デバッグ開始 ===");
            _logger.LogCritical("JobDate: {JobDate}, DepartmentCode: {DeptCode}", jobDate, departmentCode ?? "全部門");
            _logger.LogCritical("=== GetReportDataDirectly メソッド実行中（修正版） ===");
            _logger.LogInformation("直接データ取得モードで商品勘定データを準備します");
            
            var reportModels = new List<ProductAccountReportModel>();
            
            try
            {
                // 簡易的な実装 - 販売伝票データから取得
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                
                // === デバッグ: CP在庫マスタ存在確認 ===
                try
                {
                    var cpInventoryCheckSql = @"
                        SELECT 
                            COUNT(*) as TotalRecords,
                            COUNT(CASE WHEN ShippingMarkName IS NOT NULL AND ShippingMarkName != '' THEN 1 END) as ValidShippingMarkRecords,
                            COUNT(CASE WHEN ShippingMarkName IS NULL OR ShippingMarkName = '' THEN 1 END) as EmptyShippingMarkRecords
                        FROM CpInventoryMaster 
                        WHERE JobDate = @JobDate";
                    
                    var cpCheckResult = connection.QueryFirstOrDefault(cpInventoryCheckSql, new { JobDate = jobDate });
                    
                    _logger.LogCritical("CP在庫マスタ存在確認: 総件数={Total}, 荷印名有効={Valid}, 荷印名空={Empty}", 
                        (int)(cpCheckResult?.TotalRecords ?? 0), 
                        (int)(cpCheckResult?.ValidShippingMarkRecords ?? 0), 
                        (int)(cpCheckResult?.EmptyShippingMarkRecords ?? 0));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CP在庫マスタ存在確認クエリでエラーが発生");
                }
                
                // === デバッグ: JOIN条件チェック（売上伝票とCP在庫マスタ） ===
                try
                {
                    var joinCheckSql = @"
                        SELECT TOP 10
                            s.ProductCode as Sales_ProductCode,
                            cp.ProductCode as CP_ProductCode,
                            s.GradeCode as Sales_GradeCode, 
                            cp.GradeCode as CP_GradeCode,
                            s.ClassCode as Sales_ClassCode,
                            cp.ClassCode as CP_ClassCode,
                            s.ShippingMarkCode as Sales_ShippingMarkCode,
                            cp.ShippingMarkCode as CP_ShippingMarkCode,
                            s.ManualShippingMark as Sales_ManualShippingMark,
                            cp.ManualShippingMark as CP_ManualShippingMark,
                            cp.JobDate as CP_JobDate,
                            cp.ShippingMarkName as CP_ShippingMarkName,
                            CASE 
                                WHEN s.ProductCode = cp.ProductCode AND
                                     s.GradeCode = cp.GradeCode AND 
                                     s.ClassCode = cp.ClassCode AND
                                     s.ShippingMarkCode = cp.ShippingMarkCode AND
                                     s.ManualShippingMark = cp.ManualShippingMark AND
                                     cp.JobDate = @JobDate
                                THEN 'JOIN成功'
                                ELSE 'JOIN失敗'
                            END as JoinResult
                        FROM SalesVouchers s
                        FULL OUTER JOIN CpInventoryMaster cp ON s.ProductCode = cp.ProductCode 
                          AND s.GradeCode = cp.GradeCode 
                          AND s.ClassCode = cp.ClassCode 
                          AND s.ShippingMarkCode = cp.ShippingMarkCode 
                          AND s.ManualShippingMark = cp.ManualShippingMark
                          AND cp.JobDate = @JobDate
                        WHERE s.JobDate = @JobDate
                          AND s.DetailType = '1' 
                          AND s.IsActive = 1
                        ORDER BY s.VoucherNumber";
                    
                    var joinCheckResults = connection.Query(joinCheckSql, new { JobDate = jobDate });
                    
                    _logger.LogCritical("JOIN条件チェック結果（最初の10件）:");
                    foreach (var result in joinCheckResults)
                    {
                        _logger.LogCritical("商品={ProductCode} JOIN結果={JoinResult} 荷印名={ShippingMarkName}", 
                            (string)(result.Sales_ProductCode ?? result.CP_ProductCode), 
                            (string)result.JoinResult,
                            (string)(result.CP_ShippingMarkName ?? ""));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "JOIN条件チェッククエリでエラーが発生");
                }
                
                // === デバッグ: 文字列比較詳細チェック ===
                try
                {
                    var stringCheckSql = @"
                        SELECT TOP 5
                            s.ProductCode,
                            s.ManualShippingMark as Sales_Manual,
                            cp.ManualShippingMark as CP_Manual,
                            LEN(s.ManualShippingMark) as Sales_Length,
                            LEN(cp.ManualShippingMark) as CP_Length,
                            ASCII(SUBSTRING(s.ManualShippingMark, 1, 1)) as Sales_FirstChar_ASCII,
                            ASCII(SUBSTRING(cp.ManualShippingMark, 1, 1)) as CP_FirstChar_ASCII,
                            CASE WHEN s.ManualShippingMark = cp.ManualShippingMark THEN '一致' ELSE '不一致' END as CompareResult,
                            cp.ShippingMarkName
                        FROM SalesVouchers s, CpInventoryMaster cp
                        WHERE s.ProductCode = cp.ProductCode
                          AND s.GradeCode = cp.GradeCode
                          AND s.ClassCode = cp.ClassCode  
                          AND s.ShippingMarkCode = cp.ShippingMarkCode
                          AND s.JobDate = @JobDate
                          AND cp.JobDate = @JobDate
                          AND s.DetailType = '1'
                          AND s.IsActive = 1";
                    
                    var stringCheckResults = connection.Query(stringCheckSql, new { JobDate = jobDate });
                    
                    _logger.LogCritical("文字列比較詳細チェック結果（最初の5件）:");
                    foreach (var result in stringCheckResults)
                    {
                        _logger.LogCritical("商品={ProductCode} Sales手入力='{SalesManual}'({SalesLen}) CP手入力='{CpManual}'({CpLen}) 比較結果={CompareResult} 荷印名='{ShippingMarkName}'", 
                            (string)result.ProductCode,
                            (string)(result.Sales_Manual ?? ""),
                            (int)(result.Sales_Length ?? 0),
                            (string)(result.CP_Manual ?? ""),
                            (int)(result.CP_Length ?? 0),
                            (string)result.CompareResult,
                            (string)(result.ShippingMarkName ?? ""));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "文字列比較詳細チェッククエリでエラーが発生");
                }
                
                var sql = @"
                    WITH ProductAccount AS (
                        -- 前残高データ（CP在庫マスタから）
                        SELECT 
                            cp.ProductCode,
                            ISNULL(cp.ProductName, '') as ProductName,
                            ISNULL(cp.ProductCategory1, '') as ProductCategory1,
                            cp.ShippingMarkCode,
                            cp.ShippingMarkName,
                            cp.ManualShippingMark,
                            cp.GradeCode,
                            cp.GradeName,
                            cp.ClassCode,
                            cp.ClassName,
                            '' as VoucherNumber,
                            '' as VoucherType,
                            '前残' as DisplayCategory,
                            @JobDate as TransactionDate,
                            0 as PurchaseQuantity,
                            0 as SalesQuantity,
                            cp.PreviousDayStock as RemainingQuantity,
                            cp.PreviousDayUnitPrice as UnitPrice,
                            cp.PreviousDayStockAmount as Amount,
                            0 as GrossProfit,
                            '' as CustomerSupplierName,
                            'Previous' as RecordType  -- 重要：前残高のRecordType
                        FROM CpInventoryMaster cp
                        WHERE cp.JobDate = @JobDate
                          AND (@DepartmentCode IS NULL OR cp.ProductCategory1 = @DepartmentCode)
                          AND cp.PreviousDayStock <> 0
                        
                        UNION ALL
                        
                        -- 売上伝票データ
                        SELECT 
                            s.ProductCode,
                            ISNULL(s.ProductName, pm.ProductName) as ProductName,  -- 伝票の商品名、またはマスタから取得
                            ISNULL(cp.ProductCategory1, '00') as ProductCategory1,  -- CP在庫マスタから担当者取得
                            s.ShippingMarkCode,
                            cp.ShippingMarkName,
                            s.ManualShippingMark,  -- 売上伝票の手入力値
                            s.GradeCode,
                            cp.GradeName,
                            s.ClassCode,
                            cp.ClassName,
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
                            s.CustomerName as CustomerSupplierName,
                            'Sales' as RecordType
                        FROM SalesVouchers s
                        LEFT JOIN ProductMaster pm ON s.ProductCode = pm.ProductCode
                        LEFT JOIN CpInventoryMaster cp ON s.ProductCode = cp.ProductCode 
                          AND s.GradeCode = cp.GradeCode 
                          AND s.ClassCode = cp.ClassCode 
                          AND s.ShippingMarkCode = cp.ShippingMarkCode 
                          AND s.ManualShippingMark = cp.ManualShippingMark
                          AND cp.JobDate = @JobDate
                        WHERE s.JobDate = @JobDate
                          AND s.DetailType = '1'
                          AND s.IsActive = 1
                        
                        UNION ALL
                        
                        -- 仕入伝票データ
                        SELECT 
                            p.ProductCode,
                            ISNULL(p.ProductName, pm.ProductName) as ProductName,  -- 伝票の商品名、またはマスタから取得
                            ISNULL(cp.ProductCategory1, '00') as ProductCategory1,
                            p.ShippingMarkCode,
                            cp.ShippingMarkName,
                            p.ManualShippingMark,  -- 仕入伝票の手入力値
                            p.GradeCode,
                            cp.GradeName,
                            p.ClassCode,
                            cp.ClassName,
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
                            p.SupplierName as CustomerSupplierName,
                            'Purchase' as RecordType
                        FROM PurchaseVouchers p
                        LEFT JOIN ProductMaster pm ON p.ProductCode = pm.ProductCode
                        LEFT JOIN CpInventoryMaster cp ON p.ProductCode = cp.ProductCode 
                          AND p.GradeCode = cp.GradeCode 
                          AND p.ClassCode = cp.ClassCode 
                          AND p.ShippingMarkCode = cp.ShippingMarkCode 
                          AND p.ManualShippingMark = cp.ManualShippingMark
                          AND cp.JobDate = @JobDate
                        WHERE p.JobDate = @JobDate
                          AND p.DetailType = '1'
                          AND p.IsActive = 1
                        
                        UNION ALL
                        
                        -- 在庫調整データ
                        SELECT 
                            ia.ProductCode,
                            ISNULL(ia.ProductName, pm.ProductName) as ProductName,  -- 伝票の商品名、またはマスタから取得
                            ISNULL(cp.ProductCategory1, '00') as ProductCategory1,
                            ia.ShippingMarkCode,
                            cp.ShippingMarkName,
                            ia.ManualShippingMark,  -- 在庫調整の手入力値
                            ia.GradeCode,
                            cp.GradeName,
                            ia.ClassCode,
                            cp.ClassName,
                            ia.VoucherNumber,
                            ia.VoucherType,
                            CASE ia.CategoryCode
                                WHEN 1 THEN 'ロス'
                                WHEN 4 THEN '振替'
                                WHEN 6 THEN '調整'
                                ELSE 'その他'
                            END as DisplayCategory,
                            ia.VoucherDate as TransactionDate,
                            CASE WHEN ia.Quantity > 0 THEN ia.Quantity ELSE 0 END as PurchaseQuantity,
                            CASE WHEN ia.Quantity < 0 THEN ABS(ia.Quantity) ELSE 0 END as SalesQuantity,
                            0 as RemainingQuantity,
                            ia.UnitPrice,
                            ia.Amount,
                            0 as GrossProfit,
                            '' as CustomerSupplierName,
                            'Adjustment' as RecordType
                        FROM InventoryAdjustments ia
                        LEFT JOIN ProductMaster pm ON ia.ProductCode = pm.ProductCode
                        LEFT JOIN CpInventoryMaster cp ON ia.ProductCode = cp.ProductCode 
                          AND ia.GradeCode = cp.GradeCode 
                          AND ia.ClassCode = cp.ClassCode 
                          AND ia.ShippingMarkCode = cp.ShippingMarkCode 
                          AND ia.ManualShippingMark = cp.ManualShippingMark
                          AND cp.JobDate = @JobDate
                        WHERE ia.JobDate = @JobDate
                          AND ia.IsActive = 1
                    )
                    SELECT * FROM ProductAccount
                    ORDER BY ProductCategory1, ProductCode, ShippingMarkCode, ManualShippingMark, 
                             GradeCode, ClassCode, TransactionDate, VoucherNumber";
                
                var results = connection.Query<ProductAccountReportModel>(sql, new { 
                    JobDate = jobDate, 
                    DepartmentCode = departmentCode 
                });
                
                // === デバッグ: メインクエリ結果確認 ===
                _logger.LogCritical("メインクエリ実行完了: 取得件数={Count}", results?.Count() ?? 0);
                
                if (results?.Any() == true)
                {
                    var shippingMarkData = results.Where(r => !string.IsNullOrEmpty(r.ShippingMarkName)).Take(5);
                    _logger.LogCritical("荷印名が設定されているデータ（最初の5件）:");
                    foreach (var item in shippingMarkData)
                    {
                        _logger.LogCritical("商品={ProductCode} 荷印コード={ShippingMarkCode} 荷印名='{ShippingMarkName}' 手入力='{ManualShippingMark}'", 
                            item.ProductCode, 
                            item.ShippingMarkCode, 
                            item.ShippingMarkName ?? "",
                            item.ManualShippingMark ?? "");
                    }
                    
                    var emptyShippingMarkCount = results.Count(r => string.IsNullOrEmpty(r.ShippingMarkName));
                    _logger.LogCritical("荷印名が空または未設定のデータ件数: {EmptyCount}/{TotalCount}", emptyShippingMarkCount, results.Count());
                }
                
                // デバッグログ: SQLクエリ実行直後
                _logger.LogCritical("=== 商品勘定SQLクエリ結果確認 ===");
                var resultsList = results.ToList();
                if (resultsList.Any())
                {
                    var firstItem = resultsList.First();
                    _logger.LogCritical($"1件目のデータ確認:");
                    _logger.LogCritical($"  ProductCode: {firstItem.ProductCode}");
                    _logger.LogCritical($"  GradeCode: {firstItem.GradeCode}");
                    _logger.LogCritical($"  GradeName: '{firstItem.GradeName}'");  // 空文字確認のため''で囲む
                    _logger.LogCritical($"  ClassCode: {firstItem.ClassCode}");
                    _logger.LogCritical($"  ClassName: '{firstItem.ClassName}'");  // 空文字確認のため''で囲む
                }
                _logger.LogCritical($"取得件数: {resultsList.Count}");
                
                foreach (var model in resultsList)
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
        /// 担当者名取得メソッド（ProductCategory1Masterテーブルから）
        /// Migration 065対応: CategoryCodeを文字列として直接比較
        /// </summary>
        private string GetStaffName(string categoryCode)
        {
            try
            {
                // 空文字列チェック
                if (string.IsNullOrWhiteSpace(categoryCode))
                {
                    _logger.LogWarning("商品分類１コードが空です");
                    return ""; // 空文字列を返す
                }

                // ProductCategory1Master（商品分類１マスタ）から名称を取得
                // Migration 065後：CategoryCodeはNVARCHAR(3)なので文字列として直接比較
                var sql = @"
                    SELECT CategoryName 
                    FROM ProductCategory1Master 
                    WHERE CategoryCode = @CategoryCode";
                    
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                var categoryName = connection.QueryFirstOrDefault<string>(sql, new { CategoryCode = categoryCode });
                
                if (string.IsNullOrEmpty(categoryName))
                {
                    _logger.LogWarning("商品分類１名称が見つかりません。コード: {CategoryCode}", categoryCode);
                    return "";
                }
                
                _logger.LogDebug("商品分類１名称取得成功: {CategoryCode} → {CategoryName}", categoryCode, categoryName);
                return categoryName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "商品分類１名称の取得に失敗しました。コード: {CategoryCode}", categoryCode);
                return "";
            }
        }
        
        /// <summary>
        /// CP在庫マスタからデータ取得
        /// </summary>
        private dynamic GetCpInventoryData(dynamic productKey, DateTime jobDate)
        {
            try
            {
                var sql = @"
                    SELECT 
                        PreviousDayStock,
                        DailyUnitPrice,      -- 修正：CurrentStockUnitPrice → DailyUnitPrice
                        DailyStock,          -- 修正：CurrentStock → DailyStock
                        DailyStockAmount     -- 修正：CurrentStockAmount → DailyStockAmount
                    FROM CpInventoryMaster
                    WHERE ProductCode = @ProductCode
                      AND GradeCode = @GradeCode
                      AND ClassCode = @ClassCode
                      AND ShippingMarkCode = @ShippingMarkCode
                      AND ManualShippingMark = @ManualShippingMark
                      AND JobDate = @JobDate";
                      
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                return connection.QueryFirstOrDefault(sql, new
                {
                    ProductCode = (string)productKey.ProductCode,
                    GradeCode = (string)productKey.GradeCode,
                    ClassCode = (string)productKey.ClassCode,
                    ShippingMarkCode = (string)productKey.ShippingMarkCode,
                    ManualShippingMark = (string)productKey.ManualShippingMark,
                    JobDate = jobDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CP在庫マスタデータの取得に失敗しました");
                return null;
            }
        }
        
        /// <summary>
        /// PDF生成処理（アンマッチリストのパターンを踏襲）
        /// </summary>
        /// <summary>
        /// PDFレポート生成（フラットデータ用、C#側完全制御）
        /// </summary>
        private byte[] GeneratePdfReportFromFlatData(List<ProductAccountFlatRow> flatData, DateTime jobDate)
        {
            using var report = new FR.Report();
            
            // FastReportの設定
            report.ReportResourceString = "";
            report.FileName = _templatePath;
            
            // テンプレートファイルを読み込む
            _logger.LogInformation("商品勘定レポートテンプレートを読み込んでいます...");
            report.Load(_templatePath);
            
            // .NET 8対応: ScriptLanguageを強制的にNoneに設定
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
            }
            
            // フラットデータをDataTableに変換
            var dataTable = CreateFlatDataTable(flatData);
            
            // デバッグログ: フラットデータ版DataTable確認
            _logger.LogCritical("=== フラットデータ版DataTable確認 ===");
            _logger.LogCritical($"DataTable行数: {dataTable.Rows.Count}");
            if (dataTable.Rows.Count > 0)
            {
                var firstRow = dataTable.Rows[0];
                _logger.LogCritical($"1行目のGradeName: '{firstRow["GradeName"]}'");
                _logger.LogCritical($"1行目のClassName: '{firstRow["ClassName"]}'");
                
                // 空でないデータを探す
                int nonEmptyCount = 0;
                foreach (DataRow dr in dataTable.Rows)
                {
                    if (!string.IsNullOrEmpty(dr["GradeName"]?.ToString()) || 
                        !string.IsNullOrEmpty(dr["ClassName"]?.ToString()))
                    {
                        nonEmptyCount++;
                        if (nonEmptyCount == 1)  // 最初の非空データのみログ
                        {
                            _logger.LogCritical($"非空データ発見:");
                            _logger.LogCritical($"  行番号: {dataTable.Rows.IndexOf(dr)}");
                            _logger.LogCritical($"  GradeName: '{dr["GradeName"]}'");
                            _logger.LogCritical($"  ClassName: '{dr["ClassName"]}'");
                        }
                    }
                }
                _logger.LogCritical($"GradeName/ClassNameが空でない行数: {nonEmptyCount}/{dataTable.Rows.Count}");
            }
            
            // FastReportにデータソースを登録
            report.RegisterData(dataTable, "ProductAccount");
            
            // パラメータ設定
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分ss秒"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
            report.SetParameterValue("TotalCount", flatData.Count(x => x.RowType == RowTypes.Detail).ToString());
            
            
            _logger.LogInformation("レポートを準備中...");
            report.Prepare();
            
            // PDF出力設定
            using var pdfExport = new PDFExport
            {
                EmbeddingFonts = true,
                Title = $"商品勘定_{jobDate:yyyyMMdd}",
                Subject = "商品勘定帳票",
                Creator = "在庫管理システム",
                Author = "在庫管理システム",
                TextInCurves = false,
                JpegQuality = 95,
                OpenAfterExport = false
            };
            
            // PDFをメモリストリームに出力
            using var stream = new MemoryStream();
            report.Export(pdfExport, stream);
            
            return stream.ToArray();
        }

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
            
            // データ加工：従来のGroupHeaderBand方式（新実装では使用しない）
            _logger.LogInformation("データ設定：従来形式（GroupHeaderBand対応）");
            
            // データ設定
            var dataTable = CreateDataTable(reportData);
            report.RegisterData(dataTable, "ProductAccount");
            
            // デバッグログ: FastReport登録直後
            _logger.LogCritical("=== FastReport データ登録確認 ===");
            var registeredData = report.GetDataSource("ProductAccount");
            if (registeredData != null)
            {
                _logger.LogCritical($"登録されたデータソース行数: {registeredData.RowCount}");
                
                // データソースの列を確認
                if (registeredData.Columns != null)
                {
                    _logger.LogCritical("利用可能な列:");
                    foreach (DataColumn col in registeredData.Columns)
                    {
                        if (col.ColumnName.Contains("Grade") || col.ColumnName.Contains("Class"))
                        {
                            _logger.LogCritical($"  - {col.ColumnName} ({col.DataType.Name})");
                        }
                    }
                }
            }
            
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
            
            // ScriptLanguage設定（Prepare直前の追加設定）
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
                            _logger.LogInformation("GeneratePdfReport: ScriptLanguageをNoneに設定しました");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"GeneratePdfReport: ScriptLanguage設定時の警告: {ex.Message}");
            }
            
            // GroupHeaderBandの処理（担当者別改ページ対応）
            try
            {
                var pageBase = report.Pages[0] as FR.ReportPage;
                if (pageBase != null)
                {
                    foreach (var obj in pageBase.AllObjects)
                    {
                        if (obj is FR.GroupHeaderBand groupHeader)
                        {
                            _logger.LogInformation($"GroupHeaderBand '{groupHeader.Name}' found.");
                            
                            // StaffGroupHeaderは条件を保持（担当者別グループ化と改ページに必要）
                            if (groupHeader.Name == "StaffGroupHeader")
                            {
                                // 条件式を明示的に設定（グループ化に必要）
                                groupHeader.Condition = "[ProductAccount.ProductCategory1]";
                                groupHeader.StartNewPage = true;
                                groupHeader.KeepWithData = true;
                                
                                _logger.LogInformation("StaffGroupHeader: 担当者グループ化と改ページ設定を保持");
                            }
                            else
                            {
                                // その他のGroupHeaderBandは条件式をクリア（PlatformNotSupportedException対策）
                                groupHeader.Condition = "";
                                if (groupHeader.GetType().GetProperty("Expression") != null)
                                {
                                    var expProp = groupHeader.GetType().GetProperty("Expression");
                                    expProp?.SetValue(groupHeader, "");
                                }
                                _logger.LogInformation($"GroupHeaderBand '{groupHeader.Name}' の条件式をクリアしました");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"GroupHeaderBand処理中の警告: {ex.Message}");
                // エラーが発生しても処理を継続
            }
            
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
        /// フラットデータからDataTable作成（C#側完全制御）
        /// </summary>
        private DataTable CreateFlatDataTable(List<ProductAccountFlatRow> flatData)
        {
            var table = new DataTable("ProductAccount");
            
            // === デバッグ: フラットデータ確認ログ ===
            _logger.LogCritical("=== フラットデータ→DataTable変換確認 ===");
            var product00104Rows = flatData.Where(x => x.ProductCode == "00104").Take(5);
            if (!product00104Rows.Any())
            {
                _logger.LogCritical("商品00104が見つかりません。代替として最初の5件を確認します");
                product00104Rows = flatData.Take(5);
            }
            
            _logger.LogCritical("フラットデータ荷印名確認（商品00104または代替5件）:");
            foreach (var row in product00104Rows)
            {
                _logger.LogCritical("フラットデータ: 商品={ProductCode} 荷印コード={ShippingMarkCode} 荷印名='{ShippingMarkName}' 手入力='{ManualShippingMark}'", 
                    row.ProductCode ?? "", 
                    row.ShippingMarkCode ?? "", 
                    row.ShippingMarkName ?? "", 
                    row.ManualShippingMark ?? "");
            }
            
            // フラットデータの全フィールドを文字列型で定義
            table.Columns.Add("ProductCategory1", typeof(string));
            table.Columns.Add("ProductCategory1Name", typeof(string));
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
            
            // 制御用フィールド
            table.Columns.Add("RowType", typeof(string));
            table.Columns.Add("IsGrayBackground", typeof(string));    // "1"/"0"
            table.Columns.Add("IsPageBreak", typeof(string));         // "1"/"0"
            table.Columns.Add("IsBold", typeof(string));              // "1"/"0"
            table.Columns.Add("RowSequence", typeof(string));
            
            // データ追加
            _logger.LogCritical("=== フラットデータ処理開始 ===");
            _logger.LogCritical($"フラットデータ件数: {flatData.Count}");
            int debugCount = 0;
            foreach (var item in flatData)
            {
                if (debugCount < 3)  // 最初の3件のみログ出力
                {
                    _logger.LogCritical($"=== フラットデータ {debugCount + 1} ===");
                    _logger.LogCritical($"  GradeName: '{item.GradeName}'");
                    _logger.LogCritical($"  ClassName: '{item.ClassName}'");
                    debugCount++;
                }
                
                var row = table.NewRow();
                
                // 基本フィールド（既にフォーマット済み）
                row["ProductCategory1"] = item.ProductCategory1;
                row["ProductCategory1Name"] = item.ProductCategory1Name;
                row["ProductCode"] = item.ProductCode;
                row["ProductName"] = item.ProductName;
                row["ShippingMarkName"] = item.ShippingMarkName;
                row["ManualShippingMark"] = item.ManualShippingMark;
                row["GradeName"] = item.GradeName;
                row["ClassName"] = item.ClassName;
                row["VoucherNumber"] = item.VoucherNumber;
                row["DisplayCategory"] = item.DisplayCategory;
                row["MonthDay"] = item.MonthDay;
                row["PurchaseQuantity"] = item.PurchaseQuantity;
                row["SalesQuantity"] = item.SalesQuantity;
                row["RemainingQuantity"] = item.RemainingQuantity;
                row["UnitPrice"] = item.UnitPrice;
                row["Amount"] = item.Amount;
                row["GrossProfit"] = item.GrossProfit;
                row["CustomerSupplierName"] = item.CustomerSupplierName;
                
                // 制御フィールド
                row["RowType"] = item.RowType;
                row["IsGrayBackground"] = item.GrayBackgroundFlag;    // "1"/"0"
                row["IsPageBreak"] = item.PageBreakFlag;              // "1"/"0"
                row["IsBold"] = item.BoldFlag;                        // "1"/"0"
                row["RowSequence"] = item.RowSequence.ToString();
                
                table.Rows.Add(row);
            }
            
            // === デバッグ: DataTable作成後確認ログ ===
            _logger.LogCritical("=== DataTable荷印名確認（商品00104） ===");
            var dataTableRows = table.Rows.Cast<DataRow>()
                .Where(r => r["ProductCode"]?.ToString() == "00104")
                .Take(5);
                
            if (!dataTableRows.Any())
            {
                _logger.LogCritical("商品00104のDataTableが見つかりません。代替として最初の5件を確認します");
                dataTableRows = table.Rows.Cast<DataRow>().Take(5);
            }
            
            foreach (DataRow row in dataTableRows)
            {
                _logger.LogCritical("DataTable変換後: 商品={ProductCode} 荷印名='{ShippingMarkName}' 手入力='{ManualShippingMark}'", 
                    row["ProductCode"]?.ToString() ?? "", 
                    row["ShippingMarkName"]?.ToString() ?? "", 
                    row["ManualShippingMark"]?.ToString() ?? "");
            }
            
            _logger.LogCritical("DataTable総行数: {TotalRows}", table.Rows.Count);
            _logger.LogInformation("フラットDataTable作成完了: {Count}行", table.Rows.Count);
            return table;
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
            
            // C#側データ加工用の新規列
            table.Columns.Add("RowType", typeof(string));             // 行タイプ
            table.Columns.Add("IsGrayBackground", typeof(string));    // 灰色背景フラグ（"1"/"0"）
            table.Columns.Add("IsPageBreak", typeof(string));         // 改ページフラグ（"1"/"0"）
            table.Columns.Add("RowSequence", typeof(string));         // 表示順序
            
            // データ追加時のフォーマット処理
            int debugCount = 0;
            foreach (var item in reportData)
            {
                if (debugCount < 3)  // 最初の3件のみログ出力
                {
                    _logger.LogCritical($"=== DataTable追加データ {debugCount + 1} ===");
                    _logger.LogCritical($"  GradeCode: {item.GradeCode}");
                    _logger.LogCritical($"  GradeName: '{item.GradeName}'");
                    _logger.LogCritical($"  ClassCode: {item.ClassCode}");
                    _logger.LogCritical($"  ClassName: '{item.ClassName}'");
                    debugCount++;
                }
                
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
                
                // 新規プロパティの設定
                row["RowType"] = item.RowType;
                row["IsGrayBackground"] = item.IsGrayBackground ? "1" : "0";
                row["IsPageBreak"] = item.IsPageBreak ? "1" : "0";
                row["RowSequence"] = item.RowSequence.ToString();
                
                table.Rows.Add(row);
            }
            
            // デバッグログを追加
            _logger.LogInformation("CreateDataTable完了: {Count}件のデータを追加", table.Rows.Count);
            
            // デバッグログ: DataTable完成後
            _logger.LogCritical("=== DataTable最終確認 ===");
            _logger.LogCritical($"DataTable行数: {table.Rows.Count}");
            if (table.Rows.Count > 0)
            {
                var firstRow = table.Rows[0];
                _logger.LogCritical($"1行目のGradeName: '{firstRow["GradeName"]}'");
                _logger.LogCritical($"1行目のClassName: '{firstRow["ClassName"]}'");
                
                // 空でないデータを探す
                int nonEmptyCount = 0;
                foreach (DataRow dr in table.Rows)
                {
                    if (!string.IsNullOrEmpty(dr["GradeName"]?.ToString()) || 
                        !string.IsNullOrEmpty(dr["ClassName"]?.ToString()))
                    {
                        nonEmptyCount++;
                        if (nonEmptyCount == 1)  // 最初の非空データのみログ
                        {
                            _logger.LogCritical($"非空データ発見:");
                            _logger.LogCritical($"  行番号: {table.Rows.IndexOf(dr)}");
                            _logger.LogCritical($"  GradeName: '{dr["GradeName"]}'");
                            _logger.LogCritical($"  ClassName: '{dr["ClassName"]}'");
                        }
                    }
                }
                _logger.LogCritical($"GradeName/ClassNameが空でない行数: {nonEmptyCount}/{table.Rows.Count}");
            }
            
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
        /// パーセンテージフォーマット
        /// </summary>
        private string FormatPercentage(decimal value)
        {
            if (value == 0) return "";
            if (value < 0)
            {
                return $"{Math.Abs(value):0.00}▲ %";
            }
            else
            {
                return $"{value:0.00} %";
            }
        }
        
        /// <summary>
        /// 右揃え用のパディング処理（改良版）
        /// </summary>
        /// <param name="text">表示テキスト</param>
        /// <param name="columnWidth">列幅（ピクセル）</param>
        /// <returns>右揃えされたテキスト</returns>
        private string PadLeftForAlignment(string text, int columnWidth)
        {
            // MS UI Gothic 9ptでの文字幅を考慮
            // 半角文字：約6ピクセル、全角文字：約12ピクセル
            const int halfWidthCharPixels = 6;
            const int fullWidthCharPixels = 12;
            
            // テキストの実際のピクセル幅を計算
            int textPixelWidth = 0;
            foreach (char c in text)
            {
                if (IsFullWidth(c))
                {
                    textPixelWidth += fullWidthCharPixels;
                }
                else
                {
                    textPixelWidth += halfWidthCharPixels;
                }
            }
            
            // 必要なパディングスペース数を計算
            int remainingPixels = columnWidth - textPixelWidth;
            int paddingSpaces = Math.Max(0, remainingPixels / halfWidthCharPixels);
            
            return new string(' ', paddingSpaces) + text;
        }
        
        /// <summary>
        /// 全角文字判定
        /// </summary>
        private bool IsFullWidth(char c)
        {
            // 全角文字の範囲（日本語、全角記号など）
            return (c >= 0x3000 && c <= 0x9FFF) || // CJK統合漢字など
                   (c >= 0xFF00 && c <= 0xFFEF) || // 全角英数・記号
                   (c == '【' || c == '】' || c == '▲'); // 特定の全角記号
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
        
        /// <summary>
        /// データをフラットテーブル用に加工（GroupHeaderBand廃止対応）
        /// </summary>
        private IEnumerable<ProductAccountReportModel> ProcessReportDataForFlatTable(IEnumerable<ProductAccountReportModel> reportData)
        {
            var result = new List<ProductAccountReportModel>();
            var sequence = 0;
            
            // 1. 担当者（ProductCategory1）でグループ化
            var staffGroups = reportData
                .GroupBy(x => x.ProductCategory1 ?? "")
                .OrderBy(g => g.Key);
            
            foreach (var staffGroup in staffGroups)
            {
                _logger.LogInformation("担当者グループ処理開始：{StaffCode}", staffGroup.Key);
                
                // 2. 各担当者内で商品（GroupKey）でグループ化
                var productGroups = staffGroup
                    .GroupBy(x => x.GroupKey)
                    .OrderBy(g => g.Key);
                
                foreach (var productGroup in productGroups)
                {
                    var productItems = productGroup.OrderBy(x => x.TransactionDate)
                                                  .ThenBy(x => x.VoucherNumber)
                                                  .ToList();
                    
                    if (!productItems.Any()) continue;
                    
                    var firstItem = productItems.First();
                    
                    // 3. GroupHeader行を追加（新しい実装では不要のため削除）
                    // var groupHeaderRow = CreateGroupHeaderRow(firstItem, sequence++);
                    // result.Add(groupHeaderRow);
                    
                    // 4. Detail行を追加
                    foreach (var item in productItems)
                    {
                        item.RowType = "Detail";
                        item.IsGrayBackground = false;
                        item.RowSequence = sequence++;
                        result.Add(item);
                    }
                    
                    // TODO: GroupFooter行（小計）は後回し
                }
                
                // TODO: StaffFooter行（担当者合計）は後回し 
            }
            
            _logger.LogInformation("フラットテーブル生成完了：{Count}行", result.Count);
            return result;
        }
        
        // === 行作成ヘルパーメソッド（C#側完全制御用） ===
        
        /// <summary>
        /// 改ページ制御行作成（担当者情報付き）
        /// </summary>
        private ProductAccountFlatRow CreatePageBreakRow(string staffCode, string staffName, int sequence)
        {
            return new ProductAccountFlatRow
            {
                RowType = RowTypes.StaffHeader,
                RowSequence = sequence,
                IsPageBreak = true,
                IsGrayBackground = false,
                ProductCategory1 = staffCode,
                ProductCategory1Name = staffName,
                ProductName = "" // 改ページ専用なので表示内容なし
            };
        }
        
        /// <summary>
        /// 前残行作成（必要に応じて）
        /// </summary>
        private ProductAccountFlatRow? CreatePreviousBalanceIfNeeded(dynamic productKey, int sequence)
        {
            // 前残データが存在するかチェック（実装省略：通常は別途データ取得が必要）
            // 実際の実装では、productKeyに基づいてCP在庫マスタから前残データを取得
            
            // 仮実装として、前残が必要な場合のみ行を作成
            // 本実装では前残データの有無判定とデータ取得が必要
            return null; // 今回は前残行を作成しない
        }
        
        /// <summary>
        /// 各明細行に商品情報をすべて含めて作成（担当者情報も含む）
        /// </summary>
        private ProductAccountFlatRow CreateDetailRowWithProductInfo(ProductAccountReportModel data, dynamic productKey, string staffCode, string staffName, int sequence)
        {
            return new ProductAccountFlatRow
            {
                RowType = RowTypes.Detail,
                RowSequence = sequence,
                IsGrayBackground = false,
                IsBold = false,
                
                // 基本情報（商品情報をすべて含める）
                ProductCategory1 = staffCode,
                ProductCategory1Name = staffName,
                ProductCode = data.ProductCode,
                ProductName = data.ProductName,
                ShippingMarkCode = data.ShippingMarkCode,
                ShippingMarkName = data.ShippingMarkName,
                ManualShippingMark = data.ManualShippingMark,
                GradeName = data.GradeName,
                ClassName = data.ClassName,
                VoucherNumber = data.VoucherNumber,
                DisplayCategory = data.DisplayCategory,
                MonthDay = data.TransactionDate.ToString("MM/dd"),
                CustomerSupplierName = data.CustomerSupplierName,
                
                // フォーマット済み数値
                PurchaseQuantity = FormatQuantity(data.PurchaseQuantity),
                SalesQuantity = FormatQuantity(data.SalesQuantity),
                RemainingQuantity = FormatQuantity(data.RemainingQuantity),
                UnitPrice = FormatUnitPrice(data.UnitPrice),
                Amount = FormatAmount(data.Amount),
                GrossProfit = FormatGrossProfit(data.GrossProfit)
            };
        }
        
        /// <summary>
        /// 明細行作成
        /// </summary>
        private ProductAccountFlatRow CreateDetailRow(ProductAccountReportModel data, int sequence)
        {
            return new ProductAccountFlatRow
            {
                RowType = RowTypes.Detail,
                RowSequence = sequence,
                IsGrayBackground = false,
                IsBold = false,
                
                // 基本情報
                ProductCategory1 = data.ProductCategory1 ?? "",
                ProductCategory1Name = data.GetAdditionalInfo("ProductCategory1Name") ?? "",
                ProductCode = data.ProductCode,
                ProductName = data.ProductName,
                ShippingMarkCode = data.ShippingMarkCode,
                ShippingMarkName = data.ShippingMarkName,
                ManualShippingMark = data.ManualShippingMark,
                GradeName = data.GradeName,
                ClassName = data.ClassName,
                VoucherNumber = data.VoucherNumber,
                DisplayCategory = data.DisplayCategory,
                MonthDay = data.TransactionDate.ToString("MM/dd"),
                CustomerSupplierName = data.CustomerSupplierName,
                
                // フォーマット済み数値
                PurchaseQuantity = FormatQuantity(data.PurchaseQuantity),
                SalesQuantity = FormatQuantity(data.SalesQuantity),
                RemainingQuantity = FormatQuantity(data.RemainingQuantity),
                UnitPrice = FormatUnitPrice(data.UnitPrice),
                Amount = FormatAmount(data.Amount),
                GrossProfit = FormatGrossProfit(data.GrossProfit)
            };
        }
        
        /// <summary>
        /// 商品別小計見出し行作成
        /// 【前日残】を月日列に右揃えで配置
        /// </summary>
        private ProductAccountFlatRow CreateProductSubtotalHeader(int sequence)
        {
            return new ProductAccountFlatRow
            {
                RowType = RowTypes.ProductSubtotalHeader,
                RowSequence = sequence,
                IsBold = false,
                IsGrayBackground = false,
                
                // 商品情報列はすべて空
                ProductName = "",
                ManualShippingMark = "",
                GradeName = "",
                ClassName = "",
                VoucherNumber = "",
                DisplayCategory = "",                              // 区分列は空
                
                // 月日列に【前日残】を右揃えで配置
                MonthDay = "【前日残】",                           // ★ここが重要
                
                // 各集計項目の見出し
                PurchaseQuantity = "【仕入計】",
                SalesQuantity = "【売上計】",
                RemainingQuantity = "【当日残】",
                UnitPrice = "【在庫単価】",
                Amount = "【在庫金額】",
                GrossProfit = "【粗利益】",
                CustomerSupplierName = "【粗利率】"                // 取引先名列に配置
            };
        }
        
        /// <summary>
        /// 商品別小計数値行作成
        /// 前日残の数値を月日列に右揃えで配置
        /// </summary>
        private ProductAccountFlatRow CreateProductSubtotal(
            decimal previousBalance,      // 前日残（追加）
            decimal purchase,             // 仕入計
            decimal sales,                // 売上計
            decimal currentBalance,       // 当日残（追加）
            decimal inventoryUnitPrice,   // 在庫単価（追加）
            decimal inventoryAmount,      // 在庫金額
            decimal grossProfit,          // 粗利益
            decimal grossProfitRate,      // 粗利率（追加）
            int sequence)
        {
            return new ProductAccountFlatRow
            {
                RowType = RowTypes.ProductSubtotal,
                RowSequence = sequence,
                IsSubtotal = true,
                IsBold = true,
                IsGrayBackground = true,
                
                // 商品情報列はすべて空
                ProductName = "",
                ManualShippingMark = "",
                GradeName = "",
                ClassName = "",
                VoucherNumber = "",
                DisplayCategory = "",                                      // 区分列は空
                
                // 月日列に前日残の数値を配置
                MonthDay = FormatQuantity(previousBalance),               // ★前日残の数値
                
                // 各集計値
                PurchaseQuantity = FormatQuantity(purchase),
                SalesQuantity = FormatQuantity(sales),
                RemainingQuantity = FormatQuantity(currentBalance),
                UnitPrice = FormatUnitPrice(inventoryUnitPrice),
                Amount = FormatAmount(inventoryAmount),
                GrossProfit = FormatGrossProfit(grossProfit),
                CustomerSupplierName = FormatPercentage(grossProfitRate),  // 粗利率
                
                // その他の情報はクリア
                ProductCategory1 = "",
                ProductCategory1Name = ""
            };
        }
        
        /// <summary>
        /// 担当者別合計行作成
        /// </summary>
        
        
        /// <summary>
        /// 空行作成
        /// </summary>
        private ProductAccountFlatRow CreateBlankRow(int sequence)
        {
            return new ProductAccountFlatRow
            {
                RowType = RowTypes.BlankLine,
                RowSequence = sequence
            };
        }
        
        // === 集計計算メソッド ===
        
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