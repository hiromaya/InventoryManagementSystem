-- =============================================
-- 在庫管理システム マスタテーブル作成スクリプト
-- =============================================

USE InventoryManagementDB;
GO

-- 1. 得意先マスタ（CustomerMaster）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CustomerMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE CustomerMaster (
        CustomerCode NVARCHAR(15) NOT NULL PRIMARY KEY,  -- 得意先コード
        CustomerName NVARCHAR(100) NOT NULL,             -- 得意先名
        CustomerName2 NVARCHAR(100),                     -- 得意先名2
        SearchKana NVARCHAR(100),                        -- 検索カナ
        ShortName NVARCHAR(50),                          -- 略称
        PostalCode NVARCHAR(10),                         -- 郵便番号
        Address1 NVARCHAR(100),                          -- 住所1
        Address2 NVARCHAR(100),                          -- 住所2
        Address3 NVARCHAR(100),                          -- 住所3
        PhoneNumber NVARCHAR(20),                        -- 電話番号
        FaxNumber NVARCHAR(20),                          -- FAX番号
        CustomerCategory1 NVARCHAR(15),                  -- 取引先分類1
        CustomerCategory2 NVARCHAR(15),                  -- 分類2
        CustomerCategory3 NVARCHAR(15),                  -- 分類3
        CustomerCategory4 NVARCHAR(15),                  -- 分類4
        CustomerCategory5 NVARCHAR(15),                  -- 分類5
        WalkingRate DECIMAL(5,2),                        -- 歩引き率（汎用数値1）
        BillingCode NVARCHAR(15),                        -- 請求先コード
        IsActive BIT DEFAULT 1,                          -- 取引区分（1:取引中、0:取引終了）
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    -- インデックス
    CREATE INDEX IX_CustomerMaster_CustomerName ON CustomerMaster(CustomerName);
    CREATE INDEX IX_CustomerMaster_SearchKana ON CustomerMaster(SearchKana);
    CREATE INDEX IX_CustomerMaster_BillingCode ON CustomerMaster(BillingCode);
    CREATE INDEX IX_CustomerMaster_IsActive ON CustomerMaster(IsActive);
END
GO

-- 2. 商品マスタ（ProductMaster）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE ProductMaster (
        ProductCode NVARCHAR(15) NOT NULL PRIMARY KEY,   -- 商品コード
        ProductName NVARCHAR(100) NOT NULL,              -- 商品名
        ProductName2 NVARCHAR(100),                      -- 名称2
        ProductName3 NVARCHAR(100),                      -- 名称3
        ProductName4 NVARCHAR(100),                      -- 名称4
        ProductName5 NVARCHAR(100),                      -- 名称5
        SearchKana NVARCHAR(100),                        -- 検索カナ
        ShortName NVARCHAR(50),                          -- 略称
        PrintCode NVARCHAR(20),                          -- 印刷用コード
        ProductCategory1 NVARCHAR(15),                   -- 分類1コード（担当者）
        ProductCategory2 NVARCHAR(15),                   -- 分類2コード
        ProductCategory3 NVARCHAR(15),                   -- 分類3コード
        ProductCategory4 NVARCHAR(15),                   -- 分類4コード
        ProductCategory5 NVARCHAR(15),                   -- 分類5コード
        UnitCode NVARCHAR(10),                           -- バラ単位コード
        CaseUnitCode NVARCHAR(10),                       -- ケース単位コード
        Case2UnitCode NVARCHAR(10),                      -- ケース2単位コード
        CaseQuantity DECIMAL(13,4),                      -- ケース入数
        Case2Quantity DECIMAL(13,4),                     -- ケース2入数
        StandardPrice DECIMAL(16,4),                     -- バラ標準価格
        CaseStandardPrice DECIMAL(16,4),                 -- ケース標準価格
        IsStockManaged BIT DEFAULT 1,                    -- 在庫管理フラグ
        TaxRate INT,                                     -- 消費税率
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    -- インデックス
    CREATE INDEX IX_ProductMaster_ProductName ON ProductMaster(ProductName);
    CREATE INDEX IX_ProductMaster_SearchKana ON ProductMaster(SearchKana);
    CREATE INDEX IX_ProductMaster_ProductCategory1 ON ProductMaster(ProductCategory1);
    CREATE INDEX IX_ProductMaster_IsStockManaged ON ProductMaster(IsStockManaged);
END
GO

-- 3. 仕入先マスタ（SupplierMaster）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SupplierMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE SupplierMaster (
        SupplierCode NVARCHAR(15) NOT NULL PRIMARY KEY,  -- 仕入先コード
        SupplierName NVARCHAR(100) NOT NULL,             -- 仕入先名
        SupplierName2 NVARCHAR(100),                     -- 仕入先名2
        SearchKana NVARCHAR(100),                        -- 検索カナ
        ShortName NVARCHAR(50),                          -- 略称
        PostalCode NVARCHAR(10),                         -- 郵便番号
        Address1 NVARCHAR(100),                          -- 住所1
        Address2 NVARCHAR(100),                          -- 住所2
        Address3 NVARCHAR(100),                          -- 住所3
        PhoneNumber NVARCHAR(20),                        -- 電話番号
        FaxNumber NVARCHAR(20),                          -- FAX番号
        SupplierCategory1 NVARCHAR(15),                  -- 分類1（'01'なら奨励金対象）
        SupplierCategory2 NVARCHAR(15),                  -- 分類2
        SupplierCategory3 NVARCHAR(15),                  -- 分類3
        PaymentCode NVARCHAR(15),                        -- 支払先コード
        IsActive BIT DEFAULT 1,                          -- 取引区分
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    -- インデックス
    CREATE INDEX IX_SupplierMaster_SupplierName ON SupplierMaster(SupplierName);
    CREATE INDEX IX_SupplierMaster_SearchKana ON SupplierMaster(SearchKana);
    CREATE INDEX IX_SupplierMaster_PaymentCode ON SupplierMaster(PaymentCode);
    CREATE INDEX IX_SupplierMaster_IsActive ON SupplierMaster(IsActive);
END
GO

-- 4. 等級マスタ（GradeMaster）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GradeMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE GradeMaster (
        GradeCode NVARCHAR(15) NOT NULL PRIMARY KEY,     -- 等級コード
        GradeName NVARCHAR(50) NOT NULL,                 -- 等級名
        SearchKana NVARCHAR(100),                        -- 検索カナ
        NumericValue1 DECIMAL(16,4),                     -- 汎用数値1
        NumericValue2 DECIMAL(16,4),                     -- 汎用数値2
        NumericValue3 DECIMAL(16,4),                     -- 汎用数値3
        NumericValue4 DECIMAL(16,4),                     -- 汎用数値4
        NumericValue5 DECIMAL(16,4),                     -- 汎用数値5
        DateValue1 DATE,                                 -- 汎用日付1
        DateValue2 DATE,                                 -- 汎用日付2
        DateValue3 DATE,                                 -- 汎用日付3
        DateValue4 DATE,                                 -- 汎用日付4
        DateValue5 DATE,                                 -- 汎用日付5
        TextValue1 NVARCHAR(255),                        -- 汎用摘要1
        TextValue2 NVARCHAR(255),                        -- 汎用摘要2
        TextValue3 NVARCHAR(255),                        -- 汎用摘要3
        TextValue4 NVARCHAR(255),                        -- 汎用摘要4
        TextValue5 NVARCHAR(255),                        -- 汎用摘要5
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    -- インデックス
    CREATE INDEX IX_GradeMaster_GradeName ON GradeMaster(GradeName);
    CREATE INDEX IX_GradeMaster_SearchKana ON GradeMaster(SearchKana);
END
GO

-- 5. 階級マスタ（ClassMaster）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ClassMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE ClassMaster (
        ClassCode NVARCHAR(15) NOT NULL PRIMARY KEY,     -- 階級コード
        ClassName NVARCHAR(50) NOT NULL,                 -- 階級名
        SearchKana NVARCHAR(100),                        -- 検索カナ
        NumericValue1 DECIMAL(16,4),                     -- 汎用数値1
        NumericValue2 DECIMAL(16,4),                     -- 汎用数値2
        NumericValue3 DECIMAL(16,4),                     -- 汎用数値3
        NumericValue4 DECIMAL(16,4),                     -- 汎用数値4
        NumericValue5 DECIMAL(16,4),                     -- 汎用数値5
        DateValue1 DATE,                                 -- 汎用日付1
        DateValue2 DATE,                                 -- 汎用日付2
        DateValue3 DATE,                                 -- 汎用日付3
        DateValue4 DATE,                                 -- 汎用日付4
        DateValue5 DATE,                                 -- 汎用日付5
        TextValue1 NVARCHAR(255),                        -- 汎用摘要1
        TextValue2 NVARCHAR(255),                        -- 汎用摘要2
        TextValue3 NVARCHAR(255),                        -- 汎用摘要3
        TextValue4 NVARCHAR(255),                        -- 汎用摘要4
        TextValue5 NVARCHAR(255),                        -- 汎用摘要5
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    -- インデックス
    CREATE INDEX IX_ClassMaster_ClassName ON ClassMaster(ClassName);
    CREATE INDEX IX_ClassMaster_SearchKana ON ClassMaster(SearchKana);
END
GO

-- 6. 荷印マスタ（ShippingMarkMaster）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ShippingMarkMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE ShippingMarkMaster (
        ShippingMarkCode NVARCHAR(15) NOT NULL PRIMARY KEY, -- 荷印コード
        ShippingMarkName NVARCHAR(50) NOT NULL,            -- 荷印名
        SearchKana NVARCHAR(100),                           -- 検索カナ
        NumericValue1 DECIMAL(16,4),                       -- 汎用数値1
        NumericValue2 DECIMAL(16,4),                       -- 汎用数値2
        NumericValue3 DECIMAL(16,4),                       -- 汎用数値3
        NumericValue4 DECIMAL(16,4),                       -- 汎用数値4
        NumericValue5 DECIMAL(16,4),                       -- 汎用数値5
        DateValue1 DATE,                                   -- 汎用日付1
        DateValue2 DATE,                                   -- 汎用日付2
        DateValue3 DATE,                                   -- 汎用日付3
        DateValue4 DATE,                                   -- 汎用日付4
        DateValue5 DATE,                                   -- 汎用日付5
        TextValue1 NVARCHAR(255),                          -- 汎用摘要1
        TextValue2 NVARCHAR(255),                          -- 汎用摘要2
        TextValue3 NVARCHAR(255),                          -- 汎用摘要3
        TextValue4 NVARCHAR(255),                          -- 汎用摘要4
        TextValue5 NVARCHAR(255),                          -- 汎用摘要5
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    -- インデックス
    CREATE INDEX IX_ShippingMarkMaster_ShippingMarkName ON ShippingMarkMaster(ShippingMarkName);
    CREATE INDEX IX_ShippingMarkMaster_SearchKana ON ShippingMarkMaster(SearchKana);
END
GO

-- 7. 産地マスタ（OriginMaster）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OriginMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE OriginMaster (
        OriginCode NVARCHAR(15) NOT NULL PRIMARY KEY,    -- 産地コード
        OriginName NVARCHAR(50) NOT NULL,                -- 産地名
        SearchKana NVARCHAR(100),                        -- 検索カナ
        NumericValue1 DECIMAL(16,4),                     -- 汎用数値1
        NumericValue2 DECIMAL(16,4),                     -- 汎用数値2
        NumericValue3 DECIMAL(16,4),                     -- 汎用数値3
        NumericValue4 DECIMAL(16,4),                     -- 汎用数値4
        NumericValue5 DECIMAL(16,4),                     -- 汎用数値5
        DateValue1 DATE,                                 -- 汎用日付1
        DateValue2 DATE,                                 -- 汎用日付2
        DateValue3 DATE,                                 -- 汎用日付3
        DateValue4 DATE,                                 -- 汎用日付4
        DateValue5 DATE,                                 -- 汎用日付5
        TextValue1 NVARCHAR(255),                        -- 汎用摘要1
        TextValue2 NVARCHAR(255),                        -- 汎用摘要2
        TextValue3 NVARCHAR(255),                        -- 汎用摘要3
        TextValue4 NVARCHAR(255),                        -- 汎用摘要4
        TextValue5 NVARCHAR(255),                        -- 汎用摘要5
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    -- インデックス
    CREATE INDEX IX_OriginMaster_OriginName ON OriginMaster(OriginName);
    CREATE INDEX IX_OriginMaster_SearchKana ON OriginMaster(SearchKana);
END
GO

-- 8. 単位マスタ（UnitMaster）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UnitMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE UnitMaster (
        UnitCode NVARCHAR(10) NOT NULL PRIMARY KEY,      -- コード
        UnitName NVARCHAR(20) NOT NULL,                  -- 名称
        SearchKana NVARCHAR(50),                         -- 検索カナ
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    -- インデックス
    CREATE INDEX IX_UnitMaster_UnitName ON UnitMaster(UnitName);
    CREATE INDEX IX_UnitMaster_SearchKana ON UnitMaster(SearchKana);
END
GO

-- 9. 分類マスタ（汎用）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CategoryMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE CategoryMaster (
        CategoryType NVARCHAR(20) NOT NULL,              -- 分類タイプ（例：'ProductCategory1'）
        CategoryCode NVARCHAR(15) NOT NULL,              -- コード
        CategoryName NVARCHAR(50) NOT NULL,              -- 名称
        SearchKana NVARCHAR(100),                        -- 検索カナ
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT PK_CategoryMaster PRIMARY KEY (CategoryType, CategoryCode)
    );
    
    -- インデックス
    CREATE INDEX IX_CategoryMaster_CategoryType ON CategoryMaster(CategoryType);
    CREATE INDEX IX_CategoryMaster_CategoryName ON CategoryMaster(CategoryName);
    CREATE INDEX IX_CategoryMaster_SearchKana ON CategoryMaster(SearchKana);
END
GO

-- 外部キー制約（必要に応じて追加）
-- 例：商品マスタの単位コード
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ProductMaster_UnitCode]'))
BEGIN
    ALTER TABLE ProductMaster
    ADD CONSTRAINT FK_ProductMaster_UnitCode
    FOREIGN KEY (UnitCode) REFERENCES UnitMaster(UnitCode);
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ProductMaster_CaseUnitCode]'))
BEGIN
    ALTER TABLE ProductMaster
    ADD CONSTRAINT FK_ProductMaster_CaseUnitCode
    FOREIGN KEY (CaseUnitCode) REFERENCES UnitMaster(UnitCode);
END
GO

-- 得意先マスタの請求先コード（自己参照制約はマスタ投入完了後に設定）
-- IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_CustomerMaster_BillingCode]'))
-- BEGIN
--     ALTER TABLE CustomerMaster
--     ADD CONSTRAINT FK_CustomerMaster_BillingCode
--     FOREIGN KEY (BillingCode) REFERENCES CustomerMaster(CustomerCode);
-- END
-- GO

-- 仕入先マスタの支払先コード（自己参照制約はマスタ投入完了後に設定）
-- IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_SupplierMaster_PaymentCode]'))
-- BEGIN
--     ALTER TABLE SupplierMaster
--     ADD CONSTRAINT FK_SupplierMaster_PaymentCode
--     FOREIGN KEY (PaymentCode) REFERENCES SupplierMaster(SupplierCode);
-- END
-- GO

PRINT 'マスタテーブルの作成が完了しました。';
GO