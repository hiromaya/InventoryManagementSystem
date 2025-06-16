namespace InventorySystem.Core.Entities;

public class SalesVoucher
{
    public int VoucherId { get; set; }                          // 伝票ID
    public string VoucherNumber { get; set; } = string.Empty;   // 伝票番号
    public string VoucherType { get; set; } = string.Empty;     // 伝票種類
    public string DetailType { get; set; } = string.Empty;      // 明細種
    public int LineNumber { get; set; }                         // 行番号
    public DateTime VoucherDate { get; set; }                   // 伝票日付
    public DateTime JobDate { get; set; }                       // 汎用日付2（ジョブデート）
    
    public InventoryKey InventoryKey { get; set; } = new();
    
    // 便利プロパティ（InventoryKeyの各要素へのアクセス）
    public string ProductCode => InventoryKey.ProductCode;
    public string GradeCode => InventoryKey.GradeCode;
    public string ClassCode => InventoryKey.ClassCode;
    public string ShippingMarkCode => InventoryKey.ShippingMarkCode;
    public string ShippingMarkName => InventoryKey.ShippingMarkName;
    
    // 取引先情報
    public string CustomerCode { get; set; } = string.Empty;    // 得意先コード
    public string CustomerName { get; set; } = string.Empty;    // 得意先名
    public string TransactionType { get; set; } = string.Empty; // 取引種別
    
    // 売上情報
    public decimal Quantity { get; set; }                       // 数量
    public decimal UnitPrice { get; set; }                      // 売上単価
    public decimal Amount { get; set; }                         // 売上金額
    public decimal InventoryUnitPrice { get; set; }             // 在庫単価
    
    // 粗利計算
    public decimal GrossProfit => CalculateGrossProfit();
    
    public string DataSetId { get; set; } = string.Empty;      // データセットID
    
    private decimal CalculateGrossProfit()
    {
        // 0除算対策
        if (Quantity == 0) return 0;
        
        // 第1段階：売上伝票1行ごと
        return (UnitPrice - InventoryUnitPrice) * Quantity;
    }
}