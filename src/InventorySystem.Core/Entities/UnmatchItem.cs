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
    
    // 区分コード（在庫調整用）
    public int? CategoryCode { get; set; }
    
    public static UnmatchItem FromSalesVoucher(SalesVoucher sales, string alertType, 
        string productName = "", string gradeName = "", string className = "", 
        string productCategory1 = "")
    {
        var category = GetTransactionTypeName(sales.VoucherType);
        
        return new UnmatchItem
        {
            Category = category,
            CustomerCode = sales.CustomerCode ?? string.Empty,
            CustomerName = sales.CustomerName ?? string.Empty,
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
        var category = GetTransactionTypeName(purchase.VoucherType);
        
        return new UnmatchItem
        {
            Category = category,
            CustomerCode = purchase.SupplierCode ?? string.Empty,
            CustomerName = purchase.SupplierName ?? string.Empty,
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
    
    /// <summary>
    /// 在庫調整（受注伝票）からUnmatchItemを作成
    /// </summary>
    public static UnmatchItem FromInventoryAdjustment(string voucherType, int categoryCode,
        string customerCode, string customerName, InventoryKey inventoryKey,
        decimal quantity, decimal unitPrice, decimal amount, string voucherNumber,
        string alertType, string productName = "", string gradeName = "", 
        string className = "", string productCategory1 = "")
    {
        var category = GetTransactionTypeName(voucherType, categoryCode);
        
        return new UnmatchItem
        {
            Category = category,
            CustomerCode = customerCode,
            CustomerName = customerName,
            Key = inventoryKey,
            ProductName = productName,
            GradeName = gradeName,
            ClassName = className,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = amount,
            VoucherNumber = voucherNumber,
            AlertType = alertType,
            ProductCategory1 = productCategory1,
            CategoryCode = categoryCode
        };
    }
    
    /// <summary>
    /// 伝票種別と区分コードから取引種別名を取得
    /// </summary>
    public static string GetTransactionTypeName(string slipType, int? categoryCode = null)
    {
        // 売上伝票
        if (slipType == "51") return "掛売上";
        if (slipType == "52") return "現金売上";
        
        // 仕入伝票  
        if (slipType == "11") return "掛仕入";
        if (slipType == "12") return "現金仕入";
        
        // 在庫調整（受注伝票）
        if (slipType == "71" || slipType == "72")
        {
            return categoryCode switch
            {
                1 => "在庫調整",  // ロス
                2 => "加工費",    // 経費（将来用）
                3 => "在庫調整",  // 腐り（将来用）
                4 => "振替",      // 振替
                5 => "加工費",    // 加工（将来用）
                6 => "在庫調整",  // 調整
                _ => "在庫調整"
            };
        }
        
        return "";
    }
}