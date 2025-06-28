using System;
using System.Threading.Tasks;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 在庫マスタ最適化サービスのインターフェース
/// </summary>
public interface IInventoryMasterOptimizationService
{
    /// <summary>
    /// 指定日付の在庫マスタを最適化する
    /// </summary>
    /// <param name="jobDate">対象日付</param>
    /// <returns>追加された在庫マスタ件数</returns>
    Task<int> OptimizeInventoryMasterAsync(DateTime jobDate);
    
    /// <summary>
    /// 日付範囲の在庫マスタを最適化する
    /// </summary>
    /// <param name="startDate">開始日付</param>
    /// <param name="endDate">終了日付</param>
    /// <returns>追加された在庫マスタ件数</returns>
    Task<int> OptimizeInventoryMasterRangeAsync(DateTime startDate, DateTime endDate);
}