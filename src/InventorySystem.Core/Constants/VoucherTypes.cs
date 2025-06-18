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
    /// 61:掛仕入
    /// </summary>
    public const string Credit = "61";
    
    /// <summary>
    /// 62:現金仕入
    /// </summary>
    public const string Cash = "62";
}

/// <summary>
/// 在庫調整伝票種別定数
/// </summary>
public static class InventoryAdjustmentTypes
{
    /// <summary>
    /// 70:在庫調整
    /// </summary>
    public const string Adjustment = "70";
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