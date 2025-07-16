using System;
using System.Threading.Tasks;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// DataSetsとDataSetManagementの両テーブルを統一的に管理するサービスインターフェース
    /// Phase 1: 二重書き込みによる段階的移行をサポート
    /// </summary>
    public interface IUnifiedDataSetService
    {
        /// <summary>
        /// データセットを作成します（両テーブルに書き込み）
        /// </summary>
        /// <param name="info">データセット情報</param>
        /// <returns>作成されたデータセットID（GUID）</returns>
        Task<string> CreateDataSetAsync(UnifiedDataSetInfo info);

        /// <summary>
        /// データセットのステータスを更新します
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="status">新しいステータス</param>
        /// <param name="errorMessage">エラーメッセージ（エラー時のみ）</param>
        Task UpdateStatusAsync(string dataSetId, DataSetStatus status, string? errorMessage = null);

        /// <summary>
        /// データセットのレコード数を更新します
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="recordCount">レコード数</param>
        Task UpdateRecordCountAsync(string dataSetId, int recordCount);

        /// <summary>
        /// データセットの処理を完了します
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="finalRecordCount">最終的なレコード数</param>
        Task CompleteDataSetAsync(string dataSetId, int finalRecordCount);
    }

    /// <summary>
    /// 統一データセット情報
    /// </summary>
    public class UnifiedDataSetInfo
    {
        /// <summary>
        /// データセットID（nullの場合は自動生成）
        /// </summary>
        public string? DataSetId { get; set; }

        /// <summary>
        /// 処理タイプ（SALES/PURCHASE/ADJUSTMENT/PRODUCT等）
        /// </summary>
        public string ProcessType { get; set; } = string.Empty;

        /// <summary>
        /// インポートタイプ（IMPORT/INIT/CARRYOVER/MANUAL）
        /// </summary>
        public string ImportType { get; set; } = "IMPORT";

        /// <summary>
        /// データセット名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 説明
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// ジョブ日付
        /// </summary>
        public DateTime JobDate { get; set; }

        /// <summary>
        /// 部門コード
        /// </summary>
        public string? Department { get; set; }

        /// <summary>
        /// ファイルパス
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// 作成者
        /// </summary>
        public string CreatedBy { get; set; } = "System";
    }

    /// <summary>
    /// データセットステータス
    /// </summary>
    public enum DataSetStatus
    {
        /// <summary>
        /// 処理中
        /// </summary>
        Processing,

        /// <summary>
        /// 完了
        /// </summary>
        Completed,

        /// <summary>
        /// 失敗
        /// </summary>
        Failed
    }
}