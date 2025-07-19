using System;

namespace InventorySystem.Core.Interfaces
{
    /// <summary>
    /// 時刻提供サービスのインターフェース
    /// テスト容易性とタイムゾーン一貫性を確保するための抽象化層
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>
        /// 現在のUTC時刻を取得します
        /// データベース保存やビジネスロジックで使用することを推奨
        /// </summary>
        DateTime UtcNow { get; }
        
        /// <summary>
        /// 現在のローカル時刻を取得します
        /// ログ出力やUI表示で使用（推奨：UtcNowからの変換を使用）
        /// </summary>
        DateTime Now { get; }
        
        /// <summary>
        /// 現在の日付を取得します（時刻部分なし）
        /// JobDate等の日付のみのフィールドで使用
        /// </summary>
        DateOnly Today { get; }
    }
}