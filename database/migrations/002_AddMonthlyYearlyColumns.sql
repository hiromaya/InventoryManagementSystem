-- ====================================================================
-- 営業日報テーブル月計・年計カラム追加マイグレーション
-- 作成日: 2025-08-20
-- 修正日: 2025-08-25
-- 目的: BusinessDailyReportテーブルに月計16項目・年計16項目を追加
-- ====================================================================

PRINT '現在のデータベース: ' + DB_NAME();
PRINT '';

-- ====================================================================
-- 0. BusinessDailyReportテーブルの存在確認と作成
-- ====================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BusinessDailyReport')
BEGIN
    CREATE TABLE BusinessDailyReport (
        ClassificationCode NVARCHAR(3) NOT NULL PRIMARY KEY,
        CustomerClassName NVARCHAR(100) NOT NULL DEFAULT '',
        SupplierClassName NVARCHAR(100) NOT NULL DEFAULT '',
        
        -- 日計16項目（基本）
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
        
        -- 管理項目
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    
    PRINT 'BusinessDailyReportテーブルを作成しました（日計項目のみ）';
    PRINT '月計・年計カラムを追加します...';
END
ELSE
BEGIN
    PRINT 'BusinessDailyReportテーブルは既に存在します';
END
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
ELSE
BEGIN
    PRINT 'MonthlyCashSales カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashSalesTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashSalesTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashSalesTax カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCashSalesTax カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCreditSales')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCreditSales DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCreditSales カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCreditSales カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlySalesDiscount')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlySalesDiscount DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlySalesDiscount カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlySalesDiscount カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCreditSalesTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCreditSalesTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCreditSalesTax カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCreditSalesTax カラムは既に存在します';
END

-- 仕入関連（5項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashPurchase')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashPurchase DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashPurchase カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCashPurchase カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashPurchaseTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashPurchaseTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashPurchaseTax カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCashPurchaseTax カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCreditPurchase')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCreditPurchase DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCreditPurchase カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCreditPurchase カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyPurchaseDiscount')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyPurchaseDiscount DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyPurchaseDiscount カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyPurchaseDiscount カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCreditPurchaseTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCreditPurchaseTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCreditPurchaseTax カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCreditPurchaseTax カラムは既に存在します';
END

-- 入金関連（3項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashReceipt カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCashReceipt カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyBankReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyBankReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyBankReceipt カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyBankReceipt カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyOtherReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyOtherReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyOtherReceipt カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyOtherReceipt カラムは既に存在します';
END

-- 支払関連（3項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyCashPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyCashPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyCashPayment カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyCashPayment カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyBankPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyBankPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyBankPayment カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyBankPayment カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'MonthlyOtherPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        MonthlyOtherPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'MonthlyOtherPayment カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'MonthlyOtherPayment カラムは既に存在します';
END

PRINT '月計カラム追加確認完了（16項目）';
PRINT '';

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
ELSE
BEGIN
    PRINT 'YearlyCashSales カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashSalesTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashSalesTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashSalesTax カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCashSalesTax カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCreditSales')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCreditSales DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCreditSales カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCreditSales カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlySalesDiscount')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlySalesDiscount DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlySalesDiscount カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlySalesDiscount カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCreditSalesTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCreditSalesTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCreditSalesTax カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCreditSalesTax カラムは既に存在します';
END

-- 仕入関連（5項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashPurchase')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashPurchase DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashPurchase カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCashPurchase カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashPurchaseTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashPurchaseTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashPurchaseTax カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCashPurchaseTax カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCreditPurchase')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCreditPurchase DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCreditPurchase カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCreditPurchase カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyPurchaseDiscount')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyPurchaseDiscount DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyPurchaseDiscount カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyPurchaseDiscount カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCreditPurchaseTax')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCreditPurchaseTax DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCreditPurchaseTax カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCreditPurchaseTax カラムは既に存在します';
END

-- 入金関連（3項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashReceipt カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCashReceipt カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyBankReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyBankReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyBankReceipt カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyBankReceipt カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyOtherReceipt')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyOtherReceipt DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyOtherReceipt カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyOtherReceipt カラムは既に存在します';
END

-- 支払関連（3項目）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyCashPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyCashPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyCashPayment カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyCashPayment カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyBankPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyBankPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyBankPayment カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyBankPayment カラムは既に存在します';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport' AND COLUMN_NAME = 'YearlyOtherPayment')
BEGIN
    ALTER TABLE BusinessDailyReport ADD 
        YearlyOtherPayment DECIMAL(19,2) DEFAULT 0;
    PRINT 'YearlyOtherPayment カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'YearlyOtherPayment カラムは既に存在します';
END

PRINT '年計カラム追加確認完了（16項目）';
PRINT '';

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
PRINT '';

-- ====================================================================
-- 4. 最終確認とレポート
-- ====================================================================
DECLARE @RecordCount INT;
DECLARE @ColumnCount INT;

SELECT @RecordCount = COUNT(*) FROM BusinessDailyReport;
SELECT @ColumnCount = COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BusinessDailyReport';

PRINT '====================================================================';
PRINT '営業日報テーブル月計・年計カラム追加完了';
PRINT '====================================================================';
PRINT '総レコード数: ' + CAST(@RecordCount AS NVARCHAR(10));
PRINT '総カラム数: ' + CAST(@ColumnCount AS NVARCHAR(10));
PRINT '追加カラム数: 月計16項目 + 年計16項目 = 32項目';
PRINT '====================================================================';
PRINT '';

-- デバッグ用：カラム一覧表示（オプション）
PRINT 'カラム一覧（月計・年計のみ表示）:';
SELECT COLUMN_NAME, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'BusinessDailyReport' 
  AND (COLUMN_NAME LIKE 'Monthly%' OR COLUMN_NAME LIKE 'Yearly%')
ORDER BY 
    CASE 
        WHEN COLUMN_NAME LIKE 'Monthly%' THEN 1
        WHEN COLUMN_NAME LIKE 'Yearly%' THEN 2
    END,
    COLUMN_NAME;

PRINT '';
PRINT '営業日報テーブル月計・年計カラム追加マイグレーション完了';
PRINT '実行日時: ' + CONVERT(NVARCHAR(30), GETDATE(), 120);