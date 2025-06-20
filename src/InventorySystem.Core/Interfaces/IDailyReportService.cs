using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 商品日報サービスインターフェース
/// </summary>
public interface IDailyReportService
{
    /// <summary>
    /// 商品日報データを取得
    /// </summary>
    /// <param name="reportDate">レポート日付</param>
    /// <param name="dataSetId">データセットID</param>
    /// <returns>商品日報データリスト</returns>
    Task<List<DailyReportItem>> GetDailyReportDataAsync(DateTime reportDate, string dataSetId);
    
    /// <summary>
    /// 商品日報処理を実行（CP在庫Mの作成・集計含む）
    /// </summary>
    /// <param name="reportDate">レポート日付</param>
    /// <param name="existingDataSetId">既存のデータセットID（指定されない場合は新規作成）</param>
    /// <returns>処理結果</returns>
    Task<DailyReportResult> ProcessDailyReportAsync(DateTime reportDate, string? existingDataSetId = null);
}

/// <summary>
/// 商品日報処理結果
/// </summary>
public class DailyReportResult
{
    /// <summary>
    /// 処理成功フラグ
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 処理件数
    /// </summary>
    public int ProcessedCount { get; set; }
    
    /// <summary>
    /// 商品日報データ
    /// </summary>
    public List<DailyReportItem> ReportItems { get; set; } = new();
    
    /// <summary>
    /// 大分類計データ
    /// </summary>
    public List<DailyReportSubtotal> Subtotals { get; set; } = new();
    
    /// <summary>
    /// 合計データ
    /// </summary>
    public DailyReportTotal Total { get; set; } = new();
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 処理時間
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
}