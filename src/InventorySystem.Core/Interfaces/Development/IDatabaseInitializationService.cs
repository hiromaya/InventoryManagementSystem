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
/// データベース初期化結果（拡張版）
/// </summary>
public class InitializationResult
{
    public bool Success { get; set; }
    public List<string> CreatedTables { get; set; } = new();
    public List<string> ExistingTables { get; set; } = new();
    public List<string> FailedTables { get; set; } = new();
    public List<string> ExecutedMigrations { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.Now;
    
    // 新規追加プロパティ
    public DatabaseValidationResult? ValidationResult { get; set; }
    public int TotalMigrationCount { get; set; }
    public int SkippedMigrationCount { get; set; }
    public bool ForceMode { get; set; }
    public List<string> AppliedMigrationOrder { get; set; } = new();
    public Dictionary<string, long> MigrationExecutionTimes { get; set; } = new();
    public string DatabaseVersion { get; set; } = string.Empty;
    public int CreatedIndexCount { get; set; }
    public int ExistingIndexCount { get; set; }
    public List<string> DetectedIssues { get; set; } = new();
    
    // 統計情報
    public int TotalTableCount => CreatedTables.Count + ExistingTables.Count;
    public int TotalErrorCount => Errors.Count + FailedTables.Count;
    public bool HasWarnings => Warnings.Count > 0;
    public bool HasValidationIssues => ValidationResult?.IsValid == false;
    
    public string GetSummary()
    {
        var summary = $"初期化結果: {(Success ? "成功" : "失敗")} (実行時間: {ExecutionTime.TotalSeconds:F2}秒)\n";
        summary += $"モード: {(ForceMode ? "強制再作成" : "通常")}\n";
        summary += $"作成されたテーブル: {CreatedTables.Count}個\n";
        summary += $"既存のテーブル: {ExistingTables.Count}個\n";
        summary += $"総テーブル数: {TotalTableCount}個\n";
        
        if (ExecutedMigrations.Count > 0)
        {
            summary += $"実行されたマイグレーション: {ExecutedMigrations.Count}個 (スキップ: {SkippedMigrationCount}個)\n";
            foreach (var migration in ExecutedMigrations)
            {
                var executionTime = MigrationExecutionTimes.ContainsKey(migration) 
                    ? $" ({MigrationExecutionTimes[migration]}ms)" 
                    : "";
                summary += $"  - {migration}{executionTime}\n";
            }
        }
        
        if (CreatedIndexCount > 0 || ExistingIndexCount > 0)
        {
            summary += $"インデックス: 作成 {CreatedIndexCount}個, 既存 {ExistingIndexCount}個\n";
        }
        
        if (Warnings.Count > 0)
        {
            summary += $"警告: {Warnings.Count}個\n";
            foreach (var warning in Warnings)
            {
                summary += $"  - {warning}\n";
            }
        }
        
        if (DetectedIssues.Count > 0)
        {
            summary += $"検出された問題: {DetectedIssues.Count}個\n";
            foreach (var issue in DetectedIssues)
            {
                summary += $"  - {issue}\n";
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
        
        if (ValidationResult != null)
        {
            summary += $"データベース検証: {(ValidationResult.IsValid ? "OK" : "エラー")}\n";
            if (!ValidationResult.IsValid)
            {
                summary += $"  - 不足テーブル: {ValidationResult.MissingTables.Count}個\n";
                summary += $"  - 不足インデックス: {ValidationResult.MissingIndexes.Count}個\n";
                summary += $"  - データ整合性問題: {ValidationResult.DataIntegrityIssues.Count}個\n";
            }
        }
        
        if (!string.IsNullOrEmpty(DatabaseVersion))
        {
            summary += $"データベースバージョン: {DatabaseVersion}\n";
        }
        
        return summary;
    }
}

/// <summary>
/// データベース検証結果
/// </summary>
public class DatabaseValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> MissingTables { get; set; } = new();
    public List<string> MissingIndexes { get; set; } = new();
    public List<string> DataIntegrityIssues { get; set; } = new();
}