-- ===================================================
-- ProductMaster テーブルの作成
-- 作成日: 2025-07-15
-- 目的: 初期在庫インポートサービスで必要な商品マスタテーブルを作成
-- ===================================================

USE InventoryManagementDB;
GO

-- ProductMaster テーブルの作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE ProductMaster (
        ProductCode NVARCHAR(15) NOT NULL PRIMARY KEY,    -- 商品コード（5桁）
        ProductName NVARCHAR(100) NOT NULL,               -- 商品名
        ProductCategory1 NVARCHAR(10) NOT NULL DEFAULT '',-- 商品分類1
        ProductCategory2 NVARCHAR(10) NOT NULL DEFAULT '',-- 商品分類2
        Unit NVARCHAR(10) NOT NULL DEFAULT '',            -- 単位
        StandardPrice DECIMAL(12,4) NOT NULL DEFAULT 0,   -- 標準単価
        IsActive BIT NOT NULL DEFAULT 1,                  -- 有効フラグ
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 作成日
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 更新日
        Notes NVARCHAR(500) NULL                          -- 備考
    );
    
    -- インデックスの作成
    CREATE INDEX IX_ProductMaster_ProductName ON ProductMaster(ProductName);
    CREATE INDEX IX_ProductMaster_ProductCategory1 ON ProductMaster(ProductCategory1);
    CREATE INDEX IX_ProductMaster_IsActive ON ProductMaster(IsActive);
    
    PRINT 'ProductMaster テーブルを作成しました';
END
ELSE
BEGIN
    PRINT 'ProductMaster テーブルは既に存在します';
END
GO

-- CustomerMaster テーブルの作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CustomerMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE CustomerMaster (
        CustomerCode NVARCHAR(20) NOT NULL PRIMARY KEY,   -- 得意先コード
        CustomerName NVARCHAR(100) NOT NULL,              -- 得意先名
        CustomerKana NVARCHAR(100) NULL,                  -- 得意先カナ
        ZipCode NVARCHAR(10) NULL,                        -- 郵便番号
        Address1 NVARCHAR(100) NULL,                      -- 住所1
        Address2 NVARCHAR(100) NULL,                      -- 住所2
        Phone NVARCHAR(20) NULL,                          -- 電話番号
        Fax NVARCHAR(20) NULL,                            -- FAX番号
        IsActive BIT NOT NULL DEFAULT 1,                  -- 有効フラグ
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 作成日
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()  -- 更新日
    );
    
    -- インデックスの作成
    CREATE INDEX IX_CustomerMaster_CustomerName ON CustomerMaster(CustomerName);
    CREATE INDEX IX_CustomerMaster_IsActive ON CustomerMaster(IsActive);
    
    PRINT 'CustomerMaster テーブルを作成しました';
END
ELSE
BEGIN
    PRINT 'CustomerMaster テーブルは既に存在します';
END
GO

-- SupplierMaster テーブルの作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SupplierMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE SupplierMaster (
        SupplierCode NVARCHAR(20) NOT NULL PRIMARY KEY,   -- 仕入先コード
        SupplierName NVARCHAR(100) NOT NULL,              -- 仕入先名
        SupplierKana NVARCHAR(100) NULL,                  -- 仕入先カナ
        ZipCode NVARCHAR(10) NULL,                        -- 郵便番号
        Address1 NVARCHAR(100) NULL,                      -- 住所1
        Address2 NVARCHAR(100) NULL,                      -- 住所2
        Phone NVARCHAR(20) NULL,                          -- 電話番号
        Fax NVARCHAR(20) NULL,                            -- FAX番号
        IsActive BIT NOT NULL DEFAULT 1,                  -- 有効フラグ
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 作成日
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()  -- 更新日
    );
    
    -- インデックスの作成
    CREATE INDEX IX_SupplierMaster_SupplierName ON SupplierMaster(SupplierName);
    CREATE INDEX IX_SupplierMaster_IsActive ON SupplierMaster(IsActive);
    
    PRINT 'SupplierMaster テーブルを作成しました';
END
ELSE
BEGIN
    PRINT 'SupplierMaster テーブルは既に存在します';
END
GO

-- 初期在庫インポートに必要な最小限の商品マスタデータを作成
-- 実際の商品マスタは別途インポートが必要
INSERT INTO ProductMaster (ProductCode, ProductName, ProductCategory1, Unit, StandardPrice)
SELECT DISTINCT 
    ProductCode,
    '商品' + ProductCode AS ProductName,
    'その他' AS ProductCategory1,
    'KG' AS Unit,
    0 AS StandardPrice
FROM (
    VALUES 
    ('00104'), ('00105'), ('00113'), ('00120'), ('00132'), ('00134'), ('00160'), ('00177'), 
    ('00199'), ('00200'), ('00401'), ('00415'), ('00438'), ('00499'), ('00502'), ('00504'), 
    ('00583'), ('00599'), ('00605'), ('00624'), ('00698'), ('00699'), ('00998'), ('01503'), 
    ('01508'), ('01512'), ('01533'), ('01539'), ('01599'), ('01703')
) AS TempProducts(ProductCode)
WHERE NOT EXISTS (SELECT 1 FROM ProductMaster WHERE ProductMaster.ProductCode = TempProducts.ProductCode);

PRINT 'Migration 024: ProductMaster, CustomerMaster, SupplierMaster テーブルを作成しました';
GO