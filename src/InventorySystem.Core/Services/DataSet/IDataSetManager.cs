using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Services.DataSet;

/// <summary>
/// データセット管理インターフェース
/// </summary>
public interface IDataSetManager
{
    /// <summary>
    /// データセットIDを生成
    /// </summary>
    /// <param name="jobDate">ジョブ日付</param>
    /// <param name="processType">処理種別</param>
    /// <returns>データセットID</returns>
    string GenerateDataSetId(DateTime jobDate, string processType);
    
    /// <summary>
    /// データセットを登録
    /// </summary>
    /// <param name="dataSet">データセット情報</param>
    /// <returns>登録されたデータセット</returns>
    Task<DataSetManagement> RegisterDataSet(DataSetManagement dataSet);
    
    /// <summary>
    /// 指定処理種別・日付の最新データセットIDを取得
    /// </summary>
    /// <param name="processType">処理種別</param>
    /// <param name="jobDate">ジョブ日付</param>
    /// <returns>データセットID（存在しない場合は空文字）</returns>
    Task<string> GetLatestDataSetId(string processType, DateTime jobDate);
    
    /// <summary>
    /// データセットを取得
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <returns>データセット（存在しない場合はnull）</returns>
    Task<DataSetManagement?> GetDataSet(string dataSetId);
    
    /// <summary>
    /// データセットエンティティを作成（ファクトリパターン移行版）
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="jobDate">ジョブ日付</param>
    /// <param name="processType">処理種別</param>
    /// <param name="importedFiles">インポートファイル一覧</param>
    /// <param name="createdBy">作成者</param>
    /// <param name="department">部門</param>
    /// <returns>作成されたDataSetManagementエンティティ</returns>
    DataSetManagement CreateDataSet(
        string dataSetId,
        DateTime jobDate,
        string processType,
        List<string>? importedFiles = null,
        string createdBy = "System",
        string department = "DeptA");
}