using InventorySystem.Core.Entities.Masters;

namespace InventorySystem.Core.Interfaces.Masters;

/// <summary>
/// 荷印マスタリポジトリインターフェース
/// </summary>
public interface IShippingMarkMasterRepository
{
    /// <summary>
    /// 荷印マスタを取得
    /// </summary>
    Task<ShippingMarkMaster?> GetByCodeAsync(string shippingMarkCode);

    /// <summary>
    /// 全ての荷印マスタを取得
    /// </summary>
    Task<IEnumerable<ShippingMarkMaster>> GetAllAsync();

    /// <summary>
    /// 荷印マスタを登録または更新
    /// </summary>
    Task<bool> UpsertAsync(ShippingMarkMaster shippingMark);

    /// <summary>
    /// 荷印マスタを一括登録または更新
    /// </summary>
    Task<int> BulkUpsertAsync(IEnumerable<ShippingMarkMaster> shippingMarks);

    /// <summary>
    /// 荷印マスタを削除
    /// </summary>
    Task<bool> DeleteAsync(string shippingMarkCode);

    /// <summary>
    /// 荷印コードの存在確認
    /// </summary>
    Task<bool> ExistsAsync(string shippingMarkCode);

    /// <summary>
    /// 荷印マスタ数を取得
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// 荷印コードから荷印名を取得
    /// </summary>
    /// <param name="shippingMarkCode">荷印コード</param>
    /// <returns>荷印名（存在しない場合はnull）</returns>
    Task<string?> GetNameByCodeAsync(string shippingMarkCode);
}