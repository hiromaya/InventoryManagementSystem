namespace InventorySystem.Core.Entities;

public class InventoryKey
{
    public string ProductCode { get; set; } = string.Empty;      // 商品コード (15桁)
    public string GradeCode { get; set; } = string.Empty;        // 等級コード (15桁)
    public string ClassCode { get; set; } = string.Empty;        // 階級コード (15桁)
    public string ShippingMarkCode { get; set; } = string.Empty; // 荷印コード (15桁)
    public string ShippingMarkName { get; set; } = string.Empty; // 荷印名 (50桁)

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