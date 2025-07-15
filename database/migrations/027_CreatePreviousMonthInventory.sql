-- =============================================
-- PreviousMonthInventory テーブル作成
-- 作成日: 2025-07-15
-- 説明: 前月末在庫を管理するテーブル
-- =============================================

USE InventoryManagementDB;
GO

-- テーブルが存在しない場合のみ作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PreviousMonthInventory]') AND type in (N'U'))
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
        
        -- 前月末在庫情報
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
    
    PRINT 'PreviousMonthInventory テーブルを作成しました。';
END
ELSE
BEGIN
    PRINT 'PreviousMonthInventory テーブルは既に存在します。';
END
GO

-- インデックス作成
-- YearMonthによる検索用インデックス（主要カラムを含む）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[PreviousMonthInventory]') AND name = N'IX_PreviousMonthInventory_YearMonth')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PreviousMonthInventory_YearMonth 
    ON PreviousMonthInventory (YearMonth)
    INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, Quantity, Amount);
    
    PRINT 'IX_PreviousMonthInventory_YearMonth インデックスを作成しました。';
END
GO

PRINT 'Migration 027: PreviousMonthInventory テーブルとインデックスの作成が完了しました。';
GO