using InventorySystem.Core.Models;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// アンマッチチェック検証サービスインターフェース
/// 帳票実行前のアンマッチ0件必須チェック機能
/// </summary>
public interface IUnmatchCheckValidationService
{
    /// <summary>
    /// 帳票実行前の検証を実行
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="reportType">帳票種別</param>
    /// <returns>検証結果</returns>
    Task<ValidationResult> ValidateForReportExecutionAsync(string dataSetId, ReportType reportType);

    /// <summary>
    /// 最新のアンマッチチェック結果を取得
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <returns>検証結果</returns>
    Task<ValidationResult> GetLatestCheckResultAsync(string dataSetId);

    /// <summary>
    /// 指定されたDataSetIdのアンマッチチェック状況を取得
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <returns>チェック状況情報</returns>
    Task<UnmatchCheckStatus> GetCheckStatusAsync(string dataSetId);
}

/// <summary>
/// アンマッチチェック検証結果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 帳票実行可能フラグ
    /// </summary>
    public bool CanExecute { get; set; }

    /// <summary>
    /// エラーメッセージ（実行不可の場合）
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// アンマッチ件数
    /// </summary>
    public int? UnmatchCount { get; set; }

    /// <summary>
    /// 最終チェック日時
    /// </summary>
    public DateTime? LastCheckDateTime { get; set; }

    /// <summary>
    /// チェック状態
    /// </summary>
    public string? CheckStatus { get; set; }

    /// <summary>
    /// 全角エラーフラグ
    /// </summary>
    public bool HasFullWidthError { get; set; }

    /// <summary>
    /// 成功結果を作成
    /// </summary>
    /// <param name="lastCheckDateTime">最終チェック日時</param>
    /// <returns>成功の検証結果</returns>
    public static ValidationResult Success(DateTime lastCheckDateTime)
    {
        return new ValidationResult
        {
            CanExecute = true,
            UnmatchCount = 0,
            LastCheckDateTime = lastCheckDateTime,
            CheckStatus = "Passed"
        };
    }

    /// <summary>
    /// 失敗結果を作成
    /// </summary>
    /// <param name="errorMessage">エラーメッセージ</param>
    /// <param name="unmatchCount">アンマッチ件数</param>
    /// <param name="lastCheckDateTime">最終チェック日時</param>
    /// <param name="hasFullWidthError">全角エラーフラグ</param>
    /// <returns>失敗の検証結果</returns>
    public static ValidationResult Failure(string errorMessage, int? unmatchCount = null, 
        DateTime? lastCheckDateTime = null, bool hasFullWidthError = false)
    {
        return new ValidationResult
        {
            CanExecute = false,
            ErrorMessage = errorMessage,
            UnmatchCount = unmatchCount,
            LastCheckDateTime = lastCheckDateTime,
            HasFullWidthError = hasFullWidthError,
            CheckStatus = unmatchCount.HasValue ? "Failed" : "NotChecked"
        };
    }
}

/// <summary>
/// 帳票種別
/// </summary>
public enum ReportType
{
    /// <summary>
    /// 商品日報
    /// </summary>
    DailyReport,

    /// <summary>
    /// 商品勘定
    /// </summary>
    ProductAccount,

    /// <summary>
    /// 在庫表
    /// </summary>
    InventoryList,

    /// <summary>
    /// 日次終了処理
    /// </summary>
    DailyClosing
}

/// <summary>
/// アンマッチチェック状況
/// </summary>
public class UnmatchCheckStatus
{
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;

    /// <summary>
    /// チェック済みフラグ
    /// </summary>
    public bool IsChecked { get; set; }

    /// <summary>
    /// 合格フラグ（0件達成）
    /// </summary>
    public bool IsPassed { get; set; }

    /// <summary>
    /// アンマッチ件数
    /// </summary>
    public int UnmatchCount { get; set; }

    /// <summary>
    /// 最終チェック日時
    /// </summary>
    public DateTime? LastCheckDateTime { get; set; }

    /// <summary>
    /// チェック状態
    /// </summary>
    public string CheckStatus { get; set; } = string.Empty;

    /// <summary>
    /// 全角エラーフラグ
    /// </summary>
    public bool HasFullWidthError { get; set; }

    /// <summary>
    /// ステータス表示用文字列
    /// </summary>
    public string GetStatusDisplay()
    {
        if (!IsChecked)
            return "未チェック";

        return CheckStatus switch
        {
            "Passed" => "✅ 合格（0件）",
            "Failed" => HasFullWidthError ? $"❌ 全角エラー（{UnmatchCount}件）" : $"❌ アンマッチあり（{UnmatchCount}件）",
            "Error" => "⚠️ エラー",
            _ => "不明"
        };
    }
}