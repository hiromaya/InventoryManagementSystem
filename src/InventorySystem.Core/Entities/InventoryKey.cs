using System.Linq;

namespace InventorySystem.Core.Entities;

public class InventoryKey
{
    private string _productCode = string.Empty;
    private string _gradeCode = string.Empty;
    private string _classCode = string.Empty;
    private string _shippingMarkCode = string.Empty;
    private string _shippingMarkName = string.Empty;

    /// <summary>
    /// 商品コード（5桁固定、左0埋め）
    /// </summary>
    public string ProductCode 
    { 
        get => _productCode;
        set => _productCode = PadProductCode(value);
    }

    /// <summary>
    /// 等級コード（3桁固定、左0埋め）
    /// </summary>
    public string GradeCode 
    { 
        get => _gradeCode;
        set => _gradeCode = PadGradeCode(value);
    }

    /// <summary>
    /// 階級コード（3桁固定、左0埋め）
    /// </summary>
    public string ClassCode 
    { 
        get => _classCode;
        set => _classCode = PadClassCode(value);
    }

    /// <summary>
    /// 荷印コード（4桁固定、左0埋め）
    /// </summary>
    public string ShippingMarkCode 
    { 
        get => _shippingMarkCode;
        set => _shippingMarkCode = PadShippingMarkCode(value);
    }

    /// <summary>
    /// デフォルトコンストラクター
    /// </summary>
    public InventoryKey()
    {
    }

    /// <summary>
    /// 5項目複合キーを指定するコンストラクター
    /// すべての値は自動的に正しい桁数でフォーマットされます
    /// </summary>
    /// <param name="productCode">商品コード（5桁に左0埋め）</param>
    /// <param name="gradeCode">等級コード（3桁に左0埋め）</param>
    /// <param name="classCode">階級コード（3桁に左0埋め）</param>
    /// <param name="shippingMarkCode">荷印コード（4桁に左0埋め）</param>
    /// <param name="shippingMarkName">荷印名（8桁固定）</param>
    public InventoryKey(string productCode, string gradeCode, string classCode, string shippingMarkCode, string shippingMarkName)
    {
        ProductCode = productCode ?? string.Empty;      // セッターで自動0埋め
        GradeCode = gradeCode ?? string.Empty;          // セッターで自動0埋め
        ClassCode = classCode ?? string.Empty;          // セッターで自動0埋め
        ShippingMarkCode = shippingMarkCode ?? string.Empty; // セッターで自動0埋め
        ShippingMarkName = shippingMarkName ?? string.Empty; // セッターで自動8桁化
    }
    
    /// <summary>
    /// 荷印名（8桁固定長で正規化される）
    /// </summary>
    public string ShippingMarkName 
    { 
        get => _shippingMarkName;
        set => _shippingMarkName = NormalizeShippingMarkName(value);
    }

    /// <summary>
    /// ShippingMarkNameを8桁固定長に正規化する
    /// </summary>
    /// <param name="value">正規化する荷印名</param>
    /// <returns>8桁固定長の荷印名</returns>
    public static string NormalizeShippingMarkName(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return new string(' ', 8);
        
        // 右側の空白を削除し、8桁に調整（不足分は空白で埋める）
        var trimmed = value.TrimEnd();
        return trimmed.Length >= 8 
            ? trimmed.Substring(0, 8) 
            : trimmed.PadRight(8, ' ');
    }

    /// <summary>
    /// 商品コードを5桁の左0埋め形式に変換
    /// </summary>
    /// <param name="value">商品コード</param>
    /// <returns>5桁左0埋めの商品コード</returns>
    public static string PadProductCode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "00000";
        
        // 数値部分のみ抽出して0埋め
        var numericValue = new string(value.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(numericValue)) return "00000";
        
        return numericValue.PadLeft(5, '0');
    }

    /// <summary>
    /// 等級コードを3桁の左0埋め形式に変換
    /// </summary>
    /// <param name="value">等級コード</param>
    /// <returns>3桁左0埋めの等級コード</returns>
    public static string PadGradeCode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "000";
        
        // 数値部分のみ抽出して0埋め
        var numericValue = new string(value.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(numericValue)) return "000";
        
        return numericValue.PadLeft(3, '0');
    }

    /// <summary>
    /// 階級コードを3桁の左0埋め形式に変換
    /// </summary>
    /// <param name="value">階級コード</param>
    /// <returns>3桁左0埋めの階級コード</returns>
    public static string PadClassCode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "000";
        
        // 数値部分のみ抽出して0埋め
        var numericValue = new string(value.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(numericValue)) return "000";
        
        return numericValue.PadLeft(3, '0');
    }

    /// <summary>
    /// 荷印コードを4桁の左0埋め形式に変換
    /// </summary>
    /// <param name="value">荷印コード</param>
    /// <returns>4桁左0埋めの荷印コード</returns>
    public static string PadShippingMarkCode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "0000";
        
        // 数値部分のみ抽出して0埋め
        var numericValue = new string(value.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(numericValue)) return "0000";
        
        return numericValue.PadLeft(4, '0');
    }

    /// <summary>
    /// 荷印名を設定する（明示的な正規化メソッド）
    /// </summary>
    /// <param name="value">荷印名</param>
    public void SetShippingMarkName(string? value)
    {
        _shippingMarkName = NormalizeShippingMarkName(value);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not InventoryKey other) return false;
        
        return ProductCode == other.ProductCode &&
               GradeCode == other.GradeCode &&
               ClassCode == other.ClassCode &&
               ShippingMarkCode == other.ShippingMarkCode &&
               ShippingMarkName == other.ShippingMarkName;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);
    }

    public override string ToString()
    {
        return $"{ProductCode}-{GradeCode}-{ClassCode}-{ShippingMarkCode}-{ShippingMarkName}";
    }
}