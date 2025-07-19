using System;

namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// アプリケーション全体の時刻を提供します。
    /// JST（日本標準時）での現在時刻を DateTimeOffset として提供することを基本とします。
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>
        /// 現在時刻をJST（UTC+9）のタイムゾーン情報を持つ DateTimeOffset として取得します。
        /// これが時刻に関する唯一の信頼できる情報源となります。
        /// </summary>
        DateTimeOffset Now { get; }

        /// <summary>
        /// 現在時刻をUTC（協定世界時）の DateTime として取得します。
        /// このプロパティは Now.UtcDateTime から派生します。
        /// </summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// JST（日本標準時）における現在の日付を取得します。
        /// このプロパティは Now.Date から派生します。
        /// </summary>
        DateOnly Today { get; }
    }
}