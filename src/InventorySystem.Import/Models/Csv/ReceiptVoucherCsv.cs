using CsvHelper.Configuration.Attributes;

namespace InventorySystem.Import.Models.Csv;

/// <summary>
/// 入金伝票CSVモデル（販売大臣AX形式）
/// </summary>
public class ReceiptVoucherCsv
{
    /// <summary>
    /// 伝票日付
    /// </summary>
    [Index(0)]
    [Name("伝票日付")]
    public string VoucherDate { get; set; } = string.Empty;
    
    /// <summary>
    /// 伝票番号
    /// </summary>
    [Index(1)]
    [Name("伝票番号")]
    public string VoucherNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// 得意先コード
    /// </summary>
    [Index(2)]
    [Name("得意先コード")]
    public string CustomerCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 得意先名称
    /// </summary>
    [Index(3)]
    [Name("得意先名称")]
    public string? CustomerName { get; set; }
    
    /// <summary>
    /// 請求先コード
    /// </summary>
    [Index(6)]
    [Name("請求先コード")]
    public string? BillingCode { get; set; }
    
    /// <summary>
    /// ジョブ日付（汎用日付2）
    /// </summary>
    [Index(28)]
    [Name("ジョブデート")]
    public string JobDate { get; set; } = string.Empty;
    
    /// <summary>
    /// 行番号
    /// </summary>
    [Index(47)]
    [Name("行番号")]
    public int LineNumber { get; set; }
    
    /// <summary>
    /// 入金種別
    /// </summary>
    [Index(48)]
    [Name("入金種別")]
    public int PaymentType { get; set; }
    
    /// <summary>
    /// 相殺コード
    /// </summary>
    [Index(54)]
    [Name("相殺コード")]
    public string? OffsetCode { get; set; }
    
    /// <summary>
    /// 金額
    /// </summary>
    [Index(55)]
    [Name("金額")]
    public decimal Amount { get; set; }
    
    /// <summary>
    /// 手形期日
    /// </summary>
    [Index(56)]
    [Name("手形期日")]
    public string? BillDueDate { get; set; }
    
    /// <summary>
    /// 手形番号
    /// </summary>
    [Index(57)]
    [Name("手形番号")]
    public string? BillNumber { get; set; }
    
    /// <summary>
    /// 自社銀行コード
    /// </summary>
    [Index(61)]
    [Name("自社銀行コード")]
    public string? CorporateBankCode { get; set; }
    
    /// <summary>
    /// 預金口座番号
    /// </summary>
    [Index(62)]
    [Name("預金口座番号")]
    public int? DepositAccountNumber { get; set; }
    
    /// <summary>
    /// 振込人名義
    /// </summary>
    [Index(84)]
    [Name("振込人名義")]
    public string? RemitterName { get; set; }
    
    /// <summary>
    /// 摘要
    /// </summary>
    [Index(79)]
    [Name("摘要")]
    public string? Remarks { get; set; }
}