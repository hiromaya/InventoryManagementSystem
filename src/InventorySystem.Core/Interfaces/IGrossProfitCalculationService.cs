using System;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// 粗利計算サービスのインターフェース
    /// Process 2-5: 売上伝票への在庫単価書き込みと粗利計算を実装
    /// </summary>
    public interface IGrossProfitCalculationService
    {
        /// <summary>
        /// Process 2-5を実行: 売上伝票への在庫単価書き込みと粗利計算
        /// </summary>
        /// <param name="jobDate">処理対象日</param>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>処理結果</returns>
        Task ExecuteProcess25Async(DateTime jobDate, string dataSetId);
    }
}