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
    /// 説明
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 処理種類
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;

    /// <summary>
    /// データセット種別 ('Sales', 'Purchase', 'Adjustment') - 互換性のため残す
    /// </summary>
    [Obsolete("ProcessTypeを使用してください")]
    public string DataSetType
    {
        get => ProcessType;
        set => ProcessType = value;
    }

    /// <summary>
    /// 取込日時 - 互換性のため残す
    /// </summary>
    [Obsolete("CreatedDateを使用してください")]
    public DateTime ImportedAt
    {
        get => CreatedDate;
        set => CreatedDate = value;
    }

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
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 完了日時
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// 作成日 - 互換性のため残す
    /// </summary>
    [Obsolete("CreatedAtを使用してください")]
    public DateTime CreatedDate
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    /// <summary>
    /// 更新日 - 互換性のため残す
    /// </summary>
    [Obsolete("UpdatedAtを使用してください")]
    public DateTime UpdatedDate
    {
        get => UpdatedAt;
        set => UpdatedAt = value;
    }

    /// <summary>
    /// 部門コード
    /// </summary>
    public string DepartmentCode { get; set; } = "DeptA";
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