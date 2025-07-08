using System;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces.Development;

/// <summary>
/// データ状態確認サービスのインターフェース
/// </summary>
public interface IDataStatusCheckService
{
    /// <summary>
    /// 指定日付のデータ状態を確認する
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>データ状態レポート</returns>
    Task<DataStatusReport> GetDataStatusAsync(DateTime jobDate);
    
    /// <summary>
    /// データ状態レポートを表示する
    /// </summary>
    /// <param name="report">データ状態レポート</param>
    void DisplayReport(DataStatusReport report);
}

/// <summary>
/// データ状態レポート
/// </summary>
public class DataStatusReport
{
    public DateTime JobDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    
    public CsvImportStatus CsvStatus { get; set; } = new();
    public UnmatchListStatus UnmatchStatus { get; set; } = new();
    public DailyReportStatusInfo DailyReportStatus { get; set; } = new();
    public DailyCloseStatusInfo DailyCloseStatus { get; set; } = new();
    public InventoryMasterStatus InventoryStatus { get; set; } = new();
    
    /// <summary>
    /// CSV取込状況
    /// </summary>
    public class CsvImportStatus
    {
        public bool IsImported { get; set; }
        public string? DatasetId { get; set; }
        public int SalesCount { get; set; }
        public int PurchaseCount { get; set; }
        public int AdjustmentCount { get; set; }
        public int MasterCount { get; set; }
        public DateTime? ImportedAt { get; set; }
        public string? ImportedBy { get; set; }
    }
    
    /// <summary>
    /// アンマッチリスト状況
    /// </summary>
    public class UnmatchListStatus
    {
        public bool IsCreated { get; set; }
        public int UnmatchCount { get; set; }
        public int ProcessedItemCount { get; set; }
        public TimeSpan? ProcessingTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? DatasetId { get; set; }
    }
    
    /// <summary>
    /// 商品日報状況
    /// </summary>
    public class DailyReportStatusInfo
    {
        public bool IsCreated { get; set; }
        public string? ReportPath { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalPurchaseAmount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? DatasetId { get; set; }
    }
    
    /// <summary>
    /// 日次終了処理状況
    /// </summary>
    public class DailyCloseStatusInfo
    {
        public bool IsProcessed { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessedBy { get; set; }
        public string? DatasetId { get; set; }
        public int UpdatedInventoryCount { get; set; }
        public string? ValidationStatus { get; set; }
    }
    
    /// <summary>
    /// 在庫マスタ状況
    /// </summary>
    public class InventoryMasterStatus
    {
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int ZeroStockCount { get; set; }
        public int NegativeStockCount { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public bool HasPreviousDayData { get; set; }
    }
}