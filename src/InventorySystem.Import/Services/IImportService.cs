using InventorySystem.Core.Models;

namespace InventorySystem.Import.Services;

/// <summary>
/// CSVインポートサービスの共通インターフェース
/// </summary>
public interface IImportService
{
    /// <summary>
    /// ファイル名がこのサービスで処理可能かどうかを判定
    /// </summary>
    /// <param name="fileName">ファイル名</param>
    /// <returns>処理可能な場合はtrue</returns>
    bool CanHandle(string fileName);
    
    /// <summary>
    /// インポート処理を実行
    /// </summary>
    /// <param name="filePath">インポート対象ファイルパス</param>
    /// <param name="importDate">インポート日付</param>
    /// <returns>インポート結果</returns>
    Task<ImportResult> ImportAsync(string filePath, DateTime importDate);
    
    /// <summary>
    /// サービスの表示名
    /// </summary>
    string ServiceName { get; }
    
    /// <summary>
    /// 処理順序（小さいほど先に処理される）
    /// </summary>
    int ProcessOrder { get; }
}