namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 等級マスタリポジトリインターフェース
/// </summary>
public interface IGradeMasterRepository
{
    /// <summary>
    /// 等級コードから等級名を取得する
    /// </summary>
    /// <param name="gradeCode">等級コード</param>
    /// <returns>等級名（見つからない場合はnull）</returns>
    Task<string?> GetGradeNameAsync(string gradeCode);

    /// <summary>
    /// すべての等級情報を取得する
    /// </summary>
    /// <returns>等級コードと等級名のディクショナリ</returns>
    Task<Dictionary<string, string>> GetAllGradesAsync();

    /// <summary>
    /// CSVファイルからデータベースにインポートする
    /// </summary>
    /// <returns>インポートした件数</returns>
    Task<int> ImportFromCsvAsync();
}