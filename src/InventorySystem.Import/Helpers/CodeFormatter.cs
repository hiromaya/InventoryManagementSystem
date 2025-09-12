namespace InventorySystem.Import.Helpers;

/// <summary>
/// コード変換のヘルパークラス
/// 営業日報で分類別データが表示されるよう、得意先コード・仕入先コードを5桁0埋め形式に統一
/// </summary>
public static class CodeFormatter
{
    /// <summary>
    /// 得意先コード・仕入先コードを5桁0埋め形式に変換
    /// </summary>
    /// <param name="code">変換対象のコード</param>
    /// <returns>5桁0埋め形式のコード（数値変換できない場合はそのまま返す）</returns>
    public static string? FormatTo5Digits(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;
        
        var trimmed = code.Trim();
        
        // 数値として解析可能な場合のみ変換
        if (int.TryParse(trimmed, out int numericCode))
        {
            // 5桁0埋め（例: "1" → "00001", "100" → "00100"）
            return numericCode.ToString("D5");
        }
        
        // 数値でない場合はそのまま返す（英字混在等）
        return trimmed;
    }
}