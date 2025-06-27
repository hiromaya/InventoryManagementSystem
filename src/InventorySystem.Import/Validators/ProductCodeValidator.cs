using System.Linq;

namespace InventorySystem.Import.Validators;

/// <summary>
/// 商品コードの検証を行うクラス
/// </summary>
public static class ProductCodeValidator
{
    /// <summary>
    /// 商品コードが除外対象かどうかを判定します
    /// </summary>
    /// <param name="productCode">商品コード</param>
    /// <returns>除外対象の場合true</returns>
    public static bool IsExcludedProductCode(string? productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode))
            return false;
        
        // クォートを除去
        var cleanCode = productCode.Trim().Trim('"');
        
        // 空文字列の場合は除外しない
        if (string.IsNullOrEmpty(cleanCode))
            return false;
        
        // 数値として0かチェック
        if (decimal.TryParse(cleanCode, out var numValue) && numValue == 0)
            return true;
        
        // 文字列として全て0かチェック
        if (cleanCode.All(c => c == '0'))
            return true;
        
        return false;
    }
}