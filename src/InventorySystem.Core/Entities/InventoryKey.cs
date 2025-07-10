namespace InventorySystem.Core.Entities;

public class InventoryKey
{
    private string _shippingMarkName = string.Empty;

    public string ProductCode { get; set; } = string.Empty;      // 商品コード (15桁)
    public string GradeCode { get; set; } = string.Empty;        // 等級コード (15桁)
    public string ClassCode { get; set; } = string.Empty;        // 階級コード (15桁)
    public string ShippingMarkCode { get; set; } = string.Empty; // 荷印コード (15桁)
    
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