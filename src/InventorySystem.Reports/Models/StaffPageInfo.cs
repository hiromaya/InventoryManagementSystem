namespace InventorySystem.Reports.Models;

/// <summary>
/// 担当者別ページ情報管理クラス
/// </summary>
public class StaffPageInfo
{
    /// <summary>
    /// 担当者コード
    /// </summary>
    public string StaffCode { get; set; } = string.Empty;

    /// <summary>
    /// 担当者名
    /// </summary>
    public string StaffName { get; set; } = string.Empty;

    /// <summary>
    /// 実データ行数（小計含む）
    /// </summary>
    public int DataRowCount { get; set; }

    /// <summary>
    /// 必要ページ数
    /// </summary>
    public int RequiredPages { get; set; }

    /// <summary>
    /// 開始ページ番号
    /// </summary>
    public int StartPageNumber { get; set; }

    /// <summary>
    /// 終了ページ番号
    /// </summary>
    public int EndPageNumber { get; set; }

    /// <summary>
    /// 全体ページ数
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// デバッグ用文字列表現
    /// </summary>
    public override string ToString()
    {
        return $"担当者: {StaffCode}({StaffName}) データ行数: {DataRowCount} ページ数: {RequiredPages} 範囲: {StartPageNumber}-{EndPageNumber}/{TotalPages}";
    }
}