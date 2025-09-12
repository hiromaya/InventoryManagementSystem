using System;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// 移行用在庫マスタ（InventoryCarryoverMaster）関連の操作
    /// </summary>
    public interface ICarryoverRepository
    {
        /// <summary>
        /// 指定日のCP在庫マスタからCarryoverへMERGE（日次終了処理用）
        /// </summary>
        Task<int> MergeFromCpInventoryAsync(DateTime jobDate, string dataSetId);
    }
}

