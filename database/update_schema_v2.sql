-- InventoryMasterテーブルに不足しているカラムを追加
USE InventoryManagementDB;
GO

-- 1. DataSetIdカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') AND name = 'DataSetId')
BEGIN
    ALTER TABLE InventoryMaster ADD DataSetId NVARCHAR(50) NOT NULL DEFAULT '';
    PRINT 'DataSetIdカラムを追加しました';
END

-- 2. DailyGrossProfitカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') AND name = 'DailyGrossProfit')
BEGIN
    ALTER TABLE InventoryMaster ADD DailyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0;
    PRINT 'DailyGrossProfitカラムを追加しました';
END

-- 3. DailyAdjustmentAmountカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') AND name = 'DailyAdjustmentAmount')
BEGIN
    ALTER TABLE InventoryMaster ADD DailyAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
    PRINT 'DailyAdjustmentAmountカラムを追加しました';
END

-- 4. DailyProcessingCostカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') AND name = 'DailyProcessingCost')
BEGIN
    ALTER TABLE InventoryMaster ADD DailyProcessingCost DECIMAL(18,4) NOT NULL DEFAULT 0;
    PRINT 'DailyProcessingCostカラムを追加しました';
END

-- 5. FinalGrossProfitカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') AND name = 'FinalGrossProfit')
BEGIN
    ALTER TABLE InventoryMaster ADD FinalGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0;
    PRINT 'FinalGrossProfitカラムを追加しました';
END

-- 6. インデックスの追加
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'IX_InventoryMaster_DataSetId')
BEGIN
    CREATE INDEX IX_InventoryMaster_DataSetId ON InventoryMaster(DataSetId);
    PRINT 'DataSetIdインデックスを作成しました';
END

-- 7. InventoryAdjustmentsテーブルにCategoryCodeカラムを追加（必要な場合）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') AND name = 'CategoryCode')
BEGIN
    ALTER TABLE InventoryAdjustments ADD CategoryCode INT NULL;
    PRINT 'CategoryCodeカラムを追加しました';
END

-- 8. InventoryAdjustmentsテーブルにCustomerCode, CustomerNameカラムを追加（必要な場合）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') AND name = 'CustomerCode')
BEGIN
    ALTER TABLE InventoryAdjustments ADD CustomerCode NVARCHAR(20) NULL;
    PRINT 'CustomerCodeカラムを追加しました';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') AND name = 'CustomerName')
BEGIN
    ALTER TABLE InventoryAdjustments ADD CustomerName NVARCHAR(100) NULL;
    PRINT 'CustomerNameカラムを追加しました';
END

PRINT '';
PRINT '=== スキーマ更新完了 ===';
PRINT '更新されたテーブル: InventoryMaster, InventoryAdjustments';
GO