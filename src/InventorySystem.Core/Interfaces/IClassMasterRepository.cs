namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 階級マスタリポジトリインターフェース
/// </summary>
public interface IClassMasterRepository
{
    /// <summary>
    /// 階級コードから階級名を取得する
    /// </summary>
    /// <param name="classCode">階級コード</param>
    /// <returns>階級名（見つからない場合はnull）</returns>
    Task<string?> GetClassNameAsync(string classCode);

    /// <summary>
    /// すべての階級情報を取得する
    /// </summary>
    /// <returns>階級コードと階級名のディクショナリ</returns>
    Task<Dictionary<string, string>> GetAllClassesAsync();

    /// <summary>
    /// CSVファイルからデータベースにインポートする
    /// </summary>
    /// <returns>インポートした件数</returns>
    Task<int> ImportFromCsvAsync();
}