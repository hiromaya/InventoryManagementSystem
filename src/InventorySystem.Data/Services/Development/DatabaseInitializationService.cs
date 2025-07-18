using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces.Development;
using DatabaseValidationResult = InventorySystem.Core.Interfaces.Development.DatabaseValidationResult;

namespace InventorySystem.Data.Services.Development;

/// <summary>
/// データベース初期化サービス
/// </summary>
public class DatabaseInitializationService : IDatabaseInitializationService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializationService> _logger;
    
    // マイグレーション関連の定数
    private const string MigrationHistoryTable = "__SchemaVersions";
    private const string MigrationsFolderPath = "database/migrations";
    private const string CreateDatabaseScriptPath = "database/CreateDatabase.sql";
    
    // マイグレーション実行順序を明確に定義
    private readonly List<string> _migrationOrder = new()
    {
        // === 基本マイグレーション ===
        "000_CreateMigrationHistory.sql",
        
        // === データベース構造追加 ===
        "005_AddDailyCloseProtectionColumns.sql",
        "006_AddDataSetManagement.sql",
        "007_AddDeactivationIndexes.sql",
        "008_AddUnmatchOptimizationIndexes.sql",
        "009_CreateInitialInventoryStagingTable.sql",
        "010_AddPersonInChargeAndAveragePrice.sql",
        "012_AddGrossProfitColumnToSalesVouchers.sql",
        "013_AddImportTypeToInventoryMaster.sql",
        "014_AddMissingColumnsToInventoryMaster.sql",
        "015_AddMonthlyColumnsToCpInventoryMaster.sql",
        "016_AddMonthlyFieldsToCpInventory.sql",
        
        // === データ整理・修正 ===
        "017_Cleanup_Duplicate_InventoryMaster.sql",
        "018_FixExistingCpInventoryProductCategories.sql",
        "019_Fix_DepartmentCode_Size.sql",
        "020_Fix_MergeInventoryMaster_OutputClause.sql",
        "021_VerifyInventoryMasterSchema.sql",
        "022_AddLastTransactionDates.sql",
        "023_UpdateDataSetManagement.sql",
        
        // === マスタデータ・統合処理 ===
        // "024_CreateProductMaster.sql",              // 除外: migrate-phase3/5との競合回避のため
                                                        // このスクリプトはCreatedDate/UpdatedDateスキーマを前提とするが
                                                        // 移行後はCreatedAt/UpdatedAtスキーマになるため除外
        "024_PrepareDataSetUnification.sql",        // 重複024番（統合準備）
        
        // === 緊急修正・履歴管理 ===
        "025_Fix_DataSets_Columns.sql",            // DataSetsテーブルカラム修正
        "025_CreateFileProcessingHistory.sql",      // ファイル処理履歴
        "026_CreateDateProcessingHistory.sql",      // 日付処理履歴
        "027_CreatePreviousMonthInventory.sql",     // 前月在庫管理
        "028_AddDataSetTypeAndImportedAt.sql",      // DataSetTypeとImportedAtカラム追加
        "029_CreateShippingMarkMaster.sql",         // ShippingMarkMasterテーブル作成
        
        // === 重要マスタエンティティ追加 ===
        "030_CreateGradeMaster.sql",                // GradeMasterテーブル作成
        "031_CreateClassMaster.sql",                // ClassMasterテーブル作成
        "032_FixOriginMasterToRegionMaster.sql",    // 産地マスタ名統一
        "05_create_master_tables.sql",              // 完全版マスタテーブル作成（CreatedAt/UpdatedAt対応）
        
        // === DataSetsスキーマ完全修正（重要度最高） ===
        "033_FixDataSetsSchema.sql",               // DataSetsテーブル包括的修正
        "034_FixDataSetManagementSchema.sql",      // DataSetManagementカラムサイズ統一
        
        // === 追加テーブル作成 ===
        "035_AddAllMissingTables.sql",             // 不足しているテーブルの追加
        
        // === CreatedAt/UpdatedAt移行フェーズ ===
        "050_Phase1_CheckCurrentSchema.sql",       // 現在のスキーマ確認（実行不要）
        "051_Phase2_AddNewColumns.sql",            // 新しいカラムを追加
        "052_Phase3_MigrateDataAndSync.sql",       // データ移行と同期
        "053_Phase5_Cleanup.sql"                   // 古いカラムの削除
    };
    
    // 旧テーブル定義（後方互換性のため一時的に保持）
    private readonly Dictionary<string, string> _tableDefinitions = new Dictionary<string, string>
    {
        ["ProcessHistory"] = @"
            CREATE TABLE ProcessHistory (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                DatasetId NVARCHAR(50) NOT NULL,
                JobDate DATE NOT NULL,
                ProcessType NVARCHAR(50) NOT NULL,
                StartTime DATETIME2 NOT NULL,
                EndTime DATETIME2,
                Status INT NOT NULL,
                ErrorMessage NVARCHAR(MAX),
                ExecutedBy NVARCHAR(50) NOT NULL,
                RecordCount INT,
                ErrorCount INT,
                DataHash NVARCHAR(100)
            )",
        
        ["DataSetManagement"] = @"
            CREATE TABLE DataSetManagement (
                DatasetId NVARCHAR(50) PRIMARY KEY,
                JobDate DATE NOT NULL,
                ProcessType NVARCHAR(50) NOT NULL,
                ImportedFiles NVARCHAR(MAX),
                TotalRecordCount INT NOT NULL DEFAULT 0,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                CreatedBy NVARCHAR(50) NOT NULL
            )",
        
        ["DailyCloseManagement"] = @"
            CREATE TABLE DailyCloseManagement (
                JobDate DATE NOT NULL PRIMARY KEY,
                DatasetId NVARCHAR(50) NOT NULL,
                DailyReportDatasetId NVARCHAR(50),
                ProcessedAt DATETIME2,
                ProcessedBy NVARCHAR(50),
                ValidationStatus NVARCHAR(20),
                ValidationMessage NVARCHAR(MAX),
                DataHash NVARCHAR(100),
                UpdatedInventoryCount INT DEFAULT 0,
                BackupPath NVARCHAR(500),
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
            )",
        
        ["AuditLogs"] = @"
            CREATE TABLE AuditLogs (
                LogId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                LogDate DATETIME2 NOT NULL DEFAULT GETDATE(),
                JobDate DATE,
                ProcessType NVARCHAR(50) NOT NULL,
                Action NVARCHAR(100) NOT NULL,
                TableName NVARCHAR(100),
                RecordKey NVARCHAR(200),
                OldValue NVARCHAR(MAX),
                NewValue NVARCHAR(MAX),
                [User] NVARCHAR(50) NOT NULL,
                IPAddress NVARCHAR(50),
                Comment NVARCHAR(MAX)
            )",
        
        ["FileProcessingHistory"] = @"
            CREATE TABLE FileProcessingHistory (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                FileName NVARCHAR(255) NOT NULL,
                FileHash NVARCHAR(64) NOT NULL,
                FileSize BIGINT NOT NULL,
                FirstProcessedAt DATETIME2 NOT NULL,
                LastProcessedAt DATETIME2 NOT NULL,
                TotalRecordCount INT NOT NULL,
                FileType NVARCHAR(50) NOT NULL
            )",
        
        ["DateProcessingHistory"] = @"
            CREATE TABLE DateProcessingHistory (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                FileHistoryId INT NOT NULL,
                JobDate DATE NOT NULL,
                ProcessedAt DATETIME2 NOT NULL,
                RecordCount INT NOT NULL,
                DatasetId NVARCHAR(50) NOT NULL,
                ProcessType NVARCHAR(50) NOT NULL,
                Department NVARCHAR(50) NOT NULL,
                ExecutedBy NVARCHAR(50) NOT NULL DEFAULT 'System',
                CONSTRAINT FK_DateProcessingHistory_FileProcessingHistory 
                    FOREIGN KEY (FileHistoryId) REFERENCES FileProcessingHistory(Id)
            )",
        
        ["PreviousMonthInventory"] = @"
            CREATE TABLE PreviousMonthInventory (
                ProductCode NVARCHAR(5) NOT NULL,
                GradeCode NVARCHAR(3) NOT NULL,
                ClassCode NVARCHAR(3) NOT NULL,
                ShippingMarkCode NVARCHAR(4) NOT NULL,
                ShippingMarkName NVARCHAR(8) NOT NULL,
                ProductName NVARCHAR(100) NOT NULL DEFAULT '',
                Unit NVARCHAR(10) NOT NULL DEFAULT 'PCS',
                Quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
                Amount DECIMAL(18,4) NOT NULL DEFAULT 0,
                UnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
                YearMonth NVARCHAR(6) NOT NULL,
                CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
                UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
                CreatedBy NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
                CONSTRAINT PK_PreviousMonthInventory PRIMARY KEY CLUSTERED (
                    ProductCode,
                    GradeCode,
                    ClassCode,
                    ShippingMarkCode,
                    ShippingMarkName,
                    YearMonth
                )
            )"
    };

    // インデックス定義
    private readonly Dictionary<string, List<string>> _indexDefinitions = new Dictionary<string, List<string>>
    {
        ["ProcessHistory"] = new List<string>
        {
            "CREATE INDEX IX_ProcessHistory_JobDate_ProcessType ON ProcessHistory(JobDate, ProcessType)",
            "CREATE INDEX IX_ProcessHistory_DatasetId ON ProcessHistory(DatasetId)"
        },
        ["DataSetManagement"] = new List<string>
        {
            "CREATE INDEX IX_DataSetManagement_JobDate ON DataSetManagement(JobDate)"
        },
        ["DailyCloseManagement"] = new List<string>
        {
            "CREATE INDEX IX_DailyCloseManagement_DatasetId ON DailyCloseManagement(DatasetId)"
        },
        ["AuditLogs"] = new List<string>
        {
            "CREATE INDEX IX_AuditLogs_LogDate ON AuditLogs(LogDate)",
            "CREATE INDEX IX_AuditLogs_JobDate_ProcessType ON AuditLogs(JobDate, ProcessType)"
        },
        ["FileProcessingHistory"] = new List<string>
        {
            "CREATE INDEX IX_FileProcessingHistory_FileHash ON FileProcessingHistory(FileHash)",
            "CREATE INDEX IX_FileProcessingHistory_FileName ON FileProcessingHistory(FileName)",
            "CREATE INDEX IX_FileProcessingHistory_FileType ON FileProcessingHistory(FileType)"
        },
        ["DateProcessingHistory"] = new List<string>
        {
            "CREATE INDEX IX_DateProcessingHistory_JobDate ON DateProcessingHistory(JobDate)",
            "CREATE INDEX IX_DateProcessingHistory_ProcessType ON DateProcessingHistory(ProcessType)",
            "CREATE INDEX IX_DateProcessingHistory_Department ON DateProcessingHistory(Department)",
            "CREATE UNIQUE INDEX IX_DateProcessingHistory_Unique ON DateProcessingHistory(FileHistoryId, JobDate, ProcessType, Department)"
        },
        ["PreviousMonthInventory"] = new List<string>
        {
            "CREATE NONCLUSTERED INDEX IX_PreviousMonthInventory_YearMonth ON PreviousMonthInventory(YearMonth) INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, Quantity, Amount)"
        }
    };
    
    // 管理対象テーブル（テーブル定義から自動取得）
    private string[] RequiredTables => _tableDefinitions.Keys.ToArray();
    
    public DatabaseInitializationService(string connectionString, ILogger<DatabaseInitializationService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<InitializationResult> InitializeDatabaseAsync(bool force = false)
    {
        var result = new InitializationResult
        {
            ForceMode = force,
            DatabaseVersion = "1.0.0",
            TotalMigrationCount = _migrationOrder.Count
        };
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("データベース初期化を開始します。Force={Force}", force);
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // 強制削除モードの場合、すべてのテーブルを削除
            if (force)
            {
                _logger.LogInformation("強制モード: 既存のテーブルを削除します");
                await DropAllTablesAsync(connection);
            }
            
            // CreateDatabase.sqlの実行
            var createDbScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                "../../../../../", CreateDatabaseScriptPath);
            if (File.Exists(createDbScriptPath))
            {
                _logger.LogInformation("CreateDatabase.sql を実行します");
                await ExecuteSqlFileAsync(connection, createDbScriptPath, "CreateDatabase.sql");
                result.CreatedTables.Add("基本テーブル（CreateDatabase.sql）");
            }
            else
            {
                _logger.LogWarning("CreateDatabase.sql が見つかりません: {Path}", createDbScriptPath);
                result.Warnings.Add($"CreateDatabase.sql が見つかりません: {createDbScriptPath}");
            }
            
            // マイグレーション履歴テーブルの確認・作成
            await EnsureMigrationHistoryTableExistsAsync(connection);
            
            // マイグレーションスクリプトの実行
            var executedMigrations = await ApplyMigrationsAsync(connection, result);
            result.ExecutedMigrations = executedMigrations;
            result.AppliedMigrationOrder = executedMigrations;
            result.SkippedMigrationCount = result.TotalMigrationCount - executedMigrations.Count;
            
            // データベース構造の検証
            try
            {
                _logger.LogInformation("データベース構造の検証を開始します");
                result.ValidationResult = await ValidateDatabaseStructureAsync(connection);
                
                if (result.ValidationResult.Warnings.Any())
                {
                    result.Warnings.AddRange(result.ValidationResult.Warnings);
                }
                
                if (!result.ValidationResult.IsValid)
                {
                    result.DetectedIssues.AddRange(result.ValidationResult.Errors);
                    result.DetectedIssues.AddRange(result.ValidationResult.DataIntegrityIssues);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "データベース構造の検証でエラーが発生しました");
                result.Warnings.Add($"データベース構造の検証エラー: {ex.Message}");
            }
            
            result.Success = result.Errors.Count == 0;
            
            if (result.Success)
            {
                _logger.LogInformation("データベース初期化が完了しました。実行されたマイグレーション: {Count}個",
                    executedMigrations.Count);
            }
            else
            {
                _logger.LogError("データベース初期化で {ErrorCount} 個のエラーが発生しました", 
                    result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データベース初期化で予期しないエラーが発生しました");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Errors.Add($"予期しないエラー: {ex.Message}");
        }
        
        stopwatch.Stop();
        result.ExecutionTime = stopwatch.Elapsed;
        
        // 初期化サマリーをログ出力
        LogInitializationSummary(result);
        
        return result;
    }
    
    public async Task<bool> CheckTablesExistAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            foreach (var tableName in RequiredTables)
            {
                if (!await TableExistsAsync(connection, tableName))
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "テーブル存在確認でエラーが発生しました");
            return false;
        }
    }
    
    public async Task<List<string>> GetMissingTablesAsync()
    {
        var missingTables = new List<string>();
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            foreach (var requiredTable in RequiredTables)
            {
                if (!await TableExistsAsync(connection, requiredTable))
                {
                    missingTables.Add(requiredTable);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "不足テーブルの確認でエラーが発生しました");
            return RequiredTables.ToList(); // エラー時は全テーブルを不足として返す
        }
        
        return missingTables;
    }
    
    /// <summary>
    /// テーブルの存在確認
    /// </summary>
    private async Task<bool> TableExistsAsync(SqlConnection connection, string tableName)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = @TableName AND TABLE_TYPE = 'BASE TABLE'";
        
        try
        {
            var count = await connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "テーブル存在確認エラー: {Table}", tableName);
            return false;
        }
    }
    
    
    /// <summary>
    /// スキーマ不整合の修正
    /// </summary>
    private async Task<bool> FixSchemaInconsistenciesAsync(SqlConnection connection)
    {
        var fixes = new List<string>();
        
        try
        {
            // 1. ProcessHistoryテーブルのカラム名修正
            var hasProcessedBy = await CheckColumnExistsAsync(connection, "ProcessHistory", "ProcessedBy");
            var hasExecutedBy = await CheckColumnExistsAsync(connection, "ProcessHistory", "ExecutedBy");
            
            if (hasProcessedBy && !hasExecutedBy)
            {
                _logger.LogInformation("ProcessHistory.ProcessedBy を ExecutedBy にリネームします");
                await connection.ExecuteAsync("EXEC sp_rename 'ProcessHistory.ProcessedBy', 'ExecutedBy', 'COLUMN'");
                fixes.Add("ProcessHistory.ProcessedBy → ExecutedBy");
            }
            
            // 2. ProcessHistoryテーブルのIdカラム修正（ProcessId → Id）
            var hasProcessId = await CheckColumnExistsAsync(connection, "ProcessHistory", "ProcessId");
            var hasId = await CheckColumnExistsAsync(connection, "ProcessHistory", "Id");
            
            if (hasProcessId && !hasId)
            {
                _logger.LogInformation("ProcessHistory.ProcessId を Id にリネームします");
                await connection.ExecuteAsync("EXEC sp_rename 'ProcessHistory.ProcessId', 'Id', 'COLUMN'");
                fixes.Add("ProcessHistory.ProcessId → Id");
            }
            
            // 3. ProcessHistoryテーブルのDataHash列を追加（存在しない場合）
            var hasDataHash = await CheckColumnExistsAsync(connection, "ProcessHistory", "DataHash");
            if (!hasDataHash)
            {
                _logger.LogInformation("ProcessHistory.DataHash カラムを追加します");
                await connection.ExecuteAsync("ALTER TABLE ProcessHistory ADD DataHash NVARCHAR(100)");
                fixes.Add("ProcessHistory.DataHash カラム追加");
            }
            
            if (fixes.Any())
            {
                _logger.LogInformation("スキーマ不整合を修正しました: {Fixes}", string.Join(", ", fixes));
            }
            else
            {
                _logger.LogInformation("スキーマ不整合は検出されませんでした");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スキーマ修正中にエラーが発生しました");
            return false;
        }
    }
    
    /// <summary>
    /// カラムの存在確認
    /// </summary>
    private async Task<bool> CheckColumnExistsAsync(SqlConnection connection, string tableName, string columnName)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";
        
        try
        {
            var count = await connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName, ColumnName = columnName });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "カラム存在確認エラー: {Table}.{Column}", tableName, columnName);
            return false;
        }
    }
    
    /// <summary>
    /// 依存関係を考慮してテーブルを削除
    /// </summary>
    private async Task DropTablesInOrderAsync(SqlConnection connection)
    {
        try
        {
            _logger.LogInformation("依存関係を考慮してテーブルを削除します");
            
            // 削除順序を定義（依存されている側から削除）
            var dropOrder = new[]
            {
                "DateProcessingHistory",    // FileProcessingHistoryに依存
                "FileProcessingHistory",    // 他のテーブルから参照される
                "AuditLogs",
                "DailyCloseManagement",
                "DataSetManagement",
                "ProcessHistory"
            };
            
            foreach (var tableName in dropOrder)
            {
                if (await TableExistsAsync(connection, tableName))
                {
                    try
                    {
                        // 外部キー制約を無効化してから削除を試みる
                        await connection.ExecuteAsync($@"
                            -- 外部キー制約のチェックを一時的に無効化
                            ALTER TABLE {tableName} NOCHECK CONSTRAINT ALL;
                            
                            -- テーブルを削除
                            DROP TABLE {tableName};
                        ");
                        
                        _logger.LogInformation("テーブル {TableName} を削除しました", tableName);
                    }
                    catch (SqlException ex) when (ex.Number == 3726) // 外部キー制約エラー
                    {
                        _logger.LogWarning("外部キー制約により {TableName} を削除できません。関連する外部キーを削除します", tableName);
                        
                        // 外部キー制約を削除してから再試行
                        await DropForeignKeyConstraintsAsync(connection, tableName);
                        await connection.ExecuteAsync($"DROP TABLE {tableName}");
                        
                        _logger.LogInformation("テーブル {TableName} を削除しました（外部キー制約削除後）", tableName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "テーブル削除中にエラーが発生しました");
            throw;
        }
    }
    
    /// <summary>
    /// 指定されたテーブルを参照する外部キー制約を削除
    /// </summary>
    private async Task DropForeignKeyConstraintsAsync(SqlConnection connection, string tableName)
    {
        const string sql = @"
            SELECT 
                fk.name AS ConstraintName,
                OBJECT_NAME(fk.parent_object_id) AS TableName
            FROM 
                sys.foreign_keys fk
            WHERE 
                OBJECT_NAME(fk.referenced_object_id) = @TableName";
        
        var constraints = await connection.QueryAsync<(string ConstraintName, string TableName)>(
            sql, new { TableName = tableName });
        
        foreach (var (constraintName, referencingTable) in constraints)
        {
            try
            {
                await connection.ExecuteAsync($"ALTER TABLE {referencingTable} DROP CONSTRAINT {constraintName}");
                _logger.LogInformation("外部キー制約 {ConstraintName} を削除しました（テーブル: {TableName}）", 
                    constraintName, referencingTable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "外部キー制約 {ConstraintName} の削除に失敗しました", constraintName);
            }
        }
    }
    
    /// <summary>
    /// すべてのテーブルを削除（forceモード用）
    /// </summary>
    private async Task DropAllTablesAsync(SqlConnection connection)
    {
        try
        {
            _logger.LogInformation("すべてのテーブルを削除します");
            
            // 外部キー制約を一時的に無効化
            await connection.ExecuteAsync("EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'");
            
            // すべてのユーザーテーブルを削除
            var sql = @"
                DECLARE @sql NVARCHAR(MAX) = '';
                SELECT @sql = @sql + 'DROP TABLE [' + SCHEMA_NAME(schema_id) + '].[' + name + ']; '
                FROM sys.tables
                WHERE type = 'U'
                ORDER BY name;
                EXEC sp_executesql @sql;";
            
            await connection.ExecuteAsync(sql);
            _logger.LogInformation("すべてのテーブルを削除しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "テーブル削除中にエラーが発生しました");
            throw;
        }
    }
    
    /// <summary>
    /// マイグレーション履歴テーブルの確認・作成
    /// </summary>
    private async Task EnsureMigrationHistoryTableExistsAsync(SqlConnection connection)
    {
        var exists = await TableExistsAsync(connection, MigrationHistoryTable);
        if (!exists)
        {
            _logger.LogInformation("マイグレーション履歴テーブルを作成します");
            
            // 000_CreateMigrationHistory.sql を探して実行
            var migrationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                "../../../../../", MigrationsFolderPath, "000_CreateMigrationHistory.sql");
            
            if (File.Exists(migrationPath))
            {
                await ExecuteSqlFileAsync(connection, migrationPath, "000_CreateMigrationHistory.sql");
            }
            else
            {
                // ファイルが見つからない場合は直接作成
                await connection.ExecuteAsync(@"
                    CREATE TABLE __SchemaVersions (
                        MigrationId NVARCHAR(255) NOT NULL PRIMARY KEY,
                        AppliedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
                        AppliedBy NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
                        ScriptContent NVARCHAR(MAX) NULL,
                        ExecutionTimeMs INT NULL
                    );
                    CREATE INDEX IX_SchemaVersions_AppliedDate ON __SchemaVersions(AppliedDate DESC);
                ");
                _logger.LogInformation("マイグレーション履歴テーブルを直接作成しました");
            }
        }
    }
    
    /// <summary>
    /// マイグレーションスクリプトの適用（最適化版）
    /// </summary>
    private async Task<List<string>> ApplyMigrationsAsync(SqlConnection connection, InitializationResult result = null)
    {
        var appliedMigrations = new List<string>();
        
        try
        {
            // マイグレーションフォルダのパスを取得
            var migrationsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                "../../../../../", MigrationsFolderPath);
            
            if (!Directory.Exists(migrationsPath))
            {
                _logger.LogWarning("マイグレーションフォルダが見つかりません: {Path}", migrationsPath);
                return appliedMigrations;
            }
            
            // 適用済みのマイグレーションIDを取得
            var appliedMigrationIds = await GetAppliedMigrationIdsAsync(connection);
            
            // 明示的な実行順序でマイグレーションを適用
            _logger.LogInformation("定義済みマイグレーション順序で実行します: {Count}件", _migrationOrder.Count);
            
            foreach (var migrationFileName in _migrationOrder)
            {
                var migrationPath = Path.Combine(migrationsPath, migrationFileName);
                
                // ファイルが存在しない場合はスキップ
                if (!File.Exists(migrationPath))
                {
                    _logger.LogDebug("マイグレーションファイルが見つかりません: {FileName}", migrationFileName);
                    continue;
                }
                
                // 既に適用済みの場合はスキップ
                if (appliedMigrationIds.Contains(migrationFileName))
                {
                    _logger.LogDebug("マイグレーション {MigrationId} は既に適用済みです", migrationFileName);
                    continue;
                }
                
                // マイグレーションを実行
                var (success, executionTime) = await ApplyMigrationAsync(connection, migrationPath, migrationFileName);
                if (success)
                {
                    appliedMigrations.Add(migrationFileName);
                    result?.MigrationExecutionTimes.Add(migrationFileName, executionTime);
                }
                else
                {
                    _logger.LogError("マイグレーション {MigrationId} の実行に失敗しました", migrationFileName);
                    // 重要なマイグレーションが失敗した場合は処理を中断
                    break;
                }
            }
            
            // 順序リストにない追加のマイグレーションファイルをチェック（意図的除外ファイルは警告対象外）
            var excludedFromWarning = new[] { "024_CreateProductMaster.sql" }; // 意図的除外リスト
            
            var allMigrationFiles = Directory.GetFiles(migrationsPath, "*.sql")
                .Select(f => Path.GetFileName(f))
                .Where(f => !_migrationOrder.Contains(f))
                .OrderBy(f => f)
                .ToList();
            
            // 警告対象ファイルのフィルタリング
            var warningFiles = allMigrationFiles.Where(f => !excludedFromWarning.Contains(f)).ToList();
            
            if (warningFiles.Any())
            {
                _logger.LogWarning("順序リストにない追加のマイグレーションファイルが見つかりました: {Files}", 
                    string.Join(", ", warningFiles));
            }
            
            // 除外ファイルがある場合は詳細ログ（Debugレベル）
            var excludedFiles = allMigrationFiles.Where(f => excludedFromWarning.Contains(f)).ToList();
            if (excludedFiles.Any())
            {
                _logger.LogDebug("意図的に除外されたマイグレーションファイル: {Files}", 
                    string.Join(", ", excludedFiles));
            }
            
            // 未適用のマイグレーションファイルを実行（除外ファイルを含む）
            foreach (var fileName in allMigrationFiles)
            {
                if (!appliedMigrationIds.Contains(fileName))
                {
                    var filePath = Path.Combine(migrationsPath, fileName);
                    var (success, executionTime) = await ApplyMigrationAsync(connection, filePath, fileName);
                    if (success)
                    {
                        appliedMigrations.Add(fileName);
                        result?.MigrationExecutionTimes.Add(fileName, executionTime);
                    }
                }
            }
            
            _logger.LogInformation("{Count} 個のマイグレーションを適用しました", appliedMigrations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "マイグレーション適用中にエラーが発生しました");
            throw;
        }
        
        return appliedMigrations;
    }
    
    /// <summary>
    /// 適用済みのマイグレーションIDを取得
    /// </summary>
    private async Task<HashSet<string>> GetAppliedMigrationIdsAsync(SqlConnection connection)
    {
        try
        {
            var sql = $"SELECT MigrationId FROM {MigrationHistoryTable}";
            var ids = await connection.QueryAsync<string>(sql);
            return new HashSet<string>(ids);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "適用済みマイグレーションの取得に失敗しました");
            return new HashSet<string>();
        }
    }
    
    /// <summary>
    /// 個別のマイグレーションを適用
    /// </summary>
    private async Task<(bool success, long executionTimeMs)> ApplyMigrationAsync(SqlConnection connection, string filePath, string migrationId)
    {
        var stopwatch = Stopwatch.StartNew();
        SqlTransaction transaction = null;
        
        try
        {
            _logger.LogInformation("マイグレーション実行中: {MigrationId}", migrationId);
            
            // トランザクション開始
            transaction = connection.BeginTransaction();
            
            // SQLファイルを実行
            await ExecuteSqlFileAsync(connection, filePath, migrationId, transaction);
            
            // マイグレーション履歴に記録
            await RecordMigrationAsync(connection, migrationId, stopwatch.ElapsedMilliseconds, transaction);
            
            // コミット
            transaction.Commit();
            
            _logger.LogInformation("マイグレーション完了: {MigrationId} ({ElapsedMs}ms)", 
                migrationId, stopwatch.ElapsedMilliseconds);
            
            return (true, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "マイグレーションエラー: {MigrationId}", migrationId);
            
            // ロールバック
            try
            {
                transaction?.Rollback();
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "ロールバックエラー");
            }
            
            return (false, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            transaction?.Dispose();
        }
    }
    
    /// <summary>
    /// SQLファイルを実行（GOステートメントで分割）
    /// </summary>
    private async Task ExecuteSqlFileAsync(SqlConnection connection, string filePath, string fileName, 
        SqlTransaction transaction = null)
    {
        try
        {
            var script = await File.ReadAllTextAsync(filePath);
            
            // GOステートメントで分割（正規表現を使用）
            // 行の先頭にあるGO（前後の空白を許可、大文字小文字を区別しない）を区切りとする
            var regex = new System.Text.RegularExpressions.Regex(
                @"^\s*GO\s*$", 
                System.Text.RegularExpressions.RegexOptions.Multiline | 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            var batches = regex.Split(script);
            
            // InfoMessageイベントハンドラを一度だけ登録
            SqlInfoMessageEventHandler infoMessageHandler = (sender, e) =>
            {
                foreach (SqlError error in e.Errors)
                {
                    _logger.LogInformation("[SQL] {Message}", error.Message);
                }
            };
            connection.InfoMessage += infoMessageHandler;
            
            try
            {
                foreach (var batch in batches)
                {
                    if (!string.IsNullOrWhiteSpace(batch))
                    {
                        if (transaction != null)
                        {
                            await connection.ExecuteAsync(batch, transaction: transaction);
                        }
                        else
                        {
                            await connection.ExecuteAsync(batch);
                        }
                    }
                }
            }
            finally
            {
                // イベントハンドラを確実に削除
                connection.InfoMessage -= infoMessageHandler;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQLファイル実行エラー: {FileName}", fileName);
            throw;
        }
    }
    
    /// <summary>
    /// マイグレーション履歴に記録
    /// </summary>
    private async Task RecordMigrationAsync(SqlConnection connection, string migrationId, 
        long executionTimeMs, SqlTransaction transaction)
    {
        var sql = $@"
            INSERT INTO {MigrationHistoryTable} 
            (MigrationId, AppliedDate, AppliedBy, ExecutionTimeMs)
            VALUES 
            (@MigrationId, GETDATE(), SYSTEM_USER, @ExecutionTimeMs)";
        
        await connection.ExecuteAsync(sql, 
            new { MigrationId = migrationId, ExecutionTimeMs = executionTimeMs }, 
            transaction);
    }
    
    /// <summary>
    /// データベース構造の検証
    /// </summary>
    private async Task<DatabaseValidationResult> ValidateDatabaseStructureAsync(SqlConnection connection)
    {
        var result = new DatabaseValidationResult();
        
        try
        {
            _logger.LogInformation("データベース構造の検証を開始します");
            
            // 1. 必須テーブルの存在確認
            foreach (var tableName in RequiredTables)
            {
                var exists = await TableExistsAsync(connection, tableName);
                if (!exists)
                {
                    result.MissingTables.Add(tableName);
                    result.Errors.Add($"必須テーブル {tableName} が存在しません");
                }
            }
            
            // 2. マイグレーション履歴テーブルの確認
            var migrationHistoryExists = await TableExistsAsync(connection, MigrationHistoryTable);
            if (!migrationHistoryExists)
            {
                result.Errors.Add($"マイグレーション履歴テーブル {MigrationHistoryTable} が存在しません");
            }
            
            // 3. 重要なインデックスの確認
            var indexValidation = await ValidateIndexesAsync(connection);
            result.MissingIndexes.AddRange(indexValidation.missingIndexes);
            result.Warnings.AddRange(indexValidation.warnings);
            
            // 4. データ整合性チェック
            var dataValidation = await ValidateDataIntegrityAsync(connection);
            result.DataIntegrityIssues.AddRange(dataValidation);
            
            result.IsValid = result.Errors.Count == 0;
            
            _logger.LogInformation("データベース構造検証完了 - エラー: {ErrorCount}件, 警告: {WarningCount}件", 
                result.Errors.Count, result.Warnings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データベース構造検証中にエラーが発生しました");
            result.Errors.Add($"検証エラー: {ex.Message}");
            result.IsValid = false;
        }
        
        return result;
    }
    
    /// <summary>
    /// インデックスの検証
    /// </summary>
    private async Task<(List<string> missingIndexes, List<string> warnings)> ValidateIndexesAsync(SqlConnection connection)
    {
        var missingIndexes = new List<string>();
        var warnings = new List<string>();
        
        try
        {
            foreach (var tableIndexes in _indexDefinitions)
            {
                var tableName = tableIndexes.Key;
                
                // テーブルが存在しない場合はスキップ
                if (!await TableExistsAsync(connection, tableName))
                {
                    continue;
                }
                
                foreach (var indexSql in tableIndexes.Value)
                {
                    // CREATE INDEX IX_TableName_ColumnName から インデックス名を抽出
                    var indexNameMatch = System.Text.RegularExpressions.Regex.Match(indexSql, @"CREATE\s+(?:UNIQUE\s+)?INDEX\s+(\w+)");
                    if (indexNameMatch.Success)
                    {
                        var indexName = indexNameMatch.Groups[1].Value;
                        var exists = await IndexExistsAsync(connection, indexName);
                        if (!exists)
                        {
                            missingIndexes.Add($"{tableName}.{indexName}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"インデックス検証エラー: {ex.Message}");
        }
        
        return (missingIndexes, warnings);
    }
    
    /// <summary>
    /// インデックスの存在確認
    /// </summary>
    private async Task<bool> IndexExistsAsync(SqlConnection connection, string indexName)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM sys.indexes 
            WHERE name = @IndexName";
        
        try
        {
            var count = await connection.ExecuteScalarAsync<int>(sql, new { IndexName = indexName });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "インデックス存在確認エラー: {IndexName}", indexName);
            return false;
        }
    }
    
    /// <summary>
    /// データ整合性の検証
    /// </summary>
    private async Task<List<string>> ValidateDataIntegrityAsync(SqlConnection connection)
    {
        var issues = new List<string>();
        
        try
        {
            // 1. 孤立したDataSetManagementレコードの確認
            if (await TableExistsAsync(connection, "DataSetManagement") && 
                await TableExistsAsync(connection, "InventoryMaster"))
            {
                var orphanedDataSets = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) 
                    FROM DataSetManagement dsm
                    WHERE NOT EXISTS (
                        SELECT 1 FROM InventoryMaster im 
                        WHERE im.DataSetId = dsm.DataSetId
                    )");
                
                if (orphanedDataSets > 0)
                {
                    issues.Add($"孤立したDataSetManagementレコードが{orphanedDataSets}件存在します");
                }
            }
            
            // 2. 重複したマイグレーション記録の確認
            if (await TableExistsAsync(connection, MigrationHistoryTable))
            {
                var duplicateMigrations = await connection.ExecuteScalarAsync<int>($@"
                    SELECT COUNT(*) - COUNT(DISTINCT MigrationId) 
                    FROM {MigrationHistoryTable}");
                
                if (duplicateMigrations > 0)
                {
                    issues.Add($"重複したマイグレーション記録が{duplicateMigrations}件存在します");
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add($"データ整合性検証エラー: {ex.Message}");
        }
        
        return issues;
    }
    
    /// <summary>
    /// 初期化結果のサマリーをログ出力
    /// </summary>
    private void LogInitializationSummary(InitializationResult result)
    {
        _logger.LogInformation("=== データベース初期化サマリー ===");
        _logger.LogInformation("実行時間: {ExecutionTime}", result.ExecutionTime);
        _logger.LogInformation("成功: {Success}", result.Success);
        _logger.LogInformation("実行されたマイグレーション: {Count}件", result.ExecutedMigrations.Count);
        
        if (result.ExecutedMigrations.Any())
        {
            _logger.LogInformation("適用されたマイグレーション:");
            foreach (var migration in result.ExecutedMigrations)
            {
                _logger.LogInformation("  - {Migration}", migration);
            }
        }
        
        if (result.CreatedTables.Any())
        {
            _logger.LogInformation("作成されたテーブル: {Tables}", string.Join(", ", result.CreatedTables));
        }
        
        if (result.Errors.Any())
        {
            _logger.LogError("エラー: {Count}件", result.Errors.Count);
            foreach (var error in result.Errors)
            {
                _logger.LogError("  - {Error}", error);
            }
        }
        
        if (result.Warnings.Any())
        {
            _logger.LogWarning("警告: {Count}件", result.Warnings.Count);
            foreach (var warning in result.Warnings)
            {
                _logger.LogWarning("  - {Warning}", warning);
            }
        }
        
        _logger.LogInformation("==============================");
    }
}