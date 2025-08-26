-- ====================================================================
-- CSV取込用テーブル作成スクリプト
-- 作成日: 2025-06-17
-- 用途: 販売大臣AXから出力されるCSVファイルの取込処理
-- ====================================================================

USE InventoryDB;
GO

-- ====================================================================
-- 1. データセット管理テーブル
-- 用途: CSV取込の単位管理、ステータス管理
-- ====================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DataSets')
BEGIN
    CREATE TABLE DataSets (
        Id NVARCHAR(50) PRIMARY KEY,           -- GUID形式のデータセットID
        DataSetType NVARCHAR(20) NOT NULL,     -- 'Sales', 'Purchase', 'Adjustment'
        ImportedAt DATETIME2 NOT NULL,         -- 取込日時
        RecordCount INT NOT NULL,              -- 取込件数
        Status NVARCHAR(20) NOT NULL,          -- 'Imported', 'Processing', 'Completed', 'Error'
        ErrorMessage NVARCHAR(MAX),            -- エラーメッセージ
        FilePath NVARCHAR(500),                -- 元ファイルパス
        JobDate DATE NOT NULL,                 -- ジョブ日付（汎用日付2）
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE()
    );
    
    CREATE INDEX IX_DataSets_Status ON DataSets(Status);
    CREATE INDEX IX_DataSets_JobDate ON DataSets(JobDate);
    CREATE INDEX IX_DataSets_DataSetType ON DataSets(DataSetType);
    
    PRINT 'DataSets テーブルを作成しました。';
END
ELSE
    PRINT 'DataSets テーブルは既に存在します。';
GO

-- ====================================================================
-- 2. 売上伝票テーブル
-- 用途: 売上伝票CSVの取込データ保存
-- ====================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SalesVouchers')
BEGIN
    CREATE TABLE SalesVouchers (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DataSetId NVARCHAR(50) NOT NULL,        -- データセットID（取込単位の識別）
        VoucherNumber NVARCHAR(20) NOT NULL,    -- 伝票番号
        VoucherDate DATE NOT NULL,              -- 伝票日付
        JobDate DATE NOT NULL,                  -- 汎用日付2（ジョブデート）
        VoucherType NVARCHAR(2) NOT NULL,       -- 伝票種別コード (51,52)
        DetailType NVARCHAR(2) NOT NULL,        -- 明細種別コード (1,2,3,4,18)
        CustomerCode NVARCHAR(15),              -- 得意先コード
        CustomerName NVARCHAR(100),             -- 得意先名
        ProductCode NVARCHAR(15) NOT NULL,      -- 商品コード
        ProductName NVARCHAR(100),              -- 商品名
        GradeCode NVARCHAR(15) NOT NULL,        -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,        -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL, -- 荷印コード
        ManualShippingMark NVARCHAR(50) NOT NULL, -- 荷印名
        Quantity DECIMAL(13,4) NOT NULL,        -- 数量
        UnitPrice DECIMAL(16,4) NOT NULL,       -- 単価
        Amount DECIMAL(16,4) NOT NULL,          -- 金額
        ProductCategory1 NVARCHAR(15),          -- 商品分類1（担当者コード）
        ProductCategory2 NVARCHAR(15),          -- 商品分類2
        ProductCategory3 NVARCHAR(15),          -- 商品分類3
        GrossProfit DECIMAL(16,4),              -- 粗利益（後で計算して更新）
        IsExcluded BIT DEFAULT 0,               -- 除外フラグ（アンマッチ処理時）
        ExcludeReason NVARCHAR(100),            -- 除外理由
        ImportedAt DATETIME2 DEFAULT GETDATE(), -- 取込日時
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE(),
        
        -- 外部キー制約
        CONSTRAINT FK_SalesVouchers_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)
    );
    
    -- インデックス作成
    CREATE INDEX IX_SalesVouchers_DataSetId ON SalesVouchers(DataSetId);
    CREATE INDEX IX_SalesVouchers_JobDate ON SalesVouchers(JobDate);
    CREATE INDEX IX_SalesVouchers_VoucherNumber ON SalesVouchers(VoucherNumber);
    CREATE INDEX IX_SalesVouchers_CustomerCode ON SalesVouchers(CustomerCode);
    CREATE INDEX IX_SalesVouchers_ProductCategory1 ON SalesVouchers(ProductCategory1);
    CREATE INDEX IX_SalesVouchers_InventoryKey ON SalesVouchers(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
    CREATE INDEX IX_SalesVouchers_VoucherType ON SalesVouchers(VoucherType, DetailType);
    
    PRINT 'SalesVouchers テーブルを作成しました。';
END
ELSE
    PRINT 'SalesVouchers テーブルは既に存在します。';
GO

-- ====================================================================
-- 3. 仕入伝票テーブル
-- 用途: 仕入伝票CSVの取込データ保存
-- ====================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseVouchers')
BEGIN
    CREATE TABLE PurchaseVouchers (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DataSetId NVARCHAR(50) NOT NULL,        -- データセットID
        VoucherNumber NVARCHAR(20) NOT NULL,    -- 伝票番号
        VoucherDate DATE NOT NULL,              -- 伝票日付
        JobDate DATE NOT NULL,                  -- 汎用日付2（ジョブデート）
        VoucherType NVARCHAR(2) NOT NULL,       -- 伝票種別コード (11,12)
        DetailType NVARCHAR(2) NOT NULL,        -- 明細種別コード (1,2,3,4)
        SupplierCode NVARCHAR(15),              -- 仕入先コード
        SupplierName NVARCHAR(100),             -- 仕入先名
        SupplierCategory1 NVARCHAR(15),         -- 仕入先分類1（奨励金計算用）
        ProductCode NVARCHAR(15) NOT NULL,      -- 商品コード
        ProductName NVARCHAR(100),              -- 商品名
        GradeCode NVARCHAR(15) NOT NULL,        -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,        -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL, -- 荷印コード
        ManualShippingMark NVARCHAR(50) NOT NULL, -- 荷印名
        Quantity DECIMAL(13,4) NOT NULL,        -- 数量
        UnitPrice DECIMAL(16,4) NOT NULL,       -- 単価
        Amount DECIMAL(16,4) NOT NULL,          -- 金額
        ProductCategory1 NVARCHAR(15),          -- 商品分類1（担当者コード）
        ProductCategory2 NVARCHAR(15),          -- 商品分類2
        ProductCategory3 NVARCHAR(15),          -- 商品分類3
        IsExcluded BIT DEFAULT 0,               -- 除外フラグ
        ExcludeReason NVARCHAR(100),            -- 除外理由
        ImportedAt DATETIME2 DEFAULT GETDATE(), -- 取込日時
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE(),
        
        -- 外部キー制約
        CONSTRAINT FK_PurchaseVouchers_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)
    );
    
    -- インデックス作成
    CREATE INDEX IX_PurchaseVouchers_DataSetId ON PurchaseVouchers(DataSetId);
    CREATE INDEX IX_PurchaseVouchers_JobDate ON PurchaseVouchers(JobDate);
    CREATE INDEX IX_PurchaseVouchers_VoucherNumber ON PurchaseVouchers(VoucherNumber);
    CREATE INDEX IX_PurchaseVouchers_SupplierCode ON PurchaseVouchers(SupplierCode);
    CREATE INDEX IX_PurchaseVouchers_ProductCategory1 ON PurchaseVouchers(ProductCategory1);
    CREATE INDEX IX_PurchaseVouchers_InventoryKey ON PurchaseVouchers(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
    CREATE INDEX IX_PurchaseVouchers_VoucherType ON PurchaseVouchers(VoucherType, DetailType);
    
    PRINT 'PurchaseVouchers テーブルを作成しました。';
END
ELSE
    PRINT 'PurchaseVouchers テーブルは既に存在します。';
GO

-- ====================================================================
-- 4. 在庫調整テーブル
-- 用途: 在庫調整CSVの取込データ保存
-- ====================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InventoryAdjustments')
BEGIN
    CREATE TABLE InventoryAdjustments (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DataSetId NVARCHAR(50) NOT NULL,        -- データセットID
        VoucherNumber NVARCHAR(20) NOT NULL,    -- 伝票番号
        VoucherDate DATE NOT NULL,              -- 伝票日付
        JobDate DATE NOT NULL,                  -- 汎用日付2（ジョブデート）
        VoucherType NVARCHAR(2) NOT NULL,       -- 伝票種別コード (71,72) ※無視
        DetailType NVARCHAR(2) NOT NULL,        -- 明細種別コード ※無視
        UnitCode NVARCHAR(2) NOT NULL,          -- 単位コード (01-06) ※重要
        ProductCode NVARCHAR(15) NOT NULL,      -- 商品コード
        ProductName NVARCHAR(100),              -- 商品名
        GradeCode NVARCHAR(15) NOT NULL,        -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,        -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL, -- 荷印コード
        ManualShippingMark NVARCHAR(50) NOT NULL, -- 荷印名
        Quantity DECIMAL(13,4) NOT NULL,        -- 数量
        UnitPrice DECIMAL(16,4) NOT NULL,       -- 単価
        Amount DECIMAL(16,4) NOT NULL,          -- 金額
        ProductCategory1 NVARCHAR(15),          -- 商品分類1（担当者コード）
        ProductCategory2 NVARCHAR(15),          -- 商品分類2
        ProductCategory3 NVARCHAR(15),          -- 商品分類3
        IsExcluded BIT DEFAULT 0,               -- 除外フラグ
        ExcludeReason NVARCHAR(100),            -- 除外理由
        ImportedAt DATETIME2 DEFAULT GETDATE(), -- 取込日時
        CreatedAt DATETIME2 DEFAULT GETDATE(),
        UpdatedAt DATETIME2 DEFAULT GETDATE(),
        
        -- 外部キー制約
        CONSTRAINT FK_InventoryAdjustments_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)
    );
    
    -- インデックス作成
    CREATE INDEX IX_InventoryAdjustments_DataSetId ON InventoryAdjustments(DataSetId);
    CREATE INDEX IX_InventoryAdjustments_JobDate ON InventoryAdjustments(JobDate);
    CREATE INDEX IX_InventoryAdjustments_VoucherNumber ON InventoryAdjustments(VoucherNumber);
    CREATE INDEX IX_InventoryAdjustments_UnitCode ON InventoryAdjustments(UnitCode);
    CREATE INDEX IX_InventoryAdjustments_ProductCategory1 ON InventoryAdjustments(ProductCategory1);
    CREATE INDEX IX_InventoryAdjustments_InventoryKey ON InventoryAdjustments(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
    
    PRINT 'InventoryAdjustments テーブルを作成しました。';
END
ELSE
    PRINT 'InventoryAdjustments テーブルは既に存在します。';
GO

-- ====================================================================
-- 5. テーブル権限設定
-- ====================================================================
-- 必要に応じてユーザー権限を設定
-- GRANT SELECT, INSERT, UPDATE, DELETE ON DataSets TO [InventoryUser];
-- GRANT SELECT, INSERT, UPDATE, DELETE ON SalesVouchers TO [InventoryUser];
-- GRANT SELECT, INSERT, UPDATE, DELETE ON PurchaseVouchers TO [InventoryUser];
-- GRANT SELECT, INSERT, UPDATE, DELETE ON InventoryAdjustments TO [InventoryUser];

PRINT '';
PRINT '====================================================================';
PRINT 'CSV取込用テーブル作成完了';
PRINT '作成されたテーブル:';
PRINT '  - DataSets (データセット管理)';
PRINT '  - SalesVouchers (売上伝票)';
PRINT '  - PurchaseVouchers (仕入伝票)';
PRINT '  - InventoryAdjustments (在庫調整)';
PRINT '====================================================================';