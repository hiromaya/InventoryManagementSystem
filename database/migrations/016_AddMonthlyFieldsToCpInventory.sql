-- CP在庫マスタに月計フィールドを追加
-- 実行日: 2025-07-05
-- 説明: 商品日報の月計表示機能のために月初から当日までの累計フィールドを追加

USE InventoryManagementDB;
GO

-- テーブルの存在確認
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CpInventoryMaster')
BEGIN
    PRINT 'エラー: CpInventoryMasterテーブルが存在しません。';
    RETURN;
END

-- 月計フィールドの追加（存在チェック付き）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlySalesQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlySalesQuantity DECIMAL(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlySalesAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlySalesAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlySalesReturnQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlySalesReturnQuantity DECIMAL(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlySalesReturnAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlySalesReturnAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyPurchaseQuantity')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseQuantity DECIMAL(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyPurchaseAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyGrossProfit')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyGrossProfit DECIMAL(18,2) NOT NULL DEFAULT 0;
END

-- 追加のフィールド（015では定義されていない）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyWalkingAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyWalkingAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'MonthlyIncentiveAmount')
BEGIN
    ALTER TABLE CpInventoryMaster ADD MonthlyIncentiveAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
END

-- 追加されたカラムの確認
PRINT '';
PRINT '===== CpInventoryMasterテーブルの月計カラム一覧 =====';
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
PRINT 'Migration 016: 月計フィールドの追加処理が完了しました。';
GO