namespace InventorySystem.Core.Entities;

/// <summary>
/// CSVインポートエラー記録エンティティ
/// </summary>
public class ImportError
{
    /// <summary>
    /// エラーID（自動生成）
    /// </summary>
    public long ErrorId { get; set; }
    
    /// <summary>
    /// インポート日時
    /// </summary>
    public DateTime ImportTimestamp { get; set; } = DateTime.Now;
    
    /// <summary>
    /// ファイル名
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// テーブル名
    /// </summary>
    public string? TableName { get; set; }
    
    /// <summary>
    /// 行番号
    /// </summary>
    public int RowNumber { get; set; }
    
    /// <summary>
    /// 列名
    /// </summary>
    public string? ColumnName { get; set; }
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// CSVの元データ（エラー行の原文）
    /// </summary>
    public string? CsvRowData { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}