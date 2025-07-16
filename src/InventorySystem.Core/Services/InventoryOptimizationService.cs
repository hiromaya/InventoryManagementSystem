using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Core.Services;

/// <summary>
/// 在庫マスタ最適化サービス
/// 日次在庫計算と累積在庫管理を行う
/// </summary>
public class InventoryOptimizationService : IInventoryOptimizationService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
    private readonly ILogger<InventoryOptimizationService> _logger;

    public InventoryOptimizationService(
        IInventoryRepository inventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        ILogger<InventoryOptimizationService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _logger = logger;
    }

    /// <summary>
    /// 指定日の在庫マスタを最適化
    /// </summary>
    /// <param name="jobDate">対象日</param>
    /// <returns>最適化結果</returns>
    public async Task<InventoryOptimizationResult> OptimizeInventoryAsync(DateTime jobDate)
    {
        _logger.LogInformation("在庫マスタ最適化開始: JobDate={JobDate}", jobDate);

        var result = new InventoryOptimizationResult
        {
            JobDate = jobDate,
            StartTime = DateTime.Now
        };

        try
        {
            // 1. 前日在庫の継承
            var previousDayStock = await InheritPreviousDayStockAsync(jobDate);
            result.PreviousDayStockCount = previousDayStock.Count;

            // 2. 当日の伝票データ取得
            var dailyTransactions = await GetDailyTransactionsAsync(jobDate);
            result.SalesTransactionCount = dailyTransactions.Sales.Count;
            result.PurchaseTransactionCount = dailyTransactions.Purchase.Count;
            result.AdjustmentTransactionCount = dailyTransactions.Adjustment.Count;

            // 3. 在庫計算実行
            var calculatedStock = await CalculateStockAsync(previousDayStock, dailyTransactions);
            result.CalculatedStockCount = calculatedStock.Count;

            // 4. 在庫マスタ更新
            var updateResult = await UpdateInventoryMasterAsync(jobDate, calculatedStock);
            result.UpdatedRecordCount = updateResult.UpdatedCount;
            result.InsertedRecordCount = updateResult.InsertedCount;
            result.DeletedRecordCount = updateResult.DeletedCount;

            // 5. 0在庫の削除
            var cleanupResult = await CleanupZeroStockAsync(jobDate);
            result.CleanedUpRecordCount = cleanupResult;

            result.EndTime = DateTime.Now;
            result.IsSuccess = true;
            result.ProcessingTime = result.EndTime.Value - result.StartTime;

            _logger.LogInformation("在庫マスタ最適化完了: JobDate={JobDate}, 処理時間={ProcessingTime}ms", 
                jobDate, result.ProcessingTime.Value.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.Now;
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.ProcessingTime = result.EndTime.Value - result.StartTime;

            _logger.LogError(ex, "在庫マスタ最適化エラー: JobDate={JobDate}", jobDate);
            throw;
        }
    }

    /// <summary>
    /// 前日在庫を継承
    /// </summary>
    /// <param name="jobDate">対象日</param>
    /// <returns>前日在庫データ</returns>
    private async Task<Dictionary<InventoryKey, InventoryMaster>> InheritPreviousDayStockAsync(DateTime jobDate)
    {
        var previousDay = jobDate.AddDays(-1);
        _logger.LogDebug("前日在庫継承開始: PreviousDay={PreviousDay}", previousDay);

        var previousDayInventory = await _inventoryRepository.GetByJobDateAsync(previousDay);
        var inheritedStock = new Dictionary<InventoryKey, InventoryMaster>();

        foreach (var inventory in previousDayInventory)
        {
            var key = inventory.Key;

            // 前日在庫を当日の開始在庫として継承
            var inheritedInventory = new InventoryMaster
            {
                Key = new InventoryKey
                {
                    ProductCode = inventory.Key.ProductCode,
                    GradeCode = inventory.Key.GradeCode,
                    ClassCode = inventory.Key.ClassCode,
                    ShippingMarkCode = inventory.Key.ShippingMarkCode,
                    ShippingMarkName = inventory.Key.ShippingMarkName
                },
                ProductName = inventory.ProductName,
                Unit = inventory.Unit,
                StandardPrice = inventory.StandardPrice,
                AveragePrice = inventory.AveragePrice,
                ProductCategory1 = inventory.ProductCategory1,
                ProductCategory2 = inventory.ProductCategory2,
                CurrentStock = inventory.CurrentStock,
                CurrentStockAmount = inventory.CurrentStockAmount,
                DailyStock = inventory.CurrentStock,
                DailyStockAmount = inventory.CurrentStockAmount,
                PreviousMonthQuantity = inventory.PreviousMonthQuantity,
                PreviousMonthAmount = inventory.PreviousMonthAmount,
                JobDate = jobDate,
                ImportType = "CUMULATIVE",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            inheritedStock[key] = inheritedInventory;
        }

        _logger.LogDebug("前日在庫継承完了: 継承件数={Count}", inheritedStock.Count);
        return inheritedStock;
    }

    /// <summary>
    /// 当日の伝票データを取得
    /// </summary>
    /// <param name="jobDate">対象日</param>
    /// <returns>当日の伝票データ</returns>
    private async Task<DailyTransactions> GetDailyTransactionsAsync(DateTime jobDate)
    {
        _logger.LogDebug("当日伝票データ取得開始: JobDate={JobDate}", jobDate);

        var salesTask = _salesVoucherRepository.GetByJobDateAsync(jobDate);
        var purchaseTask = _purchaseVoucherRepository.GetByJobDateAsync(jobDate);
        var adjustmentTask = _inventoryAdjustmentRepository.GetByJobDateAsync(jobDate);

        await Task.WhenAll(salesTask, purchaseTask, adjustmentTask);

        var transactions = new DailyTransactions
        {
            Sales = salesTask.Result.ToList(),
            Purchase = purchaseTask.Result.ToList(),
            Adjustment = adjustmentTask.Result.ToList()
        };

        _logger.LogDebug("当日伝票データ取得完了: 売上={SalesCount}, 仕入={PurchaseCount}, 調整={AdjustmentCount}",
            transactions.Sales.Count, transactions.Purchase.Count, transactions.Adjustment.Count);

        return transactions;
    }

    /// <summary>
    /// 在庫計算を実行
    /// </summary>
    /// <param name="previousDayStock">前日在庫</param>
    /// <param name="dailyTransactions">当日伝票データ</param>
    /// <returns>計算後の在庫データ</returns>
    private async Task<Dictionary<InventoryKey, InventoryMaster>> CalculateStockAsync(
        Dictionary<InventoryKey, InventoryMaster> previousDayStock,
        DailyTransactions dailyTransactions)
    {
        _logger.LogDebug("在庫計算開始");

        var calculatedStock = new Dictionary<InventoryKey, InventoryMaster>(previousDayStock);

        // 売上伝票の処理（在庫減少）
        foreach (var sales in dailyTransactions.Sales)
        {
            var key = new InventoryKey(
                sales.ProductCode,
                sales.GradeCode,
                sales.ClassCode,
                sales.ShippingMarkCode,
                sales.ShippingMarkName);

            // 除外対象のデータはスキップ
            if (IsExcludedInventoryKey(key))
                continue;

            if (!calculatedStock.ContainsKey(key))
            {
                // 新規在庫項目の作成
                calculatedStock[key] = CreateNewInventoryMaster(key, sales.JobDate);
            }

            var inventory = calculatedStock[key];
            // 売上による在庫減少
            inventory.CurrentStock -= sales.Quantity;
            inventory.CurrentStockAmount -= sales.Quantity * inventory.AveragePrice;
            inventory.DailyStock -= sales.Quantity;
            inventory.DailyStockAmount -= sales.Quantity * inventory.AveragePrice;
            inventory.UpdatedAt = DateTime.Now;
        }

        // 仕入伝票の処理（在庫増加）
        foreach (var purchase in dailyTransactions.Purchase)
        {
            var key = new InventoryKey(
                purchase.ProductCode,
                purchase.GradeCode,
                purchase.ClassCode,
                purchase.ShippingMarkCode,
                purchase.ShippingMarkName);

            // 除外対象のデータはスキップ
            if (IsExcludedInventoryKey(key))
                continue;

            if (!calculatedStock.ContainsKey(key))
            {
                // 新規在庫項目の作成
                calculatedStock[key] = CreateNewInventoryMaster(key, purchase.JobDate);
            }

            var inventory = calculatedStock[key];
            
            // 平均価格の計算（移動平均）
            if (purchase.Quantity > 0 && purchase.UnitPrice > 0)
            {
                var totalValue = inventory.CurrentStock * inventory.AveragePrice + 
                                purchase.Quantity * purchase.UnitPrice;
                inventory.AveragePrice = (inventory.CurrentStock + purchase.Quantity) > 0 ? 
                    totalValue / (inventory.CurrentStock + purchase.Quantity) : 0;
            }
            
            // 仕入による在庫増加
            inventory.CurrentStock += purchase.Quantity;
            inventory.CurrentStockAmount += purchase.Quantity * purchase.UnitPrice;
            inventory.DailyStock += purchase.Quantity;
            inventory.DailyStockAmount += purchase.Quantity * purchase.UnitPrice;
            inventory.UpdatedAt = DateTime.Now;
        }

        // 在庫調整の処理
        foreach (var adjustment in dailyTransactions.Adjustment)
        {
            var key = new InventoryKey(
                adjustment.ProductCode,
                adjustment.GradeCode,
                adjustment.ClassCode,
                adjustment.ShippingMarkCode,
                adjustment.ShippingMarkName);

            // 除外対象のデータはスキップ
            if (IsExcludedInventoryKey(key))
                continue;

            if (!calculatedStock.ContainsKey(key))
            {
                // 新規在庫項目の作成
                calculatedStock[key] = CreateNewInventoryMaster(key, adjustment.JobDate);
            }

            var inventory = calculatedStock[key];
            // 在庫調整による変動
            inventory.CurrentStock += adjustment.Quantity;
            inventory.CurrentStockAmount += adjustment.Quantity * inventory.AveragePrice;
            inventory.DailyStock += adjustment.Quantity;
            inventory.DailyStockAmount += adjustment.Quantity * inventory.AveragePrice;
            inventory.DailyAdjustmentAmount += adjustment.Amount;
            inventory.UpdatedAt = DateTime.Now;
        }

        _logger.LogDebug("在庫計算完了: 計算後在庫件数={Count}", calculatedStock.Count);
        return calculatedStock;
    }

    /// <summary>
    /// 新規在庫マスタを作成
    /// </summary>
    /// <param name="key">在庫キー</param>
    /// <param name="jobDate">ジョブ日付</param>
    /// <returns>新規在庫マスタ</returns>
    private InventoryMaster CreateNewInventoryMaster(InventoryKey key, DateTime jobDate)
    {
        return new InventoryMaster
        {
            Key = new InventoryKey(
                key.ProductCode,
                key.GradeCode,
                key.ClassCode,
                key.ShippingMarkCode,
                key.ShippingMarkName),
            ProductName = string.Empty,
            Unit = string.Empty,
            StandardPrice = 0,
            AveragePrice = 0,
            ProductCategory1 = string.Empty,
            ProductCategory2 = string.Empty,
            CurrentStock = 0,
            CurrentStockAmount = 0,
            DailyStock = 0,
            DailyStockAmount = 0,
            PreviousMonthQuantity = 0,
            PreviousMonthAmount = 0,
            DailyAdjustmentAmount = 0,
            DailyGrossProfit = 0,
            DailyProcessingCost = 0,
            FinalGrossProfit = 0,
            JobDate = jobDate,
            ImportType = "CUMULATIVE",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 在庫マスタを更新
    /// </summary>
    /// <param name="jobDate">対象日</param>
    /// <param name="calculatedStock">計算後在庫データ</param>
    /// <returns>更新結果</returns>
    private async Task<InventoryUpdateResult> UpdateInventoryMasterAsync(
        DateTime jobDate, 
        Dictionary<InventoryKey, InventoryMaster> calculatedStock)
    {
        _logger.LogDebug("在庫マスタ更新開始: JobDate={JobDate}", jobDate);

        var result = new InventoryUpdateResult();

        // 既存の在庫マスタを削除
        var deleteCount = await _inventoryRepository.DeleteByJobDateAsync(jobDate);
        result.DeletedCount = deleteCount;

        // 新しい在庫マスタを挿入
        var inventoryList = calculatedStock.Values.ToList();
        var insertCount = await _inventoryRepository.BulkInsertAsync(inventoryList);
        result.InsertedCount = insertCount;

        _logger.LogDebug("在庫マスタ更新完了: 削除={DeletedCount}, 挿入={InsertedCount}",
            result.DeletedCount, result.InsertedCount);

        return result;
    }

    /// <summary>
    /// 0在庫の削除
    /// </summary>
    /// <param name="jobDate">対象日</param>
    /// <returns>削除件数</returns>
    private async Task<int> CleanupZeroStockAsync(DateTime jobDate)
    {
        _logger.LogDebug("0在庫削除開始: JobDate={JobDate}", jobDate);

        var deletedCount = await _inventoryRepository.DeleteZeroStockAsync(jobDate);

        _logger.LogDebug("0在庫削除完了: 削除件数={DeletedCount}", deletedCount);
        return deletedCount;
    }

    /// <summary>
    /// 除外対象のInventoryKeyかどうかを判定
    /// </summary>
    /// <param name="key">InventoryKey</param>
    /// <returns>除外対象の場合true</returns>
    private bool IsExcludedInventoryKey(InventoryKey key)
    {
        // 商品コード「00000」（オール0）は除外
        if (key.ProductCode == "00000")
            return true;

        // 荷印名先頭4文字が「EXIT」「exit」は除外
        var shippingMarkPrefix = key.ShippingMarkName.Length >= 4 
            ? key.ShippingMarkName.Substring(0, 4) 
            : key.ShippingMarkName;
        
        if (shippingMarkPrefix.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
            return true;

        // 特定の荷印コードは除外
        if (key.ShippingMarkCode == "9900" || key.ShippingMarkCode == "9910" || key.ShippingMarkCode == "1353")
            return true;

        return false;
    }
}

/// <summary>
/// 当日の伝票データ
/// </summary>
public class DailyTransactions
{
    public List<SalesVoucher> Sales { get; set; } = new();
    public List<PurchaseVoucher> Purchase { get; set; } = new();
    public List<InventoryAdjustment> Adjustment { get; set; } = new();
}

/// <summary>
/// 在庫最適化結果
/// </summary>
public class InventoryOptimizationResult
{
    public DateTime JobDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? ProcessingTime { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    
    public int PreviousDayStockCount { get; set; }
    public int SalesTransactionCount { get; set; }
    public int PurchaseTransactionCount { get; set; }
    public int AdjustmentTransactionCount { get; set; }
    public int CalculatedStockCount { get; set; }
    public int UpdatedRecordCount { get; set; }
    public int InsertedRecordCount { get; set; }
    public int DeletedRecordCount { get; set; }
    public int CleanedUpRecordCount { get; set; }
}

/// <summary>
/// 在庫更新結果
/// </summary>
public class InventoryUpdateResult
{
    public int UpdatedCount { get; set; }
    public int InsertedCount { get; set; }
    public int DeletedCount { get; set; }
}