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
    
    /// <summary>
    /// 日次終了処理は15:00（日本時間）以降にのみ実行可能です。
    /// </summary>
    public const string DailyCloseTimeTooEarly = "日次終了処理は15:00（日本時間）以降にのみ実行可能です。現在時刻: {0} JST";
    
    /// <summary>
    /// 商品日報作成から30分以上経過する必要があります。
    /// </summary>
    public const string DailyReportTooRecent = "商品日報作成（{0} JST）から30分以上経過する必要があります。経過時間: {1}分";
    
    /// <summary>
    /// データが商品日報作成時から変更されています。
    /// </summary>
    public const string DataIntegrityError = "データが商品日報作成時から変更されています。商品日報作成後に以下のデータが変更されました: {0}";
    
    /// <summary>
    /// CSV取込から5分以上経過する必要があります。
    /// </summary>
    public const string CsvImportTooRecent = "CSV取込（{0} JST）から5分以上経過する必要があります。経過時間: {1}分";
    
    /// <summary>
    /// 商品日報が作成されていません。
    /// </summary>
    public const string DailyReportNotFound = "対象日付の商品日報が作成されていません。先に商品日報を作成してください。";
    
    /// <summary>
    /// 日次終了処理は既に実行されています。
    /// </summary>
    public const string DailyCloseAlreadyExecuted = "対象日付の日次終了処理は既に実行されています。実行時刻: {0}";
}