-- ====================================================================
-- 前月末在庫テーブル
-- 作成日: 2025-07-10
-- 用途: 月初に設定する前月末在庫を管理
-- ====================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PreviousMonthInventory')
BEGIN
    CREATE TABLE PreviousMonthInventory (
        -- 5項目キー
        ProductCode NVARCHAR(5) NOT NULL,
        GradeCode NVARCHAR(3) NOT NULL,
        ClassCode NVARCHAR(3) NOT NULL,
        ShippingMarkCode NVARCHAR(4) NOT NULL,
        ShippingMarkName NVARCHAR(8) NOT NULL,
        
        -- 商品情報
        ProductName NVARCHAR(100) NOT NULL DEFAULT '',
        Unit NVARCHAR(10) NOT NULL DEFAULT 'PCS',
        
        -- 前月末在庫
        Quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        Amount DECIMAL(18,4) NOT NULL DEFAULT 0,
        UnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- 管理情報
        YearMonth NVARCHAR(6) NOT NULL, -- YYYYMM形式
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
        CreatedBy NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        
        -- 主キー（5項目キー + 年月）
        CONSTRAINT PK_PreviousMonthInventory PRIMARY KEY CLUSTERED (
            ProductCode,
            GradeCode,
            ClassCode,
            ShippingMarkCode,
            ShippingMarkName,
            YearMonth
        )
    );
    
    PRINT 'テーブル PreviousMonthInventory を作成しました。';
END
ELSE
BEGIN
    PRINT 'テーブル PreviousMonthInventory は既に存在します。';
END
GO

-- インデックス作成
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PreviousMonthInventory_YearMonth')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PreviousMonthInventory_YearMonth
    ON PreviousMonthInventory (YearMonth)
    INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, Quantity, Amount);
    
    PRINT 'インデックス IX_PreviousMonthInventory_YearMonth を作成しました。';
END
GO