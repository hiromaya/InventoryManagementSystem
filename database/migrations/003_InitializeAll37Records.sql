-- ====================================================================
-- 営業日報37レコード完全初期化スクリプト
-- 作成日: 2025-08-20
-- 目的: BusinessDailyReportテーブルに000-035と999の37レコード確実作成
-- ====================================================================

USE InventoryManagementDB;
GO

PRINT '営業日報37レコード完全初期化を開始...';

-- ====================================================================
-- 000-035の36レコードを一括MERGE
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

-- ====================================================================
-- 999（未分類）レコード追加確認
-- ====================================================================
IF NOT EXISTS (SELECT 1 FROM BusinessDailyReport WHERE ClassificationCode = '999')
BEGIN
    INSERT INTO BusinessDailyReport (
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
        '999', N'未分類', N'未分類',
        -- 日計16個の0
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        -- 月計16個の0
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        -- 年計16個の0
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        -- 管理項目
        GETDATE(), GETDATE()
    );
    PRINT '999（未分類）レコードを追加しました';
END
ELSE
    PRINT '999（未分類）レコードは既に存在します';

-- ====================================================================
-- 最終確認とレポート
-- ====================================================================
DECLARE @RecordCount INT;
SELECT @RecordCount = COUNT(*) FROM BusinessDailyReport;

PRINT '';
PRINT '====================================================================';
PRINT '営業日報37レコード完全初期化完了';
PRINT '====================================================================';
PRINT '総レコード数: ' + CAST(@RecordCount AS NVARCHAR(10));
PRINT '期待レコード数: 37（000-035の36件 + 999の1件）';

IF @RecordCount = 37
    PRINT '✅ 37レコード初期化成功';
ELSE
    PRINT '❌ レコード数不足: ' + CAST(@RecordCount AS NVARCHAR(10)) + '/37件';

-- レコード一覧表示
PRINT '';
PRINT '分類コード一覧:';
SELECT ClassificationCode, CustomerClassName, SupplierClassName
FROM BusinessDailyReport
ORDER BY ClassificationCode;

PRINT '====================================================================';
PRINT '営業日報37レコード完全初期化スクリプト完了';