namespace InventorySystem.Core.Entities;

public class ProcessingHistory
{
    public int Id { get; set; }                                 // ID
    public string DataSetId { get; set; } = string.Empty;      // データセットID
    public string ProcessType { get; set; } = string.Empty;    // 処理タイプ
    public DateTime JobDate { get; set; }                       // 汎用日付2（ジョブデート）
    public DateTime ProcessedAt { get; set; }                   // 処理日時
    public string ProcessedBy { get; set; } = string.Empty;    // 処理者
    public string Status { get; set; } = string.Empty;         // ステータス
    public string? ErrorMessage { get; set; }                  // エラーメッセージ
    public int ProcessedRecords { get; set; }                   // 処理件数
    public string? Note { get; set; }                          // 備考
}