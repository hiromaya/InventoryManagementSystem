-- =====================================================
-- スクリプト名: AddMissingColumnsToInventoryMaster.sql
-- 説明: InventoryMasterテーブルに不足しているカラムを追加
-- 作成日: 2025-01-30
-- 注意: このスクリプトは冪等性を持ち、複数回実行しても安全です
-- =====================================================

USE InventoryManagementDB;
GO

-- ProductNameカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'ProductName'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD ProductName NVARCHAR(100) NOT NULL DEFAULT N'商品名未設定';
    
    PRINT 'ProductNameカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'ProductNameカラムは既に存在します。';
END
GO

-- DailyGrossProfitカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'DailyGrossProfit'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD DailyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0;
    
    PRINT 'DailyGrossProfitカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'DailyGrossProfitカラムは既に存在します。';
END
GO

-- DailyAdjustmentAmountカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'DailyAdjustmentAmount'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD DailyAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
    
    PRINT 'DailyAdjustmentAmountカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'DailyAdjustmentAmountカラムは既に存在します。';
END
GO

-- DailyProcessingCostカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'DailyProcessingCost'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD DailyProcessingCost DECIMAL(18,4) NOT NULL DEFAULT 0;
    
    PRINT 'DailyProcessingCostカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'DailyProcessingCostカラムは既に存在します。';
END
GO

-- FinalGrossProfitカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'FinalGrossProfit'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD FinalGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0;
    
    PRINT 'FinalGrossProfitカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'FinalGrossProfitカラムは既に存在します。';
END
GO

-- DataSetIdカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'DataSetId'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD DataSetId NVARCHAR(50) NOT NULL DEFAULT '';
    
    PRINT 'DataSetIdカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'DataSetIdカラムは既に存在します。';
END
GO

-- PreviousMonthQuantityカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'PreviousMonthQuantity'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD PreviousMonthQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
    
    PRINT 'PreviousMonthQuantityカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'PreviousMonthQuantityカラムは既に存在します。';
END
GO

-- PreviousMonthAmountカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'PreviousMonthAmount'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD PreviousMonthAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
    
    PRINT 'PreviousMonthAmountカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'PreviousMonthAmountカラムは既に存在します。';
END
GO

-- 既存データの更新（ProductNameがNULLまたは空の場合）
UPDATE InventoryMaster
SET ProductName = N'商' + ProductCode
WHERE ProductName IS NULL OR ProductName = '';
GO

-- 更新結果の確認
PRINT '===== InventoryMasterテーブルの構造 =====';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventoryMaster'
ORDER BY ORDINAL_POSITION;
GO

PRINT '';
PRINT '===== スクリプト実行完了 =====';
PRINT '全てのカラムが正常に確認/追加されました。';