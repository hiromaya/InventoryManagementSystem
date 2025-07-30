-- 003_AddProductNameColumn.sql
-- 伝票テーブルにProductNameカラムを追加するマイグレーション
-- 作成日: 2025-07-30

PRINT '003_AddProductNameColumn: 開始';

-- ===================================================
-- ProductNameカラムの存在確認（念のため）
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

-- ===================================================
-- 商品名の更新（ProductMasterが存在する場合のみ）
-- ===================================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductMaster]'))
BEGIN
    PRINT '商品マスタから商品名を更新中...';
    
    -- SalesVouchers
    UPDATE s
    SET s.ProductName = p.ProductName
    FROM SalesVouchers s
    INNER JOIN ProductMaster p ON s.ProductCode = p.ProductCode
    WHERE s.ProductName IS NULL OR s.ProductName = '';
    
    -- PurchaseVouchers
    UPDATE pv
    SET pv.ProductName = p.ProductName
    FROM PurchaseVouchers pv
    INNER JOIN ProductMaster p ON pv.ProductCode = p.ProductCode
    WHERE pv.ProductName IS NULL OR pv.ProductName = '';
    
    -- InventoryAdjustments
    UPDATE ia
    SET ia.ProductName = p.ProductName
    FROM InventoryAdjustments ia
    INNER JOIN ProductMaster p ON ia.ProductCode = p.ProductCode
    WHERE ia.ProductName IS NULL OR ia.ProductName = '';
    
    PRINT '✓ 商品名を更新しました';
END
ELSE
BEGIN
    PRINT '⚠️ ProductMasterテーブルが存在しないため、商品名の更新はスキップされました';
    PRINT '   商品マスタのインポート後に商品名が設定されます';
END

PRINT '003_AddProductNameColumn: 完了';