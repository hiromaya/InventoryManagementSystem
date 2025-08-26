using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using InventorySystem.Core.Models;
using InventorySystem.Data.Repositories;

namespace InventorySystem.Data.Services;

public class MasterSyncService : IMasterSyncService
{
    private readonly string _connectionString;
    private readonly ILogger<MasterSyncService> _logger;

    public MasterSyncService(string connectionString, ILogger<MasterSyncService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MasterSyncResult> SyncFromCpInventoryMasterAsync(DateTime jobDate)
    {
        var result = new MasterSyncResult
        {
            Success = true
        };

        try
        {
            _logger.LogInformation("CP在庫マスタから等級・階級マスタへの同期を開始します。JobDate={JobDate}", jobDate);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            
            try
            {
                // 等級マスタへの同期
                result.GradeInserted = await SyncGradeMasterAsync(connection, transaction, jobDate);
                _logger.LogInformation("等級マスタ同期完了: 新規{GradeInserted}件", result.GradeInserted);

                // 階級マスタへの同期
                result.ClassInserted = await SyncClassMasterAsync(connection, transaction, jobDate);
                _logger.LogInformation("階級マスタ同期完了: 新規{ClassInserted}件", result.ClassInserted);

                // CP在庫マスタのGradeName/ClassName更新
                await UpdateCpInventoryMasterNamesAsync(connection, transaction, jobDate);
                _logger.LogInformation("CP在庫マスタの等級名・階級名を更新しました");

                await transaction.CommitAsync();

                _logger.LogInformation("マスタ同期処理が正常に完了しました");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "マスタ同期処理でエラーが発生しました");
        }

        return result;
    }

    private async Task<int> SyncGradeMasterAsync(SqlConnection connection, SqlTransaction transaction, DateTime jobDate)
    {
        const string sql = @"
            INSERT INTO GradeMaster (GradeCode, GradeName, CreatedAt, UpdatedAt)
            SELECT DISTINCT 
                cp.GradeCode,
                ISNULL(MAX(gm.GradeName), 
                       CASE 
                           WHEN cp.GradeCode = '000' THEN '未分類'
                           ELSE 'Grade-' + cp.GradeCode
                       END) as GradeName,
                GETDATE(),
                GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN GradeMaster gm ON gm.GradeCode = cp.GradeCode
            WHERE cp.JobDate = @JobDate
              AND cp.GradeCode IS NOT NULL 
              AND cp.GradeCode != ''
              AND NOT EXISTS (
                SELECT 1 FROM GradeMaster g 
                WHERE g.GradeCode = cp.GradeCode
              )
            GROUP BY cp.GradeCode;";

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@JobDate", jobDate);
        
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<int> SyncClassMasterAsync(SqlConnection connection, SqlTransaction transaction, DateTime jobDate)
    {
        const string sql = @"
            INSERT INTO ClassMaster (ClassCode, ClassName, CreatedAt, UpdatedAt)
            SELECT DISTINCT 
                cp.ClassCode,
                ISNULL(MAX(cm.ClassName), 
                       CASE 
                           WHEN cp.ClassCode = '000' THEN '未分類'
                           ELSE 'Class-' + cp.ClassCode
                       END) as ClassName,
                GETDATE(),
                GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN ClassMaster cm ON cm.ClassCode = cp.ClassCode
            WHERE cp.JobDate = @JobDate
              AND cp.ClassCode IS NOT NULL 
              AND cp.ClassCode != ''
              AND NOT EXISTS (
                SELECT 1 FROM ClassMaster c 
                WHERE c.ClassCode = cp.ClassCode
              )
            GROUP BY cp.ClassCode;";

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@JobDate", jobDate);
        
        return await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateCpInventoryMasterNamesAsync(SqlConnection connection, SqlTransaction transaction, DateTime jobDate)
    {
        const string sql = @"
            UPDATE cp
            SET 
                GradeName = ISNULL(gm.GradeName, 
                    CASE 
                        WHEN cp.GradeCode = '000' THEN '未分類'
                        ELSE 'Grade-' + cp.GradeCode
                    END),
                ClassName = ISNULL(cm.ClassName, 
                    CASE 
                        WHEN cp.ClassCode = '000' THEN '未分類'
                        ELSE 'Class-' + cp.ClassCode
                    END),
                UpdatedDate = GETDATE()
            FROM CpInventoryMaster cp
            LEFT JOIN GradeMaster gm ON gm.GradeCode = cp.GradeCode
            LEFT JOIN ClassMaster cm ON cm.ClassCode = cp.ClassCode
            WHERE cp.JobDate = @JobDate;";

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@JobDate", jobDate);
        
        await command.ExecuteNonQueryAsync();
    }
}