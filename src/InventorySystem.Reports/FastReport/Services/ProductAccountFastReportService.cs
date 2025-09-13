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
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    // RowTypesクラスの後に追加
    public class StaffPageInfo
    {
        public string StaffCode { get; set; }
        public string StaffName { get; set; }
        public int StartPage { get; set; }
        public int PageCount { get; set; }
        public int EndPage => StartPage + PageCount - 1;
    }

    public class ProductAccountFastReportService : IProductAccountReportService
    {
        private readonly ILogger<ProductAccountFastReportService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _templatePath;
        
        // C#側改ページ制御用
        private string _lastStaffCode = "";
        
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

        /// <summary>
        /// 前残行を作成
        /// </summary>
        private ProductAccountFlatRow CreatePreviousBalanceRow(
            ProductAccountReportModel previousData,
            string staffCode,
            string staffName,
            int sequence)
        {
            // LastReceiptDateはpreviousData.TransactionDateに格納される想定
            var monthDay = previousData.TransactionDate == DateTime.MinValue
                ? string.Empty
                : previousData.TransactionDate.ToString("MM/dd");

            return new ProductAccountFlatRow
            {
                RowType = RowTypes.Detail,
                RowSequence = sequence,
                IsGrayBackground = false,
                IsBold = false,

                // 基本情報
                ProductCategory1 = staffCode,
                ProductCategory1Name = staffName,
                ProductCode = previousData.ProductCode,
                ProductName = previousData.ProductName,
                ShippingMarkCode = previousData.ShippingMarkCode,
                ShippingMarkName = previousData.ShippingMarkName,
                ManualShippingMark = previousData.ManualShippingMark,
                GradeCode = previousData.GradeCode,
                GradeName = previousData.GradeName,
                ClassCode = previousData.ClassCode,
                ClassName = previousData.ClassName,

                // 前残固有の設定
                VoucherNumber = string.Empty, // 伝票番号は空欄
                DisplayCategory = "前残",
                MonthDay = monthDay, // 最終入荷日（MM/DD）
                CustomerSupplierName = string.Empty, // 取引先名は空欄

                // 数量・金額（前残は残数量のみ）
                PurchaseQuantity = FormatQuantity(0),
                SalesQuantity = FormatQuantity(0),
                RemainingQuantity = FormatQuantity(previousData.RemainingQuantity),
                UnitPrice = FormatUnitPrice(previousData.UnitPrice),
                Amount = FormatAmount(previousData.Amount),
                GrossProfit = string.Empty // 前残は粗利益なし
            };
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
                
                // テンプレートファイルの存在確認
                if (!File.Exists(_templatePath))
                {
                    var errorMessage = $"商品勘定レポートテンプレートが見つかりません: {_templatePath}";
                    _logger.LogError(errorMessage);
                    throw new FileNotFoundException(errorMessage, _templatePath);
                }
                
                // 商品勘定フラットデータを生成
                var flatData = GenerateFlatData(jobDate, departmentCode);
                
                if (!flatData.Any())
                {
                    _logger.LogWarning("商品勘定フラットデータが0件です");
                    throw new InvalidOperationException("商品勘定データが存在しません");
                }
                
                // ===== Phase 2: 2回PDF生成アプローチ =====
                
                // Step 1: 担当者コード空を"000"に変換
                foreach (var item in flatData)
                {
                    if (string.IsNullOrEmpty(item.ProductCategory1))
                    {
                        item.ProductCategory1 = "000";
                        item.ProductCategory1Name = "担当者未設定";
                    }
                }
                
                // Step 2: 担当者別にグループ化（データ行のみカウント）
                var staffGroups = flatData
                    .Where(x => x.RowType == RowTypes.Detail)
                    .GroupBy(x => x.ProductCategory1)
                    .OrderBy(g => g.Key)
                    .ToList();
                
                // Step 3: 一時フォルダ作成
                string tempFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    "Output", 
                    $"Temp_{DateTime.Now:yyyyMMddHHmmss}");
                Directory.CreateDirectory(tempFolder);
                
                _logger.LogInformation("一時フォルダ作成: {TempFolder}", tempFolder);

                try
                {
                    // Phase 3: 1次PDF生成（仮のページ数999で生成）
                    Dictionary<string, string> firstPassFiles = new Dictionary<string, string>();
                    
                    foreach (var group in staffGroups)
                    {
                        var staffCode = group.Key;
                        var staffData = flatData.Where(x => x.ProductCategory1 == staffCode).ToList();
                        
                        _logger.LogInformation("1次PDF生成開始: 担当者{Staff}", staffCode);
                        
                        // 仮の総ページ数999で生成（-1は使用しない）
                        // 1次生成では仮のページ数を使用（実測後に2次生成で正確な値を設定）
                        byte[] pdfBytes = GeneratePdfReportFromFlatDataWithPageNumber(
                            staffData, jobDate, 1, 999, 999);
                        
                        // 一時ファイルとして保存
                        string fileName = $"{staffCode}_{Guid.NewGuid()}.pdf";
                        string filePath = Path.Combine(tempFolder, fileName);
                        File.WriteAllBytes(filePath, pdfBytes);
                        firstPassFiles.Add(staffCode, filePath);
                        
                        _logger.LogInformation("1次PDF生成完了: {FileName}", fileName);
                    }
                    
                    // Phase 4: 実ページ数取得
                    Dictionary<string, int> actualPageCounts = new Dictionary<string, int>();
                    int totalPages = 0;
                    
                    _logger.LogInformation("実ページ数取得開始");
                    
                    foreach (var kvp in firstPassFiles)
                    {
                        using (var pdfDocument = PdfReader.Open(kvp.Value, PdfDocumentOpenMode.Import))
                        {
                            int pageCount = pdfDocument.PageCount;
                            actualPageCounts.Add(kvp.Key, pageCount);
                            totalPages += pageCount;
                            
                            _logger.LogInformation("担当者{Staff}: 実ページ数{Pages}ページ", 
                                kvp.Key, pageCount);
                        }
                    }
                    
                    _logger.LogInformation("総ページ数（実測値）: {TotalPages}", totalPages);
                    
                    // Phase 5: 2次PDF生成（正確なページ数で再生成）
                    List<byte[]> finalPdfList = new List<byte[]>();
                    int currentStartPage = 1;
                    
                    _logger.LogInformation("2次PDF生成開始");
                    
                    foreach (var group in staffGroups)
                    {
                        var staffCode = group.Key;
                        var staffData = flatData.Where(x => x.ProductCategory1 == staffCode).ToList();
                        int pageCount = actualPageCounts[staffCode];
                        
                        _logger.LogInformation("2次PDF生成: 担当者{Staff} 開始ページ{Start} ページ数{Count}", 
                            staffCode, currentStartPage, pageCount);
                        
                        // 正確な総ページ数とスタートページで生成
                        byte[] pdfBytes = GeneratePdfReportFromFlatDataWithPageNumber(
                            staffData, jobDate, currentStartPage, pageCount, totalPages);
                        
                        finalPdfList.Add(pdfBytes);
                        currentStartPage += pageCount;
                    }
                    
                    // Phase 6: 結合と出力
                    if (finalPdfList.Any())
                    {
                        var mergedPdf = MergePdfFiles(finalPdfList);
                        _logger.LogInformation("商品勘定帳票PDF結合完了。担当者数: {Count}, 総サイズ: {Size} bytes", 
                            finalPdfList.Count, mergedPdf.Length);
                        return mergedPdf;
                    }
                    else
                    {
                        throw new InvalidOperationException("PDFの生成に失敗しました");
                    }
                }
                finally
                {
                    // Phase 7: クリーンアップ（一時フォルダを完全削除）
                    try
                    {
                        if (Directory.Exists(tempFolder))
                        {
                            Directory.Delete(tempFolder, true);
                            _logger.LogInformation("一時フォルダ削除完了: {TempFolder}", tempFolder);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "一時フォルダの削除に失敗: {TempFolder}", tempFolder);
                    }
                }
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
        /// 複数のPDFファイルを1つに結合
        /// </summary>
        private byte[] MergePdfFiles(List<byte[]> pdfBytesList)
        {
            if (pdfBytesList == null || !pdfBytesList.Any())
            {
                throw new ArgumentException("結合するPDFがありません", nameof(pdfBytesList));
            }
            
            if (pdfBytesList.Count == 1)
            {
                return pdfBytesList[0];
            }
            
            try
            {
                using (var outputDocument = new PdfDocument())
                {
                    foreach (var pdfBytes in pdfBytesList)
                    {
                        using (var stream = new MemoryStream(pdfBytes))
                        {
                            using (var inputDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import))
                            {
                                // 全ページをコピー
                                foreach (var page in inputDocument.Pages)
                                {
                                    outputDocument.AddPage(page);
                                }
                            }
                        }
                    }
                    
                    // 結合したPDFをバイト配列として返す
                    using (var outputStream = new MemoryStream())
                    {
                        outputDocument.Save(outputStream);
                        return outputStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF結合処理でエラーが発生しました");
                throw new InvalidOperationException("PDFの結合に失敗しました", ex);
            }
        }

        /// <summary>
        /// 一時ファイルをクリーンアップ
        /// </summary>
        private void CleanupTemporaryFiles(List<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        _logger.LogDebug("一時PDFファイルを削除しました: {FilePath}", filePath);
                    }
                }
                catch (Exception ex)
                {
                    // 削除失敗はログに記録するが処理は継続
                    _logger.LogWarning(ex, "一時ファイルの削除に失敗しました: {FilePath}", filePath);
                }
            }
            
            // Outputフォルダが空の場合は削除
            try
            {
                var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
                if (Directory.Exists(outputDir) && !Directory.GetFiles(outputDir).Any())
                {
                    Directory.Delete(outputDir);
                    _logger.LogDebug("空のOutputフォルダを削除しました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outputフォルダの削除に失敗しました");
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

                    // グループ内の前残（最新1件）を抽出
                    var previousData = productGroup
                        .Where(x => string.Equals(x.RecordType, "Previous", StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();

                    // 当日取引（日付・伝票番号順）
                    var details = productGroup
                        .Where(x => !string.Equals(x.RecordType, "Previous", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x.TransactionDate)
                        .ThenBy(x => x.VoucherNumber)
                        .ToList();

                    // 前残行を最初に追加（あれば）
                    if (previousData != null)
                    {
                        var prevRow = CreatePreviousBalanceRow(previousData, staffCode, staffName, sequence++);
                        flatRows.Add(prevRow);
                    }
                    
                    // 集計用変数
                    decimal subtotalPreviousBalance = 0;  // 追加
                    decimal subtotalPurchase = 0;
                    decimal subtotalSales = 0;
                    decimal subtotalCurrentBalance = 0;   // 追加
                    decimal subtotalInventoryUnitPrice = 0; // 追加
                    decimal subtotalInventoryAmount = 0;
                    decimal subtotalGrossProfit = 0;
                    decimal subtotalSalesAmount = 0;      // 追加: 売上伝票金額合計
                    
                    // CP在庫マスタから在庫単価・金額を取得、前残数量は前残データを優先
                    var cpInventoryData = GetCpInventoryData(productGroup.Key, jobDate);
                    if (previousData != null)
                    {
                        subtotalPreviousBalance = previousData.RemainingQuantity;
                    }
                    else if (cpInventoryData != null)
                    {
                        subtotalPreviousBalance = cpInventoryData.PreviousDayStock ?? 0;
                    }
                    if (cpInventoryData != null)
                    {
                        subtotalInventoryUnitPrice = cpInventoryData.DailyUnitPrice ?? 0;
                        subtotalInventoryAmount = cpInventoryData.DailyStockAmount ?? 0;
                    }
                    
                    foreach (var detail in details)
                    {
                        // 各明細行に商品情報をすべて含める（担当者情報も含む）
                        flatRows.Add(CreateDetailRowWithProductInfo(detail, productGroup.Key, staffCode, staffName, sequence++));
                        
                        // 小計集計
                        subtotalPurchase += detail.PurchaseQuantity;
                        subtotalSales += detail.SalesQuantity;
                        subtotalGrossProfit += detail.GrossProfit;
                        subtotalSalesAmount += detail.SalesAmount;
                    }
                    
                    // 当日残を計算
                    subtotalCurrentBalance = subtotalPreviousBalance + subtotalPurchase - subtotalSales;
                    
                    // 粗利率は印字時に売上伝票金額を用いてフォーマットするため、ここでは計算しない
                    
                    // 商品別小計（2行構成）
                    if (subtotalPurchase != 0 || subtotalSales != 0 || subtotalInventoryAmount != 0)
                    {
                        // 見出し行
                        flatRows.Add(CreateProductSubtotalHeader(staffCode, staffName, sequence++));
                        
                        // 数値行
                        flatRows.Add(CreateProductSubtotal(
                            staffCode,
                            staffName,
                            subtotalPreviousBalance,
                            subtotalPurchase, 
                            subtotalSales,
                            subtotalCurrentBalance,
                            subtotalInventoryUnitPrice,
                            subtotalInventoryAmount,
                            subtotalGrossProfit,
                            subtotalSalesAmount,
                            sequence++));
                    }
                    
                    // 空行
                    flatRows.Add(CreateBlankRow(staffCode, staffName, sequence++));
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
                        -- 前残高データ（CpInventoryMasterから取得）
                        SELECT 
                            cp.ProductCode,
                            ISNULL(cp.ProductName, '') as ProductName,
                            ISNULL(cp.ProductCategory1, '') as ProductCategory1,
                            cp.ShippingMarkCode,
                            ISNULL(cp.ShippingMarkName, '') as ShippingMarkName,
                            cp.ManualShippingMark,
                            cp.GradeCode,
                            ISNULL(cp.GradeName, '') as GradeName,
                            cp.ClassCode,
                            ISNULL(cp.ClassName, '') as ClassName,
                            '' as VoucherNumber,
                            '' as VoucherType,
                            '前残' as DisplayCategory,
                            cp.LastReceiptDate as TransactionDate,
                            0 as PurchaseQuantity,
                            0 as SalesQuantity,
                            ABS(cp.PreviousDayStock) as RemainingQuantity,
                            cp.PreviousDayUnitPrice as UnitPrice,
                            ABS(cp.PreviousDayStockAmount) as Amount,
                            0 as SalesAmount,
                            0 as GrossProfit,
                            '' as CustomerSupplierName,
                            'Previous' as RecordType,  -- 重要：前残高のRecordType
                            NULL as CategoryCode
                        FROM CpInventoryMaster cp
                        WHERE cp.JobDate = @JobDate
                          AND cp.PreviousDayStock <> 0
                          AND (@DepartmentCode IS NULL OR cp.ProductCategory1 = @DepartmentCode)
                        
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
                            ISNULL(cp.DailyUnitPrice, cp.PreviousDayUnitPrice) as UnitPrice,
                            s.Amount,
                            s.Amount as SalesAmount,
                            ISNULL(s.GrossProfit, 0) as GrossProfit,
                            s.CustomerName as CustomerSupplierName,
                            'Sales' as RecordType,
                            NULL as CategoryCode
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
                            0 as SalesAmount,
                            0 as GrossProfit,
                            p.SupplierName as CustomerSupplierName,
                            'Purchase' as RecordType,
                            NULL as CategoryCode
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
                            0 as SalesAmount,
                            CASE 
                                WHEN ia.CategoryCode IN (1, 3, 2, 5, 6) THEN -ia.Amount  -- ロス・腐り・加工費・調整は金額をマイナス粗利益
                                WHEN ia.CategoryCode = 4 THEN 0  -- 振替は粗利益なし
                                ELSE 0
                            END as GrossProfit,
                            '' as CustomerSupplierName,
                            'Adjustment' as RecordType,
                            ia.CategoryCode as CategoryCode
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
                             GradeCode, ClassCode,
                             CASE WHEN RecordType = 'Previous' THEN 0 ELSE 1 END,
                             TransactionDate, VoucherNumber";
                
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
            
            // スクリプトを完全に無効化
            SetScriptLanguageToNone(report);
            
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
            
            // デバッグ用：ProductCategory1の値を確認
            var distinctCategories = dataTable.AsEnumerable()
                .Select(r => r["ProductCategory1"]?.ToString())
                .Distinct()
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
            _logger.LogCritical($"担当者一覧: {string.Join(", ", distinctCategories)}");
            _logger.LogCritical($"担当者数: {distinctCategories.Count}");
            
            // FastReportにデータソースを登録
            report.RegisterData(dataTable, "ProductAccount");
            
            // DataBandに改ページ条件を設定
            var dataBand = report.FindObject("Data1") as FR.DataBand;
            if (dataBand != null)
            {
                // StartNewPageExpressionプロパティに条件式を設定
                var startNewPageProperty = dataBand.GetType().GetProperty("StartNewPageExpression");
                if (startNewPageProperty != null)
                {
                    startNewPageProperty.SetValue(dataBand, "[ProductAccount.IsPageBreak] == \"1\"");
                    _logger.LogInformation("DataBandに改ページ条件式を設定しました");
                }
                else
                {
                    // フォールバック: 条件付き改ページは.frxテンプレート側で制御
                    _logger.LogInformation("StartNewPageExpressionプロパティが見つかりません。.frxテンプレート側で制御します");
                }
            }
            
            // ========== C#側改ページ制御 ==========
            
            // 1. GroupHeaderBandを取得して強制設定
            var groupHeader = report.FindObject("StaffGroupHeader") as FR.GroupHeaderBand;
            if (groupHeader != null)
            {
                groupHeader.StartNewPage = true;
                groupHeader.Condition = "[ProductAccount.ProductCategory1]";
                groupHeader.KeepWithData = true;
                _logger.LogInformation("GroupHeaderBand改ページ設定を強制適用");
            }
            else
            {
                _logger.LogWarning("StaffGroupHeaderが見つかりません");
            }
            
            // 2. DataBandにBeforePrintイベントを設定（代替案）- 改ページ問題のため削除
            /*
            // dataBandは既に上で定義済みなので再利用
            if (dataBand != null)
            {
                // 改ページ処理用フラグをリセット
                _lastStaffCode = "";
                
                try
                {
                    // BeforePrintイベントハンドラを動的に追加
                    dataBand.BeforePrint += (sender, e) =>
                    {
                        try
                        {
                            // 現在の行のProductCategory1を取得
                            var currentStaff = report.GetColumnValue("ProductAccount.ProductCategory1")?.ToString();
                            
                            // 前回の値と比較
                            if (!string.IsNullOrEmpty(_lastStaffCode) && 
                                _lastStaffCode != currentStaff)
                            {
                                // 担当者が変わったら改ページ（ログのみ、実際の改ページはGroupHeaderBandに委ねる）
                                _logger.LogInformation($"担当者変更検知: {_lastStaffCode} → {currentStaff}");
                                // FastReportのEngine.NewPage()は利用できないため、GroupHeaderBandの自動改ページに依存
                            }
                            
                            _lastStaffCode = currentStaff ?? "";
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"改ページ処理エラー: {ex.Message}");
                        }
                    };
                    
                    _logger.LogInformation("DataBandにBeforePrintイベントを設定しました");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"BeforePrintイベント設定エラー: {ex.Message}");
                }
            }
            */
            
            // ========== ここまで C#側改ページ制御 ==========
            
            // GroupHeaderBandを完全に無効化（改ページ復活のためコメントアウト）
            /*
            var groupHeaders = report.AllObjects.OfType<FR.GroupHeaderBand>().ToList();
            foreach (var groupHeader in groupHeaders)
            {
                groupHeader.Condition = "";
                groupHeader.StartNewPage = false;
                _logger.LogInformation($"GroupHeaderBand '{groupHeader.Name}' を無効化しました");
            }
            */
            
            // === PAGE_BREAK行による改ページ処理 ===
            var pageBreakDataBand = report.FindObject("Data1") as FR.DataBand;
            if (pageBreakDataBand != null)
            {
                pageBreakDataBand.BeforePrint += (sender, e) =>
                {
                    var currentRowType = report.GetVariableValue("ProductAccount.RowType")?.ToString();
                    if (currentRowType == RowTypes.PageBreak)
                    {
                        // PAGE_BREAK行は表示しない
                        pageBreakDataBand.Visible = false;
                        // 改ページを実行
                        var engine = report.Engine;
                        if (engine != null)
                        {
                            engine.StartNewPage();
                        }
                        _logger.LogInformation($"PAGE_BREAK行を検出し改ページを実行");
                    }
                    else
                    {
                        // 通常の行は表示
                        pageBreakDataBand.Visible = true;
                    }
                };
                _logger.LogInformation("DataBandにPAGE_BREAK改ページ処理を設定しました");
            }
            else
            {
                _logger.LogWarning("DataBand 'Data1' が見つかりません");
            }
            
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

        /// <summary>
        /// ページ番号付きPDF生成（担当者別処理用）
        /// </summary>
        private byte[] GeneratePdfReportFromFlatDataWithPageNumber(
            List<ProductAccountFlatRow> flatData, 
            DateTime jobDate,
            int startPage,
            int pageCount,  // この担当者の実測ページ数
            int totalPages)
        {
            using var report = new FR.Report();
            
            // FastReportの設定
            report.ReportResourceString = "";
            report.FileName = _templatePath;
            
            // テンプレートファイルを読み込む
            _logger.LogInformation("商品勘定レポートテンプレートを読み込んでいます...");
            report.Load(_templatePath);
            
            // スクリプトを完全に無効化
            SetScriptLanguageToNone(report);
            
            // フラットデータをDataTableに変換
            var dataTable = CreateFlatDataTableWithPageNumbers(flatData, startPage, pageCount, totalPages);
            
            // FastReportにデータソースを登録
            report.RegisterData(dataTable, "ProductAccount");
            
            // DataBandに改ページ条件を設定
            var dataBand = report.FindObject("Data1") as FR.DataBand;
            if (dataBand != null)
            {
                var startNewPageProperty = dataBand.GetType().GetProperty("StartNewPageExpression");
                if (startNewPageProperty != null)
                {
                    startNewPageProperty.SetValue(dataBand, "[ProductAccount.IsPageBreak] == \"1\"");
                    _logger.LogInformation("DataBandに改ページ条件式を設定しました");
                }
            }
            
            // PAGE_BREAK行による改ページ処理
            var pageBreakDataBand = report.FindObject("Data1") as FR.DataBand;
            if (pageBreakDataBand != null)
            {
                pageBreakDataBand.BeforePrint += (sender, e) =>
                {
                    var currentRowType = report.GetVariableValue("ProductAccount.RowType")?.ToString();
                    if (currentRowType == RowTypes.PageBreak)
                    {
                        pageBreakDataBand.Visible = false;
                        var engine = report.Engine;
                        if (engine != null)
                        {
                            engine.StartNewPage();
                        }
                    }
                    else
                    {
                        pageBreakDataBand.Visible = true;
                    }
                };
            }
            
            // パラメータ設定（重要：ページ番号の設定）
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分ss秒"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
            report.SetParameterValue("CurrentPage", startPage.ToString());
            report.SetParameterValue("TotalPages", totalPages.ToString());
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

        /// <summary>
        /// DataTableから実際のページ数を計算
        /// </summary>
        private int CalculateActualPageCount(DataTable table)
        {
            int pageCount = 1;
            int rowsInCurrentPage = 0;
            const int MAX_ROWS_PER_PAGE = 35;
            
            foreach (DataRow row in table.Rows)
            {
                string rowType = row["RowType"]?.ToString() ?? "";
                
                // PAGE_BREAK行は改ページマーカー
                if (rowType == RowTypes.PageBreak)
                {
                    if (rowsInCurrentPage > 0)
                    {
                        pageCount++;
                        rowsInCurrentPage = 0;
                    }
                    continue; // PAGE_BREAK行自体はカウントしない
                }
                
                // ダミー行と通常行をカウント
                rowsInCurrentPage++;
                
                if (rowsInCurrentPage >= MAX_ROWS_PER_PAGE)
                {
                    // 次の行があればページを増やす
                    if (table.Rows.IndexOf(row) < table.Rows.Count - 1)
                    {
                        pageCount++;
                        rowsInCurrentPage = 0;
                    }
                }
            }
            
            return pageCount;
        }

        /// <summary>
        /// ページ番号付きDataTable作成
        /// </summary>
        private DataTable CreateFlatDataTableWithPageNumbers(
            List<ProductAccountFlatRow> flatData,
            int startPage,
            int pageCount,  // この担当者の実測ページ数
            int totalPages)
        {
            var table = CreateFlatDataTable(flatData);
            
            // ページ番号列が存在しない場合は追加
            if (!table.Columns.Contains("CurrentPage"))
            {
                table.Columns.Add("CurrentPage", typeof(string));
            }
            if (!table.Columns.Contains("TotalPagesDisplay"))
            {
                table.Columns.Add("TotalPagesDisplay", typeof(string));
            }
            
            // === PAGE_BREAK行の位置を収集 ===
            var pageBreakIndices = new List<int>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                if (table.Rows[i]["RowType"]?.ToString() == RowTypes.PageBreak)
                {
                    pageBreakIndices.Add(i);
                }
            }
            
            int totalRows = table.Rows.Count;
            int breakCount = pageBreakIndices.Count;
            
            // 詳細ログ出力
            _logger.LogInformation($"CreateFlatDataTableWithPageNumbers: " +
                $"総行数={totalRows}, PAGE_BREAK行数={breakCount}, " +
                $"実測ページ数={pageCount}, 開始ページ={startPage}");
            
            // === 実測ページ数に基づく行数ベース配分 ===
            // 総行数（PAGE_BREAK行除く）を実測ページ数で割って1ページあたりの行数を算出
            int nonBreakRows = totalRows - breakCount;
            double rowsPerPageDouble = nonBreakRows > 0 ? (double)nonBreakRows / pageCount : 1.0;
            int rowsPerPage = Math.Max(1, (int)Math.Ceiling(rowsPerPageDouble));
            
            _logger.LogInformation($"計算結果: 非BREAK行数={nonBreakRows}, " +
                $"1ページ当たり行数={rowsPerPage} (小数値={rowsPerPageDouble:F2})");
            
            int currentPage = startPage;
            int nonBreakRowCount = 0; // PAGE_BREAK行以外のカウンタ
            
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DataRow row = table.Rows[i];
                string rowType = row["RowType"]?.ToString() ?? "";
                
                // PAGE_BREAK行の処理
                if (rowType == RowTypes.PageBreak)
                {
                    row["IsPageBreak"] = "1";  // FastReport用マーカー
                    row["CurrentPage"] = currentPage.ToString();
                    row["TotalPagesDisplay"] = totalPages.ToString();
                    
                    _logger.LogDebug($"PAGE_BREAK行処理: 行{i}, ページ{currentPage}");
                    continue;
                }
                
                // 通常行：行数ベースでページ番号を計算
                int pageIndex = Math.Min(nonBreakRowCount / rowsPerPage, pageCount - 1);
                currentPage = startPage + pageIndex;
                
                row["CurrentPage"] = currentPage.ToString();
                row["TotalPagesDisplay"] = totalPages.ToString();
                
                nonBreakRowCount++;
                
                // デバッグログ（最初と最後の数行のみ）
                if (i < 5 || i >= totalRows - 5)
                {
                    _logger.LogDebug($"行{i}: RowType={rowType}, " +
                        $"nonBreakRowCount={nonBreakRowCount}, pageIndex={pageIndex}, " +
                        $"currentPage={currentPage}");
                }
            }
            
            // 最終ログ
            int endPage = startPage + pageCount - 1;
            _logger.LogInformation($"ページ番号設定完了: 開始={startPage}, 終了={endPage}, " +
                $"実測ページ数={pageCount}, 総ページ={totalPages}, 非BREAK行処理数={nonBreakRowCount}");
            
            return table;
        }



        private byte[] GeneratePdfReport(IEnumerable<ProductAccountReportModel> reportData, DateTime jobDate)
        {
            using var report = new FR.Report();
            
            // FastReportの設定（アンマッチリストと同じ）
            report.ReportResourceString = "";
            report.FileName = _templatePath;
            
            // テンプレート読込
            report.Load(_templatePath);
            
            // スクリプトを完全に無効化
            SetScriptLanguageToNone(report);
            
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
            table.Columns.Add("ShippingMarkCode", typeof(string));  // 荷印コード（4桁）
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
            table.Columns.Add("IsGrossProfitRate", typeof(bool));    // 粗利率フラグ
            
            // 制御用フィールド
            table.Columns.Add("RowType", typeof(string));
            table.Columns.Add("IsGrayBackground", typeof(string));    // "1"/"0"
            table.Columns.Add("IsPageBreak", typeof(string));         // "1"/"0"
            table.Columns.Add("IsBold", typeof(string));              // "1"/"0"
            table.Columns.Add("RowSequence", typeof(string));
            table.Columns.Add("PageGroup", typeof(string));           // 35行改ページ用グループ
            table.Columns.Add("CurrentPage", typeof(string));        // 現在のページ番号
            table.Columns.Add("TotalPagesDisplay", typeof(string));  // 総ページ数
            
            // === 35行改ページ制御変数 ===
            string currentStaffCode = "";
            string currentStaffName = "";
            int currentPageRows = 0;
            // フォントサイズを11ptに拡大したため、1ページあたりの実用行数を調整
            const int MAX_ROWS_PER_PAGE = 33; // 以前: 35→34→33
            
            // === 担当者別件数の事前確認（デバッグ用） ===
            var staffGroups = flatData
                .Where(x => x.RowType != RowTypes.ProductSubtotal && x.RowType != InventorySystem.Reports.Models.RowTypes.ProductSubtotalHeader && x.RowType != InventorySystem.Reports.Models.RowTypes.BlankLine)
                .GroupBy(x => x.ProductCategory1)
                .Select(g => new { Code = g.Key, Name = g.First().ProductCategory1Name, Count = g.Count() })
                .ToList();
            
            _logger.LogCritical("=== 担当者別件数確認（小計行除外） ===");
            foreach (var group in staffGroups)
            {
                _logger.LogInformation("担当者: {Code} ({Name}) - {Count}件", group.Code, group.Name, group.Count);
            }
            
            // === メイン処理ループ ===
            for (int i = 0; i < flatData.Count; i++)
            {
                var item = flatData[i];
                
                // === 担当者変更チェック（小計行は除外） ===
                if (item.RowType != RowTypes.ProductSubtotal && 
                    item.RowType != InventorySystem.Reports.Models.RowTypes.ProductSubtotalHeader && 
                    item.RowType != InventorySystem.Reports.Models.RowTypes.BlankLine &&
                    !string.IsNullOrEmpty(currentStaffCode) && 
                    currentStaffCode != item.ProductCategory1)
                {
                    // 現在のページの残り行数を計算してダミー行で埋める
                    int remainingRows = MAX_ROWS_PER_PAGE - (currentPageRows % MAX_ROWS_PER_PAGE);
                    
                    // 35行ちょうどの場合はダミー行不要
                    if (remainingRows < MAX_ROWS_PER_PAGE && remainingRows > 0)
                    {
                        _logger.LogInformation($"担当者変更前の行埋め: 担当者={currentStaffCode}, 残り行数={remainingRows}, 現在行数={currentPageRows}");
                        
                        for (int j = 0; j < remainingRows; j++)
                        {
                            var dummyRow = CreateDummyRow(table, currentStaffCode, currentStaffName);
                            table.Rows.Add(dummyRow);
                            currentPageRows++;
                            _logger.LogDebug($"ダミー行追加: 担当者={currentStaffCode}, ダミー行{j+1}/{remainingRows}");
                        }
                    }
                    
                    // 改ページマーカー行を挿入
                    var pageBreakRow = CreatePageBreakRow(table, item.ProductCategory1, item.ProductCategory1Name);
                    table.Rows.Add(pageBreakRow);
                    
                    // カウンタリセット
                    currentPageRows = 0;
                    currentStaffCode = item.ProductCategory1;
                    currentStaffName = item.ProductCategory1Name;
                    _logger.LogInformation($"担当者変更による改ページ: {currentStaffCode} → {item.ProductCategory1}");
                }
                
                // === 新しい担当者の初回設定 ===
                if (string.IsNullOrEmpty(currentStaffCode) && 
                    item.RowType != RowTypes.ProductSubtotal && 
                    item.RowType != InventorySystem.Reports.Models.RowTypes.ProductSubtotalHeader && 
                    item.RowType != InventorySystem.Reports.Models.RowTypes.BlankLine)
                {
                    currentStaffCode = item.ProductCategory1;
                    currentStaffName = item.ProductCategory1Name;
                    _logger.LogInformation($"新ページ開始: 担当者={currentStaffName}");
                }
                
                // === 35行制限チェック（改善版） ===
                // 小計行セットがページ境界で分割されないようにする
                if (currentPageRows >= MAX_ROWS_PER_PAGE)
                {
                    // 次の3行が小計セットかチェック
                    bool isSubtotalSet = false;
                    if (i + 2 < flatData.Count)
                    {
                        var next1 = flatData[i];
                        var next2 = flatData[i + 1];
                        var next3 = flatData[i + 2];
                        
                        isSubtotalSet = 
                            next1.RowType == InventorySystem.Reports.Models.RowTypes.ProductSubtotalHeader &&
                            next2.RowType == RowTypes.ProductSubtotal &&
                            next3.RowType == InventorySystem.Reports.Models.RowTypes.BlankLine;
                    }
                    
                    // 小計セットの場合は、セット全体を次のページへ
                    if (isSubtotalSet)
                    {
                        // 現在のページを埋める
                        int remainingRows = MAX_ROWS_PER_PAGE - currentPageRows;
                        for (int j = 0; j < remainingRows; j++)
                        {
                            var dummyRow = CreateDummyRow(table, currentStaffCode, currentStaffName);
                            table.Rows.Add(dummyRow);
                        }
                        
                        // 改ページマーカー挿入
                        var pageBreakRow = CreatePageBreakRow(table, currentStaffCode, currentStaffName);
                        table.Rows.Add(pageBreakRow);
                        currentPageRows = 0;
                        
                        _logger.LogInformation($"小計セット保護のための改ページ: 担当者={currentStaffCode}");
                    }
                    else
                    {
                        // 通常の改ページ処理
                        var pageBreakRow = CreatePageBreakRow(table, currentStaffCode, currentStaffName);
                        table.Rows.Add(pageBreakRow);
                        currentPageRows = 0;
                        _logger.LogInformation($"ページブレーク挿入: 担当者={currentStaffCode}, 行数={MAX_ROWS_PER_PAGE}");
                    }
                }
                
                // === 通常行の追加 ===
                var dataRow = table.NewRow();
                
                // 重要：小計行でも必ずProductCategory1とProductCategory1Nameを設定
                dataRow["ProductCategory1"] = currentStaffCode;
                dataRow["ProductCategory1Name"] = currentStaffName;
                
                // その他のフィールドをマッピング
                MapFlatRowToDataRow(item, dataRow);
                
                table.Rows.Add(dataRow);
                currentPageRows++;
                
                // デバッグログ：小計行のProductCategory1確認
                if (item.RowType == RowTypes.ProductSubtotal || item.RowType == InventorySystem.Reports.Models.RowTypes.ProductSubtotalHeader)
                {
                    _logger.LogDebug($"小計行追加: RowType={item.RowType}, ProductCategory1='{dataRow["ProductCategory1"]}', ProductCategory1Name='{dataRow["ProductCategory1Name"]}'");
                }
            }
            
            // === 最後の担当者の行埋め処理 ===
            int finalRemainingRows = MAX_ROWS_PER_PAGE - (currentPageRows % MAX_ROWS_PER_PAGE);
            if (currentPageRows > 0 && finalRemainingRows < MAX_ROWS_PER_PAGE && finalRemainingRows > 0)
            {
                _logger.LogInformation($"最終担当者の行埋め: 担当者={currentStaffCode}, 残り行数={finalRemainingRows}, 現在行数={currentPageRows}");
                
                for (int j = 0; j < finalRemainingRows; j++)
                {
                    var dummyRow = CreateDummyRow(table, currentStaffCode, currentStaffName);
                    table.Rows.Add(dummyRow);
                    currentPageRows++;
                    _logger.LogDebug($"最終ダミー行追加: 担当者={currentStaffCode}, ダミー行{j+1}/{finalRemainingRows}");
                }
                _logger.LogInformation($"最終ページの行埋め完了: 担当者={currentStaffCode}, ダミー行数={finalRemainingRows}");
            }
            
            // === デバッグ: DUMMY行の確認 ===
            // DataTable内のDUMMY行をカウント（ProductNameが"===DUMMY行==="でRowTypeがDetailの行）
            var dummyCount = table.Rows.Cast<DataRow>()
                .Count(r => r["RowType"]?.ToString() == RowTypes.Detail && 
                           r["ProductName"]?.ToString() == "===DUMMY行===" && 
                           r["ProductCode"]?.ToString() == "DUMMY");

            // 担当者ごとのデータ件数を集計
            var debugStaffGroups = table.Rows.Cast<DataRow>()
                .GroupBy(r => r["ProductCategory1"]?.ToString() ?? "")
                .Select(g => new { 
                    StaffCode = g.Key, 
                    TotalRows = g.Count(),
                    DummyRows = g.Count(r => r["RowType"]?.ToString() == RowTypes.Detail && 
                                           r["ProductName"]?.ToString() == "===DUMMY行===" && 
                                           r["ProductCode"]?.ToString() == "DUMMY"),
                    DataRows = g.Count(r => !(r["RowType"]?.ToString() == RowTypes.Detail && 
                                             r["ProductName"]?.ToString() == "===DUMMY行===" && 
                                             r["ProductCode"]?.ToString() == "DUMMY"))
                });

            // 重要情報を目立つログで出力
            _logger.LogCritical("=== 改ページDUMMY行確認 ===");
            _logger.LogCritical($"DataTable総行数: {table.Rows.Count}");
            _logger.LogCritical($"DUMMY行総数: {dummyCount}");

            foreach (var staff in debugStaffGroups)
            {
                _logger.LogCritical($"担当者 {staff.StaffCode}: " +
                                    $"総行数={staff.TotalRows}, " +
                                    $"データ行={staff.DataRows}, " +
                                    $"DUMMY行={staff.DummyRows}");
            }

            // 最初の10個のRowTypeを確認
            _logger.LogCritical("=== 最初の10行のRowType ===");
            for (int i = 0; i < Math.Min(10, table.Rows.Count); i++)
            {
                var rowType = table.Rows[i]["RowType"]?.ToString() ?? "null";
                var staffCode = table.Rows[i]["ProductCategory1"]?.ToString() ?? "null";
                _logger.LogCritical($"行{i}: RowType={rowType}, 担当者={staffCode}");
            }

            // 担当者変更箇所の確認
            _logger.LogCritical("=== 担当者変更箇所の確認 ===");
            string previousStaff = "";
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var currentStaff = table.Rows[i]["ProductCategory1"]?.ToString() ?? "";
                if (previousStaff != "" && previousStaff != currentStaff)
                {
                    // 担当者変更を検出
                    _logger.LogCritical($"行{i}で担当者変更: {previousStaff} → {currentStaff}");
                    
                    // 変更前後5行のRowTypeを表示
                    for (int j = Math.Max(0, i - 5); j < Math.Min(table.Rows.Count, i + 5); j++)
                    {
                        var rt = table.Rows[j]["RowType"]?.ToString() ?? "null";
                        var sc = table.Rows[j]["ProductCategory1"]?.ToString() ?? "null";
                        var marker = j == i ? " <<<< 変更点" : "";
                        _logger.LogCritical($"  行{j}: RowType={rt}, 担当者={sc}{marker}");
                    }
                }
                previousStaff = currentStaff;
            }

            _logger.LogCritical("=== デバッグ終了 ===");
            
            _logger.LogInformation("フラットDataTable作成完了: {Count}行", table.Rows.Count);
            return table;
        }

        /// <summary>
        /// ダミー行作成（ページ埋め用の空行）
        /// </summary>
        private DataRow CreateDummyRow(DataTable table, string staffCode, string staffName)
        {
            _logger.LogCritical($"CreateDummyRow呼び出し: staffCode={staffCode}, RowType=Detail設定");
            
            var row = table.NewRow();
            row["ProductCategory1"] = staffCode;
            row["ProductCategory1Name"] = staffName;
            row["RowType"] = RowTypes.Detail;
            
            // ★デバッグ用：DUMMYという文字を表示して可視化
            row["ProductCode"] = "";          // ★ 変更
            row["ProductName"] = "";   // ★ 変更（目立つように）
            row["ShippingMarkCode"] = "";       // ★ 変更
            row["ShippingMarkName"] = "";     // ★ 変更
            row["ManualShippingMark"] = "";       // ★ 変更
            row["GradeName"] = "";                // ★ 変更
            row["ClassName"] = "";                // ★ 変更
            row["VoucherNumber"] = "";         // ★ 変更
            row["DisplayCategory"] = "";          // ★ 変更
            row["MonthDay"] = "";             // ★ 変更
            row["PurchaseQuantity"] = "";      // ★ 変更
            row["SalesQuantity"] = "";         // ★ 変更
            row["RemainingQuantity"] = "";     // ★ 変更
            row["UnitPrice"] = "";                // ★ 変更
            row["Amount"] = "";                   // ★ 変更
            row["GrossProfit"] = "";              // ★ 変更
            row["CustomerSupplierName"] = ""; // ★ 変更
            
            // 制御フィールド
            row["IsGrayBackground"] = "0";
            row["IsPageBreak"] = "0";
            row["IsBold"] = "0";
            row["RowSequence"] = "0";
            row["PageGroup"] = "";
            
            return row;
        }

        /// <summary>
        /// 改ページマーカー行作成
        /// </summary>
        private DataRow CreatePageBreakRow(DataTable table, string staffCode, string staffName)
        {
            var row = table.NewRow();
            row["ProductCategory1"] = staffCode;
            row["ProductCategory1Name"] = staffName;
            row["RowType"] = RowTypes.PageBreak;
            row["IsPageBreak"] = "1";
            
            // その他のフィールドは型に応じた既定値を設定
            foreach (DataColumn col in table.Columns)
            {
                if (row[col] == DBNull.Value)
                {
                    // データ型に応じて適切な既定値を設定
                    if (col.DataType == typeof(bool))
                    {
                        row[col] = false;  // Boolean型はfalse
                    }
                    else if (col.DataType == typeof(decimal))
                    {
                        row[col] = 0m;  // decimal型は0
                    }
                    else if (col.DataType == typeof(int))
                    {
                        row[col] = 0;  // int型は0
                    }
                    else if (col.DataType == typeof(DateTime))
                    {
                        row[col] = DateTime.MinValue;  // DateTime型は最小値
                    }
                    else
                    {
                        row[col] = "";  // 文字列型は空文字列
                    }
                }
            }
            
            return row;
        }

        /// <summary>
        /// 既存データのマッピング
        /// </summary>
        private void MapFlatRowToDataRow(ProductAccountFlatRow flatRow, DataRow dataRow)
        {
            dataRow["ProductCode"] = flatRow.ProductCode ?? "";
            dataRow["ProductName"] = flatRow.ProductName ?? "";
            dataRow["ShippingMarkCode"] = flatRow.ShippingMarkCode ?? "";
            dataRow["ShippingMarkName"] = flatRow.ShippingMarkName ?? "";
            dataRow["ManualShippingMark"] = flatRow.ManualShippingMark ?? "";
            dataRow["GradeName"] = flatRow.GradeName ?? "";
            dataRow["ClassName"] = flatRow.ClassName ?? "";
            dataRow["VoucherNumber"] = GetLast4Digits(flatRow.VoucherNumber);
            dataRow["DisplayCategory"] = flatRow.DisplayCategory ?? "";
            dataRow["MonthDay"] = flatRow.MonthDay ?? "";
            dataRow["PurchaseQuantity"] = flatRow.PurchaseQuantity ?? "";
            dataRow["SalesQuantity"] = flatRow.SalesQuantity ?? "";
            dataRow["RemainingQuantity"] = flatRow.RemainingQuantity ?? "";
            dataRow["UnitPrice"] = flatRow.UnitPrice ?? "";
            dataRow["Amount"] = flatRow.Amount ?? "";
            dataRow["GrossProfit"] = flatRow.GrossProfit ?? "";
            dataRow["CustomerSupplierName"] = flatRow.CustomerSupplierName ?? "";
            dataRow["IsGrossProfitRate"] = flatRow.IsGrossProfitRate;
            
            // 制御フィールド
            dataRow["RowType"] = flatRow.RowType;
            dataRow["IsGrayBackground"] = flatRow.GrayBackgroundFlag;
            dataRow["IsPageBreak"] = flatRow.PageBreakFlag;
            dataRow["IsBold"] = flatRow.BoldFlag;
            dataRow["RowSequence"] = flatRow.RowSequence.ToString();
            dataRow["PageGroup"] = "";
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
            table.Columns.Add("IsGrossProfitRate", typeof(bool));    // 粗利率フラグ
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
                
                // 文字列フィールド（切り詰め処理を適用）
                row["ProductCode"] = item.ProductCode;
                row["ProductName"] = TruncateString(item.ProductName, 10);  // 10文字切り詰め
                row["ShippingMarkCode"] = item.ShippingMarkCode;  // 荷印コード追加
                row["ShippingMarkName"] = TruncateString(item.ShippingMarkName, 6);  // 6文字切り詰め
                row["ManualShippingMark"] = TruncateString(item.ManualShippingMark, 8);  // 8文字切り詰め
                row["GradeName"] = TruncateString(item.GradeName, 6);  // 6文字切り詰め
                row["ClassName"] = TruncateString(item.ClassName, 6);  // 6文字切り詰め
                row["VoucherNumber"] = GetLast4Digits(item.VoucherNumber);  // 下4桁のみ
                row["DisplayCategory"] = item.DisplayCategory;
                // 前残で日付が未設定(DateTime.MinValue)の場合は空表示
                if (item.TransactionDate == DateTime.MinValue)
                {
                    row["MonthDay"] = ""; // または "--/--"
                }
                else
                {
                    row["MonthDay"] = item.TransactionDate.ToString("MM/dd");
                }
                row["CustomerSupplierName"] = TruncateString(item.CustomerSupplierName, 10);  // 10文字切り詰め
                row["GroupKey"] = item.GroupKey;
                
                // 数値フィールドのフォーマット
                row["PurchaseQuantity"] = FormatQuantity(item.PurchaseQuantity);
                row["SalesQuantity"] = FormatQuantity(item.SalesQuantity);
                row["RemainingQuantity"] = FormatQuantity(item.RemainingQuantity);
                row["UnitPrice"] = FormatUnitPrice(item.UnitPrice);
                row["Amount"] = FormatAmount(item.Amount);
                row["GrossProfit"] = FormatGrossProfit(item.GrossProfit, item.VoucherType, item.CategoryCode);  // ▲処理含む、仕入/振替制御
                
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
        /// 数量フォーマット（小数2桁、0の場合は空文字、マイナスは▲表示）
        /// </summary>
        private string FormatQuantity(decimal value)
        {
            if (value == 0) return "";
            
            if (value < 0)
            {
                // ▲記号を数値の後ろに配置
                return Math.Abs(value).ToString("#,##0.00") + "▲";
            }
            
            return value.ToString("#,##0.00");
        }
        
        /// <summary>
        /// 単価フォーマット（整数部7桁、小数部は四捨五入して整数表示）
        /// </summary>
        private string FormatUnitPrice(decimal value)
        {
            return value == 0 ? "" : Math.Round(value, 0).ToString("#,##0");
        }
        
        /// <summary>
        /// 金額フォーマット（整数部8桁、マイナスは▲表示）
        /// </summary>
        private string FormatAmount(decimal value)
        {
            if (value == 0) return "";
            
            if (value < 0)
            {
                // ▲記号を数値の後ろに配置
                return Math.Abs(Math.Round(value, 0)).ToString("#,##0") + "▲";
            }
            
            return Math.Round(value, 0).ToString("#,##0");
        }
        
        /// <summary>
        /// 粗利益フォーマット（整数部7桁、マイナスは▲表示）
        /// 仕入データ（11,12）と振替データでは空白を返す
        /// </summary>
        private string FormatGrossProfit(decimal value, string voucherType = "", int? categoryCode = null)
        {
            // 仕入データ（11,12）では空白
            if (voucherType == "11" || voucherType == "12")
            {
                return "";
            }

            // 振替データ（在庫調整71かつCategoryCode=4）は空白
            if (voucherType == "71" && categoryCode == 4)
            {
                return "";
            }
            
            if (value == 0) return "0";
            
            if (value < 0)
            {
                // ▲記号を数値の後ろに配置
                return Math.Abs(Math.Round(value, 0)).ToString("#,##0") + "▲";
            }
            
            return Math.Round(value, 0).ToString("#,##0");
        }
        
        /// <summary>
        /// パーセンテージフォーマット
        /// </summary>
        private string FormatPercentage(decimal value)
        {
            if (value == 0) return "0.00 %";
            if (value < 0)
            {
                // 表示順序を 数値 → 空白 → % → ▲ に統一
                return $"{Math.Abs(value):0.00} %▲";
            }
            else
            {
                return $"{value:0.00} %";
            }
        }
        
        // === 集計行専用フォーマットメソッド（0も表示） ===
        
        /// <summary>
        /// 数量フォーマット（集計行専用）- 0も表示
        /// </summary>
        private string FormatQuantityForSubtotal(decimal value)
        {
            if (value == 0) return "0.00";  // 0も表示
            
            if (value < 0)
            {
                return Math.Abs(value).ToString("#,##0.00") + "▲";
            }
            
            return value.ToString("#,##0.00");
        }

        /// <summary>
        /// 単価フォーマット（集計行専用）- 0も表示
        /// </summary>
        private string FormatUnitPriceForSubtotal(decimal value)
        {
            if (value == 0) return "0";  // 0も表示
            return Math.Round(value, 0).ToString("#,##0");
        }

        /// <summary>
        /// 金額フォーマット（集計行専用）- 0も表示
        /// </summary>
        private string FormatAmountForSubtotal(decimal value)
        {
            if (value == 0) return "0";  // 0も表示
            
            if (value < 0)
            {
                return Math.Abs(Math.Round(value, 0)).ToString("#,##0") + "▲";
            }
            
            return Math.Round(value, 0).ToString("#,##0");
        }

        /// <summary>
        /// 粗利益フォーマット（集計行専用）- 0も表示
        /// </summary>
        private string FormatGrossProfitForSubtotal(decimal value)
        {
            if (value == 0) return "0";  // 0も表示
            
            if (value < 0)
            {
                return Math.Abs(Math.Round(value, 0)).ToString("#,##0") + "▲";
            }
            
            return Math.Round(value, 0).ToString("#,##0");
        }

        /// <summary>
        /// パーセンテージフォーマット（集計行専用）- 0も表示
        /// </summary>
        private string FormatPercentageForSubtotal(decimal salesAmount, decimal grossProfit)
        {
            // 売上金額が0の場合は「****」を表示
            if (salesAmount == 0)
            {
                return "****";
            }

            var rate = Math.Round((grossProfit / salesAmount) * 100, 2);
            if (rate < 0)
            {
                // 表示順序を 数値 → 空白 → % → ▲ に統一
                return $"{Math.Abs(rate):0.00} %▲";
            }

            return $"{rate:0.00} %";
        }

        /// <summary>
        /// 粗利率を疑似右揃えでフォーマット（全角スペースパディング）
        /// </summary>
        private string FormatPercentageForSubtotalWithPadding(decimal salesAmount, decimal grossProfit)
        {
            // 要望：全角3文字相当のパディングを付けて表示（左寄せ列での疑似右寄せ用）
            var percentage = FormatPercentageForSubtotal(salesAmount, grossProfit);
            string full = "　"; // 全角スペース U+3000
            return new string(full[0], 3) + percentage;
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
                RowType = RowTypes.PageBreak,
                RowSequence = sequence,
                IsPageBreak = true,
                IsGrayBackground = false,
                ProductCategory1 = staffCode,
                ProductCategory1Name = staffName,
                ProductName = "", // 改ページ専用なので表示内容なし
                GradeCode = "",
                ClassCode = ""
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
            // 明細金額は「対象数量 × 在庫単価」で再計算する
            decimal quantity = 0m;
            if (string.Equals(data.RecordType, "Sales", StringComparison.OrdinalIgnoreCase))
            {
                quantity = data.SalesQuantity;
            }
            else if (string.Equals(data.RecordType, "Purchase", StringComparison.OrdinalIgnoreCase))
            {
                quantity = data.PurchaseQuantity;
            }
            else if (string.Equals(data.RecordType, "Adjustment", StringComparison.OrdinalIgnoreCase))
            {
                quantity = data.PurchaseQuantity != 0 ? data.PurchaseQuantity : data.SalesQuantity;
            }

            decimal unitPrice = data.UnitPrice;
            decimal calculatedAmount = Math.Round(quantity * unitPrice, 0, MidpointRounding.AwayFromZero);

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
                GradeCode = data.GradeCode,
                GradeName = data.GradeName,
                ClassCode = data.ClassCode,
                ClassName = data.ClassName,
                VoucherNumber = data.VoucherNumber,
                DisplayCategory = data.DisplayCategory,
                MonthDay = data.TransactionDate.ToString("MM/dd"),
                CustomerSupplierName = data.CustomerSupplierName,
                IsGrossProfitRate = false,                               // 通常明細は粗利率ではない
                
                // フォーマット済み数値
                PurchaseQuantity = FormatQuantity(data.PurchaseQuantity),
                SalesQuantity = FormatQuantity(data.SalesQuantity),
                RemainingQuantity = FormatQuantity(data.RemainingQuantity),
                UnitPrice = FormatUnitPrice(data.UnitPrice),
                Amount = FormatAmount(calculatedAmount),
                GrossProfit = FormatGrossProfit(data.GrossProfit, data.VoucherType, data.CategoryCode)
            };
        }
        
        /// <summary>
        /// 明細行作成
        /// </summary>
        private ProductAccountFlatRow CreateDetailRow(ProductAccountReportModel data, int sequence)
        {
            // 予備メソッド（使用箇所は限定的だが整合のため同様の計算に合わせる）
            decimal quantity = 0m;
            if (string.Equals(data.RecordType, "Sales", StringComparison.OrdinalIgnoreCase))
            {
                quantity = data.SalesQuantity;
            }
            else if (string.Equals(data.RecordType, "Purchase", StringComparison.OrdinalIgnoreCase))
            {
                quantity = data.PurchaseQuantity;
            }
            else if (string.Equals(data.RecordType, "Adjustment", StringComparison.OrdinalIgnoreCase))
            {
                quantity = data.PurchaseQuantity != 0 ? data.PurchaseQuantity : data.SalesQuantity;
            }

            decimal unitPrice = data.UnitPrice;
            decimal calculatedAmount = Math.Round(quantity * unitPrice, 0, MidpointRounding.AwayFromZero);

            return new ProductAccountFlatRow
            {
                RowType = RowTypes.Detail,
                RowSequence = sequence,
                IsGrayBackground = false,
                IsBold = false,
                
                // 基本情報
                ProductCategory1 = data.ProductCategory1 ?? "",
                ProductCategory1Name = data.ProductCategory1Name ?? "",
                ProductCode = data.ProductCode,
                ProductName = data.ProductName,
                ShippingMarkCode = data.ShippingMarkCode,
                ShippingMarkName = data.ShippingMarkName,
                ManualShippingMark = data.ManualShippingMark,
                GradeCode = data.GradeCode,
                GradeName = data.GradeName,
                ClassCode = data.ClassCode,
                ClassName = data.ClassName,
                VoucherNumber = data.VoucherNumber,
                DisplayCategory = data.DisplayCategory,
                MonthDay = data.TransactionDate.ToString("MM/dd"),
                CustomerSupplierName = data.CustomerSupplierName,
                IsGrossProfitRate = false,                               // 通常明細は粗利率ではない
                
                // フォーマット済み数値
                PurchaseQuantity = FormatQuantity(data.PurchaseQuantity),
                SalesQuantity = FormatQuantity(data.SalesQuantity),
                RemainingQuantity = FormatQuantity(data.RemainingQuantity),
                UnitPrice = FormatUnitPrice(data.UnitPrice),
                Amount = FormatAmount(calculatedAmount),
                GrossProfit = FormatGrossProfit(data.GrossProfit, data.VoucherType, data.CategoryCode)
            };
        }
        
        /// <summary>
        /// 商品別小計見出し行作成
        /// 【前日残】を月日列に右揃えで配置
        /// </summary>
        private ProductAccountFlatRow CreateProductSubtotalHeader(string staffCode, string staffName, int sequence)
        {
            _logger.LogDebug($"小計見出し行生成: StaffCode={staffCode}, StaffName={staffName}");
            
            return new ProductAccountFlatRow
            {
                RowType = InventorySystem.Reports.Models.RowTypes.ProductSubtotalHeader,
                RowSequence = sequence,
                IsBold = false,
                IsGrayBackground = false,
                
                // 担当者情報を設定
                ProductCategory1 = staffCode ?? "",
                ProductCategory1Name = staffName ?? "",
                
                // 商品情報列はすべて空
                ProductName = "",
                ManualShippingMark = "",
                GradeCode = "",
                GradeName = "",
                ClassCode = "",
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
                // 固定パディングでヘッダーを右にずらす（全角空白×2）
                CustomerSupplierName = "　　【粗利率】",
                IsGrossProfitRate = true                        // 粗利率表示フラグ
            };
        }
        
        /// <summary>
        /// 商品別小計数値行作成
        /// 前日残の数値を月日列に右揃えで配置
        /// </summary>
        private ProductAccountFlatRow CreateProductSubtotal(
            string staffCode,
            string staffName,
            decimal previousBalance,      // 前日残（追加）
            decimal purchase,             // 仕入計
            decimal sales,                // 売上計
            decimal currentBalance,       // 当日残（追加）
            decimal inventoryUnitPrice,   // 在庫単価（追加）
            decimal inventoryAmount,      // 在庫金額
            decimal grossProfit,          // 粗利益
            decimal salesAmount,          // 売上伝票金額（追加）
            int sequence)
        {
            _logger.LogDebug($"小計数値行生成: StaffCode={staffCode}, StaffName={staffName}");
            
            return new ProductAccountFlatRow
            {
                RowType = RowTypes.ProductSubtotal,
                RowSequence = sequence,
                IsSubtotal = true,
                IsBold = true,
                IsGrayBackground = true,
                
                // 担当者情報を設定
                ProductCategory1 = staffCode ?? "",
                ProductCategory1Name = staffName ?? "",
                
                // 商品情報列はすべて空
                ProductName = "",
                ManualShippingMark = "",
                GradeCode = "",
                GradeName = "",
                ClassCode = "",
                ClassName = "",
                VoucherNumber = "",
                DisplayCategory = "",                                      // 区分列は空
                
                // 月日列に前日残の数値を配置（▲処理適用）
                MonthDay = FormatQuantityForSubtotal(previousBalance),
                
                // 各集計値
                PurchaseQuantity = FormatQuantityForSubtotal(purchase),
                SalesQuantity = FormatQuantityForSubtotal(sales),
                RemainingQuantity = FormatQuantityForSubtotal(currentBalance),
                UnitPrice = FormatUnitPriceForSubtotal(inventoryUnitPrice),
                Amount = FormatAmountForSubtotal(inventoryAmount),
                GrossProfit = FormatGrossProfitForSubtotal(grossProfit),
                // 粗利率（疑似右揃え）。分母=売上伝票金額（SalesVouchers.Amount合計）
                CustomerSupplierName = FormatPercentageForSubtotalWithPadding(
                    salesAmount,
                    grossProfit),
                IsGrossProfitRate = true                                           // 粗利率表示フラグ
            };
        }
        
        /// <summary>
        /// 担当者別合計行作成
        /// </summary>
        
        
        /// <summary>
        /// 空行作成
        /// </summary>
        private ProductAccountFlatRow CreateBlankRow(string staffCode, string staffName, int sequence)
        {
            return new ProductAccountFlatRow
            {
                RowType = InventorySystem.Reports.Models.RowTypes.BlankLine,
                RowSequence = sequence,
                
                // 担当者情報を設定
                ProductCategory1 = staffCode ?? "",
                ProductCategory1Name = staffName ?? "",
                
                // 商品情報列は空
                GradeCode = "",
                ClassCode = "",
                CustomerSupplierName = "",
                IsGrossProfitRate = false                                // 空行は粗利率ではない
            };
        }
        
        // === 集計計算メソッド ===
        
        /// <summary>
        /// 文字列を指定文字数で切り詰め（全角2バイト、半角1バイトとして計算）
        /// </summary>
        /// <param name="input">入力文字列</param>
        /// <param name="maxCharCount">最大文字数（全角換算）</param>
        /// <returns>切り詰められた文字列</returns>
        private string TruncateString(string input, int maxCharCount)
        {
            if (string.IsNullOrEmpty(input)) return "";
            
            // 単純な文字数カウント方式（全角も半角も1文字として扱う）
            if (input.Length <= maxCharCount)
                return input;
            
            return input.Substring(0, maxCharCount);
        }

        /// <summary>
        /// 伝票番号の下4桁を取得
        /// </summary>
        private string GetLast4Digits(string voucherNumber)
        {
            if (string.IsNullOrEmpty(voucherNumber)) return "";
            
            // 数字のみを抽出
            var digits = new string(voucherNumber.Where(char.IsDigit).ToArray());
            
            // 4桁以上なら下4桁、それ以下ならそのまま返す
            return digits.Length >= 4 ? digits.Substring(digits.Length - 4) : digits;
        }
        
        /// <summary>
        /// ページブレーク行の空フィールドを設定する
        /// </summary>
        private void SetEmptyRowFields(DataRow row)
        {
            row["ProductCode"] = "";
            row["ProductName"] = "";
            row["ShippingMarkCode"] = "";
            row["ShippingMarkName"] = "";
            row["ManualShippingMark"] = "";
            row["GradeName"] = "";
            row["ClassName"] = "";
            row["VoucherNumber"] = "";
            row["DisplayCategory"] = "";
            row["MonthDay"] = "";
            row["PurchaseQuantity"] = "";
            row["SalesQuantity"] = "";
            row["RemainingQuantity"] = "";
            row["UnitPrice"] = "";
            row["Amount"] = "";
            row["GrossProfit"] = "";
            row["CustomerSupplierName"] = "";
            row["IsGrayBackground"] = "0";
            row["IsPageBreak"] = "1";
            row["IsBold"] = "0";
            row["RowSequence"] = "0";
            row["PageGroup"] = "";
        }
        
        /// <summary>
        /// FastReportのスクリプトを完全に無効化する
        /// </summary>
        private void SetScriptLanguageToNone(FR.Report report)
        {
            try
            {
                // ScriptLanguageをNoneに設定
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
                            _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }
                
                // Scriptプロパティをnullに設定（重要）
                var scriptProperty = report.GetType().GetProperty("Script", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                    _logger.LogInformation("Scriptプロパティをnullに設定しました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
            }
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
