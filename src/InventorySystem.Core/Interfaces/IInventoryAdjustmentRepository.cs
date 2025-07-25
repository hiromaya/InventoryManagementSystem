using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 在庫調整リポジトリインターフェース
/// </summary>
public interface IInventoryAdjustmentRepository
{
    /// <summary>
    /// 在庫調整データを一括挿入
    /// </summary>
    Task<int> BulkInsertAsync(IEnumerable<InventoryAdjustment> adjustments);

    /// <summary>
    /// データセットIDで在庫調整データを取得
    /// </summary>
    Task<IEnumerable<InventoryAdjustment>> GetByDataSetIdAsync(string dataSetId);

    /// <summary>
    /// ジョブ日付で在庫調整データを取得
    /// </summary>
    Task<IEnumerable<InventoryAdjustment>> GetByJobDateAsync(DateTime jobDate);

    /// <summary>
    /// ジョブ日付でDataSetIdを取得
    /// </summary>
    Task<string?> GetDataSetIdByJobDateAsync(DateTime jobDate);

    /// <summary>
    /// 在庫キーで在庫調整データを取得
    /// </summary>
    Task<IEnumerable<InventoryAdjustment>> GetByInventoryKeyAsync(InventoryKey inventoryKey, DateTime jobDate);

    /// <summary>
    /// 在庫調整データを更新
    /// </summary>
    Task UpdateAsync(InventoryAdjustment adjustment);

    /// <summary>
    /// データセットIDで在庫調整データを削除
    /// </summary>
    Task DeleteByDataSetIdAsync(string dataSetId);

    /// <summary>
    /// 除外フラグを更新
    /// </summary>
    Task UpdateExcludeStatusAsync(string voucherId, int lineNumber, bool isExcluded, string? excludeReason);

    /// <summary>
    /// データセットIDごとの件数を取得
    /// </summary>
    Task<int> GetCountByDataSetIdAsync(string dataSetId);

    /// <summary>
    /// 商品分類1による集計データを取得
    /// </summary>
    Task<IEnumerable<(string ProductCategory1, int Count, decimal TotalAmount)>> GetSummaryByProductCategory1Async(DateTime jobDate);

    /// <summary>
    /// ジョブ日付で在庫調整データを削除
    /// </summary>
    Task<int> DeleteByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// ジョブ日付での件数を取得
    /// </summary>
    Task<int> GetCountAsync(DateTime jobDate);
    
    /// <summary>
    /// ジョブ日付での在庫調整件数を取得（区分1,4,6のみ）
    /// </summary>
    Task<int> GetInventoryAdjustmentCountByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// 指定日時以降に変更されたデータ件数を取得
    /// </summary>
    Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter);
    
    /// <summary>
    /// すべての在庫調整データを取得
    /// </summary>
    Task<IEnumerable<InventoryAdjustment>> GetAllAsync();
    
    /// <summary>
    /// 指定されたDataSetIdの伝票データのIsActiveフラグを更新
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="isActive">アクティブフラグの値</param>
    /// <returns>更新件数</returns>
    Task<int> UpdateIsActiveByDataSetIdAsync(string dataSetId, bool isActive);
    
    /// <summary>
    /// アクティブな伝票のみを取得（IsActive = true）
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>アクティブな在庫調整一覧</returns>
    Task<IEnumerable<InventoryAdjustment>> GetActiveByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// 指定されたJobDateとProcessTypeの伝票データを無効化
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <param name="excludeDataSetId">除外するDataSetId（nullの場合は除外しない）</param>
    /// <returns>無効化件数</returns>
    Task<int> DeactivateByJobDateAsync(DateTime jobDate, string? excludeDataSetId = null);
}