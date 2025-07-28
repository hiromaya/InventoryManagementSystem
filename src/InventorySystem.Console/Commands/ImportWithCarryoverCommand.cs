using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Core.Factories;
using InventorySystem.Core.Services;
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
    private readonly IDataSetManagementRepository _dataSetRepository;
    private readonly IDataSetManagementFactory _dataSetFactory;
    private readonly ITimeProvider _timeProvider;
    private readonly GrossProfitCalculationService _grossProfitCalculationService;
    private readonly IProductMasterRepository _productMasterRepository;
    private readonly IGradeMasterRepository _gradeMasterRepository;
    private readonly IClassMasterRepository _classMasterRepository;
    private readonly IShippingMarkMasterRepository _shippingMarkMasterRepository;
    private readonly ILogger<ImportWithCarryoverCommand> _logger;

    public ImportWithCarryoverCommand(
        IInventoryRepository inventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository adjustmentRepository,
        IDataSetManagementRepository dataSetRepository,
        IDataSetManagementFactory dataSetFactory,
        ITimeProvider timeProvider,
        GrossProfitCalculationService grossProfitCalculationService,
        IProductMasterRepository productMasterRepository,
        IGradeMasterRepository gradeMasterRepository,
        IClassMasterRepository classMasterRepository,
        IShippingMarkMasterRepository shippingMarkMasterRepository,
        ILogger<ImportWithCarryoverCommand> logger)
    {
        _inventoryRepository = inventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _adjustmentRepository = adjustmentRepository;
        _dataSetRepository = dataSetRepository;
        _dataSetFactory = dataSetFactory;
        _timeProvider = timeProvider;
        _grossProfitCalculationService = grossProfitCalculationService;
        _productMasterRepository = productMasterRepository;
        _gradeMasterRepository = gradeMasterRepository;
        _classMasterRepository = classMasterRepository;
        _shippingMarkMasterRepository = shippingMarkMasterRepository;
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
            // ⭐ Phase 2-B: ITimeProvider使用（JST統一）
            var dataSetId = $"CARRYOVER_{targetDate:yyyyMMdd}_{_timeProvider.Now:HHmmss}_{GenerateRandomString(6)}";
            _logger.LogInformation("DataSetId: {DataSetId}", dataSetId);
            
            // 4. 現在の在庫マスタ全データを取得（最新の有効データ）
            List<InventoryMaster> currentInventory;
            
            // 最終処理日が基準日（2025-05-31）の場合、INITデータを検索
            if (lastProcessedDate == new DateTime(2025, 5, 31))
            {
                _logger.LogInformation("初回処理のため、前月末在庫（ImportType='INIT'）を検索します。");
                currentInventory = (await _inventoryRepository.GetByImportTypeAsync("INIT"))
                    .Where(x => x.IsActive)
                    .ToList();
                    
                if (!currentInventory.Any())
                {
                    _logger.LogWarning("前月末在庫が見つかりません。init-inventoryコマンドを先に実行してください。");
                    System.Console.WriteLine("❌ 前月末在庫が見つかりません。init-inventoryコマンドを先に実行してください。");
                    return;
                }
            }
            else
            {
                // 通常処理：全有効在庫データを取得
                currentInventory = await _inventoryRepository.GetAllActiveInventoryAsync();
            }
            
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
            
            // 7. DataSetManagementエンティティを作成
            // ⭐ Phase 2-B: ファクトリパターン使用（Gemini推奨）
            var dataSetManagement = _dataSetFactory.CreateForCarryover(
                dataSetId,
                targetDate,
                department,
                mergedInventory.Count,
                parentDataSetId: currentInventory.FirstOrDefault()?.DataSetId,
                notes: $"前日在庫引継: {currentInventory.Count}件");
            
            // 8. トランザクション内でMERGE処理とDataSetManagement登録を実行
            var affectedRows = await _inventoryRepository.ProcessCarryoverInTransactionAsync(
                mergedInventory, 
                targetDate, 
                dataSetId,
                dataSetManagement
            );
            
            // 9. 最終取引日を更新
            if (salesVouchers.Any())
            {
                try
                {
                    await _inventoryRepository.UpdateLastSalesDateAsync(targetDate);
                    _logger.LogInformation("最終売上日を更新しました: {TargetDate:yyyy-MM-dd}", targetDate);
                }
                catch (Exception updateEx)
                {
                    _logger.LogWarning(updateEx, "最終売上日の更新に失敗しました。処理は継続します。");
                }
            }
            
            if (purchaseVouchers.Any())
            {
                try
                {
                    await _inventoryRepository.UpdateLastPurchaseDateAsync(targetDate);
                    _logger.LogInformation("最終仕入日を更新しました: {TargetDate:yyyy-MM-dd}", targetDate);
                }
                catch (Exception updateEx)
                {
                    _logger.LogWarning(updateEx, "最終仕入日の更新に失敗しました。処理は継続します。");
                }
            }
            
            // 10. Process 2-5の実行（伝票データがある場合のみ）
            var hasVoucherData = salesVouchers.Any() || purchaseVouchers.Any() || adjustmentVouchers.Any();
            if (hasVoucherData)
            {
                _logger.LogInformation("Process 2-5: 売上伝票への在庫単価書込・粗利計算を開始");
                System.Console.WriteLine("\n--- Process 2-5: 売上伝票への在庫単価書込・粗利計算 ---");
                
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await _grossProfitCalculationService.ExecuteProcess25Async(targetDate, dataSetId);
                    stopwatch.Stop();
                    
                    System.Console.WriteLine($"✅ Process 2-5完了 ({stopwatch.ElapsedMilliseconds}ms)");
                    _logger.LogInformation("Process 2-5が完了しました: 処理時間={ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Process 2-5でエラーが発生しました");
                    System.Console.WriteLine($"❌ Process 2-5エラー: {ex.Message}");
                    // エラーが発生しても処理は継続（在庫引継処理は完了済み）
                }
            }
            else
            {
                _logger.LogInformation("Process 2-5スキップ: 伝票データが0件のため");
                System.Console.WriteLine("⚠️ Process 2-5スキップ: 伝票データが0件");
            }
            
            // 11. 完了メッセージ
            System.Console.WriteLine($"\n===== 在庫引継インポート完了 =====");
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
                UpdatedDate = _timeProvider.Now.DateTime,  // ⭐ Phase 2-B: JST統一（日本ビジネスシステム）
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
                // ☆新規商品の場合、マスタ存在チェックを追加☆
                if (!await IsExistsInAllMastersAsync(sales))
                {
                    _logger.LogWarning(
                        "マスタ未登録のため在庫マスタ作成をスキップ(売上): " +
                        "ProductCode={ProductCode}, GradeCode={GradeCode}, " +
                        "ClassCode={ClassCode}, ShippingMarkCode={ShippingMarkCode}",
                        sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode);
                    continue; // 在庫マスタには登録しない
                }
                
                // マスタに存在する場合のみ登録
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
                // ☆新規商品の場合、マスタ存在チェックを追加☆
                if (!await IsExistsInAllMastersAsync(purchase))
                {
                    _logger.LogWarning(
                        "マスタ未登録のため在庫マスタ作成をスキップ(仕入): " +
                        "ProductCode={ProductCode}, GradeCode={GradeCode}, " +
                        "ClassCode={ClassCode}, ShippingMarkCode={ShippingMarkCode}",
                        purchase.ProductCode, purchase.GradeCode, purchase.ClassCode, purchase.ShippingMarkCode);
                    continue; // 在庫マスタには登録しない
                }
                
                // マスタに存在する場合のみ登録
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
                // ☆新規商品の場合、マスタ存在チェックを追加☆
                if (!await IsExistsInAllMastersAsync(adj))
                {
                    _logger.LogWarning(
                        "マスタ未登録のため在庫マスタ作成をスキップ(在庫調整): " +
                        "ProductCode={ProductCode}, GradeCode={GradeCode}, " +
                        "ClassCode={ClassCode}, ShippingMarkCode={ShippingMarkCode}",
                        adj.ProductCode, adj.GradeCode, adj.ClassCode, adj.ShippingMarkCode);
                    continue; // 在庫マスタには登録しない
                }
                
                // マスタに存在する場合のみ登録
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
            CreatedDate = _timeProvider.Now.DateTime,  // ⭐ Phase 2-B: JST統一（日本ビジネスシステム）
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
            CreatedDate = _timeProvider.Now.DateTime,  // ⭐ Phase 2-B: JST統一（日本ビジネスシステム）
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
            CreatedDate = _timeProvider.Now.DateTime,  // ⭐ Phase 2-B: JST統一（日本ビジネスシステム）
            UpdatedDate = DateTime.Now,
            PreviousMonthQuantity = 0,
            PreviousMonthAmount = 0
        };
    }
    
    /// <summary>
    /// すべてのマスタに存在するかチェック
    /// </summary>
    private async Task<bool> IsExistsInAllMastersAsync(dynamic voucher)
    {
        try
        {
            // 商品マスタチェック
            var product = await _productMasterRepository.GetByCodeAsync(voucher.ProductCode);
            if (product == null)
            {
                _logger.LogDebug("商品マスタ未登録: ProductCode={Code}", voucher.ProductCode);
                return false;
            }
            
            // 等級マスタチェック（コード000は許可）
            if (voucher.GradeCode != "000")
            {
                var grade = await _gradeMasterRepository.GetByCodeAsync(voucher.GradeCode);
                if (grade == null)
                {
                    _logger.LogDebug("等級マスタ未登録: GradeCode={Code}", voucher.GradeCode);
                    return false;
                }
            }
            
            // 階級マスタチェック（コード000は許可）
            if (voucher.ClassCode != "000")
            {
                var classEntity = await _classMasterRepository.GetByCodeAsync(voucher.ClassCode);
                if (classEntity == null)
                {
                    _logger.LogDebug("階級マスタ未登録: ClassCode={Code}", voucher.ClassCode);
                    return false;
                }
            }
            
            // 荷印マスタチェック（コード0000は許可）
            if (voucher.ShippingMarkCode != "0000")
            {
                var shippingMark = await _shippingMarkMasterRepository.GetByCodeAsync(voucher.ShippingMarkCode);
                if (shippingMark == null)
                {
                    _logger.LogDebug("荷印マスタ未登録: ShippingMarkCode={Code}", voucher.ShippingMarkCode);
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "マスタ存在チェック中にエラーが発生しました");
            return false;
        }
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