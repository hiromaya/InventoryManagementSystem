-- =============================================
-- 初期在庫ステージングテーブル作成
-- 作成日: 2025-07-13
-- 説明: ZAIK*.csvファイルの一時取り込み用テーブル
-- =============================================

USE InventoryManagementDB;
GO

-- 既存のテーブルがある場合は削除
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InitialInventory_Staging')
BEGIN
    DROP TABLE InitialInventory_Staging;
END
GO

-- ステージングテーブル作成
CREATE TABLE InitialInventory_Staging (
    -- 自動採番ID
    StagingId INT IDENTITY(1,1) NOT NULL,
    
    -- CSVデータ列
    ProductCode NVARCHAR(15) NOT NULL,
    GradeCode NVARCHAR(15) NOT NULL,
    ClassCode NVARCHAR(15) NOT NULL,
    ShippingMarkCode NVARCHAR(15) NOT NULL,
    ManualShippingMark NVARCHAR(50) NOT NULL,
    PersonInChargeCode INT NOT NULL,
    PreviousStockQuantity DECIMAL(18,4) NOT NULL,
    PreviousStockAmount DECIMAL(18,4) NOT NULL,
    CurrentStockQuantity DECIMAL(18,4) NOT NULL,
    StandardPrice DECIMAL(18,4) NOT NULL,
    CurrentStockAmount DECIMAL(18,4) NOT NULL,
    AveragePrice DECIMAL(18,4) NOT NULL,
    
    -- 処理管理列
    ProcessId NVARCHAR(50) NOT NULL,          -- バッチ処理ID（DataSetId）
    ProcessDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    ProcessStatus NVARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING/PROCESSED/ERROR
    ErrorMessage NVARCHAR(MAX) NULL,
    
    -- 制約
    CONSTRAINT PK_InitialInventory_Staging PRIMARY KEY (StagingId)
);
GO

-- インデックス作成
CREATE INDEX IX_InitialInventory_Staging_ProcessId 
ON InitialInventory_Staging (ProcessId, ProcessStatus);

CREATE INDEX IX_InitialInventory_Staging_Keys
ON InitialInventory_Staging (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
GO

-- エラーログテーブル作成
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InitialInventory_ErrorLog')
BEGIN
    DROP TABLE InitialInventory_ErrorLog;
END
GO

CREATE TABLE InitialInventory_ErrorLog (
    ErrorId INT IDENTITY(1,1) NOT NULL,
    ProcessId NVARCHAR(50) NOT NULL,
    StagingId INT NULL,
    ProductCode NVARCHAR(15) NULL,
    GradeCode NVARCHAR(15) NULL,
    ClassCode NVARCHAR(15) NULL,
    ShippingMarkCode NVARCHAR(15) NULL,
    ManualShippingMark NVARCHAR(50) NULL,
    ErrorType NVARCHAR(50) NOT NULL,
    ErrorMessage NVARCHAR(MAX) NOT NULL,
    ErrorDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    CONSTRAINT PK_InitialInventory_ErrorLog PRIMARY KEY (ErrorId)
);
GO

CREATE INDEX IX_InitialInventory_ErrorLog_ProcessId 
ON InitialInventory_ErrorLog (ProcessId);
GO

PRINT '初期在庫ステージングテーブルを作成しました。';