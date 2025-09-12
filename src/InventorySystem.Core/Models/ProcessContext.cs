using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Models;

/// <summary>
/// 処理コンテキスト
/// </summary>
public class ProcessContext
{
    /// <summary>
    /// ジョブ日付
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 処理種別
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;
    
    /// <summary>
    /// 処理履歴
    /// </summary>
    public ProcessHistory? ProcessHistory { get; set; }
    
    /// <summary>
    /// インポートファイル一覧
    /// </summary>
    public List<string> ImportedFiles { get; set; } = new();
    
    /// <summary>
    /// 実行者
    /// </summary>
    public string ExecutedBy { get; set; } = "System";
}