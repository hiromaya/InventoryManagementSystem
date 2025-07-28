using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// UN在庫マスタ（アンマッチチェック専用）リポジトリ実装
/// 数量のみを管理し、単価・金額は含まない
/// </summary>
public class UnInventoryRepository : BaseRepository, IUnInventoryRepository
{
    public UnInventoryRepository(string connectionString, ILogger<UnInventoryRepository> logger) : base(connectionString, logger)
    {
    }

    /// <summary>
    /// 在庫マスタからUN在庫マスタを作成する
    /// </summary>
    public async Task<int> CreateFromInventoryMasterAsync(string dataSetId, DateTime? targetDate = null)
    {
        const string sql = """
            INSERT INTO UnInventoryMaster (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                DataSetId, PreviousDayStock, DailyStock, DailyFlag, JobDate,
                CreatedDate, UpdatedDate
            )
            SELECT 
                i.ProductCode, i.GradeCode, i.ClassCode, i.ShippingMarkCode, i.ShippingMarkName,
                @DataSetId,
                ISNULL(i.PreviousMonthQuantity, 0),
                0 as DailyStock,
                '9' as DailyFlag,
                i.JobDate,
                GETDATE(),
                GETDATE()
            FROM InventoryMaster i
            WHERE (@TargetDate IS NULL OR i.JobDate = @TargetDate)
            """;

        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new 
            { 
                DataSetId = dataSetId, 
                TargetDate = targetDate 
            });

            LogInfo($"UN在庫マスタ作成完了: {count}件", new { dataSetId, targetDate });
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ作成エラー", new { dataSetId, targetDate });
            throw;
        }
    }

    /// <summary>
    /// UN在庫マスタの当日エリアをクリアし、当日発生フラグを'9'にセットする
    /// </summary>
    public async Task<int> ClearDailyAreaAsync(string dataSetId)
    {
        const string sql = """
            UPDATE UnInventoryMaster 
            SET 
                DailyStock = 0,
                DailyFlag = '9',
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });

            LogInfo($"UN在庫マスタ当日エリアクリア完了: {count}件", new { dataSetId });
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ当日エリアクリアエラー", new { dataSetId });
            throw;
        }
    }

    /// <summary>
    /// 売上返品データをUN在庫マスタに集計する（入荷データのみ）
    /// </summary>
    public async Task<int> AggregateSalesDataAsync(string dataSetId, DateTime? targetDate = null)
    {
        var dateCondition = targetDate.HasValue ? "AND s.JobDate = @TargetDate" : "";
        
        var sql = $"""
            UPDATE un
            SET 
                DailyStock = un.DailyStock + ISNULL(ABS(sales.SalesReturnQuantity), 0),
                DailyFlag = CASE 
                    WHEN sales.ProductCode IS NOT NULL THEN '0' 
                    ELSE un.DailyFlag 
                END,
                UpdatedDate = GETDATE()
            FROM UnInventoryMaster un
            LEFT JOIN (
                SELECT 
                    s.ProductCode, s.GradeCode, s.ClassCode, 
                    s.ShippingMarkCode, s.ShippingMarkName,
                    SUM(s.Quantity) as SalesReturnQuantity
                FROM SalesVouchers s
                WHERE s.DataSetId = @DataSetId
                AND s.VoucherType IN ('51', '52')  -- 掛売・現売
                AND s.Quantity < 0  -- 売上返品のみ（入荷）
                {dateCondition}
                GROUP BY 
                    s.ProductCode, s.GradeCode, s.ClassCode, 
                    s.ShippingMarkCode, s.ShippingMarkName
            ) sales ON 
                un.ProductCode = sales.ProductCode
                AND un.GradeCode = sales.GradeCode
                AND un.ClassCode = sales.ClassCode
                AND un.ShippingMarkCode = sales.ShippingMarkCode
                AND un.ShippingMarkName = sales.ShippingMarkName
            WHERE un.DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new 
            { 
                DataSetId = dataSetId, 
                TargetDate = targetDate 
            });

            LogInfo($"UN在庫マスタ売上データ集計完了: {count}件", new { dataSetId, targetDate });
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ売上データ集計エラー", new { dataSetId, targetDate });
            throw;
        }
    }

    /// <summary>
    /// 仕入データをUN在庫マスタに集計する
    /// </summary>
    public async Task<int> AggregatePurchaseDataAsync(string dataSetId, DateTime? targetDate = null)
    {
        var dateCondition = targetDate.HasValue ? "AND p.JobDate = @TargetDate" : "";
        
        var sql = $"""
            UPDATE un
            SET 
                DailyStock = un.DailyStock + ISNULL(purchase.PurchaseQuantity, 0),
                DailyFlag = CASE 
                    WHEN purchase.ProductCode IS NOT NULL THEN '0' 
                    ELSE un.DailyFlag 
                END,
                UpdatedDate = GETDATE()
            FROM UnInventoryMaster un
            LEFT JOIN (
                SELECT 
                    p.ProductCode, p.GradeCode, p.ClassCode, 
                    p.ShippingMarkCode, p.ShippingMarkName,
                    SUM(p.Quantity) as PurchaseQuantity
                FROM PurchaseVouchers p
                WHERE p.DataSetId = @DataSetId
                AND p.VoucherType IN ('11', '12')  -- 掛仕入・現金仕入
                AND p.Quantity > 0  -- 通常仕入（返品除外）
                {dateCondition}
                GROUP BY 
                    p.ProductCode, p.GradeCode, p.ClassCode, 
                    p.ShippingMarkCode, p.ShippingMarkName
            ) purchase ON 
                un.ProductCode = purchase.ProductCode
                AND un.GradeCode = purchase.GradeCode
                AND un.ClassCode = purchase.ClassCode
                AND un.ShippingMarkCode = purchase.ShippingMarkCode
                AND un.ShippingMarkName = purchase.ShippingMarkName
            WHERE un.DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new 
            { 
                DataSetId = dataSetId, 
                TargetDate = targetDate 
            });

            LogInfo($"UN在庫マスタ仕入データ集計完了: {count}件", new { dataSetId, targetDate });
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ仕入データ集計エラー", new { dataSetId, targetDate });
            throw;
        }
    }

    /// <summary>
    /// 在庫調整データをUN在庫マスタに集計する（入荷データのみ）
    /// </summary>
    public async Task<int> AggregateInventoryAdjustmentDataAsync(string dataSetId, DateTime? targetDate = null)
    {
        var dateCondition = targetDate.HasValue ? "AND ia.JobDate = @TargetDate" : "";
        
        var sql = $"""
            UPDATE un
            SET 
                DailyStock = un.DailyStock + ISNULL(adjustment.AdjustmentQuantity, 0),
                DailyFlag = CASE 
                    WHEN adjustment.ProductCode IS NOT NULL THEN '0' 
                    ELSE un.DailyFlag 
                END,
                UpdatedDate = GETDATE()
            FROM UnInventoryMaster un
            LEFT JOIN (
                SELECT 
                    ia.ProductCode, ia.GradeCode, ia.ClassCode, 
                    ia.ShippingMarkCode, ia.ShippingMarkName,
                    SUM(ia.Quantity) as AdjustmentQuantity
                FROM InventoryAdjustments ia
                WHERE ia.DataSetId = @DataSetId
                AND ia.VoucherType = '71'  -- 在庫調整
                AND ia.DetailType = '1'    -- ロス
                AND ia.Quantity > 0        -- 入荷調整のみ（プラス数量）
                {dateCondition}
                GROUP BY 
                    ia.ProductCode, ia.GradeCode, ia.ClassCode, 
                    ia.ShippingMarkCode, ia.ShippingMarkName
            ) adjustment ON 
                un.ProductCode = adjustment.ProductCode
                AND un.GradeCode = adjustment.GradeCode
                AND un.ClassCode = adjustment.ClassCode
                AND un.ShippingMarkCode = adjustment.ShippingMarkCode
                AND un.ShippingMarkName = adjustment.ShippingMarkName
            WHERE un.DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new 
            { 
                DataSetId = dataSetId, 
                TargetDate = targetDate 
            });

            LogInfo($"UN在庫マスタ在庫調整データ集計完了: {count}件", new { dataSetId, targetDate });
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ在庫調整データ集計エラー", new { dataSetId, targetDate });
            throw;
        }
    }

    /// <summary>
    /// 当日在庫数量を計算する
    /// </summary>
    public async Task<int> CalculateDailyStockAsync(string dataSetId)
    {
        const string sql = """
            UPDATE UnInventoryMaster 
            SET 
                DailyStock = PreviousDayStock + DailyStock,
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });

            LogInfo($"UN在庫マスタ当日在庫計算完了: {count}件", new { dataSetId });
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ当日在庫計算エラー", new { dataSetId });
            throw;
        }
    }

    /// <summary>
    /// 当日発生フラグを'0'（処理済み）に更新する
    /// </summary>
    public async Task<int> SetDailyFlagToProcessedAsync(string dataSetId)
    {
        const string sql = """
            UPDATE UnInventoryMaster 
            SET 
                DailyFlag = '0',
                UpdatedDate = GETDATE()
            WHERE DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });

            LogInfo($"UN在庫マスタ処理フラグ更新完了: {count}件", new { dataSetId });
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ処理フラグ更新エラー", new { dataSetId });
            throw;
        }
    }

    /// <summary>
    /// UN在庫マスタを取得する（キー指定）
    /// </summary>
    public async Task<UnInventoryMaster?> GetByKeyAsync(InventoryKey key, string dataSetId)
    {
        const string sql = """
            SELECT * FROM UnInventoryMaster 
            WHERE ProductCode = @ProductCode
            AND GradeCode = @GradeCode
            AND ClassCode = @ClassCode
            AND ShippingMarkCode = @ShippingMarkCode
            AND ShippingMarkName = @ShippingMarkName
            AND DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new 
            { 
                ProductCode = key.ProductCode,
                GradeCode = key.GradeCode,
                ClassCode = key.ClassCode,
                ShippingMarkCode = key.ShippingMarkCode,
                ShippingMarkName = key.ShippingMarkName,
                DataSetId = dataSetId 
            });

            return result != null ? MapToUnInventoryMaster(result) : null;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ取得エラー", new { key, dataSetId });
            throw;
        }
    }

    /// <summary>
    /// UN在庫マスタを一括取得する
    /// </summary>
    public async Task<IEnumerable<UnInventoryMaster>> GetAllAsync(string dataSetId)
    {
        const string sql = """
            SELECT * FROM UnInventoryMaster 
            WHERE DataSetId = @DataSetId
            ORDER BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            """;

        try
        {
            using var connection = CreateConnection();
            var results = await connection.QueryAsync<dynamic>(sql, new { DataSetId = dataSetId });

            return results.Select(MapToUnInventoryMaster);
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ一括取得エラー", new { dataSetId });
            throw;
        }
    }

    /// <summary>
    /// UN在庫マスタを削除する（データセット指定）
    /// </summary>
    public async Task<int> DeleteByDataSetIdAsync(string dataSetId)
    {
        const string sql = """
            DELETE FROM UnInventoryMaster 
            WHERE DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, new { DataSetId = dataSetId });

            LogInfo($"UN在庫マスタ削除完了: {count}件", new { dataSetId });
            return count;
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ削除エラー", new { dataSetId });
            throw;
        }
    }

    /// <summary>
    /// UN在庫マスタの件数を取得する
    /// </summary>
    public async Task<int> GetCountAsync(string dataSetId)
    {
        const string sql = """
            SELECT COUNT(*) FROM UnInventoryMaster 
            WHERE DataSetId = @DataSetId
            """;

        try
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, new { DataSetId = dataSetId });
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ件数取得エラー", new { dataSetId });
            throw;
        }
    }

    /// <summary>
    /// JobDateとDataSetIdでUN在庫マスタを取得
    /// </summary>
    public async Task<IEnumerable<UnInventoryMaster>> GetByJobDateAndDataSetIdAsync(DateTime jobDate, string dataSetId)
    {
        const string sql = """
            SELECT * FROM UnInventoryMaster 
            WHERE DataSetId = @DataSetId 
            AND JobDate = @JobDate
            ORDER BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            """;

        try
        {
            using var connection = CreateConnection();
            var results = await connection.QueryAsync<dynamic>(sql, new 
            { 
                DataSetId = dataSetId,
                JobDate = jobDate 
            });

            LogInfo($"UN在庫マスタ取得完了: JobDate={jobDate}, DataSetId={dataSetId}, 件数={results.Count()}", 
                new { jobDate, dataSetId });
            
            return results.Select(MapToUnInventoryMaster);
        }
        catch (Exception ex)
        {
            LogError(ex, "UN在庫マスタ取得エラー", new { jobDate, dataSetId });
            throw;
        }
    }

    /// <summary>
    /// 動的型からUnInventoryMasterにマッピング
    /// InventoryKeyは冪等性のある0埋め処理で自動フォーマットされます
    /// </summary>
    private UnInventoryMaster MapToUnInventoryMaster(dynamic item)
    {
        // DailyFlagの安全な変換（string→char型変換）
        char dailyFlag = '9';
        if (item.DailyFlag != null)
        {
            if (item.DailyFlag is char)
            {
                dailyFlag = item.DailyFlag;
            }
            else if (item.DailyFlag is string flagStr && !string.IsNullOrEmpty(flagStr))
            {
                dailyFlag = flagStr[0];  // 文字列の最初の文字を取得
            }
        }

        return new UnInventoryMaster
        {
            Key = new InventoryKey
            {
                ProductCode = item.ProductCode ?? string.Empty,      // 冪等性のある5桁0埋め
                GradeCode = item.GradeCode ?? string.Empty,          // 冪等性のある3桁0埋め
                ClassCode = item.ClassCode ?? string.Empty,          // 冪等性のある3桁0埋め
                ShippingMarkCode = item.ShippingMarkCode ?? string.Empty, // 冪等性のある4桁0埋め
                ShippingMarkName = item.ShippingMarkName ?? string.Empty  // 8桁固定（正規化済み）
            },
            DataSetId = item.DataSetId ?? string.Empty,
            PreviousDayStock = item.PreviousDayStock ?? 0,
            DailyStock = item.DailyStock ?? 0,
            DailyFlag = dailyFlag,
            JobDate = item.JobDate,
            CreatedDate = item.CreatedDate ?? DateTime.Now,
            UpdatedDate = item.UpdatedDate ?? DateTime.Now
        };
    }
}