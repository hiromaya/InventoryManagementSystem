using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// アンマッチチェック結果リポジトリインターフェース
/// アンマッチチェック0件必須機能のためのデータアクセス
/// </summary>
public interface IUnmatchCheckRepository
{
    /// <summary>
    /// アンマッチチェック結果を保存または更新（Upsert）
    /// DataSetIdが既に存在する場合は更新、存在しない場合は新規作成
    /// </summary>
    /// <param name="result">保存するアンマッチチェック結果</param>
    /// <returns>処理成功可否</returns>
    Task<bool> SaveOrUpdateAsync(UnmatchCheckResult result);

    /// <summary>
    /// 指定されたDataSetIdの最新アンマッチチェック結果を取得
    /// </summary>
    /// <param name="dataSetId">データセットID</param>
    /// <returns>アンマッチチェック結果、存在しない場合はnull</returns>
    Task<UnmatchCheckResult?> GetByDataSetIdAsync(string dataSetId);

    /// <summary>
    /// 最新のアンマッチチェック結果を取得（CheckDateTime順）
    /// </summary>
    /// <returns>最新のアンマッチチェック結果、存在しない場合はnull</returns>
    Task<UnmatchCheckResult?> GetLatestAsync();

    /// <summary>
    /// 合格済み（IsPassed=true）の結果一覧を取得
    /// </summary>
    /// <param name="limit">取得件数上限（デフォルト10件）</param>
    /// <returns>合格済み結果一覧</returns>
    Task<IEnumerable<UnmatchCheckResult>> GetPassedResultsAsync(int limit = 10);

    /// <summary>
    /// 指定期間内のアンマッチチェック結果を取得
    /// </summary>
    /// <param name="startDate">開始日</param>
    /// <param name="endDate">終了日</param>
    /// <returns>期間内の結果一覧</returns>
    Task<IEnumerable<UnmatchCheckResult>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// 古いアンマッチチェック結果を削除
    /// データセットごとに最新1件のみ残し、古いレコードを削除
    /// </summary>
    /// <param name="keepDays">保持日数（デフォルト30日）</param>
    /// <returns>削除件数</returns>
    Task<int> CleanupOldResultsAsync(int keepDays = 30);

    /// <summary>
    /// アンマッチチェック結果の統計情報を取得
    /// </summary>
    /// <returns>統計情報（合格数、不合格数、エラー数）</returns>
    Task<(int PassedCount, int FailedCount, int ErrorCount)> GetStatisticsAsync();
}