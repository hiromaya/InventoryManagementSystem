namespace InventorySystem.Core.Entities;

/// <summary>
/// データセット管理エンティティ
/// </summary>
public class DataSetManagement
{
    /// <summary>
    /// データセットID
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// ジョブ日付
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// データセット名
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 説明
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// ファイルパス
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// ステータス
    /// </summary>
    public string Status { get; set; } = "Pending";
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 処理種別
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;
    
    /// <summary>
    /// インポートタイプ（INIT/IMPORT/CARRYOVER/MANUAL/UNKNOWN）
    /// </summary>
    public string ImportType { get; set; } = "UNKNOWN";
    
    /// <summary>
    /// レコード数
    /// </summary>
    public int RecordCount { get; set; }
    
    /// <summary>
    /// 総レコード数（RecordCountと同じ値を設定）
    /// </summary>
    public int TotalRecordCount { get; set; }
    
    /// <summary>
    /// アクティブフラグ
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// アーカイブフラグ
    /// </summary>
    public bool IsArchived { get; set; } = false;
    
    /// <summary>
    /// 親データセットID
    /// </summary>
    public string? ParentDataSetId { get; set; }
    
    /// <summary>
    /// インポートファイル一覧（JSON形式）
    /// </summary>
    public string? ImportedFiles { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// 作成者
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// 無効化日時
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }
    
    /// <summary>
    /// 無効化実行者
    /// </summary>
    public string? DeactivatedBy { get; set; }
    
    /// <summary>
    /// アーカイブ日時
    /// </summary>
    public DateTime? ArchivedAt { get; set; }
    
    /// <summary>
    /// アーカイブ実行者
    /// </summary>
    public string? ArchivedBy { get; set; }
    
    /// <summary>
    /// 備考
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// 部門コード
    /// </summary>
    public string Department { get; set; } = "DeptA";
    
    /// <summary>
    /// 処理履歴（ナビゲーションプロパティ）
    /// </summary>
    public ICollection<ProcessHistory> ProcessHistories { get; set; } = new List<ProcessHistory>();
    
    /// <summary>
    /// 一意なDataSetIdを生成する
    /// </summary>
    public static string GenerateDataSetId(string importType)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var random = GenerateRandomString(6);
        return $"{importType}_{timestamp}_{random}";
    }
    
    /// <summary>
    /// ランダム文字列を生成する
    /// </summary>
    private static string GenerateRandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}