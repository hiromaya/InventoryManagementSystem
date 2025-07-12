using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface IUnmatchListService
{
    /// <summary>
    /// アンマッチリスト処理を実行する（全期間対象）
    /// </summary>
    Task<UnmatchListResult> ProcessUnmatchListAsync();
    
    /// <summary>
    /// アンマッチリスト処理を実行する（指定日以前対象）
    /// </summary>
    Task<UnmatchListResult> ProcessUnmatchListAsync(DateTime targetDate);
    
    /// <summary>
    /// アンマッチリストを生成する（全期間対象）
    /// </summary>
    Task<IEnumerable<UnmatchItem>> GenerateUnmatchListAsync(string dataSetId);
    
    /// <summary>
    /// アンマッチリストを生成する（指定日以前対象）
    /// </summary>
    Task<IEnumerable<UnmatchItem>> GenerateUnmatchListAsync(string dataSetId, DateTime targetDate);
}

public class UnmatchListResult
{
    public bool Success { get; set; }
    public string DataSetId { get; set; } = string.Empty;
    public int UnmatchCount { get; set; }
    public IEnumerable<UnmatchItem> UnmatchItems { get; set; } = new List<UnmatchItem>();
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan ProcessingTime { get; set; }
}