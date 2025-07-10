using System;

namespace InventorySystem.Core.Services;

/// <summary>
/// 日本標準時（JST）での時刻を提供するサービス
/// </summary>
public interface IJapanTimeService
{
    /// <summary>
    /// 現在の日本時間を取得
    /// </summary>
    DateTime Now { get; }
    
    /// <summary>
    /// 現在の日本時間の日付部分を取得
    /// </summary>
    DateTime Today { get; }
    
    /// <summary>
    /// UTC時刻を日本時間に変換
    /// </summary>
    DateTime ConvertFromUtc(DateTime utcDateTime);
}

/// <summary>
/// 日本標準時（JST）での時刻を提供するサービスの実装
/// </summary>
public class JapanTimeService : IJapanTimeService
{
    private static readonly TimeZoneInfo JstTimeZone;
    
    static JapanTimeService()
    {
        // クロスプラットフォーム対応
        try
        {
            // Windows環境
            JstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Linux/Mac環境
                JstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
            }
            catch (TimeZoneNotFoundException)
            {
                // フォールバック: UTC+9として手動作成
                JstTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                    "JST",
                    TimeSpan.FromHours(9),
                    "Japan Standard Time",
                    "Japan Standard Time"
                );
            }
        }
    }
    
    /// <inheritdoc/>
    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, JstTimeZone);
    
    /// <inheritdoc/>
    public DateTime Today => Now.Date;
    
    /// <inheritdoc/>
    public DateTime ConvertFromUtc(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be in UTC", nameof(utcDateTime));
        }
        
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, JstTimeZone);
    }
}