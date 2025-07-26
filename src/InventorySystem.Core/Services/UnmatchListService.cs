using System.Diagnostics;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Services;

public class UnmatchListService : IUnmatchListService
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
    private readonly IUnmatchCheckRepository _unmatchCheckRepository;
    private readonly ILogger<UnmatchListService> _logger;

    public UnmatchListService(
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
        IUnmatchCheckRepository unmatchCheckRepository,
        ILogger<UnmatchListService> logger)
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
        _unmatchCheckRepository = unmatchCheckRepository;
        _logger = logger;
    }

    public async Task<UnmatchListResult> ProcessUnmatchListAsync()
    {
        return await ProcessUnmatchListInternalAsync(null);
    }

    public async Task<UnmatchListResult> ProcessUnmatchListAsync(DateTime targetDate)
    {
        _logger.LogCritical("===== ProcessUnmatchListAsync（外部呼び出し）開始 =====");
        _logger.LogCritical("引数 targetDate: {TargetDate}", targetDate.ToString("yyyy-MM-dd HH:mm:ss"));
        
        var result = await ProcessUnmatchListInternalAsync(targetDate);
        
        _logger.LogCritical("===== ProcessUnmatchListAsync（外部呼び出し）完了 =====");
        
        return result;
    }

    private async Task<UnmatchListResult> ProcessUnmatchListInternalAsync(DateTime? targetDate)
    {
        _logger.LogCritical("===== ProcessUnmatchListInternalAsync 開始 =====");
        _logger.LogCritical("引数 targetDate: {TargetDate}", targetDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "NULL");
        
        var stopwatch = Stopwatch.StartNew();
        var processType = targetDate.HasValue ? $"指定日以前（{targetDate:yyyy-MM-dd}）" : "全期間";
        
        // DataSetIdをメソッドスコープで定義（初期値設定）
        string dataSetId = Guid.NewGuid().ToString();
        
        try
        {
            // 在庫マスタから最新JobDateを取得（表示用）
            var latestJobDate = await _inventoryRepository.GetMaxJobDateAsync();
            
            // 既存の伝票データからDataSetIdを取得（優先順位: 売上→仕入→在庫調整）
            string? existingDataSetId = null;
            if (targetDate.HasValue)
            {
                _logger.LogCritical("既存DataSetId検索開始...");
                
                // 売上伝票から検索
                existingDataSetId = await _salesVoucherRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
                _logger.LogCritical("売上伝票からのDataSetId: {DataSetId}", existingDataSetId ?? "NULL");
                
                // 仕入伝票から検索
                if (string.IsNullOrEmpty(existingDataSetId))
                {
                    existingDataSetId = await _purchaseVoucherRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
                    _logger.LogCritical("仕入伝票からのDataSetId: {DataSetId}", existingDataSetId ?? "NULL");
                }
                
                // 在庫調整から検索
                if (string.IsNullOrEmpty(existingDataSetId))
                {
                    existingDataSetId = await _inventoryAdjustmentRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
                    _logger.LogCritical("在庫調整からのDataSetId: {DataSetId}", existingDataSetId ?? "NULL");
                }
            }
            else
            {
                _logger.LogCritical("targetDateがNULLのため、既存DataSetId検索をスキップ");
            }
            
            // DataSetId決定部分
            var originalDataSetId = dataSetId;
            dataSetId = !string.IsNullOrEmpty(existingDataSetId) 
                ? existingDataSetId 
                : Guid.NewGuid().ToString();
            
            _logger.LogCritical("===== DataSetId決定結果 =====");
            _logger.LogCritical("既存DataSetId: {ExistingDataSetId}", existingDataSetId ?? "NULL");
            _logger.LogCritical("最終DataSetId: {DataSetId}", dataSetId);
            _logger.LogCritical("新規生成: {IsNew}", string.IsNullOrEmpty(existingDataSetId));
            _logger.LogCritical("===============================");
            
            // 既存DataSetIdが見つかった場合は置き換える
            if (!string.IsNullOrEmpty(existingDataSetId))
            {
                _logger.LogInformation("既存のDataSetIdを使用します: {DataSetId}", dataSetId);
            }
            else
            {
                _logger.LogWarning("指定日の既存DataSetIdが見つからないため新規生成したDataSetIdを使用: {DataSetId}", dataSetId);
            }
            
            _logger.LogInformation("アンマッチリスト処理開始 - {ProcessType}, 最新JobDate: {JobDate}, データセットID: {DataSetId}", 
                processType, latestJobDate, dataSetId);

            // 在庫マスタ最適化処理
            _logger.LogInformation("在庫マスタの最適化を開始します（{ProcessType}）", processType);
            await OptimizeInventoryMasterAsync(dataSetId);
            _logger.LogInformation("在庫マスタの最適化が完了しました");

            // CP在庫マスタの削除を保留（日次終了処理まで保持）
            // Phase 1改修: 削除タイミングを日次終了処理後に変更
            _logger.LogInformation("CP在庫マスタを保持します（削除は日次終了処理後） - データセットID: {DataSetId}", dataSetId);
            var deletedCount = 0; // 削除はスキップ
            
            /*
            // 重要: 既存のCP在庫マスタを全件削除
            _logger.LogInformation("既存のCP在庫マスタを全件削除します");
            var deletedCount = await _cpInventoryRepository.DeleteAllAsync();
            _logger.LogInformation("CP在庫マスタから{Count}件のレコードを削除しました", deletedCount);
            */

            // 処理1-1: CP在庫M作成（指定日以前のアクティブな在庫マスタから）
            _logger.LogInformation("CP在庫マスタ作成開始（{ProcessType}） - DataSetId: {DataSetId}", processType, dataSetId);
            var createResult = await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(dataSetId, targetDate);
            _logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}, DataSetId: {DataSetId}", createResult, dataSetId);

            // 前日在庫の引き継ぎ処理は不要（期間対象のため）
            if (targetDate.HasValue)
            {
                _logger.LogInformation("指定日以前対象のため、前日在庫引き継ぎ処理はスキップします");
            }
            else
            {
                _logger.LogInformation("全期間対象のため、前日在庫引き継ぎ処理はスキップします");
            }

            // 処理1-2: 当日エリアクリア
            _logger.LogInformation("当日エリアクリア開始");
            await _cpInventoryRepository.ClearDailyAreaAsync(dataSetId);
            _logger.LogInformation("当日エリアクリア完了");
            
            // CP在庫マスタ作成後、文字化けチェック
            var garbledCount = await _cpInventoryRepository.CountGarbledShippingMarkNamesAsync(dataSetId);
            if (garbledCount > 0)
            {
                _logger.LogWarning("CP在庫マスタに文字化けデータが{Count}件検出されました。修復を試みます。", garbledCount);
                var repairCount = await _cpInventoryRepository.RepairShippingMarkNamesAsync(dataSetId);
                _logger.LogInformation("文字化けデータ{Count}件を修復しました。", repairCount);
            }

            // データ集計と検証
            _logger.LogInformation("{ProcessType}データ集計開始", processType);
            await AggregateDailyDataWithValidationAsync(dataSetId, targetDate);
            _logger.LogInformation("{ProcessType}データ集計完了", processType);
            
            // 月計データ集計はスキップ（期間対象のため）
            _logger.LogInformation("{ProcessType}対象のため、月計データ集計はスキップします", processType);

            // 集計結果の検証
            var aggregationResult = await ValidateAggregationResultAsync(dataSetId);
            _logger.LogInformation("集計結果 - 総数: {TotalCount}, 集計済み: {AggregatedCount}, 未集計: {NotAggregatedCount}, 取引なし: {ZeroTransactionCount}",
                aggregationResult.TotalCount, aggregationResult.AggregatedCount, aggregationResult.NotAggregatedCount, aggregationResult.ZeroTransactionCount);

            if (aggregationResult.NotAggregatedCount > 0)
            {
                _logger.LogWarning("未集計のレコードが{Count}件存在します", aggregationResult.NotAggregatedCount);
            }

            // 処理1-6: アンマッチリスト生成
            _logger.LogInformation("アンマッチリスト生成開始（{ProcessType}） - DataSetId: {DataSetId}", processType, dataSetId);
            var unmatchItems = targetDate.HasValue 
                ? await GenerateUnmatchListAsync(dataSetId, targetDate.Value)
                : await GenerateUnmatchListAsync(dataSetId);
            var unmatchList = unmatchItems.ToList();
            _logger.LogInformation("アンマッチリスト生成完了 - アンマッチ件数: {Count}, DataSetId: {DataSetId}", unmatchList.Count, dataSetId);

            stopwatch.Stop();

            // CP在庫マスタの削除を保留（日次終了処理まで保持）
            // Phase 1改修: 削除タイミングを日次終了処理後に変更
            _logger.LogInformation("CP在庫マスタを保持します（削除は日次終了処理後） - データセットID: {DataSetId}", dataSetId);
            
            /*
            // CP在庫マスタを削除
            try
            {
                await _cpInventoryRepository.DeleteByDataSetIdAsync(dataSetId);
                _logger.LogInformation("CP在庫マスタを削除しました - データセットID: {DataSetId}", dataSetId);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "CP在庫マスタの削除に失敗しました - データセットID: {DataSetId}", dataSetId);
                // 削除に失敗しても処理は成功として扱う
            }
            */

            // 最終確認ログ
            _logger.LogCritical("===== UnmatchListService 最終結果確認 =====");
            _logger.LogCritical("処理完了 - データセットID: {DataSetId}", dataSetId);
            _logger.LogCritical("検出されたアンマッチ項目数: {Count}", unmatchList.Count);
            _logger.LogCritical("処理時間: {ProcessingTime}", stopwatch.Elapsed);
            
            // アンマッチ項目の内訳確認
            if (unmatchList.Count > 0)
            {
                var categoryBreakdown = unmatchList.GroupBy(x => x.Category).ToList();
                _logger.LogCritical("カテゴリ別内訳 (最終確認):");
                foreach (var group in categoryBreakdown)
                {
                    _logger.LogCritical("  {Category}: {Count}件", group.Key, group.Count());
                }
                
                var alertTypeBreakdown = unmatchList.GroupBy(x => x.AlertType).ToList();
                _logger.LogCritical("アラート種別内訳 (最終確認):");
                foreach (var group in alertTypeBreakdown)
                {
                    _logger.LogCritical("  {AlertType}: {Count}件", group.Key, group.Count());
                }
                
                _logger.LogCritical("これらの {Count} 件がFastReportに渡されます", unmatchList.Count);
            }
            else
            {
                _logger.LogCritical("アンマッチ項目は検出されませんでした (0件)");
            }

            var result = new UnmatchListResult
            {
                Success = true,
                DataSetId = dataSetId,
                UnmatchCount = unmatchList.Count,
                UnmatchItems = unmatchList,
                ProcessingTime = stopwatch.Elapsed
            };

            // アンマッチチェック結果を保存
            await SaveUnmatchCheckResultAsync(dataSetId, result);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "アンマッチリスト処理でエラーが発生しました - データセットID: {DataSetId}", dataSetId);
            
            // CP在庫マスタの削除を保留（日次終了処理まで保持）
            // Phase 1改修: 削除タイミングを日次終了処理後に変更
            _logger.LogInformation("CP在庫マスタを保持します（削除は日次終了処理後） - データセットID: {DataSetId}", dataSetId);
            
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

            var errorResult = new UnmatchListResult
            {
                Success = false,
                DataSetId = dataSetId,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };

            // エラー結果も保存
            await SaveUnmatchCheckResultAsync(dataSetId, errorResult);

            return errorResult;
        }
    }

    public async Task<IEnumerable<UnmatchItem>> GenerateUnmatchListAsync(string dataSetId)
    {
        return await GenerateUnmatchListInternalAsync(dataSetId, null);
    }

    public async Task<IEnumerable<UnmatchItem>> GenerateUnmatchListAsync(string dataSetId, DateTime targetDate)
    {
        return await GenerateUnmatchListInternalAsync(dataSetId, targetDate);
    }

    private async Task<IEnumerable<UnmatchItem>> GenerateUnmatchListInternalAsync(string dataSetId, DateTime? targetDate)
    {
        _logger.LogCritical("===== GenerateUnmatchListInternalAsync 開始 =====");
        _logger.LogCritical("DataSetId: {DataSetId}", dataSetId);
        _logger.LogCritical("TargetDate: {TargetDate}", targetDate?.ToString("yyyy-MM-dd") ?? "NULL");
        
        var unmatchItems = new List<UnmatchItem>();
        var processType = targetDate.HasValue ? $"指定日以前（{targetDate:yyyy-MM-dd}）" : "全期間";

        // 売上伝票のアンマッチチェック
        _logger.LogCritical("売上伝票アンマッチチェック開始...");
        var salesUnmatches = await CheckSalesUnmatchAsync(dataSetId, targetDate);
        _logger.LogCritical("売上伝票アンマッチ件数: {Count}", salesUnmatches.Count());
        unmatchItems.AddRange(salesUnmatches);

        // 仕入伝票のアンマッチチェック
        _logger.LogCritical("仕入伝票アンマッチチェック開始...");
        var purchaseUnmatches = await CheckPurchaseUnmatchAsync(dataSetId, targetDate);
        _logger.LogCritical("仕入伝票アンマッチ件数: {Count}", purchaseUnmatches.Count());
        unmatchItems.AddRange(purchaseUnmatches);

        // 在庫調整のアンマッチチェック
        _logger.LogCritical("在庫調整アンマッチチェック開始...");
        var adjustmentUnmatches = await CheckInventoryAdjustmentUnmatchAsync(dataSetId, targetDate);
        _logger.LogCritical("在庫調整アンマッチ件数: {Count}", adjustmentUnmatches.Count());
        unmatchItems.AddRange(adjustmentUnmatches);

        // マスタデータで名前を補完
        var enrichedItems = new List<UnmatchItem>();
        foreach (var item in unmatchItems)
        {
            var enrichedItem = await EnrichWithMasterData(item);
            enrichedItems.Add(enrichedItem);
        }

        _logger.LogCritical("===== GenerateUnmatchListInternalAsync 完了 =====");
        _logger.LogCritical("総アンマッチ件数: {TotalCount}", unmatchItems.Count);
        
        // ソート：商品分類1、商品コード、荷印コード、荷印名、等級コード、階級コード
        return enrichedItems
            .OrderBy(x => x.ProductCategory1)
            .ThenBy(x => x.Key.ProductCode)
            .ThenBy(x => x.Key.ShippingMarkCode)
            .ThenBy(x => x.Key.ShippingMarkName)
            .ThenBy(x => x.Key.GradeCode)
            .ThenBy(x => x.Key.ClassCode);
    }

    private async Task<IEnumerable<UnmatchItem>> CheckSalesUnmatchAsync(string dataSetId, DateTime? targetDate)
    {
        _logger.LogCritical("===== CheckSalesUnmatchAsync 詳細デバッグ開始 =====");
        _logger.LogCritical("引数 - DataSetId: {DataSetId}", dataSetId);
        _logger.LogCritical("引数 - TargetDate: {TargetDate}", targetDate?.ToString("yyyy-MM-dd") ?? "NULL");
        
        var unmatchItems = new List<UnmatchItem>();
        var processType = targetDate.HasValue ? $"指定日以前（{targetDate:yyyy-MM-dd}）" : "全期間";

        // 売上伝票取得（DataSetIdフィルタリング対応）
        IEnumerable<SalesVoucher> salesVouchers;
        
        _logger.LogCritical("条件判定: DataSetId={HasDataSetId}, TargetDate={HasTargetDate}", 
            !string.IsNullOrEmpty(dataSetId), targetDate.HasValue);

        if (!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue)
        {
            _logger.LogCritical("★★★ GetByDataSetIdAsync を実行 ★★★");
            salesVouchers = await _salesVoucherRepository.GetByDataSetIdAsync(dataSetId);
            _logger.LogCritical("GetByDataSetIdAsync 結果: {Count}件", salesVouchers.Count());
            
            // 最初の5件のDataSetIdを表示
            var first5 = salesVouchers.Take(5);
            foreach (var sv in first5)
            {
                _logger.LogCritical("  - VoucherNumber: {VoucherNumber}, DataSetId: {DataSetId}", 
                    sv.VoucherNumber, sv.DataSetId);
            }
        }
        else
        {
            _logger.LogCritical("！！！ GetAllAsync を実行（警告：全件取得） ！！！");
            salesVouchers = await _salesVoucherRepository.GetAllAsync();
            _logger.LogCritical("GetAllAsync 結果: {Count}件", salesVouchers.Count());
        }
        
        // フィルタリング前後の件数
        var salesList = salesVouchers
            .Where(s => s.VoucherType == "51" || s.VoucherType == "52") // 売上伝票
            .Where(s => s.DetailType == "1" || s.DetailType == "2")  // 明細種（売上・返品のみ、単品値引は除外）
            .Where(s => s.Quantity != 0)                                // 数量0以外（共通仕様）
            .Where(s => s.ProductCode != "00000")                       // 商品コード"00000"を除外
            .Where(s => !targetDate.HasValue || s.JobDate <= targetDate.Value) // 指定日以前フィルタ
            .ToList();
        
        _logger.LogCritical("フィルタリング前: {BeforeCount}件", salesVouchers.Count());
        _logger.LogCritical("フィルタリング後: {AfterCount}件", salesList.Count);
        
        // 最初の5件の文字列状態を確認
        foreach (var (sales, index) in salesList.Take(5).Select((s, i) => (s, i)))
        {
            _logger.LogDebug("売上伝票 行{Index}: 得意先名='{CustomerName}', 商品名='{ProductName}', 荷印名='{ShippingMarkName}'", 
                index + 1, sales.CustomerName, sales.ProductName, sales.ShippingMarkName);
        }

        // CP在庫マスタとの照合
        _logger.LogCritical("CP在庫マスタとの照合開始...");
        int checkedCount = 0;
        int notFoundCount = 0;

        foreach (var sales in salesList)
        {
            checkedCount++;
            if (checkedCount % 100 == 0)
            {
                _logger.LogInformation("処理進捗: {Checked}/{Total}", checkedCount, salesList.Count);
            }

            var inventoryKey = new InventoryKey
            {
                ProductCode = sales.ProductCode,
                GradeCode = sales.GradeCode,
                ClassCode = sales.ClassCode,
                ShippingMarkCode = sales.ShippingMarkCode,
                ShippingMarkName = sales.ShippingMarkName
            };

            var cpInventory = await _cpInventoryRepository.GetByKeyAsync(inventoryKey, dataSetId);

            if (cpInventory == null)
            {
                notFoundCount++;
                if (notFoundCount <= 5)  // 最初の5件のみログ出力
                {
                    _logger.LogCritical("在庫マスタ無サンプル: Product={Product}, Grade={Grade}, Class={Class}, Mark={Mark}, Name={Name}",
                        sales.ProductCode, sales.GradeCode, sales.ClassCode, 
                        sales.ShippingMarkCode, sales.ShippingMarkName);
                }
                
                // 在庫マスタ未登録エラー - 商品分類1を取得
                var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
                    sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode);
                
                var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "", productCategory1);
                unmatchItem.AlertType2 = "在庫マスタ無";
                unmatchItems.Add(unmatchItem);
                
                // アンマッチ項目作成時の文字列状態を確認
                _logger.LogDebug("アンマッチ項目作成: 得意先名='{CustomerName}', 商品名='{ProductName}', 荷印名='{ShippingMarkName}', カテゴリ={Category}", 
                    unmatchItem.CustomerName, unmatchItem.ProductName, unmatchItem.Key.ShippingMarkName, unmatchItem.Category);
            }
            // 在庫0チェックを削除：マイナス在庫を許容
        }

        _logger.LogCritical("===== CheckSalesUnmatchAsync 処理結果 =====");
        _logger.LogCritical("処理対象件数: {Total}", salesList.Count);
        _logger.LogCritical("在庫マスタ無件数: {NotFound}", notFoundCount);
        _logger.LogCritical("アンマッチ合計: {Unmatch}", unmatchItems.Count);
        _logger.LogCritical("=========================================");

        return unmatchItems;
    }

    private async Task<IEnumerable<UnmatchItem>> CheckPurchaseUnmatchAsync(string dataSetId, DateTime? targetDate)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 仕入伝票取得（DataSetIdフィルタリング対応）
        var processType = targetDate.HasValue ? $"指定日以前（{targetDate:yyyy-MM-dd}）" : "全期間";
        IEnumerable<PurchaseVoucher> purchaseVouchers;
        if (!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue)
        {
            // 指定日処理：DataSetIdでフィルタリング
            purchaseVouchers = await _purchaseVoucherRepository.GetByDataSetIdAsync(dataSetId);
            _logger.LogInformation("仕入伝票取得（DataSetIdフィルタ）: DataSetId={DataSetId}, 件数={Count}", 
                dataSetId, purchaseVouchers.Count());
        }
        else
        {
            // 全期間処理：従来通り全件取得
            purchaseVouchers = await _purchaseVoucherRepository.GetAllAsync();
            _logger.LogDebug("仕入伝票取得（全件）: 総件数={TotalCount}", purchaseVouchers.Count());
        }
        var purchaseList = purchaseVouchers
            .Where(p => p.VoucherType == "11" || p.VoucherType == "12") // 仕入伝票
            .Where(p => p.DetailType == "1" || p.DetailType == "2")  // 明細種（仕入・返品のみ、単品値引は除外）
            .Where(p => p.Quantity != 0)                                // 数量0以外（共通仕様）
            .Where(p => p.ProductCode != "00000")                       // 商品コード"00000"を除外
            .Where(p => !targetDate.HasValue || p.JobDate <= targetDate.Value) // 指定日以前フィルタ
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
                // 在庫マスタ未登録エラー - 商品分類1を取得
                var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
                    purchase.ProductCode, purchase.GradeCode, purchase.ClassCode, purchase.ShippingMarkCode);
                
                var unmatchItem = UnmatchItem.FromPurchaseVoucher(purchase, "", productCategory1);
                unmatchItem.AlertType2 = "在庫マスタ無";
                unmatchItems.Add(unmatchItem);
            }
            // 在庫0チェックを削除：マイナス在庫を許容
        }

        return unmatchItems;
    }

    private async Task<string> GetProductCategory1FromInventoryMasterAsync(
        string productCode, string gradeCode, string classCode, string shippingMarkCode)
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

        var inventory = await _inventoryRepository.GetLatestByKeyAsync(inventoryKey);
        
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
            sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode);
        task.Wait();
        return task.Result;
    }

    private string GetProductCategory1FromPurchase(PurchaseVoucher purchase)
    {
        // 非同期メソッドを同期的に呼び出す（理想的ではないが、既存のインターフェースを維持するため）
        var task = GetProductCategory1FromInventoryMasterAsync(
            purchase.ProductCode, purchase.GradeCode, purchase.ClassCode, purchase.ShippingMarkCode);
        task.Wait();
        return task.Result;
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

    private async Task<IEnumerable<UnmatchItem>> CheckInventoryAdjustmentUnmatchAsync(string dataSetId, DateTime? targetDate)
    {
        var unmatchItems = new List<UnmatchItem>();

        // 在庫調整伝票取得（DataSetIdフィルタリング対応）
        var processType = targetDate.HasValue ? $"指定日以前（{targetDate:yyyy-MM-dd}）" : "全期間";
        IEnumerable<InventoryAdjustment> adjustments;
        if (!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue)
        {
            // 指定日処理：DataSetIdでフィルタリング
            adjustments = await _inventoryAdjustmentRepository.GetByDataSetIdAsync(dataSetId);
            _logger.LogInformation("在庫調整取得（DataSetIdフィルタ）: DataSetId={DataSetId}, 件数={Count}", 
                dataSetId, adjustments.Count());
        }
        else
        {
            // 全期間処理：従来通り全件取得
            adjustments = await _inventoryAdjustmentRepository.GetAllAsync();
            _logger.LogDebug("在庫調整取得（全件）: 総件数={TotalCount}", adjustments.Count());
        }
        var adjustmentList = adjustments
            .Where(a => a.VoucherType == "71" || a.VoucherType == "72")  // 在庫調整伝票
            .Where(a => a.DetailType == "3" || a.DetailType == "4")      // 明細種（受注伝票代用のため3,4を使用）
            .Where(a => a.Quantity != 0)                                 // 数量0以外（共通仕様）
            .Where(a => a.ProductCode != "00000")                        // 商品コード"00000"を除外
            .Where(a => a.UnitCode != "02" && a.UnitCode != "05")        // 単位コード02（ギフト経費）,05（加工費B）は除外
            .Where(a => !targetDate.HasValue || a.JobDate <= targetDate.Value) // 指定日以前フィルタ
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
                // 在庫マスタ未登録エラー - 商品分類1を取得
                var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
                    adjustment.ProductCode, adjustment.GradeCode, adjustment.ClassCode, 
                    adjustment.ShippingMarkCode);
                
                var unmatchItem = UnmatchItem.FromInventoryAdjustment(adjustment, "", productCategory1);
                unmatchItem.AlertType2 = "在庫マスタ無";
                unmatchItems.Add(unmatchItem);
            }
            // 在庫0チェックを削除：マイナス在庫を許容
        }

        return unmatchItems;
    }

    private string GetProductCategory1FromAdjustment(InventoryAdjustment adjustment)
    {
        // 非同期メソッドを同期的に呼び出す（理想的ではないが、既存のインターフェースを維持するため）
        var task = GetProductCategory1FromInventoryMasterAsync(
            adjustment.ProductCode, adjustment.GradeCode, adjustment.ClassCode, 
            adjustment.ShippingMarkCode);
        task.Wait();
        return task.Result;
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
                
                var inventory = await _inventoryRepository.GetByKeyAsync(inventoryKey, item.JobDate);
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
    
    /// <summary>
    /// 在庫マスタ最適化処理（累積管理対応版）
    /// </summary>
    private async Task OptimizeInventoryMasterAsync(string dataSetId)
    {
        try
        {
            _logger.LogInformation("=== 在庫マスタ最適化処理開始（累積管理版・全期間） ===");
            
            // 最新のJobDateを取得（表示用）
            var latestJobDate = await _inventoryRepository.GetMaxJobDateAsync();
            
            // 全期間の売上・仕入・在庫調整伝票の商品数を確認（分析用のため全件取得を維持）
            var salesProducts = await _salesVoucherRepository.GetAllAsync();
            var purchaseProducts = await _purchaseVoucherRepository.GetAllAsync();
            var adjustmentProducts = await _inventoryAdjustmentRepository.GetAllAsync();
            
            _logger.LogInformation("分析対象データ件数 - 売上: {SalesCount}, 仕入: {PurchaseCount}, 在庫調整: {AdjustmentCount}",
                salesProducts.Count(), purchaseProducts.Count(), adjustmentProducts.Count());
            
            // 5項目での商品種類を正確にカウント
            var salesUniqueProducts = salesProducts
                .Where(s => (s.VoucherType == "51" || s.VoucherType == "52") &&
                           (s.DetailType == "1" || s.DetailType == "2") &&
                           s.Quantity != 0)
                .Select(s => new { s.ProductCode, s.GradeCode, s.ClassCode, s.ShippingMarkCode, s.ShippingMarkName })
                .Distinct()
                .ToList();
            
            var purchaseUniqueProducts = purchaseProducts
                .Where(p => (p.VoucherType == "11" || p.VoucherType == "12") &&
                           (p.DetailType == "1" || p.DetailType == "2") &&
                           p.Quantity != 0)
                .Select(p => new { p.ProductCode, p.GradeCode, p.ClassCode, p.ShippingMarkCode, p.ShippingMarkName })
                .Distinct()
                .ToList();
            
            var adjustmentUniqueProducts = adjustmentProducts
                .Where(a => a.Quantity != 0)
                .Select(a => new { a.ProductCode, a.GradeCode, a.ClassCode, a.ShippingMarkCode, a.ShippingMarkName })
                .Distinct()
                .ToList();
            
            _logger.LogInformation("売上伝票の商品種類: {Count}", salesUniqueProducts.Count);
            _logger.LogInformation("仕入伝票の商品種類: {Count}", purchaseUniqueProducts.Count);
            _logger.LogInformation("在庫調整の商品種類: {Count}", adjustmentUniqueProducts.Count);
            
            // 最初の5件をログ出力して確認
            foreach (var (product, index) in salesUniqueProducts.Take(5).Select((p, i) => (p, i)))
            {
                _logger.LogDebug("売上商品 {Index}: 商品={ProductCode}, 等級={GradeCode}, 階級={ClassCode}, 荷印={ShippingMarkCode}, 荷印名='{ShippingMarkName}'",
                    index + 1, product.ProductCode, product.GradeCode, product.ClassCode, product.ShippingMarkCode, product.ShippingMarkName);
            }
            
            // 累積管理対応：UpdateOrCreateFromVouchersAsyncメソッドを使用
            _logger.LogInformation("在庫マスタの更新または作成を開始します（累積管理対応）");
            int processedCount = 0;
            try
            {
                processedCount = await _inventoryRepository.UpdateOrCreateFromVouchersAsync(latestJobDate, dataSetId);
                _logger.LogInformation("在庫マスタの更新または作成完了: {Count}件", processedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "在庫マスタの更新または作成でエラーが発生しました");
                // エラーが発生しても処理を継続
            }
            
            // 処理後の状態確認（累積管理では全体数を確認）
            var currentInventoryCount = await _inventoryRepository.GetCountByJobDateAsync(latestJobDate);
            _logger.LogInformation("現在の在庫マスタ総件数（JobDate={JobDate}）: {Count}", latestJobDate, currentInventoryCount);
            
            // 結果の検証
            var allUniqueProducts = salesUniqueProducts
                .Union(purchaseUniqueProducts)
                .Union(adjustmentUniqueProducts)
                .Select(p => $"{p.ProductCode}|{p.GradeCode}|{p.ClassCode}|{p.ShippingMarkCode}|{p.ShippingMarkName}")
                .Distinct()
                .Count();
            
            _logger.LogInformation("本日の伝票に含まれる商品種類（重複なし）: {Count}", allUniqueProducts);
            
            if (processedCount < allUniqueProducts * 0.8)
            {
                _logger.LogWarning(
                    "在庫マスタ最適化が不完全な可能性があります。" +
                    "期待値: {Expected}件, 実際: {Actual}件", 
                    allUniqueProducts, processedCount);
            }
            
            _logger.LogInformation("=== 在庫マスタ最適化処理完了（累積管理版） ===");
            _logger.LogInformation("処理件数: {Count}件", processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫マスタ最適化処理で予期しないエラーが発生しました");
            // CP在庫マスタ作成は継続するため、ここでは例外を再スローしない
            _logger.LogWarning("エラーが発生しましたが、処理を継続します");
        }
    }

    /// <summary>
    /// アンマッチチェック結果を保存する
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="result">アンマッチリスト処理結果</param>
    private async Task SaveUnmatchCheckResultAsync(string dataSetId, UnmatchListResult result)
    {
        try
        {
            _logger.LogInformation("アンマッチチェック結果を保存開始 - DataSetId: {DataSetId}, Status: {Success}, Count: {Count}",
                dataSetId, result.Success, result.UnmatchCount);

            var checkResult = UnmatchCheckResult.FromUnmatchListResult(dataSetId, result);
            var saved = await _unmatchCheckRepository.SaveOrUpdateAsync(checkResult);

            if (saved)
            {
                _logger.LogInformation("✅ アンマッチチェック結果を保存しました - DataSetId: {DataSetId}, Status: {Status}, 帳票実行可能: {CanExecute}",
                    dataSetId, checkResult.CheckStatus, checkResult.CanExecuteReport());
                
                if (checkResult.CanExecuteReport())
                {
                    _logger.LogInformation("🎯 アンマッチ0件達成！帳票実行が可能になりました");
                }
                else
                {
                    _logger.LogWarning("⚠️ アンマッチあり（{Count}件）。帳票実行前にデータ修正が必要です", result.UnmatchCount);
                }
            }
            else
            {
                _logger.LogError("❌ アンマッチチェック結果の保存に失敗しました - DataSetId: {DataSetId}", dataSetId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アンマッチチェック結果保存処理でエラーが発生しました - DataSetId: {DataSetId}", dataSetId);
            // 保存に失敗してもメイン処理は継続
        }
    }
    
    /// <summary>
    /// 当日データ集計と検証処理
    /// </summary>
    private async Task AggregateDailyDataWithValidationAsync(string dataSetId, DateTime? targetDate)
    {
        try
        {
            var processType = targetDate.HasValue ? $"指定日以前（{targetDate:yyyy-MM-dd}）" : "全期間";
            
            // 1. 仕入データの集計
            var purchaseCount = await _cpInventoryRepository.AggregatePurchaseDataAsync(dataSetId, targetDate);
            _logger.LogInformation("仕入データを集計しました（{ProcessType}）。更新件数: {Count}件", processType, purchaseCount);
            
            // 2. 売上データの集計
            var salesCount = await _cpInventoryRepository.AggregateSalesDataAsync(dataSetId, targetDate);
            _logger.LogInformation("売上データを集計しました（{ProcessType}）。更新件数: {Count}件", processType, salesCount);
            
            // 3. 在庫調整データの集計
            var adjustmentCount = await _cpInventoryRepository.AggregateInventoryAdjustmentDataAsync(dataSetId, targetDate);
            _logger.LogInformation("在庫調整データを集計しました（{ProcessType}）。更新件数: {Count}件", processType, adjustmentCount);
            
            // 4. 当日在庫計算
            var calculatedCount = await _cpInventoryRepository.CalculateDailyStockAsync(dataSetId);
            _logger.LogInformation("当日在庫を計算しました。更新件数: {Count}件", calculatedCount);
            
            // 5. 当日発生フラグ更新は各集計処理内で実行されるため、ここでは実行しない
            // var flagCount = await _cpInventoryRepository.SetDailyFlagToProcessedAsync(dataSetId);
            // _logger.LogInformation("当日発生フラグを更新しました。更新件数: {Count}件", flagCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "当日データ集計中にエラーが発生しました");
            throw;
        }
    }
    
    /// <summary>
    /// 月計データを集計する
    /// </summary>
    private async Task AggregateMonthlyDataAsync(DateTime jobDate)
    {
        try
        {
            _logger.LogInformation("月計データ集計開始");
            
            // 月初日を計算
            var monthStartDate = new DateTime(jobDate.Year, jobDate.Month, 1);
            
            // 売上月計の集計
            var monthlySalesUpdated = await _cpInventoryRepository.UpdateMonthlySalesAsync(monthStartDate, jobDate);
            _logger.LogInformation("売上月計を集計しました。更新件数: {Count}件", monthlySalesUpdated);
            
            // 仕入月計の集計
            var monthlyPurchaseUpdated = await _cpInventoryRepository.UpdateMonthlyPurchaseAsync(monthStartDate, jobDate);
            _logger.LogInformation("仕入月計を集計しました。更新件数: {Count}件", monthlyPurchaseUpdated);
            
            // 在庫調整月計の集計
            var adjustmentUpdateCount = await _cpInventoryRepository.UpdateMonthlyInventoryAdjustmentAsync(monthStartDate, jobDate);
            _logger.LogInformation("在庫調整月計を集計しました。更新件数: {Count}件", adjustmentUpdateCount);
            
            // 月計粗利益の計算
            var monthlyGrossProfitUpdated = await _cpInventoryRepository.CalculateMonthlyGrossProfitAsync(jobDate);
            _logger.LogInformation("月計粗利益を計算しました。更新件数: {Count}件", monthlyGrossProfitUpdated);
            
            _logger.LogInformation("月計データ集計完了");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "月計データ集計中にエラーが発生しました");
            throw;
        }
    }
    
    /// <summary>
    /// 集計結果の検証
    /// </summary>
    private async Task<AggregationResult> ValidateAggregationResultAsync(string dataSetId)
    {
        try
        {
            return await _cpInventoryRepository.GetAggregationResultAsync(dataSetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "集計結果の検証中にエラーが発生しました");
            throw;
        }
    }
}