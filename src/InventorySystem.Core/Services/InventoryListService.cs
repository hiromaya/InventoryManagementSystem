using System.Diagnostics;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Services;

/// <summary>
/// 在庫表サービス
/// </summary>
public class InventoryListService : IInventoryListService
{
    private readonly ICpInventoryRepository _cpInventoryRepository;
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
    private readonly IUnmatchCheckValidationService _unmatchCheckValidationService;
    private readonly ILogger<InventoryListService> _logger;

    public InventoryListService(
        ICpInventoryRepository cpInventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        IUnmatchCheckValidationService unmatchCheckValidationService,
        ILogger<InventoryListService> logger)
    {
        _cpInventoryRepository = cpInventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _unmatchCheckValidationService = unmatchCheckValidationService;
        _logger = logger;
    }

    public async Task<InventoryListResult> ProcessInventoryListAsync(DateTime reportDate)
    {
        return await ProcessInventoryListAsync(reportDate, null, skipUnmatchCheck: true);
    }

    public async Task<InventoryListResult> ProcessInventoryListAsync(DateTime reportDate, string? dataSetId, bool skipUnmatchCheck = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var finalDataSetId = dataSetId ?? Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogInformation("在庫表処理開始 - レポート日付: {ReportDate}, データセットID: {DataSetId}, アンマッチチェックスキップ: {SkipCheck}", 
                reportDate, finalDataSetId, skipUnmatchCheck);

            // アンマッチチェック検証（DataSetId指定時のみ）
            if (!string.IsNullOrEmpty(dataSetId) && !skipUnmatchCheck)
            {
                _logger.LogInformation("アンマッチチェック検証開始 - DataSetId: {DataSetId}", dataSetId);
                var validation = await _unmatchCheckValidationService.ValidateForReportExecutionAsync(
                    dataSetId, ReportType.InventoryList);

                if (!validation.CanExecute)
                {
                    _logger.LogError("❌ 在庫表実行不可 - {ErrorMessage}", validation.ErrorMessage);
                    throw new InvalidOperationException($"在庫表を実行できません。{validation.ErrorMessage}");
                }

                _logger.LogInformation("✅ アンマッチチェック検証合格 - 在庫表実行を継続します");
            }

            // 1. CP在庫M作成
            _logger.LogInformation("CP在庫マスタ作成開始");
            var createResult = await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(finalDataSetId, reportDate);
            _logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}", createResult);

            // 2. 当日エリアクリア
            _logger.LogInformation("当日エリアクリア開始");
            await _cpInventoryRepository.ClearDailyAreaAsync(finalDataSetId);
            _logger.LogInformation("当日エリアクリア完了");

            // 3. 当日データ集計
            _logger.LogInformation("当日データ集計開始");
            await _cpInventoryRepository.AggregateSalesDataAsync(finalDataSetId, reportDate);
            await _cpInventoryRepository.AggregatePurchaseDataAsync(finalDataSetId, reportDate);
            await _cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(finalDataSetId, reportDate);
            _logger.LogInformation("当日データ集計完了");

            // 4. 当日在庫計算
            _logger.LogInformation("当日在庫計算開始");
            await _cpInventoryRepository.CalculateDailyStockAsync(finalDataSetId);
            await _cpInventoryRepository.SetDailyFlagToProcessedAsync(finalDataSetId);
            _logger.LogInformation("当日在庫計算完了");

            // 5. 担当者別在庫表データ生成
            _logger.LogInformation("担当者別在庫表データ生成開始");
            var staffInventories = await GetInventoryListByStaffAsync(reportDate);
            _logger.LogInformation("担当者別在庫表データ生成完了 - 担当者数: {StaffCount}", staffInventories.Count);

            // 6. 全体合計作成
            var grandTotal = CreateGrandTotal(staffInventories);

            stopwatch.Stop();

            return new InventoryListResult
            {
                Success = true,
                DataSetId = finalDataSetId,
                ProcessedCount = staffInventories.Sum(s => s.Items.Count),
                StaffInventories = staffInventories,
                GrandTotal = grandTotal,
                ProcessingTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "在庫表処理でエラーが発生しました - データセットID: {DataSetId}", finalDataSetId);
            
            // CP在庫マスタの削除を保留（日次終了処理まで保持）
            // Phase 1改修: 削除タイミングを日次終了処理後に変更
            _logger.LogInformation("CP在庫マスタを保持します（削除は日次終了処理後） - データセットID: {DataSetId}", finalDataSetId);
            
            /*
            try
            {
                await _cpInventoryRepository.DeleteByDataSetIdAsync(dataSetId);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "CP在庫マスタのクリーンアップに失敗しました - データセットID: {DataSetId}", dataSetId);
            }
            */

            return new InventoryListResult
            {
                Success = false,
                DataSetId = finalDataSetId,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<List<InventoryListItem>> GetInventoryListDataAsync(DateTime reportDate)
    {
        _logger.LogInformation("在庫表データ取得開始 - レポート日付: {ReportDate}", reportDate);

        var inventoryItems = new List<InventoryListItem>();

        // 一時的なデータセットIDを生成（実際の実装では、ProcessInventoryListAsyncで生成されたIDを使用）
        var tempDataSetId = Guid.NewGuid().ToString();
        
        // 仮実装：CP在庫Mからデータを取得してInventoryListItemに変換
        var cpInventories = await _cpInventoryRepository.GetAllAsync(tempDataSetId);

        foreach (var cpInventory in cpInventories)
        {
            var item = new InventoryListItem
            {
                ProductCode = cpInventory.Key.ProductCode,
                ProductCategory1 = cpInventory.ProductCategory1 ?? string.Empty,
                ProductName = cpInventory.ProductName ?? cpInventory.Key.ProductCode, // 仮実装：商品コード表示
                ShippingMarkCode = cpInventory.Key.ShippingMarkCode,
                ShippingMarkName = cpInventory.Key.ShippingMarkName,
                GradeCode = cpInventory.Key.GradeCode,
                GradeName = cpInventory.Key.GradeCode, // 仮実装：等級コード表示
                ClassCode = cpInventory.Key.ClassCode,
                ClassName = cpInventory.Key.ClassCode, // 仮実装：階級コード表示
                CurrentStockQuantity = cpInventory.DailyStock,
                CurrentStockUnitPrice = cpInventory.DailyUnitPrice,
                CurrentStockAmount = cpInventory.DailyStockAmount,
                PreviousStockQuantity = cpInventory.PreviousDayStock,
                PreviousStockAmount = cpInventory.PreviousDayStockAmount,
                LastReceiptDate = cpInventory.DailyReceiptQuantity > 0 ? cpInventory.JobDate : (DateTime?)null
            };

            // 滞留マーク計算
            item.StagnationMark = InventoryListItem.CalculateStagnationMark(reportDate, item.LastReceiptDate);

            // 印字対象の判定
            if (item.ShouldBePrinted())
            {
                inventoryItems.Add(item);
            }
        }

        // ソート：商品コード → 荷印コード → 荷印名 → 等級コード → 階級コード
        var sortedItems = inventoryItems
            .OrderBy(item => item.ProductCode)
            .ThenBy(item => item.ShippingMarkCode)
            .ThenBy(item => item.ShippingMarkName)
            .ThenBy(item => item.GradeCode)
            .ThenBy(item => item.ClassCode)
            .ToList();

        _logger.LogInformation("在庫表データ取得完了 - 件数: {Count}", sortedItems.Count);
        return sortedItems;
    }

    public async Task<List<InventoryListByStaff>> GetInventoryListByStaffAsync(DateTime reportDate)
    {
        _logger.LogInformation("担当者別在庫表データ取得開始 - レポート日付: {ReportDate}", reportDate);

        var allItems = await GetInventoryListDataAsync(reportDate);

        // 担当者コード（商品分類1）でグループ化
        var groupedByStaff = allItems
            .GroupBy(item => item.ProductCategory1)
            .OrderBy(group => group.Key)
            .ToList();

        var staffInventories = new List<InventoryListByStaff>();

        foreach (var staffGroup in groupedByStaff)
        {
            var staffInventory = new InventoryListByStaff
            {
                StaffCode = staffGroup.Key,
                StaffName = $"担当者{staffGroup.Key}", // 仮実装
                Items = staffGroup.ToList()
            };

            // 商品コード別小計作成
            var productGroups = staffGroup.GroupBy(item => item.ProductCode);
            foreach (var productGroup in productGroups)
            {
                var subtotal = new InventoryListSubtotal
                {
                    ProductCode = productGroup.Key,
                    SubtotalQuantity = productGroup.Sum(item => item.CurrentStockQuantity),
                    SubtotalAmount = productGroup.Sum(item => item.CurrentStockAmount)
                };
                staffInventory.Subtotals.Add(subtotal);
            }

            // 担当者合計
            staffInventory.Total = new InventoryListTotal
            {
                GrandTotalQuantity = staffGroup.Sum(item => item.CurrentStockQuantity),
                GrandTotalAmount = staffGroup.Sum(item => item.CurrentStockAmount)
            };

            staffInventories.Add(staffInventory);
        }

        _logger.LogInformation("担当者別在庫表データ取得完了 - 担当者数: {StaffCount}", staffInventories.Count);
        return staffInventories;
    }

    /// <summary>
    /// 全体合計を作成
    /// </summary>
    private InventoryListTotal CreateGrandTotal(List<InventoryListByStaff> staffInventories)
    {
        return new InventoryListTotal
        {
            GrandTotalQuantity = staffInventories.Sum(staff => staff.Total.GrandTotalQuantity),
            GrandTotalAmount = staffInventories.Sum(staff => staff.Total.GrandTotalAmount)
        };
    }
}