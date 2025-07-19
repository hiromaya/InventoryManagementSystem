using System;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// 日本標準時（JST）の現在時刻を提供する ITimeProvider の実装です。
    /// </summary>
    public class JstTimeProvider : ITimeProvider
    {
        // JSTのタイムゾーン情報をキャッシュしておく
        private static readonly TimeZoneInfo JstZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

        /// <summary>
        /// 現在のUTC時刻をJSTに変換して返します。
        /// </summary>
        public DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, JstZoneInfo);

        /// <summary>
        /// JSTの現在時刻からUTC時刻を返します。
        /// </summary>
        public DateTime UtcNow => Now.UtcDateTime;

        /// <summary>
        /// JSTの現在日付を返します。
        /// </summary>
        public DateOnly Today => DateOnly.FromDateTime(Now.Date);
    }
}