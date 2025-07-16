using System;
using System.Threading.Tasks;
using InventorySystem.Core.Services;

namespace InventorySystem.Core.Interfaces;

/// <summary>
/// 在庫最適化サービスインターフェース
/// </summary>
public interface IInventoryOptimizationService
{
    /// <summary>
    /// 指定日の在庫マスタを最適化
    /// </summary>
    /// <param name="jobDate">対象日</param>
    /// <returns>最適化結果</returns>
    Task<InventoryOptimizationResult> OptimizeInventoryAsync(DateTime jobDate);
}