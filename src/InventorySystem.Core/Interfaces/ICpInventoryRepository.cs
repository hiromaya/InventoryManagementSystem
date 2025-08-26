using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface ICpInventoryRepository
{
    /// <summary>
    /// 在庫マスタからCP在庫マスタを作成する（処理1-1）
    /// 仮テーブル設計：全削除後に再作成
    /// </summary>
    /// <param name="jobDate">対象日付（nullの場合は全期間）</param>
    Task<int> CreateCpInventoryFromInventoryMasterAsync(DateTime? jobDate);
    
    /// <summary>
    /// CP在庫マスタの当日エリアをクリアし、当日発生フラグを'9'にセットする
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> ClearDailyAreaAsync();
    
    /// <summary>
    /// CP在庫マスタを取得する（キー指定）
    /// 仮テーブル設計：5項目複合キーで検索
    /// </summary>
    Task<CpInventoryMaster?> GetByKeyAsync(InventoryKey key);
    
    /// <summary>
    /// CP在庫マスタを一括取得する
    /// 仮テーブル設計：全レコード取得
    /// </summary>
    Task<IEnumerable<CpInventoryMaster>> GetAllAsync();
    
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
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    /// <param name="jobDate">対象日付（nullの場合は全期間）</param>
    Task<int> AggregateSalesDataAsync(DateTime? jobDate);
    
    /// <summary>
    /// 仕入伝票データをCP在庫マスタに集計する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    /// <param name="jobDate">対象日付（nullの場合は全期間）</param>
    Task<int> AggregatePurchaseDataAsync(DateTime? jobDate);
    
    /// <summary>
    /// 在庫調整データをCP在庫マスタに集計する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    /// <param name="jobDate">対象日付（nullの場合は全期間）</param>
    Task<int> AggregateInventoryAdjustmentDataAsync(DateTime? jobDate);
    
    /// <summary>
    /// 当日在庫数量を計算する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> CalculateDailyStockAsync();
    
    /// <summary>
    /// 当日発生フラグを'0'（処理済み）に更新する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> SetDailyFlagToProcessedAsync();
    
    
    /// <summary>
    /// 文字化けしたManualShippingMarkを修復する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> RepairManualShippingMarksAsync();
    
    /// <summary>
    /// 文字化けしたManualShippingMarkの件数を取得する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> CountGarbledManualShippingMarksAsync();
    
    /// <summary>
    /// CP在庫マスタの全レコードを削除する
    /// </summary>
    Task<int> DeleteAllAsync();
    
    /// <summary>
    /// 集計結果を取得する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<InventorySystem.Core.Models.AggregationResult> GetAggregationResultAsync();
    
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
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> CalculateInventoryUnitPriceAsync();
    
    /// <summary>
    /// 粗利益を計算する（売上伝票1行ごと）
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> CalculateGrossProfitAsync(DateTime jobDate);
    
    /// <summary>
    /// 仕入値引を集計する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> CalculatePurchaseDiscountAsync(DateTime jobDate);
    
    /// <summary>
    /// 奨励金を計算する（仕入先分類1='01'の場合、仕入金額の1%）
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> CalculateIncentiveAsync(DateTime jobDate);
    
    /// <summary>
    /// 歩引き金を計算する（得意先マスタの汎用数値1×売上金額）
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> CalculateWalkingAmountAsync(DateTime jobDate);
    
    /// <summary>
    /// 前日の在庫マスタから前日在庫を引き継ぐ
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    /// <param name="jobDate">処理日</param>
    /// <param name="previousDate">前日</param>
    /// <returns>更新件数</returns>
    Task<int> InheritPreviousDayStockAsync(DateTime jobDate, DateTime previousDate);
    
    /// <summary>
    /// 月計合計を計算する
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    Task<int> CalculateMonthlyTotalsAsync(DateTime jobDate);
    
    /// <summary>
    /// 古いCP在庫マスタデータをクリーンアップする
    /// </summary>
    /// <param name="cutoffDate">削除基準日（この日付より前のデータを削除）</param>
    /// <returns>削除件数</returns>
    Task<int> CleanupOldDataAsync(DateTime cutoffDate);
    
    /// <summary>
    /// Process 2-5: CP在庫マスタの当日粗利益・歩引き金額を更新
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <param name="totalGrossProfit">総粗利益</param>
    /// <param name="totalDiscountAmount">総歩引き金額</param>
    /// <returns>更新件数</returns>
    Task<int> UpdateDailyTotalsAsync(DateTime jobDate, decimal totalGrossProfit, decimal totalDiscountAmount);
    
    /// <summary>
    /// Process 2-5: JobDateでCP在庫マスタを取得
    /// 仮テーブル設計：全レコード対象
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>CP在庫マスタ一覧</returns>
    Task<IEnumerable<CpInventoryMaster>> GetByJobDateAsync(DateTime jobDate);
}