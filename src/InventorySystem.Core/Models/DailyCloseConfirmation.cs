using System.Collections.Generic;

namespace InventorySystem.Core.Models;

/// <summary>
/// 日次終了処理確認情報
/// </summary>
public class DailyCloseConfirmation
{
    /// <summary>
    /// 対象日付
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// 現在時刻
    /// </summary>
    public DateTime CurrentTime { get; set; }
    
    /// <summary>
    /// 商品日報情報
    /// </summary>
    public DailyReportInfo? DailyReport { get; set; }
    
    /// <summary>
    /// 最新CSV取込情報
    /// </summary>
    public CsvImportInfo? LatestCsvImport { get; set; }
    
    /// <summary>
    /// データ件数サマリー
    /// </summary>
    public DataCountSummary DataCounts { get; set; } = new();
    
    /// <summary>
    /// 金額サマリー
    /// </summary>
    public AmountSummary Amounts { get; set; } = new();
    
    /// <summary>
    /// 検証結果
    /// </summary>
    public List<ValidationMessage> ValidationResults { get; set; } = new();
    
    /// <summary>
    /// 処理可能フラグ
    /// </summary>
    public bool CanProcess { get; set; }
    
    /// <summary>
    /// データハッシュ値
    /// </summary>
    public string? CurrentDataHash { get; set; }
}

/// <summary>
/// 商品日報情報
/// </summary>
public class DailyReportInfo
{
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string DataSetId { get; set; } = string.Empty;
    public string? DataHash { get; set; }
}

/// <summary>
/// CSV取込情報
/// </summary>
public class CsvImportInfo
{
    public DateTime ImportedAt { get; set; }
    public string ImportedBy { get; set; } = string.Empty;
    public string FileNames { get; set; } = string.Empty;
}

/// <summary>
/// データ件数サマリー
/// </summary>
public class DataCountSummary
{
    public int SalesCount { get; set; }
    public int PurchaseCount { get; set; }
    public int AdjustmentCount { get; set; }
    public int CpInventoryCount { get; set; }
}

/// <summary>
/// 金額サマリー
/// </summary>
public class AmountSummary
{
    public decimal SalesAmount { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal EstimatedGrossProfit { get; set; }
}

/// <summary>
/// 検証メッセージ
/// </summary>
public class ValidationMessage
{
    public ValidationLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

/// <summary>
/// 検証レベル
/// </summary>
public enum ValidationLevel
{
    Error,
    Warning,
    Info
}

/// <summary>
/// データ検証結果
/// </summary>
public class DataValidationResult
{
    public bool IsValid { get; set; }
    public string? CurrentHash { get; set; }
    public string? ExpectedHash { get; set; }
    public List<string> Changes { get; set; } = new();
    public Dictionary<string, int> DataCounts { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}