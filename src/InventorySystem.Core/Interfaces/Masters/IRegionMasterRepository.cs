using InventorySystem.Core.Entities.Masters;

namespace InventorySystem.Core.Interfaces.Masters;

/// <summary>
/// 産地マスタリポジトリインターフェース
/// </summary>
public interface IRegionMasterRepository
{
    /// <summary>
    /// 産地マスタを取得
    /// </summary>
    Task<RegionMaster?> GetByCodeAsync(string regionCode);

    /// <summary>
    /// 全ての産地マスタを取得
    /// </summary>
    Task<IEnumerable<RegionMaster>> GetAllAsync();

    /// <summary>
    /// 産地マスタを登録または更新
    /// </summary>
    Task<bool> UpsertAsync(RegionMaster region);

    /// <summary>
    /// 産地マスタを一括登録または更新
    /// </summary>
    Task<int> BulkUpsertAsync(IEnumerable<RegionMaster> regions);

    /// <summary>
    /// 産地マスタを削除
    /// </summary>
    Task<bool> DeleteAsync(string regionCode);

    /// <summary>
    /// 産地コードの存在確認
    /// </summary>
    Task<bool> ExistsAsync(string regionCode);

    /// <summary>
    /// 産地マスタ数を取得
    /// </summary>
    Task<int> GetCountAsync();
}