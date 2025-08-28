using CsvHelper.Configuration.Attributes;

namespace InventorySystem.Import.Models.Csv.Masters;

/// <summary>
/// 分類マスタCSVの共通基底クラス
/// </summary>
public class CategoryMasterCsv
{
    /// <summary>
    /// コード（3桁0埋め文字列）
    /// </summary>
    [Index(0)]
    [Name("コード")]
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// 名称
    /// </summary>
    [Index(1)]
    [Name("名称")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 検索カナ
    /// </summary>
    [Index(2)]
    [Name("検索カナ")]
    public string? SearchKana { get; set; }
}

// ========== 各分類マスタCSVクラス ==========

/// <summary>
/// 単位マスタCSV
/// </summary>
public class UnitMasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 商品分類1マスタCSV
/// </summary>
public class ProductCategory1MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 商品分類2マスタCSV
/// </summary>
public class ProductCategory2MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 商品分類3マスタCSV
/// </summary>
public class ProductCategory3MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 得意先分類1マスタCSV
/// </summary>
public class CustomerCategory1MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 得意先分類2マスタCSV
/// </summary>
public class CustomerCategory2MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 得意先分類3マスタCSV
/// </summary>
public class CustomerCategory3MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 得意先分類4マスタCSV
/// </summary>
public class CustomerCategory4MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 得意先分類5マスタCSV
/// </summary>
public class CustomerCategory5MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 仕入先分類1マスタCSV
/// </summary>
public class SupplierCategory1MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 仕入先分類2マスタCSV
/// </summary>
public class SupplierCategory2MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 仕入先分類3マスタCSV
/// </summary>
public class SupplierCategory3MasterCsv : CategoryMasterCsv
{
}

/// <summary>
/// 担当者分類1マスタCSV
/// </summary>
public class StaffCategory1MasterCsv : CategoryMasterCsv
{
}