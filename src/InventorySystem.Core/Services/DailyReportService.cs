using System.Diagnostics;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Services;

/// <summary>
/// 商品日報サービス
/// </summary>
public class DailyReportService : IDailyReportService
{
    private readonly ICpInventoryRepository _cpInventoryRepository;
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
    private readonly ILogger<DailyReportService> _logger;

    public DailyReportService(
        ICpInventoryRepository cpInventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        ILogger<DailyReportService> logger)
    {
        _cpInventoryRepository = cpInventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _logger = logger;
    }

    public async Task<DailyReportResult> ProcessDailyReportAsync(DateTime reportDate)
    {
        var stopwatch = Stopwatch.StartNew();
        var dataSetId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogInformation("商品日報処理開始 - レポート日付: {ReportDate}, データセットID: {DataSetId}", 
                reportDate, dataSetId);

            // 1. CP在庫M作成
            _logger.LogInformation("CP在庫マスタ作成開始");
            var createResult = await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(dataSetId, reportDate);
            _logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}", createResult);

            // 2. 当日エリアクリア
            _logger.LogInformation("当日エリアクリア開始");
            await _cpInventoryRepository.ClearDailyAreaAsync(dataSetId);
            _logger.LogInformation("当日エリアクリア完了");

            // 3. 当日データ集計
            _logger.LogInformation("当日データ集計開始");
            await _cpInventoryRepository.AggregateSalesDataAsync(dataSetId, reportDate);
            await _cpInventoryRepository.AggregatePurchaseDataAsync(dataSetId, reportDate);
            await _cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(dataSetId, reportDate);
            _logger.LogInformation("当日データ集計完了");

            // 4. 当日在庫計算
            _logger.LogInformation("当日在庫計算開始");
            await _cpInventoryRepository.CalculateDailyStockAsync(dataSetId);
            await _cpInventoryRepository.SetDailyFlagToProcessedAsync(dataSetId);
            _logger.LogInformation("当日在庫計算完了");

            // 5. 商品日報データ生成
            _logger.LogInformation("商品日報データ生成開始");
            var reportItems = await GetDailyReportDataAsync(reportDate);
            _logger.LogInformation("商品日報データ生成完了 - データ件数: {Count}", reportItems.Count);

            // 6. 集計データ作成
            var subtotals = CreateSubtotals(reportItems);
            var total = CreateTotal(reportItems);

            stopwatch.Stop();

            return new DailyReportResult
            {
                Success = true,
                DataSetId = dataSetId,
                ProcessedCount = reportItems.Count,
                ReportItems = reportItems,
                Subtotals = subtotals,
                Total = total,
                ProcessingTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "商品日報処理でエラーが発生しました - データセットID: {DataSetId}", dataSetId);
            
            try
            {
                await _cpInventoryRepository.DeleteByDataSetIdAsync(dataSetId);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "CP在庫マスタのクリーンアップに失敗しました - データセットID: {DataSetId}", dataSetId);
            }

            return new DailyReportResult
            {
                Success = false,
                DataSetId = dataSetId,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<List<DailyReportItem>> GetDailyReportDataAsync(DateTime reportDate)
    {
        _logger.LogInformation("商品日報データ取得開始 - レポート日付: {ReportDate}", reportDate);

        var reportItems = new List<DailyReportItem>();

        // 仮実装：CP在庫Mから商品ごとに集計してDailyReportItemを作成
        // 実際の実装では、CP在庫Mテーブルからデータを取得して変換する
        var cpInventories = await _cpInventoryRepository.GetAllAsync(); // 仮メソッド

        var groupedData = cpInventories
            .GroupBy(cp => new { cp.Key.ProductCode, cp.ProductCategory1 })
            .ToList();

        foreach (var group in groupedData)
        {
            var item = new DailyReportItem
            {
                ProductCode = group.Key.ProductCode,
                ProductCategory1 = group.Key.ProductCategory1 ?? string.Empty,
                ProductName = group.First().ProductName ?? group.Key.ProductCode, // 仮実装

                // 日計項目（集計）
                DailySalesQuantity = group.Sum(cp => cp.DailySalesQuantity),
                DailySalesAmount = group.Sum(cp => cp.DailySalesAmount + cp.DailySalesReturnAmount),
                DailyPurchaseDiscount = group.Sum(cp => cp.DailyPurchaseDiscountAmount),
                DailyInventoryAdjustment = group.Sum(cp => cp.DailyInventoryAdjustmentAmount),
                DailyProcessingCost = group.Sum(cp => cp.DailyProcessingAmount),
                DailyTransfer = group.Sum(cp => cp.DailyTransferAmount),
                DailyIncentive = group.Sum(cp => cp.DailyIncentiveAmount),
                DailyGrossProfit1 = group.Sum(cp => cp.DailyGrossProfitAmount),
                DailyDiscountAmount = group.Sum(cp => cp.DailyDiscountAmount)
            };

            // ２粗利益計算（１粗利益－歩引額）
            item.DailyGrossProfit2 = item.DailyGrossProfit1 - item.DailyDiscountAmount;

            // 粗利率計算（0除算対策）
            item.DailyGrossProfitRate1 = DailyReportItem.CalculateGrossProfitRate(item.DailyGrossProfit1, item.DailySalesAmount);
            item.DailyGrossProfitRate2 = DailyReportItem.CalculateGrossProfitRate(item.DailyGrossProfit2, item.DailySalesAmount);

            // 月計データを仮設定
            item.SetTemporaryMonthlyData();

            // オール0明細は除外
            if (!item.IsAllZero())
            {
                reportItems.Add(item);
            }
        }

        // ソート：商品分類1 → 商品コード
        var sortedItems = reportItems
            .OrderBy(item => item.ProductCategory1)
            .ThenBy(item => item.ProductCode)
            .ToList();

        _logger.LogInformation("商品日報データ取得完了 - 件数: {Count}", sortedItems.Count);
        return sortedItems;
    }

    /// <summary>
    /// 大分類計を作成
    /// </summary>
    private List<DailyReportSubtotal> CreateSubtotals(List<DailyReportItem> items)
    {
        var subtotals = new List<DailyReportSubtotal>();

        var groupedByCategory = items.GroupBy(item => item.ProductCategory1);

        foreach (var group in groupedByCategory)
        {
            var subtotal = new DailyReportSubtotal
            {
                ProductCategory1 = group.Key,
                TotalDailySalesQuantity = group.Sum(item => item.DailySalesQuantity),
                TotalDailySalesAmount = group.Sum(item => item.DailySalesAmount),
                TotalDailyPurchaseDiscount = group.Sum(item => item.DailyPurchaseDiscount),
                TotalDailyInventoryAdjustment = group.Sum(item => item.DailyInventoryAdjustment),
                TotalDailyProcessingCost = group.Sum(item => item.DailyProcessingCost),
                TotalDailyTransfer = group.Sum(item => item.DailyTransfer),
                TotalDailyIncentive = group.Sum(item => item.DailyIncentive),
                TotalDailyGrossProfit1 = group.Sum(item => item.DailyGrossProfit1),
                TotalDailyGrossProfit2 = group.Sum(item => item.DailyGrossProfit2),
                TotalMonthlySalesAmount = group.Sum(item => item.MonthlySalesAmount),
                TotalMonthlyGrossProfit1 = group.Sum(item => item.MonthlyGrossProfit1),
                TotalMonthlyGrossProfit2 = group.Sum(item => item.MonthlyGrossProfit2)
            };

            subtotals.Add(subtotal);
        }

        return subtotals;
    }

    /// <summary>
    /// 合計を作成
    /// </summary>
    private DailyReportTotal CreateTotal(List<DailyReportItem> items)
    {
        return new DailyReportTotal
        {
            GrandTotalDailySalesQuantity = items.Sum(item => item.DailySalesQuantity),
            GrandTotalDailySalesAmount = items.Sum(item => item.DailySalesAmount),
            GrandTotalDailyPurchaseDiscount = items.Sum(item => item.DailyPurchaseDiscount),
            GrandTotalDailyInventoryAdjustment = items.Sum(item => item.DailyInventoryAdjustment),
            GrandTotalDailyProcessingCost = items.Sum(item => item.DailyProcessingCost),
            GrandTotalDailyTransfer = items.Sum(item => item.DailyTransfer),
            GrandTotalDailyIncentive = items.Sum(item => item.DailyIncentive),
            GrandTotalDailyGrossProfit1 = items.Sum(item => item.DailyGrossProfit1),
            GrandTotalDailyGrossProfit2 = items.Sum(item => item.DailyGrossProfit2),
            GrandTotalMonthlySalesAmount = items.Sum(item => item.MonthlySalesAmount),
            GrandTotalMonthlyGrossProfit1 = items.Sum(item => item.MonthlyGrossProfit1),
            GrandTotalMonthlyGrossProfit2 = items.Sum(item => item.MonthlyGrossProfit2)
        };
    }
}