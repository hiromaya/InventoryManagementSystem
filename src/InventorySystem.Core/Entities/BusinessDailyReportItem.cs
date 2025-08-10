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
        
        // 月計項目
        public decimal MonthlyCashSales { get; set; }
        public decimal MonthlyCashSalesTax { get; set; }
        public decimal MonthlyCreditSales { get; set; }
        public decimal MonthlySalesDiscount { get; set; }
        public decimal MonthlyCreditSalesTax { get; set; }
        public decimal MonthlyCashPurchase { get; set; }
        public decimal MonthlyCashPurchaseTax { get; set; }
        public decimal MonthlyCreditPurchase { get; set; }
        public decimal MonthlyPurchaseDiscount { get; set; }
        public decimal MonthlyCreditPurchaseTax { get; set; }
        public decimal MonthlyCashReceipt { get; set; }
        public decimal MonthlyBankReceipt { get; set; }
        public decimal MonthlyOtherReceipt { get; set; }
        public decimal MonthlyCashPayment { get; set; }
        public decimal MonthlyBankPayment { get; set; }
        public decimal MonthlyOtherPayment { get; set; }
        
        // 年計項目（4項目のみ）
        public decimal YearlyCashSales { get; set; }
        public decimal YearlyCashSalesTax { get; set; }
        public decimal YearlyCashPurchase { get; set; }
        public decimal YearlyCashPurchaseTax { get; set; }
        
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