namespace InventorySystem.Core.Constants;

/// <summary>
/// 伝票種別定数
/// </summary>
public static class SalesVoucherTypes
{
    /// <summary>
    /// 51:掛売上
    /// </summary>
    public const string Credit = "51";
    
    /// <summary>
    /// 52:現金売上
    /// </summary>
    public const string Cash = "52";
}

/// <summary>
/// 仕入伝票種別定数
/// </summary>
public static class PurchaseVoucherTypes
{
    /// <summary>
    /// 11:掛仕入
    /// </summary>
    public const string Credit = "11";
    
    /// <summary>
    /// 12:現金仕入
    /// </summary>
    public const string Cash = "12";
}

/// <summary>
/// 在庫調整伝票種別定数
/// </summary>
public static class InventoryAdjustmentTypes
{
    /// <summary>
    /// 71:受注
    /// </summary>
    public const string Order = "71";
    
    /// <summary>
    /// 72:注文
    /// </summary>
    public const string Purchase = "72";
}

/// <summary>
/// 明細種別定数
/// </summary>
public static class DetailTypes
{
    /// <summary>
    /// 1:商品
    /// </summary>
    public const string Product = "1";
    
    /// <summary>
    /// 2:返品
    /// </summary>
    public const string Return = "2";
    
    /// <summary>
    /// 3:値引
    /// </summary>
    public const string Discount = "3";
    
    /// <summary>
    /// 4:その他
    /// </summary>
    public const string Other = "4";
    
    /// <summary>
    /// 18:諸経費
    /// </summary>
    public const string Expense = "18";
}