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
                Message NVARCHAR(MAX),
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
            
            // 各テーブルをチェックして作成
            foreach (var tableName in RequiredTables)
            {
                try
                {
                    var exists = await TableExistsAsync(connection, tableName);
                    
                    if (!exists || force)
                    {
                        if (exists && force)
                        {
                            await connection.ExecuteAsync($"DROP TABLE IF EXISTS {tableName}");
                            _logger.LogInformation("既存テーブル {TableName} を削除しました", tableName);
                        }
                        
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
}