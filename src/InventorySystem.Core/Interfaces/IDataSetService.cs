namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// データセット管理の統一インターフェース
    /// </summary>
    public interface IDataSetService
    {
        /// <summary>
        /// データセットを作成する
        /// </summary>
        Task<string> CreateDataSetAsync(
            string name,
            string processType,
            DateTime jobDate,
            string? description = null,
            string? filePath = null);
            
        /// <summary>
        /// ステータスを更新する
        /// </summary>
        Task UpdateStatusAsync(string dataSetId, string status);
        
        /// <summary>
        /// レコード数を更新する
        /// </summary>
        Task UpdateRecordCountAsync(string dataSetId, int recordCount);
        
        /// <summary>
        /// エラーを設定する
        /// </summary>
        Task SetErrorAsync(string dataSetId, string errorMessage);
        
        /// <summary>
        /// データセットが存在するか確認する
        /// </summary>
        Task<bool> ExistsAsync(string dataSetId);
        
        /// <summary>
        /// IDでデータセットを取得する
        /// </summary>
        Task<DataSetInfo?> GetByIdAsync(string dataSetId);
        
        /// <summary>
        /// 更新日時を更新する
        /// </summary>
        Task UpdateTimestampAsync(string dataSetId);
    }
    
    /// <summary>
    /// データセット情報の統一モデル
    /// </summary>
    public class DataSetInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProcessType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime JobDate { get; set; }
        public int RecordCount { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public string? FilePath { get; set; }
        public string? Description { get; set; }
    }
}