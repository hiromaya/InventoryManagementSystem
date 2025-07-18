using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// データセット管理リポジトリインターフェース
/// </summary>
public interface IDataSetRepository
{
    /// <summary>
    /// データセットを作成
    /// </summary>
    Task<string> CreateAsync(DataSet dataSet);

    /// <summary>
    /// データセットを取得
    /// </summary>
    Task<DataSet?> GetByIdAsync(string id);

    /// <summary>
    /// データセットのステータスを更新
    /// </summary>
    Task UpdateStatusAsync(string id, string status, string? errorMessage = null);

    /// <summary>
    /// データセットの件数を更新
    /// </summary>
    Task UpdateRecordCountAsync(string id, int recordCount);

    /// <summary>
    /// データセットを更新
    /// </summary>
    Task UpdateAsync(DataSet dataSet);

    /// <summary>
    /// 指定した日付のデータセット一覧を取得
    /// </summary>
    Task<IEnumerable<DataSet>> GetByJobDateAsync(DateTime jobDate);

    /// <summary>
    /// 指定したステータスのデータセット一覧を取得
    /// </summary>
    Task<IEnumerable<DataSet>> GetByStatusAsync(string status);

    /// <summary>
    /// データセットを削除
    /// </summary>
    Task DeleteAsync(string id);

    /// <summary>
    /// 処理完了したデータセットの件数を取得
    /// </summary>
    Task<int> GetCompletedCountAsync(DateTime jobDate);
}