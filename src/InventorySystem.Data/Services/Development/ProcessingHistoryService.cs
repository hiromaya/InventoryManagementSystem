using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces.Development;

namespace InventorySystem.Data.Services.Development;

/// <summary>
/// 処理履歴サービス
/// </summary>
public class ProcessingHistoryService : IProcessingHistoryService
{
    private readonly string _connectionString;
    private readonly ILogger<ProcessingHistoryService> _logger;
    
    public ProcessingHistoryService(string connectionString, ILogger<ProcessingHistoryService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<bool> IsFileProcessedAsync(string fileName, string fileHash)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            const string sql = @"
                SELECT COUNT(*) 
                FROM FileProcessingHistory 
                WHERE FileName = @FileName AND FileHash = @FileHash";
            
            var count = await connection.ExecuteScalarAsync<int>(sql, new { FileName = fileName, FileHash = fileHash });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ファイル処理履歴確認エラー: {FileName}", fileName);
            return false;
        }
    }
    
    public async Task<bool> IsDateProcessedAsync(string fileName, DateTime jobDate, string processType, string department = "DeptA")
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            const string sql = @"
                SELECT COUNT(*) 
                FROM DateProcessingHistory dph
                INNER JOIN FileProcessingHistory fph ON dph.FileHistoryId = fph.Id
                WHERE fph.FileName = @FileName 
                    AND dph.JobDate = @JobDate 
                    AND dph.ProcessType = @ProcessType 
                    AND dph.Department = @Department";
            
            var count = await connection.ExecuteScalarAsync<int>(sql, new 
            { 
                FileName = fileName, 
                JobDate = jobDate, 
                ProcessType = processType, 
                Department = department 
            });
            
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日付処理履歴確認エラー: {FileName}, {JobDate}, {ProcessType}", 
                fileName, jobDate, processType);
            return false;
        }
    }
    
    public async Task<int> RecordFileProcessingAsync(string fileName, string fileHash, long fileSize, string fileType, int totalRecordCount)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            const string sql = @"
                INSERT INTO FileProcessingHistory 
                (FileName, FileHash, FileSize, FirstProcessedAt, LastProcessedAt, TotalRecordCount, FileType)
                VALUES 
                (@FileName, @FileHash, @FileSize, @ProcessedAt, @ProcessedAt, @TotalRecordCount, @FileType);
                SELECT SCOPE_IDENTITY();";
            
            var processedAt = DateTime.Now;
            var id = await connection.ExecuteScalarAsync<int>(sql, new 
            { 
                FileName = fileName,
                FileHash = fileHash,
                FileSize = fileSize,
                ProcessedAt = processedAt,
                TotalRecordCount = totalRecordCount,
                FileType = fileType
            });
            
            _logger.LogInformation("ファイル処理履歴を記録しました: {FileName}, ID={Id}", fileName, id);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ファイル処理履歴記録エラー: {FileName}", fileName);
            throw;
        }
    }
    
    public async Task<int> RecordDateProcessingAsync(int fileHistoryId, DateTime jobDate, int recordCount, 
        string datasetId, string processType, string department = "DeptA", string executedBy = "System")
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            const string sql = @"
                INSERT INTO DateProcessingHistory 
                (FileHistoryId, JobDate, ProcessedAt, RecordCount, DatasetId, ProcessType, Department, ExecutedBy)
                VALUES 
                (@FileHistoryId, @JobDate, @ProcessedAt, @RecordCount, @DatasetId, @ProcessType, @Department, @ExecutedBy);
                SELECT SCOPE_IDENTITY();";
            
            var processedAt = DateTime.Now;
            var id = await connection.ExecuteScalarAsync<int>(sql, new 
            { 
                FileHistoryId = fileHistoryId,
                JobDate = jobDate,
                ProcessedAt = processedAt,
                RecordCount = recordCount,
                DatasetId = datasetId,
                ProcessType = processType,
                Department = department,
                ExecutedBy = executedBy
            });
            
            // 最終処理日時を更新
            await UpdateLastProcessedAt(connection, fileHistoryId, processedAt);
            
            _logger.LogInformation("日付処理履歴を記録しました: FileHistoryId={FileHistoryId}, JobDate={JobDate}, ProcessType={ProcessType}", 
                fileHistoryId, jobDate, processType);
            
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日付処理履歴記録エラー: FileHistoryId={FileHistoryId}, JobDate={JobDate}", 
                fileHistoryId, jobDate);
            throw;
        }
    }
    
    public async Task<List<DateTime>> GetUnprocessedDatesAsync(string fileName, string processType, string department = "DeptA", 
        DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;
            
            // ファイルから抽出可能な全日付を取得（実際のCSVファイルを読み取る必要があるため、
            // ここでは単純に期間内の日付を生成）
            var allDates = new List<DateTime>();
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                allDates.Add(date);
            }
            
            // 処理済み日付を取得
            const string sql = @"
                SELECT DISTINCT dph.JobDate
                FROM DateProcessingHistory dph
                INNER JOIN FileProcessingHistory fph ON dph.FileHistoryId = fph.Id
                WHERE fph.FileName = @FileName 
                    AND dph.ProcessType = @ProcessType 
                    AND dph.Department = @Department
                    AND dph.JobDate BETWEEN @StartDate AND @EndDate";
            
            var processedDates = await connection.QueryAsync<DateTime>(sql, new 
            { 
                FileName = fileName,
                ProcessType = processType,
                Department = department,
                StartDate = start,
                EndDate = end
            });
            
            // 未処理日付を返す
            return allDates.Except(processedDates).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未処理日付取得エラー: {FileName}, {ProcessType}", fileName, processType);
            return new List<DateTime>();
        }
    }
    
    public async Task<int> GetOrCreateFileHistoryAsync(string fileName, string fileHash, long fileSize, string fileType, int totalRecordCount)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            // 既存の履歴を確認
            const string selectSql = @"
                SELECT Id 
                FROM FileProcessingHistory 
                WHERE FileName = @FileName AND FileHash = @FileHash";
            
            var existingId = await connection.ExecuteScalarAsync<int?>(selectSql, new 
            { 
                FileName = fileName, 
                FileHash = fileHash 
            });
            
            if (existingId.HasValue)
            {
                // 最終処理日時を更新
                await UpdateLastProcessedAt(connection, existingId.Value, DateTime.Now);
                return existingId.Value;
            }
            
            // 新規作成
            return await RecordFileProcessingAsync(fileName, fileHash, fileSize, fileType, totalRecordCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ファイル履歴取得・作成エラー: {FileName}", fileName);
            throw;
        }
    }
    
    public async Task<int> CleanupOldHistoryAsync(int retentionDays = 90)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            
            // 古い日付処理履歴を削除
            const string deleteDateSql = @"
                DELETE FROM DateProcessingHistory 
                WHERE ProcessedAt < @CutoffDate";
            
            var deletedDateRecords = await connection.ExecuteAsync(deleteDateSql, new { CutoffDate = cutoffDate });
            
            // 日付処理履歴がないファイル処理履歴を削除
            const string deleteFileSql = @"
                DELETE FROM FileProcessingHistory 
                WHERE NOT EXISTS (
                    SELECT 1 FROM DateProcessingHistory 
                    WHERE FileHistoryId = FileProcessingHistory.Id
                )";
            
            var deletedFileRecords = await connection.ExecuteAsync(deleteFileSql);
            
            _logger.LogInformation("処理履歴クリーンアップ完了: 日付履歴={DateRecords}件, ファイル履歴={FileRecords}件", 
                deletedDateRecords, deletedFileRecords);
            
            return deletedDateRecords + deletedFileRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理履歴クリーンアップエラー");
            throw;
        }
    }
    
    public async Task<ProcessingStatistics> GetProcessingStatisticsAsync(string fileName, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;
            
            const string sql = @"
                SELECT 
                    COUNT(DISTINCT dph.JobDate) as TotalDatesProcessed,
                    SUM(dph.RecordCount) as TotalRecordsProcessed,
                    MIN(dph.ProcessedAt) as FirstProcessedAt,
                    MAX(dph.ProcessedAt) as LastProcessedAt
                FROM DateProcessingHistory dph
                INNER JOIN FileProcessingHistory fph ON dph.FileHistoryId = fph.Id
                WHERE fph.FileName = @FileName 
                    AND dph.JobDate BETWEEN @StartDate AND @EndDate";
            
            var stats = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new 
            { 
                FileName = fileName,
                StartDate = start,
                EndDate = end
            });
            
            const string processTypesSql = @"
                SELECT DISTINCT ProcessType
                FROM DateProcessingHistory dph
                INNER JOIN FileProcessingHistory fph ON dph.FileHistoryId = fph.Id
                WHERE fph.FileName = @FileName 
                    AND dph.JobDate BETWEEN @StartDate AND @EndDate";
            
            var processTypes = await connection.QueryAsync<string>(processTypesSql, new 
            { 
                FileName = fileName,
                StartDate = start,
                EndDate = end
            });
            
            const string departmentsSql = @"
                SELECT DISTINCT Department
                FROM DateProcessingHistory dph
                INNER JOIN FileProcessingHistory fph ON dph.FileHistoryId = fph.Id
                WHERE fph.FileName = @FileName 
                    AND dph.JobDate BETWEEN @StartDate AND @EndDate";
            
            var departments = await connection.QueryAsync<string>(departmentsSql, new 
            { 
                FileName = fileName,
                StartDate = start,
                EndDate = end
            });
            
            return new ProcessingStatistics
            {
                FileName = fileName,
                TotalDatesProcessed = stats?.TotalDatesProcessed ?? 0,
                TotalRecordsProcessed = stats?.TotalRecordsProcessed ?? 0,
                FirstProcessedAt = stats?.FirstProcessedAt,
                LastProcessedAt = stats?.LastProcessedAt,
                ProcessTypes = processTypes.ToList(),
                Departments = departments.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理統計取得エラー: {FileName}", fileName);
            return new ProcessingStatistics { FileName = fileName };
        }
    }
    
    /// <summary>
    /// 最終処理日時を更新
    /// </summary>
    private async Task UpdateLastProcessedAt(SqlConnection connection, int fileHistoryId, DateTime lastProcessedAt)
    {
        const string sql = @"
            UPDATE FileProcessingHistory 
            SET LastProcessedAt = @LastProcessedAt 
            WHERE Id = @Id";
        
        await connection.ExecuteAsync(sql, new 
        { 
            Id = fileHistoryId, 
            LastProcessedAt = lastProcessedAt 
        });
    }
    
    /// <summary>
    /// ファイルハッシュを計算
    /// </summary>
    public static string CalculateFileHash(string filePath)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(fileStream);
            return Convert.ToHexString(hashBytes);
        }
        catch
        {
            // ファイル読み取りエラーの場合は、ファイル名とサイズからハッシュを生成
            var fileInfo = new FileInfo(filePath);
            var data = $"{fileInfo.Name}_{fileInfo.Length}_{fileInfo.LastWriteTime:yyyyMMddHHmmss}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
        }
    }
}