using System;

namespace InventorySystem.Core.Interfaces.Services
{
    /// <summary>
    /// 年末年始などの特殊期間の日付範囲を管理するサービスのインターフェース
    /// </summary>
    public interface ISpecialDateRangeService
    {
        /// <summary>
        /// 指定された日付が特殊期間内かどうかを判定
        /// </summary>
        /// <param name="date">判定対象の日付</param>
        /// <returns>特殊期間内の場合はtrue</returns>
        bool IsInSpecialDateRange(DateTime date);

        /// <summary>
        /// 指定された日付範囲が特殊期間と重複するかどうかを判定
        /// </summary>
        /// <param name="fromDate">開始日</param>
        /// <param name="toDate">終了日</param>
        /// <returns>重複がある場合はtrue</returns>
        bool HasOverlapWithSpecialRange(DateTime fromDate, DateTime toDate);

        /// <summary>
        /// 特殊期間を考慮して日付範囲を調整
        /// </summary>
        /// <param name="fromDate">元の開始日</param>
        /// <param name="toDate">元の終了日</param>
        /// <returns>調整後の開始日と終了日</returns>
        (DateTime adjustedFrom, DateTime adjustedTo) AdjustDateRangeForSpecialPeriod(DateTime fromDate, DateTime toDate);
    }
}