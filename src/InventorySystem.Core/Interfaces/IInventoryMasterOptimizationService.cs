using System;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces.Services
{
    /// <summary>
    /// 在庫マスタ最適化サービスのインターフェース
    /// </summary>
    public interface IInventoryMasterOptimizationService
    {
        /// <summary>
        /// 指定日の在庫マスタを最適化する
        /// </summary>
        /// <param name="jobDate">処理対象日</param>
        /// <param name="dataSetId">データセットID</param>
        /// <returns>最適化結果</returns>
        Task<OptimizationResult> OptimizeAsync(DateTime jobDate, string dataSetId);
    }

    /// <summary>
    /// 在庫マスタ最適化結果
    /// </summary>
    public class OptimizationResult
    {
        /// <summary>売上伝票の商品種類数</summary>
        public int SalesProductCount { get; set; }
        
        /// <summary>仕入伝票の商品種類数</summary>
        public int PurchaseProductCount { get; set; }
        
        /// <summary>在庫調整の商品種類数</summary>
        public int AdjustmentProductCount { get; set; }
        
        /// <summary>処理済み件数</summary>
        public int ProcessedCount { get; set; }
        
        /// <summary>新規登録件数</summary>
        public int InsertedCount { get; set; }
        
        /// <summary>更新件数</summary>
        public int UpdatedCount { get; set; }
        
        /// <summary>エラー件数</summary>
        public int ErrorCount { get; set; }
        
        /// <summary>処理成功フラグ</summary>
        public bool IsSuccess => ErrorCount == 0;
    }
}