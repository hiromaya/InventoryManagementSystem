using InventorySystem.Core.Models;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 日次終了処理サービスインターフェース
/// </summary>
public interface IDailyCloseService
{
    /// <summary>
    /// 日次終了処理を実行
    /// </summary>
    /// <param name="jobDate">処理対象日付</param>
    /// <param name="executedBy">実行者</param>
    Task ExecuteDailyClose(DateTime jobDate, string executedBy = "System");
    
    /// <summary>
    /// 日次終了処理が完了しているかチェック
    /// </summary>
    /// <param name="jobDate">処理対象日付</param>
    /// <returns>完了している場合true</returns>
    Task<bool> IsDailyClosedAsync(DateTime jobDate);
    
    /// <summary>
    /// 日次終了処理の確認情報を取得
    /// </summary>
    /// <param name="jobDate">処理対象日付</param>
    /// <returns>確認情報</returns>
    Task<DailyCloseConfirmation> GetConfirmationInfo(DateTime jobDate);
    
    /// <summary>
    /// 開発環境用の日次終了処理実行メソッド
    /// </summary>
    /// <param name="jobDate">処理対象日</param>
    /// <param name="skipValidation">バリデーションをスキップするか</param>
    /// <param name="dryRun">ドライランモード（実際の更新を行わない）</param>
    /// <returns>処理結果</returns>
    Task<DailyCloseResult> ExecuteDevelopmentAsync(DateTime jobDate, bool skipValidation = false, bool dryRun = false);
}