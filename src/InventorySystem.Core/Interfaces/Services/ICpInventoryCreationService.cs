using System;
using System.Threading.Tasks;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Interfaces.Services
{
    /// <summary>
    /// CP在庫マスタ作成サービスのインターフェース
    /// </summary>
    public interface ICpInventoryCreationService
    {
        /// <summary>
        /// 移行用在庫マスタ（IsActive=1）からCP在庫マスタを作成する（仮テーブル設計）
        /// </summary>
        /// <param name="jobDate">処理日付</param>
        /// <returns>作成結果</returns>
        Task<CpInventoryCreationResult> CreateCpInventoryFromCarryoverAsync(DateTime jobDate);

        /// <summary>
        /// 在庫マスタに存在しない商品を検出する
        /// </summary>
        /// <param name="jobDate">処理日付</param>
        /// <returns>未登録商品のリスト</returns>
        Task<MissingProductsResult> DetectMissingProductsAsync(DateTime jobDate);
    }
}
