#pragma warning disable CA1416
#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FastReport;
using FastReport.Export.Pdf;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Interfaces;
using InventorySystem.Reports.Models;
using Microsoft.Extensions.Logging;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    /// <summary>
    /// 営業日報FastReportサービス - 完全パラメータ方式（スクリプトレス）
    /// 4ページ固定レイアウト（1ページ目:合計+分類01-08、2-4ページ目:分類09-17,18-26,27-35）
    /// </summary>
    public class BusinessDailyReportFastReportService : 
        InventorySystem.Reports.Interfaces.IBusinessDailyReportService, 
        InventorySystem.Core.Interfaces.IBusinessDailyReportReportService
    {
        private readonly ILogger<BusinessDailyReportFastReportService> _logger;
        private readonly IBusinessDailyReportRepository _repository;
        private readonly string _templatePath;

        public BusinessDailyReportFastReportService(
            ILogger<BusinessDailyReportFastReportService> logger,
            IBusinessDailyReportRepository repository)
        {
            _logger = logger;
            _repository = repository;
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _templatePath = Path.Combine(baseDirectory, "FastReport", "Templates", "BusinessDailyReport.frx");
            
            _logger.LogInformation("営業日報テンプレートパス: {Path}", _templatePath);
        }

        public async Task<byte[]> GenerateBusinessDailyReportAsync(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            try
            {
                _logger.LogInformation("営業日報PDF生成を開始します（Step1: 最小限実装）: JobDate={JobDate}", jobDate);

                if (!File.Exists(_templatePath))
                {
                    throw new FileNotFoundException($"営業日報テンプレートが見つかりません: {_templatePath}");
                }

                using var report = new FR.Report();
                report.Load(_templatePath);
                SetScriptLanguageToNone(report);

                // 基本パラメータ
                report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒"));
                report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("PageNumber", "１");  // 全角数字

                // テスト用の固定値を設定（日計の最初の3行のみ）
                // 行1：現金売上
                report.SetParameterValue("Daily_Row1_Total", "3,020,840");
                report.SetParameterValue("Daily_Row1_Col1", "2,364,100");  // 分類001
                report.SetParameterValue("Daily_Row1_Col2", "");           // 分類002（データなし）
                report.SetParameterValue("Daily_Row1_Col3", "630,490");    // 分類003
                report.SetParameterValue("Daily_Row1_Col4", "26,250");     // 分類004
                report.SetParameterValue("Daily_Row1_Col5", "");
                report.SetParameterValue("Daily_Row1_Col6", "");
                report.SetParameterValue("Daily_Row1_Col7", "");
                report.SetParameterValue("Daily_Row1_Col8", "");

                // 行2：現売消費税（10%として計算）
                report.SetParameterValue("Daily_Row2_Total", "302,084");
                report.SetParameterValue("Daily_Row2_Col1", "236,410");
                report.SetParameterValue("Daily_Row2_Col2", "");
                report.SetParameterValue("Daily_Row2_Col3", "63,049");
                report.SetParameterValue("Daily_Row2_Col4", "2,625");
                report.SetParameterValue("Daily_Row2_Col5", "");
                report.SetParameterValue("Daily_Row2_Col6", "");
                report.SetParameterValue("Daily_Row2_Col7", "");
                report.SetParameterValue("Daily_Row2_Col8", "");

                // 行3：掛売上と返品（テストのため0）
                report.SetParameterValue("Daily_Row3_Total", "");
                report.SetParameterValue("Daily_Row3_Col1", "");
                report.SetParameterValue("Daily_Row3_Col2", "");
                report.SetParameterValue("Daily_Row3_Col3", "");
                report.SetParameterValue("Daily_Row3_Col4", "");
                report.SetParameterValue("Daily_Row3_Col5", "");
                report.SetParameterValue("Daily_Row3_Col6", "");
                report.SetParameterValue("Daily_Row3_Col7", "");
                report.SetParameterValue("Daily_Row3_Col8", "");

                // 分類名設定（テスト用）
                report.SetParameterValue("CustomerName1", "イオン");
                report.SetParameterValue("CustomerName2", "");
                report.SetParameterValue("CustomerName3", "ヨーカドー");
                report.SetParameterValue("CustomerName4", "その他");
                report.SetParameterValue("CustomerName5", "");
                report.SetParameterValue("CustomerName6", "");
                report.SetParameterValue("CustomerName7", "");
                report.SetParameterValue("CustomerName8", "");

                report.SetParameterValue("SupplierName1", "三菱食品");
                report.SetParameterValue("SupplierName2", "");
                report.SetParameterValue("SupplierName3", "日本アクセス");
                report.SetParameterValue("SupplierName4", "その他");
                report.SetParameterValue("SupplierName5", "");
                report.SetParameterValue("SupplierName6", "");
                report.SetParameterValue("SupplierName7", "");
                report.SetParameterValue("SupplierName8", "");

                _logger.LogInformation("レポート準備中...");
                report.Prepare();

                return ExportToPdf(report, jobDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "営業日報PDF生成中にエラーが発生しました");
                throw;
            }
        }

        // 同期版（既存インターフェース対応）
        public byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            return GenerateBusinessDailyReportAsync(items, jobDate).GetAwaiter().GetResult();
        }

        public byte[] GenerateBusinessDailyReport(IEnumerable<object> businessDailyReportItems, DateTime jobDate)
        {
            var items = businessDailyReportItems.Cast<BusinessDailyReportItem>();
            return GenerateBusinessDailyReport(items, jobDate);
        }



        /// <summary>
        /// ScriptLanguageをNoneに設定
        /// </summary>
        private void SetScriptLanguageToNone(FR.Report report)
        {
            try
            {
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
                            _logger.LogDebug("ScriptLanguageをNoneに設定しました");
                        }
                    }
                }

                // Scriptプロパティもnullに設定
                var scriptProperty = report.GetType().GetProperty("Script", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (scriptProperty != null)
                {
                    scriptProperty.SetValue(report, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScriptLanguage設定時の警告");
            }
        }

        /// <summary>
        /// PDFエクスポート
        /// </summary>
        private byte[] ExportToPdf(FR.Report report, DateTime jobDate)
        {
            using var pdfExport = new PDFExport
            {
                EmbeddingFonts = true,
                Title = $"営業日報_{jobDate:yyyyMMdd}",
                Subject = "営業日報",
                Creator = "在庫管理システム",
                Author = "在庫管理システム",
                TextInCurves = false,
                JpegQuality = 95,
                OpenAfterExport = false
            };

            using var stream = new MemoryStream();
            report.Export(pdfExport, stream);

            var result = stream.ToArray();
            _logger.LogInformation("営業日報PDF生成完了: サイズ={Size}bytes", result.Length);

            return result;
        }
    }
}
#endif
#pragma warning restore CA1416