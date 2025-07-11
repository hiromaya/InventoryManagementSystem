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

    public async Task ExecuteAsync(string department)  // 日付引数を削除
    {
        System.Console.WriteLine("===== 在庫引継インポート開始 =====");
        _logger.LogInformation("部門: {Department}", department);

        try
        {
            // 1. 最終処理日を取得
            var lastProcessedDate = await _inventoryRepository.GetMaxJobDateAsync();
            _logger.LogInformation("最終処理日: {Date:yyyy-MM-dd}", lastProcessedDate);
            
            // 2. 処理対象日を決定（最終処理日の翌日）
            var targetDate = lastProcessedDate.AddDays(1);
            _logger.LogInformation("処理対象日: {Date:yyyy-MM-dd}", targetDate);
            
            // 3. DataSetIdを生成
            var dataSetId = $"CARRYOVER_{targetDate:yyyyMMdd}_{DateTime.Now:HHmmss}_{GenerateRandomString(6)}";
            _logger.LogInformation("DataSetId: {DataSetId}", dataSetId);
            
            // 4. 現在の在庫マスタ全データを取得（最新の有効データ）
            var currentInventory = await _inventoryRepository.GetAllActiveInventoryAsync();
            _logger.LogInformation("現在の在庫マスタ: {Count}件", currentInventory.Count);
            
            // 5. 処理対象日の伝票データを取得
            var salesVouchers = (await _salesVoucherRepository.GetByJobDateAsync(targetDate)).ToList();
            var purchaseVouchers = (await _purchaseVoucherRepository.GetByJobDateAsync(targetDate)).ToList();
            var adjustmentVouchers = (await _adjustmentRepository.GetByJobDateAsync(targetDate)).ToList();
            
            _logger.LogInformation("当日伝票数 - 売上: {Sales}件, 仕入: {Purchase}件, 在庫調整: {Adjustment}件",
                salesVouchers.Count, purchaseVouchers.Count, adjustmentVouchers.Count);
            
            // 6. 在庫計算処理
            var mergedInventory = CalculateInventory(
                currentInventory,
                salesVouchers,
                purchaseVouchers,
                adjustmentVouchers,
                targetDate,
                dataSetId
            );
            
            _logger.LogInformation("計算後の在庫: {Count}件", mergedInventory.Count);
            
            // 7. MERGE処理で保存（既存は更新、新規は挿入）
            var affectedRows = await _inventoryRepository.MergeInventoryAsync(mergedInventory, targetDate, dataSetId);
            
            // 8. DataSetManagementに記録
            await _dataSetRepository.CreateAsync(new DatasetManagement
            {
                DatasetId = dataSetId,
                JobDate = targetDate,
                ProcessType = "CARRYOVER",
                ImportType = "CARRYOVER",
                RecordCount = affectedRows,
                IsActive = true,
                CreatedAt = DateTime.Now,
                CreatedBy = "ImportWithCarryover"
            });
            
            // 9. 完了メッセージ
            System.Console.WriteLine($"===== 在庫引継インポート完了 =====");
            System.Console.WriteLine($"処理対象日: {targetDate:yyyy-MM-dd}");
            System.Console.WriteLine($"DataSetId: {dataSetId}");
            System.Console.WriteLine($"更新/挿入件数: {affectedRows}件");
            System.Console.WriteLine($"売上伝票: {salesVouchers.Count}件");
            System.Console.WriteLine($"仕入伝票: {purchaseVouchers.Count}件");
            System.Console.WriteLine($"在庫調整: {adjustmentVouchers.Count}件");
            
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
    /// 在庫計算メソッド（新規追加）
    /// </summary>
    private List<InventoryMaster> CalculateInventory(
        List<InventoryMaster> currentInventory,
        List<SalesVoucher> salesVouchers,
        List<PurchaseVoucher> purchaseVouchers,
        List<InventoryAdjustment> adjustmentVouchers,
        DateTime targetDate,
        string dataSetId)
    {
        // 現在在庫を辞書化（5項目キーで管理）
        var inventoryDict = currentInventory.ToDictionary(
            i => $"{i.Key.ProductCode}_{i.Key.GradeCode}_{i.Key.ClassCode}_{i.Key.ShippingMarkCode}_{i.Key.ShippingMarkName}",
            i => new InventoryMaster
            {
                Key = i.Key,
                ProductName = i.ProductName,
                Unit = i.Unit,
                StandardPrice = i.StandardPrice,
                ProductCategory1 = i.ProductCategory1,
                ProductCategory2 = i.ProductCategory2,
                CurrentStock = i.CurrentStock,
                CurrentStockAmount = i.CurrentStockAmount,
                DailyStock = 0,  // 当日変動をリセット
                DailyStockAmount = 0,
                JobDate = targetDate,
                DataSetId = dataSetId,
                IsActive = true,
                UpdatedDate = DateTime.Now,
                PreviousMonthQuantity = i.PreviousMonthQuantity,
                PreviousMonthAmount = i.PreviousMonthAmount
            }
        );
        
        // 売上伝票の反映（在庫減少）
        foreach (var sales in salesVouchers)
        {
            var key = $"{sales.ProductCode}_{sales.GradeCode}_{sales.ClassCode}_{sales.ShippingMarkCode}_{sales.ShippingMarkName}";
            if (inventoryDict.TryGetValue(key, out var inv))
            {
                inv.DailyStock -= sales.Quantity;
                inv.CurrentStock -= sales.Quantity;
                inv.DailyStockAmount -= sales.Amount;
                inv.CurrentStockAmount -= sales.Amount;
            }
            else
            {
                // 新規商品（在庫なしで売上）
                inventoryDict[key] = CreateNewInventory(sales, targetDate, dataSetId, -sales.Quantity, -sales.Amount);
            }
        }
        
        // 仕入伝票の反映（在庫増加）
        foreach (var purchase in purchaseVouchers)
        {
            var key = $"{purchase.ProductCode}_{purchase.GradeCode}_{purchase.ClassCode}_{purchase.ShippingMarkCode}_{purchase.ShippingMarkName}";
            if (inventoryDict.TryGetValue(key, out var inv))
            {
                inv.DailyStock += purchase.Quantity;
                inv.CurrentStock += purchase.Quantity;
                inv.DailyStockAmount += purchase.Amount;
                inv.CurrentStockAmount += purchase.Amount;
            }
            else
            {
                // 新規商品
                inventoryDict[key] = CreateNewInventory(purchase, targetDate, dataSetId, purchase.Quantity, purchase.Amount);
            }
        }
        
        // 在庫調整の反映（区分1, 4, 6のみ）
        foreach (var adj in adjustmentVouchers.Where(a => a.CategoryCode == 1 || a.CategoryCode == 4 || a.CategoryCode == 6))
        {
            var key = $"{adj.ProductCode}_{adj.GradeCode}_{adj.ClassCode}_{adj.ShippingMarkCode}_{adj.ShippingMarkName}";
            if (inventoryDict.TryGetValue(key, out var inv))
            {
                inv.DailyStock += adj.Quantity;
                inv.CurrentStock += adj.Quantity;
                inv.DailyStockAmount += adj.Amount;
                inv.CurrentStockAmount += adj.Amount;
            }
            else
            {
                // 新規商品
                inventoryDict[key] = CreateNewInventory(adj, targetDate, dataSetId, adj.Quantity, adj.Amount);
            }
        }
        
        return inventoryDict.Values.ToList();
    }
    
    /// <summary>
    /// 新規在庫レコードを作成（売上伝票用）
    /// </summary>
    private InventoryMaster CreateNewInventory(SalesVoucher voucher, DateTime targetDate, string dataSetId, decimal quantity, decimal amount)
    {
        return new InventoryMaster
        {
            Key = new InventoryKey
            {
                ProductCode = voucher.ProductCode,
                GradeCode = voucher.GradeCode,
                ClassCode = voucher.ClassCode,
                ShippingMarkCode = voucher.ShippingMarkCode,
                ShippingMarkName = voucher.ShippingMarkName
            },
            ProductName = voucher.ProductName ?? "商品名未設定",
            Unit = "PCS",
            StandardPrice = 0,
            ProductCategory1 = "",
            ProductCategory2 = "",
            CurrentStock = quantity,
            CurrentStockAmount = amount,
            DailyStock = quantity,
            DailyStockAmount = amount,
            JobDate = targetDate,
            DataSetId = dataSetId,
            IsActive = true,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now,
            PreviousMonthQuantity = 0,
            PreviousMonthAmount = 0
        };
    }
    
    /// <summary>
    /// 新規在庫レコードを作成（仕入伝票用）
    /// </summary>
    private InventoryMaster CreateNewInventory(PurchaseVoucher voucher, DateTime targetDate, string dataSetId, decimal quantity, decimal amount)
    {
        return new InventoryMaster
        {
            Key = new InventoryKey
            {
                ProductCode = voucher.ProductCode,
                GradeCode = voucher.GradeCode,
                ClassCode = voucher.ClassCode,
                ShippingMarkCode = voucher.ShippingMarkCode,
                ShippingMarkName = voucher.ShippingMarkName
            },
            ProductName = voucher.ProductName ?? "商品名未設定",
            Unit = "PCS",
            StandardPrice = 0,
            ProductCategory1 = "",
            ProductCategory2 = "",
            CurrentStock = quantity,
            CurrentStockAmount = amount,
            DailyStock = quantity,
            DailyStockAmount = amount,
            JobDate = targetDate,
            DataSetId = dataSetId,
            IsActive = true,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now,
            PreviousMonthQuantity = 0,
            PreviousMonthAmount = 0
        };
    }
    
    /// <summary>
    /// 新規在庫レコードを作成（在庫調整用）
    /// </summary>
    private InventoryMaster CreateNewInventory(InventoryAdjustment voucher, DateTime targetDate, string dataSetId, decimal quantity, decimal amount)
    {
        return new InventoryMaster
        {
            Key = new InventoryKey
            {
                ProductCode = voucher.ProductCode,
                GradeCode = voucher.GradeCode,
                ClassCode = voucher.ClassCode,
                ShippingMarkCode = voucher.ShippingMarkCode,
                ShippingMarkName = voucher.ShippingMarkName
            },
            ProductName = voucher.ProductName ?? "商品名未設定",
            Unit = "PCS",
            StandardPrice = 0,
            ProductCategory1 = "",
            ProductCategory2 = "",
            CurrentStock = quantity,
            CurrentStockAmount = amount,
            DailyStock = quantity,
            DailyStockAmount = amount,
            JobDate = targetDate,
            DataSetId = dataSetId,
            IsActive = true,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now,
            PreviousMonthQuantity = 0,
            PreviousMonthAmount = 0
        };
    }
    
    /// <summary>
    /// ランダム文字列生成
    /// </summary>
    private string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
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