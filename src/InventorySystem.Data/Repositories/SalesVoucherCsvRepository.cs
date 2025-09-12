using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 売上伝票CSV取込用リポジトリ実装
/// </summary>
public class SalesVoucherCsvRepository : BaseRepository, ISalesVoucherRepository
{
    public SalesVoucherCsvRepository(string connectionString, ILogger<SalesVoucherCsvRepository> logger)
        : base(connectionString, logger)
    {
    }

    /// <summary>
    /// 売上伝票データを一括挿入
    /// </summary>
    public async Task<int> BulkInsertAsync(IEnumerable<SalesVoucher> vouchers)
    {
        const string sql = @"
            INSERT INTO SalesVouchers (
                VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                CustomerCode, CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                Quantity, UnitPrice, Amount, InventoryUnitPrice, GrossProfit, CreatedDate, DataSetId
            ) VALUES (
                @VoucherId, @LineNumber, @VoucherNumber, @VoucherDate, @JobDate, @VoucherType, @DetailType,
                @CustomerCode, @CustomerName, @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ManualShippingMark,
                @Quantity, @UnitPrice, @Amount, @InventoryUnitPrice, @GrossProfit, @CreatedDate, @DataSetId
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var now = DateTime.Now;
            
            // デバッグログ追加: 保存前のデータ確認
            var voucherList = vouchers.ToList();
            _logger.LogDebug("BulkInsert開始: {Count}件", voucherList.Count);
            
            // 最初の5件のJobDateを確認
            foreach (var voucher in voucherList.Take(5))
            {
                _logger.LogDebug("保存データ: VoucherNumber={Number}, JobDate={JobDate:yyyy-MM-dd}, VoucherDate={VoucherDate:yyyy-MM-dd}",
                    voucher.VoucherNumber, voucher.JobDate, voucher.VoucherDate);
            }
            
            // JobDateの分布を確認
            var jobDateGroups = voucherList.GroupBy(v => v.JobDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() });
            foreach (var group in jobDateGroups)
            {
                _logger.LogInformation("JobDate分布: {Date:yyyy-MM-dd} = {Count}件", group.Date, group.Count);
            }
            
            // LineNumberはすでにToEntityで設定されているため、そのまま使用
            var parameters = voucherList.Select(voucher => new
            {
                voucher.VoucherId,  // すでに正しく設定されている
                voucher.LineNumber, // すでに正しく設定されている
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.JobDate,
                voucher.VoucherType,
                voucher.DetailType,
                voucher.CustomerCode,
                voucher.CustomerName,
                voucher.ProductCode,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ManualShippingMark,
                voucher.Quantity,
                voucher.UnitPrice,
                voucher.Amount,
                InventoryUnitPrice = voucher.InventoryUnitPrice,
                voucher.GrossProfit,  // 粗利益を追加（初期値はnull）
                CreatedDate = now,
                voucher.DataSetId
            });

            // 文字化け調査用: 最初の5件のデータ状態をログ出力
            var sampleVouchers = vouchers.Take(5).ToList();
            foreach (var (voucher, index) in sampleVouchers.Select((v, i) => (v, i)))
            {
                _logger.LogDebug("DB保存前 行{Index}: 得意先名='{CustomerName}', 荷印名='{ManualShippingMark}', 商品名='{ProductName}'", 
                    index + 1, voucher.CustomerName, voucher.ManualShippingMark, voucher.ProductName);
                
                if (!string.IsNullOrEmpty(voucher.CustomerName))
                {
                    _logger.LogDebug("DB保存前 得意先名バイト列: {Bytes}", BitConverter.ToString(Encoding.UTF8.GetBytes(voucher.CustomerName)));
                }
            }

            var insertedCount = await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("売上伝票データ一括挿入完了: {Count}件", insertedCount);
            return insertedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "売上伝票データ一括挿入エラー");
            throw;
        }
    }

    /// <summary>
    /// データセットIDで売上伝票データを取得
    /// </summary>
    public async Task<IEnumerable<SalesVoucher>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType, CustomerCode,
                   CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, InventoryUnitPrice, GrossProfit, CreatedDate
            FROM SalesVouchers 
            WHERE DataSetId = @DataSetId
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<SalesVoucher>(sql, new { DataSetId = dataSetId });
            
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データセット別売上伝票データ取得エラー: {DataSetId}", dataSetId);
            throw;
        }
    }

    /// <summary>
    /// ジョブ日付で売上伝票データを取得
    /// </summary>
    public async Task<IEnumerable<SalesVoucher>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, DataSetId, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType, CustomerCode,
                   CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, InventoryUnitPrice, GrossProfit, CreatedDate
            FROM SalesVouchers 
            WHERE JobDate = @JobDate
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<SalesVoucher>(sql, new { JobDate = jobDate.Date });
            
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブ日付別売上伝票データ取得エラー: {JobDate}", jobDate);
            throw;
        }
    }

    // 以下は互換性のためのメソッド（旧インターフェース対応）
    public async Task<int> CreateAsync(SalesVoucher voucher)
    {
        return await BulkInsertAsync(new[] { voucher });
    }

    public async Task UpdateAsync(SalesVoucher voucher)
    {
        const string sql = @"
            UPDATE SalesVouchers 
            SET VoucherNumber = @VoucherNumber,
                VoucherDate = @VoucherDate,
                JobDate = @JobDate,
                VoucherType = @VoucherType,
                DetailType = @DetailType,
                CustomerCode = @CustomerCode,
                CustomerName = @CustomerName,
                ProductCode = @ProductCode,
                GradeCode = @GradeCode,
                ClassCode = @ClassCode,
                ShippingMarkCode = @ShippingMarkCode,
                ManualShippingMark = @ManualShippingMark,
                Quantity = @Quantity,
                UnitPrice = @UnitPrice,
                Amount = @Amount,
                InventoryUnitPrice = @InventoryUnitPrice
            WHERE VoucherId = @VoucherId AND LineNumber = @LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var parameters = new
            {
                voucher.VoucherId,
                voucher.LineNumber,
                voucher.VoucherNumber,
                voucher.VoucherDate,
                voucher.JobDate,
                voucher.VoucherType,
                voucher.DetailType,
                voucher.CustomerCode,
                voucher.CustomerName,
                voucher.ProductCode,
                voucher.GradeCode,
                voucher.ClassCode,
                voucher.ShippingMarkCode,
                voucher.ManualShippingMark,
                voucher.Quantity,
                voucher.UnitPrice,
                voucher.Amount,
                voucher.InventoryUnitPrice
            };

            await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("売上伝票データ更新完了: {VoucherId}-{LineNumber}", voucher.VoucherId, voucher.LineNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "売上伝票データ更新エラー: {VoucherId}-{LineNumber}", voucher.VoucherId, voucher.LineNumber);
            throw;
        }
    }

    public async Task DeleteAsync(long id)
    {
        // Note: This method signature uses long id for compatibility, but we need VoucherId and LineNumber
        // This is a limitation of the interface - consider updating interface to accept composite key
        const string sql = "DELETE FROM SalesVouchers WHERE VoucherId = @VoucherId AND LineNumber = @LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, new { Id = id });
            
            _logger.LogInformation("売上伝票データ削除完了: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "売上伝票データ削除エラー: {Id}", id);
            throw;
        }
    }

    public async Task<int> DeleteByJobDateAsync(DateTime jobDate)
    {
        const string sql = "DELETE FROM SalesVouchers WHERE JobDate = @JobDate";
        
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { JobDate = jobDate });
            
            LogInfo($"Deleted {result} sales voucher records", new { jobDate });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeleteByJobDateAsync), new { jobDate });
            throw;
        }
    }
    
    public async Task<int> GetCountAsync(DateTime jobDate)
    {
        const string sql = "SELECT COUNT(*) FROM SalesVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    public async Task<int> GetCountByJobDateAsync(DateTime jobDate)
    {
        const string sql = "SELECT COUNT(*) FROM SalesVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate });
    }
    
    public async Task<decimal> GetTotalAmountAsync(DateTime jobDate)
    {
        const string sql = "SELECT ISNULL(SUM(Amount), 0) FROM SalesVouchers WHERE JobDate = @jobDate";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(sql, new { jobDate });
    }
    
    public async Task<int> GetModifiedAfterAsync(DateTime jobDate, DateTime modifiedAfter)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM SalesVouchers 
            WHERE JobDate = @jobDate 
            AND CreatedDate > @modifiedAfter";
        
        using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { jobDate, modifiedAfter });
    }

    public async Task<string?> GetDataSetIdByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT TOP 1 DataSetId 
            FROM SalesVouchers 
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
    
    /// <summary>
    /// すべての売上伝票データを取得
    /// </summary>
    public async Task<IEnumerable<SalesVoucher>> GetAllAsync()
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   CustomerCode, CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, InventoryUnitPrice, CreatedDate, DataSetId
            FROM SalesVouchers
            ORDER BY JobDate DESC, VoucherDate DESC, VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<SalesVoucher>(sql);
            
            _logger.LogInformation("すべての売上伝票データを取得しました: {Count}件", vouchers.Count());
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "すべての売上伝票データの取得中にエラーが発生しました");
            throw;
        }
    }
    
    /// <summary>
    /// Process 2-5: JobDateとDataSetIdで売上伝票を取得
    /// </summary>
    public async Task<IEnumerable<SalesVoucher>> GetByJobDateAndDataSetIdAsync(DateTime jobDate, string dataSetId)
    {
        // CSV用リポジトリでもデータベースからの読み込みを行う
        _logger.LogInformation("CSV用リポジトリでProcess 2-5用データを取得します: JobDate={JobDate:yyyy-MM-dd}, DataSetId={DataSetId}", jobDate, dataSetId);
        
        const string sql = @"
            SELECT 
                VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                CustomerCode, CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                Quantity, UnitPrice, Amount, InventoryUnitPrice, GrossProfit, WalkingDiscount, CreatedDate, DataSetId
            FROM SalesVouchers 
            WHERE JobDate = @JobDate 
                AND DataSetId = @DataSetId
                AND VoucherType IN ('51', '52')
                AND ProductCode != '00000'
            ORDER BY VoucherNumber, LineNumber";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<SalesVoucher>(sql, new { JobDate = jobDate, DataSetId = dataSetId });
            
            _logger.LogInformation("Process 2-5用データ取得完了: {Count}件", vouchers.Count());
            return vouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Process 2-5用データ取得エラー: JobDate={JobDate:yyyy-MM-dd}, DataSetId={DataSetId}", jobDate, dataSetId);
            throw;
        }
    }
    
    /// <summary>
    /// Process 2-5: 売上伝票の在庫単価と粗利益をバッチ更新
    /// </summary>
    public async Task<int> UpdateInventoryUnitPriceAndGrossProfitBatchAsync(IEnumerable<SalesVoucher> vouchers)
    {
        _logger.LogInformation("CSV用リポジトリで売上伝票の在庫単価と粗利益を更新します: {Count}件", vouchers.Count());
        
        const string updateSql = @"
            UPDATE SalesVouchers 
            SET 
                InventoryUnitPrice = @InventoryUnitPrice,
                GrossProfit = @GrossProfit,
                WalkingDiscount = @WalkingDiscount
            WHERE VoucherId = @VoucherId 
                AND LineNumber = @LineNumber";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var transaction = connection.BeginTransaction();
            try
            {
                var updateParams = vouchers.Select(v => new
                {
                    VoucherId = v.VoucherId,
                    LineNumber = v.LineNumber,
                    InventoryUnitPrice = v.InventoryUnitPrice,
                    GrossProfit = v.GrossProfit,      // 粗利益
                    WalkingDiscount = v.WalkingDiscount // 歩引き金
                });
                
                var updatedCount = await connection.ExecuteAsync(updateSql, updateParams, transaction);
                await transaction.CommitAsync();
                
                _logger.LogInformation("CSV用リポジトリで{Count}件の売上伝票を更新しました", updatedCount);
                return updatedCount;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV用リポジトリでの売上伝票更新エラー");
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
            UPDATE SalesVouchers 
            SET IsActive = @IsActive
            WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, IsActive = isActive });
            
            _logger.LogInformation("Updated {Count} sales vouchers IsActive flag (CSV repository)", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSVリポジトリでの売上伝票IsActiveフラグ更新エラー: DataSetId={DataSetId}, IsActive={IsActive}", dataSetId, isActive);
            throw;
        }
    }

    /// <summary>
    /// アクティブな伝票のみを取得（IsActive = true）
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>アクティブな売上伝票一覧</returns>
    public async Task<IEnumerable<SalesVoucher>> GetActiveByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT VoucherId, LineNumber, VoucherNumber, VoucherDate, JobDate, VoucherType, DetailType,
                   CustomerCode, CustomerName, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                   Quantity, UnitPrice, Amount, InventoryUnitPrice, GrossProfit, CreatedDate, DataSetId, IsActive
            FROM SalesVouchers 
            WHERE JobDate = @jobDate AND IsActive = 1
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var vouchers = await connection.QueryAsync<SalesVoucher>(sql, new { jobDate = jobDate.Date });
            
            var results = vouchers.ToList();
            _logger.LogInformation("Retrieved {Count} active sales vouchers (CSV repository)", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSVリポジトリでのアクティブ売上伝票データ取得エラー: JobDate={JobDate}", jobDate);
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
            UPDATE SalesVouchers 
            SET IsActive = 0
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
            
            _logger.LogInformation("Deactivated {Count} sales vouchers (CSV repository)", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSVリポジトリでの売上伝票無効化エラー: JobDate={JobDate}, ExcludeDataSetId={ExcludeDataSetId}", jobDate, excludeDataSetId);
            throw;
        }
    }
}