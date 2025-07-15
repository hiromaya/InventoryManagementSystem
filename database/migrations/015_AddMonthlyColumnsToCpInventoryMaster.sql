-- CpInventoryMasterテーブルに月計カラムを追加
USE InventoryManagementDB;
GO

-- 月計売上関連
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlySalesQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlySalesQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlySalesAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlySalesAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlySalesReturnQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlySalesReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlySalesReturnAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlySalesReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
END

-- 月計仕入関連
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyPurchaseQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyPurchaseAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyPurchaseReturnQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyPurchaseReturnAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
END

-- 月計在庫調整関連
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyInventoryAdjustmentQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyInventoryAdjustmentQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyInventoryAdjustmentAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyInventoryAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
END

-- 月計加工・振替関連
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyProcessingQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyProcessingQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyProcessingAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyProcessingAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyTransferQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyTransferQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyTransferAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyTransferAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
END

-- 月計粗利益
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyGrossProfit')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0;
END

-- 追加されたカラムの状況を表示
PRINT '';
PRINT '===== CpInventoryMasterテーブルの月計カラム確認 =====';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'CpInventoryMaster'
  AND COLUMN_NAME LIKE 'Monthly%'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'CpInventoryMasterテーブルに月計カラムの追加処理が完了しました。';
GO