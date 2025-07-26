using InventorySystem.Core.Models;

namespace InventorySystem.Core.Services.Validation;

/// <summary>
/// 日付検証サービスインターフェース
/// </summary>
public interface IDateValidationService
{
    /// <summary>
    /// ジョブ日付を検証
    /// </summary>
    /// <param name="jobDate">処理対象日付</param>
    /// <param name="processType">処理種別</param>
    /// <param name="allowDuplicateProcessing">重複処理を許可するか（開発用）</param>
    /// <returns>検証結果</returns>
    Task<Models.ValidationResult> ValidateJobDate(DateTime jobDate, string processType, bool allowDuplicateProcessing = false);
    
    /// <summary>
    /// 最後に処理された日付を取得
    /// </summary>
    /// <param name="processType">処理種別</param>
    /// <returns>最終処理日付（存在しない場合はnull）</returns>
    Task<DateTime?> GetLastProcessedDate(string processType);
    
    /// <summary>
    /// 指定日付が既に処理済みかチェック
    /// </summary>
    /// <param name="jobDate">処理対象日付</param>
    /// <param name="processType">処理種別</param>
    /// <returns>処理済みの場合true</returns>
    Task<bool> IsDateAlreadyProcessed(DateTime jobDate, string processType);
    
    /// <summary>
    /// 特殊日付範囲（年末年始等）かどうかチェック
    /// </summary>
    /// <param name="jobDate">処理対象日付</param>
    /// <returns>特殊日付範囲の場合true</returns>
    bool IsSpecialDateRange(DateTime jobDate);
    
    /// <summary>
    /// 特殊日付範囲を取得
    /// </summary>
    /// <param name="jobDate">処理対象日付</param>
    /// <returns>日付範囲（開始日・終了日）</returns>
    (DateTime Start, DateTime End) GetSpecialDateRange(DateTime jobDate);
}