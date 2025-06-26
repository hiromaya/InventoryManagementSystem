using InventorySystem.Core.Configuration;

namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// ファイル管理サービスインターフェース
    /// </summary>
    public interface IFileManagementService
    {
        /// <summary>
        /// ファイルを処理済みフォルダに移動
        /// </summary>
        /// <param name="sourceFile">移動元ファイルパス</param>
        /// <param name="department">部門コード</param>
        /// <returns>移動先ファイルパス</returns>
        Task<string> MoveToProcessedAsync(string sourceFile, string department);
        
        /// <summary>
        /// ファイルをエラーフォルダに移動
        /// </summary>
        /// <param name="sourceFile">移動元ファイルパス</param>
        /// <param name="department">部門コード</param>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>移動先ファイルパス</returns>
        Task<string> MoveToErrorAsync(string sourceFile, string department, string errorMessage);
        
        /// <summary>
        /// インポートフォルダパスを取得
        /// </summary>
        /// <param name="department">部門コード</param>
        /// <returns>インポートフォルダパス</returns>
        string GetImportPath(string department);
        
        /// <summary>
        /// 処理済みフォルダパスを取得
        /// </summary>
        /// <param name="department">部門コード</param>
        /// <returns>処理済みフォルダパス</returns>
        string GetProcessedPath(string department);
        
        /// <summary>
        /// エラーフォルダパスを取得
        /// </summary>
        /// <param name="department">部門コード</param>
        /// <returns>エラーフォルダパス</returns>
        string GetErrorPath(string department);
        
        /// <summary>
        /// 共有フォルダパスを取得
        /// </summary>
        /// <returns>共有フォルダパス</returns>
        string GetSharedPath();
        
        /// <summary>
        /// 帳票フォルダパスを取得
        /// </summary>
        /// <param name="date">対象日付</param>
        /// <returns>帳票フォルダパス</returns>
        string GetReportPath(DateTime date);
        
        /// <summary>
        /// バックアップフォルダパスを取得
        /// </summary>
        /// <param name="backupType">バックアップタイプ</param>
        /// <param name="date">対象日付</param>
        /// <returns>バックアップフォルダパス</returns>
        string GetBackupPath(BackupType backupType, DateTime date);
        
        /// <summary>
        /// ディレクトリ構造を初期化
        /// </summary>
        Task InitializeDirectoryStructureAsync();
        
        /// <summary>
        /// 処理待ちファイル一覧を取得
        /// </summary>
        /// <param name="department">部門コード</param>
        /// <returns>処理待ちファイル一覧</returns>
        Task<List<string>> GetPendingFilesAsync(string department);
        
        /// <summary>
        /// 帳票ファイルを保存
        /// </summary>
        /// <param name="reportData">帳票データ</param>
        /// <param name="reportName">帳票名</param>
        /// <param name="reportDate">帳票対象日</param>
        /// <returns>保存先ファイルパス</returns>
        Task<string> SaveReportAsync(byte[] reportData, string reportName, DateTime reportDate);
        
        /// <summary>
        /// 古いファイルをクリーンアップ
        /// </summary>
        /// <param name="folderType">フォルダタイプ</param>
        /// <param name="retentionDays">保持日数</param>
        Task CleanupOldFilesAsync(FolderType folderType, int retentionDays);
        
        /// <summary>
        /// ディスク容量をチェック
        /// </summary>
        /// <param name="path">チェック対象パス</param>
        /// <param name="requiredSpaceGB">必要な空き容量(GB)</param>
        /// <returns>容量が十分な場合true</returns>
        Task<bool> CheckDiskSpaceAsync(string path, double requiredSpaceGB = 1.0);
        
        /// <summary>
        /// 書き込み権限をチェック
        /// </summary>
        /// <param name="path">チェック対象パス</param>
        /// <returns>書き込み可能な場合true</returns>
        Task<bool> CheckWritePermissionAsync(string path);
        
        /// <summary>
        /// 帳票の出力パスを取得
        /// </summary>
        /// <param name="reportType">帳票タイプ（unmatch_list, daily_report等）</param>
        /// <param name="jobDate">ジョブ日付</param>
        /// <param name="extension">拡張子（pdf, xlsx等）</param>
        /// <returns>完全な出力パス</returns>
        Task<string> GetReportOutputPathAsync(string reportType, DateTime jobDate, string extension);
    }
    
    /// <summary>
    /// フォルダタイプ
    /// </summary>
    public enum FolderType
    {
        /// <summary>
        /// 処理済みフォルダ
        /// </summary>
        Processed,
        
        /// <summary>
        /// エラーフォルダ
        /// </summary>
        Error,
        
        /// <summary>
        /// 帳票フォルダ
        /// </summary>
        Report,
        
        /// <summary>
        /// バックアップフォルダ
        /// </summary>
        Backup
    }
}