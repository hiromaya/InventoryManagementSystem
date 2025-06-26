namespace InventorySystem.Core.Constants;

/// <summary>
/// エラーメッセージ定数クラス
/// </summary>
public static class ErrorMessages
{
    /// <summary>
    /// 未来日エラー
    /// </summary>
    public const string FutureDateError = "【異常終了】翌日以降のデータが選択されています。再度、エクスポートをやりなおしてください！！";
    
    /// <summary>
    /// 重複処理エラー
    /// </summary>
    public const string AlreadyProcessedError = "この日付は既に処理済みです。重複処理はできません。";
    
    /// <summary>
    /// 商品日報未作成エラー
    /// </summary>
    public const string NoDailyReportError = "商品日報が作成されていません。先に商品日報を作成してください。";
    
    /// <summary>
    /// バックアップ失敗エラー
    /// </summary>
    public const string BackupFailedError = "バックアップの作成に失敗しました。";
    
    /// <summary>
    /// 過去日付範囲超過エラー
    /// </summary>
    public const string PastDateRangeError = "処理可能な日付範囲を超えています。{0}日以内の日付を指定してください。";
    
    /// <summary>
    /// データセット不整合エラー
    /// </summary>
    public const string DatasetInconsistentError = "データセットが見つかりません。データの整合性を確認してください。";
}