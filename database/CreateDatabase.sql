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
        DataSetId NVARCHAR(100),                           -- データセットID
        
        CONSTRAINT PK_InventoryMaster PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName)
    );
    PRINT 'InventoryMaster テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_InventoryMaster_ProductCode ON InventoryMaster(ProductCode);
    CREATE INDEX IX_InventoryMaster_ProductCategory1 ON InventoryMaster(ProductCategory1);
    CREATE INDEX IX_InventoryMaster_JobDate ON InventoryMaster(JobDate);
    CREATE INDEX IX_InventoryMaster_DataSetId ON InventoryMaster(DataSetId);
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
        DailyDiscountAmount DECIMAL(12,4) NOT NULL DEFAULT 0,      -- 当日歩引き額
        DailyPurchaseDiscountAmount DECIMAL(12,4) NOT NULL DEFAULT 0, -- 当日仕入値引き額
        
        -- 月計売上関連
        MonthlySalesQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,        -- 月計売上数量
        MonthlySalesAmount DECIMAL(18,4) NOT NULL DEFAULT 0,          -- 月計売上金額
        MonthlySalesReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,  -- 月計売上返品数量
        MonthlySalesReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0,    -- 月計売上返品金額
        
        -- 月計仕入関連
        MonthlyPurchaseQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,     -- 月計仕入数量
        MonthlyPurchaseAmount DECIMAL(18,4) NOT NULL DEFAULT 0,       -- 月計仕入金額
        MonthlyPurchaseReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0, -- 月計仕入返品数量
        MonthlyPurchaseReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0,   -- 月計仕入返品金額
        
        -- 月計在庫調整関連
        MonthlyInventoryAdjustmentQuantity DECIMAL(18,4) NOT NULL DEFAULT 0, -- 月計在庫調整数量
        MonthlyInventoryAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0,   -- 月計在庫調整金額
        
        -- 月計加工・振替関連
        MonthlyProcessingQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,   -- 月計加工数量
        MonthlyProcessingAmount DECIMAL(18,4) NOT NULL DEFAULT 0,     -- 月計加工金額
        MonthlyTransferQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,     -- 月計振替数量
        MonthlyTransferAmount DECIMAL(18,4) NOT NULL DEFAULT 0,       -- 月計振替金額
        
        -- 月計粗利益関連
        MonthlyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,          -- 月計粗利益
        MonthlyWalkingAmount DECIMAL(18,4) NOT NULL DEFAULT 0,        -- 月計歩引き額
        MonthlyIncentiveAmount DECIMAL(18,4) NOT NULL DEFAULT 0,      -- 月計奨励金
        
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
-- 3. SalesVouchers テーブル（売上伝票）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') AND type in (N'U'))
BEGIN
    CREATE TABLE SalesVouchers (
        VoucherId NVARCHAR(100) NOT NULL,            -- 伝票ID
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
        GrossProfit DECIMAL(16,4) NULL,             -- 粗利益（商品日報で計算）
        WalkingDiscount DECIMAL(16,4) NULL,         -- 歩引き金
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),   -- 作成日
        DataSetId NVARCHAR(100),                    -- データセットID
        
        CONSTRAINT PK_SalesVouchers PRIMARY KEY (VoucherId, LineNumber)
    );
    PRINT 'SalesVouchers テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_SalesVouchers_VoucherDate ON SalesVouchers(VoucherDate);
    CREATE INDEX IX_SalesVouchers_JobDate ON SalesVouchers(JobDate);
    CREATE INDEX IX_SalesVouchers_ProductCode ON SalesVouchers(ProductCode);
    CREATE INDEX IX_SalesVouchers_DataSetId ON SalesVouchers(DataSetId);
END

-- ===================================================
-- 4. PurchaseVouchers テーブル（仕入伝票）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVouchers]') AND type in (N'U'))
BEGIN
    CREATE TABLE PurchaseVouchers (
        VoucherId NVARCHAR(100) NOT NULL,            -- 伝票ID
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
        
        CONSTRAINT PK_PurchaseVouchers PRIMARY KEY (VoucherId, LineNumber)
    );
    PRINT 'PurchaseVouchers テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_PurchaseVouchers_VoucherDate ON PurchaseVouchers(VoucherDate);
    CREATE INDEX IX_PurchaseVouchers_JobDate ON PurchaseVouchers(JobDate);
    CREATE INDEX IX_PurchaseVouchers_ProductCode ON PurchaseVouchers(ProductCode);
    CREATE INDEX IX_PurchaseVouchers_DataSetId ON PurchaseVouchers(DataSetId);
END

-- ===================================================
-- 5. InventoryAdjustments テーブル（在庫調整）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') AND type in (N'U'))
BEGIN
    CREATE TABLE InventoryAdjustments (
        VoucherId NVARCHAR(100) NOT NULL,            -- 伝票ID
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
        
        CONSTRAINT PK_InventoryAdjustments PRIMARY KEY (VoucherId, LineNumber)
    );
    PRINT 'InventoryAdjustments テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_InventoryAdjustments_VoucherDate ON InventoryAdjustments(VoucherDate);
    CREATE INDEX IX_InventoryAdjustments_JobDate ON InventoryAdjustments(JobDate);
    CREATE INDEX IX_InventoryAdjustments_ProductCode ON InventoryAdjustments(ProductCode);
    CREATE INDEX IX_InventoryAdjustments_CategoryCode ON InventoryAdjustments(CategoryCode);
    CREATE INDEX IX_InventoryAdjustments_DataSetId ON InventoryAdjustments(DataSetId);
END

-- ===================================================
-- 6. ShippingMarkMaster テーブル（荷印マスタ）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ShippingMarkMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE ShippingMarkMaster (
        ShippingMarkCode NVARCHAR(15) NOT NULL,         -- 荷印コード（主キー）
        ShippingMarkName NVARCHAR(100) NOT NULL,        -- 荷印名
        SearchKana NVARCHAR(100) NULL,                  -- 検索用カナ
        NumericValue1 DECIMAL(18,4) NULL,               -- 数値項目1
        NumericValue2 DECIMAL(18,4) NULL,               -- 数値項目2
        NumericValue3 DECIMAL(18,4) NULL,               -- 数値項目3
        NumericValue4 DECIMAL(18,4) NULL,               -- 数値項目4
        NumericValue5 DECIMAL(18,4) NULL,               -- 数値項目5
        DateValue1 DATE NULL,                           -- 日付項目1
        DateValue2 DATE NULL,                           -- 日付項目2
        DateValue3 DATE NULL,                           -- 日付項目3
        DateValue4 DATE NULL,                           -- 日付項目4
        DateValue5 DATE NULL,                           -- 日付項目5
        TextValue1 NVARCHAR(100) NULL,                  -- テキスト項目1
        TextValue2 NVARCHAR(100) NULL,                  -- テキスト項目2
        TextValue3 NVARCHAR(100) NULL,                  -- テキスト項目3
        TextValue4 NVARCHAR(100) NULL,                  -- テキスト項目4
        TextValue5 NVARCHAR(100) NULL,                  -- テキスト項目5
        
        CONSTRAINT PK_ShippingMarkMaster PRIMARY KEY (ShippingMarkCode)
    );
    PRINT 'ShippingMarkMaster テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_ShippingMarkMaster_ShippingMarkName ON ShippingMarkMaster(ShippingMarkName);
    CREATE INDEX IX_ShippingMarkMaster_SearchKana ON ShippingMarkMaster(SearchKana);
END

-- ===================================================
-- 7. DataSets テーブル（データセット管理）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND type in (N'U'))
BEGIN
    CREATE TABLE DataSets (
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
        
        CONSTRAINT PK_DataSets PRIMARY KEY (Id)
    );
    PRINT 'DataSets テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_DataSets_Status ON DataSets(Status);
    CREATE INDEX IX_DataSets_JobDate ON DataSets(JobDate);
    CREATE INDEX IX_DataSets_CreatedDate ON DataSets(CreatedDate);
END

PRINT '=== データベースセットアップが完了しました ===';