using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 在庫調整リポジトリ実装
/// </summary>
public class InventoryAdjustmentRepository : BaseRepository, IInventoryAdjustmentRepository
{
    public InventoryAdjustmentRepository(string connectionString, ILogger<InventoryAdjustmentRepository> logger)
        : base(connectionString, logger)
    {
    }

    /// <summary>
    /// 在庫調整データを一括挿入
    /// </summary>
    public async Task<int> BulkInsertAsync(IEnumerable<InventoryAdjustment> adjustments)
    {
        const string sql = @"
            INSERT INTO InventoryAdjustments (
                VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                CustomerCode, CustomerName, CategoryCode,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                Quantity, UnitPrice, Amount
            ) VALUES (
                @VoucherId, @LineNumber, @DataSetId, @VoucherNumber, @VoucherDate, @JobDate, @VoucherType, @DetailType,
                @CustomerCode, @CustomerName, @CategoryCode,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ManualShippingMark,
                @Quantity, @UnitPrice, @Amount
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var now = DateTime.Now;
            
            var parameters = adjustments.Select(adj => new
            {
                adj.VoucherId,
                adj.LineNumber,
                adj.DataSetId,
                adj.VoucherNumber,
                adj.VoucherDate,
                adj.JobDate,
                adj.VoucherType,
                adj.DetailType,
                adj.CustomerCode,
                adj.CustomerName,
                adj.CategoryCode,
                adj.ProductCode,
                adj.GradeCode,
                adj.ClassCode,
                adj.ShippingMarkCode,
                adj.ManualShippingMark,
                adj.Quantity,
                adj.UnitPrice,
                adj.Amount
            });

            var insertedCount = await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("在庫調整データ一括挿入完了: {Count}件", insertedCount);
            return insertedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整データ一括挿入エラー");
            throw;
        }
    }

    /// <summary>
    /// データセットIDで在庫調整データを取得
    /// </summary>
    public async Task<IEnumerable<InventoryAdjustment>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   CustomerCode, CustomerName, CategoryCode,
                   ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate
            FROM InventoryAdjustments 
            WHERE DataSetId = @DataSetId
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var adjustments = await connection.QueryAsync<InventoryAdjustment>(sql, new { DataSetId = dataSetId });
            
            return adjustments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット別在庫調整データ取得エラー: {DataSetId}", dataSetId);
            throw;
        }
    }

    /// <summary>
    /// ジョブ日付で在庫調整データを取得
    /// </summary>
    public async Task<IEnumerable<InventoryAdjustment>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   CustomerCode, CustomerName, CategoryCode,
                   ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate
            FROM InventoryAdjustments 
            WHERE JobDate = @JobDate
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var adjustments = await connection.QueryAsync<InventoryAdjustment>(sql, new { JobDate = jobDate.Date });
            
            return adjustments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブ日付別在庫調整データ取得エラー: {JobDate}", jobDate);
            throw;
        }
    }

    /// <summary>
    /// 在庫キーで在庫調整データを取得
    /// </summary>
    public async Task<IEnumerable<InventoryAdjustment>> GetByInventoryKeyAsync(InventoryKey inventoryKey, DateTime jobDate)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   CustomerCode, CustomerName, CategoryCode,
                   ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate
            FROM InventoryAdjustments 
            WHERE ProductCode = @ProductCode 
              AND GradeCode = @GradeCode 
              AND ClassCode = @ClassCode 
              AND ShippingMarkCode = @ShippingMarkCode 
              AND ManualShippingMark = @ManualShippingMark 
              AND JobDate = @JobDate
            ORDER BY VoucherNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var adjustments = await connection.QueryAsync<InventoryAdjustment>(sql, new
            {
                inventoryKey.ProductCode,
                inventoryKey.GradeCode,
                inventoryKey.ClassCode,
                inventoryKey.ShippingMarkCode,
                inventoryKey.ManualShippingMark,
                JobDate = jobDate.Date
            });
            
            return adjustments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫キー別在庫調整データ取得エラー: {InventoryKey}", inventoryKey);
            throw;
        }
    }

    /// <summary>
    /// 在庫調整データを更新
    /// </summary>
    public async Task UpdateAsync(InventoryAdjustment adjustment)
    {
        const string sql = @"
            UPDATE InventoryAdjustments 
            SET VoucherNumber = @VoucherNumber,
                VoucherDate = @VoucherDate,
                JobDate = @JobDate,
                VoucherType = @VoucherType,
                DetailType = @DetailType,
                CustomerCode = @CustomerCode,
                CustomerName = @CustomerName,
                CategoryCode = @CategoryCode,
                ProductCode = @ProductCode,
                GradeCode = @GradeCode,
                ClassCode = @ClassCode,
                ShippingMarkCode = @ShippingMarkCode,
                ManualShippingMark = @ManualShippingMark,
                Quantity = @Quantity,
                UnitPrice = @UnitPrice,
                Amount = @Amount
            WHERE VoucherId = @VoucherId AND LineNumber = @LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var parameters = new
            {
                adjustment.VoucherId,
                adjustment.LineNumber,
                adjustment.VoucherNumber,
                adjustment.VoucherDate,
                adjustment.JobDate,
                adjustment.VoucherType,
                adjustment.DetailType,
                adjustment.CustomerCode,
                adjustment.CustomerName,
                adjustment.CategoryCode,
                adjustment.ProductCode,
                adjustment.GradeCode,
                adjustment.ClassCode,
                adjustment.ShippingMarkCode,
                adjustment.ManualShippingMark,
                adjustment.Quantity,
                adjustment.UnitPrice,
                adjustment.Amount
            };

            var affectedRows = await connection.ExecuteAsync(sql, parameters);
            
            if (affectedRows == 0)
            {
                throw new InvalidOperationException($"在庫調整データが見つかりません: VoucherId={adjustment.VoucherId}, LineNumber={adjustment.LineNumber}");
            }

            _logger.LogInformation("在庫調整データ更新完了: VoucherId={VoucherId}, LineNumber={LineNumber}", adjustment.VoucherId, adjustment.LineNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整データ更新エラー: VoucherId={VoucherId}, LineNumber={LineNumber}", adjustment.VoucherId, adjustment.LineNumber);
            throw;
        }
    }

    /// <summary>
    /// データセットIDで在庫調整データを削除
    /// </summary>
    public async Task DeleteByDataSetIdAsync(string dataSetId)
    {
        const string sql = "DELETE FROM InventoryAdjustments WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var deletedCount = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });
            
            _logger.LogInformation("在庫調整データ削除完了: {DataSetId}, 削除件数: {Count}", dataSetId, deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整データ削除エラー: {DataSetId}", dataSetId);
            throw;
        }
    }

    /// <summary>
    /// 除外フラグを更新
    /// </summary>
    public async Task UpdateExcludeStatusAsync(string voucherId, int lineNumber, bool isExcluded, string? excludeReason)
    {
        const string sql = @"
            UPDATE InventoryAdjustments 
            SET IsExcluded = @IsExcluded, 
                ExcludeReason = @ExcludeReason
            WHERE VoucherId = @VoucherId AND LineNumber = @LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var parameters = new
            {
                VoucherId = voucherId,
                LineNumber = lineNumber,
                IsExcluded = isExcluded,
                ExcludeReason = excludeReason
            };

            await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("在庫調整除外ステータス更新: VoucherId={VoucherId}, LineNumber={LineNumber} -> {IsExcluded}", voucherId, lineNumber, isExcluded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整除外ステータス更新エラー: VoucherId={VoucherId}, LineNumber={LineNumber}", voucherId, lineNumber);
            throw;
        }
    }

    /// <summary>
    /// データセットIDごとの件数を取得
    /// </summary>
    public async Task<int> GetCountByDataSetIdAsync(string dataSetId)
    {
        const string sql = "SELECT COUNT(*) FROM InventoryAdjustments WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var count = await connection.QuerySingleAsync<int>(sql, new { DataSetId = dataSetId });
            
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整データ件数取得エラー: {DataSetId}", dataSetId);
            throw;
        }
    }

    /// <summary>
    /// 商品分類1による集計データを取得
    /// </summary>
    public async Task<IEnumerable<(string ProductCategory1, int Count, decimal TotalAmount)>> GetSummaryByProductCategory1Async(DateTime jobDate)
    {
        const string sql = @"
            SELECT CategoryCode as ProductCategory1, 
                   COUNT(*) as Count, 
                   SUM(Amount) as TotalAmount
            FROM InventoryAdjustments 
            WHERE JobDate = @JobDate
            GROUP BY CategoryCode
            ORDER BY CategoryCode";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var results = await connection.QueryAsync(sql, new { JobDate = jobDate.Date });
            
            return results.Select(r => (
                ProductCategory1: (string)(r.ProductCategory1 ?? "未分類"),
                Count: (int)r.Count,
                TotalAmount: (decimal)r.TotalAmount
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整集計データ取得エラー: {JobDate}", jobDate);
            throw;
        }
    }

    public async Task<int> DeleteByJobDateAsync(DateTime jobDate)
    {
        const string sql = "DELETE FROM InventoryAdjustments WHERE JobDate = @JobDate";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            _logger.LogInformation("Deleted {Count} inventory adjustment records for JobDate: {JobDate}", result, jobDate);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整データ削除エラー: {JobDate}", jobDate);
            throw;
        }
    }
    
    public async Task<int> GetCountAsync(DateTime jobDate)
    {
        const string sql = "SELECT COUNT(*) FROM InventoryAdjustments WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    public async Task<int> GetInventoryAdjustmentCountByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM InventoryAdjustments 
            WHERE JobDate = @jobDate 
            AND CategoryCode IN (1, 4, 6)";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    public async Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM InventoryAdjustments 
            WHERE JobDate = @jobDate 
            AND CreatedDate > @modifiedAfter";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate, modifiedAfter });
    }
    
    /// <summary>
    /// すべての在庫調整データを取得
    /// </summary>
    public async Task<string?> GetDataSetIdByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT TOP 1 DataSetId 
            FROM InventoryAdjustments 
            WHERE JobDate = @jobDate 
            AND DataSetId IS NOT NULL";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { jobDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブ日付でDataSetId取得エラー: {JobDate}", jobDate);
            throw;
        }
    }

    public async Task<IEnumerable<InventoryAdjustment>> GetAllAsync()
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   CustomerCode, CustomerName, CategoryCode,
                   ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate
            FROM InventoryAdjustments 
            ORDER BY JobDate DESC, VoucherDate DESC, VoucherId DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var adjustments = await connection.QueryAsync<InventoryAdjustment>(sql);
            
            var results = adjustments.ToList();
            _logger.LogInformation("Retrieved {Count} inventory adjustments (all records)", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "すべての在庫調整データ取得エラー");
            throw;
        }
    }

    /// <summary>
    /// 指定されたDataSetIdの伝票データのIsActiveフラグを更新
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="isActive">アクティブフラグの値</param>
    /// <returns>更新件数</returns>
    public async Task<int> UpdateIsActiveByDataSetIdAsync(string dataSetId, bool isActive)
    {
        const string sql = @"
            UPDATE InventoryAdjustments 
            SET IsActive = @IsActive, 
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, IsActive = isActive });
            
            _logger.LogInformation("Updated {Count} inventory adjustments IsActive flag", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整IsActiveフラグ更新エラー: DataSetId={DataSetId}, IsActive={IsActive}", dataSetId, isActive);
            throw;
        }
    }

    /// <summary>
    /// アクティブな伝票のみを取得（IsActive = true）
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>アクティブな在庫調整一覧</returns>
    public async Task<IEnumerable<InventoryAdjustment>> GetActiveByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   CustomerCode, CustomerName, CategoryCode,
                   ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, CreatedDate, IsActive
            FROM InventoryAdjustments 
            WHERE JobDate = @jobDate AND IsActive = 1
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var adjustments = await connection.QueryAsync<InventoryAdjustment>(sql, new { jobDate = jobDate.Date });
            
            var results = adjustments.ToList();
            _logger.LogInformation("Retrieved {Count} active inventory adjustments", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アクティブ在庫調整データ取得エラー: JobDate={JobDate}", jobDate);
            throw;
        }
    }

    /// <summary>
    /// 指定されたJobDateの伝票データを無効化
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <param name="excludeDataSetId">除外するDataSetId（nullの場合は除外しない）</param>
    /// <returns>無効化件数</returns>
    public async Task<int> DeactivateByJobDateAsync(DateTime jobDate, string? excludeDataSetId = null)
    {
        string sql = @"
            UPDATE InventoryAdjustments 
            SET IsActive = 0, 
                UpdatedDate = GETDATE()
            WHERE JobDate = @JobDate";

        object parameters;
        
        if (!string.IsNullOrEmpty(excludeDataSetId))
        {
            sql += " AND DataSetId != @ExcludeDataSetId";
            parameters = new { JobDate = jobDate, ExcludeDataSetId = excludeDataSetId };
        }
        else
        {
            parameters = new { JobDate = jobDate };
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("Deactivated {Count} inventory adjustments", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫調整無効化エラー: JobDate={JobDate}, ExcludeDataSetId={ExcludeDataSetId}", jobDate, excludeDataSetId);
            throw;
        }
    }
}