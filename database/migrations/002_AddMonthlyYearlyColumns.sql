-- ====================================================================
-- 営業日報テーブル月計・年計カラム追加マイグレーション
-- 作成日: 2025-08-20
-- 目的: BusinessDailyReportテーブルに月計16項目・年計16項目を追加
-- ====================================================================

USE InventoryDB;
GO

-- ====================================================================
-- 1. 月計カラム追加（16項目）
-- ====================================================================
PRINT '営業日報テーブルに月計カラムを追加開始...';

-- 売上関連（5項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashSales')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashSales DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashSales カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashSalesTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashSalesTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashSalesTax カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCreditSales')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCreditSales DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCreditSales カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlySalesDiscount')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlySalesDiscount DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlySalesDiscount カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCreditSalesTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCreditSalesTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCreditSalesTax カラムを追加しました';
END

-- 仕入関連（5項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashPurchase')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashPurchase DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashPurchase カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashPurchaseTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashPurchaseTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashPurchaseTax カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCreditPurchase')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCreditPurchase DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCreditPurchase カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyPurchaseDiscount')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyPurchaseDiscount DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyPurchaseDiscount カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCreditPurchaseTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCreditPurchaseTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCreditPurchaseTax カラムを追加しました';
END

-- 入金関連（3項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashReceipt カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyBankReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyBankReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyBankReceipt カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyOtherReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyOtherReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyOtherReceipt カラムを追加しました';
END

-- 支払関連（3項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashPayment カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyBankPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyBankPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyBankPayment カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyOtherPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyOtherPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyOtherPayment カラムを追加しました';
END

PRINT '月計カラム追加完了（16項目）';

-- ====================================================================
-- 2. 年計カラム追加（16項目）
-- ====================================================================
PRINT '営業日報テーブルに年計カラムを追加開始...';

-- 売上関連（5項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashSales')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashSales DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashSales カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashSalesTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashSalesTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashSalesTax カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCreditSales')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCreditSales DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCreditSales カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlySalesDiscount')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlySalesDiscount DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlySalesDiscount カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCreditSalesTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCreditSalesTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCreditSalesTax カラムを追加しました';
END

-- 仕入関連（5項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashPurchase')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashPurchase DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashPurchase カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashPurchaseTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashPurchaseTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashPurchaseTax カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCreditPurchase')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCreditPurchase DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCreditPurchase カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyPurchaseDiscount')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyPurchaseDiscount DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyPurchaseDiscount カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCreditPurchaseTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCreditPurchaseTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCreditPurchaseTax カラムを追加しました';
END

-- 入金関連（3項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashReceipt カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyBankReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyBankReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyBankReceipt カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyOtherReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyOtherReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyOtherReceipt カラムを追加しました';
END

-- 支払関連（3項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashPayment カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyBankPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyBankPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyBankPayment カラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyOtherPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyOtherPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyOtherPayment カラムを追加しました';
END

PRINT '年計カラム追加完了（16項目）';

-- ====================================================================
-- 3. 999レコード（未分類）の追加確認
-- ====================================================================
PRINT '未分類レコード（999）の存在確認...';

IF NOT EXISTS (SELECT 1 FROM BusinessDailyReport WHERE ClassificationCode = '999')
BEGIN
    INSERT INTO BusinessDailyReport (ClassificationCode, CustomerClassName, SupplierClassName)
    VALUES ('999', '未分類', '未分類');
    PRINT '未分類レコード（999）を追加しました';
END
ELSE
BEGIN
    PRINT '未分類レコード（999）は既に存在します';
END

-- ====================================================================
-- 4. 最終確認とレポート
-- ====================================================================
DECLARE @RecordCount INT;
DECLARE @ColumnCount INT;

SELECT @RecordCount = COUNT(*) FROM BusinessDailyReport;
SELECT @ColumnCount = COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport';

PRINT '';
PRINT '====================================================================';
PRINT '営業日報テーブル月計・年計カラム追加完了';
PRINT '====================================================================';
PRINT '総レコード数: ' + CAST(@RecordCount AS NVARCHAR(10));
PRINT '総カラム数: ' + CAST(@ColumnCount AS NVARCHAR(10));
PRINT '追加カラム数: 月計16項目 + 年計16項目 = 32項目';
PRINT '====================================================================';

-- デバッグ用：カラム一覧表示
PRINT '';
PRINT 'カラム一覧（月計・年計のみ表示）:';
SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'BusinessDailyReport' 
  AND (COLUMN_NAME LIKE 'Monthly%' OR COLUMN_NAME LIKE 'Yearly%')
ORDER BY COLUMN_NAME;

PRINT '';
PRINT '営業日報テーブル月計・年計カラム追加マイグレーション完了';