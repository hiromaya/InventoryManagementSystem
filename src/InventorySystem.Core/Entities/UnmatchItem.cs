namespace InventorySystem.Core.Entities;

public class UnmatchItem
{
    public string Category { get; set; } = string.Empty;         // 区分（掛売上、現金売上など）
    public string CustomerCode { get; set; } = string.Empty;     // 取引先コード
    public string CustomerName { get; set; } = string.Empty;     // 取引先名
    public InventoryKey Key { get; set; } = new();              // 在庫キー（商品コード、荷印コード等）
    public string ProductName { get; set; } = string.Empty;      // 商品名
    public string GradeName { get; set; } = string.Empty;        // 等級名
    public string ClassName { get; set; } = string.Empty;        // 階級名
    public decimal Quantity { get; set; }                        // 数量
    public decimal UnitPrice { get; set; }                       // 単価
    public decimal Amount { get; set; }                          // 金額
    public string VoucherNumber { get; set; } = string.Empty;    // 伝票番号
    public string AlertType { get; set; } = string.Empty;        // アラート種別（在庫0、該当無）
    
    // ソート用（商品分類1、商品コード、荷印コード、荷印名、等級コード、階級コード）
    public string ProductCategory1 { get; set; } = string.Empty;
    
    public static UnmatchItem FromSalesVoucher(SalesVoucher sales, string alertType, 
        string productName = "", string gradeName = "", string className = "", 
        string productCategory1 = "")
    {
        var category = sales.TransactionType switch
        {
            "掛売上" => "掛売上",
            "現金売上" => "現金売上",
            "掛売上返品" => "掛売上",
            _ => "売上"
        };
        
        return new UnmatchItem
        {
            Category = category,
            CustomerCode = sales.CustomerCode,
            CustomerName = sales.CustomerName,
            Key = new InventoryKey
            {
                ProductCode = sales.ProductCode,
                GradeCode = sales.GradeCode,
                ClassCode = sales.ClassCode,
                ShippingMarkCode = sales.ShippingMarkCode,
                ShippingMarkName = sales.ShippingMarkName
            },
            ProductName = productName,
            GradeName = gradeName,
            ClassName = className,
            Quantity = sales.Quantity,
            UnitPrice = sales.UnitPrice,
            Amount = sales.Amount,
            VoucherNumber = sales.VoucherNumber,
            AlertType = alertType,
            ProductCategory1 = productCategory1
        };
    }
    
    public static UnmatchItem FromPurchaseVoucher(PurchaseVoucher purchase, string alertType,
        string productName = "", string gradeName = "", string className = "", 
        string productCategory1 = "")
    {
        var category = purchase.TransactionType switch
        {
            "掛買" => "掛仕入",
            "現金買" => "現金仕入",
            "掛買返品" => "掛仕入",
            _ => "仕入"
        };
        
        return new UnmatchItem
        {
            Category = category,
            CustomerCode = purchase.SupplierCode,
            CustomerName = purchase.SupplierName,
            Key = new InventoryKey
            {
                ProductCode = purchase.ProductCode,
                GradeCode = purchase.GradeCode,
                ClassCode = purchase.ClassCode,
                ShippingMarkCode = purchase.ShippingMarkCode,
                ShippingMarkName = purchase.ShippingMarkName
            },
            ProductName = productName,
            GradeName = gradeName,
            ClassName = className,
            Quantity = purchase.Quantity,
            UnitPrice = purchase.UnitPrice,
            Amount = purchase.Amount,
            VoucherNumber = purchase.VoucherNumber,
            AlertType = alertType,
            ProductCategory1 = productCategory1
        };
    }
}