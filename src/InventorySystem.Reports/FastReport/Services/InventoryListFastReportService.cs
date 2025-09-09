#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Entities;
using FR = FastReport;
using FastReport.Export.Pdf;

namespace InventorySystem.Reports.FastReport.Services
{
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

        // メインメソッド: GenerateInventoryListAsync
        public async Task<byte[]> GenerateInventoryListAsync(DateTime jobDate, string? dataSetId = null)
        {
            _logger.LogInformation("=== 在庫表作成開始（新実装/Phase1） ===");
            _logger.LogInformation("対象日: {JobDate}", jobDate.ToString("yyyy-MM-dd"));

            var tempFolder = CreateTempFolder();

            try
            {
                // Phase 1: データ準備
                var inventoryData = await PrepareInventoryData(jobDate, dataSetId);
                _logger.LogInformation("Phase 1完了: {Count}件取得", inventoryData.Count);

                // Phase 2: 担当者別フラットデータ生成
                var staffDataDict = GenerateFlatDataByStaff(inventoryData);
                _logger.LogInformation("Phase 2完了: {StaffCount}名の担当者", staffDataDict.Count);

                // Phase 3: 担当者別1次PDF生成（仮ページ番号999）
                var firstPassPdfs = await GenerateFirstPassPdfs(staffDataDict, tempFolder, jobDate);
                _logger.LogInformation("Phase 3完了: {PdfCount}個のPDF生成", firstPassPdfs.Count);

                // Phase 1では一時的にダミーのbyte配列を返す
                _logger.LogInformation("=== Phase 1実装完了（仮PDF生成まで） ===");
                return new byte[] { 0x00 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "在庫表生成エラー");
                DeleteTempFolder(tempFolder);
                throw;
            }
        }

        // Phase 1: データ準備
        private async Task<List<CpInventoryMaster>> PrepareInventoryData(DateTime jobDate, string? dataSetId)
        {
            _logger.LogInformation("Phase 1: データ準備開始");

            // CP在庫マスタから読み取り（更新は一切しない）
            var all = await _cpInventoryRepository.GetAllAsync();

            // JobDateで対象日を絞る（テーブル構成により同日1セット想定）
            var query = all.Where(x => x.JobDate == jobDate);

            // DataSetIdはテーブル仕様により存在しない場合があるため、使用しない（仕様合意: DataSetId不使用）

            // フィルタリング（当日在庫が0の明細は除外）
            var filtered = query.Where(x => !(x.DailyStock == 0 && x.DailyStockAmount == 0)).ToList();
            _logger.LogInformation("フィルタ後件数: {Count}件", filtered.Count);

            // ソート（担当者→商品→荷印→手入力→等級→階級）
            var sorted = filtered
                .OrderBy(x => x.ProductCategory1 ?? "000")
                .ThenBy(x => x.Key.ProductCode)
                .ThenBy(x => x.Key.ShippingMarkCode)
                .ThenBy(x => x.Key.ManualShippingMark)
                .ThenBy(x => x.Key.GradeCode)
                .ThenBy(x => x.Key.ClassCode)
                .ToList();

            return sorted;
        }

        // Phase 2: 担当者別フラットデータ生成
        private Dictionary<string, List<InventoryFlatRow>> GenerateFlatDataByStaff(List<CpInventoryMaster> sourceData)
        {
            _logger.LogInformation("Phase 2: 担当者別フラットデータ生成");

            var result = new Dictionary<string, List<InventoryFlatRow>>();

            // 担当者別にグループ化
            var staffGroups = sourceData.GroupBy(x => x.ProductCategory1 ?? "000");

            foreach (var staffGroup in staffGroups.OrderBy(g => g.Key))
            {
                var staffCode = string.IsNullOrEmpty(staffGroup.Key) ? "000" : staffGroup.Key;
                var staffName = staffCode == "000" ? "担当者未設定" : $"担当者{staffCode}";
                var staffData = new List<InventoryFlatRow>();

                // 担当者ヘッダー
                staffData.Add(new InventoryFlatRow
                {
                    RowType = RowTypes.StaffHeader,
                    IsPageBreak = "1",
                    IsBold = "1",
                    StaffCode = staffCode,
                    StaffName = staffName,
                    Col1 = $"担当者: {staffCode} - {staffName}"
                });

                // 商品別処理
                var productGroups = staffGroup.GroupBy(x => x.Key.ProductCode);

                foreach (var productGroup in productGroups)
                {
                    // 商品明細
                    foreach (var item in productGroup)
                    {
                        staffData.Add(CreateDetailRow(item));
                    }

                    // 商品小計
                    var subtotal = CalculateProductSubtotal(productGroup);
                    staffData.Add(subtotal);
                }

                // 担当者合計
                var total = CalculateStaffTotal(staffGroup);
                staffData.Add(total);

                // ページ区切り制御（35行ごと）
                staffData = ApplyPageBreakControl(staffData);

                result[staffCode] = staffData;
                _logger.LogInformation("担当者 {Staff}: {Count}行", staffCode, staffData.Count);
            }

            return result;
        }

        private InventoryFlatRow CreateDetailRow(CpInventoryMaster item)
        {
            // 滞留マーク判定（LastReceiptDateの有無はPhase 2以降で確定）
            var retentionMark = ""; // Phase1では空

            return new InventoryFlatRow
            {
                RowType = RowTypes.Detail,
                Col1 = $"{item.Key.ProductCode} {item.ProductName}",
                Col2 = item.Key.ManualShippingMark ?? item.Key.ShippingMarkCode ?? string.Empty,
                Col3 = item.GradeName ?? item.Key.GradeCode ?? string.Empty,
                Col4 = item.ClassName ?? item.Key.ClassCode ?? string.Empty,
                Col5 = FormatQuantity(item.PreviousDayStock),
                Col6 = FormatQuantity(item.DailyStock),
                Col7 = FormatAmount(item.DailyStockAmount),
                Col8 = "", // 最終入荷日（Phase2以降で設定）
                Col9 = retentionMark
            };
        }

        private InventoryFlatRow CalculateProductSubtotal(IEnumerable<CpInventoryMaster> productGroup)
        {
            var q = productGroup.Sum(x => x.DailyStock);
            var a = productGroup.Sum(x => x.DailyStockAmount);
            return new InventoryFlatRow
            {
                RowType = RowTypes.ProductSubtotal,
                IsBold = "1",
                Col1 = "＊　小　　計　＊",
                Col5 = FormatQuantity(q),
                Col7 = FormatAmount(a)
            };
        }

        private InventoryFlatRow CalculateStaffTotal(IEnumerable<CpInventoryMaster> staffGroup)
        {
            var q = staffGroup.Sum(x => x.DailyStock);
            var a = staffGroup.Sum(x => x.DailyStockAmount);
            return new InventoryFlatRow
            {
                RowType = RowTypes.StaffTotal,
                IsBold = "1",
                Col1 = "※　合　　計　※",
                Col5 = FormatQuantity(q),
                Col7 = FormatAmount(a)
            };
        }

        private List<InventoryFlatRow> ApplyPageBreakControl(List<InventoryFlatRow> rows)
        {
            var result = new List<InventoryFlatRow>();
            int count = 0;
            foreach (var row in rows)
            {
                if (row.RowType == RowTypes.StaffHeader)
                {
                    row.IsPageBreak = "1";
                    count = 1;
                }
                else if (count >= 35)
                {
                    result.Add(new InventoryFlatRow { RowType = RowTypes.PageBreak, IsPageBreak = "1" });
                    count = 1;
                }
                else
                {
                    count++;
                }

                result.Add(row);
            }
            return result;
        }

        // Phase 3: 担当者別1次PDF生成
        private async Task<Dictionary<string, string>> GenerateFirstPassPdfs(
            Dictionary<string, List<InventoryFlatRow>> staffDataDict,
            string tempFolder,
            DateTime jobDate)
        {
            _logger.LogInformation("Phase 3: 担当者別1次PDF生成（仮ページ番号999）");

            var pdfPaths = new Dictionary<string, string>();

            foreach (var kvp in staffDataDict.OrderBy(x => x.Key))
            {
                var staffCode = kvp.Key;
                var staffData = kvp.Value;

                var pdfFileName = $"temp_staff_{staffCode}.pdf";
                var pdfPath = Path.Combine(tempFolder, pdfFileName);
                _logger.LogInformation("担当者 {Staff} の1次PDF生成: {File}", staffCode, pdfFileName);

                // DataTable作成（仮ページ番号999）
                var dataTable = CreateDataTable(staffData, 999);

                // PDF生成（DataBand直接アクセス方式）
                var pdfBytes = await GenerateSinglePdf(dataTable, jobDate);

                // ファイル保存
                await File.WriteAllBytesAsync(pdfPath, pdfBytes);
                _logger.LogInformation("PDF生成完了: {Path}, サイズ: {Size}bytes", pdfPath, pdfBytes.Length);

                if (pdfBytes.Length == 0)
                {
                    throw new InvalidOperationException($"PDFが0バイトです: {pdfFileName}");
                }

                pdfPaths[staffCode] = pdfPath;
            }

            return pdfPaths;
        }

        private async Task<byte[]> GenerateSinglePdf(DataTable dataTable, DateTime jobDate)
        {
            using (var report = new FR.Report())
            {
                // テンプレート読み込み
                var templatePath = GetTemplatePath();
                _logger.LogInformation("テンプレート読込: {Template}", templatePath);
                report.Load(templatePath);

                // .NET 8対策: レポートスクリプトを完全無効化（CodeDom回避）
                SetScriptLanguageToNone(report);

                // データ登録
                report.RegisterData(dataTable, "InventoryData");
                var dataSource = report.GetDataSource("InventoryData");
                if (dataSource != null)
                {
                    dataSource.Enabled = true;
                    dataSource.Init();
                }

                // DataBandへの直接割当
                AssignDataSourceToDataBand(report);

                // パラメータ設定（存在しない場合は無視）
                try { report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日")); } catch { }

                // レポート準備
                report.Prepare();

                // PDF出力
                using (var pdfExport = new PDFExport())
                using (var stream = new MemoryStream())
                {
                    report.Export(pdfExport, stream);
                    return stream.ToArray();
                }
            }
        }

        // FastReportのスクリプトを完全に無効化（商品勘定/アンマッチと同等の対策）
        private void SetScriptLanguageToNone(FR.Report report)
        {
            try
            {
                var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
                if (scriptLanguageProperty != null)
                {
                    var enumType = scriptLanguageProperty.PropertyType;
                    if (enumType.IsEnum)
                    {
                        var noneValue = Enum.GetValues(enumType).Cast<object>()
                            .FirstOrDefault(v => v.ToString() == "None");
                        if (noneValue != null)
                        {
                            scriptLanguageProperty.SetValue(report, noneValue);
                            _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }

                // ReportResourceStringをクリア
                report.ReportResourceString = "";

                // 内部Scriptプロパティをnullにクリア（反射）
                var scriptProperty = report.GetType().GetProperty(
                    "Script",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                    _logger.LogInformation("Scriptプロパティをnullに設定しました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Script無効化処理中の警告");
            }
        }

        private void AssignDataSourceToDataBand(FR.Report report)
        {
            // 最初のページのDataBand("Data1")にデータソースを割当
            if (report.Pages.Count > 0 && report.Pages[0] is FR.ReportPage reportPage)
            {
                foreach (FR.BandBase band in reportPage.Bands)
                {
                    if (band is FR.DataBand dataBand && dataBand.Name == "Data1")
                    {
                        var dataSource = report.GetDataSource("InventoryData");
                        if (dataSource != null)
                        {
                            dataBand.DataSource = dataSource;
                            _logger.LogInformation("DataSource '{DS}' を DataBand '{Band}' に割当", dataSource.Name, dataBand.Name);
                        }
                        break;
                    }
                }
            }
        }

        private DataTable CreateDataTable(List<InventoryFlatRow> rows, int totalPages)
        {
            var table = new DataTable("InventoryData");

            // 列定義（全て文字列）
            table.Columns.Add("RowType", typeof(string));
            table.Columns.Add("IsPageBreak", typeof(string));
            table.Columns.Add("IsBold", typeof(string));
            table.Columns.Add("IsGrayBackground", typeof(string));
            table.Columns.Add("StaffCode", typeof(string));
            table.Columns.Add("StaffName", typeof(string));
            for (int i = 1; i <= 9; i++)
            {
                table.Columns.Add($"Col{i}", typeof(string));
            }
            table.Columns.Add("CurrentPage", typeof(string));
            table.Columns.Add("TotalPages", typeof(string));

            // 行投入
            foreach (var r in rows)
            {
                var row = table.NewRow();
                row["RowType"] = r.RowType;
                row["IsPageBreak"] = r.IsPageBreak;
                row["IsBold"] = r.IsBold;
                row["IsGrayBackground"] = r.IsGrayBackground;
                row["StaffCode"] = r.StaffCode ?? string.Empty;
                row["StaffName"] = r.StaffName ?? string.Empty;
                row["Col1"] = r.Col1 ?? string.Empty;
                row["Col2"] = r.Col2 ?? string.Empty;
                row["Col3"] = r.Col3 ?? string.Empty;
                row["Col4"] = r.Col4 ?? string.Empty;
                row["Col5"] = r.Col5 ?? string.Empty;
                row["Col6"] = r.Col6 ?? string.Empty;
                row["Col7"] = r.Col7 ?? string.Empty;
                row["Col8"] = r.Col8 ?? string.Empty;
                row["Col9"] = r.Col9 ?? string.Empty;
                row["CurrentPage"] = "1"; // 1次PDFでは固定
                row["TotalPages"] = totalPages.ToString(); // 仮: 999
                table.Rows.Add(row);
            }

            return table;
        }

        private string CreateTempFolder()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempFolder = Path.Combine(baseDir, "Output", $"Temp_{timestamp}");
            Directory.CreateDirectory(tempFolder);
            _logger.LogInformation("一時フォルダ作成: {Folder}", tempFolder);
            return tempFolder;
        }

        private void DeleteTempFolder(string? tempFolder)
        {
            if (string.IsNullOrWhiteSpace(tempFolder)) return;
            try
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                    _logger.LogInformation("一時フォルダ削除: {Folder}", tempFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "一時フォルダ削除エラー（続行）");
            }
        }

        private string GetTemplatePath()
        {
            var baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "FastReport", "Templates", "InventoryList.frx");
        }

        private string FormatQuantity(decimal value)
        {
            if (value == 0) return string.Empty;
            var s = value.ToString("#,##0.00").TrimEnd('0').TrimEnd('.');
            return value < 0 ? s + "-" : s;
        }

        private string FormatAmount(decimal value)
        {
            if (value == 0) return string.Empty;
            var s = value.ToString("#,##0");
            return value < 0 ? s + "-" : s;
        }
    }

    // フラット行（Phase 1用の最小構成）
    public class InventoryFlatRow
    {
        public string RowType { get; set; } = string.Empty;
        public string IsPageBreak { get; set; } = "0";
        public string IsBold { get; set; } = "0";
        public string IsGrayBackground { get; set; } = "0";
        public string StaffCode { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Col1 { get; set; } = string.Empty;
        public string Col2 { get; set; } = string.Empty;
        public string Col3 { get; set; } = string.Empty;
        public string Col4 { get; set; } = string.Empty;
        public string Col5 { get; set; } = string.Empty;
        public string Col6 { get; set; } = string.Empty;
        public string Col7 { get; set; } = string.Empty;
        public string Col8 { get; set; } = string.Empty;
        public string Col9 { get; set; } = string.Empty;
        public string CurrentPage { get; set; } = string.Empty;
        public string TotalPages { get; set; } = string.Empty;
    }

    public static class RowTypes
    {
        public const string StaffHeader = "STAFF_HEADER";
        public const string Detail = "DETAIL";
        public const string ProductSubtotal = "PRODUCT_SUBTOTAL";
        public const string StaffTotal = "STAFF_TOTAL";
        public const string PageBreak = "PAGE_BREAK";
    }
}
#endif
