using System;

namespace InventorySystem.Core.Exceptions;

/// <summary>
/// 日次終了処理の重複実行時にスローされる例外
/// </summary>
public class DuplicateDailyCloseException : Exception
{
    /// <summary>
    /// JobDate
    /// </summary>
    public DateTime JobDate { get; }

    /// <summary>
    /// 既存の ValidationStatus
    /// </summary>
    public string? ExistingStatus { get; }

    /// <summary>
    /// DuplicateDailyCloseExceptionの新しいインスタンスを初期化します
    /// </summary>
    /// <param name="jobDate">重複したJobDate</param>
    /// <param name="existingStatus">既存のValidationStatus</param>
    /// <param name="message">エラーメッセージ</param>
    /// <param name="innerException">内部例外</param>
    public DuplicateDailyCloseException(DateTime jobDate, string? existingStatus = null, string? message = null, Exception? innerException = null)
        : base(message ?? $"指定された日付 ({jobDate:yyyy-MM-dd}) の日次終了処理は既に存在します (Status: {existingStatus ?? "不明"})", innerException)
    {
        JobDate = jobDate;
        ExistingStatus = existingStatus;
    }

    /// <summary>
    /// DuplicateDailyCloseExceptionの新しいインスタンスを初期化します
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    /// <param name="innerException">内部例外</param>
    public DuplicateDailyCloseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
        JobDate = DateTime.MinValue;
    }
}