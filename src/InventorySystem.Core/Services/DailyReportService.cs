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

    public async Task<DailyReportResult> ProcessDailyReportAsync(DateTime reportDate, string? existingDataSetId = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var dataSetId = existingDataSetId ?? Guid.NewGuid().ToString();
        var isNewDataSet = existingDataSetId == null;
        
        try
        {
            _logger.LogInformation("商品日報処理開始 - レポート日付: {ReportDate}, データセットID: {DataSetId}", 
                reportDate, dataSetId);

            if (isNewDataSet)
            {
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
                var salesResult = await _cpInventoryRepository.AggregateSalesDataAsync(dataSetId, reportDate);
                _logger.LogInformation("売上データ集計完了 - 更新件数: {Count}", salesResult);
                
                var purchaseResult = await _cpInventoryRepository.AggregatePurchaseDataAsync(dataSetId, reportDate);
                _logger.LogInformation("仕入データ集計完了 - 更新件数: {Count}", purchaseResult);
                
                var adjustmentResult = await _cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(dataSetId, reportDate);
                _logger.LogInformation("在庫調整データ集計完了 - 更新件数: {Count}", adjustmentResult);
                
                _logger.LogInformation("当日データ集計完了");

                // 4. 当日在庫計算
                _logger.LogInformation("当日在庫計算開始");
                await _cpInventoryRepository.CalculateDailyStockAsync(dataSetId);
                await _cpInventoryRepository.SetDailyFlagToProcessedAsync(dataSetId);
                _logger.LogInformation("当日在庫計算完了");
            }
            else
            {
                _logger.LogInformation("既存のデータセットを使用: {DataSetId}", dataSetId);
            }

            // 5. 商品日報データ生成
            _logger.LogInformation("商品日報データ生成開始");
            var reportItems = await GetDailyReportDataAsync(reportDate, dataSetId);
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
                if (isNewDataSet)
                {
                    await _cpInventoryRepository.DeleteByDataSetIdAsync(dataSetId);
                }
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

    public async Task<List<DailyReportItem>> GetDailyReportDataAsync(DateTime reportDate, string dataSetId)
    {
        _logger.LogInformation("商品日報データ取得開始 - レポート日付: {ReportDate}, データセットID: {DataSetId}", reportDate, dataSetId);

        var reportItems = new List<DailyReportItem>();
        
        // CP在庫Mから商品ごとに集計してDailyReportItemを作成
        var cpInventories = await _cpInventoryRepository.GetAllAsync(dataSetId);
        _logger.LogInformation("CP在庫Mデータ取得完了 - 件数: {Count}", cpInventories.Count());
        
        // デバッグ: CP在庫データのサンプルを表示
        var cpSample = cpInventories.Take(5);
        foreach (var cp in cpSample)
        {
            _logger.LogInformation("CP在庫サンプル: 商品={ProductCode}, 売上数量={SalesQty}, 売上金額={SalesAmt}, 仕入数量={PurchaseQty}, 仕入金額={PurchaseAmt}",
                cp.Key.ProductCode, cp.DailySalesQuantity, cp.DailySalesAmount, cp.DailyPurchaseQuantity, cp.DailyPurchaseAmount);
        }
        
        // 統計情報を追加
        var salesCount = cpInventories.Count(cp => cp.DailySalesAmount != 0);
        var purchaseCount = cpInventories.Count(cp => cp.DailyPurchaseAmount != 0);
        var adjustmentCount = cpInventories.Count(cp => cp.DailyInventoryAdjustmentAmount != 0);
        _logger.LogInformation("統計: 売上データあり={SalesCount}件, 仕入データあり={PurchaseCount}件, 調整データあり={AdjustmentCount}件",
            salesCount, purchaseCount, adjustmentCount);
        
        // データがある在庫のみを対象とする（売上・仕入・調整のいずれかがある）
        _logger.LogInformation("フィルタリング条件: 売上数量!=0 OR 売上金額!=0 OR 仕入数量!=0 OR 仕入金額!=0 OR 調整数量!=0 OR 調整金額!=0");
        
        var activeInventories = cpInventories.Where(cp => 
        {
            bool hasData = cp.DailySalesQuantity != 0 || cp.DailySalesAmount != 0 ||
                          cp.DailyPurchaseQuantity != 0 || cp.DailyPurchaseAmount != 0 ||
                          cp.DailyInventoryAdjustmentQuantity != 0 || cp.DailyInventoryAdjustmentAmount != 0;
            
            if (!hasData && cpInventories.ToList().IndexOf(cp) < 3) // 最初の3件のみログ出力
            {
                _logger.LogInformation("除外されたデータ: 商品={ProductCode}, 売上数量={SalesQty}, 売上金額={SalesAmt}",
                    cp.Key.ProductCode, cp.DailySalesQuantity, cp.DailySalesAmount);
            }
            
            return hasData;
        }).ToList();
        
        _logger.LogInformation("有効な在庫データ件数: {Count}", activeInventories.Count);
        
        // デバッグ: 売上データがある最初の数件をログ出力
        var salesDataSample = activeInventories.Where(cp => cp.DailySalesAmount > 0).Take(5);
        _logger.LogInformation("有効な在庫データ中の売上データ件数: {Count}", salesDataSample.Count());
        foreach (var sample in salesDataSample)
        {
            _logger.LogInformation("売上データサンプル: 商品コード={ProductCode}, 売上金額={SalesAmount}, 売上数量={SalesQuantity}",
                sample.Key.ProductCode, sample.DailySalesAmount, sample.DailySalesQuantity);
        }
        
        // もし有効なデータが0件の場合、全データからサンプルを表示
        if (activeInventories.Count == 0)
        {
            _logger.LogWarning("有効なデータが0件です。全CP在庫データからサンプルを表示します。");
            var allSample = cpInventories.Take(10);
            foreach (var sample in allSample)
            {
                _logger.LogWarning("全データサンプル: 商品={ProductCode}, 売上数量={SalesQty}, 売上金額={SalesAmt}, 仕入数量={PurchaseQty}, 仕入金額={PurchaseAmt}, 調整数量={AdjQty}, 調整金額={AdjAmt}",
                    sample.Key.ProductCode, sample.DailySalesQuantity, sample.DailySalesAmount, 
                    sample.DailyPurchaseQuantity, sample.DailyPurchaseAmount,
                    sample.DailyInventoryAdjustmentQuantity, sample.DailyInventoryAdjustmentAmount);
            }
        }

        var groupedData = activeInventories
            .GroupBy(cp => new { cp.Key.ProductCode, cp.ProductCategory1 })
            .ToList();
            
        _logger.LogInformation("グループ化後のデータ件数: {Count}", groupedData.Count);

        foreach (var group in groupedData)
        {
            var item = new DailyReportItem
            {
                ProductCode = group.Key.ProductCode,
                ProductCategory1 = group.Key.ProductCategory1 ?? string.Empty,
                ProductName = group.First().ProductName ?? group.Key.ProductCode, // 仮実装

                // 日計項目（集計）
                DailySalesQuantity = group.Sum(cp => cp.DailySalesQuantity),
                DailySalesAmount = group.Sum(cp => cp.DailySalesAmount),
                DailyPurchaseDiscount = group.Sum(cp => cp.DailyDiscountAmount),
                DailyInventoryAdjustment = group.Sum(cp => cp.DailyInventoryAdjustmentAmount),
                DailyProcessingCost = group.Sum(cp => cp.DailyProcessingAmount),
                DailyTransfer = group.Sum(cp => cp.DailyTransferAmount),
                DailyIncentive = group.Sum(cp => cp.DailyIncentiveAmount),
                DailyGrossProfit1 = group.Sum(cp => cp.DailyGrossProfit),
                DailyDiscountAmount = group.Sum(cp => cp.DailyDiscountAmount)
            };

            // ２粗利益計算（１粗利益－歩引額）
            item.DailyGrossProfit2 = item.DailyGrossProfit1 - item.DailyDiscountAmount;

            // 粗利率計算（0除算対策）
            item.DailyGrossProfitRate1 = DailyReportItem.CalculateGrossProfitRate(item.DailyGrossProfit1, item.DailySalesAmount);
            item.DailyGrossProfitRate2 = DailyReportItem.CalculateGrossProfitRate(item.DailyGrossProfit2, item.DailySalesAmount);

            // 月計データを仮設定
            item.SetTemporaryMonthlyData();

            // データがある場合は追加（すでにフィルタリング済み）
            reportItems.Add(item);
            
            _logger.LogDebug("商品データ追加: {ProductCode} - 売上: {SalesAmount}円", 
                item.ProductCode, item.DailySalesAmount);
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