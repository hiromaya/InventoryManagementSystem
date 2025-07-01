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
    /// 指定日時以降に変更されたデータ件数を取得
    /// </summary>
    Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter);
}