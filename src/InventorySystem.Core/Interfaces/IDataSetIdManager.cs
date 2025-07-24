using System;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// DataSetIdの一元管理インターフェース
    /// JobDateとJobTypeに基づいてDataSetIdの一意性を保証する
    /// </summary>
    public interface IDataSetIdManager
    {
        /// <summary>
        /// 指定されたジョブ日付とジョブタイプに対応するDataSetIdを取得します。
        /// 存在しない場合は新しいIDを生成・永続化して返します。
        /// </summary>
        /// <param name="jobDate">ジョブの対象日付</param>
        /// <param name="jobType">ジョブの種類（例: "SalesVoucher", "CpInventoryMaster"）</param>
        /// <returns>そのジョブ実行に対応する唯一のDataSetId</returns>
        Task<string> GetOrCreateDataSetIdAsync(DateTime jobDate, string jobType);

        /// <summary>
        /// 売上伝票のDataSetIdを取得（Process 2-5用）
        /// </summary>
        /// <param name="jobDate">対象日付</param>
        /// <returns>売上伝票のDataSetId、存在しない場合はnull</returns>
        Task<string?> GetSalesVoucherDataSetIdAsync(DateTime jobDate);

        /// <summary>
        /// 指定されたJobDateとJobTypeのDataSetIdが存在するかチェック
        /// </summary>
        /// <param name="jobDate">ジョブの対象日付</param>
        /// <param name="jobType">ジョブの種類</param>
        /// <returns>存在する場合true</returns>
        Task<bool> ExistsAsync(DateTime jobDate, string jobType);

        /// <summary>
        /// JobDateで使用されているすべてのDataSetIdを取得
        /// </summary>
        /// <param name="jobDate">対象日付</param>
        /// <returns>DataSetIdのリスト</returns>
        Task<List<string>> GetAllDataSetIdsAsync(DateTime jobDate);
    }
}