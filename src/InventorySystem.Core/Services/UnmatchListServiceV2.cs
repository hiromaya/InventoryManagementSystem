using System.Diagnostics;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Base;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Core.Models;
using InventorySystem.Core.Services.Validation;
using InventorySystem.Core.Services.Dataset;
using InventorySystem.Core.Services.History;

namespace InventorySystem.Core.Services;

/// <summary>
/// アンマッチリストサービス（誤操作防止機能対応版）
/// </summary>
public class UnmatchListServiceV2 : BatchProcessBase, IUnmatchListService
{
    private readonly ICpInventoryRepository _cpInventoryRepository;
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IGradeMasterRepository _gradeMasterRepository;
    private readonly IClassMasterRepository _classMasterRepository;
    private readonly ICustomerMasterRepository _customerMasterRepository;
    private readonly IProductMasterRepository _productMasterRepository;
    private readonly ISupplierMasterRepository _supplierMasterRepository;

    public UnmatchListServiceV2(
        ICpInventoryRepository cpInventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        IInventoryRepository inventoryRepository,
        IGradeMasterRepository gradeMasterRepository,
        IClassMasterRepository classMasterRepository,
        ICustomerMasterRepository customerMasterRepository,
        IProductMasterRepository productMasterRepository,
        ISupplierMasterRepository supplierMasterRepository,
        IDateValidationService dateValidator,
        IDatasetManager datasetManager,
        IProcessHistoryService historyService,
        ILogger<UnmatchListServiceV2> logger)
        : base(dateValidator, datasetManager, historyService, logger)
    {
        _cpInventoryRepository = cpInventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _inventoryRepository = inventoryRepository;
        _gradeMasterRepository = gradeMasterRepository;
        _classMasterRepository = classMasterRepository;
        _customerMasterRepository = customerMasterRepository;
        _productMasterRepository = productMasterRepository;
        _supplierMasterRepository = supplierMasterRepository;
    }

    /// <summary>
    /// アンマッチリスト処理を実行（誤操作防止機能対応）
    /// </summary>
    public async Task<UnmatchListResult> ProcessUnmatchListAsync()
    {
        ProcessContext? context = null;
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 在庫マスタから最新JobDateを取得
            var jobDate = await _inventoryRepository.GetMaxJobDateAsync();
            
            // 処理初期化（日付検証、データセット登録、履歴開始）
            context = await InitializeProcess(jobDate, "UNMATCH_CHECK");
            
            _logger.LogInformation("アンマッチリスト処理開始 - 最新JobDate: {JobDate}, データセットID: {DataSetId}", 
                jobDate, context.DatasetId);

            // 処理1-1: CP在庫M作成（全期間対象）
            _logger.LogInformation("CP在庫マスタ作成開始（全期間対象）");
            var createResult = await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(context.DatasetId, null);
            _logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}", createResult);

            // 処理1-2: 当日エリアクリア
            _logger.LogInformation("当日エリアクリア開始");
            await _cpInventoryRepository.ClearDailyAreaAsync(context.DatasetId);
            _logger.LogInformation("当日エリアクリア完了");

            // 全期間データ集計
            _logger.LogInformation("全期間データ集計開始");
            await _cpInventoryRepository.AggregateSalesDataAsync(context.DatasetId, null);
            await _cpInventoryRepository.AggregatePurchaseDataAsync(context.DatasetId, null);
            await _cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(context.DatasetId, null);
            _logger.LogInformation("全期間データ集計完了");

            // 処理1-3: 当日在庫計算
            _logger.LogInformation("当日在庫計算開始");
            await _cpInventoryRepository.CalculateDailyStockAsync(context.DatasetId);
            await _cpInventoryRepository.SetDailyFlagToProcessedAsync(context.DatasetId);
            _logger.LogInformation("当日在庫計算完了");

            // 処理1-6: アンマッチリスト生成（全期間対象）
            _logger.LogInformation("アンマッチリスト生成開始（全期間）");
            var unmatchItems = await GenerateUnmatchListAsync(context.DatasetId);
            var unmatchList = unmatchItems.ToList();
            _logger.LogInformation("アンマッチリスト生成完了 - アンマッチ件数: {Count}", unmatchList.Count);

            stopwatch.Stop();

            var result = new UnmatchListResult
            {
                Success = true,
                DataSetId = context.DatasetId,
                UnmatchCount = unmatchList.Count,
                UnmatchItems = unmatchList,
                ProcessingTime = stopwatch.Elapsed
            };
            
            // 処理終了
            await FinalizeProcess(context, true, $"アンマッチ件数: {unmatchList.Count}");
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "アンマッチリスト処理でエラーが発生しました");
            
            if (context != null)
            {
                // 処理終了（エラー）
                await FinalizeProcess(context, false, ex.Message);
                
                // CP在庫マスタの削除を保留（日次終了処理まで保持）
                // Phase 1改修: 削除タイミングを日次終了処理後に変更
                _logger.LogInformation("CP在庫マスタを保持します（削除は日次終了処理後） - データセットID: {DataSetId}", context.DatasetId);
                
                /*
                try
                {
                    await _cpInventoryRepository.DeleteByDataSetIdAsync(context.DatasetId);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "CP在庫マスタのクリーンアップに失敗しました - データセットID: {DataSetId}", 
                        context.DatasetId);
                }
                */
            }

            return new UnmatchListResult
            {
                Success = false,
                DataSetId = context?.DatasetId ?? string.Empty,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    // 以下、既存のUnmatchListServiceから必要なメソッドをコピー
    public async Task<IEnumerable<UnmatchItem>> GenerateUnmatchListAsync(string dataSetId)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 売上伝票のアンマッチチェック（全期間）
        var salesUnmatches = await CheckSalesUnmatchAsync(dataSetId);
        unmatchItems.AddRange(salesUnmatches);

        // 仕入伝票のアンマッチチェック（全期間）
        var purchaseUnmatches = await CheckPurchaseUnmatchAsync(dataSetId);
        unmatchItems.AddRange(purchaseUnmatches);

        // 在庫調整のアンマッチチェック（全期間）
        var adjustmentUnmatches = await CheckInventoryAdjustmentUnmatchAsync(dataSetId);
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

    private async Task<IEnumerable<UnmatchItem>> CheckSalesUnmatchAsync(string dataSetId)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 売上伝票取得（全期間）
        var salesVouchers = await _salesVoucherRepository.GetAllAsync();
        _logger.LogDebug("売上伝票取得（全期間）: 総件数={TotalCount}", salesVouchers.Count());
        
        var salesList = salesVouchers
            .Where(s => s.VoucherType == "51" || s.VoucherType == "52") // 売上伝票
            .Where(s => s.DetailType == "1" || s.DetailType == "2")     // 明細種
            .Where(s => s.Quantity != 0)                                // 数量0以外
            .ToList();
            
        _logger.LogDebug("売上伝票フィルタ後: 件数={FilteredCount}", salesList.Count);

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
                    sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode);
                
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

    private async Task<IEnumerable<UnmatchItem>> CheckPurchaseUnmatchAsync(string dataSetId)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 仕入伝票取得（全期間）
        var purchaseVouchers = await _purchaseVoucherRepository.GetAllAsync();
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
                    purchase.ProductCode, purchase.GradeCode, purchase.ClassCode, purchase.ShippingMarkCode);
                
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
        string productCode, string gradeCode, string classCode, string shippingMarkCode)
    {
        var inventoryKey = new InventoryKey
        {
            ProductCode = productCode,
            GradeCode = gradeCode,
            ClassCode = classCode,
            ShippingMarkCode = shippingMarkCode,
            ShippingMarkName = string.Empty
        };

        // 全期間から最新の在庫マスタを取得
        var inventory = await _inventoryRepository.GetLatestByKeyAsync(inventoryKey);
        return inventory?.ProductCategory1 ?? string.Empty;
    }

    private async Task<IEnumerable<UnmatchItem>> CheckInventoryAdjustmentUnmatchAsync(string dataSetId)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 在庫調整伝票取得（全期間）
        var adjustments = await _inventoryAdjustmentRepository.GetAllAsync();
        var adjustmentList = adjustments
            .Where(a => a.VoucherType == "71" || a.VoucherType == "72")  // 在庫調整伝票
            .Where(a => a.DetailType == "1")                             // 明細種
            .Where(a => a.Quantity > 0)                                  // 数量 > 0
            .Where(a => a.CategoryCode.HasValue)                         // 区分コードあり
            .Where(a => a.CategoryCode.GetValueOrDefault() != 2 && a.CategoryCode.GetValueOrDefault() != 5)  // 区分2,5は除外
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
                    adjustment.ShippingMarkCode);
                
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

    private async Task<UnmatchItem> EnrichWithMasterData(UnmatchItem item)
    {
        // 等級名と階級名を取得して設定
        item.GradeName = await GetGradeNameAsync(item.Key.GradeCode);
        item.ClassName = await GetClassNameAsync(item.Key.ClassCode);
        
        // 得意先名が空の場合、得意先マスタから取得
        if (string.IsNullOrEmpty(item.CustomerName) && !string.IsNullOrEmpty(item.CustomerCode))
        {
            var customer = await _customerMasterRepository.GetByCodeAsync(item.CustomerCode);
            if (customer != null)
            {
                item.CustomerName = customer.CustomerName;
                _logger.LogInformation("得意先名補完: {Code} -> {Name}", item.CustomerCode, item.CustomerName);
            }
            else
            {
                item.CustomerName = $"得意先({item.CustomerCode})";
                _logger.LogInformation("得意先名補完(デフォルト): {Code} -> {Name}", item.CustomerCode, item.CustomerName);
            }
        }
        
        // 仕入先名が空の場合（仕入伝票の場合）、仕入先マスタから取得
        if (item.Category == "掛仕入" || item.Category == "現金仕入")
        {
            if (string.IsNullOrEmpty(item.CustomerName) && !string.IsNullOrEmpty(item.CustomerCode))
            {
                var supplier = await _supplierMasterRepository.GetByCodeAsync(item.CustomerCode);
                if (supplier != null)
                {
                    item.CustomerName = supplier.SupplierName;
                    _logger.LogInformation("仕入先名補完: {Code} -> {Name}", item.CustomerCode, item.CustomerName);
                }
                else
                {
                    item.CustomerName = $"仕入先({item.CustomerCode})";
                    _logger.LogInformation("仕入先名補完(デフォルト): {Code} -> {Name}", item.CustomerCode, item.CustomerName);
                }
            }
        }
        
        // 商品名が空の場合、商品マスタから取得
        if (string.IsNullOrEmpty(item.ProductName) && !string.IsNullOrEmpty(item.Key.ProductCode))
        {
            var product = await _productMasterRepository.GetByCodeAsync(item.Key.ProductCode);
            if (product != null)
            {
                item.ProductName = product.ProductName;
                _logger.LogInformation("商品名補完: {Code} -> {Name}", item.Key.ProductCode, item.ProductName);
            }
            else
            {
                // 商品マスタになければ在庫マスタから取得を試みる
                var inventoryKey = new InventoryKey
                {
                    ProductCode = item.Key.ProductCode,
                    GradeCode = item.Key.GradeCode,
                    ClassCode = item.Key.ClassCode,
                    ShippingMarkCode = item.Key.ShippingMarkCode,
                    ShippingMarkName = item.Key.ShippingMarkName
                };
                
                var inventory = await _inventoryRepository.GetLatestByKeyAsync(inventoryKey);
                if (inventory != null && !string.IsNullOrEmpty(inventory.ProductName))
                {
                    item.ProductName = inventory.ProductName;
                    _logger.LogInformation("商品名補完(在庫マスタ): {Code} -> {Name}", item.Key.ProductCode, item.ProductName);
                }
                else
                {
                    item.ProductName = $"商品({item.Key.ProductCode})";
                    _logger.LogInformation("商品名補完(デフォルト): {Code} -> {Name}", item.Key.ProductCode, item.ProductName);
                }
            }
        }
        
        return item;
    }

    private async Task<string> GetGradeNameAsync(string gradeCode)
    {
        if (string.IsNullOrEmpty(gradeCode)) return string.Empty;
        var gradeName = await _gradeMasterRepository.GetGradeNameAsync(gradeCode);
        return gradeName ?? $"等{gradeCode}";
    }

    private async Task<string> GetClassNameAsync(string classCode)
    {
        if (string.IsNullOrEmpty(classCode)) return string.Empty;
        var className = await _classMasterRepository.GetClassNameAsync(classCode);
        return className ?? $"階{classCode}";
    }
}