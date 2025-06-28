namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 産地マスタインポートサービスインターフェース
/// </summary>
public interface IRegionMasterImportService
{
    /// <summary>
    /// 産地マスタCSVファイルをインポート
    /// </summary>
    /// <param name="filePath">CSVファイルパス</param>
    /// <returns>インポート結果</returns>
    Task<ImportResult> ImportAsync(string filePath);
}