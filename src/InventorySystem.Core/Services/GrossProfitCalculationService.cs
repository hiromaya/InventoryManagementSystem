using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// Process 2-5: 売上伝票への在庫単価書き込みと粗利計算サービス
    /// </summary>
    public class GrossProfitCalculationService
    {
        private readonly ILogger<GrossProfitCalculationService> _logger;
        private readonly ISalesVoucherRepository _salesVoucherRepository;
        private readonly ICpInventoryRepository _cpInventoryRepository;
        private readonly ICustomerMasterRepository _customerMasterRepository;
        private readonly IDataSetIdManager _dataSetIdManager;
        private const int BatchSize = 1000;

        public GrossProfitCalculationService(
            ILogger<GrossProfitCalculationService> logger,
            ISalesVoucherRepository salesVoucherRepository,
            ICpInventoryRepository cpInventoryRepository,
            ICustomerMasterRepository customerMasterRepository,
            IDataSetIdManager dataSetIdManager)
        {
            _logger = logger;
            _salesVoucherRepository = salesVoucherRepository;
            _cpInventoryRepository = cpInventoryRepository;
            _customerMasterRepository = customerMasterRepository;
            _dataSetIdManager = dataSetIdManager;
        }

        /// <summary>
        /// Process 2-5: 売上伝票への在庫単価書き込みと粗利計算
        /// DataSetIdManagerを使用してDataSetIdの一意性を保証
        /// </summary>
        public async Task ExecuteProcess25Async(DateTime jobDate, string dataSetId)
        {
            _logger.LogInformation("Process 2-5 開始: JobDate={JobDate}, DataSetId={DataSetId}", 
                jobDate, dataSetId);

            try
            {
                // DataSetIdManagerから売上伝票の正しいDataSetIdを取得
                var salesVoucherDataSetId = await _dataSetIdManager.GetSalesVoucherDataSetIdAsync(jobDate);
                if (string.IsNullOrEmpty(salesVoucherDataSetId))
                {
                    _logger.LogWarning("売上伝票のDataSetIdが見つかりません: JobDate={JobDate}", jobDate);
                    return;
                }

                // 引数のdataSetIdをそのまま使用（新規作成しない）
                var cpInventoryDataSetId = dataSetId;
                
                _logger.LogInformation("DataSetId解決: 売上伝票={SalesDataSetId}, CP在庫マスタ={CpDataSetId}（引数使用）", 
                    salesVoucherDataSetId, cpInventoryDataSetId);

                // 1. 売上伝票を取得（DataSetIdManagerで解決したIDを使用）
                var allSalesVouchers = await _salesVoucherRepository
                    .GetByJobDateAndDataSetIdAsync(jobDate, salesVoucherDataSetId);
                
                _logger.LogInformation("売上伝票件数: {Count}", allSalesVouchers.Count());

                // 2. CP在庫マスタを取得（DataSetIdManagerで解決したIDを使用）
                var cpInventoryDict = await GetCpInventoryDictionaryAsync(jobDate, cpInventoryDataSetId);
                _logger.LogInformation("CP在庫マスタ件数: {Count}", cpInventoryDict.Count);

                // 3. 得意先マスタを取得（歩引き率用）
                var customerDict = await GetCustomerDictionaryAsync();

                // 4. バッチ処理で売上伝票を更新
                var totalGrossProfit = 0m;
                var totalDiscountAmount = 0m;
                var updatedVouchers = new List<SalesVoucher>();

                foreach (var voucher in allSalesVouchers)
                {
                    // 5項目キーで在庫単価を取得
                    var inventoryKey = CreateInventoryKey(voucher);
                    
                    if (cpInventoryDict.TryGetValue(inventoryKey, out var cpInventory))
                    {
                        // 在庫単価を設定
                        voucher.InventoryUnitPrice = cpInventory.DailyUnitPrice;

                        // 売上単価の確認（0の場合は計算）
                        var salesUnitPrice = voucher.UnitPrice;
                        if (salesUnitPrice == 0 && voucher.Quantity != 0)
                        {
                            salesUnitPrice = Math.Round(voucher.Amount / voucher.Quantity, 4, 
                                MidpointRounding.AwayFromZero);
                            _logger.LogDebug("売上単価を計算: {Price}", salesUnitPrice);
                        }

                        // 粗利益計算
                        var grossProfit = CalculateGrossProfit(
                            salesUnitPrice, 
                            cpInventory.DailyUnitPrice, 
                            voucher.Quantity);

                        // 粗利益を設定
                        voucher.GrossProfit = grossProfit;
                        totalGrossProfit += grossProfit;

                        // 歩引き金計算
                        if (customerDict.TryGetValue(voucher.CustomerCode ?? "", out var customer))
                        {
                            var discountRate = customer.WalkingRate ?? 0; // 歩引き率
                            var discountAmount = Math.Round(voucher.Amount * (discountRate / 100), 2, 
                                MidpointRounding.AwayFromZero);
                            
                            // 歩引き金を設定
                            voucher.WalkingDiscount = discountAmount;
                            totalDiscountAmount += discountAmount;
                        }

                        updatedVouchers.Add(voucher);
                    }
                    else
                    {
                        _logger.LogWarning("CP在庫マスタが見つかりません: {Key}", inventoryKey);
                    }

                    // バッチサイズに達したら更新
                    if (updatedVouchers.Count >= BatchSize)
                    {
                        await UpdateSalesVouchersBatchAsync(updatedVouchers);
                        updatedVouchers.Clear();
                    }
                }

                // 残りの更新
                if (updatedVouchers.Any())
                {
                    await UpdateSalesVouchersBatchAsync(updatedVouchers);
                }

                // 5. CP在庫マスタの粗利益・歩引き金額を更新
                await UpdateCpInventoryTotalsAsync(jobDate, cpInventoryDataSetId, totalGrossProfit, totalDiscountAmount);

                _logger.LogInformation("Process 2-5 完了: 総粗利益={GrossProfit}, 総歩引き金={Discount}", 
                    totalGrossProfit, totalDiscountAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Process 2-5でエラーが発生しました");
                throw;
            }
        }

        /// <summary>
        /// 粗利益計算（0除算対策付き）
        /// </summary>
        private decimal CalculateGrossProfit(decimal salesUnitPrice, decimal inventoryUnitPrice, decimal quantity)
        {
            var unitProfit = salesUnitPrice - inventoryUnitPrice;
            var grossProfit = unitProfit * quantity;
            return Math.Round(grossProfit, 4, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 5項目キーの作成（正規化処理付き）
        /// </summary>
        private string CreateInventoryKey(SalesVoucher voucher)
        {
            return CreateNormalizedKey(
                voucher.ProductCode,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ShippingMarkName);
        }

        /// <summary>
        /// CP在庫マスタ用の5項目キー作成（正規化処理付き）
        /// </summary>
        private string CreateInventoryKey(CpInventoryMaster cpInventory)
        {
            return CreateNormalizedKey(
                cpInventory.Key.ProductCode,
                cpInventory.Key.GradeCode,
                cpInventory.Key.ClassCode,
                cpInventory.Key.ShippingMarkCode,
                cpInventory.Key.ShippingMarkName);
        }

        /// <summary>
        /// 正規化された5項目複合キーを生成
        /// 空白文字やnullを統一的に処理して重複を防ぐ
        /// </summary>
        private string CreateNormalizedKey(string productCode, string gradeCode, string classCode, 
            string shippingMarkCode, string shippingMarkName)
        {
            // null・空白文字の正規化
            var normalizedProductCode = NormalizeKeyPart(productCode);
            var normalizedGradeCode = NormalizeKeyPart(gradeCode);
            var normalizedClassCode = NormalizeKeyPart(classCode);
            var normalizedShippingMarkCode = NormalizeKeyPart(shippingMarkCode);
            var normalizedShippingMarkName = NormalizeKeyPart(shippingMarkName);

            return $"{normalizedProductCode}_{normalizedGradeCode}_{normalizedClassCode}_" +
                   $"{normalizedShippingMarkCode}_{normalizedShippingMarkName}";
        }

        /// <summary>
        /// キー構成要素の正規化（null・空白統一処理）
        /// </summary>
        private string NormalizeKeyPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            return value.Trim();
        }

        /// <summary>
        /// CP在庫マスタを辞書形式で取得（重複排除処理付き）
        /// </summary>
        private async Task<Dictionary<string, CpInventoryMaster>> GetCpInventoryDictionaryAsync(
            DateTime jobDate, string dataSetId)
        {
            var cpInventories = await _cpInventoryRepository.GetByJobDateAndDataSetIdAsync(jobDate, dataSetId);
            
            _logger.LogDebug("CP在庫マスタ取得: {Count}件", cpInventories.Count());
            
            // GroupByで重複キーを排除してからToDictionaryを実行
            var groupedInventories = cpInventories
                .GroupBy(cp => CreateInventoryKey(cp))
                .ToList();
            
            // 重複キーの警告出力
            var duplicateGroups = groupedInventories.Where(g => g.Count() > 1).ToList();
            if (duplicateGroups.Any())
            {
                _logger.LogWarning("CP在庫マスタで重複キーを検出: {Count}グループ", duplicateGroups.Count);
                foreach (var group in duplicateGroups)
                {
                    _logger.LogWarning("重複キー: {Key}, 件数: {Count}", group.Key, group.Count());
                }
            }
            
            // 各グループの最初の要素を辞書に登録
            return groupedInventories.ToDictionary(
                group => group.Key,
                group => group.First());
        }

        /// <summary>
        /// 得意先マスタを辞書形式で取得
        /// </summary>
        private async Task<Dictionary<string, CustomerMaster>> GetCustomerDictionaryAsync()
        {
            var customers = await _customerMasterRepository.GetActiveAsync();
            return customers.ToDictionary(c => c.CustomerCode, c => c);
        }

        /// <summary>
        /// 売上伝票のバッチ更新
        /// </summary>
        private async Task UpdateSalesVouchersBatchAsync(List<SalesVoucher> vouchers)
        {
            // InventoryUnitPrice, GenericNumeric1, GenericNumeric2 を更新
            await _salesVoucherRepository.UpdateInventoryUnitPriceAndGrossProfitBatchAsync(vouchers);
            
            _logger.LogDebug("売上伝票を更新しました: {Count}件", vouchers.Count);
        }

        /// <summary>
        /// CP在庫マスタの粗利益・歩引き金額を更新
        /// </summary>
        private async Task UpdateCpInventoryTotalsAsync(
            DateTime jobDate, string dataSetId, decimal totalGrossProfit, decimal totalDiscountAmount)
        {
            // CP在庫マスタの当日粗利益、当日歩引き金額に集計値を加算
            await _cpInventoryRepository.UpdateDailyTotalsAsync(
                jobDate, dataSetId, totalGrossProfit, totalDiscountAmount);
        }
    }
}