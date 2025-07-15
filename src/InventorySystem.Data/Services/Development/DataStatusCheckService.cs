using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces.Development;

namespace InventorySystem.Data.Services.Development;

/// <summary>
/// データ状態確認サービス
/// </summary>
public class DataStatusCheckService : IDataStatusCheckService
{
    private readonly string _connectionString;
    private readonly ILogger<DataStatusCheckService> _logger;
    
    public DataStatusCheckService(string connectionString, ILogger<DataStatusCheckService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<DataStatusReport> GetDataStatusAsync(DateTime jobDate)
    {
        var report = new DataStatusReport { JobDate = jobDate };
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            // CSV取込状況の確認
            await CheckCsvImportStatusAsync(connection, jobDate, report.CsvStatus);
            
            // アンマッチリスト状況の確認
            await CheckUnmatchListStatusAsync(connection, jobDate, report.UnmatchStatus);
            
            // 商品日報状況の確認
            await CheckDailyReportStatusAsync(connection, jobDate, report.DailyReportStatus);
            
            // 日次終了処理状況の確認
            await CheckDailyCloseStatusAsync(connection, jobDate, report.DailyCloseStatus);
            
            // 在庫マスタ状況の確認
            await CheckInventoryMasterStatusAsync(connection, jobDate, report.InventoryStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データ状態確認でエラーが発生しました");
        }
        
        return report;
    }
    
    public void DisplayReport(DataStatusReport report)
    {
        Console.WriteLine($"\n=== データ状態確認: {report.JobDate:yyyy-MM-dd} ===");
        Console.WriteLine($"確認日時: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}\n");
        
        // CSV取込状況
        Console.WriteLine("【CSV取込状況】");
        if (report.CsvStatus.IsImported)
        {
            Console.WriteLine($"✅ CSV取込済み");
            Console.WriteLine($"   DataSetId: {report.CsvStatus.DataSetId}");
            Console.WriteLine($"   取込日時: {report.CsvStatus.ImportedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   売上伝票: {report.CsvStatus.SalesCount:N0}件");
            Console.WriteLine($"   仕入伝票: {report.CsvStatus.PurchaseCount:N0}件");
            Console.WriteLine($"   在庫調整: {report.CsvStatus.AdjustmentCount:N0}件");
            if (report.CsvStatus.MasterCount > 0)
            {
                Console.WriteLine($"   マスタ: {report.CsvStatus.MasterCount:N0}件");
            }
        }
        else
        {
            Console.WriteLine("❌ CSVデータ未取込");
        }
        
        // アンマッチリスト状況
        Console.WriteLine("\n【アンマッチリスト】");
        if (report.UnmatchStatus.IsCreated)
        {
            Console.WriteLine($"✅ アンマッチリスト作成済み");
            Console.WriteLine($"   作成日時: {report.UnmatchStatus.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   アンマッチ件数: {report.UnmatchStatus.UnmatchCount:N0}件");
            Console.WriteLine($"   処理アイテム数: {report.UnmatchStatus.ProcessedItemCount:N0}件");
            if (report.UnmatchStatus.ProcessingTime.HasValue)
            {
                Console.WriteLine($"   処理時間: {report.UnmatchStatus.ProcessingTime.Value.TotalSeconds:F1}秒");
            }
        }
        else
        {
            Console.WriteLine("❌ アンマッチリスト未作成");
        }
        
        // 商品日報状況
        Console.WriteLine("\n【商品日報】");
        if (report.DailyReportStatus.IsCreated)
        {
            Console.WriteLine($"✅ 商品日報作成済み");
            Console.WriteLine($"   作成日時: {report.DailyReportStatus.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   アイテム数: {report.DailyReportStatus.ItemCount:N0}件");
            Console.WriteLine($"   売上合計: {report.DailyReportStatus.TotalSalesAmount:C}");
            Console.WriteLine($"   仕入合計: {report.DailyReportStatus.TotalPurchaseAmount:C}");
            if (!string.IsNullOrEmpty(report.DailyReportStatus.ReportPath))
            {
                Console.WriteLine($"   ファイル: {report.DailyReportStatus.ReportPath}");
            }
        }
        else
        {
            Console.WriteLine("❌ 商品日報未作成");
        }
        
        // 日次終了処理状況
        Console.WriteLine("\n【日次終了処理】");
        if (report.DailyCloseStatus.IsProcessed)
        {
            Console.WriteLine($"✅ 日次終了処理済み");
            Console.WriteLine($"   処理日時: {report.DailyCloseStatus.ProcessedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   処理者: {report.DailyCloseStatus.ProcessedBy}");
            Console.WriteLine($"   更新在庫数: {report.DailyCloseStatus.UpdatedInventoryCount:N0}件");
            if (!string.IsNullOrEmpty(report.DailyCloseStatus.ValidationStatus))
            {
                Console.WriteLine($"   検証結果: {report.DailyCloseStatus.ValidationStatus}");
            }
        }
        else
        {
            Console.WriteLine("❌ 日次終了処理未実行");
        }
        
        // 在庫マスタ状況
        Console.WriteLine("\n【在庫マスタ状況】");
        Console.WriteLine($"   総レコード数: {report.InventoryStatus.TotalCount:N0}件");
        Console.WriteLine($"   アクティブ: {report.InventoryStatus.ActiveCount:N0}件");
        Console.WriteLine($"   在庫ゼロ: {report.InventoryStatus.ZeroStockCount:N0}件");
        if (report.InventoryStatus.NegativeStockCount > 0)
        {
            Console.WriteLine($"   ⚠️ マイナス在庫: {report.InventoryStatus.NegativeStockCount:N0}件");
        }
        Console.WriteLine($"   前日データ: {(report.InventoryStatus.HasPreviousDayData ? "あり" : "なし")}");
        if (report.InventoryStatus.LastUpdatedAt.HasValue)
        {
            Console.WriteLine($"   最終更新: {report.InventoryStatus.LastUpdatedAt:yyyy-MM-dd HH:mm:ss}");
        }
        
        Console.WriteLine("\n" + new string('=', 50) + "\n");
    }
    
    private async Task CheckCsvImportStatusAsync(SqlConnection connection, DateTime jobDate, DataStatusReport.CsvImportStatus status)
    {
        // DataSetManagementから情報取得
        var dataSetInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT TOP 1 DataSetId, CreatedAt, CreatedBy, TotalRecordCount
            FROM DataSetManagement
            WHERE JobDate = @JobDate
            ORDER BY CreatedAt DESC",
            new { JobDate = jobDate });
        
        if (dataSetInfo != null)
        {
            status.IsImported = true;
            status.DataSetId = dataSetInfo.DataSetId;
            status.ImportedAt = dataSetInfo.CreatedAt;
            status.ImportedBy = dataSetInfo.CreatedBy;
            
            // 各種伝票の件数を取得
            status.SalesCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM SalesVouchers WHERE JobDate = @JobDate",
                new { JobDate = jobDate });
            
            status.PurchaseCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PurchaseVouchers WHERE JobDate = @JobDate",
                new { JobDate = jobDate });
            
            status.AdjustmentCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM InventoryAdjustments WHERE JobDate = @JobDate",
                new { JobDate = jobDate });
            
            // マスタ件数（当日更新分）
            var masterCount = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM (
                    SELECT ProductCode FROM ProductMaster WHERE CAST(UpdatedDate AS DATE) = @JobDate
                    UNION ALL
                    SELECT CustomerCode FROM CustomerMaster WHERE CAST(UpdatedDate AS DATE) = @JobDate
                    UNION ALL
                    SELECT SupplierCode FROM SupplierMaster WHERE CAST(UpdatedDate AS DATE) = @JobDate
                ) t",
                new { JobDate = jobDate });
            
            status.MasterCount = masterCount;
        }
    }
    
    private async Task CheckUnmatchListStatusAsync(SqlConnection connection, DateTime jobDate, DataStatusReport.UnmatchListStatus status)
    {
        var processInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT TOP 1 StartTime, EndTime, Status, RecordCount, DataSetId
            FROM ProcessHistory
            WHERE JobDate = @JobDate 
                AND ProcessType = 'UNMATCH_LIST'
                AND Status = 2
            ORDER BY StartTime DESC",
            new { JobDate = jobDate });
        
        if (processInfo != null)
        {
            status.IsCreated = true;
            status.CreatedAt = processInfo.EndTime ?? processInfo.StartTime;
            status.UnmatchCount = processInfo.RecordCount ?? 0;
            status.DataSetId = processInfo.DataSetId;
            
            if (processInfo.StartTime != null && processInfo.EndTime != null)
            {
                status.ProcessingTime = processInfo.EndTime - processInfo.StartTime;
            }
            
            // CP在庫マスタのアイテム数を取得（処理されたアイテム数の参考）
            status.ProcessedItemCount = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(DISTINCT CONCAT(ProductCode, '-', GradeCode, '-', ClassCode, '-', ShippingMarkCode, '-', ShippingMarkName))
                FROM CpInventoryMaster
                WHERE DataSetId = @DataSetId",
                new { DataSetId = status.DataSetId });
        }
    }
    
    private async Task CheckDailyReportStatusAsync(SqlConnection connection, DateTime jobDate, DataStatusReport.DailyReportStatusInfo status)
    {
        var processInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT TOP 1 StartTime, EndTime, Status, RecordCount, DataSetId, ErrorMessage
            FROM ProcessHistory
            WHERE JobDate = @JobDate 
                AND ProcessType = 'DAILY_REPORT'
                AND Status = 2
            ORDER BY StartTime DESC",
            new { JobDate = jobDate });
        
        if (processInfo != null)
        {
            status.IsCreated = true;
            status.CreatedAt = processInfo.EndTime ?? processInfo.StartTime;
            status.ItemCount = processInfo.RecordCount ?? 0;
            status.DataSetId = processInfo.DataSetId;
            
            // メッセージからファイルパスを抽出（もし含まれていれば）
            if (processInfo.Message != null && processInfo.Message.Contains(".pdf"))
            {
                status.ReportPath = processInfo.Message;
            }
            
            // 売上・仕入の合計金額を取得
            var amounts = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT 
                    (SELECT ISNULL(SUM(Amount), 0) FROM SalesVouchers WHERE JobDate = @JobDate) as TotalSales,
                    (SELECT ISNULL(SUM(Amount), 0) FROM PurchaseVouchers WHERE JobDate = @JobDate) as TotalPurchase",
                new { JobDate = jobDate });
            
            if (amounts != null)
            {
                status.TotalSalesAmount = amounts.TotalSales ?? 0m;
                status.TotalPurchaseAmount = amounts.TotalPurchase ?? 0m;
            }
        }
    }
    
    private async Task CheckDailyCloseStatusAsync(SqlConnection connection, DateTime jobDate, DataStatusReport.DailyCloseStatusInfo status)
    {
        var closeInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT ProcessedAt, ProcessedBy, DataSetId, UpdatedInventoryCount, ValidationStatus
            FROM DailyCloseManagement
            WHERE JobDate = @JobDate",
            new { JobDate = jobDate });
        
        if (closeInfo != null)
        {
            status.IsProcessed = true;
            status.ProcessedAt = closeInfo.ProcessedAt;
            status.ProcessedBy = closeInfo.ProcessedBy;
            status.DataSetId = closeInfo.DataSetId;
            status.UpdatedInventoryCount = closeInfo.UpdatedInventoryCount ?? 0;
            status.ValidationStatus = closeInfo.ValidationStatus;
        }
    }
    
    private async Task CheckInventoryMasterStatusAsync(SqlConnection connection, DateTime jobDate, DataStatusReport.InventoryMasterStatus status)
    {
        // 在庫マスタの統計情報を取得
        var stats = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT 
                COUNT(*) as TotalCount,
                SUM(CASE WHEN CurrentStock > 0 THEN 1 ELSE 0 END) as ActiveCount,
                SUM(CASE WHEN CurrentStock = 0 THEN 1 ELSE 0 END) as ZeroStockCount,
                SUM(CASE WHEN CurrentStock < 0 THEN 1 ELSE 0 END) as NegativeStockCount,
                MAX(UpdatedDate) as LastUpdatedAt
            FROM InventoryMaster
            WHERE JobDate = @JobDate",
            new { JobDate = jobDate });
        
        if (stats != null)
        {
            status.TotalCount = stats.TotalCount ?? 0;
            status.ActiveCount = stats.ActiveCount ?? 0;
            status.ZeroStockCount = stats.ZeroStockCount ?? 0;
            status.NegativeStockCount = stats.NegativeStockCount ?? 0;
            status.LastUpdatedAt = stats.LastUpdatedAt;
        }
        
        // 前日データの存在確認
        var previousDate = jobDate.AddDays(-1);
        status.HasPreviousDayData = await connection.ExecuteScalarAsync<bool>(@"
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM InventoryMaster WHERE JobDate = @PreviousDate
            ) THEN 1 ELSE 0 END",
            new { PreviousDate = previousDate });
    }
}