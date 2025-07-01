using System;
using System.Threading.Tasks;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Interfaces.Services
{
    /// <summary>
    /// 前月末在庫CSVインポートサービスのインターフェース
    /// </summary>
    public interface IPreviousMonthInventoryImportService
    {
        /// <summary>
        /// 前月末在庫CSVファイルをインポートする
        /// </summary>
        /// <param name="filePath">CSVファイルのパス</param>
        /// <param name="dataSetId">データセットID</param>
        /// <param name="jobDate">処理日付</param>
        /// <returns>インポート結果</returns>
        Task<ImportResult> ImportAsync(string filePath, string dataSetId, DateTime jobDate);
    }
}