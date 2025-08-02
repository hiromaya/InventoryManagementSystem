-- 営業日報テーブル作成マイグレーション
-- 分類コード000-035の36行構成
-- 16項目の日計フィールドを持つ

-- テーブル存在確認と作成
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BusinessDailyReport')
BEGIN
    CREATE TABLE BusinessDailyReport (
        ClassificationCode NVARCHAR(3) NOT NULL,
        CustomerClassName NVARCHAR(12),
        SupplierClassName NVARCHAR(12),
        
        -- 売上関連（5項目）
        DailyCashSales DECIMAL(19,2) DEFAULT 0,
        DailyCashSalesTax DECIMAL(19,2) DEFAULT 0,
        DailyCreditSales DECIMAL(19,2) DEFAULT 0,
        DailySalesDiscount DECIMAL(19,2) DEFAULT 0,
        DailyCreditSalesTax DECIMAL(19,2) DEFAULT 0,
        
        -- 仕入関連（5項目）
        DailyCashPurchase DECIMAL(19,2) DEFAULT 0,
        DailyCashPurchaseTax DECIMAL(19,2) DEFAULT 0,
        DailyCreditPurchase DECIMAL(19,2) DEFAULT 0,
        DailyPurchaseDiscount DECIMAL(19,2) DEFAULT 0,
        DailyCreditPurchaseTax DECIMAL(19,2) DEFAULT 0,
        
        -- 入金関連（3項目）
        DailyCashReceipt DECIMAL(19,2) DEFAULT 0,
        DailyBankReceipt DECIMAL(19,2) DEFAULT 0,
        DailyOtherReceipt DECIMAL(19,2) DEFAULT 0,
        
        -- 支払関連（3項目）
        DailyCashPayment DECIMAL(19,2) DEFAULT 0,
        DailyBankPayment DECIMAL(19,2) DEFAULT 0,
        DailyOtherPayment DECIMAL(19,2) DEFAULT 0,
        
        -- 管理項目
        CreatedDate DATETIME2 DEFAULT GETDATE(),
        UpdatedDate DATETIME2 DEFAULT GETDATE(),
        
        CONSTRAINT PK_BusinessDailyReport PRIMARY KEY (ClassificationCode)
    );
    
    PRINT '営業日報テーブルを作成しました';
END
ELSE
BEGIN
    PRINT '営業日報テーブルは既に存在します';
END

-- インデックス作成確認
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('BusinessDailyReport') AND name = 'IX_BusinessDailyReport_Classification')
BEGIN
    CREATE INDEX IX_BusinessDailyReport_Classification 
    ON BusinessDailyReport (ClassificationCode);
    PRINT '営業日報インデックスを作成しました';
END

-- 初期データ挿入（000-035の36レコード）
-- 既存データがない場合のみ挿入
IF NOT EXISTS (SELECT 1 FROM BusinessDailyReport WHERE ClassificationCode = '000')
BEGIN
    -- 合計行（000）
    INSERT INTO BusinessDailyReport (ClassificationCode, CustomerClassName, SupplierClassName)
    VALUES ('000', '合計', '合計');
    
    -- 分類001-035
    INSERT INTO BusinessDailyReport (ClassificationCode, CustomerClassName, SupplierClassName)
    VALUES 
        ('001', '', ''),
        ('002', '', ''),
        ('003', '', ''),
        ('004', '', ''),
        ('005', '', ''),
        ('006', '', ''),
        ('007', '', ''),
        ('008', '', ''),
        ('009', '', ''),
        ('010', '', ''),
        ('011', '', ''),
        ('012', '', ''),
        ('013', '', ''),
        ('014', '', ''),
        ('015', '', ''),
        ('016', '', ''),
        ('017', '', ''),
        ('018', '', ''),
        ('019', '', ''),
        ('020', '', ''),
        ('021', '', ''),
        ('022', '', ''),
        ('023', '', ''),
        ('024', '', ''),
        ('025', '', ''),
        ('026', '', ''),
        ('027', '', ''),
        ('028', '', ''),
        ('029', '', ''),
        ('030', '', ''),
        ('031', '', ''),
        ('032', '', ''),
        ('033', '', ''),
        ('034', '', ''),
        ('035', '', '');
    
    PRINT '営業日報初期データ（36レコード）を挿入しました';
END
ELSE
BEGIN
    PRINT '営業日報初期データは既に存在します';
END

-- レコード数確認
DECLARE @RecordCount INT;
SELECT @RecordCount = COUNT(*) FROM BusinessDailyReport;
PRINT '営業日報テーブル総レコード数: ' + CAST(@RecordCount AS NVARCHAR(10));