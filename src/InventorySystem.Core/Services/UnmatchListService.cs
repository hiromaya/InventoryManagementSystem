using System.Diagnostics;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Services;

public class UnmatchListService : IUnmatchListService
{
    private readonly ICpInventoryRepository _cpInventoryRepository;
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
    private readonly ILogger<UnmatchListService> _logger;

    public UnmatchListService(
        ICpInventoryRepository cpInventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        ILogger<UnmatchListService> logger)
    {
        _cpInventoryRepository = cpInventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _logger = logger;
    }

    public async Task<UnmatchListResult> ProcessUnmatchListAsync(DateTime jobDate)
    {
        var stopwatch = Stopwatch.StartNew();
        var dataSetId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogInformation("アンマッチリスト処理開始 - ジョブ日付: {JobDate}, データセットID: {DataSetId}", 
                jobDate, dataSetId);

            // 処理1-1: CP在庫M作成
            _logger.LogInformation("CP在庫マスタ作成開始");
            var createResult = await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(dataSetId, jobDate);
            _logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}", createResult);

            // 処理1-2: 当日エリアクリア
            _logger.LogInformation("当日エリアクリア開始");
            await _cpInventoryRepository.ClearDailyAreaAsync(dataSetId);
            _logger.LogInformation("当日エリアクリア完了");

            // 当日データ集計
            _logger.LogInformation("当日データ集計開始");
            await _cpInventoryRepository.AggregateSalesDataAsync(dataSetId, jobDate);
            await _cpInventoryRepository.AggregatePurchaseDataAsync(dataSetId, jobDate);
            await _cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(dataSetId, jobDate);
            _logger.LogInformation("当日データ集計完了");

            // 処理1-3: 当日在庫計算
            _logger.LogInformation("当日在庫計算開始");
            await _cpInventoryRepository.CalculateDailyStockAsync(dataSetId);
            await _cpInventoryRepository.SetDailyFlagToProcessedAsync(dataSetId);
            _logger.LogInformation("当日在庫計算完了");

            // 処理1-6: アンマッチリスト生成
            _logger.LogInformation("アンマッチリスト生成開始");
            var unmatchItems = await GenerateUnmatchListAsync(dataSetId, jobDate);
            var unmatchList = unmatchItems.ToList();
            _logger.LogInformation("アンマッチリスト生成完了 - アンマッチ件数: {Count}", unmatchList.Count);

            stopwatch.Stop();

            return new UnmatchListResult
            {
                Success = true,
                DataSetId = dataSetId,
                UnmatchCount = unmatchList.Count,
                UnmatchItems = unmatchList,
                ProcessingTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "アンマッチリスト処理でエラーが発生しました - データセットID: {DataSetId}", dataSetId);
            
            try
            {
                await _cpInventoryRepository.DeleteByDataSetIdAsync(dataSetId);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "CP在庫マスタのクリーンアップに失敗しました - データセットID: {DataSetId}", dataSetId);
            }

            return new UnmatchListResult
            {
                Success = false,
                DataSetId = dataSetId,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<IEnumerable<UnmatchItem>> GenerateUnmatchListAsync(string dataSetId, DateTime jobDate)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 売上伝票のアンマッチチェック
        var salesUnmatches = await CheckSalesUnmatchAsync(dataSetId, jobDate);
        unmatchItems.AddRange(salesUnmatches);

        // 仕入伝票のアンマッチチェック
        var purchaseUnmatches = await CheckPurchaseUnmatchAsync(dataSetId, jobDate);
        unmatchItems.AddRange(purchaseUnmatches);

        // 在庫調整のアンマッチチェック
        var adjustmentUnmatches = await CheckInventoryAdjustmentUnmatchAsync(dataSetId, jobDate);
        unmatchItems.AddRange(adjustmentUnmatches);

        // ソート：商品分類1、商品コード、荷印コード、荷印名、等級コード、階級コード
        return unmatchItems
            .OrderBy(x => x.ProductCategory1)
            .ThenBy(x => x.Key.ProductCode)
            .ThenBy(x => x.Key.ShippingMarkCode)
            .ThenBy(x => x.Key.ShippingMarkName)
            .ThenBy(x => x.Key.GradeCode)
            .ThenBy(x => x.Key.ClassCode);
    }

    private async Task<IEnumerable<UnmatchItem>> CheckSalesUnmatchAsync(string dataSetId, DateTime jobDate)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 売上伝票取得
        var salesVouchers = await _salesVoucherRepository.GetByJobDateAsync(jobDate);
        var salesList = salesVouchers
            .Where(s => s.VoucherType == "51" || s.VoucherType == "52") // 売上伝票
            .Where(s => s.DetailType == "1" || s.DetailType == "2")     // 明細種
            .Where(s => s.Quantity != 0)                                // 数量0以外
            .ToList();

        foreach (var sales in salesList)
        {
            var inventoryKey = new InventoryKey
            {
                ProductCode = sales.ProductCode,
                GradeCode = sales.GradeCode,
                ClassCode = sales.ClassCode,
                ShippingMarkCode = sales.ShippingMarkCode,
                ShippingMarkName = sales.ShippingMarkName
            };

            // CP在庫マスタから該当データを取得
            var cpInventory = await _cpInventoryRepository.GetByKeyAsync(inventoryKey, dataSetId);

            if (cpInventory == null)
            {
                // 該当無エラー
                var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "該当無", 
                    productCategory1: GetProductCategory1FromSales(sales));
                unmatchItems.Add(unmatchItem);
            }
            else if (cpInventory.PreviousDayStock >= 0 && cpInventory.DailyStock == 0)
            {
                // 在庫0エラー
                var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "在庫0",
                    cpInventory.ProductName, 
                    GetGradeName(cpInventory.Key.GradeCode),
                    GetClassName(cpInventory.Key.ClassCode),
                    cpInventory.GetAdjustedProductCategory1());
                unmatchItems.Add(unmatchItem);
            }
        }

        return unmatchItems;
    }

    private async Task<IEnumerable<UnmatchItem>> CheckPurchaseUnmatchAsync(string dataSetId, DateTime jobDate)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 仕入伝票取得
        var purchaseVouchers = await _purchaseVoucherRepository.GetByJobDateAsync(jobDate);
        var purchaseList = purchaseVouchers
            .Where(p => p.VoucherType == "11" || p.VoucherType == "12") // 仕入伝票
            .Where(p => p.DetailType == "1" || p.DetailType == "2")     // 明細種
            .Where(p => p.Quantity != 0)                                // 数量0以外
            .ToList();

        foreach (var purchase in purchaseList)
        {
            var inventoryKey = new InventoryKey
            {
                ProductCode = purchase.ProductCode,
                GradeCode = purchase.GradeCode,
                ClassCode = purchase.ClassCode,
                ShippingMarkCode = purchase.ShippingMarkCode,
                ShippingMarkName = purchase.ShippingMarkName
            };

            // CP在庫マスタから該当データを取得
            var cpInventory = await _cpInventoryRepository.GetByKeyAsync(inventoryKey, dataSetId);

            if (cpInventory == null)
            {
                // 該当無エラー
                var unmatchItem = UnmatchItem.FromPurchaseVoucher(purchase, "該当無",
                    productCategory1: GetProductCategory1FromPurchase(purchase));
                unmatchItems.Add(unmatchItem);
            }
            else if (cpInventory.DailyStock == 0)
            {
                // 在庫0エラー
                var unmatchItem = UnmatchItem.FromPurchaseVoucher(purchase, "在庫0",
                    cpInventory.ProductName,
                    GetGradeName(cpInventory.Key.GradeCode),
                    GetClassName(cpInventory.Key.ClassCode),
                    cpInventory.GetAdjustedProductCategory1());
                unmatchItems.Add(unmatchItem);
            }
        }

        return unmatchItems;
    }

    private string GetProductCategory1FromSales(SalesVoucher sales)
    {
        // 商品分類1を取得するロジック（商品マスタから取得する必要があります）
        // 現時点では空文字を返す
        return string.Empty;
    }

    private string GetProductCategory1FromPurchase(PurchaseVoucher purchase)
    {
        // 商品分類1を取得するロジック（商品マスタから取得する必要があります）
        // 現時点では空文字を返す
        return string.Empty;
    }

    private string GetGradeName(string gradeCode)
    {
        // 等級名を取得するロジック（等級マスタから取得する必要があります）
        // 現時点ではコードをそのまま返す
        return gradeCode;
    }

    private string GetClassName(string classCode)
    {
        // 階級名を取得するロジック（階級マスタから取得する必要があります）
        // 現時点ではコードをそのまま返す
        return classCode;
    }

    private async Task<IEnumerable<UnmatchItem>> CheckInventoryAdjustmentUnmatchAsync(string dataSetId, DateTime jobDate)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 在庫調整伝票取得
        var adjustments = await _inventoryAdjustmentRepository.GetByJobDateAsync(jobDate);
        var adjustmentList = adjustments
            .Where(a => a.VoucherType == "71" || a.VoucherType == "72")  // 在庫調整伝票
            .Where(a => a.DetailType == "1")                             // 明細種
            .Where(a => a.Quantity > 0)                                  // 数量 > 0
            .Where(a => a.CategoryCode.HasValue)                         // 区分コードあり
            .Where(a => a.CategoryCode.GetValueOrDefault() != 2 && a.CategoryCode.GetValueOrDefault() != 5)  // 区分2,5（経費、加工）は除外
            .ToList();

        foreach (var adjustment in adjustmentList)
        {
            var inventoryKey = new InventoryKey
            {
                ProductCode = adjustment.ProductCode,
                GradeCode = adjustment.GradeCode,
                ClassCode = adjustment.ClassCode,
                ShippingMarkCode = adjustment.ShippingMarkCode,
                ShippingMarkName = adjustment.ShippingMarkName
            };

            // CP在庫マスタから該当データを取得
            var cpInventory = await _cpInventoryRepository.GetByKeyAsync(inventoryKey, dataSetId);

            if (cpInventory == null)
            {
                // 該当無エラー
                var unmatchItem = UnmatchItem.FromInventoryAdjustment(
                    adjustment.VoucherType,
                    adjustment.CategoryCode.GetValueOrDefault(),
                    adjustment.CustomerCode ?? string.Empty,
                    adjustment.CustomerName ?? string.Empty,
                    inventoryKey,
                    adjustment.Quantity,
                    adjustment.UnitPrice,
                    adjustment.Amount,
                    adjustment.VoucherNumber,
                    "該当無",
                    productCategory1: GetProductCategory1FromAdjustment(adjustment)
                );
                unmatchItems.Add(unmatchItem);
            }
            else if (cpInventory.DailyStock == 0)
            {
                // 在庫0エラー
                var unmatchItem = UnmatchItem.FromInventoryAdjustment(
                    adjustment.VoucherType,
                    adjustment.CategoryCode.GetValueOrDefault(),
                    adjustment.CustomerCode ?? string.Empty,
                    adjustment.CustomerName ?? string.Empty,
                    inventoryKey,
                    adjustment.Quantity,
                    adjustment.UnitPrice,
                    adjustment.Amount,
                    adjustment.VoucherNumber,
                    "在庫0",
                    cpInventory.ProductName,
                    GetGradeName(cpInventory.Key.GradeCode),
                    GetClassName(cpInventory.Key.ClassCode),
                    cpInventory.GetAdjustedProductCategory1()
                );
                unmatchItems.Add(unmatchItem);
            }
        }

        return unmatchItems;
    }

    private string GetProductCategory1FromAdjustment(InventoryAdjustment adjustment)
    {
        // 商品分類1を取得するロジック（商品マスタから取得する必要があります）
        // 現時点では空文字を返す
        return string.Empty;
    }
}