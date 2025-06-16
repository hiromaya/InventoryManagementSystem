-- ==================================================
-- 在庫管理システム データベーススキーマ
-- ==================================================

USE master;
GO

-- データベース作成
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'InventoryManagementDB')
BEGIN
    CREATE DATABASE InventoryManagementDB;
END
GO

USE InventoryManagementDB;
GO

-- ==================================================
-- 1. CP在庫マスタ（InventoryMaster）
-- ==================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='InventoryMaster' AND xtype='U')
BEGIN
    CREATE TABLE InventoryMaster (
        -- 複合キー（5項目）
        ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
        GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
        ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
        ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
        ShippingMarkName NVARCHAR(50) NOT NULL,     -- 荷印名
        
        -- 基本情報
        ProductName NVARCHAR(100) NOT NULL,         -- 商品名
        Unit NVARCHAR(10) NOT NULL,                 -- 単位
        StandardPrice DECIMAL(18,4) NOT NULL DEFAULT 0, -- 標準単価
        ProductCategory1 NVARCHAR(10) NOT NULL DEFAULT '', -- 商品分類1
        ProductCategory2 NVARCHAR(10) NOT NULL DEFAULT '', -- 商品分類2
        
        -- 日付管理（汎用日付2＝ジョブデート必須）
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 作成日
        UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 更新日
        
        -- 在庫情報
        CurrentStock DECIMAL(18,4) NOT NULL DEFAULT 0,     -- 現在在庫数
        CurrentStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0, -- 現在在庫金額
        DailyStock DECIMAL(18,4) NOT NULL DEFAULT 0,       -- 当日在庫数
        DailyStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,  -- 当日在庫金額
        
        -- 当日発生フラグ ('0':データあり, '9':クリア状態)
        DailyFlag CHAR(1) NOT NULL DEFAULT '9',
        
        -- 粗利情報
        DailyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,      -- 当日粗利益
        DailyAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0, -- 当日在庫調整金額
        DailyProcessingCost DECIMAL(18,4) NOT NULL DEFAULT 0,   -- 当日加工費
        FinalGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,      -- 最終粗利益
        
        -- データセットID管理
        DataSetId NVARCHAR(50) NOT NULL DEFAULT '',
        
        -- 複合主キー
        CONSTRAINT PK_InventoryMaster PRIMARY KEY (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate
        )
    );
END
GO

-- ==================================================
-- 2. 売上伝票（SalesVoucher）
-- ==================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SalesVoucher' AND xtype='U')
BEGIN
    CREATE TABLE SalesVoucher (
        VoucherId INT NOT NULL,                     -- 伝票ID
        LineNumber INT NOT NULL,                    -- 行番号
        VoucherDate DATE NOT NULL,                  -- 伝票日付
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        
        -- 在庫キー
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ShippingMarkName NVARCHAR(50) NOT NULL,
        
        -- 売上情報
        Quantity DECIMAL(18,4) NOT NULL DEFAULT 0,         -- 数量
        SalesUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,   -- 売上単価
        SalesAmount DECIMAL(18,4) NOT NULL DEFAULT 0,      -- 売上金額
        InventoryUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0, -- 在庫単価
        
        -- データセットID
        DataSetId NVARCHAR(50) NOT NULL DEFAULT '',
        
        CONSTRAINT PK_SalesVoucher PRIMARY KEY (VoucherId, LineNumber)
    );
END
GO

-- ==================================================
-- 3. 仕入伝票（PurchaseVoucher）
-- ==================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PurchaseVoucher' AND xtype='U')
BEGIN
    CREATE TABLE PurchaseVoucher (
        VoucherId INT NOT NULL,                     -- 伝票ID
        LineNumber INT NOT NULL,                    -- 行番号
        VoucherDate DATE NOT NULL,                  -- 伝票日付
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        
        -- 在庫キー
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ShippingMarkName NVARCHAR(50) NOT NULL,
        
        -- 仕入情報
        Quantity DECIMAL(18,4) NOT NULL DEFAULT 0,           -- 数量
        PurchaseUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0,  -- 仕入単価
        PurchaseAmount DECIMAL(18,4) NOT NULL DEFAULT 0,     -- 仕入金額
        
        -- データセットID
        DataSetId NVARCHAR(50) NOT NULL DEFAULT '',
        
        CONSTRAINT PK_PurchaseVoucher PRIMARY KEY (VoucherId, LineNumber)
    );
END
GO

-- ==================================================
-- 4. 処理履歴（ProcessingHistory）
-- ==================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProcessingHistory' AND xtype='U')
BEGIN
    CREATE TABLE ProcessingHistory (
        Id INT IDENTITY(1,1) NOT NULL,              -- ID
        DataSetId NVARCHAR(50) NOT NULL,            -- データセットID
        ProcessType NVARCHAR(50) NOT NULL,          -- 処理タイプ
        JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
        ProcessedAt DATETIME2 NOT NULL DEFAULT GETDATE(), -- 処理日時
        ProcessedBy NVARCHAR(50) NOT NULL DEFAULT SYSTEM_USER, -- 処理者
        Status NVARCHAR(20) NOT NULL DEFAULT 'SUCCESS', -- ステータス
        ErrorMessage NVARCHAR(MAX) NULL,            -- エラーメッセージ
        ProcessedRecords INT NOT NULL DEFAULT 0,    -- 処理件数
        Note NVARCHAR(500) NULL,                    -- 備考
        
        CONSTRAINT PK_ProcessingHistory PRIMARY KEY (Id)
    );
END
GO