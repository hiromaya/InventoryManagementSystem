-- InventoryMasterテーブルに月計カラムを追加
USE InventoryManagementDB;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'MonthlySalesAmount')
BEGIN
    ALTER TABLE InventoryMaster ADD MonthlySalesAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'MonthlySalesAmountカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'MonthlySalesAmountカラムは既に存在します。';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'MonthlySalesReturnAmount')
BEGIN
    ALTER TABLE InventoryMaster ADD MonthlySalesReturnAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'MonthlySalesReturnAmountカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'MonthlySalesReturnAmountカラムは既に存在します。';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'MonthlyGrossProfit1')
BEGIN
    ALTER TABLE InventoryMaster ADD MonthlyGrossProfit1 DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'MonthlyGrossProfit1カラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'MonthlyGrossProfit1カラムは既に存在します。';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'MonthlyGrossProfit2')
BEGIN
    ALTER TABLE InventoryMaster ADD MonthlyGrossProfit2 DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'MonthlyGrossProfit2カラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'MonthlyGrossProfit2カラムは既に存在します。';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'MonthlyWalkingAmount')
BEGIN
    ALTER TABLE InventoryMaster ADD MonthlyWalkingAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'MonthlyWalkingAmountカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'MonthlyWalkingAmountカラムは既に存在します。';
END
GO

PRINT '';
PRINT '===== InventoryMasterテーブルの月計カラム確認 =====';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventoryMaster'
  AND COLUMN_NAME IN (
        'MonthlySalesAmount',
        'MonthlySalesReturnAmount',
        'MonthlyGrossProfit1',
        'MonthlyGrossProfit2',
        'MonthlyWalkingAmount')
ORDER BY ORDINAL_POSITION;
GO

PRINT '';
PRINT 'InventoryMasterテーブルの月計カラム追加処理が完了しました。';
GO
