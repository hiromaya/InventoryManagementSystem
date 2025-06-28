namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 荷印マスタインポートサービスインターフェース
/// </summary>
public interface IShippingMarkMasterImportService
{
    /// <summary>
    /// 荷印マスタCSVファイルをインポート
    /// </summary>
    /// <param name="filePath">CSVファイルパス</param>
    /// <returns>インポート結果</returns>
    Task<ImportResult> ImportAsync(string filePath);
}

/// <summary>
/// インポート結果
/// </summary>
public class ImportResult
{
    public int ImportedCount { get; set; }
    public int ErrorCount { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}