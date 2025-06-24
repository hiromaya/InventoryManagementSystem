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
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ILogger<UnmatchListService> _logger;

    public UnmatchListService(
        ICpInventoryRepository cpInventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        IInventoryRepository inventoryRepository,
        ILogger<UnmatchListService> logger)
    {
        _cpInventoryRepository = cpInventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _inventoryRepository = inventoryRepository;
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

        // マスタデータで名前を補完
        var enrichedItems = new List<UnmatchItem>();
        foreach (var item in unmatchItems)
        {
            var enrichedItem = await EnrichWithMasterData(item);
            enrichedItems.Add(enrichedItem);
        }

        // ソート：商品分類1、商品コード、荷印コード、荷印名、等級コード、階級コード
        return enrichedItems
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
                // 該当無エラー - 商品分類1を取得
                var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
                    sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode, jobDate);
                
                var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "", productCategory1);
                unmatchItem.AlertType2 = "該当無";
                unmatchItems.Add(unmatchItem);
            }
            else if (cpInventory.PreviousDayStock >= 0 && cpInventory.DailyStock == 0)
            {
                // 在庫0エラー
                var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "在庫0",
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
                // 該当無エラー - 商品分類1を取得
                var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
                    purchase.ProductCode, purchase.GradeCode, purchase.ClassCode, purchase.ShippingMarkCode, jobDate);
                
                var unmatchItem = UnmatchItem.FromPurchaseVoucher(purchase, "", productCategory1);
                unmatchItem.AlertType2 = "該当無";
                unmatchItems.Add(unmatchItem);
            }
            else if (cpInventory.DailyStock == 0)
            {
                // 在庫0エラー
                var unmatchItem = UnmatchItem.FromPurchaseVoucher(purchase, "在庫0",
                    cpInventory.GetAdjustedProductCategory1());
                unmatchItems.Add(unmatchItem);
            }
        }

        return unmatchItems;
    }

    private async Task<string> GetProductCategory1FromInventoryMasterAsync(
        string productCode, string gradeCode, string classCode, string shippingMarkCode, DateTime jobDate)
    {
        // 商品コードだけでなく、全てのキー項目で在庫マスタを検索
        var inventoryKey = new InventoryKey
        {
            ProductCode = productCode,
            GradeCode = gradeCode,
            ClassCode = classCode,
            ShippingMarkCode = shippingMarkCode,
            ShippingMarkName = string.Empty // 荷印名は検索キーに含めない
        };

        var inventory = await _inventoryRepository.GetByKeyAsync(inventoryKey, jobDate);
        
        if (inventory != null)
        {
            return inventory.ProductCategory1;
        }

        // 見つからない場合は空文字を返す
        return string.Empty;
    }

    private string GetProductCategory1FromSales(SalesVoucher sales)
    {
        // 非同期メソッドを同期的に呼び出す（理想的ではないが、既存のインターフェースを維持するため）
        var task = GetProductCategory1FromInventoryMasterAsync(
            sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode, sales.JobDate);
        task.Wait();
        return task.Result;
    }

    private string GetProductCategory1FromPurchase(PurchaseVoucher purchase)
    {
        // 非同期メソッドを同期的に呼び出す（理想的ではないが、既存のインターフェースを維持するため）
        var task = GetProductCategory1FromInventoryMasterAsync(
            purchase.ProductCode, purchase.GradeCode, purchase.ClassCode, purchase.ShippingMarkCode, purchase.JobDate);
        task.Wait();
        return task.Result;
    }

    private string GetGradeName(string gradeCode)
    {
        // 等級マスタが存在しないため、コードをそのまま返す
        // 将来的に等級マスタが追加された場合はここを修正
        return gradeCode;
    }

    private string GetClassName(string classCode)
    {
        // 階級マスタが存在しないため、コードをそのまま返す
        // 将来的に階級マスタが追加された場合はここを修正
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
                // 該当無エラー - 商品分類1を取得
                var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
                    adjustment.ProductCode, adjustment.GradeCode, adjustment.ClassCode, 
                    adjustment.ShippingMarkCode, jobDate);
                
                var unmatchItem = UnmatchItem.FromInventoryAdjustment(adjustment, "", productCategory1);
                unmatchItem.AlertType2 = "該当無";
                unmatchItems.Add(unmatchItem);
            }
            else if (cpInventory.DailyStock == 0)
            {
                // 在庫0エラー
                var unmatchItem = UnmatchItem.FromInventoryAdjustment(adjustment, "在庫0",
                    cpInventory.GetAdjustedProductCategory1());
                unmatchItems.Add(unmatchItem);
            }
        }

        return unmatchItems;
    }

    private string GetProductCategory1FromAdjustment(InventoryAdjustment adjustment)
    {
        // 非同期メソッドを同期的に呼び出す（理想的ではないが、既存のインターフェースを維持するため）
        var task = GetProductCategory1FromInventoryMasterAsync(
            adjustment.ProductCode, adjustment.GradeCode, adjustment.ClassCode, 
            adjustment.ShippingMarkCode, adjustment.JobDate);
        task.Wait();
        return task.Result;
    }

    private async Task<UnmatchItem> EnrichWithMasterData(UnmatchItem item)
    {
        // 得意先名が空の場合、得意先マスタから取得を試みる
        if (string.IsNullOrEmpty(item.CustomerName) && !string.IsNullOrEmpty(item.CustomerCode))
        {
            // 現在得意先マスタは実装されていないため、コードを表示
            item.CustomerName = $"得意先({item.CustomerCode})";
            _logger.LogInformation("得意先名補完: {Code} -> {Name}", item.CustomerCode, item.CustomerName);
        }
        
        // 商品名が空の場合、在庫マスタから取得を試みる
        if (string.IsNullOrEmpty(item.ProductName) && !string.IsNullOrEmpty(item.Key.ProductCode))
        {
            var inventoryKey = new InventoryKey
            {
                ProductCode = item.Key.ProductCode,
                GradeCode = item.Key.GradeCode,
                ClassCode = item.Key.ClassCode,
                ShippingMarkCode = item.Key.ShippingMarkCode,
                ShippingMarkName = item.Key.ShippingMarkName
            };
            
            var inventory = await _inventoryRepository.GetByKeyAsync(inventoryKey, item.VoucherDate);
            if (inventory != null && !string.IsNullOrEmpty(inventory.ProductName))
            {
                item.ProductName = inventory.ProductName;
                _logger.LogInformation("商品名補完: {Code} -> {Name}", item.Key.ProductCode, item.ProductName);
            }
            else
            {
                item.ProductName = $"商品({item.Key.ProductCode})";
                _logger.LogInformation("商品名補完(デフォルト): {Code} -> {Name}", item.Key.ProductCode, item.ProductName);
            }
        }
        
        return item;
    }
}