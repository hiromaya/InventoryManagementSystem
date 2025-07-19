using System;
using System.Collections.Generic;
using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Factories
{
    /// <summary>
    /// DataSetManagementエンティティの生成を担当するファクトリのインターフェース
    /// Gemini推奨：エンティティの生成ロジックを集約し、時刻設定の一貫性を保証
    /// </summary>
    public interface IDataSetManagementFactory
    {
        /// <summary>
        /// 新しいDataSetManagementエンティティを作成します
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="jobDate">ジョブ日付</param>
        /// <param name="processType">処理種別</param>
        /// <param name="createdBy">作成者</param>
        /// <param name="department">部門</param>
        /// <param name="importType">インポート種別（省略時はprocessTypeから自動判定）</param>
        /// <param name="importedFiles">インポートファイル一覧（省略可）</param>
        /// <param name="notes">備考（省略可）</param>
        /// <returns>時刻が適切に設定されたDataSetManagementエンティティ</returns>
        DataSetManagement CreateNew(
            string dataSetId,
            DateTime jobDate,
            string processType,
            string createdBy = "System",
            string department = "DeptA",
            string? importType = null,
            List<string>? importedFiles = null,
            string? notes = null);

        /// <summary>
        /// 繰越処理用のDataSetManagementエンティティを作成します
        /// </summary>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="targetDate">繰越対象日</param>
        /// <param name="department">部門</param>
        /// <param name="recordCount">レコード数</param>
        /// <param name="parentDataSetId">親データセットID（省略可）</param>
        /// <param name="notes">備考（省略可）</param>
        /// <returns>繰越処理用DataSetManagementエンティティ</returns>
        DataSetManagement CreateForCarryover(
            string dataSetId,
            DateTime targetDate,
            string department,
            int recordCount,
            string? parentDataSetId = null,
            string? notes = null);

        /// <summary>
        /// 既存のDataSetManagementエンティティのUpdatedAtを現在時刻で更新します
        /// </summary>
        /// <param name="dataSetManagement">更新対象のエンティティ</param>
        void UpdateTimestamp(DataSetManagement dataSetManagement);
    }
}