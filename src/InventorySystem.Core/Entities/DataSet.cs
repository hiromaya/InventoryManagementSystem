namespace InventorySystem.Core.Entities;

/// <summary>
/// データセット管理エンティティ
/// CSV取込の単位管理とステータス管理を行う
/// </summary>
public class DataSet
{
    /// <summary>
    /// データセットID（GUID形式）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// データセット名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// データセット説明
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// データセット種別 ('Sales', 'Purchase', 'Adjustment')
    /// </summary>
    public string DataSetType { get; set; } = string.Empty;

    /// <summary>
    /// 処理種別 ('SALES', 'PURCHASE', 'ADJUSTMENT')
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;

    /// <summary>
    /// 取込日時
    /// </summary>
    public DateTime ImportedAt { get; set; }

    /// <summary>
    /// 取込件数
    /// </summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// ステータス ('Imported', 'Processing', 'Completed', 'Error')
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 元ファイルパス
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// ジョブ日付（汎用日付2）
    /// </summary>
    public DateTime JobDate { get; set; }

    /// <summary>
    /// 完了日時（データベースのCompletedDateカラム）
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// 作成日時（データベースのCreatedDateカラム）
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// 更新日時（データベースのUpdatedDateカラム）
    /// </summary>
    public DateTime UpdatedDate { get; set; }

    /// <summary>
    /// 作成日時（後方互換性のためのエイリアス）
    /// </summary>
    public DateTime CreatedAt 
    { 
        get => CreatedDate; 
        set => CreatedDate = value; 
    }

    /// <summary>
    /// 更新日時（後方互換性のためのエイリアス）
    /// </summary>
    public DateTime UpdatedAt 
    { 
        get => UpdatedDate; 
        set => UpdatedDate = value; 
    }
}

/// <summary>
/// データセット種別の定数
/// </summary>
public static class DataSetTypes
{
    public const string Sales = "Sales";
    public const string Purchase = "Purchase";
    public const string Adjustment = "Adjustment";
    public const string ProductMaster = "ProductMaster";
    public const string CustomerMaster = "CustomerMaster";
    public const string SupplierMaster = "SupplierMaster";
}

/// <summary>
/// データセットステータスの定数
/// </summary>
public static class DataSetStatus
{
    public const string Imported = "Imported";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string PartialSuccess = "PartialSuccess";
    public const string Failed = "Failed";
    public const string Error = "Error";
}