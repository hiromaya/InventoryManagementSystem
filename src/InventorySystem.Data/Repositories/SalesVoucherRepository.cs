using Dapper;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

public class SalesVoucherRepository : BaseRepository, ISalesVoucherRepository
{
    public SalesVoucherRepository(string connectionString, ILogger<SalesVoucherRepository> logger)
        : base(connectionString, logger)
    {
    }

    public async Task<IEnumerable<SalesVoucher>> GetByJobDateAsync(DateTime jobDate)
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                CustomerCode,
                CustomerName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                Quantity,
                UnitPrice as SalesUnitPrice,
                Amount as SalesAmount,
                InventoryUnitPrice,
                JobDate,
                DetailType,
                DataSetId,
                GrossProfit,
                WalkingDiscount
            FROM SalesVouchers
            WHERE JobDate = @jobDate
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { jobDate });
            
            return vouchers.Select(MapToSalesVoucher);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByJobDateAsync), new { jobDate });
            throw;
        }
    }

    public async Task<int> CreateAsync(SalesVoucher voucher)
    {
        const string sql = @"
            INSERT INTO SalesVouchers (
                VoucherNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, UnitPrice, Amount, DataSetId, VoucherType,
                CustomerCode, DetailType
            ) VALUES (
                @VoucherNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @UnitPrice, @Amount, @DataSetId, @VoucherType,
                @CustomerCode, @DetailType
            )";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, MapFromSalesVoucher(voucher));
            
            LogInfo($"Created sales voucher record", new { voucher.VoucherId });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(CreateAsync), voucher);
            throw;
        }
    }

    public async Task<int> BulkInsertAsync(IEnumerable<SalesVoucher> vouchers)
    {
        const string sql = @"
            INSERT INTO SalesVouchers (
                VoucherNumber, VoucherDate, JobDate,
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                Quantity, UnitPrice, Amount, DataSetId, VoucherType,
                CustomerCode, DetailType
            ) VALUES (
                @VoucherNumber, @VoucherDate, @JobDate,
                @ProductCode, @GradeCode, @ClassCode, @ShippingMarkCode, @ShippingMarkName,
                @Quantity, @UnitPrice, @Amount, @DataSetId, @VoucherType,
                @CustomerCode, @DetailType
            )";

        try
        {
            using var connection = CreateConnection();
            var parameters = vouchers.Select(MapFromSalesVoucher);
            var result = await connection.ExecuteAsync(sql, parameters);
            
            LogInfo($"Bulk inserted {result} sales voucher records");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(BulkInsertAsync), new { Count = vouchers.Count() });
            throw;
        }
    }

    private static SalesVoucher MapToSalesVoucher(dynamic row)
    {
        return new SalesVoucher
        {
            VoucherId = row.VoucherId?.ToString() ?? string.Empty,
            LineNumber = row.LineNumber ?? 0,
            VoucherNumber = row.VoucherNumber?.ToString() ?? string.Empty,
            VoucherDate = row.VoucherDate,
            JobDate = row.JobDate,
            VoucherType = row.VoucherType?.ToString() ?? string.Empty,
            DetailType = row.DetailType?.ToString() ?? string.Empty,
            CustomerCode = row.CustomerCode?.ToString(),
            CustomerName = row.CustomerName?.ToString(),
            ProductCode = row.ProductCode?.ToString() ?? string.Empty,
            GradeCode = row.GradeCode?.ToString() ?? string.Empty,
            ClassCode = row.ClassCode?.ToString() ?? string.Empty,
            ShippingMarkCode = row.ShippingMarkCode?.ToString() ?? string.Empty,
            ShippingMarkName = row.ShippingMarkName?.ToString() ?? string.Empty,
            Quantity = row.Quantity ?? 0m,
            UnitPrice = row.SalesUnitPrice ?? 0m,
            Amount = row.SalesAmount ?? 0m,
            InventoryUnitPrice = row.InventoryUnitPrice ?? 0m,
            DataSetId = row.DataSetId?.ToString() ?? string.Empty
        };
    }

    private static object MapFromSalesVoucher(SalesVoucher voucher)
    {
        return new
        {
            VoucherNumber = voucher.VoucherId,
            voucher.VoucherDate,
            voucher.JobDate,
            voucher.ProductCode,
            voucher.GradeCode,
            voucher.ClassCode,
            voucher.ShippingMarkCode,
            voucher.ShippingMarkName,
            voucher.Quantity,
            UnitPrice = voucher.UnitPrice,
            Amount = voucher.Amount,
            voucher.DataSetId,
            voucher.VoucherType,
            voucher.CustomerCode,
            voucher.DetailType
        };
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
    
    public async Task<IEnumerable<SalesVoucher>> GetByDataSetIdAsync(string dataSetId)
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                CustomerCode,
                CustomerName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                Quantity,
                UnitPrice as SalesUnitPrice,
                Amount as SalesAmount,
                InventoryUnitPrice,
                JobDate,
                DetailType,
                DataSetId,
                GrossProfit,
                WalkingDiscount
            FROM SalesVouchers
            WHERE DataSetId = @dataSetId
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { dataSetId });
            
            return vouchers.Select(MapToSalesVoucher);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByDataSetIdAsync), new { dataSetId });
            throw;
        }
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
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { jobDate });
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetDataSetIdByJobDateAsync), new { jobDate });
            throw;
        }
    }

    public async Task<IEnumerable<SalesVoucher>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                CustomerCode,
                CustomerName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                Quantity,
                UnitPrice as SalesUnitPrice,
                Amount as SalesAmount,
                InventoryUnitPrice,
                JobDate,
                DetailType,
                DataSetId,
                GrossProfit,
                WalkingDiscount
            FROM SalesVouchers
            ORDER BY JobDate DESC, VoucherDate DESC, VoucherId DESC";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql);
            
            var results = vouchers.Select(MapToSalesVoucher).ToList();
            LogInfo($"Retrieved {results.Count} sales vouchers (all records)");
            return results;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetAllAsync));
            throw;
        }
    }
    
    /// <summary>
    /// Process 2-5: JobDateとDataSetIdで売上伝票を取得
    /// </summary>
    public async Task<IEnumerable<SalesVoucher>> GetByJobDateAndDataSetIdAsync(DateTime jobDate, string dataSetId)
    {
        const string sql = @"
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                CustomerCode,
                CustomerName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                Quantity,
                UnitPrice,
                Amount,
                InventoryUnitPrice,
                JobDate,
                DetailType,
                DataSetId,
                GrossProfit,
                WalkingDiscount
            FROM SalesVouchers
            WHERE JobDate = @JobDate AND DataSetId = @DataSetId
                AND VoucherType IN ('51', '52')
                AND ProductCode != '00000'
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<SalesVoucher>(sql, new { JobDate = jobDate, DataSetId = dataSetId });
            
            LogInfo($"Retrieved {vouchers.Count()} sales vouchers for JobDate: {jobDate:yyyy-MM-dd}, DataSetId: {dataSetId}");
            return vouchers;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetByJobDateAndDataSetIdAsync));
            throw;
        }
    }
    
    /// <summary>
    /// Process 2-5: 売上伝票の在庫単価と粗利益をバッチ更新
    /// </summary>
    public async Task<int> UpdateInventoryUnitPriceAndGrossProfitBatchAsync(IEnumerable<SalesVoucher> vouchers)
    {
        const string updateSql = @"
            UPDATE SalesVouchers 
            SET 
                InventoryUnitPrice = @InventoryUnitPrice,
                GrossProfit = @GrossProfit,
                WalkingDiscount = @WalkingDiscount,
                UpdatedDate = GETDATE()
            WHERE Id = @Id";

        try
        {
            using var connection = CreateConnection();
            
            var updateParams = vouchers.Select(v => new
            {
                Id = v.Id,
                InventoryUnitPrice = v.InventoryUnitPrice,
                GrossProfit = v.GrossProfit,      // 粗利益
                WalkingDiscount = v.WalkingDiscount // 歩引き金
            }).ToList();

            var updatedCount = await connection.ExecuteAsync(updateSql, updateParams);
            
            LogInfo($"Updated {updatedCount} sales vouchers with inventory unit price and gross profit");
            return updatedCount;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateInventoryUnitPriceAndGrossProfitBatchAsync));
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
            SET IsActive = @IsActive, 
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId";

        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId, IsActive = isActive });
            
            LogInfo($"Updated {result} sales vouchers IsActive flag", new { dataSetId, isActive });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateIsActiveByDataSetIdAsync), new { dataSetId, isActive });
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
            SELECT 
                VoucherId,
                LineNumber,
                VoucherNumber,
                VoucherDate,
                VoucherType,
                CustomerCode,
                CustomerName,
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                ShippingMarkName,
                Quantity,
                UnitPrice as SalesUnitPrice,
                Amount as SalesAmount,
                InventoryUnitPrice,
                JobDate,
                DetailType,
                DataSetId,
                GrossProfit,
                WalkingDiscount,
                IsActive
            FROM SalesVouchers
            WHERE JobDate = @jobDate AND IsActive = 1
            ORDER BY VoucherNumber, LineNumber";

        try
        {
            using var connection = CreateConnection();
            var vouchers = await connection.QueryAsync<dynamic>(sql, new { jobDate });
            
            var results = vouchers.Select(MapToSalesVoucher).ToList();
            LogInfo($"Retrieved {results.Count} active sales vouchers", new { jobDate });
            return results;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetActiveByJobDateAsync), new { jobDate });
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
            using var connection = CreateConnection();
            var result = await connection.ExecuteAsync(sql, parameters);
            
            LogInfo($"Deactivated {result} sales vouchers", new { jobDate, excludeDataSetId });
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeactivateByJobDateAsync), new { jobDate, excludeDataSetId });
            throw;
        }
    }
}