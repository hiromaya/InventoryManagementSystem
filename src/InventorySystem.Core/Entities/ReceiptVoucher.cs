namespace InventorySystem.Core.Entities;

/// <summary>
/// 入金伝票エンティティ
/// </summary>
public class ReceiptVoucher
{
    /// <summary>
    /// ID（自動生成）
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// 伝票日付
    /// </summary>
    public DateTime VoucherDate { get; set; }
    
    /// <summary>
    /// 伝票番号
    /// </summary>
    public string VoucherNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// 得意先コード
    /// </summary>
    public string CustomerCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 得意先名称
    /// </summary>
    public string? CustomerName { get; set; }
    
    /// <summary>
    /// 請求先コード
    /// </summary>
    public string? BillingCode { get; set; }
    
    /// <summary>
    /// ジョブ日付（処理基準日）
    /// </summary>
    public DateTime JobDate { get; set; }
    
    /// <summary>
    /// 行番号
    /// </summary>
    public int LineNumber { get; set; }
    
    /// <summary>
    /// 入金種別
    /// </summary>
    public int PaymentType { get; set; }
    
    /// <summary>
    /// 相殺コード
    /// </summary>
    public string? OffsetCode { get; set; }
    
    /// <summary>
    /// 金額
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// 手形期日
    /// </summary>
    public DateTime? BillDueDate { get; set; }
    
    /// <summary>
    /// 手形番号
    /// </summary>
    public string? BillNumber { get; set; }
    
    /// <summary>
    /// 自社銀行コード
    /// </summary>
    public string? CorporateBankCode { get; set; }
    
    /// <summary>
    /// 預金口座番号
    /// </summary>
    public int? DepositAccountNumber { get; set; }
    
    /// <summary>
    /// 振込人名義
    /// </summary>
    public string? RemitterName { get; set; }
    
    /// <summary>
    /// 摘要
    /// </summary>
    public string? Remarks { get; set; }
    
    /// <summary>
    /// データセットID
    /// </summary>
    public string? DataSetId { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}