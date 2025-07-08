using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
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
    
    // テーブル定義をコード内に保持（SQLファイル依存を解消）
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
            
            // スキーマ不整合の自動修正（既存テーブルがある場合のみ）
            if (!force)
            {
                _logger.LogInformation("スキーマ不整合をチェック中...");
                await FixSchemaInconsistenciesAsync(connection);
            }
            
            // 強制削除モードの場合、依存関係を考慮した順序で削除
            if (force)
            {
                await DropTablesInOrderAsync(connection);
            }
            
            // 各テーブルをチェックして作成
            foreach (var tableName in RequiredTables)
            {
                try
                {
                    var exists = await TableExistsAsync(connection, tableName);
                    
                    if (!exists)
                    {
                        // テーブル作成
                        await connection.ExecuteAsync(_tableDefinitions[tableName]);
                        result.CreatedTables.Add(tableName);
                        _logger.LogInformation("テーブル {TableName} を作成しました", tableName);
                        
                        // インデックス作成
                        if (_indexDefinitions.ContainsKey(tableName))
                        {
                            foreach (var indexSql in _indexDefinitions[tableName])
                            {
                                await connection.ExecuteAsync(indexSql);
                            }
                            _logger.LogInformation("テーブル {TableName} のインデックスを作成しました", tableName);
                        }
                    }
                    else
                    {
                        result.ExistingTables.Add(tableName);
                        _logger.LogInformation("テーブル {TableName} は既に存在します", tableName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "テーブル {TableName} の作成に失敗しました", tableName);
                    result.FailedTables.Add(tableName);
                    result.Errors.Add($"テーブル {tableName} の作成に失敗: {ex.Message}");
                }
            }
            
            result.Success = result.FailedTables.Count == 0;
            
            if (result.Success)
            {
                _logger.LogInformation("データベース初期化が完了しました。作成: {Created}個、既存: {Existing}個",
                    result.CreatedTables.Count, result.ExistingTables.Count);
            }
            else
            {
                _logger.LogError("データベース初期化で {ErrorCount} 個のエラーが発生しました。失敗: {Failed}個", 
                    result.Errors.Count, result.FailedTables.Count);
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
}