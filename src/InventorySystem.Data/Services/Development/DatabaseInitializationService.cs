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
        
        ["DatasetManagement"] = @"
            CREATE TABLE DatasetManagement (
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
        ["DatasetManagement"] = new List<string>
        {
            "CREATE INDEX IX_DatasetManagement_JobDate ON DatasetManagement(JobDate)"
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
        var result = new InitializationResult();
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
            }
            
            // マイグレーション履歴テーブルの確認・作成
            await EnsureMigrationHistoryTableExistsAsync(connection);
            
            // マイグレーションスクリプトの実行
            var executedMigrations = await ApplyMigrationsAsync(connection);
            result.ExecutedMigrations = executedMigrations;
            
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
                "DatasetManagement",
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
    /// マイグレーションスクリプトの適用
    /// </summary>
    private async Task<List<string>> ApplyMigrationsAsync(SqlConnection connection)
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
            
            // すべての.sqlファイルを取得し、ファイル名でソート
            var migrationFiles = Directory.GetFiles(migrationsPath, "*.sql")
                .OrderBy(f => Path.GetFileName(f))
                .ToList();
            
            _logger.LogInformation("{Count} 個のマイグレーションファイルが見つかりました", migrationFiles.Count);
            
            foreach (var filePath in migrationFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var migrationId = fileName;
                
                // 既に適用済みの場合はスキップ
                if (appliedMigrationIds.Contains(migrationId))
                {
                    _logger.LogDebug("マイグレーション {MigrationId} は既に適用済みです", migrationId);
                    continue;
                }
                
                // マイグレーションを実行
                var success = await ApplyMigrationAsync(connection, filePath, migrationId);
                if (success)
                {
                    appliedMigrations.Add(migrationId);
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
    private async Task<bool> ApplyMigrationAsync(SqlConnection connection, string filePath, string migrationId)
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
            
            return true;
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
            
            return false;
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
}