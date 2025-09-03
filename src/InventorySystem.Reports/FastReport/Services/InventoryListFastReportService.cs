using System.Data;
using System.Globalization;
using FastReport;
using FastReport.Export.PdfSimple;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Models;

namespace InventorySystem.Reports.FastReport.Services;

/// <summary>
/// 在庫表FastReport帳票サービス
/// ProductAccountFastReportServiceと同じ7段階PDF生成プロセスを使用
/// </summary>
public class InventoryListFastReportService
{
    private readonly ICpInventoryRepository _cpInventoryRepository;
    private readonly ILogger<InventoryListFastReportService> _logger;
    private readonly string _templatePath;
    private readonly string _tempDirectory;

    public InventoryListFastReportService(
        ICpInventoryRepository cpInventoryRepository,
        ILogger<InventoryListFastReportService> logger)
    {
        _cpInventoryRepository = cpInventoryRepository;
        _logger = logger;
        
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "InventoryList.frx");
        _tempDirectory = Path.Combine(Path.GetTempPath(), "InventoryList", Guid.NewGuid().ToString());
        
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// 在庫表PDF生成（7段階プロセス）
    /// </summary>
    public async Task<byte[]> GenerateInventoryListPdfAsync(DateTime jobDate)
    {
        _logger.LogInformation("在庫表PDF生成開始 - 作業日: {JobDate}", jobDate);

        try
        {
            // Phase 1: データ準備
            var cpInventoryData = await GetCpInventoryDataAsync();
            if (!cpInventoryData.Any())
            {
                throw new InvalidOperationException("CP在庫マスタが作成されていません。先に商品勘定を実行してください。");
            }

            // Phase 2: フラットデータ生成
            var flatData = CreateInventoryFlatData(cpInventoryData, jobDate);

            // Phase 3: 担当者別グループ化
            var staffGroups = GroupDataByStaff(flatData);

            // Phase 4: 一時PDF生成（ページ数カウント用）
            var tempPdfPaths = new List<string>();
            var staffPageCounts = new Dictionary<string, int>();

            foreach (var staffGroup in staffGroups)
            {
                var tempPdfPath = await CreateTempPdfForStaffAsync(staffGroup.Key, staffGroup.Value, jobDate, 1, 1);
                tempPdfPaths.Add(tempPdfPath);
                
                // ページ数をカウント
                var pageCount = CountPdfPages(tempPdfPath);
                staffPageCounts[staffGroup.Key] = pageCount;
                
                _logger.LogDebug("担当者 {StaffCode} の一時PDF作成完了 - ページ数: {PageCount}", staffGroup.Key, pageCount);
            }

            // Phase 5: 総ページ数計算とページ番号割り当て
            var totalPages = staffPageCounts.Values.Sum();
            var currentPageStart = 1;
            var staffPageRanges = new Dictionary<string, (int Start, int End, int Total)>();

            foreach (var staffGroup in staffGroups)
            {
                var pageCount = staffPageCounts[staffGroup.Key];
                staffPageRanges[staffGroup.Key] = (currentPageStart, currentPageStart + pageCount - 1, totalPages);
                currentPageStart += pageCount;
            }

            // Phase 6: 正式PDF生成（正確なページ番号付き）
            var finalPdfPaths = new List<string>();
            
            foreach (var staffGroup in staffGroups)
            {
                var pageRange = staffPageRanges[staffGroup.Key];
                var finalPdfPath = await CreateFinalPdfForStaffAsync(
                    staffGroup.Key, 
                    staffGroup.Value, 
                    jobDate,
                    pageRange.Start, 
                    pageRange.Total);
                    
                finalPdfPaths.Add(finalPdfPath);
                
                _logger.LogDebug("担当者 {StaffCode} の最終PDF作成完了 - ページ範囲: {Start}-{End}/{Total}", 
                    staffGroup.Key, pageRange.Start, pageRange.End, pageRange.Total);
            }

            // Phase 7: PDF結合
            var mergedPdfBytes = MergePdfs(finalPdfPaths);

            _logger.LogInformation("在庫表PDF生成完了 - 総ページ数: {TotalPages}, 担当者数: {StaffCount}", 
                totalPages, staffGroups.Count);

            return mergedPdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫表PDF生成でエラーが発生しました - 作業日: {JobDate}", jobDate);
            throw;
        }
        finally
        {
            // 一時ファイルクリーンアップ
            CleanupTempFiles();
        }
    }

    /// <summary>
    /// Phase 1: CP在庫マスタからデータ取得
    /// </summary>
    private async Task<List<dynamic>> GetCpInventoryDataAsync()
    {
        _logger.LogDebug("CP在庫マスタからデータ取得開始");
        
        var cpInventories = await _cpInventoryRepository.GetAllAsync();
        
        // 動的オブジェクトに変換（在庫表用フィールドのみ）
        var result = cpInventories.Select(cp => new
        {
            ProductCategory1 = cp.ProductCategory1 ?? string.Empty,
            ProductCode = cp.Key.ProductCode,
            ProductName = cp.ProductName ?? cp.Key.ProductCode,
            ShippingMarkCode = cp.Key.ShippingMarkCode,
            ShippingMarkName = cp.ShippingMarkName ?? cp.Key.ShippingMarkCode,
            ManualShippingMark = cp.Key.ManualShippingMark,
            GradeCode = cp.Key.GradeCode,
            GradeName = cp.Key.GradeCode, // 仮実装：等級名はマスタから取得予定
            ClassCode = cp.Key.ClassCode,
            ClassName = cp.Key.ClassCode, // 仮実装：階級名はマスタから取得予定
            DailyStock = cp.DailyStock,
            DailyUnitPrice = cp.DailyUnitPrice,
            DailyStockAmount = cp.DailyStockAmount,
            PreviousDayStock = cp.PreviousDayStock,
            PreviousDayStockAmount = cp.PreviousDayStockAmount,
            LastReceiptDate = cp.DailyReceiptQuantity > 0 ? cp.JobDate : (DateTime?)null
        }).Cast<dynamic>().ToList();

        _logger.LogDebug("CP在庫マスタからデータ取得完了 - 件数: {Count}", result.Count);
        return result;
    }

    /// <summary>
    /// Phase 2: フラットデータ生成（印字対象フィルタリング含む）
    /// </summary>
    private List<InventoryFlatRow> CreateInventoryFlatData(List<dynamic> cpInventoryData, DateTime jobDate)
    {
        _logger.LogDebug("在庫表フラットデータ生成開始");
        
        var flatRows = new List<InventoryFlatRow>();
        var sequence = 1;

        // データフィルタリング
        var filteredData = cpInventoryData.Where(cp => 
        {
            // 条件1: 前日在庫数が0の行は除外
            if (cp.PreviousDayStock <= 0) return false;
            
            // 条件2: 当日在庫数量・金額が両方0の行は除外
            if (cp.DailyStock <= 0 && cp.DailyStockAmount <= 0) return false;
            
            return true;
        }).ToList();

        // 担当者コード → 商品コード → 荷印 → 等級 → 階級でソート
        var sortedData = filteredData
            .OrderBy(cp => cp.ProductCategory1 ?? string.Empty)
            .ThenBy(cp => cp.ProductCode)
            .ThenBy(cp => cp.ShippingMarkCode)
            .ThenBy(cp => cp.ManualShippingMark)
            .ThenBy(cp => cp.GradeCode)
            .ThenBy(cp => cp.ClassCode)
            .ToList();

        // フラットデータ変換
        foreach (var cp in sortedData)
        {
            var flatRow = new InventoryFlatRow
            {
                StaffCode = cp.ProductCategory1 ?? string.Empty,
                StaffName = $"担当者{cp.ProductCategory1}", // 仮実装
                ProductCode = cp.ProductCode,
                Col1 = cp.ProductName, // 商品名
                Col2 = cp.ShippingMarkName, // 荷印名
                Col3 = cp.GradeName, // 等級名
                Col4 = cp.ClassName, // 階級名
                Col5 = FormatQuantity(cp.DailyStock), // 在庫数量
                Col6 = FormatCurrency(cp.DailyUnitPrice), // 在庫単価
                Col7 = FormatCurrency(cp.DailyStockAmount), // 在庫金額
                Col8 = FormatDate(cp.LastReceiptDate), // 最終入荷日
                Col9 = CalculateStagnationMark(jobDate, cp.LastReceiptDate), // 滞留マーク
                
                // 計算用データ
                StockQuantity = cp.DailyStock,
                StockAmount = cp.DailyStockAmount,
                
                RowSequence = sequence++,
                RowType = InventoryRowTypes.Detail
            };

            flatRows.Add(flatRow);
        }

        _logger.LogDebug("在庫表フラットデータ生成完了 - 印字対象件数: {Count}", flatRows.Count);
        return flatRows;
    }

    /// <summary>
    /// Phase 3: 担当者別グループ化（小計・合計行追加）
    /// </summary>
    private Dictionary<string, List<InventoryFlatRow>> GroupDataByStaff(List<InventoryFlatRow> flatData)
    {
        _logger.LogDebug("担当者別グループ化開始");
        
        var staffGroups = new Dictionary<string, List<InventoryFlatRow>>();
        var globalSequence = 1;

        var dataByStaff = flatData.GroupBy(row => row.StaffCode).OrderBy(g => g.Key);

        foreach (var staffGroup in dataByStaff)
        {
            var staffCode = staffGroup.Key;
            var staffRows = new List<InventoryFlatRow>();

            // 担当者ヘッダー行追加
            var staffHeader = CreateStaffHeader(staffCode, staffGroup.First().StaffName, globalSequence++);
            staffRows.Add(staffHeader);

            // 商品別グループ化
            var productGroups = staffGroup.GroupBy(row => row.ProductCode).OrderBy(g => g.Key);

            foreach (var productGroup in productGroups)
            {
                var productCode = productGroup.Key;
                
                // 商品グループヘッダー行追加
                var productHeader = CreateProductGroupHeader(productCode, productGroup.First().Col1, globalSequence++);
                staffRows.Add(productHeader);

                // 明細行追加
                foreach (var detailRow in productGroup)
                {
                    detailRow.RowSequence = globalSequence++;
                    detailRow.PageGroup = staffCode; // 担当者別ページ管理
                    staffRows.Add(detailRow);
                }

                // 商品別小計追加
                var productSubtotal = CreateProductSubtotal(productCode, productGroup, globalSequence);
                staffRows.AddRange(productSubtotal);
                globalSequence += productSubtotal.Count;

                // 商品間の空行
                var blankLine = CreateBlankLine(globalSequence++);
                staffRows.Add(blankLine);
            }

            // 担当者別合計追加
            var staffTotal = CreateStaffTotal(staffCode, staffGroup, globalSequence);
            staffRows.AddRange(staffTotal);
            globalSequence += staffTotal.Count;

            staffGroups[staffCode] = staffRows;
        }

        _logger.LogDebug("担当者別グループ化完了 - 担当者数: {StaffCount}", staffGroups.Count);
        return staffGroups;
    }

    /// <summary>
    /// 担当者ヘッダー行作成
    /// </summary>
    private InventoryFlatRow CreateStaffHeader(string staffCode, string staffName, int sequence)
    {
        return new InventoryFlatRow
        {
            StaffCode = staffCode,
            StaffName = staffName,
            Col1 = $"【担当者: {staffCode} {staffName}】",
            RowType = InventoryRowTypes.StaffHeader,
            RowSequence = sequence,
            IsPageBreak = true,
            IsBold = true,
            IsGrayBackground = true,
            PageGroup = staffCode
        };
    }

    /// <summary>
    /// 商品グループヘッダー行作成
    /// </summary>
    private InventoryFlatRow CreateProductGroupHeader(string productCode, string productName, int sequence)
    {
        return new InventoryFlatRow
        {
            ProductCode = productCode,
            Col1 = $"■ {productCode} {productName}",
            RowType = InventoryRowTypes.ProductGroupHeader,
            RowSequence = sequence,
            IsBold = true,
            IsGrayBackground = true
        };
    }

    /// <summary>
    /// 商品別小計行作成
    /// </summary>
    private List<InventoryFlatRow> CreateProductSubtotal(string productCode, IGrouping<string, InventoryFlatRow> productGroup, int startSequence)
    {
        var subtotalRows = new List<InventoryFlatRow>();
        var sequence = startSequence;

        // 小計見出し行
        var subtotalHeader = new InventoryFlatRow
        {
            ProductCode = productCode,
            Col1 = $"【{productCode} 小計】",
            RowType = InventoryRowTypes.ProductSubtotalHeader,
            RowSequence = sequence++,
            IsBold = true,
            IsSubtotal = true
        };
        subtotalRows.Add(subtotalHeader);

        // 小計数値行
        var totalQuantity = productGroup.Sum(row => row.StockQuantity);
        var totalAmount = productGroup.Sum(row => row.StockAmount);
        
        var subtotalData = new InventoryFlatRow
        {
            ProductCode = productCode,
            Col5 = FormatQuantity(totalQuantity), // 合計数量
            Col7 = FormatCurrency(totalAmount), // 合計金額
            RowType = InventoryRowTypes.ProductSubtotal,
            RowSequence = sequence++,
            IsBold = true,
            IsSubtotal = true,
            StockQuantity = totalQuantity,
            StockAmount = totalAmount
        };
        subtotalRows.Add(subtotalData);

        return subtotalRows;
    }

    /// <summary>
    /// 担当者別合計行作成
    /// </summary>
    private List<InventoryFlatRow> CreateStaffTotal(string staffCode, IGrouping<string, InventoryFlatRow> staffGroup, int startSequence)
    {
        var totalRows = new List<InventoryFlatRow>();
        var sequence = startSequence;

        // 合計見出し行
        var totalHeader = new InventoryFlatRow
        {
            StaffCode = staffCode,
            Col1 = $"【担当者 {staffCode} 合計】",
            RowType = InventoryRowTypes.StaffTotalHeader,
            RowSequence = sequence++,
            IsBold = true,
            IsSubtotal = true
        };
        totalRows.Add(totalHeader);

        // 合計数値行
        var totalQuantity = staffGroup.Sum(row => row.StockQuantity);
        var totalAmount = staffGroup.Sum(row => row.StockAmount);
        
        var totalData = new InventoryFlatRow
        {
            StaffCode = staffCode,
            Col5 = FormatQuantity(totalQuantity), // 合計数量
            Col7 = FormatCurrency(totalAmount), // 合計金額
            RowType = InventoryRowTypes.StaffTotal,
            RowSequence = sequence++,
            IsBold = true,
            IsSubtotal = true,
            StockQuantity = totalQuantity,
            StockAmount = totalAmount
        };
        totalRows.Add(totalData);

        return totalRows;
    }

    /// <summary>
    /// 空行作成
    /// </summary>
    private InventoryFlatRow CreateBlankLine(int sequence)
    {
        return new InventoryFlatRow
        {
            RowType = InventoryRowTypes.BlankLine,
            RowSequence = sequence
        };
    }

    /// <summary>
    /// Phase 4: 一時PDF生成（ページ数カウント用）
    /// </summary>
    private async Task<string> CreateTempPdfForStaffAsync(string staffCode, List<InventoryFlatRow> staffData, DateTime jobDate, int pageStart, int totalPages)
    {
        var tempFileName = $"temp_staff_{staffCode}_{Guid.NewGuid()}.pdf";
        var tempFilePath = Path.Combine(_tempDirectory, tempFileName);

        await CreatePdfForStaffDataAsync(staffData, jobDate, pageStart, totalPages, tempFilePath);
        
        return tempFilePath;
    }

    /// <summary>
    /// Phase 6: 最終PDF生成（正確なページ番号付き）
    /// </summary>
    private async Task<string> CreateFinalPdfForStaffAsync(string staffCode, List<InventoryFlatRow> staffData, DateTime jobDate, int pageStart, int totalPages)
    {
        var finalFileName = $"final_staff_{staffCode}_{Guid.NewGuid()}.pdf";
        var finalFilePath = Path.Combine(_tempDirectory, finalFileName);

        // ページ番号をデータに反映
        var currentPage = pageStart;
        foreach (var row in staffData)
        {
            row.PageNumber = currentPage;
            row.TotalPages = totalPages;
        }

        await CreatePdfForStaffDataAsync(staffData, jobDate, pageStart, totalPages, finalFilePath);
        
        return finalFilePath;
    }

    /// <summary>
    /// 担当者データ用PDF生成（共通処理）
    /// </summary>
    private async Task<byte[]> CreatePdfForStaffDataAsync(List<InventoryFlatRow> staffData, DateTime jobDate, int pageStart, int totalPages, string outputPath)
    {
        using var report = new Report();
        report.Load(_templatePath);

        // パラメータ設定
        report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分ss秒"));
        report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));

        // データソース設定
        var dataTable = ConvertToDataTable(staffData);
        report.RegisterData(dataTable, "InventoryData");

        // PDF生成
        report.Prepare();
        
        using var pdfExport = new PDFSimpleExport();
        using var stream = new MemoryStream();
        report.Export(pdfExport, stream);
        
        var pdfBytes = stream.ToArray();
        await File.WriteAllBytesAsync(outputPath, pdfBytes);
        
        return pdfBytes;
    }

    /// <summary>
    /// InventoryFlatRow → DataTable変換
    /// </summary>
    private DataTable ConvertToDataTable(List<InventoryFlatRow> flatData)
    {
        var dataTable = new DataTable("InventoryData");
        
        // カラム定義
        dataTable.Columns.Add("StaffCode", typeof(string));
        dataTable.Columns.Add("StaffName", typeof(string));
        dataTable.Columns.Add("Col1", typeof(string)); // 商品名
        dataTable.Columns.Add("Col2", typeof(string)); // 荷印名
        dataTable.Columns.Add("Col3", typeof(string)); // 等級名
        dataTable.Columns.Add("Col4", typeof(string)); // 階級名
        dataTable.Columns.Add("Col5", typeof(string)); // 在庫数量
        dataTable.Columns.Add("Col6", typeof(string)); // 在庫単価
        dataTable.Columns.Add("Col7", typeof(string)); // 在庫金額
        dataTable.Columns.Add("Col8", typeof(string)); // 最終入荷日
        dataTable.Columns.Add("Col9", typeof(string)); // 滞留マーク
        
        // 制御フィールド
        dataTable.Columns.Add("RowType", typeof(string));
        dataTable.Columns.Add("RowSequence", typeof(int));
        dataTable.Columns.Add("PageBreakFlag", typeof(string));
        dataTable.Columns.Add("GrayBackgroundFlag", typeof(string));
        dataTable.Columns.Add("BoldFlag", typeof(string));
        dataTable.Columns.Add("PageNumber", typeof(int));
        dataTable.Columns.Add("TotalPages", typeof(int));
        dataTable.Columns.Add("PageGroup", typeof(string));
        dataTable.Columns.Add("CurrentPage", typeof(string));
        dataTable.Columns.Add("TotalPagesDisplay", typeof(string));

        // データ行追加
        foreach (var row in flatData)
        {
            var dataRow = dataTable.NewRow();
            dataRow["StaffCode"] = row.StaffCode;
            dataRow["StaffName"] = row.StaffName;
            dataRow["Col1"] = row.Col1;
            dataRow["Col2"] = row.Col2;
            dataRow["Col3"] = row.Col3;
            dataRow["Col4"] = row.Col4;
            dataRow["Col5"] = row.Col5;
            dataRow["Col6"] = row.Col6;
            dataRow["Col7"] = row.Col7;
            dataRow["Col8"] = row.Col8;
            dataRow["Col9"] = row.Col9;
            
            dataRow["RowType"] = row.RowType;
            dataRow["RowSequence"] = row.RowSequence;
            dataRow["PageBreakFlag"] = row.PageBreakFlag;
            dataRow["GrayBackgroundFlag"] = row.GrayBackgroundFlag;
            dataRow["BoldFlag"] = row.BoldFlag;
            dataRow["PageNumber"] = row.PageNumber;
            dataRow["TotalPages"] = row.TotalPages;
            dataRow["PageGroup"] = row.PageGroup;
            dataRow["CurrentPage"] = row.PageNumber.ToString();
            dataRow["TotalPagesDisplay"] = row.TotalPages.ToString();
            
            dataTable.Rows.Add(dataRow);
        }

        return dataTable;
    }

    /// <summary>
    /// Phase 5: PDFページ数カウント
    /// </summary>
    private int CountPdfPages(string pdfFilePath)
    {
        try
        {
            using var document = PdfReader.Open(pdfFilePath, PdfDocumentOpenMode.ReadOnly);
            return document.PageCount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDFページ数カウント失敗 - ファイル: {FilePath}", pdfFilePath);
            return 1; // デフォルト値
        }
    }

    /// <summary>
    /// Phase 7: PDF結合
    /// </summary>
    private byte[] MergePdfs(List<string> pdfFilePaths)
    {
        _logger.LogDebug("PDF結合開始 - ファイル数: {FileCount}", pdfFilePaths.Count);
        
        using var mergedDocument = new PdfDocument();
        
        foreach (var pdfPath in pdfFilePaths)
        {
            try
            {
                using var inputDocument = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
                
                for (int pageIndex = 0; pageIndex < inputDocument.PageCount; pageIndex++)
                {
                    var page = inputDocument.Pages[pageIndex];
                    mergedDocument.AddPage(page);
                }
                
                _logger.LogDebug("PDF結合 - ファイル追加完了: {FilePath} ({PageCount}ページ)", 
                    pdfPath, inputDocument.PageCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF結合エラー - ファイル: {FilePath}", pdfPath);
                throw;
            }
        }

        using var stream = new MemoryStream();
        mergedDocument.Save(stream);
        
        var mergedBytes = stream.ToArray();
        _logger.LogDebug("PDF結合完了 - 総ページ数: {TotalPages}, サイズ: {Size}KB", 
            mergedDocument.PageCount, mergedBytes.Length / 1024);
            
        return mergedBytes;
    }

    /// <summary>
    /// 一時ファイルクリーンアップ
    /// </summary>
    private void CleanupTempFiles()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
                _logger.LogDebug("一時ファイルクリーンアップ完了 - ディレクトリ: {TempDirectory}", _tempDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "一時ファイルクリーンアップ失敗 - ディレクトリ: {TempDirectory}", _tempDirectory);
        }
    }

    // === フォーマット用ヘルパーメソッド ===

    /// <summary>
    /// 数量フォーマット（整数部カンマ区切り、小数部2桁）
    /// </summary>
    private string FormatQuantity(decimal quantity)
    {
        if (quantity == 0) return "";
        return quantity.ToString("#,##0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 通貨フォーマット（整数部カンマ区切り、小数部なし）
    /// </summary>
    private string FormatCurrency(decimal amount)
    {
        if (amount == 0) return "";
        return amount.ToString("#,##0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 日付フォーマット（MM/dd形式）
    /// </summary>
    private string FormatDate(DateTime? date)
    {
        return date?.ToString("MM/dd") ?? "";
    }

    /// <summary>
    /// 滞留マーク計算（簡易版）
    /// </summary>
    private string CalculateStagnationMark(DateTime reportDate, DateTime? lastReceiptDate)
    {
        if (lastReceiptDate == null) return "";
        
        var daysSinceReceipt = (reportDate - lastReceiptDate.Value).Days;
        
        if (daysSinceReceipt >= 31) return "!!!";
        if (daysSinceReceipt >= 21) return "!!";
        if (daysSinceReceipt >= 11) return "!";
        
        return "";
    }
}