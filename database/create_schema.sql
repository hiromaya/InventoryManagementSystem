-- 在庫管理システム データベーススキーマ
-- 作成日: 2025年6月16日

-- データベース作成
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'InventoryManagementDB')
BEGIN
    CREATE DATABASE InventoryManagementDB;
END;
GO

USE InventoryManagementDB;
GO

-- ===================================================================
-- 1. 在庫マスタテーブル
-- ===================================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='InventoryMaster' AND xtype='U')
BEGIN
    CREATE TABLE InventoryMaster (
        -- 5項目複合キー（スナップショット管理モデル）
        ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
        GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
        ManualShippingMark NVARCHAR(50) NOT NULL,     -- 荷印名
        
        -- 基本情報
        ProductName NVARCHAR(100) NOT NULL,         -- 商品名
        Unit NVARCHAR(20) NOT NULL,                 -- 単位
        StandardPrice DECIMAL(18,4) NOT NULL,       -- 標準単価
        ProductCategory1 NVARCHAR(10) NOT NULL,     -- 商品分類1
        ProductCategory2 NVARCHAR(10) NOT NULL,     -- 商品分類2
        
        -- 日付管理
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        CreatedDate DATETIME2 NOT NULL,             -- 作成日
        UpdatedDate DATETIME2 NOT NULL,             -- 更新日
        
        -- 在庫情報
        CurrentStock DECIMAL(18,4) NOT NULL,        -- 現在在庫数
        CurrentStockAmount DECIMAL(18,4) NOT NULL,  -- 現在在庫金額
        DailyStock DECIMAL(18,4) NOT NULL,          -- 当日在庫数
        DailyStockAmount DECIMAL(18,4) NOT NULL,    -- 当日在庫金額
        
        -- 当日発生フラグ ('0':データあり, '9':クリア状態)
        DailyFlag CHAR(1) NOT NULL DEFAULT '9',
        
        -- 粗利情報
        DailyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,      -- 当日粗利益
        DailyAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0, -- 当日在庫調整金額
        DailyProcessingCost DECIMAL(18,4) NOT NULL DEFAULT 0,   -- 当日加工費
        FinalGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,      -- 最終粗利益
        
        -- データセットID管理
        DataSetId NVARCHAR(50) NOT NULL DEFAULT '',
        
        -- 制約（5項目主キー - JobDateは含まない）
        CONSTRAINT PK_InventoryMaster PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark)
    );
    
    -- インデックス
    CREATE INDEX IX_InventoryMaster_JobDate ON InventoryMaster (JobDate);
    CREATE INDEX IX_InventoryMaster_ProductCategory1 ON InventoryMaster (ProductCategory1);
    CREATE INDEX IX_InventoryMaster_DataSetId ON InventoryMaster (DataSetId);
END;

-- ===================================================================
-- 2. CP在庫マスタテーブル（コピー用）
-- ===================================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CpInventoryMaster' AND xtype='U')
BEGIN
    CREATE TABLE CpInventoryMaster (
        -- 5項目複合キー
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ManualShippingMark NVARCHAR(50) NOT NULL,
        
        -- 基本情報
        ProductName NVARCHAR(100) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        StandardPrice DECIMAL(18,4) NOT NULL,
        ProductCategory1 NVARCHAR(10) NOT NULL,
        ProductCategory2 NVARCHAR(10) NOT NULL,
        
        -- 日付管理
        JobDate DATE NOT NULL,
        CreatedDate DATETIME2 NOT NULL,
        UpdatedDate DATETIME2 NOT NULL,
        
        -- 前日在庫情報
        PreviousDayStock DECIMAL(18,4) NOT NULL DEFAULT 0,
        PreviousDayStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        PreviousDayUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- 当日在庫情報
        DailyStock DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- 当日発生フラグ
        DailyFlag CHAR(1) NOT NULL DEFAULT '9',
        
        -- 当日売上関連
        DailySalesQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailySalesAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailySalesReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailySalesReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- 当日仕入関連
        DailyPurchaseQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyPurchaseAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyPurchaseReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyPurchaseReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- 当日在庫調整関連
        DailyInventoryAdjustmentQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyInventoryAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- 当日加工・振替関連
        DailyProcessingQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyProcessingAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyTransferQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyTransferAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- 当日出入荷関連
        DailyReceiptQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyReceiptAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyShipmentQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyShipmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- 粗利関連
        DailyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyWalkingAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyIncentiveAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        DailyDiscountAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        -- データセットID管理
        DataSetId NVARCHAR(50) NOT NULL,
        
        -- 制約
        CONSTRAINT PK_CpInventoryMaster PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, DataSetId)
    );
    
    -- インデックス
    CREATE INDEX IX_CpInventoryMaster_DataSetId ON CpInventoryMaster (DataSetId);
    CREATE INDEX IX_CpInventoryMaster_DailyFlag ON CpInventoryMaster (DailyFlag);
END;

-- ===================================================================
-- 3. 売上伝票テーブル
-- ===================================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SalesVoucher' AND xtype='U')
BEGIN
    CREATE TABLE SalesVoucher (
        VoucherId INT IDENTITY(1,1) NOT NULL,
        VoucherNumber NVARCHAR(20) NOT NULL,
        VoucherType NVARCHAR(10) NOT NULL,
        DetailType NVARCHAR(10) NOT NULL,
        LineNumber INT NOT NULL,
        VoucherDate DATE NOT NULL,
        JobDate DATE NOT NULL,
        
        -- 在庫キー
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ManualShippingMark NVARCHAR(50) NOT NULL,
        
        -- 取引先情報
        CustomerCode NVARCHAR(20) NOT NULL,
        CustomerName NVARCHAR(100) NOT NULL,
        TransactionType NVARCHAR(20) NOT NULL,
        
        -- 売上情報
        Quantity DECIMAL(18,4) NOT NULL,
        UnitPrice DECIMAL(18,4) NOT NULL,
        Amount DECIMAL(18,4) NOT NULL,
        InventoryUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
        
        DataSetId NVARCHAR(50) NOT NULL DEFAULT '',
        
        CONSTRAINT PK_SalesVoucher PRIMARY KEY (VoucherId)
    );
    
    -- インデックス
    CREATE INDEX IX_SalesVoucher_JobDate ON SalesVoucher (JobDate);
    CREATE INDEX IX_SalesVoucher_VoucherType ON SalesVoucher (VoucherType);
    CREATE INDEX IX_SalesVoucher_Key ON SalesVoucher (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
END;

-- ===================================================================
-- 4. 仕入伝票テーブル
-- ===================================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PurchaseVoucher' AND xtype='U')
BEGIN
    CREATE TABLE PurchaseVoucher (
        VoucherId INT IDENTITY(1,1) NOT NULL,
        VoucherNumber NVARCHAR(20) NOT NULL,
        VoucherType NVARCHAR(10) NOT NULL,
        DetailType NVARCHAR(10) NOT NULL,
        LineNumber INT NOT NULL,
        VoucherDate DATE NOT NULL,
        JobDate DATE NOT NULL,
        
        -- 在庫キー
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ManualShippingMark NVARCHAR(50) NOT NULL,
        
        -- 取引先情報
        SupplierCode NVARCHAR(20) NOT NULL,
        SupplierName NVARCHAR(100) NOT NULL,
        TransactionType NVARCHAR(20) NOT NULL,
        
        -- 仕入情報
        Quantity DECIMAL(18,4) NOT NULL,
        UnitPrice DECIMAL(18,4) NOT NULL,
        Amount DECIMAL(18,4) NOT NULL,
        
        DataSetId NVARCHAR(50) NOT NULL DEFAULT '',
        
        CONSTRAINT PK_PurchaseVoucher PRIMARY KEY (VoucherId)
    );
    
    -- インデックス
    CREATE INDEX IX_PurchaseVoucher_JobDate ON PurchaseVoucher (JobDate);
    CREATE INDEX IX_PurchaseVoucher_VoucherType ON PurchaseVoucher (VoucherType);
    CREATE INDEX IX_PurchaseVoucher_Key ON PurchaseVoucher (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
END;

-- ===================================================================
-- 5. 在庫調整テーブル
-- ===================================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='InventoryAdjustment' AND xtype='U')
BEGIN
    CREATE TABLE InventoryAdjustment (
        AdjustmentId INT IDENTITY(1,1) NOT NULL,
        VoucherNumber NVARCHAR(20) NOT NULL,
        VoucherType NVARCHAR(10) NOT NULL,
        DetailType NVARCHAR(10) NOT NULL,
        LineNumber INT NOT NULL,
        VoucherDate DATE NOT NULL,
        JobDate DATE NOT NULL,
        
        -- 在庫キー
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ManualShippingMark NVARCHAR(50) NOT NULL,
        
        -- 調整情報
        Quantity DECIMAL(18,4) NOT NULL,
        UnitPrice DECIMAL(18,4) NOT NULL,
        Amount DECIMAL(18,4) NOT NULL,
        UnitCode NVARCHAR(10) NOT NULL,         -- 単位コード
        ReasonCode NVARCHAR(10) NOT NULL,       -- 理由コード
        CategoryCode INT NULL,                   -- 区分コード
        CustomerCode NVARCHAR(20) NULL,          -- 得意先コード
        CustomerName NVARCHAR(100) NULL,         -- 得意先名
        
        DataSetId NVARCHAR(50) NOT NULL DEFAULT '',
        
        CONSTRAINT PK_InventoryAdjustment PRIMARY KEY (AdjustmentId)
    );
    
    -- インデックス
    CREATE INDEX IX_InventoryAdjustment_JobDate ON InventoryAdjustment (JobDate);
    CREATE INDEX IX_InventoryAdjustment_VoucherType ON InventoryAdjustment (VoucherType);
    CREATE INDEX IX_InventoryAdjustment_Key ON InventoryAdjustment (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
END;

-- ===================================================================
-- 6. 処理履歴テーブル
-- ===================================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProcessingHistory' AND xtype='U')
BEGIN
    CREATE TABLE ProcessingHistory (
        HistoryId INT IDENTITY(1,1) NOT NULL,
        ProcessType NVARCHAR(50) NOT NULL,      -- 処理種別
        JobDate DATE NOT NULL,                  -- ジョブ日付
        DataSetId NVARCHAR(50) NOT NULL,        -- データセットID
        Status NVARCHAR(20) NOT NULL,           -- ステータス
        StartTime DATETIME2 NOT NULL,           -- 開始時刻
        EndTime DATETIME2 NULL,                 -- 終了時刻
        ProcessingTime INT NULL,                -- 処理時間（秒）
        RecordCount INT NULL,                   -- 処理件数
        ErrorMessage NVARCHAR(MAX) NULL,        -- エラーメッセージ
        CreatedBy NVARCHAR(50) NOT NULL,        -- 実行者
        
        CONSTRAINT PK_ProcessingHistory PRIMARY KEY (HistoryId)
    );
    
    -- インデックス
    CREATE INDEX IX_ProcessingHistory_JobDate ON ProcessingHistory (JobDate);
    CREATE INDEX IX_ProcessingHistory_ProcessType ON ProcessingHistory (ProcessType);
    CREATE INDEX IX_ProcessingHistory_DataSetId ON ProcessingHistory (DataSetId);
END;

PRINT 'データベーススキーマの作成が完了しました。';