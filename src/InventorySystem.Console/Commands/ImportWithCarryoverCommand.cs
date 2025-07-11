using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Data.Repositories;

namespace InventorySystem.Console.Commands;

/// <summary>
/// 前日在庫を引き継いでインポートするコマンド
/// </summary>
public class ImportWithCarryoverCommand
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly IInventoryAdjustmentRepository _adjustmentRepository;
    private readonly IDatasetManagementRepository _dataSetRepository;
    private readonly ILogger<ImportWithCarryoverCommand> _logger;

    public ImportWithCarryoverCommand(
        IInventoryRepository inventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository adjustmentRepository,
        IDatasetManagementRepository dataSetRepository,
        ILogger<ImportWithCarryoverCommand> logger)
    {
        _inventoryRepository = inventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _adjustmentRepository = adjustmentRepository;
        _dataSetRepository = dataSetRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(string department, DateTime targetDate)
    {
        var dataSetId = DatasetManagement.GenerateDataSetId("CARRYOVER");
        
        _logger.LogInformation("===== 在庫引継インポート開始 =====");
        _logger.LogInformation("部門: {Department}", department);
        _logger.LogInformation("対象日付: {TargetDate:yyyy-MM-dd}", targetDate);
        _logger.LogInformation("DataSetId: {DataSetId}", dataSetId);
        
        try
        {
            // 1. 前日の有効な在庫マスタを取得
            var previousDate = targetDate.AddDays(-1);
            var previousInventory = await _inventoryRepository.GetActiveByJobDateAsync(previousDate);
            
            // 前日在庫が存在しない場合、前月末在庫を探す
            if (!previousInventory.Any())
            {
                _logger.LogInformation("前日の在庫が見つかりません。前月末在庫を検索します。");
                
                var lastMonthEnd = new DateTime(targetDate.Year, targetDate.Month, 1).AddDays(-1);
                var initInventory = await _inventoryRepository.GetActiveInitInventoryAsync(lastMonthEnd);
                
                if (initInventory.Any())
                {
                    previousInventory = initInventory;
                    _logger.LogInformation("前月末在庫 {Count}件を使用します", initInventory.Count);
                }
                else
                {
                    _logger.LogWarning("前月末在庫も見つかりません。新規在庫として処理します。");
                    previousInventory = new List<InventoryMaster>();
                }
            }
            else
            {
                _logger.LogInformation("前日在庫 {Count}件を引き継ぎます", previousInventory.Count);
            }
            
            // 2. 当日の伝票データを取得
            var salesVouchers = await _salesVoucherRepository.GetByJobDateAsync(targetDate);
            var purchaseVouchers = await _purchaseVoucherRepository.GetByJobDateAsync(targetDate);
            var adjustmentVouchers = await _adjustmentRepository.GetByJobDateAsync(targetDate);
            
            _logger.LogInformation("当日伝票数 - 売上: {Sales}件, 仕入: {Purchase}件, 在庫調整: {Adjustment}件",
                salesVouchers.Count(), purchaseVouchers.Count(), adjustmentVouchers.Count());
            
            // 3. 5項目キーを抽出
            var voucherKeys = ExtractInventoryKeys(salesVouchers, purchaseVouchers, adjustmentVouchers);
            _logger.LogInformation("伝票から抽出したキー: {Count}件（重複除去後）", voucherKeys.Count);
            
            // 4. 前日在庫と当日伝票のキーをマージ
            var allKeys = new HashSet<InventoryKey>(new InventoryKeyEqualityComparer());
            
            // 前日在庫のキーを追加
            foreach (var inv in previousInventory)
            {
                allKeys.Add(inv.Key);
            }
            
            // 当日伝票のキーを追加
            allKeys.UnionWith(voucherKeys);
            
            _logger.LogInformation("統合後の総キー数: {Count}件", allKeys.Count);
            
            // 5. 在庫マスタを作成
            var inventoryList = new List<InventoryMaster>();
            var inheritedCount = 0;
            var newCount = 0;
            
            foreach (var key in allKeys)
            {
                var inventory = new InventoryMaster
                {
                    Key = key,
                    JobDate = targetDate,
                    DataSetId = dataSetId,
                    ImportType = "CARRYOVER",
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now,
                    DailyFlag = '9'
                };
                
                // 前日在庫から引き継ぎ
                var prevInv = previousInventory.FirstOrDefault(p => 
                    new InventoryKeyEqualityComparer().Equals(p.Key, key));
                
                if (prevInv != null)
                {
                    inventory.CurrentStock = prevInv.CurrentStock;
                    inventory.CurrentStockAmount = prevInv.CurrentStockAmount;
                    inventory.DailyStock = prevInv.CurrentStock;
                    inventory.DailyStockAmount = prevInv.CurrentStockAmount;
                    inventory.StandardPrice = prevInv.StandardPrice;
                    inventory.ParentDataSetId = prevInv.DataSetId;
                    inventory.ProductName = prevInv.ProductName;
                    inventory.Unit = prevInv.Unit;
                    inventory.StandardPrice = prevInv.StandardPrice;
                    inventory.ProductCategory1 = prevInv.ProductCategory1;
                    inventory.ProductCategory2 = prevInv.ProductCategory2;
                    inventory.PreviousMonthQuantity = prevInv.PreviousMonthQuantity;
                    inventory.PreviousMonthAmount = prevInv.PreviousMonthAmount;
                    inheritedCount++;
                }
                else
                {
                    inventory.CurrentStock = 0;
                    inventory.CurrentStockAmount = 0;
                    inventory.DailyStock = 0;
                    inventory.DailyStockAmount = 0;
                    inventory.StandardPrice = 0;
                    inventory.ProductName = "商品名未設定";
                    inventory.Unit = "PCS";
                    inventory.ProductCategory1 = "";
                    inventory.ProductCategory2 = "";
                    inventory.PreviousMonthQuantity = 0;
                    inventory.PreviousMonthAmount = 0;
                    newCount++;
                }
                
                inventoryList.Add(inventory);
            }
            
            // 6. 既存データを無効化
            await _inventoryRepository.DeactivateByJobDateAsync(targetDate);
            
            // 7. 新規データを保存
            if (inventoryList.Any())
            {
                await _inventoryRepository.BulkInsertAsync(inventoryList);
            }
            
            // 8. DataSet管理テーブルに記録
            await _dataSetRepository.CreateAsync(new DatasetManagement
            {
                DatasetId = dataSetId,
                JobDate = targetDate,
                ProcessType = "CARRYOVER",
                ImportType = "CARRYOVER",
                RecordCount = inventoryList.Count,
                ParentDataSetId = previousInventory.FirstOrDefault()?.DataSetId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                CreatedBy = "System",
                Notes = $"前日在庫引継: {inheritedCount}件, 新規: {newCount}件"
            });
            
            // 9. 結果表示
            System.Console.WriteLine("===== 在庫引継インポート完了 =====");
            System.Console.WriteLine($"対象日付: {targetDate:yyyy-MM-dd}");
            System.Console.WriteLine($"DataSetId: {dataSetId}");
            System.Console.WriteLine($"前日在庫引継: {inheritedCount}件");
            System.Console.WriteLine($"新規作成: {newCount}件");
            System.Console.WriteLine($"合計: {inventoryList.Count}件");
            System.Console.WriteLine($"売上伝票: {salesVouchers.Count()}件");
            System.Console.WriteLine($"仕入伝票: {purchaseVouchers.Count()}件");
            System.Console.WriteLine($"在庫調整: {adjustmentVouchers.Count()}件");
            
            _logger.LogInformation("在庫引継インポートが正常に完了しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫引継インポート中にエラーが発生しました");
            System.Console.WriteLine($"❌ エラー: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 伝票から5項目キーを抽出
    /// </summary>
    private HashSet<InventoryKey> ExtractInventoryKeys(
        IEnumerable<SalesVoucher> salesVouchers,
        IEnumerable<PurchaseVoucher> purchaseVouchers,
        IEnumerable<InventoryAdjustment> adjustmentVouchers)
    {
        var keys = new HashSet<InventoryKey>(new InventoryKeyEqualityComparer());
        
        // 売上伝票
        foreach (var sv in salesVouchers)
        {
            keys.Add(new InventoryKey
            {
                ProductCode = sv.ProductCode,
                GradeCode = sv.GradeCode,
                ClassCode = sv.ClassCode,
                ShippingMarkCode = sv.ShippingMarkCode,
                ShippingMarkName = sv.ShippingMarkName
            });
        }
        
        // 仕入伝票
        foreach (var pv in purchaseVouchers)
        {
            keys.Add(new InventoryKey
            {
                ProductCode = pv.ProductCode,
                GradeCode = pv.GradeCode,
                ClassCode = pv.ClassCode,
                ShippingMarkCode = pv.ShippingMarkCode,
                ShippingMarkName = pv.ShippingMarkName
            });
        }
        
        // 在庫調整
        foreach (var ia in adjustmentVouchers)
        {
            keys.Add(new InventoryKey
            {
                ProductCode = ia.ProductCode,
                GradeCode = ia.GradeCode,
                ClassCode = ia.ClassCode,
                ShippingMarkCode = ia.ShippingMarkCode,
                ShippingMarkName = ia.ShippingMarkName
            });
        }
        
        return keys;
    }
}

/// <summary>
/// InventoryKeyの比較クラス
/// </summary>
public class InventoryKeyEqualityComparer : IEqualityComparer<InventoryKey>
{
    public bool Equals(InventoryKey? x, InventoryKey? y)
    {
        if (x == null || y == null) return false;
        
        return x.ProductCode == y.ProductCode &&
               x.GradeCode == y.GradeCode &&
               x.ClassCode == y.ClassCode &&
               x.ShippingMarkCode == y.ShippingMarkCode &&
               x.ShippingMarkName == y.ShippingMarkName;
    }
    
    public int GetHashCode(InventoryKey obj)
    {
        return HashCode.Combine(
            obj.ProductCode,
            obj.GradeCode,
            obj.ClassCode,
            obj.ShippingMarkCode,
            obj.ShippingMarkName);
    }
}