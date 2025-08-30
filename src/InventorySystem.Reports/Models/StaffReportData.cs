using System.Data;

namespace InventorySystem.Reports.Models;

/// <summary>
/// 担当者別レポートデータ
/// </summary>
public class StaffReportData
{
    /// <summary>
    /// ページ情報
    /// </summary>
    public StaffPageInfo PageInfo { get; set; } = new();

    /// <summary>
    /// フラットデータ
    /// </summary>
    public List<ProductAccountFlatRow> FlatData { get; set; } = new();

    /// <summary>
    /// DataTable
    /// </summary>
    public DataTable? DataTable { get; set; }

    /// <summary>
    /// 生成されたPDF（Phase 3で使用）
    /// </summary>
    public byte[]? PdfBytes { get; set; }

    /// <summary>
    /// デバッグ用文字列表現
    /// </summary>
    public override string ToString()
    {
        return $"StaffReportData: {PageInfo} フラットデータ: {FlatData.Count}行 DataTable: {(DataTable?.Rows.Count ?? 0)}行";
    }
}