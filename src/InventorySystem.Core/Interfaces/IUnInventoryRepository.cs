using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// UN在庫マスタ（アンマッチチェック専用）リポジトリインターフェース
    /// </summary>
    public interface IUnInventoryRepository
    {
        /// <summary>
        /// 在庫マスタからUN在庫マスタを作成する
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="targetDate">対象日付（nullの場合は全期間）</param>
        /// <returns>作成件数</returns>
        Task<int> CreateFromInventoryMasterAsync(string dataSetId, DateTime? targetDate = null);

        /// <summary>
        /// UN在庫マスタの当日エリアをクリアし、当日発生フラグを'9'にセットする
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>更新件数</returns>
        Task<int> ClearDailyAreaAsync(string dataSetId);

        /// <summary>
        /// 売上データをUN在庫マスタに集計する
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="targetDate">対象日付（nullの場合は全期間）</param>
        /// <returns>更新件数</returns>
        Task<int> AggregateSalesDataAsync(string dataSetId, DateTime? targetDate = null);

        /// <summary>
        /// 仕入データをUN在庫マスタに集計する
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="targetDate">対象日付（nullの場合は全期間）</param>
        /// <returns>更新件数</returns>
        Task<int> AggregatePurchaseDataAsync(string dataSetId, DateTime? targetDate = null);

        /// <summary>
        /// 在庫調整データをUN在庫マスタに集計する
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="targetDate">対象日付（nullの場合は全期間）</param>
        /// <returns>更新件数</returns>
        Task<int> AggregateInventoryAdjustmentDataAsync(string dataSetId, DateTime? targetDate = null);

        /// <summary>
        /// 当日在庫数量を計算する
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>更新件数</returns>
        Task<int> CalculateDailyStockAsync(string dataSetId);

        /// <summary>
        /// 当日発生フラグを'0'（処理済み）に更新する
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>更新件数</returns>
        Task<int> SetDailyFlagToProcessedAsync(string dataSetId);

        /// <summary>
        /// UN在庫マスタを取得する（キー指定）
        /// </summary>
        /// <param name="key">5項目複合キー</param>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>UN在庫マスタ</returns>
        Task<UnInventoryMaster?> GetByKeyAsync(InventoryKey key, string dataSetId);

        /// <summary>
        /// UN在庫マスタを一括取得する
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>UN在庫マスタ一覧</returns>
        Task<IEnumerable<UnInventoryMaster>> GetAllAsync(string dataSetId);

        /// <summary>
        /// UN在庫マスタを削除する（データセット指定）
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>削除件数</returns>
        Task<int> DeleteByDataSetIdAsync(string dataSetId);

        /// <summary>
        /// UN在庫マスタの件数を取得する
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>件数</returns>
        Task<int> GetCountAsync(string dataSetId);

        /// <summary>
        /// JobDateとDataSetIdでUN在庫マスタを取得
        /// </summary>
        /// <param name="jobDate">対象日付</param>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>UN在庫マスタ一覧</returns>
        Task<IEnumerable<UnInventoryMaster>> GetByJobDateAndDataSetIdAsync(DateTime jobDate, string dataSetId);
    }
}