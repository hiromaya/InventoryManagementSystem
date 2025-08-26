-- =============================================
-- 伝票テーブルインデックス再作成
-- 作成日: 2025-08-26
-- 目的: ManualShippingMark変更に対応したインデックス最適化
-- =============================================

USE InventoryManagementDB;
GO

PRINT '伝票テーブルインデックス再作成を開始...';

-- ===== SalesVouchers インデックス =====

-- 既存インデックス削除（ManualShippingMarkを含むもの）
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesVouchers_JobDate_VoucherType' AND object_id = OBJECT_ID('SalesVouchers'))
BEGIN
    DROP INDEX IX_SalesVouchers_JobDate_VoucherType ON SalesVouchers;
    PRINT 'SalesVouchers既存インデックス削除完了';
END

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesVouchers_InventoryKey' AND object_id = OBJECT_ID('SalesVouchers'))
BEGIN
    DROP INDEX IX_SalesVouchers_InventoryKey ON SalesVouchers;
    PRINT 'SalesVouchers在庫キーインデックス削除完了';
END

-- 新規インデックス作成
CREATE INDEX IX_SalesVouchers_JobDate_VoucherType 
ON SalesVouchers(JobDate, VoucherType, DetailType) 
INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, Quantity, UnitPrice, Amount);

CREATE INDEX IX_SalesVouchers_InventoryKey 
ON SalesVouchers(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark) 
INCLUDE (JobDate, VoucherType, DetailType, Quantity, UnitPrice);

PRINT 'SalesVouchersインデックス再作成完了';

-- ===== PurchaseVouchers インデックス =====

-- 既存インデックス削除
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseVouchers_JobDate_VoucherType' AND object_id = OBJECT_ID('PurchaseVouchers'))
BEGIN
    DROP INDEX IX_PurchaseVouchers_JobDate_VoucherType ON PurchaseVouchers;
    PRINT 'PurchaseVouchers既存インデックス削除完了';
END

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseVouchers_InventoryKey' AND object_id = OBJECT_ID('PurchaseVouchers'))
BEGIN
    DROP INDEX IX_PurchaseVouchers_InventoryKey ON PurchaseVouchers;
    PRINT 'PurchaseVouchers在庫キーインデックス削除完了';
END

-- 新規インデックス作成
CREATE INDEX IX_PurchaseVouchers_JobDate_VoucherType 
ON PurchaseVouchers(JobDate, VoucherType, DetailType) 
INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, Quantity, UnitPrice, Amount);

CREATE INDEX IX_PurchaseVouchers_InventoryKey 
ON PurchaseVouchers(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark) 
INCLUDE (JobDate, VoucherType, DetailType, Quantity, UnitPrice);

PRINT 'PurchaseVouchersインデックス再作成完了';

-- ===== InventoryAdjustments インデックス =====

-- 既存インデックス削除
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryAdjustments_JobDate_CategoryCode' AND object_id = OBJECT_ID('InventoryAdjustments'))
BEGIN
    DROP INDEX IX_InventoryAdjustments_JobDate_CategoryCode ON InventoryAdjustments;
    PRINT 'InventoryAdjustments既存インデックス削除完了';
END

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryAdjustments_InventoryKey' AND object_id = OBJECT_ID('InventoryAdjustments'))
BEGIN
    DROP INDEX IX_InventoryAdjustments_InventoryKey ON InventoryAdjustments;
    PRINT 'InventoryAdjustments在庫キーインデックス削除完了';
END

-- 新規インデックス作成
CREATE INDEX IX_InventoryAdjustments_JobDate_CategoryCode 
ON InventoryAdjustments(JobDate, VoucherType, DetailType, CategoryCode) 
INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, Quantity, UnitPrice, Amount);

CREATE INDEX IX_InventoryAdjustments_InventoryKey 
ON InventoryAdjustments(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark) 
INCLUDE (JobDate, VoucherType, DetailType, CategoryCode, Quantity, UnitPrice);

PRINT 'InventoryAdjustmentsインデックス再作成完了';

-- インデックス作成確認
PRINT '=== インデックス作成確認 ===';
SELECT 
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name IN ('SalesVouchers', 'PurchaseVouchers', 'InventoryAdjustments')
    AND i.name LIKE 'IX_%'
ORDER BY t.name, i.name;

PRINT '伝票テーブルインデックス再作成が完了しました';
GO