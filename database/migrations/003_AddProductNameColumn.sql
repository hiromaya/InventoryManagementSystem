-- 003_AddProductNameColumn.sql
-- 伝票テーブルにProductNameカラムを追加するマイグレーション
-- 作成日: 2025-07-30

PRINT '003_AddProductNameColumn: 伝票テーブルへのProductName追加開始';

-- ===================================================
-- 1. SalesVouchersテーブル
-- ===================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
    AND name = 'ProductName'
)
BEGIN
    ALTER TABLE [dbo].[SalesVouchers]
    ADD [ProductName] NVARCHAR(100) NULL;
    PRINT '✓ SalesVouchersテーブルにProductNameカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- SalesVouchersテーブルにはProductNameカラムが既に存在します';
END

-- ===================================================
-- 2. PurchaseVouchersテーブル
-- ===================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVouchers]') 
    AND name = 'ProductName'
)
BEGIN
    ALTER TABLE [dbo].[PurchaseVouchers]
    ADD [ProductName] NVARCHAR(100) NULL;
    PRINT '✓ PurchaseVouchersテーブルにProductNameカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- PurchaseVouchersテーブルにはProductNameカラムが既に存在します';
END

-- ===================================================
-- 3. InventoryAdjustmentsテーブル
-- ===================================================
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') 
    AND name = 'ProductName'
)
BEGIN
    ALTER TABLE [dbo].[InventoryAdjustments]
    ADD [ProductName] NVARCHAR(100) NULL;
    PRINT '✓ InventoryAdjustmentsテーブルにProductNameカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- InventoryAdjustmentsテーブルにはProductNameカラムが既に存在します';
END

-- ===================================================
-- 4. 既存データの商品名を商品マスタから更新
-- ===================================================
PRINT '既存データの商品名を更新中...';

-- SalesVouchers
UPDATE s
SET s.ProductName = p.ProductName
FROM SalesVouchers s
INNER JOIN ProductMaster p ON s.ProductCode = p.ProductCode
WHERE s.ProductName IS NULL OR s.ProductName = '';

PRINT '✓ SalesVouchersの商品名を更新しました';

-- PurchaseVouchers
UPDATE pv
SET pv.ProductName = p.ProductName
FROM PurchaseVouchers pv
INNER JOIN ProductMaster p ON pv.ProductCode = p.ProductCode
WHERE pv.ProductName IS NULL OR pv.ProductName = '';

PRINT '✓ PurchaseVouchersの商品名を更新しました';

-- InventoryAdjustments
UPDATE ia
SET ia.ProductName = p.ProductName
FROM InventoryAdjustments ia
INNER JOIN ProductMaster p ON ia.ProductCode = p.ProductCode
WHERE ia.ProductName IS NULL OR ia.ProductName = '';

PRINT '✓ InventoryAdjustmentsの商品名を更新しました';

PRINT '003_AddProductNameColumn: 完了';