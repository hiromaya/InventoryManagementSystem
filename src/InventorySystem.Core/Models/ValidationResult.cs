namespace InventorySystem.Core.Models;

/// <summary>
/// 検証結果クラス
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 検証が成功したかどうか
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// エラーメッセージ（検証失敗時）
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// 追加情報
    /// </summary>
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();
    
    /// <summary>
    /// 成功結果を生成
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }
    
    /// <summary>
    /// 失敗結果を生成
    /// </summary>
    public static ValidationResult Failure(string message)
    {
        return new ValidationResult 
        { 
            IsValid = false, 
            Message = message 
        };
    }
}