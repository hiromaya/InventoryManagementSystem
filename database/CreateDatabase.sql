-- ===================================================
-- 在庫管理システム データベース作成スクリプト
-- ===================================================

-- データベース作成
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'InventoryManagementDB')
BEGIN
    CREATE DATABASE InventoryManagementDB;
    PRINT 'データベース InventoryManagementDB を作成しました';
END
ELSE
BEGIN
    PRINT 'データベース InventoryManagementDB は既に存在します';
END
GO

USE InventoryManagementDB;
GO

-- ===================================================
-- 1. InventoryMaster テーブル（在庫マスタ）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE InventoryMaster (
        ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
        GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
        ShippingMarkName NVARCHAR(50) NOT NULL,     -- 荷印名
        ProductName NVARCHAR(100) NOT NULL,         -- 商品名
        Unit NVARCHAR(10) NOT NULL,                 -- 単位
        StandardPrice DECIMAL(12,4) NOT NULL DEFAULT 0,    -- 標準単価
        ProductCategory1 NVARCHAR(10) NOT NULL DEFAULT '', -- 商品分類1
        ProductCategory2 NVARCHAR(10) NOT NULL DEFAULT '', -- 商品分類2
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),  -- 作成日
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),  -- 更新日
        CurrentStock DECIMAL(9,4) NOT NULL DEFAULT 0,      -- 現在在庫数
        CurrentStockAmount DECIMAL(12,4) NOT NULL DEFAULT 0, -- 現在在庫金額
        DailyStock DECIMAL(9,4) NOT NULL DEFAULT 0,        -- 当日在庫数
        DailyStockAmount DECIMAL(12,4) NOT NULL DEFAULT 0, -- 当日在庫金額
        DailyFlag NCHAR(1) NOT NULL DEFAULT '9',           -- 当日発生フラグ
        
        CONSTRAINT PK_InventoryMaster PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName)
    );
    PRINT 'InventoryMaster テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_InventoryMaster_ProductCode ON InventoryMaster(ProductCode);
    CREATE INDEX IX_InventoryMaster_ProductCategory1 ON InventoryMaster(ProductCategory1);
    CREATE INDEX IX_InventoryMaster_JobDate ON InventoryMaster(JobDate);
END

-- ===================================================
-- 2. CpInventoryMaster テーブル（CP在庫マスタ）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE CpInventoryMaster (
        ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
        GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
        ShippingMarkName NVARCHAR(50) NOT NULL,     -- 荷印名
        DataSetId NVARCHAR(100) NOT NULL,           -- データセットID
        ProductName NVARCHAR(100) NOT NULL,         -- 商品名
        Unit NVARCHAR(10) NOT NULL,                 -- 単位
        StandardPrice DECIMAL(12,4) NOT NULL DEFAULT 0,    -- 標準単価
        ProductCategory1 NVARCHAR(10) NOT NULL DEFAULT '', -- 商品分類1
        ProductCategory2 NVARCHAR(10) NOT NULL DEFAULT '', -- 商品分類2
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),  -- 作成日
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),  -- 更新日
        
        -- 前日在庫情報
        PreviousDayStock DECIMAL(9,4) NOT NULL DEFAULT 0,          -- 前日在庫数
        PreviousDayStockAmount DECIMAL(12,4) NOT NULL DEFAULT 0,   -- 前日在庫金額
        PreviousDayUnitPrice DECIMAL(12,4) NOT NULL DEFAULT 0,     -- 前日在庫単価
        
        -- 当日在庫情報
        DailyStock DECIMAL(9,4) NOT NULL DEFAULT 0,                -- 当日在庫数
        DailyStockAmount DECIMAL(12,4) NOT NULL DEFAULT 0,         -- 当日在庫金額
        DailyUnitPrice DECIMAL(12,4) NOT NULL DEFAULT 0,           -- 当日在庫単価
        DailyFlag NCHAR(1) NOT NULL DEFAULT '9',                   -- 当日発生フラグ
        
        -- 当日売上関連
        DailySalesQuantity DECIMAL(9,4) NOT NULL DEFAULT 0,        -- 当日売上数量
        DailySalesAmount DECIMAL(12,4) NOT NULL DEFAULT 0,         -- 当日売上金額
        DailySalesReturnQuantity DECIMAL(9,4) NOT NULL DEFAULT 0,  -- 当日売上返品数量
        DailySalesReturnAmount DECIMAL(12,4) NOT NULL DEFAULT 0,   -- 当日売上返品金額
        
        -- 当日仕入関連
        DailyPurchaseQuantity DECIMAL(9,4) NOT NULL DEFAULT 0,     -- 当日仕入数量
        DailyPurchaseAmount DECIMAL(12,4) NOT NULL DEFAULT 0,      -- 当日仕入金額
        DailyPurchaseReturnQuantity DECIMAL(9,4) NOT NULL DEFAULT 0, -- 当日仕入返品数量
        DailyPurchaseReturnAmount DECIMAL(12,4) NOT NULL DEFAULT 0,  -- 当日仕入返品金額
        
        -- 当日在庫調整関連
        DailyInventoryAdjustmentQuantity DECIMAL(9,4) NOT NULL DEFAULT 0, -- 当日在庫調整数量
        DailyInventoryAdjustmentAmount DECIMAL(12,4) NOT NULL DEFAULT 0,  -- 当日在庫調整金額
        
        -- 当日加工・振替関連
        DailyProcessingQuantity DECIMAL(9,4) NOT NULL DEFAULT 0,   -- 当日加工数量
        DailyProcessingAmount DECIMAL(12,4) NOT NULL DEFAULT 0,    -- 当日加工金額
        DailyTransferQuantity DECIMAL(9,4) NOT NULL DEFAULT 0,     -- 当日振替数量
        DailyTransferAmount DECIMAL(12,4) NOT NULL DEFAULT 0,      -- 当日振替金額
        
        -- 当日出入荷関連
        DailyReceiptQuantity DECIMAL(9,4) NOT NULL DEFAULT 0,      -- 当日入荷数量
        DailyReceiptAmount DECIMAL(12,4) NOT NULL DEFAULT 0,       -- 当日入荷金額
        DailyShipmentQuantity DECIMAL(9,4) NOT NULL DEFAULT 0,     -- 当日出荷数量
        DailyShipmentAmount DECIMAL(12,4) NOT NULL DEFAULT 0,      -- 当日出荷金額
        
        -- 粗利関連
        DailyGrossProfit DECIMAL(12,4) NOT NULL DEFAULT 0,         -- 当日粗利益
        DailyWalkingAmount DECIMAL(12,4) NOT NULL DEFAULT 0,       -- 当日歩引き額
        DailyIncentiveAmount DECIMAL(12,4) NOT NULL DEFAULT 0,     -- 当日奨励金
        DailyDiscountAmount DECIMAL(12,4) NOT NULL DEFAULT 0,      -- 当日仕入値引き額
        
        CONSTRAINT PK_CpInventoryMaster PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, DataSetId)
    );
    PRINT 'CpInventoryMaster テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_CpInventoryMaster_DataSetId ON CpInventoryMaster(DataSetId);
    CREATE INDEX IX_CpInventoryMaster_JobDate ON CpInventoryMaster(JobDate);
    CREATE INDEX IX_CpInventoryMaster_DailyFlag ON CpInventoryMaster(DailyFlag);
    CREATE INDEX IX_CpInventoryMaster_ProductCode ON CpInventoryMaster(ProductCode);
    CREATE INDEX IX_CpInventoryMaster_ProductCategory1 ON CpInventoryMaster(ProductCategory1);
END

-- ===================================================
-- 3. SalesVoucher テーブル（売上伝票）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SalesVoucher]') AND type in (N'U'))
BEGIN
    CREATE TABLE SalesVoucher (
        VoucherId NVARCHAR(50) NOT NULL,            -- 伝票ID
        LineNumber INT NOT NULL,                    -- 行番号
        ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
        GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
        ShippingMarkName NVARCHAR(50) NOT NULL,     -- 荷印名
        VoucherType NVARCHAR(10) NOT NULL,          -- 伝票種類
        DetailType NVARCHAR(10) NOT NULL,           -- 明細種類
        VoucherNumber NVARCHAR(20) NOT NULL,        -- 伝票番号
        VoucherDate DATE NOT NULL,                  -- 伝票日付
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        CustomerCode NVARCHAR(20),                  -- 得意先コード
        CustomerName NVARCHAR(100),                 -- 得意先名
        Quantity DECIMAL(9,4) NOT NULL,             -- 数量
        UnitPrice DECIMAL(12,4) NOT NULL,           -- 単価
        Amount DECIMAL(12,4) NOT NULL,              -- 金額
        InventoryUnitPrice DECIMAL(12,4) NOT NULL DEFAULT 0, -- 在庫単価
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),   -- 作成日
        DataSetId NVARCHAR(100),                    -- データセットID
        
        CONSTRAINT PK_SalesVoucher PRIMARY KEY (VoucherId, LineNumber)
    );
    PRINT 'SalesVoucher テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_SalesVoucher_VoucherDate ON SalesVoucher(VoucherDate);
    CREATE INDEX IX_SalesVoucher_JobDate ON SalesVoucher(JobDate);
    CREATE INDEX IX_SalesVoucher_ProductCode ON SalesVoucher(ProductCode);
    CREATE INDEX IX_SalesVoucher_DataSetId ON SalesVoucher(DataSetId);
END

-- ===================================================
-- 4. PurchaseVoucher テーブル（仕入伝票）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVoucher]') AND type in (N'U'))
BEGIN
    CREATE TABLE PurchaseVoucher (
        VoucherId NVARCHAR(50) NOT NULL,            -- 伝票ID
        LineNumber INT NOT NULL,                    -- 行番号
        ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
        GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
        ShippingMarkName NVARCHAR(50) NOT NULL,     -- 荷印名
        VoucherType NVARCHAR(10) NOT NULL,          -- 伝票種類
        DetailType NVARCHAR(10) NOT NULL,           -- 明細種類
        VoucherNumber NVARCHAR(20) NOT NULL,        -- 伝票番号
        VoucherDate DATE NOT NULL,                  -- 伝票日付
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        SupplierCode NVARCHAR(20),                  -- 仕入先コード
        SupplierName NVARCHAR(100),                 -- 仕入先名
        Quantity DECIMAL(9,4) NOT NULL,             -- 数量
        UnitPrice DECIMAL(12,4) NOT NULL,           -- 単価
        Amount DECIMAL(12,4) NOT NULL,              -- 金額
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),   -- 作成日
        DataSetId NVARCHAR(100),                    -- データセットID
        
        CONSTRAINT PK_PurchaseVoucher PRIMARY KEY (VoucherId, LineNumber)
    );
    PRINT 'PurchaseVoucher テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_PurchaseVoucher_VoucherDate ON PurchaseVoucher(VoucherDate);
    CREATE INDEX IX_PurchaseVoucher_JobDate ON PurchaseVoucher(JobDate);
    CREATE INDEX IX_PurchaseVoucher_ProductCode ON PurchaseVoucher(ProductCode);
    CREATE INDEX IX_PurchaseVoucher_DataSetId ON PurchaseVoucher(DataSetId);
END

-- ===================================================
-- 5. InventoryAdjustment テーブル（在庫調整）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustment]') AND type in (N'U'))
BEGIN
    CREATE TABLE InventoryAdjustment (
        VoucherId NVARCHAR(50) NOT NULL,            -- 伝票ID
        LineNumber INT NOT NULL,                    -- 行番号
        ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
        GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
        ShippingMarkName NVARCHAR(50) NOT NULL,     -- 荷印名
        VoucherType NVARCHAR(10) NOT NULL,          -- 伝票種類
        DetailType NVARCHAR(10) NOT NULL,           -- 明細種類
        VoucherNumber NVARCHAR(20) NOT NULL,        -- 伝票番号
        VoucherDate DATE NOT NULL,                  -- 伝票日付
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        CustomerCode NVARCHAR(20),                  -- 得意先コード
        CustomerName NVARCHAR(100),                 -- 得意先名
        CategoryCode INT,                           -- 区分コード (1:ロス, 4:振替, 6:調整, 2:経費, 5:加工)
        Quantity DECIMAL(9,4) NOT NULL,             -- 数量
        UnitPrice DECIMAL(12,4) NOT NULL,           -- 単価
        Amount DECIMAL(12,4) NOT NULL,              -- 金額
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),   -- 作成日
        DataSetId NVARCHAR(100),                    -- データセットID
        
        CONSTRAINT PK_InventoryAdjustment PRIMARY KEY (VoucherId, LineNumber)
    );
    PRINT 'InventoryAdjustment テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_InventoryAdjustment_VoucherDate ON InventoryAdjustment(VoucherDate);
    CREATE INDEX IX_InventoryAdjustment_JobDate ON InventoryAdjustment(JobDate);
    CREATE INDEX IX_InventoryAdjustment_ProductCode ON InventoryAdjustment(ProductCode);
    CREATE INDEX IX_InventoryAdjustment_CategoryCode ON InventoryAdjustment(CategoryCode);
    CREATE INDEX IX_InventoryAdjustment_DataSetId ON InventoryAdjustment(DataSetId);
END

-- ===================================================
-- 6. DataSet テーブル（データセット管理）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DataSet]') AND type in (N'U'))
BEGIN
    CREATE TABLE DataSet (
        Id NVARCHAR(100) NOT NULL,                  -- データセットID
        Name NVARCHAR(100) NOT NULL,                -- データセット名
        Description NVARCHAR(500),                  -- 説明
        ProcessType NVARCHAR(50) NOT NULL,          -- 処理種類
        Status NVARCHAR(20) NOT NULL DEFAULT 'Created', -- ステータス
        JobDate DATE NOT NULL,                      -- ジョブ日付
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),   -- 作成日
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),   -- 更新日
        CompletedDate DATETIME2,                    -- 完了日
        ErrorMessage NVARCHAR(MAX),                 -- エラーメッセージ
        
        CONSTRAINT PK_DataSet PRIMARY KEY (Id)
    );
    PRINT 'DataSet テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_DataSet_Status ON DataSet(Status);
    CREATE INDEX IX_DataSet_JobDate ON DataSet(JobDate);
    CREATE INDEX IX_DataSet_CreatedDate ON DataSet(CreatedDate);
END

PRINT '=== データベースセットアップが完了しました ===';