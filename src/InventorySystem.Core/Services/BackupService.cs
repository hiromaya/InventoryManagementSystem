using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InventorySystem.Core.Configuration;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// バックアップサービスの実装（Dドライブ対応版）
    /// </summary>
    public class BackupService : IBackupService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackupService> _logger;
        private readonly FileStorageSettings _settings;
        private readonly IFileManagementService _fileManagementService;

        public BackupService(
            IConfiguration configuration,
            IOptions<FileStorageSettings> settings,
            IFileManagementService fileManagementService,
            ILogger<BackupService> logger)
        {
            _configuration = configuration;
            _settings = settings.Value;
            _fileManagementService = fileManagementService;
            _logger = logger;
        }

        public async Task<string> CreateBackup(string processType, DateTime jobDate)
        {
            try
            {
                // 日次、週次、月次のバックアップタイプを決定
                var backupType = DetermineBackupType(jobDate);
                var backupFolder = _fileManagementService.GetBackupPath(backupType, jobDate);

                // バックアップフォルダの作成
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                // ディスク容量チェック
                if (!await _fileManagementService.CheckDiskSpaceAsync(backupFolder, 2.0)) // 2GB必要
                {
                    throw new InvalidOperationException("バックアップ実行に必要なディスク容量が不足しています");
                }

                // 書き込み権限チェック
                if (!await _fileManagementService.CheckWritePermissionAsync(backupFolder))
                {
                    throw new UnauthorizedAccessException("バックアップフォルダへの書き込み権限がありません");
                }

                // バックアップファイル名の生成
                var timestamp = DateTime.Now.ToString("HHmmss");
                var fileName = $"{processType}_{jobDate:yyyyMMdd}_{timestamp}_{backupType}.sql";
                var backupPath = Path.Combine(backupFolder, fileName);

                // バックアップメタデータの作成
                var metadata = new
                {
                    ProcessType = processType,
                    JobDate = jobDate,
                    BackupType = backupType.ToString(),
                    CreatedAt = DateTime.Now,
                    BackupPath = backupPath,
                    Version = "1.0"
                };

                // メタデータファイルの作成
                var metadataPath = Path.ChangeExtension(backupPath, ".metadata.json");
                await File.WriteAllTextAsync(metadataPath, System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                // 実際のバックアップ処理（プレースホルダー）
                // 実際の実装では、データベースのバックアップSQLコマンドを実行
                var backupScript = GenerateBackupScript(processType, jobDate);
                await File.WriteAllTextAsync(backupPath, backupScript);

                _logger.LogInformation("バックアップ作成成功: {BackupPath}", backupPath);
                return backupPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "バックアップ作成エラー: ProcessType={ProcessType}, JobDate={JobDate}", 
                    processType, jobDate);
                throw;
            }
        }

        public async Task<string> CreateBackup(DateTime jobDate, BackupType backupType)
        {
            try
            {
                var backupFolder = _fileManagementService.GetBackupPath(backupType, jobDate);

                // バックアップフォルダの作成
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                // ディスク容量チェック
                if (!await _fileManagementService.CheckDiskSpaceAsync(backupFolder, 2.0)) // 2GB必要
                {
                    throw new InvalidOperationException("バックアップ実行に必要なディスク容量が不足しています");
                }

                // 書き込み権限チェック
                if (!await _fileManagementService.CheckWritePermissionAsync(backupFolder))
                {
                    throw new UnauthorizedAccessException("バックアップフォルダへの書き込み権限がありません");
                }

                // バックアップファイル名の生成
                var timestamp = DateTime.Now.ToString("HHmmss");
                var processType = backupType.ToString();
                var fileName = $"{processType}_{jobDate:yyyyMMdd}_{timestamp}.sql";
                var backupPath = Path.Combine(backupFolder, fileName);

                // バックアップメタデータの作成
                var metadata = new
                {
                    ProcessType = processType,
                    JobDate = jobDate,
                    BackupType = backupType.ToString(),
                    CreatedAt = DateTime.Now,
                    BackupPath = backupPath,
                    Version = "1.0"
                };

                // メタデータファイルの作成
                var metadataPath = Path.ChangeExtension(backupPath, ".metadata.json");
                await File.WriteAllTextAsync(metadataPath, System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                // 実際のバックアップ処理（プレースホルダー）
                var backupScript = GenerateBackupScript(processType, jobDate);
                await File.WriteAllTextAsync(backupPath, backupScript);

                _logger.LogInformation("バックアップ作成成功: {BackupPath}", backupPath);
                return backupPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "バックアップ作成エラー: JobDate={JobDate}, BackupType={BackupType}", 
                    jobDate, backupType);
                throw;
            }
        }

        public async Task<bool> VerifyBackup(string backupPath)
        {
            try
            {
                // 簡易的な検証: ファイル存在チェック
                if (File.Exists(backupPath))
                {
                    _logger.LogInformation("バックアップ検証成功: {BackupPath}", backupPath);
                    return true;
                }
                else
                {
                    _logger.LogWarning("バックアップファイルが存在しません: {BackupPath}", backupPath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "バックアップ検証エラー: {BackupPath}", backupPath);
                return false;
            }
        }

        public async Task CleanupOldBackups(int retentionDays)
        {
            try
            {
                _logger.LogInformation("古いバックアップのクリーンアップを開始します (保持日数: {RetentionDays}日)", retentionDays);

                // FileManagementServiceのクリーンアップ機能を使用
                await _fileManagementService.CleanupOldFilesAsync(FolderType.Backup, retentionDays);

                _logger.LogInformation("バックアップクリーンアップが完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "バックアップクリーンアップエラー");
                throw;
            }
        }

        public async Task RestoreBackup(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    throw new FileNotFoundException("バックアップファイルが見つかりません", backupPath);
                }

                // メタデータファイルの確認
                var metadataPath = Path.ChangeExtension(backupPath, ".metadata.json");
                if (File.Exists(metadataPath))
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataPath);
                    _logger.LogInformation("バックアップメタデータ: {Metadata}", metadataJson);
                }

                // 実際の復元処理は別途実装が必要
                _logger.LogInformation("データベース復元処理を実行: {BackupPath}", backupPath);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "データベース復元エラー: {BackupPath}", backupPath);
                throw;
            }
        }

        /// <summary>
        /// バックアップタイプを決定
        /// </summary>
        private BackupType DetermineBackupType(DateTime jobDate)
        {
            // 月末の場合は月次バックアップ
            if (jobDate.Day == DateTime.DaysInMonth(jobDate.Year, jobDate.Month))
            {
                return BackupType.Monthly;
            }

            // 日曜日の場合は週次バックアップ
            if (jobDate.DayOfWeek == DayOfWeek.Sunday)
            {
                return BackupType.Weekly;
            }

            // その他は日次バックアップ
            return BackupType.Daily;
        }

        /// <summary>
        /// バックアップスクリプトを生成
        /// </summary>
        private string GenerateBackupScript(string processType, DateTime jobDate)
        {
            var script = $@"-- バックアップスクリプト
-- 処理タイプ: {processType}
-- 対象日付: {jobDate:yyyy-MM-dd}
-- 作成日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

-- このファイルは実際のバックアップスクリプトのプレースホルダーです
-- 実際の実装では、以下のような処理を行います：
-- 1. データベースの整合性チェック
-- 2. トランザクションログの切り捨て
-- 3. フルバックアップまたは差分バックアップの作成
-- 4. バックアップファイルの検証

PRINT 'バックアップ処理開始: {processType} - {jobDate:yyyy-MM-dd}';

-- バックアップ対象テーブル一覧
-- CpInventoryMaster
-- SalesVouchers
-- PurchaseVouchers
-- InventoryAdjustments
-- Inventories
-- 各種マスタテーブル

-- 実際のバックアップコマンド（例）
-- BACKUP DATABASE [InventorySystem] TO DISK = N'バックアップファイルパス'
-- WITH FORMAT, INIT, NAME = N'InventorySystem-完全 データベース バックアップ'

PRINT 'バックアップ処理完了';
";
            return script;
        }
    }
}