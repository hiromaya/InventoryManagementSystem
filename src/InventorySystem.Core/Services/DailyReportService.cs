using System.Diagnostics;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Base;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Services.DataSet;
using InventorySystem.Core.Services.History;
using InventorySystem.Core.Services.Validation;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Services;

/// <summary>
/// 商品日報サービス
/// </summary>
public class DailyReportService : BatchProcessBase, IDailyReportService
{
    private readonly ICpInventoryRepository _cpInventoryRepository;
    private readonly ISalesVoucherRepository _salesVoucherRepository;
    private readonly IPurchaseVoucherRepository _purchaseVoucherRepository;
    private readonly IInventoryAdjustmentRepository _inventoryAdjustmentRepository;
    private readonly GrossProfitCalculationService _grossProfitCalculationService;
    private readonly IUnmatchCheckValidationService _unmatchCheckValidationService;

    public DailyReportService(
        IDateValidationService dateValidator,
        IDataSetManager dataSetManager,
        IProcessHistoryService historyService,
        ICpInventoryRepository cpInventoryRepository,
        ISalesVoucherRepository salesVoucherRepository,
        IPurchaseVoucherRepository purchaseVoucherRepository,
        IInventoryAdjustmentRepository inventoryAdjustmentRepository,
        GrossProfitCalculationService grossProfitCalculationService,
        IUnmatchCheckValidationService unmatchCheckValidationService,
        ILogger<DailyReportService> logger)
        : base(dateValidator, dataSetManager, historyService, logger)
    {
        _cpInventoryRepository = cpInventoryRepository;
        _salesVoucherRepository = salesVoucherRepository;
        _purchaseVoucherRepository = purchaseVoucherRepository;
        _inventoryAdjustmentRepository = inventoryAdjustmentRepository;
        _grossProfitCalculationService = grossProfitCalculationService;
        _unmatchCheckValidationService = unmatchCheckValidationService;
    }

    public async Task<DailyReportResult> ProcessDailyReportAsync(DateTime reportDate, string? existingDataSetId = null, bool allowDuplicateProcessing = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var isNewDataSet = existingDataSetId == null;
        ProcessContext? context = null;
        
        try
        {
            _logger.LogInformation("商品日報処理開始 - レポート日付: {ReportDate}", reportDate);

            // 既存DataSetIdがある場合は、アンマッチチェック0件必須の検証を実行
            if (!isNewDataSet && !string.IsNullOrEmpty(existingDataSetId))
            {
                _logger.LogInformation("アンマッチチェック検証開始 - DataSetId: {DataSetId}", existingDataSetId);
                var validation = await _unmatchCheckValidationService.ValidateForReportExecutionAsync(
                    existingDataSetId, ReportType.DailyReport);

                if (!validation.CanExecute)
                {
                    _logger.LogError("❌ 商品日報実行不可 - {ErrorMessage}", validation.ErrorMessage);
                    throw new InvalidOperationException($"商品日報を実行できません。{validation.ErrorMessage}");
                }

                _logger.LogInformation("✅ アンマッチチェック検証合格 - 商品日報実行を継続します");
            }
            
            var executedBy = Environment.UserName ?? "System";
            
            if (isNewDataSet)
            {
                // 新規作成時は InitializeProcess を使用してDataSetManagementに登録
                context = await InitializeProcess(reportDate, "DAILY_REPORT", null, executedBy, allowDuplicateProcessing);
                
                _logger.LogInformation("新規データセット作成 - DataSetId: {DataSetId}", context.DataSetId);
                // 1. CP在庫M作成
                _logger.LogInformation("CP在庫マスタ作成開始");
                var createResult = await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(reportDate);
                _logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}", createResult);

                // 2. 当日エリアクリア
                _logger.LogInformation("当日エリアクリア開始");
                await _cpInventoryRepository.ClearDailyAreaAsync();
                _logger.LogInformation("当日エリアクリア完了");

                // 3. 当日データ集計
                _logger.LogInformation("当日データ集計開始");
                var salesResult = await _cpInventoryRepository.AggregateSalesDataAsync(reportDate);
                _logger.LogInformation("売上データ集計完了 - 更新件数: {Count}", salesResult);
                
                var purchaseResult = await _cpInventoryRepository.AggregatePurchaseDataAsync(reportDate);
                _logger.LogInformation("仕入データ集計完了 - 更新件数: {Count}", purchaseResult);
                
                var adjustmentResult = await _cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(reportDate);
                _logger.LogInformation("在庫調整データ集計完了 - 更新件数: {Count}", adjustmentResult);
                
                // 経費項目の計算を追加
                var discountResult = await _cpInventoryRepository.CalculatePurchaseDiscountAsync(reportDate);
                _logger.LogInformation("仕入値引計算完了 - 更新件数: {Count}", discountResult);

                var incentiveResult = await _cpInventoryRepository.CalculateIncentiveAsync(reportDate);
                _logger.LogInformation("奨励金計算完了 - 更新件数: {Count}", incentiveResult);

                var walkingResult = await _cpInventoryRepository.CalculateWalkingAmountAsync(reportDate);
                _logger.LogInformation("歩引き金計算完了 - 更新件数: {Count}", walkingResult);
                
                _logger.LogInformation("当日データ集計完了");

                // 4. 当日在庫計算
                _logger.LogInformation("当日在庫計算開始");
                await _cpInventoryRepository.CalculateDailyStockAsync();
                await _cpInventoryRepository.SetDailyFlagToProcessedAsync();
                _logger.LogInformation("当日在庫計算完了");
                
                // 処理2-4: 在庫単価計算
                _logger.LogInformation("在庫単価計算開始");
                await _cpInventoryRepository.CalculateInventoryUnitPriceAsync();
                _logger.LogInformation("在庫単価計算完了");

                // 処理2-5: Process 2-5（売上伝票への在庫単価書込・粗利計算）の実行確認
                _logger.LogInformation("Process 2-5の実行確認開始");
                
                // Process 2-5が実行済みかチェック
                var salesVouchers = await _salesVoucherRepository.GetByJobDateAsync(reportDate);
                var zeroUnitPriceCount = salesVouchers.Count(sv => sv.InventoryUnitPrice == 0);
                
                if (zeroUnitPriceCount > 0)
                {
                    _logger.LogWarning("在庫単価が未設定の売上伝票が{Count}件あります。Process 2-5を実行します。", zeroUnitPriceCount);
                    
                    // Process 2-5を実行
                    await _grossProfitCalculationService.ExecuteProcess25Async(reportDate, context.DataSetId);
                    
                    _logger.LogInformation("Process 2-5実行完了");
                }
                else
                {
                    _logger.LogInformation("Process 2-5スキップ: 全売上伝票で在庫単価が設定済み");
                }
                
                _logger.LogInformation("Process 2-5の実行確認完了");

                // 月計計算
                _logger.LogInformation("月計計算開始");
                await _cpInventoryRepository.CalculateMonthlyTotalsAsync(reportDate);
                _logger.LogInformation("月計計算完了");
            }
            else
            {
                // 既存データセット使用時は ProcessHistory のみ開始
                _logger.LogInformation("既存のデータセットを使用: {DataSetId}", existingDataSetId);
                context = new ProcessContext
                {
                    JobDate = reportDate,
                    DataSetId = existingDataSetId,
                    ProcessType = "DAILY_REPORT",
                    ExecutedBy = executedBy
                };
                context.ProcessHistory = await _historyService.StartProcess(existingDataSetId, reportDate, "DAILY_REPORT", executedBy);
            }

            var dataSetId = context.DataSetId;

            // 5. 商品日報データ生成
            _logger.LogInformation("商品日報データ生成開始");
            var reportItems = await GetDailyReportDataAsync(reportDate);
            _logger.LogInformation("商品日報データ生成完了 - データ件数: {Count}", reportItems.Count);

            // 6. 集計データ作成
            var subtotals = CreateSubtotals(reportItems);
            var total = CreateTotal(reportItems);

            stopwatch.Stop();
            
            // 処理完了を記録（0件データでも記録）
            var message = reportItems.Count > 0 
                ? $"商品日報処理が正常に完了しました。データ件数: {reportItems.Count}件"
                : $"商品日報処理が完了しました。データが0件でした。";
            
            await FinalizeProcess(context, true, message);
            
            if (reportItems.Count == 0)
            {
                _logger.LogWarning("{ReportDate}の商品日報は0件データですが、ProcessHistoryに記録されました", reportDate);
            }

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
            var dataSetId = context?.DataSetId ?? existingDataSetId ?? "UNKNOWN";
            _logger.LogError(ex, "商品日報処理でエラーが発生しました - データセットID: {DataSetId}", dataSetId);
            
            // 処理失敗を記録
            if (context != null)
            {
                await FinalizeProcess(context, false, ex.Message);
            }
            
            // CP在庫マスタの削除を保留（日次終了処理まで保持）
            // Phase 1改修: 削除タイミングを日次終了処理後に変更
            if (isNewDataSet && !string.IsNullOrEmpty(dataSetId) && dataSetId != "UNKNOWN")
            {
                _logger.LogInformation("CP在庫マスタを保持します（削除は日次終了処理後） - データセットID: {DataSetId}", dataSetId);
            }
            
            /*
            try
            {
                if (isNewDataSet && !string.IsNullOrEmpty(dataSetId) && dataSetId != "UNKNOWN")
                {
                    await _cpInventoryRepository.DeleteAllAsync() // 仮テーブル設計：全削除;
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "CP在庫マスタのクリーンアップに失敗しました - データセットID: {DataSetId}", dataSetId);
            }
            */

            return new DailyReportResult
            {
                Success = false,
                DataSetId = dataSetId,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<List<DailyReportItem>> GetDailyReportDataAsync(DateTime reportDate)
    {
        _logger.LogInformation("商品日報データ取得開始 - レポート日付: {ReportDate}", reportDate);

        var reportItems = new List<DailyReportItem>();
        
        // CP在庫Mから商品ごとに集計してDailyReportItemを作成
        var cpInventories = await _cpInventoryRepository.GetAllAsync(); // 仮テーブル設計：全レコード取得
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
            var firstCp = group.First();
            var item = new DailyReportItem
            {
                ProductCode = group.Key.ProductCode,
                ProductCategory1 = group.Key.ProductCategory1 ?? string.Empty,
                ProductName = firstCp.ProductName ?? group.Key.ProductCode,
                GradeCode = firstCp.Key.GradeCode,
                ClassCode = firstCp.Key.ClassCode,
                ShippingMarkCode = firstCp.Key.ShippingMarkCode,
                ShippingMarkName = firstCp.Key.ShippingMarkName,

                // 日計項目（集計）
                DailySalesQuantity = group.Sum(cp => cp.DailySalesQuantity),
                DailySalesAmount = group.Sum(cp => cp.DailySalesAmount),
                DailyPurchaseDiscount = group.Sum(cp => cp.DailyPurchaseDiscountAmount),
                DailyInventoryAdjustment = group.Sum(cp => cp.DailyInventoryAdjustmentAmount),
                DailyProcessingCost = group.Sum(cp => cp.DailyProcessingAmount),
                DailyTransfer = group.Sum(cp => cp.DailyTransferAmount),
                DailyIncentive = group.Sum(cp => cp.DailyIncentiveAmount),
                DailyGrossProfit1 = group.Sum(cp => cp.DailyGrossProfit),
                DailyDiscountAmount = group.Sum(cp => cp.DailyWalkingAmount),  // 歩引き額: DailyWalkingAmountを参照
                
                // 月計項目（実データを設定）
                MonthlySalesAmount = group.Sum(cp => cp.MonthlySalesAmount + cp.MonthlySalesReturnAmount),
                MonthlyGrossProfit1 = group.Sum(cp => cp.MonthlyGrossProfit),
                MonthlyGrossProfit2 = group.Sum(cp => cp.MonthlyGrossProfit - cp.MonthlyWalkingAmount)
            };

            // ２粗利益計算（１粗利益－歩引額）
            item.DailyGrossProfit2 = item.DailyGrossProfit1 - item.DailyDiscountAmount;

            // 粗利率計算（0除算対策）
            item.DailyGrossProfitRate1 = DailyReportItem.CalculateGrossProfitRate(item.DailyGrossProfit1, item.DailySalesAmount);
            item.DailyGrossProfitRate2 = DailyReportItem.CalculateGrossProfitRate(item.DailyGrossProfit2, item.DailySalesAmount);
            
            // 月計粗利率計算
            item.MonthlyGrossProfitRate1 = DailyReportItem.CalculateGrossProfitRate(item.MonthlyGrossProfit1, item.MonthlySalesAmount);
            item.MonthlyGrossProfitRate2 = DailyReportItem.CalculateGrossProfitRate(item.MonthlyGrossProfit2, item.MonthlySalesAmount);

            // データがある場合は追加（すでにフィルタリング済み）
            reportItems.Add(item);
            
            _logger.LogDebug("商品データ追加: {ProductCode} - 売上: {SalesAmount}円", 
                item.ProductCode, item.DailySalesAmount);
        }

        // ソート：商品分類1 → 商品コード → 荷印コード → 荷印名 → 等級コード → 階級コード
        var sortedItems = reportItems
            .OrderBy(item => item.ProductCategory1)      // 商品分類1（担当者コード）
            .ThenBy(item => item.ProductCode)            // 商品コード
            .ThenBy(item => item.ShippingMarkCode)       // 荷印コード
            .ThenBy(item => item.ShippingMarkName)       // 荷印名
            .ThenBy(item => item.GradeCode)              // 等級コード
            .ThenBy(item => item.ClassCode)              // 階級コード
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