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
using FR = global::FastReport;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Reports.FastReport.Services
{
    /// <summary>
    /// 在庫表FastReport実装（ProductAccountFastReportServiceと同じ7段階プロセス）
    /// </summary>
    public class InventoryListFastReportService
    {
        private readonly ILogger<InventoryListFastReportService> _logger;
        private readonly ICpInventoryRepository _cpInventoryRepository;
        private readonly string _templatePath;

        public InventoryListFastReportService(
            ILogger<InventoryListFastReportService> logger, 
            ICpInventoryRepository cpInventoryRepository)
        {
            _logger = logger;
            _cpInventoryRepository = cpInventoryRepository;
            
            _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                "FastReport", "Templates", "InventoryList.frx");
                
            _logger.LogInformation("在庫表FastReportテンプレートパス: {TemplatePath}", _templatePath);
        }

        /// <summary>
        /// 在庫表の7段階プロセス実装（ProductAccountと同じ構造）
        /// </summary>
        public async Task<byte[]> GenerateInventoryListAsync(DateTime jobDate, string? dataSetId = null)
        {
            string tempFolder = null;
            try
            {
                _logger.LogInformation("=== 在庫表FastReport 7段階プロセス開始 ===");
                _logger.LogInformation("Job date: {JobDate:yyyy-MM-dd}", jobDate);
                
                // Phase 1: データ準備
                _logger.LogInformation("Phase 1: データ準備開始");
                var cpInventoryData = await PrepareInventoryData(jobDate);
                
                // Phase 2: フラットデータ生成
                _logger.LogInformation("Phase 2: 担当者別フラットデータ生成");
                var flatData = GenerateFlatData(cpInventoryData);
                
                // 担当者コード空を"000"に変換（ProductAccountと同じ）
                foreach (var item in flatData)
                {
                    if (string.IsNullOrEmpty(item.StaffCode))
                    {
                        item.StaffCode = "000";
                        item.StaffName = "担当者未設定";
                    }
                }
                
                if (!flatData.Any())
                {
                    throw new InvalidOperationException("在庫表のデータが存在しません");
                }
                
                // Phase 3: 1次PDF生成（仮ページ番号）
                _logger.LogInformation("Phase 3: 1次PDF生成（仮ページ番号）");
                tempFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Output",
                    $"Temp_{DateTime.Now:yyyyMMddHHmmss}");
                Directory.CreateDirectory(tempFolder);
                
                var tempPdfFiles = new List<string>();
                var tempPdfFile = Path.Combine(tempFolder, "temp_inventory.pdf");
                await GenerateSinglePdfAsync(flatData, 1, flatData.Count, 999, tempPdfFile, jobDate);
                tempPdfFiles.Add(tempPdfFile);
                
                // Phase 4: ページ数解析
                _logger.LogInformation("Phase 4: ページ数解析");
                var totalPages = GetPdfPageCount(tempPdfFile);
                _logger.LogInformation("総ページ数: {TotalPages}", totalPages);
                
                // Phase 5: 2次PDF生成（正確なページ番号）
                _logger.LogInformation("Phase 5: 2次PDF生成（正確なページ番号）");
                var finalPdfFile = Path.Combine(tempFolder, "final_inventory.pdf");
                await GenerateSinglePdfAsync(flatData, 1, flatData.Count, totalPages, finalPdfFile, jobDate);
                
                // Phase 6: 最終PDF読み込み
                _logger.LogInformation("Phase 6: 最終PDF読み込み");
                var finalPdfBytes = await File.ReadAllBytesAsync(finalPdfFile);
                
                _logger.LogInformation("在庫表FastReport PDF生成完了: ファイルサイズ={FileSize}bytes", finalPdfBytes.Length);
                return finalPdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "在庫表FastReport PDF生成エラー");
                throw;
            }
            finally
            {
                // Phase 7: クリーンアップ
                if (!string.IsNullOrEmpty(tempFolder) && Directory.Exists(tempFolder))
                {
                    try
                    {
                        Directory.Delete(tempFolder, true);
                        _logger.LogInformation("一時フォルダ削除完了");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "一時フォルダの削除に失敗");
                    }
                }
            }
        }

        /// <summary>
        /// CP在庫マスタからデータ取得・フィルタリング
        /// </summary>
        private async Task<List<CpInventoryMaster>> PrepareInventoryData(DateTime jobDate)
        {
            _logger.LogInformation("CP在庫マスタからデータ取得中...");
            var cpInventories = await _cpInventoryRepository.GetAllAsync();
            
            _logger.LogInformation("取得件数: {Count}件", cpInventories.Count());
            
            // フィルタリング条件
            var filtered = cpInventories
                .Where(cp => !(cp.DailyStock == 0 && cp.DailyStockAmount == 0))  // 当日在庫数量・金額が共に0を除外
                .Where(cp => cp.PreviousDayStock != 0)  // 前日在庫数が0を除外
                .ToList();
            
            _logger.LogInformation("フィルタ後件数: {FilteredCount}件", filtered.Count);
            
            // ソート：担当者→商品→荷印→等級→階級
            var sorted = filtered
                .OrderBy(cp => cp.ProductCategory1 ?? "000")
                .ThenBy(cp => cp.Key.ProductCode)
                .ThenBy(cp => cp.Key.ShippingMarkCode)
                .ThenBy(cp => cp.Key.ManualShippingMark)
                .ThenBy(cp => cp.Key.GradeCode)
                .ThenBy(cp => cp.Key.ClassCode)
                .ToList();
            
            return sorted;
        }

        /// <summary>
        /// フラットデータ生成（ProductAccountと同じ構造）
        /// </summary>
        private List<InventoryFlatRow> GenerateFlatData(List<CpInventoryMaster> sourceData)
        {
            var flatRows = new List<InventoryFlatRow>();
            
            // 担当者でグループ化
            var staffGroups = sourceData
                .GroupBy(x => x.ProductCategory1 ?? "000")
                .OrderBy(g => g.Key);
            
            foreach (var staffGroup in staffGroups)
            {
                // STAFF_HEADER行
                flatRows.Add(new InventoryFlatRow
                {
                    RowType = RowTypes.StaffHeader,
                    IsPageBreak = "1",
                    StaffCode = staffGroup.Key,
                    StaffName = $"担当者{staffGroup.Key}"
                });
                
                // 商品でグループ化
                var productGroups = staffGroup
                    .GroupBy(x => x.Key.ProductCode)
                    .OrderBy(g => g.Key);
                
                decimal staffTotalQuantity = 0;
                decimal staffTotalAmount = 0;
                
                foreach (var productGroup in productGroups)
                {
                    decimal productSubtotalQuantity = 0;
                    decimal productSubtotalAmount = 0;
                    
                    // 明細行
                    foreach (var item in productGroup)
                    {
                        flatRows.Add(CreateDetailRow(item));
                        productSubtotalQuantity += item.DailyStock;
                        productSubtotalAmount += item.DailyStockAmount;
                    }
                    
                    // 商品小計
                    flatRows.Add(CreateProductSubtotalRow(productSubtotalQuantity, productSubtotalAmount));
                    
                    staffTotalQuantity += productSubtotalQuantity;
                    staffTotalAmount += productSubtotalAmount;
                }
                
                // 担当者合計
                flatRows.Add(CreateStaffTotalRow(staffTotalQuantity, staffTotalAmount));
                
                // 空行（担当者間）
                flatRows.Add(new InventoryFlatRow
                {
                    RowType = RowTypes.Blank
                });
            }
            
            // 35行改ページ制御（ProductAccountと同じ実装）
            return ApplyPageBreakControl(flatRows);
        }

        /// <summary>
        /// 明細行作成
        /// </summary>
        private InventoryFlatRow CreateDetailRow(CpInventoryMaster item)
        {
            return new InventoryFlatRow
            {
                RowType = RowTypes.Detail,
                Col1 = item.ProductName ?? "",
                Col2 = item.Key.ManualShippingMark ?? item.Key.ShippingMarkCode,
                Col3 = item.GradeName ?? item.Key.GradeCode,
                Col4 = item.ClassName ?? item.Key.ClassCode,
                Col5 = FormatQuantity(item.DailyStock),
                Col6 = FormatUnitPrice(item.DailyUnitPrice),
                Col7 = FormatAmount(item.DailyStockAmount),
                Col8 = "", // 最終入荷日（初期実装では空）
                Col9 = ""  // 滞留マーク（初期実装では空）
            };
        }

        /// <summary>
        /// 商品小計行作成
        /// </summary>
        private InventoryFlatRow CreateProductSubtotalRow(decimal quantity, decimal amount)
        {
            return new InventoryFlatRow
            {
                RowType = RowTypes.ProductSubtotal,
                IsBold = "1",
                Col1 = "＊　小　　計　＊",
                Col5 = FormatQuantity(quantity),
                Col7 = FormatAmount(amount)
            };
        }

        /// <summary>
        /// 担当者合計行作成
        /// </summary>
        private InventoryFlatRow CreateStaffTotalRow(decimal quantity, decimal amount)
        {
            return new InventoryFlatRow
            {
                RowType = RowTypes.StaffTotal,
                IsBold = "1",
                Col1 = "※　合　　計　※",
                Col5 = FormatQuantity(quantity),
                Col7 = FormatAmount(amount)
            };
        }

        /// <summary>
        /// 35行改ページ制御
        /// </summary>
        private List<InventoryFlatRow> ApplyPageBreakControl(List<InventoryFlatRow> flatRows)
        {
            var result = new List<InventoryFlatRow>();
            int rowCount = 0;
            
            foreach (var row in flatRows)
            {
                if (row.RowType == RowTypes.StaffHeader)
                {
                    // 担当者ヘッダーは必ず改ページ
                    row.IsPageBreak = "1";
                    rowCount = 1;
                }
                else if (rowCount >= 35)
                {
                    // 35行に達した場合、改ページ制御行を挿入
                    result.Add(new InventoryFlatRow
                    {
                        RowType = RowTypes.PageBreak,
                        IsPageBreak = "1"
                    });
                    rowCount = 1;
                }
                else
                {
                    rowCount++;
                }
                
                result.Add(row);
            }
            
            return result;
        }

        /// <summary>
        /// フォーマット：数量
        /// </summary>
        private string FormatQuantity(decimal quantity)
        {
            if (quantity == 0) return "";
            var formatted = quantity.ToString("#,##0.00").TrimEnd('0').TrimEnd('.');
            return quantity < 0 ? formatted + "-" : formatted;
        }

        /// <summary>
        /// フォーマット：単価
        /// </summary>
        private string FormatUnitPrice(decimal price)
        {
            if (price == 0) return "";
            return price.ToString("#,##0");
        }

        /// <summary>
        /// フォーマット：金額
        /// </summary>
        private string FormatAmount(decimal amount)
        {
            if (amount == 0) return "";
            var formatted = amount.ToString("#,##0");
            return amount < 0 ? formatted + "-" : formatted;
        }

        /// <summary>
        /// 単一PDF生成（ProductAccount実証済みパターン適用）
        /// </summary>
        private async Task GenerateSinglePdfAsync(
            List<InventoryFlatRow> flatData,
            int startPage,
            int pageCount,
            int totalPages,
            string outputPath,
            DateTime jobDate)
        {
            if (!File.Exists(_templatePath))
            {
                throw new FileNotFoundException($"FastReportテンプレートファイルが見つかりません: {_templatePath}");
            }

            using var report = new FR.Report();
            
            // テンプレート選択（環境変数 USE_MINIMAL_TEMPLATE=true で最小テンプレートを使用）
            var useMinimalTemplate = string.Equals(Environment.GetEnvironmentVariable("USE_MINIMAL_TEMPLATE"), "true", StringComparison.OrdinalIgnoreCase);
            var templatePath = GetTemplatePath(useMinimalTemplate);

            // テンプレート読み込み → スクリプト無効化（ProductAccountと同じ順序）
            report.ReportResourceString = "";
            report.FileName = templatePath;
            _logger.LogInformation("テンプレート読込: {Template}", templatePath);
            report.Load(templatePath);
            SetScriptLanguageToNone(report);
            
            // フラットデータをDataTableに変換
            var dataTable = CreateFlatDataTableWithPageNumbers(flatData, startPage, pageCount, totalPages);
            
            // デバッグログ
            _logger.LogInformation("DataTable行数: {Count}", dataTable.Rows.Count);
            
            // FastReportにデータソースを登録
            report.RegisterData(dataTable, "InventoryData");
            
            // ⭐重要: データソースを有効化（ProductAccountと同じ）
            var registeredDataSource = report.GetDataSource("InventoryData");
            if (registeredDataSource != null)
            {
                registeredDataSource.Enabled = true;
                try
                {
                    // FR側データソースを初期化してRowCountを有効化
                    registeredDataSource.Init();
                }
                catch { /* ignore */ }
                _logger.LogInformation("データソース 'InventoryData' を有効化しました");
                try
                {
                    _logger.LogInformation("データソース行数: {RowCount}", registeredDataSource.RowCount);
                }
                catch { /* ignore */ }

                // DataBandに明示的にデータソースを割り当て（テンプレートの参照ズレ対策）
                try
                {
                    var page = report.Pages.Count > 0 ? report.Pages[0] as FR.ReportPage : null;
                    if (page != null)
                    {
                        foreach (var obj in page.AllObjects)
                        {
                            if (obj is FR.DataBand band)
                            {
                                if (string.Equals(band.Name, "Data1", StringComparison.OrdinalIgnoreCase))
                                {
                                    band.DataSource = registeredDataSource;
                                    _logger.LogInformation("DataBand '{BandName}' にデータソース '{DSName}' を割当", band.Name, registeredDataSource.Name);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DataBandへのDataSource割当時の警告");
                }
            }
            else
            {
                _logger.LogWarning("データソース 'InventoryData' が見つかりません");
            }

            // 追加診断: テンプレート内のDataBandとデータソースの関連をログ出力
            try
            {
                var page = report.Pages.Count > 0 ? report.Pages[0] as FR.ReportPage : null;
                if (page != null)
                {
                    _logger.LogInformation("ページ '{PageName}' には {Count} 個のオブジェクトがあります", page.Name, page.AllObjects.Count);
                    foreach (var obj in page.AllObjects)
                    {
                        if (obj is FR.DataBand band)
                        {
                            var ds = band.DataSource;
                            _logger.LogInformation("DataBand '{BandName}' のDataSource: {DS}", band.Name, ds?.Name ?? "(null)");
                        }
                    }
                }

                // 辞書に登録されているデータソース一覧
                var list = report.Dictionary?.DataSources?.Cast<object>()?.ToList();
                if (list != null)
                {
                    _logger.LogInformation("辞書データソース数: {Count}", list.Count);
                    foreach (var dsObj in list)
                    {
                        try
                        {
                            var nameProp = dsObj.GetType().GetProperty("Name");
                            var name = nameProp?.GetValue(dsObj)?.ToString() ?? "(unknown)";
                            _logger.LogInformation("  - DataSource: {Name}", name);
                        }
                        catch { /* ignore */ }
                    }
                }
            }
            catch { /* ignore */ }
            
            // パラメータ設定
            _logger.LogInformation("レポートパラメータを設定しています...");
            report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分ss秒"));
            report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));

            // ⭐重要: TotalCountパラメータを追加
            // 明細行（DETAIL）のみをカウントする方法
            int detailCount = dataTable.AsEnumerable()
                .Count(row => (row.Field<string>("RowType") ?? "") == "DETAIL");
            report.SetParameterValue("TotalCount", detailCount.ToString());

            _logger.LogInformation("TotalCountパラメータ設定: {Count}件", detailCount);

            // レポート準備（エラーハンドリング追加）
            _logger.LogInformation("レポートを準備中...");
            try
            {
                report.Prepare();
                var preparedCount = 0;
                try
                {
                    preparedCount = report.PreparedPages?.Count ?? -1;
                }
                catch { /* ignore */ }
                _logger.LogInformation("レポート準備完了 (PreparedPages={PreparedPages})", preparedCount);
                if (preparedCount == 0)
                {
                    _logger.LogWarning("PreparedPagesが0です。テンプレートまたはデータバインドに問題がある可能性があります。");
                    try
                    {
                        // DataBandの詳細診断
                        var dataBand = report.FindObject("Data1") as FR.DataBand;
                        if (dataBand == null)
                        {
                            _logger.LogError("DataBand 'Data1' が見つかりません");
                        }
                        else
                        {
                            _logger.LogError("=== DataBand診断 ===");
                            _logger.LogError("DataBand.Name: {Name}", dataBand.Name);
                            _logger.LogError("DataBand.Height: {Height}", dataBand.Height);
                            _logger.LogError("DataBand.Visible: {Visible}", dataBand.Visible);
                            _logger.LogError("DataBand.CanGrow: {CanGrow}", dataBand.CanGrow);
                            _logger.LogError("DataBand.DataSource: {DS}", dataBand.DataSource?.Name ?? "(null)");
                            if (dataBand.DataSource != null)
                            {
                                _logger.LogError("DataSource.Enabled: {Enabled}", dataBand.DataSource.Enabled);
                                _logger.LogError("DataSource.RowCount: {RowCount}", dataBand.DataSource.RowCount);
                            }
                        }

                        // 全オブジェクト一覧（ページ0のみ）
                        var page = report.Pages.Count > 0 ? report.Pages[0] as FR.ReportPage : null;
                        if (page != null)
                        {
                            _logger.LogError("=== ReportPageオブジェクト一覧 ({Count}件) ===", page.AllObjects.Count);
                            foreach (var obj in page.AllObjects)
                            {
                                var type = obj?.GetType();
                                var nameValue = "(unknown)";
                                try
                                {
                                    var nameProp = type?.GetProperty("Name");
                                    if (nameProp != null)
                                    {
                                        var val = nameProp.GetValue(obj);
                                        nameValue = val?.ToString() ?? "(null)";
                                    }
                                }
                                catch { /* ignore diagnostics errors */ }
                                _logger.LogError(" - {Type}: {Name}", type?.Name ?? "(null)", nameValue);
                            }
                        }
                    }
                    catch (Exception diagEx)
                    {
                        _logger.LogWarning(diagEx, "PreparedPages=0時の診断で例外が発生しました");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "report.Prepare()でエラーが発生しました");
                throw new InvalidOperationException("レポートの準備に失敗しました", ex);
            }
            
            // ⭐重要: PDFExport設定（ProductAccountから完全コピー）
            using var pdfExport = new FR.Export.Pdf.PDFExport
            {
                EmbeddingFonts = true,              // 日本語フォント埋め込み（必須）
                Title = $"在庫表_{jobDate:yyyyMMdd}",
                Subject = "在庫表",
                Creator = "在庫管理システム",
                Author = "InventoryManagementSystem",
                TextInCurves = false,
                JpegQuality = 95,
                OpenAfterExport = false
            };
            
            // ⭐重要: MemoryStream経由でPDF生成
            using var memoryStream = new MemoryStream();
            report.Export(pdfExport, memoryStream);
            
            // MemoryStreamからファイルに書き出し
            var length = memoryStream.Length;
            if (length == 0)
            {
                _logger.LogWarning("PDF書き出し後のMemoryStream.Lengthが0です。Export処理が正常に行われていない可能性があります。");
            }
            var pdfBytes = memoryStream.ToArray();
            if (pdfBytes.Length == 0)
            {
                _logger.LogWarning("ToArray()の結果も0バイトです。テンプレートのデータソース名/バンド設定を確認してください。");
            }
            await File.WriteAllBytesAsync(outputPath, pdfBytes);
            
            _logger.LogInformation("PDF生成完了: {FilePath}, サイズ: {Size}bytes", outputPath, pdfBytes.Length);
        }

        private string GetTemplatePath(bool useMinimal = false)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var templateName = useMinimal ? "InventoryList_minimal.frx" : "InventoryList.frx";
            return Path.Combine(baseDir, "FastReport", "Templates", templateName);
        }

        /// <summary>
        /// フラットデータをDataTableに変換（ページ番号付き）
        /// </summary>
        private DataTable CreateFlatDataTableWithPageNumbers(
            List<InventoryFlatRow> flatData,
            int startPage,
            int pageCount,
            int totalPages)
        {
            var dt = new DataTable("InventoryData");
            
            // カラム定義（全てstring型）
            dt.Columns.Add("RowType", typeof(string));
            dt.Columns.Add("IsPageBreak", typeof(string));
            dt.Columns.Add("IsBold", typeof(string));
            dt.Columns.Add("IsGrayBackground", typeof(string));
            dt.Columns.Add("StaffCode", typeof(string));
            dt.Columns.Add("StaffName", typeof(string));
            dt.Columns.Add("Col1", typeof(string));
            dt.Columns.Add("Col2", typeof(string));
            dt.Columns.Add("Col3", typeof(string));
            dt.Columns.Add("Col4", typeof(string));
            dt.Columns.Add("Col5", typeof(string));
            dt.Columns.Add("Col6", typeof(string));
            dt.Columns.Add("Col7", typeof(string));
            dt.Columns.Add("Col8", typeof(string));
            dt.Columns.Add("Col9", typeof(string));
            dt.Columns.Add("CurrentPage", typeof(string));
            dt.Columns.Add("TotalPages", typeof(string));
            
            // ページ番号計算と行追加
            int currentPage = startPage;
            int rowCount = 0;
            
            foreach (var row in flatData)
            {
                // ページ番号設定
                row.CurrentPage = currentPage.ToString();
                row.TotalPages = totalPages.ToString();
                
                // DataTable行追加
                dt.Rows.Add(
                    row.RowType,
                    row.IsPageBreak,
                    row.IsBold,
                    row.IsGrayBackground,
                    row.StaffCode,
                    row.StaffName,
                    row.Col1,
                    row.Col2,
                    row.Col3,
                    row.Col4,
                    row.Col5,
                    row.Col6,
                    row.Col7,
                    row.Col8,
                    row.Col9,
                    row.CurrentPage,
                    row.TotalPages
                );
                
                // 35行改ページ制御
                if (row.IsPageBreak == "1")
                {
                    currentPage++;
                    rowCount = 0;
                }
                else if (++rowCount >= 35)
                {
                    currentPage++;
                    rowCount = 0;
                }
            }
            
            return dt;
        }

        /// <summary>
        /// FastReportのスクリプトを完全に無効化（ProductAccountから流用）
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
                
                // ReportResourceStringをクリア
                report.ReportResourceString = "";
                
                // Scriptプロパティをクリア（リフレクションで非公開メンバーアクセス）
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
            }
        }

        /// <summary>
        /// PDFページ数取得（ProductAccountから流用）
        /// </summary>
        private int GetPdfPageCount(string pdfFilePath)
        {
            try
            {
                using var document = PdfReader.Open(pdfFilePath, PdfDocumentOpenMode.ReadOnly);
                return document.PageCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDFページ数取得エラー: {FilePath}", pdfFilePath);
                return 1;
            }
        }
    }

    /// <summary>
    /// 在庫表フラットデータ行（ProductAccountFlatRowと同じ構造）
    /// </summary>
    public class InventoryFlatRow
    {
        // 行制御
        public string RowType { get; set; } = "";
        public string IsPageBreak { get; set; } = "0";
        public string IsBold { get; set; } = "0";
        public string IsGrayBackground { get; set; } = "0";
        
        // 担当者情報
        public string StaffCode { get; set; } = "";
        public string StaffName { get; set; } = "";
        
        // 表示データ（全て文字列）
        public string Col1 { get; set; } = "";  // 商品名
        public string Col2 { get; set; } = "";  // 荷印
        public string Col3 { get; set; } = "";  // 等級
        public string Col4 { get; set; } = "";  // 階級
        public string Col5 { get; set; } = "";  // 在庫数量
        public string Col6 { get; set; } = "";  // 在庫単価
        public string Col7 { get; set; } = "";  // 在庫金額
        public string Col8 { get; set; } = "";  // 最終入荷日
        public string Col9 { get; set; } = "";  // マーク
        
        // ページ番号
        public string CurrentPage { get; set; } = "";
        public string TotalPages { get; set; } = "";
    }

    /// <summary>
    /// 行種別定数（ProductAccountと同じ）
    /// </summary>
    public static class RowTypes
    {
        public const string StaffHeader = "STAFF_HEADER";
        public const string Detail = "DETAIL";
        public const string ProductSubtotal = "PRODUCT_SUBTOTAL";
        public const string StaffTotal = "STAFF_TOTAL";
        public const string Blank = "BLANK";
        public const string PageBreak = "PAGE_BREAK";
        public const string Dummy = "DUMMY";
    }
}
#endif
