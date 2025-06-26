namespace InventorySystem.Core.Configuration
{
    /// <summary>
    /// ファイル保存設定
    /// </summary>
    public class FileStorageSettings
    {
        /// <summary>
        /// インポートファイルのルートパス
        /// </summary>
        public string ImportRootPath { get; set; } = "D:\\InventoryImport";
        
        /// <summary>
        /// バックアップファイルのルートパス
        /// </summary>
        public string BackupRootPath { get; set; } = "D:\\InventoryBackup";
        
        /// <summary>
        /// 部門コード一覧
        /// </summary>
        public List<string> Departments { get; set; } = new() { "User01" };
        
        /// <summary>
        /// ファイル保持期間設定
        /// </summary>
        public FileRetentionSettings FileRetention { get; set; } = new();
        
        /// <summary>
        /// 開発環境用設定
        /// </summary>
        public bool IsDevEnvironment { get; set; } = false;
        
        /// <summary>
        /// 開発環境でのルートパス
        /// </summary>
        public string DevImportRootPath { get; set; } = "C:\\Temp\\InventoryTest\\Import";
        
        /// <summary>
        /// 開発環境でのバックアップルートパス
        /// </summary>
        public string DevBackupRootPath { get; set; } = "C:\\Temp\\InventoryTest\\Backup";
        
        /// <summary>
        /// 実際のインポートルートパスを取得
        /// </summary>
        public string GetImportRootPath() => IsDevEnvironment ? DevImportRootPath : ImportRootPath;
        
        /// <summary>
        /// 実際のバックアップルートパスを取得
        /// </summary>
        public string GetBackupRootPath() => IsDevEnvironment ? DevBackupRootPath : BackupRootPath;
    }
    
    /// <summary>
    /// ファイル保持期間設定
    /// </summary>
    public class FileRetentionSettings
    {
        /// <summary>
        /// 処理済みファイル保持日数
        /// </summary>
        public int ProcessedDays { get; set; } = 30;
        
        /// <summary>
        /// エラーファイル保持日数
        /// </summary>
        public int ErrorDays { get; set; } = 90;
        
        /// <summary>
        /// 帳票ファイル保持月数
        /// </summary>
        public int ReportMonths { get; set; } = 12;
        
        /// <summary>
        /// 日次バックアップ保持数
        /// </summary>
        public int DailyBackupCount { get; set; } = 7;
        
        /// <summary>
        /// 週次バックアップ保持数
        /// </summary>
        public int WeeklyBackupCount { get; set; } = 4;
        
        /// <summary>
        /// 月次バックアップ保持数
        /// </summary>
        public int MonthlyBackupCount { get; set; } = 12;
    }
    
    /// <summary>
    /// バックアップタイプ
    /// </summary>
    public enum BackupType
    {
        /// <summary>
        /// 日次バックアップ
        /// </summary>
        Daily,
        
        /// <summary>
        /// 週次バックアップ
        /// </summary>
        Weekly,
        
        /// <summary>
        /// 月次バックアップ
        /// </summary>
        Monthly
    }
}