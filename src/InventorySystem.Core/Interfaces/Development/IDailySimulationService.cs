using System;
using System.Threading.Tasks;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Interfaces.Development;

/// <summary>
/// 日次処理シミュレーションサービスのインターフェース
/// </summary>
public interface IDailySimulationService
{
    /// <summary>
    /// 日次処理をシミュレーション実行
    /// </summary>
    /// <param name="department">部門名</param>
    /// <param name="jobDate">処理対象日</param>
    /// <param name="isDryRun">ドライランモード</param>
    /// <returns>シミュレーション結果</returns>
    Task<DailySimulationResult> SimulateDailyProcessingAsync(string department, DateTime jobDate, bool isDryRun = false);
}