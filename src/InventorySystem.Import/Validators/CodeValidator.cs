namespace InventorySystem.Import.Validators;

/// <summary>
/// 各種コードの汎用バリデーター
/// </summary>
public static class CodeValidator
{
    /// <summary>
    /// コードが除外対象（オール0）かどうかを判定します
    /// </summary>
    /// <param name="code">検証するコード</param>
    /// <returns>除外対象の場合はtrue</returns>
    public static bool IsExcludedCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        
        var cleanCode = code.Trim().Trim('"');
        
        if (string.IsNullOrEmpty(cleanCode))
            return false;
        
        // 数値として0かチェック
        if (decimal.TryParse(cleanCode, out var numValue) && numValue == 0)
            return true;
        
        // 文字列として全て0かチェック（例：0, 00, 000, 0000, 00000）
        if (cleanCode.All(c => c == '0'))
            return true;
        
        return false;
    }
}