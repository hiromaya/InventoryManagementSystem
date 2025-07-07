-- ====================================================================
-- GrossProfitカラム追加スクリプト
-- 作成日: 2025-07-07
-- 用途: 商品日報の粗利計算機能対応
-- ====================================================================

USE InventoryManagementDB;
GO

-- SalesVouchersテーブルにGrossProfitカラムを追加
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') 
    AND name = 'GrossProfit'
)
BEGIN
    ALTER TABLE [dbo].[SalesVouchers]
    ADD [GrossProfit] DECIMAL(16,4) NULL;
    
    PRINT 'SalesVouchersテーブルにGrossProfitカラムを追加しました。'
END
ELSE
BEGIN
    PRINT 'GrossProfitカラムは既に存在します。'
END
GO

-- PurchaseVouchersテーブルにもGrossProfitカラムを追加（将来の拡張用）
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVouchers]') 
    AND name = 'GrossProfit'
)
BEGIN
    ALTER TABLE [dbo].[PurchaseVouchers]
    ADD [GrossProfit] DECIMAL(16,4) NULL;
    
    PRINT 'PurchaseVouchersテーブルにGrossProfitカラムを追加しました。'
END
GO

-- InventoryAdjustmentsテーブルにもGrossProfitカラムを追加（将来の拡張用）
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') 
    AND name = 'GrossProfit'
)
BEGIN
    ALTER TABLE [dbo].[InventoryAdjustments]
    ADD [GrossProfit] DECIMAL(16,4) NULL;
    
    PRINT 'InventoryAdjustmentsテーブルにGrossProfitカラムを追加しました。'
END
GO

-- 変更確認クエリ
PRINT '';
PRINT '===================================================================';
PRINT 'カラム追加結果の確認:';
PRINT '===================================================================';

SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.precision,
    c.scale,
    c.is_nullable
FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE t.name IN ('SalesVouchers', 'PurchaseVouchers', 'InventoryAdjustments')
    AND c.name = 'GrossProfit'
ORDER BY t.name;