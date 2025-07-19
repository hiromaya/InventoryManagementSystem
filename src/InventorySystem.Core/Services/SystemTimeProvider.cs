using System;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// システム時刻を提供する実装クラス
    /// 本番環境での標準的な時刻取得を行う
    /// </summary>
    public class SystemTimeProvider : ITimeProvider
    {
        /// <summary>
        /// 現在のUTC時刻を取得します
        /// データベース保存時の標準として使用
        /// </summary>
        public DateTime UtcNow => DateTime.UtcNow;
        
        /// <summary>
        /// 現在のローカル時刻を取得します
        /// 表示用途で使用（内部的にはUtcNowからの変換を推奨）
        /// </summary>
        public DateTime Now => DateTime.Now;
        
        /// <summary>
        /// 現在の日付を取得します
        /// JobDate等の日付フィールドで使用
        /// </summary>
        public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    }
}