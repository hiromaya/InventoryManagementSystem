using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InventorySystem.Core.Configuration;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// ファイル管理サービス
    /// </summary>
    public class FileManagementService : IFileManagementService
    {
        private readonly FileStorageSettings _settings;
        private readonly ILogger<FileManagementService> _logger;

        public FileManagementService(IOptions<FileStorageSettings> settings, ILogger<FileManagementService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// ファイルを処理済みフォルダに移動
        /// </summary>
        public async Task<string> MoveToProcessedAsync(string sourceFile, string department)
        {
            try
            {
                var processedFolder = GetProcessedPath(department);
                var fileName = Path.GetFileName(sourceFile);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var destinationFile = Path.Combine(processedFolder, $"{timestamp}_{fileName}");

                await EnsureDirectoryExistsAsync(processedFolder);
                File.Move(sourceFile, destinationFile);

                _logger.LogInformation("ファイルを処理済みフォルダに移動しました: {Source} -> {Destination}", sourceFile, destinationFile);
                return destinationFile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル移動中にエラーが発生しました: {SourceFile}", sourceFile);
                throw;
            }
        }

        /// <summary>
        /// ファイルをエラーフォルダに移動
        /// </summary>
        public async Task<string> MoveToErrorAsync(string sourceFile, string department, string errorMessage)
        {
            try
            {
                var errorFolder = GetErrorPath(department);
                var fileName = Path.GetFileName(sourceFile);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var destinationFile = Path.Combine(errorFolder, $"{timestamp}_{fileName}");

                await EnsureDirectoryExistsAsync(errorFolder);
                File.Move(sourceFile, destinationFile);

                // エラーファイルと同じ場所にエラー内容を記録
                var errorLogFile = Path.ChangeExtension(destinationFile, ".error.txt");
                await File.WriteAllTextAsync(errorLogFile, $"エラー日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nエラー内容: {errorMessage}");

                _logger.LogInformation("ファイルをエラーフォルダに移動しました: {Source} -> {Destination}", sourceFile, destinationFile);
                return destinationFile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "エラーファイル移動中にエラーが発生しました: {SourceFile}", sourceFile);
                throw;
            }
        }

        /// <summary>
        /// インポートフォルダパスを取得
        /// </summary>
        public string GetImportPath(string department)
        {
            var rootPath = _settings.GetImportRootPath();
            return Path.Combine(rootPath, department, "Import");
        }

        /// <summary>
        /// 処理済みフォルダパスを取得
        /// </summary>
        public string GetProcessedPath(string department)
        {
            var rootPath = _settings.GetImportRootPath();
            return Path.Combine(rootPath, department, "Processed");
        }

        /// <summary>
        /// エラーフォルダパスを取得
        /// </summary>
        public string GetErrorPath(string department)
        {
            var rootPath = _settings.GetImportRootPath();
            return Path.Combine(rootPath, department, "Error");
        }

        /// <summary>
        /// 共有フォルダパスを取得
        /// </summary>
        public string GetSharedPath()
        {
            var rootPath = _settings.GetImportRootPath();
            return Path.Combine(rootPath, "Shared");
        }

        /// <summary>
        /// 帳票フォルダパスを取得
        /// </summary>
        public string GetReportPath(DateTime date)
        {
            var rootPath = _settings.GetReportOutputPath();
            return Path.Combine(rootPath, date.ToString("yyyy"), date.ToString("MM"));
        }

        /// <summary>
        /// バックアップフォルダパスを取得
        /// </summary>
        public string GetBackupPath(BackupType backupType, DateTime date)
        {
            var rootPath = _settings.GetBackupRootPath();
            return backupType switch
            {
                BackupType.Daily => Path.Combine(rootPath, "Daily", date.ToString("yyyy"), date.ToString("MM")),
                BackupType.Weekly => Path.Combine(rootPath, "Weekly", date.Year.ToString()),
                BackupType.Monthly => Path.Combine(rootPath, "Monthly", date.Year.ToString()),
                BackupType.BeforeDailyClose => Path.Combine(rootPath, "DailyClose", date.ToString("yyyy"), date.ToString("MM")),
                _ => throw new ArgumentException($"サポートされていないバックアップタイプ: {backupType}")
            };
        }

        /// <summary>
        /// ディレクトリ構造を初期化
        /// </summary>
        public async Task InitializeDirectoryStructureAsync()
        {
            try
            {
                _logger.LogInformation("ディレクトリ構造の初期化を開始します");

                // 各部門のフォルダ作成
                foreach (var department in _settings.Departments)
                {
                    await EnsureDirectoryExistsAsync(GetImportPath(department));
                    await EnsureDirectoryExistsAsync(GetProcessedPath(department));
                    await EnsureDirectoryExistsAsync(GetErrorPath(department));
                }

                // 共有フォルダ作成
                await EnsureDirectoryExistsAsync(GetSharedPath());

                // 帳票フォルダ作成（当月と前月）
                var currentDate = DateTime.Now;
                var previousMonth = currentDate.AddMonths(-1);
                await EnsureDirectoryExistsAsync(GetReportPath(currentDate));
                await EnsureDirectoryExistsAsync(GetReportPath(previousMonth));

                // バックアップフォルダ作成
                await EnsureDirectoryExistsAsync(GetBackupPath(BackupType.Daily, currentDate));
                await EnsureDirectoryExistsAsync(GetBackupPath(BackupType.Weekly, currentDate));
                await EnsureDirectoryExistsAsync(GetBackupPath(BackupType.Monthly, currentDate));
                await EnsureDirectoryExistsAsync(GetBackupPath(BackupType.BeforeDailyClose, currentDate));

                _logger.LogInformation("ディレクトリ構造の初期化が完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ディレクトリ構造の初期化中にエラーが発生しました");
                throw;
            }
        }

        /// <summary>
        /// 処理待ちファイル一覧を取得
        /// </summary>
        public async Task<List<string>> GetPendingFilesAsync(string department)
        {
            try
            {
                var importPath = GetImportPath(department);
                if (!Directory.Exists(importPath))
                {
                    return new List<string>();
                }

                var files = Directory.GetFiles(importPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => !Path.GetFileName(f).StartsWith("~")) // Excelの一時ファイルを除外
                    .Where(f => !Path.GetFileName(f).StartsWith(".")) // 隠しファイルを除外
                    .OrderBy(f => File.GetCreationTime(f))
                    .ToList();

                _logger.LogDebug("{Department}の処理待ちファイル数: {Count}", department, files.Count);
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "処理待ちファイル取得中にエラーが発生しました: {Department}", department);
                return new List<string>();
            }
        }

        /// <summary>
        /// 帳票ファイルを保存
        /// </summary>
        public async Task<string> SaveReportAsync(byte[] reportData, string reportName, DateTime reportDate)
        {
            try
            {
                var reportPath = GetReportPath(reportDate);
                await EnsureDirectoryExistsAsync(reportPath);

                var fileName = $"{reportDate:yyyyMMdd}_{reportName}.pdf";
                var fullPath = Path.Combine(reportPath, fileName);

                await File.WriteAllBytesAsync(fullPath, reportData);

                _logger.LogInformation("帳票ファイルを保存しました: {FilePath}", fullPath);
                return fullPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "帳票ファイル保存中にエラーが発生しました: {ReportName}", reportName);
                throw;
            }
        }

        /// <summary>
        /// 古いファイルをクリーンアップ
        /// </summary>
        public async Task CleanupOldFilesAsync(FolderType folderType, int retentionDays)
        {
            try
            {
                _logger.LogInformation("{FolderType}フォルダのクリーンアップを開始します (保持日数: {RetentionDays}日)", folderType, retentionDays);

                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var deletedCount = 0;

                switch (folderType)
                {
                    case FolderType.Processed:
                        foreach (var department in _settings.Departments)
                        {
                            deletedCount += await CleanupDirectoryAsync(GetProcessedPath(department), cutoffDate);
                        }
                        break;
                    case FolderType.Error:
                        foreach (var department in _settings.Departments)
                        {
                            deletedCount += await CleanupDirectoryAsync(GetErrorPath(department), cutoffDate);
                        }
                        break;
                    case FolderType.Report:
                        var reportBasePath = Path.Combine(_settings.GetImportRootPath(), "Reports");
                        if (Directory.Exists(reportBasePath))
                        {
                            deletedCount += await CleanupDirectoryRecursiveAsync(reportBasePath, cutoffDate);
                        }
                        break;
                    case FolderType.Backup:
                        await CleanupBackupFilesAsync();
                        break;
                }

                _logger.LogInformation("{FolderType}フォルダのクリーンアップが完了しました (削除ファイル数: {DeletedCount})", folderType, deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{FolderType}フォルダのクリーンアップ中にエラーが発生しました", folderType);
                throw;
            }
        }

        /// <summary>
        /// ディスク容量をチェック
        /// </summary>
        public async Task<bool> CheckDiskSpaceAsync(string path, double requiredSpaceGB = 1.0)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path));
                var availableSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                
                var hasSpace = availableSpaceGB >= requiredSpaceGB;
                
                _logger.LogDebug("ディスク容量チェック - パス: {Path}, 利用可能: {Available:F2}GB, 必要: {Required:F2}GB, 結果: {Result}", 
                    path, availableSpaceGB, requiredSpaceGB, hasSpace ? "OK" : "不足");

                return hasSpace;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ディスク容量チェック中にエラーが発生しました: {Path}", path);
                return false;
            }
        }

        /// <summary>
        /// 書き込み権限をチェック
        /// </summary>
        public async Task<bool> CheckWritePermissionAsync(string path)
        {
            try
            {
                var testFile = Path.Combine(path, $"test_write_permission_{Guid.NewGuid()}.tmp");
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                
                _logger.LogDebug("書き込み権限チェック - パス: {Path}, 結果: OK", path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "書き込み権限チェック失敗 - パス: {Path}", path);
                return false;
            }
        }

        /// <summary>
        /// ディレクトリが存在しない場合は作成
        /// </summary>
        private async Task EnsureDirectoryExistsAsync(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogDebug("ディレクトリを作成しました: {Path}", path);
            }
        }

        /// <summary>
        /// 指定ディレクトリ内の古いファイルを削除
        /// </summary>
        private async Task<int> CleanupDirectoryAsync(string directoryPath, DateTime cutoffDate)
        {
            if (!Directory.Exists(directoryPath))
                return 0;

            var deletedCount = 0;
            var files = Directory.GetFiles(directoryPath);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogDebug("古いファイルを削除しました: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ファイル削除に失敗しました: {FilePath}", file);
                    }
                }
            }

            return deletedCount;
        }

        /// <summary>
        /// 指定ディレクトリ内の古いファイルを再帰的に削除
        /// </summary>
        private async Task<int> CleanupDirectoryRecursiveAsync(string directoryPath, DateTime cutoffDate)
        {
            if (!Directory.Exists(directoryPath))
                return 0;

            var deletedCount = 0;

            // ファイルを削除
            var files = Directory.GetFiles(directoryPath);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ファイル削除に失敗しました: {FilePath}", file);
                    }
                }
            }

            // サブディレクトリを再帰的に処理
            var directories = Directory.GetDirectories(directoryPath);
            foreach (var directory in directories)
            {
                deletedCount += await CleanupDirectoryRecursiveAsync(directory, cutoffDate);
                
                // 空のディレクトリを削除
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                        _logger.LogDebug("空のディレクトリを削除しました: {DirectoryPath}", directory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ディレクトリ削除に失敗しました: {DirectoryPath}", directory);
                }
            }

            return deletedCount;
        }

        /// <summary>
        /// バックアップファイルの世代管理クリーンアップ
        /// </summary>
        private async Task CleanupBackupFilesAsync()
        {
            var backupRoot = _settings.GetBackupRootPath();
            if (!Directory.Exists(backupRoot))
                return;

            // 日次バックアップのクリーンアップ
            await CleanupBackupTypeAsync(BackupType.Daily, _settings.FileRetention.DailyBackupCount);
            
            // 週次バックアップのクリーンアップ
            await CleanupBackupTypeAsync(BackupType.Weekly, _settings.FileRetention.WeeklyBackupCount);
            
            // 月次バックアップのクリーンアップ
            await CleanupBackupTypeAsync(BackupType.Monthly, _settings.FileRetention.MonthlyBackupCount);
        }

        /// <summary>
        /// 指定されたバックアップタイプの世代管理
        /// </summary>
        private async Task CleanupBackupTypeAsync(BackupType backupType, int retentionCount)
        {
            try
            {
                var backupTypeDir = Path.Combine(_settings.GetBackupRootPath(), backupType.ToString());
                if (!Directory.Exists(backupTypeDir))
                    return;

                var backupFiles = Directory.GetFiles(backupTypeDir, "*.*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(retentionCount)
                    .ToList();

                foreach (var file in backupFiles)
                {
                    try
                    {
                        File.Delete(file.FullName);
                        _logger.LogDebug("古いバックアップファイルを削除しました: {FilePath}", file.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "バックアップファイル削除に失敗しました: {FilePath}", file.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{BackupType}バックアップのクリーンアップ中にエラーが発生しました", backupType);
            }
        }
        
        /// <summary>
        /// 帳票の出力パスを取得
        /// </summary>
        public async Task<string> GetReportOutputPathAsync(string reportType, DateTime jobDate, string extension)
        {
            await Task.CompletedTask; // 非同期対応
            
            try
            {
                // 帳票出力ベースパスを取得
                var reportBasePath = _settings.GetReportOutputPath();
                
                // 年月別フォルダ（YYYY-MM形式）
                var yearMonth = jobDate.ToString("yyyy-MM");
                var reportPath = Path.Combine(reportBasePath, yearMonth);
                
                // ディレクトリが存在しない場合は作成
                if (!Directory.Exists(reportPath))
                {
                    Directory.CreateDirectory(reportPath);
                    _logger.LogInformation("帳票出力ディレクトリを作成しました: {Path}", reportPath);
                }
                
                // ファイル名を生成（reportType_YYYYMMDD_HHMMSS.extension）
                var fileName = $"{reportType}_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}.{extension}";
                var fullPath = Path.Combine(reportPath, fileName);
                
                _logger.LogInformation("帳票出力パス: {Path}", fullPath);
                return fullPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "帳票出力パス取得中にエラーが発生しました: {ReportType}, {JobDate}", reportType, jobDate);
                throw;
            }
        }
    }
}