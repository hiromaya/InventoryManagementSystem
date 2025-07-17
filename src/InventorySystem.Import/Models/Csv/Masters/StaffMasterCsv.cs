using CsvHelper.Configuration.Attributes;

namespace InventorySystem.Import.Models.Csv.Masters;

/// <summary>
/// 担当者マスタCSVモデル
/// </summary>
public class StaffMasterCsv
{
    /// <summary>
    /// 担当者コード
    /// </summary>
    [Index(0)]
    [Name("コード")]
    public int Code { get; set; }
    
    /// <summary>
    /// 担当者名称
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
    
    /// <summary>
    /// 分類1コード
    /// </summary>
    [Index(3)]
    [Name("分類１コード")]
    public int? Category1Code { get; set; }
    
    /// <summary>
    /// 分類2コード
    /// </summary>
    [Index(4)]
    [Name("分類２コード")]
    public int? Category2Code { get; set; }
    
    /// <summary>
    /// 分類3コード
    /// </summary>
    [Index(5)]
    [Name("分類３コード")]
    public int? Category3Code { get; set; }
    
    /// <summary>
    /// 部門コード
    /// </summary>
    [Index(6)]
    [Name("部門コード")]
    public int? DepartmentCode { get; set; }
}