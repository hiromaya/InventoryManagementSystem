using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface ICpInventoryRepository
{
    /// <summary>
    /// 在庫マスタからCP在庫マスタを作成する（処理1-1）
    /// </summary>
    Task<int> CreateCpInventoryFromInventoryMasterAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// CP在庫マスタの当日エリアをクリアし、当日発生フラグを'9'にセットする
    /// </summary>
    Task<int> ClearDailyAreaAsync(string dataSetId);
    
    /// <summary>
    /// CP在庫マスタを取得する（キー指定）
    /// </summary>
    Task<CpInventoryMaster?> GetByKeyAsync(InventoryKey key, string dataSetId);
    
    /// <summary>
    /// CP在庫マスタを一括取得する
    /// </summary>
    Task<IEnumerable<CpInventoryMaster>> GetAllAsync(string dataSetId);
    
    /// <summary>
    /// CP在庫マスタを更新する
    /// </summary>
    Task<int> UpdateAsync(CpInventoryMaster cpInventory);
    
    /// <summary>
    /// CP在庫マスタを一括更新する
    /// </summary>
    Task<int> UpdateBatchAsync(IEnumerable<CpInventoryMaster> cpInventories);
    
    /// <summary>
    /// 売上伝票データをCP在庫マスタに集計する
    /// </summary>
    Task<int> AggregateSalesDataAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 仕入伝票データをCP在庫マスタに集計する
    /// </summary>
    Task<int> AggregatePurchaseDataAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 在庫調整データをCP在庫マスタに集計する
    /// </summary>
    Task<int> AggregateInventoryAdjustmentDataAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 当日在庫数量を計算する
    /// </summary>
    Task<int> CalculateDailyStockAsync(string dataSetId);
    
    /// <summary>
    /// 当日発生フラグを'0'（処理済み）に更新する
    /// </summary>
    Task<int> SetDailyFlagToProcessedAsync(string dataSetId);
    
    /// <summary>
    /// CP在庫マスタを削除する（データセット指定）
    /// </summary>
    Task<int> DeleteByDataSetIdAsync(string dataSetId);
    
    /// <summary>
    /// 文字化けしたShippingMarkNameを修復する
    /// </summary>
    Task<int> RepairShippingMarkNamesAsync(string dataSetId);
    
    /// <summary>
    /// 文字化けしたShippingMarkNameの件数を取得する
    /// </summary>
    Task<int> CountGarbledShippingMarkNamesAsync(string dataSetId);
    
    /// <summary>
    /// CP在庫マスタの全レコードを削除する
    /// </summary>
    Task<int> DeleteAllAsync();
    
    /// <summary>
    /// 集計結果を取得する
    /// </summary>
    Task<InventorySystem.Core.Models.AggregationResult> GetAggregationResultAsync(string dataSetId);
    
    /// <summary>
    /// ジョブ日付での件数を取得
    /// </summary>
    Task<int> GetCountAsync(DateTime jobDate);
    
    /// <summary>
    /// 月計売上を更新する
    /// </summary>
    Task<int> UpdateMonthlySalesAsync(DateTime monthStartDate, DateTime jobDate);
    
    /// <summary>
    /// 月計仕入を更新する
    /// </summary>
    Task<int> UpdateMonthlyPurchaseAsync(DateTime monthStartDate, DateTime jobDate);
    
    /// <summary>
    /// 月計粗利益を計算する
    /// </summary>
    Task<int> CalculateMonthlyGrossProfitAsync(DateTime jobDate);
    
    /// <summary>
    /// 在庫調整月計を更新する
    /// </summary>
    Task<int> UpdateMonthlyInventoryAdjustmentAsync(DateTime monthStartDate, DateTime jobDate);
    
    /// <summary>
    /// 在庫単価を計算する（移動平均法）
    /// </summary>
    Task<int> CalculateInventoryUnitPriceAsync(string dataSetId);
    
    /// <summary>
    /// 粗利益を計算する（売上伝票1行ごと）
    /// </summary>
    Task<int> CalculateGrossProfitAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 仕入値引を集計する
    /// </summary>
    Task<int> CalculatePurchaseDiscountAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 奨励金を計算する（仕入先分類1='01'の場合、仕入金額の1%）
    /// </summary>
    Task<int> CalculateIncentiveAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 歩引き金を計算する（得意先マスタの汎用数値1×売上金額）
    /// </summary>
    Task<int> CalculateWalkingAmountAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 前日の在庫マスタから前日在庫を引き継ぐ
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="jobDate">処理日</param>
    /// <param name="previousDate">前日</param>
    /// <returns>更新件数</returns>
    Task<int> InheritPreviousDayStockAsync(string dataSetId, DateTime jobDate, DateTime previousDate);
    
    /// <summary>
    /// 月計合計を計算する
    /// </summary>
    Task<int> CalculateMonthlyTotalsAsync(string dataSetId, DateTime jobDate);
}