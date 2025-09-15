// Windows限定（FastReport/PdfSharp 使用）
#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FastReport;
using FastReport.Export.Pdf;
using FR = global::FastReport;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Dapper;
using System.Data.SqlClient;
using InventorySystem.Reports.Interfaces;

namespace InventorySystem.Reports.FastReport.Services
{
    /// <summary>
    /// 在庫表PDF生成サービス（商品勘定と同じ7段階プロセス）
    /// </summary>
    public class InventoryListFastReportService : IInventoryListReportService
    {
        private readonly ILogger<InventoryListFastReportService> _logger;
        private readonly IConfiguration _configuration;
        private const int MaxRowsPerPage = 35;
        private readonly string _templatePath;

        public InventoryListFastReportService(
            ILogger<InventoryListFastReportService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "InventoryList.frx");
        }

        public byte[] GenerateInventoryListReport(DateTime jobDate, string? departmentCode = null)
        {
            _logger.LogInformation("在庫表7段階プロセス開始: {JobDate}", jobDate);

            // Phase 1: データ準備（SQL 1回）
            var rows = QueryInventoryRows(jobDate);
            if (rows.Count == 0)
            {
                _logger.LogWarning("対象データ0件: JobDate={JobDate}", jobDate);
                return Array.Empty<byte>();
            }

            // NULL担当者→'000'
            foreach (var r in rows)
            {
                if (string.IsNullOrEmpty(r.StaffCode))
                {
                    r.StaffCode = "000";
                    r.StaffName = string.IsNullOrEmpty(r.StaffName) ? "担当者未設定" : r.StaffName;
                }
            }

            // Phase 2: 担当者別フラットデータ生成
            var grouped = rows
                .GroupBy(r => r.StaffCode)
                .OrderBy(g => g.Key)
                .ToList();

            string tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", $"Temp_{DateTime.Now:yyyyMMddHHmmss}");
            Directory.CreateDirectory(tempFolder);
            _logger.LogInformation("一時フォルダ: {Folder}", tempFolder);

            var firstPassFiles = new Dictionary<string, string>();

            try
            {
                // Phase 3: 1次PDF（仮ページ番号）
                foreach (var g in grouped)
                {
                    var dt = BuildDataTable(jobDate, g.ToList());
                    var pdf = GeneratePdfForDataTable(dt, jobDate, 1, 999, 999);
                    var path = Path.Combine(tempFolder, $"{g.Key}_{Guid.NewGuid()}.pdf");
                    File.WriteAllBytes(path, pdf);
                    firstPassFiles[g.Key] = path;
                }

                // Phase 4: 実ページ数取得
                var pageCounts = new Dictionary<string, int>();
                int totalPages = 0;
                foreach (var kv in firstPassFiles)
                {
                    using var doc = PdfReader.Open(kv.Value, PdfDocumentOpenMode.Import);
                    pageCounts[kv.Key] = doc.PageCount;
                    totalPages += doc.PageCount;
                }

                // Phase 5: 2次PDF（正確なページ番号）
                var finalPdfs = new List<byte[]>();
                int startPage = 1;
                foreach (var g in grouped)
                {
                    var dt = BuildDataTable(jobDate, g.ToList());
                    var pageCount = pageCounts[g.Key];
                    var pdf = GeneratePdfForDataTable(dt, jobDate, startPage, pageCount, totalPages);
                    finalPdfs.Add(pdf);
                    startPage += pageCount;
                }

                // Phase 6: 結合
                var merged = MergePdfFiles(finalPdfs);
                return merged;
            }
            finally
            {
                // Phase 7: クリーンアップ
                try
                {
                    if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "一時フォルダ削除失敗: {Folder}", tempFolder);
                }
            }
        }

        private List<InventoryRow> QueryInventoryRows(DateTime jobDate)
        {
            const string sql = @"
SELECT 
    ISNULL(p.ProductCategory1, '000') AS StaffCode,
    cp.ProductCode,
    p.ProductName,
    cp.ShippingMarkCode,
    cp.ShippingMarkName,
    cp.ManualShippingMark,
    cp.GradeCode,
    cp.GradeName,
    cp.ClassCode,
    cp.ClassName,
    cp.DailyStock,
    cp.DailyUnitPrice,
    cp.DailyStockAmount,
    cp.LastReceiptDate
FROM CpInventoryMaster cp
INNER JOIN ProductMaster p ON cp.ProductCode = p.ProductCode
WHERE cp.JobDate = @JobDate
  AND cp.DailyStock <> 0
ORDER BY 
    ISNULL(p.ProductCategory1, '000'),
    cp.ProductCode,
    cp.ShippingMarkCode,
    cp.ManualShippingMark,
    cp.GradeCode,
    cp.ClassCode";

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                var list = conn.Query<InventoryRow>(sql, new { JobDate = jobDate }).ToList();
                _logger.LogInformation("取得件数: {Count}", list.Count);
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "在庫表データ取得エラー");
                return new List<InventoryRow>();
            }
        }

        private DataTable BuildDataTable(DateTime jobDate, List<InventoryRow> items)
        {
            var dt = new DataTable("InventoryData");
            dt.Columns.Add("RowType", typeof(string));
            dt.Columns.Add("IsPageBreak", typeof(string));
            dt.Columns.Add("IsBold", typeof(string));
            dt.Columns.Add("IsGrayBackground", typeof(string));
            dt.Columns.Add("StaffCode", typeof(string));
            dt.Columns.Add("StaffName", typeof(string));
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
            // ページ番号列（frx参照用）
            dt.Columns.Add("CurrentPage", typeof(string));
            dt.Columns.Add("TotalPages", typeof(string));

            // ヘッダー（担当者）
            if (items.Count > 0)
            {
                var head = dt.NewRow();
                head["RowType"] = "STAFF_HEADER";
                head["IsPageBreak"] = "1"; // 担当者の先頭で改ページ
                head["IsBold"] = "1";
                head["IsGrayBackground"] = "1";
                head["StaffCode"] = items[0].StaffCode;
                head["StaffName"] = string.Empty; // 在庫表では担当者名は出力しない
                dt.Rows.Add(head);
            }

            string? prevProduct = null;
            decimal subtotalQty = 0m;
            decimal subtotalAmt = 0m;
            int detailCountOnPage = 0;

            foreach (var x in items)
            {
                // 商品境界で小計
                if (!string.IsNullOrEmpty(prevProduct) && prevProduct != x.ProductCode)
                {
                    AddSubtotalRow(dt, subtotalQty, subtotalAmt, items[0].StaffCode, string.Empty);
                    subtotalQty = 0m;
                    subtotalAmt = 0m;
                    detailCountOnPage += 2; // 前後の空行
                }

                var row = dt.NewRow();
                row["RowType"] = "DETAIL";
                row["IsPageBreak"] = "0";
                row["IsBold"] = "0";
                row["IsGrayBackground"] = "0";
                row["StaffCode"] = x.StaffCode;
                row["StaffName"] = string.Empty; // 在庫表は担当者名を出力しない
                row["Col1"] = x.ProductName ?? string.Empty;
                var shipping = string.IsNullOrEmpty(x.ManualShippingMark) ? (x.ShippingMarkName ?? x.ShippingMarkCode) : x.ManualShippingMark;
                row["Col2"] = shipping ?? string.Empty;
                row["ColManual"] = x.ManualShippingMark ?? string.Empty;
                row["Col3"] = x.GradeName ?? string.Empty;
                row["Col4"] = x.ClassName ?? string.Empty;
                row["Col5"] = FormatQuantity(x.DailyStock);
                row["Col6"] = FormatUnitPrice(x.DailyUnitPrice);
                row["Col7"] = FormatAmount(x.DailyStockAmount);
                row["Col8"] = x.LastReceiptDate.HasValue ? x.LastReceiptDate.Value.ToString("(yy-MM-dd)") : string.Empty;
                row["Col9"] = x.DailyStock > 0 && x.LastReceiptDate.HasValue ? CalculateStagnationMark(x.LastReceiptDate, jobDate) : string.Empty;
                dt.Rows.Add(row);

                subtotalQty += x.DailyStock;
                subtotalAmt += x.DailyStockAmount;
                prevProduct = x.ProductCode;

                // ページ制御（明細行ベース）
                detailCountOnPage++;
                if (detailCountOnPage >= MaxRowsPerPage)
                {
                    AddPageBreak(dt, items[0].StaffCode, string.Empty);
                    detailCountOnPage = 0;
                }
            }

            // 最後の小計
            if (!string.IsNullOrEmpty(prevProduct))
            {
                AddSubtotalRow(dt, subtotalQty, subtotalAmt, items[0].StaffCode, string.Empty);
            }

            // 担当者合計
            var totalQty = items.Sum(i => i.DailyStock);
            var totalAmt = items.Sum(i => i.DailyStockAmount);
            AddStaffTotalRow(dt, totalQty, totalAmt, items[0].StaffCode, string.Empty);

            return dt;
        }

        private void AddPageBreak(DataTable dt, string staffCode, string staffName)
        {
            var br = dt.NewRow();
            br["RowType"] = "PAGE_BREAK";
            br["IsPageBreak"] = "1";
            br["IsBold"] = "0";
            br["IsGrayBackground"] = "0";
            br["StaffCode"] = staffCode;
            br["StaffName"] = staffName;
            dt.Rows.Add(br);
        }

        private void AddSubtotalRow(DataTable dt, decimal qty, decimal amt, string staffCode, string staffName)
        {
            AddBlank(dt, staffCode, staffName);
            var r = dt.NewRow();
            r["RowType"] = "PRODUCT_SUBTOTAL";
            r["IsPageBreak"] = "0";
            r["IsBold"] = "1";
            r["IsGrayBackground"] = "0";
            r["StaffCode"] = staffCode;
            r["StaffName"] = staffName;
            r["Col1"] = "＊　小　 計　＊";
            r["Col5"] = FormatQuantity(qty);
            r["Col7"] = FormatAmount(amt);
            dt.Rows.Add(r);
            AddBlank(dt, staffCode, staffName);
        }

        private void AddBlank(DataTable dt, string staffCode, string staffName)
        {
            var r = dt.NewRow();
            r["RowType"] = "BLANK";
            r["IsPageBreak"] = "0";
            r["IsBold"] = "0";
            r["IsGrayBackground"] = "0";
            r["StaffCode"] = staffCode;
            r["StaffName"] = staffName;
            dt.Rows.Add(r);
        }

        private void AddStaffTotalRow(DataTable dt, decimal qty, decimal amt, string staffCode, string staffName)
        {
            AddBlank(dt, staffCode, staffName);
            var r = dt.NewRow();
            r["RowType"] = "STAFF_TOTAL";
            r["IsPageBreak"] = "0";
            r["IsBold"] = "1";
            r["IsGrayBackground"] = "1";
            r["StaffCode"] = staffCode;
            r["StaffName"] = staffName;
            r["Col1"] = "※　合　 計　※";
            r["Col5"] = FormatQuantity(qty);
            r["Col7"] = FormatAmount(amt);
            dt.Rows.Add(r);
            AddBlank(dt, staffCode, staffName);
        }

        private byte[] GeneratePdfForDataTable(DataTable dataTable, DateTime jobDate, int startPage, int pageCount, int totalPages)
        {
            using var report = new FR.Report();
            report.Load(_templatePath);
            SetScriptLanguageToNone(report);

            // Data登録
            report.RegisterData(dataTable, "InventoryData");
            var ds = report.GetDataSource("InventoryData");
            if (ds != null) ds.Enabled = true;

            // 改ページトリガ
            var dataBand = report.FindObject("Data1") as FR.DataBand;
            if (dataBand != null)
            {
                var startNewPageProperty = dataBand.GetType().GetProperty("StartNewPageExpression");
                startNewPageProperty?.SetValue(dataBand, "[InventoryData.IsPageBreak] == \"1\"");
            }

            // ページ番号を全行に設定（商品勘定と同等のデータ列方式）
            int currentPage = startPage;
            int rowsInCurrentPage = 0;
            foreach (DataRow row in dataTable.Rows)
            {
                var rowType = row["RowType"]?.ToString() ?? string.Empty;
                var isBreak = (row["IsPageBreak"]?.ToString() == "1");

                // 行にページ番号を付与
                row["CurrentPage"] = currentPage.ToString();
                row["TotalPages"] = totalPages.ToString();

                // PAGE_BREAK行はカウントせず、ページを進める
                if (rowType == "PAGE_BREAK" || isBreak)
                {
                    if (rowsInCurrentPage > 0)
                    {
                        currentPage++;
                        rowsInCurrentPage = 0;
                    }
                    continue;
                }

                rowsInCurrentPage++;
                if (rowsInCurrentPage >= MaxRowsPerPage)
                {
                    currentPage++;
                    rowsInCurrentPage = 0;
                }
            }

            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy/MM/dd"));

            report.Prepare();
            using var pdf = new PDFExport();
            using var ms = new MemoryStream();
            report.Export(pdf, ms);
            return ms.ToArray();
        }

        private byte[] MergePdfFiles(List<byte[]> pdfs)
        {
            if (pdfs == null || pdfs.Count == 0) return Array.Empty<byte>();
            if (pdfs.Count == 1) return pdfs[0];

            using var output = new PdfDocument();
            foreach (var bytes in pdfs)
            {
                using var stream = new MemoryStream(bytes);
                using var input = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                foreach (var page in input.Pages) output.AddPage(page);
            }
            using var outStream = new MemoryStream();
            output.Save(outStream);
            return outStream.ToArray();
        }

        private void SetScriptLanguageToNone(Report report)
        {
            try
            {
                var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                if (scriptLanguageProperty != null)
                {
                    var enumValues = Enum.GetValues(scriptLanguageProperty.PropertyType);
                    var noneValue = enumValues.GetValue(0);
                    if (noneValue != null) scriptLanguageProperty.SetValue(report, noneValue);
                }
                var scriptProperty = report.GetType().GetProperty("Script", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scriptProperty != null && scriptProperty.CanWrite) scriptProperty.SetValue(report, null);
                var reportScriptProperty = report.GetType().GetProperty("ReportScript", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (reportScriptProperty != null && reportScriptProperty.CanWrite) reportScriptProperty.SetValue(report, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SetScriptLanguageToNone 警告");
            }
        }

        private string CalculateStagnationMark(DateTime? lastReceiptDate, DateTime jobDate)
        {
            if (!lastReceiptDate.HasValue) return string.Empty;
            var days = (jobDate.Date - lastReceiptDate.Value.Date).Days;
            if (days >= 31) return "!!!";
            if (days >= 21) return "!!";
            if (days >= 11) return "!";
            return string.Empty;
        }

        private string FormatQuantity(decimal value)
        {
            if (value == 0m) return string.Empty;
            if (value < 0m) return $"{Math.Abs(value):N2}▲";
            return value.ToString("N2");
        }
        private string FormatAmount(decimal value)
        {
            if (value == 0m) return string.Empty;
            if (value < 0m) return $"{Math.Abs(value):N0}▲";
            return value.ToString("N0");
        }
        private string FormatUnitPrice(decimal value)
        {
            if (value == 0m) return string.Empty;
            if (value < 0m) return $"{Math.Abs(value):N0}▲";
            return value.ToString("N0");
        }

        private class InventoryRow
        {
            public string StaffCode { get; set; } = string.Empty;
            public string StaffName { get; set; } = string.Empty;
            public string ProductCode { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public string ShippingMarkCode { get; set; } = string.Empty;
            public string ShippingMarkName { get; set; } = string.Empty;
            public string ManualShippingMark { get; set; } = string.Empty;
            public string GradeCode { get; set; } = string.Empty;
            public string GradeName { get; set; } = string.Empty;
            public string ClassCode { get; set; } = string.Empty;
            public string ClassName { get; set; } = string.Empty;
            public decimal DailyStock { get; set; }
            public decimal DailyUnitPrice { get; set; }
            public decimal DailyStockAmount { get; set; }
            public DateTime? LastReceiptDate { get; set; }
        }
    }
}
#else
using System;
using Microsoft.Extensions.Logging;
using InventorySystem.Reports.Interfaces;

namespace InventorySystem.Reports.FastReport.Services
{
    public class InventoryListFastReportService : IInventoryListReportService
    {
        private readonly ILogger<InventoryListFastReportService> _logger;
        public InventoryListFastReportService(ILogger<InventoryListFastReportService> logger)
        {
            _logger = logger;
        }

        public byte[] GenerateInventoryListReport(DateTime jobDate, string? departmentCode = null)
        {
            _logger.LogWarning("InventoryListFastReportService は Windows 専用です");
            throw new PlatformNotSupportedException("FastReport/PdfSharp は Windows 環境でのみ利用可能です");
        }
    }
}
#endif
