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
                
                // 分類名設定の前にデータ確認
                _logger.LogInformation("===== 営業日報生成開始 =====");
                _logger.LogInformation($"処理日付: {jobDate:yyyy-MM-dd}");
                _logger.LogInformation($"日計データ件数: {dailyItems?.Count ?? 0}");

                
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

                _logger.LogInformation("分類名設定完了");

                // テンプレート検証（デバッグ用）
                ValidateReportTemplate(report);

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
                
                var customerParam = $"Page2_CustomerName{i}";
                var supplierParam = $"Page2_SupplierName{i}";
                
                report.SetParameterValue(customerParam, customerName);
                report.SetParameterValue(supplierParam, supplierName);
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
                
                var customerParam = $"Page3_CustomerName{i}";
                var supplierParam = $"Page3_SupplierName{i}";
                
                report.SetParameterValue(customerParam, customerName);
                report.SetParameterValue(supplierParam, supplierName);
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
            SetPageMonthlyData(report, monthlyItems, "", 1, 8, 8);
            
            // 年計4行
            SetPageYearlyData(report, yearlyItems, "", 1, 8, 8);
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
                    report.SetParameterValue($"Daily_Row6_Col{col}", "0");
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
                    report.SetParameterValue($"Daily_Row12_Col{col}", "0");
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
                    report.SetParameterValue($"Daily_Row15_Col{col}", "0");
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
                    report.SetParameterValue($"Daily_Row18_Col{col}", "0");
                }
            }
        }



        /// <summary>
        /// 合計計算（分類000があればそれを使用、なければ001-035を合計）
        /// </summary>
        private BusinessDailyReportItem CalculateTotal(List<BusinessDailyReportItem> items)
        {
            // 分類000があればそれを返す（既に合計値が設定済み）
            var total000 = items.FirstOrDefault(x => x.ClassificationCode == "000");
            if (total000 != null)
            {
                _logger.LogDebug("分類000を使用して合計計算: DailyCreditSales={Value}", total000.DailyCreditSales ?? 0);
                return total000;
            }
            
            // 分類000がない場合は001-035を合計（000を除外）
            var itemsWithoutTotal = items.Where(x => x.ClassificationCode != "000").ToList();
            _logger.LogDebug("分類000なし、{Count}件から合計計算", itemsWithoutTotal.Count);
            
            var calculatedTotal = new BusinessDailyReportItem
            {
                DailyCashSales = itemsWithoutTotal.Sum(x => x.DailyCashSales ?? 0),
                DailyCashSalesTax = itemsWithoutTotal.Sum(x => x.DailyCashSalesTax ?? 0),
                DailyCreditSales = itemsWithoutTotal.Sum(x => x.DailyCreditSales ?? 0),
                DailySalesDiscount = itemsWithoutTotal.Sum(x => x.DailySalesDiscount ?? 0),
                DailyCreditSalesTax = itemsWithoutTotal.Sum(x => x.DailyCreditSalesTax ?? 0),
                DailyCashPurchase = itemsWithoutTotal.Sum(x => x.DailyCashPurchase ?? 0),
                DailyCashPurchaseTax = itemsWithoutTotal.Sum(x => x.DailyCashPurchaseTax ?? 0),
                DailyCreditPurchase = itemsWithoutTotal.Sum(x => x.DailyCreditPurchase ?? 0),
                DailyPurchaseDiscount = itemsWithoutTotal.Sum(x => x.DailyPurchaseDiscount ?? 0),
                DailyCreditPurchaseTax = itemsWithoutTotal.Sum(x => x.DailyCreditPurchaseTax ?? 0),
                DailyCashReceipt = itemsWithoutTotal.Sum(x => x.DailyCashReceipt ?? 0),
                DailyBankReceipt = itemsWithoutTotal.Sum(x => x.DailyBankReceipt ?? 0),
                DailyOtherReceipt = itemsWithoutTotal.Sum(x => x.DailyOtherReceipt ?? 0),
                DailyCashPayment = itemsWithoutTotal.Sum(x => x.DailyCashPayment ?? 0),
                DailyBankPayment = itemsWithoutTotal.Sum(x => x.DailyBankPayment ?? 0),
                DailyOtherPayment = itemsWithoutTotal.Sum(x => x.DailyOtherPayment ?? 0),
                // 月計項目
                MonthlyCashSales = itemsWithoutTotal.Sum(x => x.MonthlyCashSales ?? 0),
                MonthlyCashSalesTax = itemsWithoutTotal.Sum(x => x.MonthlyCashSalesTax ?? 0),
                MonthlyCreditSales = itemsWithoutTotal.Sum(x => x.MonthlyCreditSales ?? 0),
                MonthlySalesDiscount = itemsWithoutTotal.Sum(x => x.MonthlySalesDiscount ?? 0),
                MonthlyCreditSalesTax = itemsWithoutTotal.Sum(x => x.MonthlyCreditSalesTax ?? 0),
                MonthlyCashPurchase = itemsWithoutTotal.Sum(x => x.MonthlyCashPurchase ?? 0),
                MonthlyCashPurchaseTax = itemsWithoutTotal.Sum(x => x.MonthlyCashPurchaseTax ?? 0),
                MonthlyCreditPurchase = itemsWithoutTotal.Sum(x => x.MonthlyCreditPurchase ?? 0),
                MonthlyPurchaseDiscount = itemsWithoutTotal.Sum(x => x.MonthlyPurchaseDiscount ?? 0),
                MonthlyCreditPurchaseTax = itemsWithoutTotal.Sum(x => x.MonthlyCreditPurchaseTax ?? 0),
                MonthlyCashReceipt = itemsWithoutTotal.Sum(x => x.MonthlyCashReceipt ?? 0),
                MonthlyBankReceipt = itemsWithoutTotal.Sum(x => x.MonthlyBankReceipt ?? 0),
                MonthlyOtherReceipt = itemsWithoutTotal.Sum(x => x.MonthlyOtherReceipt ?? 0),
                MonthlyCashPayment = itemsWithoutTotal.Sum(x => x.MonthlyCashPayment ?? 0),
                MonthlyBankPayment = itemsWithoutTotal.Sum(x => x.MonthlyBankPayment ?? 0),
                MonthlyOtherPayment = itemsWithoutTotal.Sum(x => x.MonthlyOtherPayment ?? 0),
                // 年計項目
                YearlyCashSales = itemsWithoutTotal.Sum(x => x.YearlyCashSales ?? 0),
                YearlyCashSalesTax = itemsWithoutTotal.Sum(x => x.YearlyCashSalesTax ?? 0),
                YearlyCreditSales = itemsWithoutTotal.Sum(x => x.YearlyCreditSales ?? 0),
                YearlySalesDiscount = itemsWithoutTotal.Sum(x => x.YearlySalesDiscount ?? 0),
                YearlyCreditSalesTax = itemsWithoutTotal.Sum(x => x.YearlyCreditSalesTax ?? 0),
                YearlyCashPurchase = itemsWithoutTotal.Sum(x => x.YearlyCashPurchase ?? 0),
                YearlyCashPurchaseTax = itemsWithoutTotal.Sum(x => x.YearlyCashPurchaseTax ?? 0),
                YearlyCreditPurchase = itemsWithoutTotal.Sum(x => x.YearlyCreditPurchase ?? 0),
                YearlyPurchaseDiscount = itemsWithoutTotal.Sum(x => x.YearlyPurchaseDiscount ?? 0),
                YearlyCreditPurchaseTax = itemsWithoutTotal.Sum(x => x.YearlyCreditPurchaseTax ?? 0),
                YearlyCashReceipt = itemsWithoutTotal.Sum(x => x.YearlyCashReceipt ?? 0),
                YearlyBankReceipt = itemsWithoutTotal.Sum(x => x.YearlyBankReceipt ?? 0),
                YearlyOtherReceipt = itemsWithoutTotal.Sum(x => x.YearlyOtherReceipt ?? 0),
                YearlyCashPayment = itemsWithoutTotal.Sum(x => x.YearlyCashPayment ?? 0),
                YearlyBankPayment = itemsWithoutTotal.Sum(x => x.YearlyBankPayment ?? 0),
                YearlyOtherPayment = itemsWithoutTotal.Sum(x => x.YearlyOtherPayment ?? 0)
            };
            
            _logger.LogDebug("計算済み合計: DailyCreditSales={Value}", calculatedTotal.DailyCreditSales ?? 0);
            return calculatedTotal;
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
                    report.SetParameterValue($"{pagePrefix}Daily_Row6_Col{col}", "0");
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
                    report.SetParameterValue($"{pagePrefix}Daily_Row12_Col{col}", "0");
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
                    report.SetParameterValue($"{pagePrefix}Daily_Row15_Col{col}", "0");
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
                    report.SetParameterValue($"{pagePrefix}Daily_Row18_Col{col}", "0");
                }
            }
        }

        /// <summary>
        /// 指定ページの月計データ設定（18行）
        /// </summary>
        private void SetPageMonthlyData(FR.Report report, List<BusinessDailyReportItem> monthlyItems, string pagePrefix, int startClassCode, int endClassCode, int maxColumns)
        {
            // 分類別データを辞書化（列番号をキーとする）
            var dataByClass = new Dictionary<int, BusinessDailyReportItem>();
            for (int i = startClassCode; i <= endClassCode; i++)
            {
                var code = i.ToString("000");
                var colIndex = i - startClassCode + 1;
                dataByClass[colIndex] = monthlyItems.FirstOrDefault(x => x.ClassificationCode == code);
            }

            // 000（合計）データを取得（Page1のみ）
            BusinessDailyReportItem? totalAll = null;
            if (string.IsNullOrEmpty(pagePrefix))
            {
                totalAll = monthlyItems.FirstOrDefault(x => x.ClassificationCode == "000");
            }

            // 行1：現金売上
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row1_Total", FormatNumber(totalAll.MonthlyCashSales));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCashSales;
                report.SetParameterValue($"{pagePrefix}Monthly_Row1_Col{col}", FormatNumber(value));
            }

            // 行2：現売消費税
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row2_Total", FormatNumber(totalAll.MonthlyCashSalesTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCashSalesTax;
                report.SetParameterValue($"{pagePrefix}Monthly_Row2_Col{col}", FormatNumber(value));
            }

            // 行3：掛売上と返品
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row3_Total", FormatNumber(totalAll.MonthlyCreditSales));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCreditSales;
                report.SetParameterValue($"{pagePrefix}Monthly_Row3_Col{col}", FormatNumber(value));
            }

            // 行4：売上値引
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row4_Total", FormatNumber(totalAll.MonthlySalesDiscount));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlySalesDiscount;
                report.SetParameterValue($"{pagePrefix}Monthly_Row4_Col{col}", FormatNumber(value));
            }

            // 行5：掛売消費税
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row5_Total", FormatNumber(totalAll.MonthlyCreditSalesTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCreditSalesTax;
                report.SetParameterValue($"{pagePrefix}Monthly_Row5_Col{col}", FormatNumber(value));
            }

            // 行6：＊売上計＊（合計行）
            if (totalAll != null) // Page1のみ合計列あり
            {
                var salesTotalAll = (totalAll.MonthlyCashSales ?? 0m) + (totalAll.MonthlyCashSalesTax ?? 0m) +
                                    (totalAll.MonthlyCreditSales ?? 0m) + (totalAll.MonthlySalesDiscount ?? 0m) +
                                    (totalAll.MonthlyCreditSalesTax ?? 0m);
                report.SetParameterValue($"{pagePrefix}Monthly_Row6_Total", FormatNumber(salesTotalAll));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.MonthlyCashSales ?? 0m) + (item.MonthlyCashSalesTax ?? 0m) +
                                   (item.MonthlyCreditSales ?? 0m) + (item.MonthlySalesDiscount ?? 0m) +
                                   (item.MonthlyCreditSalesTax ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Monthly_Row6_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Monthly_Row6_Col{col}", "0");
                }
            }

            // 行7：現金仕入
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row7_Total", FormatNumber(totalAll.MonthlyCashPurchase));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCashPurchase;
                report.SetParameterValue($"{pagePrefix}Monthly_Row7_Col{col}", FormatNumber(value));
            }

            // 行8：現仕消費税
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row8_Total", FormatNumber(totalAll.MonthlyCashPurchaseTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCashPurchaseTax;
                report.SetParameterValue($"{pagePrefix}Monthly_Row8_Col{col}", FormatNumber(value));
            }

            // 行9：掛仕入と返品
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row9_Total", FormatNumber(totalAll.MonthlyCreditPurchase));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCreditPurchase;
                report.SetParameterValue($"{pagePrefix}Monthly_Row9_Col{col}", FormatNumber(value));
            }

            // 行10：仕入値引
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row10_Total", FormatNumber(totalAll.MonthlyPurchaseDiscount));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyPurchaseDiscount;
                report.SetParameterValue($"{pagePrefix}Monthly_Row10_Col{col}", FormatNumber(value));
            }

            // 行11：掛仕入消費税
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row11_Total", FormatNumber(totalAll.MonthlyCreditPurchaseTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCreditPurchaseTax;
                report.SetParameterValue($"{pagePrefix}Monthly_Row11_Col{col}", FormatNumber(value));
            }

            // 行12：＊仕入計＊（合計行）
            if (totalAll != null) // Page1のみ合計列あり
            {
                var purchaseTotalAll = (totalAll.MonthlyCashPurchase ?? 0m) + (totalAll.MonthlyCashPurchaseTax ?? 0m) +
                                       (totalAll.MonthlyCreditPurchase ?? 0m) - (totalAll.MonthlyPurchaseDiscount ?? 0m) +
                                       (totalAll.MonthlyCreditPurchaseTax ?? 0m);
                report.SetParameterValue($"{pagePrefix}Monthly_Row12_Total", FormatNumber(purchaseTotalAll));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.MonthlyCashPurchase ?? 0m) + (item.MonthlyCashPurchaseTax ?? 0m) +
                                   (item.MonthlyCreditPurchase ?? 0m) - (item.MonthlyPurchaseDiscount ?? 0m) +
                                   (item.MonthlyCreditPurchaseTax ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Monthly_Row12_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Monthly_Row12_Col{col}", "0");
                }
            }

            // 行13：現金・小切手・手形入金
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row13_Total", FormatNumber(totalAll.MonthlyCashReceipt));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCashReceipt;
                report.SetParameterValue($"{pagePrefix}Monthly_Row13_Col{col}", FormatNumber(value));
            }

            // 行14：振込入金
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row14_Total", FormatNumber(totalAll.MonthlyBankReceipt));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyBankReceipt;
                report.SetParameterValue($"{pagePrefix}Monthly_Row14_Col{col}", FormatNumber(value));
            }

            // 行15：＊入金計＊（合計行）
            if (totalAll != null) // Page1のみ合計列あり
            {
                var receiptTotalAll = (totalAll.MonthlyCashReceipt ?? 0m) + 
                                      (totalAll.MonthlyBankReceipt ?? 0m) + 
                                      (totalAll.MonthlyOtherReceipt ?? 0m);
                report.SetParameterValue($"{pagePrefix}Monthly_Row15_Total", FormatNumber(receiptTotalAll));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.MonthlyCashReceipt ?? 0m) + 
                                   (item.MonthlyBankReceipt ?? 0m) + 
                                   (item.MonthlyOtherReceipt ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Monthly_Row15_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Monthly_Row15_Col{col}", "0");
                }
            }

            // 行16：現金・小切手・手形支払
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row16_Total", FormatNumber(totalAll.MonthlyCashPayment));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyCashPayment;
                report.SetParameterValue($"{pagePrefix}Monthly_Row16_Col{col}", FormatNumber(value));
            }

            // 行17：振込支払
            if (totalAll != null) // Page1のみ合計列あり
            {
                report.SetParameterValue($"{pagePrefix}Monthly_Row17_Total", FormatNumber(totalAll.MonthlyBankPayment));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var value = dataByClass[col]?.MonthlyBankPayment;
                report.SetParameterValue($"{pagePrefix}Monthly_Row17_Col{col}", FormatNumber(value));
            }

            // 行18：＊支払計＊（合計行）
            if (totalAll != null) // Page1のみ合計列あり
            {
                var paymentTotalAll = (totalAll.MonthlyCashPayment ?? 0m) + 
                                      (totalAll.MonthlyBankPayment ?? 0m) + 
                                      (totalAll.MonthlyOtherPayment ?? 0m);
                report.SetParameterValue($"{pagePrefix}Monthly_Row18_Total", FormatNumber(paymentTotalAll));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.MonthlyCashPayment ?? 0m) + 
                                   (item.MonthlyBankPayment ?? 0m) + 
                                   (item.MonthlyOtherPayment ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Monthly_Row18_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Monthly_Row18_Col{col}", "0");
                }
            }
        }

        /// <summary>
        /// 指定ページの年計データ設定（4行）
        /// </summary>
        private void SetPageYearlyData(FR.Report report, List<BusinessDailyReportItem> yearlyItems, 
            string pagePrefix, int startClassCode, int endClassCode, int maxColumns)
        {
            // 分類別データを辞書化（列番号をキーとする）
            var dataByClass = new Dictionary<int, BusinessDailyReportItem>();
            for (int i = startClassCode; i <= endClassCode; i++)
            {
                var code = i.ToString("000");
                var colIndex = i - startClassCode + 1;
                dataByClass[colIndex] = yearlyItems.FirstOrDefault(x => x.ClassificationCode == code);
            }

            // 000（合計）データを取得（Page1のみ）
            BusinessDailyReportItem? totalAll = null;
            if (string.IsNullOrEmpty(pagePrefix))
            {
                totalAll = yearlyItems.FirstOrDefault(x => x.ClassificationCode == "000");
            }

            // デバッグログ追加
            _logger.LogDebug($"年計データ設定開始 - Page: {pagePrefix}, データ件数: {yearlyItems?.Count ?? 0}");
            if (yearlyItems?.Any() == true)
            {
                var sample = yearlyItems.First();
                _logger.LogDebug($"年計サンプル - 分類{sample.ClassificationCode}: " +
                    $"現金売上={sample.YearlyCashSales}, 掛売上={sample.YearlyCreditSales}");
            }

            // 行1：売上（現金売上＋掛売上－売上値引）
            if (totalAll != null)
            {
                var yearlyTotalSales = (totalAll.YearlyCashSales ?? 0m) + 
                                      (totalAll.YearlyCreditSales ?? 0m) - 
                                      (totalAll.YearlySalesDiscount ?? 0m);
                report.SetParameterValue($"{pagePrefix}Yearly_Row1_Total", FormatNumber(yearlyTotalSales));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.YearlyCashSales ?? 0m) + 
                                  (item.YearlyCreditSales ?? 0m) - 
                                  (item.YearlySalesDiscount ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Yearly_Row1_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Yearly_Row1_Col{col}", "0");
                }
            }

            // 行2：売上消費税（現金売上消費税＋掛売上消費税）
            if (totalAll != null)
            {
                var yearlyTotalSalesTax = (totalAll.YearlyCashSalesTax ?? 0m) + 
                                          (totalAll.YearlyCreditSalesTax ?? 0m);
                report.SetParameterValue($"{pagePrefix}Yearly_Row2_Total", FormatNumber(yearlyTotalSalesTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.YearlyCashSalesTax ?? 0m) + 
                                  (item.YearlyCreditSalesTax ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Yearly_Row2_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Yearly_Row2_Col{col}", "0");
                }
            }

            // 行3：仕入（現金仕入＋掛仕入－仕入値引）
            if (totalAll != null)
            {
                var yearlyTotalPurchase = (totalAll.YearlyCashPurchase ?? 0m) + 
                                          (totalAll.YearlyCreditPurchase ?? 0m) - 
                                          (totalAll.YearlyPurchaseDiscount ?? 0m);
                report.SetParameterValue($"{pagePrefix}Yearly_Row3_Total", FormatNumber(yearlyTotalPurchase));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.YearlyCashPurchase ?? 0m) + 
                                  (item.YearlyCreditPurchase ?? 0m) - 
                                  (item.YearlyPurchaseDiscount ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Yearly_Row3_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Yearly_Row3_Col{col}", "0");
                }
            }

            // 行4：仕入消費税（現金仕入消費税＋掛仕入消費税）
            if (totalAll != null)
            {
                var yearlyTotalPurchaseTax = (totalAll.YearlyCashPurchaseTax ?? 0m) + 
                                             (totalAll.YearlyCreditPurchaseTax ?? 0m);
                report.SetParameterValue($"{pagePrefix}Yearly_Row4_Total", FormatNumber(yearlyTotalPurchaseTax));
            }
            for (int col = 1; col <= maxColumns; col++)
            {
                var item = dataByClass[col];
                if (item != null)
                {
                    var colTotal = (item.YearlyCashPurchaseTax ?? 0m) + 
                                  (item.YearlyCreditPurchaseTax ?? 0m);
                    report.SetParameterValue($"{pagePrefix}Yearly_Row4_Col{col}", FormatNumber(colTotal));
                }
                else
                {
                    report.SetParameterValue($"{pagePrefix}Yearly_Row4_Col{col}", "0");
                }
            }
        }

        /// <summary>
        /// FastReportテンプレートの構造を検証
        /// </summary>
        private void ValidateReportTemplate(FR.Report report)
        {
            // 本番環境では詳細なテンプレート検証は不要
            #if DEBUG
            _logger.LogDebug("FastReportテンプレート検証開始（デバッグモード）");
            
            try
            {
                var paramCount = report.Dictionary.Parameters.Count;
                _logger.LogDebug($"FastReportパラメータ総数: {paramCount}");
                
                // デバッグ環境でのみ詳細検証
                foreach (var page in report.Pages)
                {
                    if (page is FR.ReportPage reportPage)
                    {
                        foreach (var band in reportPage.AllObjects)
                        {
                            if (band is FR.TextObject textObj)
                            {
                                if (textObj.Name.Contains("Header_C9") || textObj.Name.Contains("Header_S9") ||
                                    textObj.Name.Contains("CustomerName9") || textObj.Name.Contains("SupplierName9"))
                                {
                                    _logger.LogDebug($"TextObject: {textObj.Name} at {textObj.Left}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"テンプレート検証エラー（デバッグ）: {ex.Message}");
            }
            
            _logger.LogDebug("FastReportテンプレート検証終了");
            #endif
        }

        /// <summary>
        /// 数値フォーマット（NULL→空文字、ゼロ→"0"、マイナス→▲記号、カンマ区切り）
        /// </summary>
        private string FormatNumber(decimal? value)
        {
            // NULLまたは0の場合は "0" を返す
            if (value == null || value == 0) return "0";

            // マイナス値は▲記号
            if (value < 0)
                return "▲" + Math.Abs(value.Value).ToString("#,##0");

            // 正の値はカンマ区切りで表示
            return value.Value.ToString("#,##0");
        }

        /// <summary>
        /// 文字列を指定長に切り詰め
        /// </summary>
        private string TruncateToLength(string value, int maxLength)
        {
            if (value == null)
            {
                return "";
            }
            
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            
            if (value.Length <= maxLength)
            {
                return value;
            }
            
            return value.Substring(0, maxLength);
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