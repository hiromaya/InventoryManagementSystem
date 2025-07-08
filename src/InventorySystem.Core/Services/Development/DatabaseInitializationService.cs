using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces.Development;

namespace InventorySystem.Core.Services.Development;

/// <summary>
/// データベース初期化サービス
/// </summary>
public class DatabaseInitializationService : IDatabaseInitializationService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializationService> _logger;
    
    // 管理対象テーブル
    private readonly string[] _requiredTables = new[]
    {
        "DatasetManagement",
        "ProcessHistory",
        "DailyCloseManagement",
        "AuditLogs"
    };
    
    public DatabaseInitializationService(string connectionString, ILogger<DatabaseInitializationService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<InitializationResult> InitializeDatabaseAsync(bool force = false)
    {
        var result = new InitializationResult();
        
        try
        {
            _logger.LogInformation("データベース初期化を開始します。Force={Force}", force);
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // 既存テーブルの確認
            var existingTables = await GetExistingTablesAsync(connection);
            result.ExistingTables = existingTables.ToList();
            
            if (force)
            {
                // 既存テーブルを削除
                foreach (var table in existingTables.Intersect(_requiredTables))
                {
                    try
                    {
                        await DropTableAsync(connection, table);
                        _logger.LogWarning("テーブル {TableName} を削除しました", table);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "テーブル {TableName} の削除に失敗しました", table);
                        result.Errors.Add($"テーブル {table} の削除に失敗: {ex.Message}");
                    }
                }
                result.ExistingTables.Clear();
            }
            
            // 不足テーブルの作成
            var missingTables = _requiredTables.Except(result.ExistingTables).ToList();
            foreach (var table in missingTables)
            {
                try
                {
                    await CreateTableAsync(connection, table);
                    result.CreatedTables.Add(table);
                    _logger.LogInformation("テーブル {TableName} を作成しました", table);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "テーブル {TableName} の作成に失敗しました", table);
                    result.Errors.Add($"テーブル {table} の作成に失敗: {ex.Message}");
                }
            }
            
            result.Success = result.Errors.Count == 0;
            
            if (result.Success)
            {
                _logger.LogInformation("データベース初期化が完了しました。作成: {Created}個、既存: {Existing}個",
                    result.CreatedTables.Count, result.ExistingTables.Count);
            }
            else
            {
                _logger.LogError("データベース初期化で {ErrorCount} 個のエラーが発生しました", result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データベース初期化で予期しないエラーが発生しました");
            result.Success = false;
            result.Errors.Add($"予期しないエラー: {ex.Message}");
        }
        
        return result;
    }
    
    public async Task<bool> CheckTablesExistAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var existingTables = await GetExistingTablesAsync(connection);
            return _requiredTables.All(t => existingTables.Contains(t));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "テーブル存在確認でエラーが発生しました");
            return false;
        }
    }
    
    public async Task<List<string>> GetMissingTablesAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var existingTables = await GetExistingTablesAsync(connection);
            return _requiredTables.Except(existingTables).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "不足テーブルの確認でエラーが発生しました");
            return _requiredTables.ToList(); // エラー時は全テーブルを不足として返す
        }
    }
    
    private async Task<IEnumerable<string>> GetExistingTablesAsync(SqlConnection connection)
    {
        const string sql = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = 'dbo' 
                AND TABLE_TYPE = 'BASE TABLE'
                AND TABLE_NAME IN @TableNames";
        
        return await connection.QueryAsync<string>(sql, new { TableNames = _requiredTables });
    }
    
    private async Task DropTableAsync(SqlConnection connection, string tableName)
    {
        var sql = $"DROP TABLE IF EXISTS [dbo].[{tableName}]";
        await connection.ExecuteAsync(sql);
    }
    
    private async Task CreateTableAsync(SqlConnection connection, string tableName)
    {
        var sql = tableName switch
        {
            "DatasetManagement" => GetDatasetManagementTableSql(),
            "ProcessHistory" => GetProcessHistoryTableSql(),
            "DailyCloseManagement" => GetDailyCloseManagementTableSql(),
            "AuditLogs" => GetAuditLogsTableSql(),
            _ => throw new NotSupportedException($"テーブル {tableName} の作成SQLが定義されていません")
        };
        
        await connection.ExecuteAsync(sql);
    }
    
    private string GetDatasetManagementTableSql() => @"
        CREATE TABLE [dbo].[DatasetManagement] (
            [DatasetId] NVARCHAR(50) NOT NULL PRIMARY KEY,
            [JobDate] DATE NOT NULL,
            [ProcessType] NVARCHAR(50) NOT NULL,
            [ImportedFiles] NVARCHAR(MAX),
            [TotalRecordCount] INT NOT NULL DEFAULT 0,
            [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
            [CreatedBy] NVARCHAR(50) NOT NULL,
            INDEX IX_DatasetManagement_JobDate (JobDate)
        )";
    
    private string GetProcessHistoryTableSql() => @"
        CREATE TABLE [dbo].[ProcessHistory] (
            [ProcessId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            [JobDate] DATE NOT NULL,
            [ProcessType] NVARCHAR(50) NOT NULL,
            [DatasetId] NVARCHAR(50),
            [StartTime] DATETIME2 NOT NULL,
            [EndTime] DATETIME2,
            [Status] NVARCHAR(20) NOT NULL,
            [Message] NVARCHAR(MAX),
            [ProcessedBy] NVARCHAR(50) NOT NULL,
            [RecordCount] INT,
            [ErrorCount] INT,
            INDEX IX_ProcessHistory_JobDate_ProcessType (JobDate, ProcessType)
        )";
    
    private string GetDailyCloseManagementTableSql() => @"
        CREATE TABLE [dbo].[DailyCloseManagement] (
            [JobDate] DATE NOT NULL PRIMARY KEY,
            [DatasetId] NVARCHAR(50) NOT NULL,
            [DailyReportDatasetId] NVARCHAR(50),
            [ProcessedAt] DATETIME2,
            [ProcessedBy] NVARCHAR(50),
            [ValidationStatus] NVARCHAR(20),
            [ValidationMessage] NVARCHAR(MAX),
            [DataHash] NVARCHAR(100),
            [UpdatedInventoryCount] INT DEFAULT 0,
            [BackupPath] NVARCHAR(500),
            [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
            INDEX IX_DailyCloseManagement_DatasetId (DatasetId)
        )";
    
    private string GetAuditLogsTableSql() => @"
        CREATE TABLE [dbo].[AuditLogs] (
            [LogId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            [LogDate] DATETIME2 NOT NULL DEFAULT GETDATE(),
            [JobDate] DATE,
            [ProcessType] NVARCHAR(50) NOT NULL,
            [Action] NVARCHAR(100) NOT NULL,
            [TableName] NVARCHAR(100),
            [RecordKey] NVARCHAR(200),
            [OldValue] NVARCHAR(MAX),
            [NewValue] NVARCHAR(MAX),
            [User] NVARCHAR(50) NOT NULL,
            [IPAddress] NVARCHAR(50),
            [Comment] NVARCHAR(MAX),
            INDEX IX_AuditLogs_LogDate (LogDate),
            INDEX IX_AuditLogs_JobDate_ProcessType (JobDate, ProcessType)
        )";
}