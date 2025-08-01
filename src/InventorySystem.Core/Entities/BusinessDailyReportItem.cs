namespace InventorySystem.Core.Entities
{
    public class BusinessDailyReportItem
    {
        public string ClassificationCode { get; set; }
        public string CustomerClassName { get; set; }
        public string SupplierClassName { get; set; }
        
        // 日計項目
        public decimal DailyCashSales { get; set; }
        public decimal DailyCashSalesTax { get; set; }
        public decimal DailyCreditSales { get; set; }
        public decimal DailySalesDiscount { get; set; }
        public decimal DailyCreditSalesTax { get; set; }
        public decimal DailyCashPurchase { get; set; }
        public decimal DailyCashPurchaseTax { get; set; }
        public decimal DailyCreditPurchase { get; set; }
        public decimal DailyPurchaseDiscount { get; set; }
        public decimal DailyCreditPurchaseTax { get; set; }
        public decimal DailyCashReceipt { get; set; }
        public decimal DailyBankReceipt { get; set; }
        public decimal DailyOtherReceipt { get; set; }
        public decimal DailyCashPayment { get; set; }
        public decimal DailyBankPayment { get; set; }
        public decimal DailyOtherPayment { get; set; }
        
        // 計算プロパティ
        public decimal DailySalesTotal => 
            DailyCashSales + DailyCashSalesTax + DailyCreditSales + 
            DailySalesDiscount + DailyCreditSalesTax;
            
        public decimal DailyPurchaseTotal => 
            DailyCashPurchase + DailyCashPurchaseTax + DailyCreditPurchase + 
            DailyPurchaseDiscount + DailyCreditPurchaseTax;
    }
    
    public class BusinessDailyReportResult
    {
        public bool Success { get; set; }
        public string DataSetId { get; set; }
        public string ErrorMessage { get; set; }
        public int ProcessedCount { get; set; }
        public string OutputPath { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}