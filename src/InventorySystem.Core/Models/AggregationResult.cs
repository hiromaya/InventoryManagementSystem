namespace InventorySystem.Core.Models
{
    /// <summary>
    /// 集計結果クラス
    /// </summary>
    public class AggregationResult
    {
        /// <summary>
        /// 総レコード数
        /// </summary>
        public int TotalCount { get; set; }
        
        /// <summary>
        /// 集計済みレコード数（DailyFlag='0'）
        /// </summary>
        public int AggregatedCount { get; set; }
        
        /// <summary>
        /// 未集計レコード数（DailyFlag='9'）
        /// </summary>
        public int NotAggregatedCount { get; set; }
        
        /// <summary>
        /// 取引がないレコード数（DailyFlag='0'かつ売上・仕入がゼロ）
        /// </summary>
        public int ZeroTransactionCount { get; set; }
    }
}