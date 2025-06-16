namespace InventorySystem.Core.Entities;

public class PurchaseVoucher
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
    public string SupplierCode { get; set; } = string.Empty;    // 仕入先コード
    public string SupplierName { get; set; } = string.Empty;    // 仕入先名
    public string TransactionType { get; set; } = string.Empty; // 取引種別
    
    // 仕入情報
    public decimal Quantity { get; set; }                       // 数量
    public decimal UnitPrice { get; set; }                      // 仕入単価
    public decimal Amount { get; set; }                         // 仕入金額
    
    public string DataSetId { get; set; } = string.Empty;      // データセットID
}