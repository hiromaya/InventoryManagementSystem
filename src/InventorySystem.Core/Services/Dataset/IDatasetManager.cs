using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Services.Dataset;

/// <summary>
/// データセット管理インターフェース
/// </summary>
public interface IDatasetManager
{
    /// <summary>
    /// データセットIDを生成
    /// </summary>
    /// <param name="jobDate">ジョブ日付</param>
    /// <param name="processType">処理種別</param>
    /// <returns>データセットID</returns>
    string GenerateDatasetId(DateTime jobDate, string processType);
    
    /// <summary>
    /// データセットを登録
    /// </summary>
    /// <param name="dataset">データセット情報</param>
    /// <returns>登録されたデータセット</returns>
    Task<DatasetManagement> RegisterDataset(DatasetManagement dataset);
    
    /// <summary>
    /// 指定処理種別・日付の最新データセットIDを取得
    /// </summary>
    /// <param name="processType">処理種別</param>
    /// <param name="jobDate">ジョブ日付</param>
    /// <returns>データセットID（存在しない場合は空文字）</returns>
    Task<string> GetLatestDatasetId(string processType, DateTime jobDate);
    
    /// <summary>
    /// データセットを取得
    /// </summary>
    /// <param name="datasetId">データセットID</param>
    /// <returns>データセット（存在しない場合はnull）</returns>
    Task<DatasetManagement?> GetDataset(string datasetId);
}