using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces.Development;

/// <summary>
/// データベース初期化サービスのインターフェース
/// </summary>
public interface IDatabaseInitializationService
{
    /// <summary>
    /// データベースを初期化する
    /// </summary>
    /// <param name="force">既存テーブルを削除して再作成するかどうか</param>
    /// <returns>初期化結果</returns>
    Task<InitializationResult> InitializeDatabaseAsync(bool force = false);
    
    /// <summary>
    /// 必要なテーブルが存在するか確認する
    /// </summary>
    /// <returns>すべてのテーブルが存在する場合はtrue</returns>
    Task<bool> CheckTablesExistAsync();
    
    /// <summary>
    /// 存在しないテーブルのリストを取得する
    /// </summary>
    /// <returns>存在しないテーブル名のリスト</returns>
    Task<List<string>> GetMissingTablesAsync();
}

/// <summary>
/// データベース初期化結果
/// </summary>
public class InitializationResult
{
    public bool Success { get; set; }
    public List<string> CreatedTables { get; set; } = new();
    public List<string> ExistingTables { get; set; } = new();
    public List<string> FailedTables { get; set; } = new();
    public List<string> ExecutedMigrations { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.Now;
    
    public string GetSummary()
    {
        var summary = $"初期化結果: {(Success ? "成功" : "失敗")} (実行時間: {ExecutionTime.TotalSeconds:F2}秒)\n";
        summary += $"作成されたテーブル: {CreatedTables.Count}個\n";
        summary += $"既存のテーブル: {ExistingTables.Count}個\n";
        if (ExecutedMigrations.Count > 0)
        {
            summary += $"実行されたマイグレーション: {ExecutedMigrations.Count}個\n";
            foreach (var migration in ExecutedMigrations)
            {
                summary += $"  - {migration}\n";
            }
        }
        if (FailedTables.Count > 0)
        {
            summary += $"失敗したテーブル: {FailedTables.Count}個\n";
            foreach (var table in FailedTables)
            {
                summary += $"  - {table}\n";
            }
        }
        if (Errors.Count > 0)
        {
            summary += $"エラー: {Errors.Count}個\n";
            foreach (var error in Errors)
            {
                summary += $"  - {error}\n";
            }
        }
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            summary += $"エラーメッセージ: {ErrorMessage}\n";
        }
        return summary;
    }
}