using System.Collections.Generic;

namespace InventorySystem.Core.Models
{
    public class CpInventoryValidationResult
    {
        public DateTime JobDate { get; set; }
        public DateTime ValidatedAt { get; set; }
        public int TotalRecords { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new();
    }

    public class ValidationIssue
    {
        public string IssueType { get; set; } = string.Empty;  // "単価同値", "在庫金額不整合", "マイナス在庫" 等
        public string Severity { get; set; } = string.Empty;    // "Error", "Warning", "Info"
        public string ProductCode { get; set; } = string.Empty;
        public string ShippingMarkCode { get; set; } = string.Empty;
        public string ManualShippingMark { get; set; } = string.Empty;
        public string GradeCode { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public decimal? ExpectedValue { get; set; }
        public decimal? ActualValue { get; set; }
        public decimal? Difference { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CorrectionApplied { get; set; } = string.Empty;
    }
}

