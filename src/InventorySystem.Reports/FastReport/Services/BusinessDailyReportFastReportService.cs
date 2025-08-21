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
using InventorySystem.Reports.Tools;
using Microsoft.Extensions.Logging;
using FR = global::FastReport;

namespace InventorySystem.Reports.FastReport.Services
{
    /// <summary>
    /// 営業日報FastReportサービス - 完全パラメータ方式（スクリプトレス）
    /// 4ページ固定レイアウト（A3横：Page1=合計+001-008、Page2-4=009-017、018-026、027-035）
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
                _logger.LogInformation("営業日報PDF生成を開始します（Step2: 完全実装）: JobDate={JobDate}", jobDate);

                if (!File.Exists(_templatePath))
                {
                    throw new FileNotFoundException($"営業日報テンプレートが見つかりません: {_templatePath}");
                }

                var dailyItems = items.ToList();
                
                // 月計・年計データの取得（Repository経由）
                var monthlyItems = await _repository.GetMonthlyDataAsync(jobDate);
                var yearlyItems = await _repository.GetYearlyDataAsync(jobDate);

                // 1) レイアウトパッチ適用（重なり解消）
                // FastReportPatcher.Patch(_templatePath); // 無効化: データ行X座標ズレ問題のため

                using var report = new FR.Report();
                report.Load(_templatePath);
                SetScriptLanguageToNone(report);

                // 基本パラメータ
                report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日HH時mm分ss秒"));
                report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));

                // 全ページの分類名設定（実データから取得）
                await SetAllPagesClassificationNamesAsync(report, dailyItems);

                // 4ページのデータパラメータ設定（日計・月計・年計）
                SetPage1DataParameters(report, dailyItems, monthlyItems.ToList(), yearlyItems.ToList());
                SetPage2DataParameters(report, dailyItems, monthlyItems.ToList(), yearlyItems.ToList());
                SetPage3DataParameters(report, dailyItems, monthlyItems.ToList(), yearlyItems.ToList());
                SetPage4DataParameters(report, dailyItems, monthlyItems.ToList(), yearlyItems.ToList());

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
        /// 全ページの分類名設定（実データから取得）
        /// </summary>
        private async Task SetAllPagesClassificationNamesAsync(FR.Report report, List<BusinessDailyReportItem> dailyItems)
        {
            // Page1: 分類001～008（既存処理、合計列+8分類）
            await SetClassificationNamesForPage1(report, dailyItems);
            
            // Page2: 分類009～017（9分類、合計列なし）
            await SetClassificationNamesForPage2(report, dailyItems);
            
            // Page3: 分類018～026（9分類、合計列なし）
            await SetClassificationNamesForPage3(report, dailyItems);
            
            // Page4: 分類027～035（9分類、合計列なし）
            await SetClassificationNamesForPage4(report, dailyItems);
        }

        /// <summary>
        /// Page1分類名設定（分類001～008）
        /// </summary>
        private async Task SetClassificationNamesForPage1(FR.Report report, List<BusinessDailyReportItem> dailyItems)
        {
            for (int i = 1; i <= 8; i++)
            {
                var code = i.ToString("000");
                var item = dailyItems.FirstOrDefault(x => x.ClassificationCode == code);
                
                var customerName = TruncateToLength(item?.CustomerClassName ?? "", 6);
                var supplierName = TruncateToLength(item?.SupplierClassName ?? "", 6);
                
                report.SetParameterValue($"CustomerName{i}", customerName);
                report.SetParameterValue($"SupplierName{i}", supplierName);
            }
        }

        /// <summary>
        /// Page2分類名設定（分類009～017、9分類）
        /// </summary>
        private async Task SetClassificationNamesForPage2(FR.Report report, List<BusinessDailyReportItem> dailyItems)
        {
            for (int i = 1; i <= 9; i++)
            {
                var code = (i + 8).ToString("000"); // 009～017
                var item = dailyItems.FirstOrDefault(x => x.ClassificationCode == code);
                
                var customerName = TruncateToLength(item?.CustomerClassName ?? "", 6);
                var supplierName = TruncateToLength(item?.SupplierClassName ?? "", 6);
                
                report.SetParameterValue($"Page2_CustomerName{i}", customerName);
                report.SetParameterValue($"Page2_SupplierName{i}", supplierName);
            }
        }

        /// <summary>
        /// Page3分類名設定（分類018～026、9分類）
        /// </summary>
        private async Task SetClassificationNamesForPage3(FR.Report report, List<BusinessDailyReportItem> dailyItems)
        {
            for (int i = 1; i <= 9; i++)
            {
                var code = (i + 17).ToString("000"); // 018～026
                var item = dailyItems.FirstOrDefault(x => x.ClassificationCode == code);
                
                var customerName = TruncateToLength(item?.CustomerClassName ?? "", 6);
                var supplierName = TruncateToLength(item?.SupplierClassName ?? "", 6);
                
                report.SetParameterValue($"Page3_CustomerName{i}", customerName);
                report.SetParameterValue($"Page3_SupplierName{i}", supplierName);
            }
        }

        /// <summary>
        /// Page4分類名設定（分類027～035、9分類）
        /// </summary>
        private async Task SetClassificationNamesForPage4(FR.Report report, List<BusinessDailyReportItem> dailyItems)
        {
            for (int i = 1; i <= 9; i++)
            {
                var code = (i + 26).ToString("000"); // 027～035
                var item = dailyItems.FirstOrDefault(x => x.ClassificationCode == code);
                
                var customerName = TruncateToLength(item?.CustomerClassName ?? "", 6);
                var supplierName = TruncateToLength(item?.SupplierClassName ?? "", 6);
                
                report.SetParameterValue($"Page4_CustomerName{i}", customerName);
                report.SetParameterValue($"Page4_SupplierName{i}", supplierName);
            }
        }


        /// <summary>
        /// 1ページ目のデータパラメータ設定（日計・月計・年計）
        /// </summary>
        private void SetPage1DataParameters(
            FR.Report report,
            List<BusinessDailyReportItem> dailyItems,
            List<BusinessDailyReportItem> monthlyItems,
            List<BusinessDailyReportItem> yearlyItems)
        {
            // 日計18行
            SetDailyData(report, dailyItems);
            
            // 月計18行
            SetMonthlyData(report, monthlyItems);
            
            // 年計4行
            SetYearlyData(report, yearlyItems);
        }

        /// <summary>
        /// 日計データ設定（18行）
        /// </summary>
        private void SetDailyData(FR.Report report, List<BusinessDailyReportItem> items)
        {
            // 分類001〜008のデータを取得（分類コードで正しく紐付け）
            var dataByClass = new Dictionary<int, BusinessDailyReportItem>();
            const int maxColumns = 8; // Page1は8列固定
            for (int i = 1; i <= 8; i++)
            {
                var code = i.ToString("000");
                dataByClass[i] = items.FirstOrDefault(x => x.ClassificationCode == code);
            }

            // 合計を計算
            var total = CalculateTotal(items);

            // 行1：現金売上
            report.SetParameterValue("Daily_Row1_Total", FormatNumber(total.DailyCashSales));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashSales;
                report.SetParameterValue($"Daily_Row1_Col{col}", FormatNumber(value));
            }

            // 行2：現売消費税
            report.SetParameterValue("Daily_Row2_Total", FormatNumber(total.DailyCashSalesTax));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashSalesTax;
                report.SetParameterValue($"Daily_Row2_Col{col}", FormatNumber(value));
            }

            // 行3：掛売上と返品
            report.SetParameterValue("Daily_Row3_Total", FormatNumber(total.DailyCreditSales));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCreditSales;
                report.SetParameterValue($"Daily_Row3_Col{col}", FormatNumber(value));
            }

            // 行4：売上値引
            report.SetParameterValue("Daily_Row4_Total", FormatNumber(total.DailySalesDiscount));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailySalesDiscount;
                report.SetParameterValue($"Daily_Row4_Col{col}", FormatNumber(value));
            }

            // 行5：掛売消費税
            report.SetParameterValue("Daily_Row5_Total", FormatNumber(total.DailyCreditSalesTax));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCreditSalesTax;
                report.SetParameterValue($"Daily_Row5_Col{col}", FormatNumber(value));
            }

            // 行6：＊売上計＊（合計行）
            var salesTotal = (total.DailyCashSales ?? 0m) + (total.DailyCashSalesTax ?? 0m) +
                             (total.DailyCreditSales ?? 0m) + (total.DailySalesDiscount ?? 0m) +
                             (total.DailyCreditSalesTax ?? 0m);
            report.SetParameterValue("Daily_Row6_Total", FormatNumber(salesTotal));
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.DailyCashSales ?? 0m) + (item.DailyCashSalesTax ?? 0m) +
                                   (item.DailyCreditSales ?? 0m) + (item.DailySalesDiscount ?? 0m) +
                                   (item.DailyCreditSalesTax ?? 0m);
                    report.SetParameterValue($"Daily_Row6_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"Daily_Row6_Col{col}", "");
                }
            }

            // 行7：現金仕入
            report.SetParameterValue("Daily_Row7_Total", FormatNumber(total.DailyCashPurchase));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashPurchase;
                report.SetParameterValue($"Daily_Row7_Col{col}", FormatNumber(value));
            }

            // 行8：現仕消費税
            report.SetParameterValue("Daily_Row8_Total", FormatNumber(total.DailyCashPurchaseTax));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashPurchaseTax;
                report.SetParameterValue($"Daily_Row8_Col{col}", FormatNumber(value));
            }

            // 行9：掛仕入と返品
            report.SetParameterValue("Daily_Row9_Total", FormatNumber(total.DailyCreditPurchase));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCreditPurchase;
                report.SetParameterValue($"Daily_Row9_Col{col}", FormatNumber(value));
            }

            // 行10：仕入値引
            report.SetParameterValue("Daily_Row10_Total", FormatNumber(total.DailyPurchaseDiscount));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyPurchaseDiscount;
                report.SetParameterValue($"Daily_Row10_Col{col}", FormatNumber(value));
            }

            // 行11：掛仕入消費税
            report.SetParameterValue("Daily_Row11_Total", FormatNumber(total.DailyCreditPurchaseTax));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCreditPurchaseTax;
                report.SetParameterValue($"Daily_Row11_Col{col}", FormatNumber(value));
            }

            // 行12：＊仕入計＊
            var purchaseTotal = (total.DailyCashPurchase ?? 0m) + (total.DailyCashPurchaseTax ?? 0m) +
                                (total.DailyCreditPurchase ?? 0m) + (total.DailyPurchaseDiscount ?? 0m) +
                                (total.DailyCreditPurchaseTax ?? 0m);
            report.SetParameterValue("Daily_Row12_Total", FormatNumber(purchaseTotal));
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.DailyCashPurchase ?? 0m) + (item.DailyCashPurchaseTax ?? 0m) +
                                   (item.DailyCreditPurchase ?? 0m) + (item.DailyPurchaseDiscount ?? 0m) +
                                   (item.DailyCreditPurchaseTax ?? 0m);
                    report.SetParameterValue($"Daily_Row12_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"Daily_Row12_Col{col}", "");
                }
            }

            // 行13：入金と現売
            report.SetParameterValue("Daily_Row13_Total", FormatNumber(total.DailyCashReceipt));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashReceipt;
                report.SetParameterValue($"Daily_Row13_Col{col}", FormatNumber(value));
            }

            // 行14：入金値引・他
            report.SetParameterValue("Daily_Row14_Total", FormatNumber(total.DailyOtherReceipt));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyOtherReceipt;
                report.SetParameterValue($"Daily_Row14_Col{col}", FormatNumber(value));
            }

            // 行15：＊入金計＊
            var receiptTotal = (total.DailyCashReceipt ?? 0m) + (total.DailyOtherReceipt ?? 0m);
            report.SetParameterValue("Daily_Row15_Total", FormatNumber(receiptTotal));
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.DailyCashReceipt ?? 0m) + (item.DailyOtherReceipt ?? 0m);
                    report.SetParameterValue($"Daily_Row15_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"Daily_Row15_Col{col}", "");
                }
            }

            // 行16：支払と現金支払
            report.SetParameterValue("Daily_Row16_Total", FormatNumber(total.DailyCashPayment));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashPayment;
                report.SetParameterValue($"Daily_Row16_Col{col}", FormatNumber(value));
            }

            // 行17：支払値引・他
            report.SetParameterValue("Daily_Row17_Total", FormatNumber(total.DailyOtherPayment));
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyOtherPayment;
                report.SetParameterValue($"Daily_Row17_Col{col}", FormatNumber(value));
            }

            // 行18：＊支払計＊
            var paymentTotal = (total.DailyCashPayment ?? 0m) + (total.DailyOtherPayment ?? 0m);
            report.SetParameterValue("Daily_Row18_Total", FormatNumber(paymentTotal));
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.DailyCashPayment ?? 0m) + (item.DailyOtherPayment ?? 0m);
                    report.SetParameterValue($"Daily_Row18_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"Daily_Row18_Col{col}", "");
                }
            }
        }

        /// <summary>
        /// 月計データ設定（18行）
        /// </summary>
        private void SetMonthlyData(FR.Report report, List<BusinessDailyReportItem> items)
        {
            // 分類001〜008のデータを取得
            var dataByClass = new Dictionary<int, BusinessDailyReportItem>();
            const int maxColumns = 8; // Page1は8列固定
            for (int i = 1; i <= 8; i++)
            {
                var code = i.ToString("000");
                dataByClass[i] = items.FirstOrDefault(x => x.ClassificationCode == code);
            }

            // 合計を計算
            var total = CalculateTotal(items);

            // 月計は日計と同じ18行構成で設定
            // 行1〜18まで同様の処理（Monthly_Row1〜Monthly_Row18）
            for (int row = 1; row <= 18; row++)
            {
                // 簡略化のため、月計は0として設定（実装時に詳細化）
                report.SetParameterValue($"Monthly_Row{row}_Total", "");
                for (int col = 1; col <= maxColumns; col++)
                {
                    report.SetParameterValue($"Monthly_Row{row}_Col{col}", "");
                }
            }
        }

        /// <summary>
        /// 年計データ設定（4行のみ）
        /// </summary>
        private void SetYearlyData(FR.Report report, List<BusinessDailyReportItem> items)
        {
            // 年計は4項目のみ
            // 分類001〜008のデータを取得
            var dataByClass = new Dictionary<int, BusinessDailyReportItem>();
            const int maxColumns = 8; // Page1は8列固定
            for (int i = 1; i <= 8; i++)
            {
                var code = i.ToString("000");
                dataByClass[i] = items.FirstOrDefault(x => x.ClassificationCode == code);
            }

            // 年計4行を設定（Yearly_Row1〜Yearly_Row4）
            for (int row = 1; row <= 4; row++)
            {
                // 簡略化のため、年計は0として設定（実装時に詳細化）
                report.SetParameterValue($"Yearly_Row{row}_Total", "");
                for (int col = 1; col <= maxColumns; col++)
                {
                    report.SetParameterValue($"Yearly_Row{row}_Col{col}", "");
                }
            }
        }

        /// <summary>
        /// 合計計算
        /// </summary>
        private BusinessDailyReportItem CalculateTotal(List<BusinessDailyReportItem> items)
        {
            return new BusinessDailyReportItem
            {
                DailyCashSales = items.Sum(x => x.DailyCashSales ?? 0),
                DailyCashSalesTax = items.Sum(x => x.DailyCashSalesTax ?? 0),
                DailyCreditSales = items.Sum(x => x.DailyCreditSales ?? 0),
                DailySalesDiscount = items.Sum(x => x.DailySalesDiscount ?? 0),
                DailyCreditSalesTax = items.Sum(x => x.DailyCreditSalesTax ?? 0),
                DailyCashPurchase = items.Sum(x => x.DailyCashPurchase ?? 0),
                DailyCashPurchaseTax = items.Sum(x => x.DailyCashPurchaseTax ?? 0),
                DailyCreditPurchase = items.Sum(x => x.DailyCreditPurchase ?? 0),
                DailyPurchaseDiscount = items.Sum(x => x.DailyPurchaseDiscount ?? 0),
                DailyCreditPurchaseTax = items.Sum(x => x.DailyCreditPurchaseTax ?? 0),
                DailyCashReceipt = items.Sum(x => x.DailyCashReceipt ?? 0),
                DailyBankReceipt = items.Sum(x => x.DailyBankReceipt ?? 0),
                DailyOtherReceipt = items.Sum(x => x.DailyOtherReceipt ?? 0),
                DailyCashPayment = items.Sum(x => x.DailyCashPayment ?? 0),
                DailyBankPayment = items.Sum(x => x.DailyBankPayment ?? 0),
                DailyOtherPayment = items.Sum(x => x.DailyOtherPayment ?? 0)
            };
        }

        /// <summary>
        /// Page2のデータパラメータ設定（分類009～017、9分類）
        /// </summary>
        private void SetPage2DataParameters(
            FR.Report report,
            List<BusinessDailyReportItem> dailyItems,
            List<BusinessDailyReportItem> monthlyItems,
            List<BusinessDailyReportItem> yearlyItems)
        {
            SetPageDataParameters(report, dailyItems, monthlyItems, yearlyItems, 2, 9, 17);
        }

        /// <summary>
        /// Page3のデータパラメータ設定（分類018～026、9分類）
        /// </summary>
        private void SetPage3DataParameters(
            FR.Report report,
            List<BusinessDailyReportItem> dailyItems,
            List<BusinessDailyReportItem> monthlyItems,
            List<BusinessDailyReportItem> yearlyItems)
        {
            SetPageDataParameters(report, dailyItems, monthlyItems, yearlyItems, 3, 18, 26);
        }

        /// <summary>
        /// Page4のデータパラメータ設定（分類027～035、9分類）
        /// </summary>
        private void SetPage4DataParameters(
            FR.Report report,
            List<BusinessDailyReportItem> dailyItems,
            List<BusinessDailyReportItem> monthlyItems,
            List<BusinessDailyReportItem> yearlyItems)
        {
            SetPageDataParameters(report, dailyItems, monthlyItems, yearlyItems, 4, 27, 35);
        }

        /// <summary>
        /// 指定ページのデータパラメータ設定（共通処理）
        /// </summary>
        private void SetPageDataParameters(
            FR.Report report,
            List<BusinessDailyReportItem> dailyItems,
            List<BusinessDailyReportItem> monthlyItems,
            List<BusinessDailyReportItem> yearlyItems,
            int pageNumber,
            int startClassCode,
            int endClassCode)
        {
            var pagePrefix = pageNumber == 1 ? "" : $"Page{pageNumber}_";
            var maxColumns = pageNumber == 1 ? 8 : 9; // Page1は8列、Page2-4は9列

            // 対象分類のデータを取得
            var dataByClass = new Dictionary<int, BusinessDailyReportItem>();
            for (int i = 1; i <= maxColumns; i++)
            {
                var classCode = startClassCode + i - 1;
                if (classCode <= endClassCode)
                {
                    var code = classCode.ToString("000");
                    dataByClass[i] = dailyItems.FirstOrDefault(x => x.ClassificationCode == code);
                }
                else
                {
                    dataByClass[i] = null; // 範囲外は空
                }
            }

            // Page1のみ全35分類の合計を表示、Page2-4は合計列なし
            var totalAll = pageNumber == 1 ? CalculateTotal(dailyItems) : null;

            // 日計18行設定
            SetPageDailyData(report, dataByClass, totalAll, pagePrefix, maxColumns);
            
            // 月計18行設定
            SetPageMonthlyData(report, monthlyItems, pagePrefix, startClassCode, endClassCode, maxColumns);
            
            // 年計4行設定
            SetPageYearlyData(report, yearlyItems, pagePrefix, startClassCode, endClassCode, maxColumns);
        }

        /// <summary>
        /// 指定ページの日計データ設定（18行）
        /// </summary>
        private void SetPageDailyData(FR.Report report, Dictionary<int, BusinessDailyReportItem> dataByClass, BusinessDailyReportItem totalAll, string pagePrefix, int maxColumns)
        {
            // 行1：現金売上
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row1_Total", FormatNumber(totalAll.DailyCashSales));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashSales;
                report.SetParameterValue($"{pagePrefix}Daily_Row1_Col{col}", FormatNumber(value));
            }

            // 行2：現売消費税
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row2_Total", FormatNumber(totalAll.DailyCashSalesTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashSalesTax;
                report.SetParameterValue($"{pagePrefix}Daily_Row2_Col{col}", FormatNumber(value));
            }

            // 行3：掛売上と返品
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row3_Total", FormatNumber(totalAll.DailyCreditSales));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCreditSales;
                report.SetParameterValue($"{pagePrefix}Daily_Row3_Col{col}", FormatNumber(value));
            }

            // 行4：売上値引
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row4_Total", FormatNumber(totalAll.DailySalesDiscount));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailySalesDiscount;
                report.SetParameterValue($"{pagePrefix}Daily_Row4_Col{col}", FormatNumber(value));
            }

            // 行5：掛売消費税
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row5_Total", FormatNumber(totalAll.DailyCreditSalesTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCreditSalesTax;
                report.SetParameterValue($"{pagePrefix}Daily_Row5_Col{col}", FormatNumber(value));
            }

            // 行6：＊売上計＊（合計行）
            if (totalAll != null) // Page1のみ合計列あり
            {
                var salesTotalAll = (totalAll.DailyCashSales ?? 0m) + (totalAll.DailyCashSalesTax ?? 0m) +
                                    (totalAll.DailyCreditSales ?? 0m) + (totalAll.DailySalesDiscount ?? 0m) +
                                    (totalAll.DailyCreditSalesTax ?? 0m);
                report.SetParameterValue($"{pagePrefix}Daily_Row6_Total", FormatNumber(salesTotalAll));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.DailyCashSales ?? 0m) + (item.DailyCashSalesTax ?? 0m) +
                                   (item.DailyCreditSales ?? 0m) + (item.DailySalesDiscount ?? 0m) +
                                   (item.DailyCreditSalesTax ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Daily_Row6_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Daily_Row6_Col{col}", "");
                }
            }

            // 行7：現金仕入
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row7_Total", FormatNumber(totalAll.DailyCashPurchase));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashPurchase;
                report.SetParameterValue($"{pagePrefix}Daily_Row7_Col{col}", FormatNumber(value));
            }

            // 行8：現仕消費税
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row8_Total", FormatNumber(totalAll.DailyCashPurchaseTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashPurchaseTax;
                report.SetParameterValue($"{pagePrefix}Daily_Row8_Col{col}", FormatNumber(value));
            }

            // 行9：掛仕入と返品
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row9_Total", FormatNumber(totalAll.DailyCreditPurchase));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCreditPurchase;
                report.SetParameterValue($"{pagePrefix}Daily_Row9_Col{col}", FormatNumber(value));
            }

            // 行10：仕入値引
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row10_Total", FormatNumber(totalAll.DailyPurchaseDiscount));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyPurchaseDiscount;
                report.SetParameterValue($"{pagePrefix}Daily_Row10_Col{col}", FormatNumber(value));
            }

            // 行11：掛仕入消費税
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row11_Total", FormatNumber(totalAll.DailyCreditPurchaseTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCreditPurchaseTax;
                report.SetParameterValue($"{pagePrefix}Daily_Row11_Col{col}", FormatNumber(value));
            }

            // 行12：＊仕入計＊
            if (totalAll != null) // Page1のみ合計列あり
            {
                var purchaseTotalAll = (totalAll.DailyCashPurchase ?? 0m) + (totalAll.DailyCashPurchaseTax ?? 0m) +
                                       (totalAll.DailyCreditPurchase ?? 0m) + (totalAll.DailyPurchaseDiscount ?? 0m) +
                                       (totalAll.DailyCreditPurchaseTax ?? 0m);
                report.SetParameterValue($"{pagePrefix}Daily_Row12_Total", FormatNumber(purchaseTotalAll));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.DailyCashPurchase ?? 0m) + (item.DailyCashPurchaseTax ?? 0m) +
                                   (item.DailyCreditPurchase ?? 0m) + (item.DailyPurchaseDiscount ?? 0m) +
                                   (item.DailyCreditPurchaseTax ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Daily_Row12_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Daily_Row12_Col{col}", "");
                }
            }

            // 行13：入金と現売
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row13_Total", FormatNumber(totalAll.DailyCashReceipt));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashReceipt;
                report.SetParameterValue($"{pagePrefix}Daily_Row13_Col{col}", FormatNumber(value));
            }

            // 行14：入金値引・他
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row14_Total", FormatNumber(totalAll.DailyOtherReceipt));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyOtherReceipt;
                report.SetParameterValue($"{pagePrefix}Daily_Row14_Col{col}", FormatNumber(value));
            }

            // 行15：＊入金計＊
            if (totalAll != null) // Page1のみ合計列あり
            {
                var receiptTotalAll = (totalAll.DailyCashReceipt ?? 0m) + (totalAll.DailyOtherReceipt ?? 0m);
                report.SetParameterValue($"{pagePrefix}Daily_Row15_Total", FormatNumber(receiptTotalAll));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.DailyCashReceipt ?? 0m) + (item.DailyOtherReceipt ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Daily_Row15_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Daily_Row15_Col{col}", "");
                }
            }

            // 行16：支払と現金支払
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row16_Total", FormatNumber(totalAll.DailyCashPayment));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyCashPayment;
                report.SetParameterValue($"{pagePrefix}Daily_Row16_Col{col}", FormatNumber(value));
            }

            // 行17：支払値引・他
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Daily_Row17_Total", FormatNumber(totalAll.DailyOtherPayment));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.DailyOtherPayment;
                report.SetParameterValue($"{pagePrefix}Daily_Row17_Col{col}", FormatNumber(value));
            }

            // 行18：＊支払計＊
            if (totalAll != null) // Page1のみ合計列あり
            {
                var paymentTotalAll = (totalAll.DailyCashPayment ?? 0m) + (totalAll.DailyOtherPayment ?? 0m);
                report.SetParameterValue($"{pagePrefix}Daily_Row18_Total", FormatNumber(paymentTotalAll));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.DailyCashPayment ?? 0m) + (item.DailyOtherPayment ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Daily_Row18_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Daily_Row18_Col{col}", "");
                }
            }
        }

        /// <summary>
        /// 指定ページの月計データ設定（18行）
        /// </summary>
        private void SetPageMonthlyData(FR.Report report, List<BusinessDailyReportItem> monthlyItems, string pagePrefix, int startClassCode, int endClassCode, int maxColumns)
        {
            // 月計は現在未実装（空文字設定）
            for (int row = 1; row <= 18; row++)
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row{row}_Total", "");
                for (int col = 1; col <= maxColumns; col++)
                {
                    report.SetParameterValue($"{pagePrefix}Monthly_Row{row}_Col{col}", "");
                }
            }
        }

        /// <summary>
        /// 指定ページの年計データ設定（4行）
        /// </summary>
        private void SetPageYearlyData(FR.Report report, List<BusinessDailyReportItem> yearlyItems, string pagePrefix, int startClassCode, int endClassCode, int maxColumns)
        {
            // 年計は現在未実装（空文字設定）
            for (int row = 1; row <= 4; row++)
            {
                report.SetParameterValue($"{pagePrefix}Yearly_Row{row}_Total", "");
                for (int col = 1; col <= maxColumns; col++)
                {
                    report.SetParameterValue($"{pagePrefix}Yearly_Row{row}_Col{col}", "");
                }
            }
        }

        /// <summary>
        /// 数値フォーマット（ゼロ→空文字、マイナス→▲記号、カンマ区切り）
        /// </summary>
        private string FormatNumber(decimal? value)
        {
            if (value == null || value == 0) return "";

            // マイナス値は▲記号
            if (value < 0)
                return "▲" + Math.Abs(value.Value).ToString("#,##0");

            return value.Value.ToString("#,##0");
        }

        /// <summary>
        /// 文字列を指定長に切り詰め
        /// </summary>
        private string TruncateToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
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