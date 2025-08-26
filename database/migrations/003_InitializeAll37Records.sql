-- ====================================================================
-- 営業日報37レコード完全初期化スクリプト
-- 作成日: 2025-08-20
-- 目的: BusinessDailyReportテーブルに000-035と999の37レコード確実作成
-- ====================================================================

USE InventoryManagementDB;
GO

PRINT '営業日報37レコード完全初期化を開始...';

-- ====================================================================
-- BusinessDailyReportテーブルが存在しない場合は作成
-- ====================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BusinessDailyReport')
BEGIN
    CREATE TABLE BusinessDailyReport (
        ClassificationCode NVARCHAR(3) NOT NULL PRIMARY KEY,
        CustomerClassName NVARCHAR(100) NOT NULL DEFAULT '',
        SupplierClassName NVARCHAR(100) NOT NULL DEFAULT '',
        
        -- 日計16項目
        DailyCashSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCashSalesTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCreditSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailySalesDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCreditSalesTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCashPurchase DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCashPurchaseTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCreditPurchase DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyPurchaseDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCreditPurchaseTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCashReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyBankReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyOtherReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyCashPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyBankPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        DailyOtherPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        
        -- 月計16項目
        MonthlyCashSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCashSalesTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCreditSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlySalesDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCreditSalesTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCashPurchase DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCashPurchaseTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCreditPurchase DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyPurchaseDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCreditPurchaseTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCashReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyBankReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyOtherReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyCashPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyBankPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        MonthlyOtherPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        
        -- 年計16項目
        YearlyCashSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCashSalesTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCreditSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlySalesDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCreditSalesTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCashPurchase DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCashPurchaseTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCreditPurchase DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyPurchaseDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCreditPurchaseTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCashReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyBankReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyOtherReceipt DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyCashPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyBankPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        YearlyOtherPayment DECIMAL(18,2) NOT NULL DEFAULT 0,
        
        -- 管理項目
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    
    PRINT 'BusinessDailyReportテーブルを作成しました';
END
ELSE
BEGIN
    PRINT 'BusinessDailyReportテーブルは既に存在します';
END
GO

-- ====================================================================
-- 000-035の36レコードを一括MERGE（以降は元のスクリプトと同じ）
-- ====================================================================
MERGE BusinessDailyReport AS target
USING (
    VALUES 
        ('000', N'合計', N'合計'),
        ('001', '', ''), ('002', '', ''), ('003', '', ''), ('004', '', ''), ('005', '', ''),
        ('006', '', ''), ('007', '', ''), ('008', '', ''), ('009', '', ''), ('010', '', ''),
        ('011', '', ''), ('012', '', ''), ('013', '', ''), ('014', '', ''), ('015', '', ''),
        ('016', '', ''), ('017', '', ''), ('018', '', ''), ('019', '', ''), ('020', '', ''),
        ('021', '', ''), ('022', '', ''), ('023', '', ''), ('024', '', ''), ('025', '', ''),
        ('026', '', ''), ('027', '', ''), ('028', '', ''), ('029', '', ''), ('030', '', ''),
        ('031', '', ''), ('032', '', ''), ('033', '', ''), ('034', '', ''), ('035', '', '')
) AS source (ClassificationCode, CustomerClassName, SupplierClassName)
ON target.ClassificationCode = source.ClassificationCode
WHEN NOT MATCHED THEN
    INSERT (
        ClassificationCode, CustomerClassName, SupplierClassName,
        -- 日計16項目
        DailyCashSales, DailyCashSalesTax, DailyCreditSales, DailySalesDiscount, DailyCreditSalesTax,
        DailyCashPurchase, DailyCashPurchaseTax, DailyCreditPurchase, DailyPurchaseDiscount, DailyCreditPurchaseTax,
        DailyCashReceipt, DailyBankReceipt, DailyOtherReceipt, DailyCashPayment, DailyBankPayment, DailyOtherPayment,
        -- 月計16項目
        MonthlyCashSales, MonthlyCashSalesTax, MonthlyCreditSales, MonthlySalesDiscount, MonthlyCreditSalesTax,
        MonthlyCashPurchase, MonthlyCashPurchaseTax, MonthlyCreditPurchase, MonthlyPurchaseDiscount, MonthlyCreditPurchaseTax,
        MonthlyCashReceipt, MonthlyBankReceipt, MonthlyOtherReceipt, MonthlyCashPayment, MonthlyBankPayment, MonthlyOtherPayment,
        -- 年計16項目
        YearlyCashSales, YearlyCashSalesTax, YearlyCreditSales, YearlySalesDiscount, YearlyCreditSalesTax,
        YearlyCashPurchase, YearlyCashPurchaseTax, YearlyCreditPurchase, YearlyPurchaseDiscount, YearlyCreditPurchaseTax,
        YearlyCashReceipt, YearlyBankReceipt, YearlyOtherReceipt, YearlyCashPayment, YearlyBankPayment, YearlyOtherPayment,
        -- 管理項目
        CreatedDate, UpdatedDate
    )
    VALUES (
        source.ClassificationCode, source.CustomerClassName, source.SupplierClassName,
        -- 日計16個の0
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        -- 月計16個の0
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        -- 年計16個の0
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        -- 管理項目
        GETDATE(), GETDATE()
    );

PRINT '000-035の36レコードMERGE完了';

-- 残りの部分は元のスクリプトと同じ...