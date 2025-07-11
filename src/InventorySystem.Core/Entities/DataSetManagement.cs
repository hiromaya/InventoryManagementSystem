namespace InventorySystem.Core.Entities;

/// <summary>
/// データセット管理エンティティ
/// データセットの世代管理と親子関係を管理
/// </summary>
public class DataSetManagement
{
    /// <summary>
    /// データセットID（主キー）
    /// 形式: {ImportType}_{YYYYMMDD}_{HHmmss}_{RandomString}
    /// 例: INIT_20250531_100000_aXbY7z
    /// </summary>
    public string DataSetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 対象日付（JobDate）
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// インポートタイプ
    /// INIT: 前月末在庫の初期登録
    /// IMPORT: 通常のCSVインポート
    /// CARRYOVER: 前日在庫を引き継いだインポート
    /// MANUAL: 手動登録
    /// UNKNOWN: 不明（レガシーデータ等）
    /// </summary>
    public string ImportType { get; set; } = "UNKNOWN";
    
    /// <summary>
    /// レコード数
    /// </summary>
    public int RecordCount { get; set; }
    
    /// <summary>
    /// アクティブフラグ（同一JobDateで最新のものがtrue）
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// アーカイブフラグ（論理アーカイブ用）
    /// </summary>
    public bool IsArchived { get; set; } = false;
    
    /// <summary>
    /// 親データセットID（引き継ぎ元）
    /// </summary>
    public string? ParentDataSetId { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 作成者
    /// </summary>
    public string? CreatedBy { get; set; }
    
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