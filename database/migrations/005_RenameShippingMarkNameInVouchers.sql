-- =============================================
-- 伝票テーブル ManualShippingMark → ManualShippingMark 変更
-- 作成日: 2025-08-26
-- 目的: InventoryMasterとの命名統一、データ型最適化
-- =============================================

USE InventoryManagementDB;
GO

PRINT '伝票テーブルカラム名変更を開始...';

-- 事前データ長チェック
PRINT '=== データ長チェック開始 ===';

DECLARE @MaxLengthSales INT, @MaxLengthPurchase INT, @MaxLengthAdjustment INT;

SELECT @MaxLengthSales = ISNULL(MAX(LEN(ShippingMarkName)), 0) FROM SalesVouchers WHERE ShippingMarkName IS NOT NULL;
SELECT @MaxLengthPurchase = ISNULL(MAX(LEN(ShippingMarkName)), 0) FROM PurchaseVouchers WHERE ShippingMarkName IS NOT NULL;
SELECT @MaxLengthAdjustment = ISNULL(MAX(LEN(ShippingMarkName)), 0) FROM InventoryAdjustments WHERE ShippingMarkName IS NOT NULL;

PRINT 'SalesVouchers 最大荷印名長: ' + CAST(@MaxLengthSales AS NVARCHAR(10));
PRINT 'PurchaseVouchers 最大荷印名長: ' + CAST(@MaxLengthPurchase AS NVARCHAR(10));
PRINT 'InventoryAdjustments 最大荷印名長: ' + CAST(@MaxLengthAdjustment AS NVARCHAR(10));

-- データ切り捨て警告
IF @MaxLengthSales > 8 OR @MaxLengthPurchase > 8 OR @MaxLengthAdjustment > 8
BEGIN
    PRINT '警告: 8文字を超える荷印名が存在します。データ切り捨てが発生します。';
    
    -- 8文字超過データの詳細表示
    IF @MaxLengthSales > 8
    BEGIN
        PRINT 'SalesVouchers 8文字超過データ:';
        SELECT TOP 10 VoucherId, LineNumber, ShippingMarkName, LEN(ShippingMarkName) as Length
        FROM SalesVouchers 
        WHERE LEN(ShippingMarkName) > 8
        ORDER BY LEN(ShippingMarkName) DESC;
    END
    
    IF @MaxLengthPurchase > 8
    BEGIN
        PRINT 'PurchaseVouchers 8文字超過データ:';
        SELECT TOP 10 VoucherId, LineNumber, ShippingMarkName, LEN(ShippingMarkName) as Length
        FROM PurchaseVouchers 
        WHERE LEN(ShippingMarkName) > 8
        ORDER BY LEN(ShippingMarkName) DESC;
    END
    
    IF @MaxLengthAdjustment > 8
    BEGIN
        PRINT 'InventoryAdjustments 8文字超過データ:';
        SELECT TOP 10 VoucherId, LineNumber, ShippingMarkName, LEN(ShippingMarkName) as Length
        FROM InventoryAdjustments 
        WHERE LEN(ShippingMarkName) > 8
        ORDER BY LEN(ShippingMarkName) DESC;
    END
END
ELSE
BEGIN
    PRINT '確認: 全ての荷印名が8文字以内です。';
END

PRINT '=== データ長チェック終了 ===';

-- 1. SalesVouchersテーブル
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SalesVouchers') AND name = 'ShippingMarkName')
BEGIN
    PRINT '--- SalesVouchers処理開始 ---';
    
    -- バックアップテーブル作成
    IF OBJECT_ID('SalesVouchers_Backup_20250826', 'U') IS NOT NULL
        DROP TABLE SalesVouchers_Backup_20250826;
    
    SELECT * INTO SalesVouchers_Backup_20250826 FROM SalesVouchers;
    PRINT 'SalesVouchersバックアップ作成完了';
    
    -- カラム名変更
    EXEC sp_rename 'SalesVouchers.ShippingMarkName', 'ManualShippingMark', 'COLUMN';
    PRINT 'SalesVouchersカラム名変更完了';
    
    -- データ型調整（NVARCHAR(50) → NVARCHAR(8)）
    ALTER TABLE SalesVouchers 
    ALTER COLUMN ManualShippingMark NVARCHAR(8) NOT NULL;
    PRINT 'SalesVouchersデータ型変更完了';
    
    PRINT 'SalesVouchers.ShippingMarkName → ManualShippingMark 変更完了';
END
ELSE
BEGIN
    PRINT 'SalesVouchers.ShippingMarkNameは存在しません（既に変更済みまたは未作成）';
END

-- 2. PurchaseVouchersテーブル
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseVouchers') AND name = 'ShippingMarkName')
BEGIN
    PRINT '--- PurchaseVouchers処理開始 ---';
    
    -- バックアップテーブル作成
    IF OBJECT_ID('PurchaseVouchers_Backup_20250826', 'U') IS NOT NULL
        DROP TABLE PurchaseVouchers_Backup_20250826;
    
    SELECT * INTO PurchaseVouchers_Backup_20250826 FROM PurchaseVouchers;
    PRINT 'PurchaseVouchersバックアップ作成完了';
    
    -- カラム名変更
    EXEC sp_rename 'PurchaseVouchers.ShippingMarkName', 'ManualShippingMark', 'COLUMN';
    PRINT 'PurchaseVouchersカラム名変更完了';
    
    -- データ型調整
    ALTER TABLE PurchaseVouchers 
    ALTER COLUMN ManualShippingMark NVARCHAR(8) NOT NULL;
    PRINT 'PurchaseVouchersデータ型変更完了';
    
    PRINT 'PurchaseVouchers.ShippingMarkName → ManualShippingMark 変更完了';
END
ELSE
BEGIN
    PRINT 'PurchaseVouchers.ShippingMarkNameは存在しません（既に変更済みまたは未作成）';
END

-- 3. InventoryAdjustmentsテーブル
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryAdjustments') AND name = 'ShippingMarkName')
BEGIN
    PRINT '--- InventoryAdjustments処理開始 ---';
    
    -- バックアップテーブル作成
    IF OBJECT_ID('InventoryAdjustments_Backup_20250826', 'U') IS NOT NULL
        DROP TABLE InventoryAdjustments_Backup_20250826;
    
    SELECT * INTO InventoryAdjustments_Backup_20250826 FROM InventoryAdjustments;
    PRINT 'InventoryAdjustmentsバックアップ作成完了';
    
    -- カラム名変更
    EXEC sp_rename 'InventoryAdjustments.ShippingMarkName', 'ManualShippingMark', 'COLUMN';
    PRINT 'InventoryAdjustmentsカラム名変更完了';
    
    -- データ型調整
    ALTER TABLE InventoryAdjustments 
    ALTER COLUMN ManualShippingMark NVARCHAR(8) NOT NULL;
    PRINT 'InventoryAdjustmentsデータ型変更完了';
    
    PRINT 'InventoryAdjustments.ShippingMarkName → ManualShippingMark 変更完了';
END
ELSE
BEGIN
    PRINT 'InventoryAdjustments.ShippingMarkNameは存在しません（既に変更済みまたは未作成）';
END

-- 変更完了確認
PRINT '=== 変更完了確認 ===';
SELECT 
    'SalesVouchers' as TableName,
    CASE WHEN EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SalesVouchers') AND name = 'ManualShippingMark') 
         THEN 'ManualShippingMark存在' 
         ELSE 'ManualShippingMark未存在' END as Status
UNION ALL
SELECT 
    'PurchaseVouchers',
    CASE WHEN EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseVouchers') AND name = 'ManualShippingMark') 
         THEN 'ManualShippingMark存在' 
         ELSE 'ManualShippingMark未存在' END
UNION ALL
SELECT 
    'InventoryAdjustments',
    CASE WHEN EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryAdjustments') AND name = 'ManualShippingMark') 
         THEN 'ManualShippingMark存在' 
         ELSE 'ManualShippingMark未存在' END;

PRINT '伝票テーブルカラム名変更が完了しました';
GO