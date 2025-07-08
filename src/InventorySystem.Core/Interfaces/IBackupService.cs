namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// バックアップサービスのインターフェース
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// データベースバックアップを作成する
        /// </summary>
        /// <param name="processType">処理タイプ</param>
        /// <param name="jobDate">処理日付</param>
        /// <returns>バックアップファイルパス</returns>
        Task<string> CreateBackup(string processType, DateTime jobDate);
        
        /// <summary>
        /// データベースバックアップを作成する（BackupType指定版）
        /// </summary>
        /// <param name="jobDate">処理日付</param>
        /// <param name="backupType">バックアップタイプ</param>
        /// <returns>バックアップファイルパス</returns>
        Task<string> CreateBackup(DateTime jobDate, Configuration.BackupType backupType);
        
        /// <summary>
        /// 古いバックアップファイルをクリーンアップする
        /// </summary>
        /// <param name="retentionDays">保持日数</param>
        Task CleanupOldBackups(int retentionDays);
        
        /// <summary>
        /// バックアップからデータベースを復元する
        /// </summary>
        /// <param name="backupPath">バックアップファイルパス</param>
        Task RestoreBackup(string backupPath);
        
        /// <summary>
        /// バックアップの検証を行う
        /// </summary>
        /// <param name="backupPath">バックアップファイルパス</param>
        /// <returns>検証成功の場合true</returns>
        Task<bool> VerifyBackup(string backupPath);
    }
}