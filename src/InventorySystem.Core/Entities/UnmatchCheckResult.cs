using System.ComponentModel.DataAnnotations;
using InventorySystem.Core.Models;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Entities;

/// <summary>
/// アンマッチチェック結果エンティティ
/// アンマッチチェック0件必須機能のための結果保存
/// </summary>
public class UnmatchCheckResult
{
    /// <summary>
    /// データセットID（プライマリキー）
    /// </summary>
    [Key]
    [MaxLength(50)]
    public string DataSetId { get; set; } = string.Empty;

    /// <summary>
    /// チェック実行日時
    /// </summary>
    public DateTime CheckDateTime { get; set; }

    /// <summary>
    /// アンマッチ件数
    /// </summary>
    public int UnmatchCount { get; set; }

    /// <summary>
    /// 全角エラーフラグ
    /// 荷印名の全角文字等のエラーの有無
    /// </summary>
    public bool HasFullWidthError { get; set; }

    /// <summary>
    /// チェック合格フラグ（0件達成）
    /// true: アンマッチ0件で帳票実行可能
    /// false: アンマッチありで帳票実行不可
    /// </summary>
    public bool IsPassed { get; set; }

    /// <summary>
    /// チェック状態
    /// Passed: 正常終了・0件達成
    /// Failed: 正常終了・アンマッチあり
    /// Error: 処理エラー
    /// </summary>
    [MaxLength(20)]
    public string CheckStatus { get; set; } = string.Empty;

    /// <summary>
    /// エラーメッセージ
    /// チェック状態がErrorの場合の詳細メッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 帳票実行可能かどうか判定
    /// </summary>
    /// <returns>true: 実行可能, false: 実行不可</returns>
    public bool CanExecuteReport() => IsPassed && CheckStatus == "Passed";

    /// <summary>
    /// エラーメッセージ生成
    /// 帳票実行不可の場合の理由を返す
    /// </summary>
    /// <returns>エラーメッセージ</returns>
    public string GetErrorMessage()
    {
        if (CheckStatus == "Error")
        {
            return $"アンマッチチェックでエラーが発生しています: {ErrorMessage}";
        }

        if (!IsPassed && UnmatchCount > 0)
        {
            if (HasFullWidthError)
            {
                return $"全角データエラー！！修正してください！（{UnmatchCount}件）";
            }
            else
            {
                return $"アンマッチデータあり！！修正してください！（{UnmatchCount}件）";
            }
        }

        return "アンマッチチェックが実行されていません";
    }

    /// <summary>
    /// チェック結果のサマリー文字列を生成
    /// </summary>
    /// <returns>サマリー文字列</returns>
    public string GetSummary()
    {
        return $"[{CheckDateTime:yyyy-MM-dd HH:mm:ss}] {CheckStatus}: {UnmatchCount}件" +
               (HasFullWidthError ? " (全角エラーあり)" : "");
    }

    /// <summary>
    /// UnmatchListResultから UnmatchCheckResult を作成
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="result">アンマッチリスト処理結果</param>
    /// <returns>UnmatchCheckResult インスタンス</returns>
    public static UnmatchCheckResult FromUnmatchListResult(string dataSetId, UnmatchListResult result)
    {
        // 全角エラーの判定（AlertType2で"全角エラー"があるかチェック）
        var hasFullWidthError = result.UnmatchItems?.Any(x => 
            !string.IsNullOrEmpty(x.AlertType2) && x.AlertType2.Contains("全角")) ?? false;

        return new UnmatchCheckResult
        {
            DataSetId = dataSetId,
            CheckDateTime = DateTime.Now,
            UnmatchCount = result.UnmatchCount,
            HasFullWidthError = hasFullWidthError,
            IsPassed = result.Success && result.UnmatchCount == 0,
            CheckStatus = result.Success ? (result.UnmatchCount == 0 ? "Passed" : "Failed") : "Error",
            ErrorMessage = result.ErrorMessage,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }
}