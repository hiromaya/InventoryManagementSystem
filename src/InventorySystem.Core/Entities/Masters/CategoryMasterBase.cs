namespace InventorySystem.Core.Entities.Masters;

/// <summary>
/// 分類マスタの共通基底クラス
/// </summary>
public abstract class CategoryMasterBase
{
    /// <summary>
    /// 分類コード（3桁0埋め文字列）
    /// </summary>
    public string CategoryCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 分類名称
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;
    
    /// <summary>
    /// 検索カナ
    /// </summary>
    public string? SearchKana { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

// ========== 商品分類マスタ（1-3） ==========

/// <summary>
/// 商品分類1マスタ
/// </summary>
public class ProductCategory1Master : CategoryMasterBase
{
}

/// <summary>
/// 商品分類2マスタ
/// </summary>
public class ProductCategory2Master : CategoryMasterBase
{
}

/// <summary>
/// 商品分類3マスタ
/// </summary>
public class ProductCategory3Master : CategoryMasterBase
{
}

// ========== 得意先分類マスタ（1-5） ==========

/// <summary>
/// 得意先分類1マスタ
/// </summary>
public class CustomerCategory1Master : CategoryMasterBase
{
}

/// <summary>
/// 得意先分類2マスタ
/// </summary>
public class CustomerCategory2Master : CategoryMasterBase
{
}

/// <summary>
/// 得意先分類3マスタ
/// </summary>
public class CustomerCategory3Master : CategoryMasterBase
{
}

/// <summary>
/// 得意先分類4マスタ
/// </summary>
public class CustomerCategory4Master : CategoryMasterBase
{
}

/// <summary>
/// 得意先分類5マスタ
/// </summary>
public class CustomerCategory5Master : CategoryMasterBase
{
}

// ========== 仕入先分類マスタ（1-3） ==========

/// <summary>
/// 仕入先分類1マスタ
/// </summary>
public class SupplierCategory1Master : CategoryMasterBase
{
}

/// <summary>
/// 仕入先分類2マスタ
/// </summary>
public class SupplierCategory2Master : CategoryMasterBase
{
}

/// <summary>
/// 仕入先分類3マスタ
/// </summary>
public class SupplierCategory3Master : CategoryMasterBase
{
}

// ========== 担当者分類マスタ ==========

/// <summary>
/// 担当者分類1マスタ
/// </summary>
public class StaffCategory1Master : CategoryMasterBase
{
}