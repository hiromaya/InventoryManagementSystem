using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Core.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        ILogger<InventoryService> logger)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _salesVoucherRepository = salesVoucherRepository ?? throw new ArgumentNullException(nameof(salesVoucherRepository));
        _purchaseVoucherRepository = purchaseVoucherRepository ?? throw new ArgumentNullException(nameof(purchaseVoucherRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ProcessDailyInventoryAsync(DateTime jobDate)
    {
        var dataSetId = GenerateDataSetId(jobDate);
        
        try
        {
            _logger.LogInformation("Starting daily inventory processing for {JobDate}. DataSetId: {DataSetId}", jobDate, dataSetId);
            
            // 1. 当日エリアクリア (当日発生フラグ='9')
            var clearedRecords = await ClearDailyAreaAsync(jobDate);
            _logger.LogInformation("Cleared {Count} records for {JobDate}", clearedRecords, jobDate);
            
            // 2. データ集計 (当日発生フラグ='0')
            await ProcessSalesAndPurchaseDataAsync(jobDate, dataSetId);
            
            // 3. 在庫計算
            await CalculateInventoryAsync(jobDate, dataSetId);
            
            // 4. 粗利計算
            var totalGrossProfit = await CalculateGrossProfitAsync(jobDate);
            
            _logger.LogInformation("Completed daily inventory processing for {JobDate}. Total Gross Profit: {TotalGrossProfit}", 
                jobDate, totalGrossProfit);
            
            return dataSetId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing daily inventory for {JobDate}. DataSetId: {DataSetId}", jobDate, dataSetId);
            throw;
        }
    }

    public async Task<decimal> CalculateGrossProfitAsync(DateTime jobDate)
    {
        try
        {
            _logger.LogInformation("Starting gross profit calculation for {JobDate}", jobDate);
            
            // 売上伝票から粗利を集計
            var salesVouchers = await _salesVoucherRepository.GetByJobDateAsync(jobDate);
            var inventories = await _inventoryRepository.GetByJobDateAsync(jobDate);
            
            decimal totalGrossProfit = 0;
            var inventoryDict = inventories.ToDictionary(CreateKeyString, i => i);
            
            foreach (var sales in salesVouchers)
            {
                // アンマッチ・商品勘定でのみ除外チェック
                if (IsExcludedFromCalculation(sales.InventoryKey))
                {
                    _logger.LogDebug("Skipping excluded item: {Key}", sales.InventoryKey);
                    continue;
                }
                
                var keyString = CreateKeyString(sales.InventoryKey, jobDate);
                if (inventoryDict.TryGetValue(keyString, out var inventory))
                {
                    // 第1段階: 売上伝票1行ごとの粗利計算
                    var grossProfit = sales.GrossProfit;
                    
                    inventory.DailyGrossProfit += grossProfit;
                    totalGrossProfit += grossProfit;
                    
                    // 第2段階: 調整後の最終粗利益計算
                    var calculation = new GrossProfitCalculation(
                        inventory.DailyGrossProfit,
                        inventory.DailyAdjustmentAmount,
                        inventory.DailyProcessingCost);
                    
                    inventory.FinalGrossProfit = calculation.FinalGrossProfit;
                    
                    await _inventoryRepository.UpdateAsync(inventory);
                }
            }
            
            _logger.LogInformation("Completed gross profit calculation for {JobDate}. Total: {TotalGrossProfit}", 
                jobDate, totalGrossProfit);
            
            return totalGrossProfit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating gross profit for {JobDate}", jobDate);
            throw;
        }
    }

    public async Task<int> ClearDailyAreaAsync(DateTime jobDate)
    {
        try
        {
            _logger.LogInformation("Clearing daily area for {JobDate}", jobDate);
            
            var result = await _inventoryRepository.ClearDailyFlagAsync(jobDate);
            
            _logger.LogInformation("Cleared {Count} records for {JobDate}", result, jobDate);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing daily area for {JobDate}", jobDate);
            throw;
        }
    }

    public async Task<bool> RollbackProcessingAsync(string dataSetId)
    {
        try
        {
            _logger.LogInformation("Starting rollback for DataSetId: {DataSetId}", dataSetId);
            
            // 実装は簡略化。実際には、データセットIDで結びついたデータを削除する
            // ここではログ出力のみ
            _logger.LogWarning("Rollback requested for DataSetId: {DataSetId}. Manual intervention required.", dataSetId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rollback for DataSetId: {DataSetId}", dataSetId);
            return false;
        }
    }

    private async Task ProcessSalesAndPurchaseDataAsync(DateTime jobDate, string dataSetId)
    {
        _logger.LogInformation("Processing sales and purchase data for {JobDate}", jobDate);
        
        // 売上データの処理
        var salesVouchers = await _salesVoucherRepository.GetByJobDateAsync(jobDate);
        var inventories = await _inventoryRepository.GetByJobDateAsync(jobDate);
        var inventoryDict = inventories.ToDictionary(CreateKeyString, i => i);
        
        foreach (var sales in salesVouchers)
        {
            var keyString = CreateKeyString(sales.InventoryKey, jobDate);
            if (inventoryDict.TryGetValue(keyString, out var inventory))
            {
                inventory.DailyFlag = '0'; // データありフラグ
                inventory.DataSetId = dataSetId;
                inventory.UpdatedDate = DateTime.Now;
                
                await _inventoryRepository.UpdateAsync(inventory);
            }
        }
        
        // 仕入データの処理
        var purchaseVouchers = await _purchaseVoucherRepository.GetByJobDateAsync(jobDate);
        
        foreach (var purchase in purchaseVouchers)
        {
            var keyString = CreateKeyString(purchase.InventoryKey, jobDate);
            if (inventoryDict.TryGetValue(keyString, out var inventory))
            {
                inventory.DailyFlag = '0'; // データありフラグ
                inventory.DataSetId = dataSetId;
                inventory.UpdatedDate = DateTime.Now;
                
                await _inventoryRepository.UpdateAsync(inventory);
            }
        }
    }

    private async Task CalculateInventoryAsync(DateTime jobDate, string dataSetId)
    {
        _logger.LogInformation("Calculating inventory for {JobDate}", jobDate);
        
        var inventories = await _inventoryRepository.GetByJobDateAsync(jobDate);
        var salesVouchers = await _salesVoucherRepository.GetByJobDateAsync(jobDate);
        var purchaseVouchers = await _purchaseVoucherRepository.GetByJobDateAsync(jobDate);
        
        foreach (var inventory in inventories.Where(i => i.DailyFlag == '0'))
        {
            var keyString = CreateKeyString(inventory);
            
            // 売上数量と金額の集計
            var salesForItem = salesVouchers.Where(s => CreateKeyString(s.InventoryKey, jobDate) == keyString);
            var totalSalesQty = salesForItem.Sum(s => s.Quantity);
            var totalSalesAmount = salesForItem.Sum(s => s.Amount);
            
            // 仕入数量と金額の集計
            var purchasesForItem = purchaseVouchers.Where(p => CreateKeyString(p.InventoryKey, jobDate) == keyString);
            var totalPurchaseQty = purchasesForItem.Sum(p => p.Quantity);
            var totalPurchaseAmount = purchasesForItem.Sum(p => p.Amount);
            
            // 在庫計算 (簡略化した例)
            inventory.DailyStock = inventory.CurrentStock + totalPurchaseQty - totalSalesQty;
            
            // 0除算対策
            if (inventory.DailyStock != 0)
            {
                // 単価計算 (簡略化)
                var totalValue = inventory.CurrentStockAmount + totalPurchaseAmount - totalSalesAmount;
                inventory.DailyStockAmount = totalValue;
            }
            else
            {
                inventory.DailyStockAmount = 0;
            }
            
            inventory.DataSetId = dataSetId;
            inventory.UpdatedDate = DateTime.Now;
            
            await _inventoryRepository.UpdateAsync(inventory);
        }
    }

    private static string GenerateDataSetId(DateTime jobDate)
    {
        return $"INV_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}";
    }

    private static string CreateKeyString(InventoryMaster inventory)
    {
        return CreateKeyString(inventory.Key, inventory.JobDate);
    }

    private static string CreateKeyString(InventoryKey key, DateTime jobDate)
    {
        return $"{key.ProductCode}|{key.GradeCode}|{key.ClassCode}|{key.ShippingMarkCode}|{key.ShippingMarkName}|{jobDate:yyyyMMdd}";
    }

    private static bool IsExcludedFromCalculation(InventoryKey key)
    {
        // アンマッチ・商品勘定でのみ除外
        var markName = key.ShippingMarkName?.ToUpper() ?? string.Empty;
        var markCode = key.ShippingMarkCode ?? string.Empty;
        
        return markName.StartsWith("EXIT") || 
               markCode == "9900" || 
               markCode == "9910" || 
               markCode == "1353";
    }
}