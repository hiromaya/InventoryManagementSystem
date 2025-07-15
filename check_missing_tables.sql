-- ===================================================
-- 不足しているテーブルの確認スクリプト
-- ===================================================

USE InventoryManagementDB;
GO

-- 1. 主要テーブルの存在確認
PRINT '===== 主要テーブルの存在確認 ====='
SELECT 
    TABLE_NAME,
    CASE 
        WHEN OBJECT_ID('dbo.' + TABLE_NAME) IS NOT NULL THEN '✅ 存在'
        ELSE '❌ 存在しない'
    END AS Status
FROM (VALUES 
    ('ProductMaster'),
    ('CustomerMaster'),
    ('SupplierMaster'),
    ('GradeMaster'),
    ('ClassMaster'),
    ('ShippingMarkMaster'),
    ('RegionMaster'),
    ('InventoryMaster'),
    ('CpInventoryMaster'),
    ('SalesVouchers'),
    ('PurchaseVouchers'),
    ('InventoryAdjustments'),
    ('DataSets'),
    ('DataSetManagement'),
    ('InitialInventoryStaging')
) AS Tables(TABLE_NAME);
GO

-- 2. 作成済みテーブルの詳細確認
PRINT '===== 作成済みテーブルの詳細 ====='
SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length,
    c.is_nullable
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.system_type_id = ty.system_type_id
WHERE t.name IN ('InventoryMaster', 'CpInventoryMaster', 'SalesVouchers', 'PurchaseVouchers', 'InventoryAdjustments')
ORDER BY t.name, c.column_id;
GO

-- 3. 必要なマスタテーブルの一覧
PRINT '===== 必要なマスタテーブルの一覧 ====='
PRINT 'ProductMaster - 商品マスタ'
PRINT 'CustomerMaster - 得意先マスタ'  
PRINT 'SupplierMaster - 仕入先マスタ'
PRINT 'GradeMaster - 等級マスタ'
PRINT 'ClassMaster - 階級マスタ'
PRINT 'ShippingMarkMaster - 荷印マスタ'
PRINT 'RegionMaster - 産地マスタ'
GO

-- 4. 初期在庫インポートに必要なテーブル
PRINT '===== 初期在庫インポートに必要なテーブル ====='
SELECT 
    'ProductMaster' AS TableName,
    '商品コード → 商品名の変換に必要' AS Purpose,
    CASE 
        WHEN OBJECT_ID('dbo.ProductMaster') IS NOT NULL THEN '✅ 存在'
        ELSE '❌ 存在しない - 作成が必要'
    END AS Status
UNION ALL
SELECT 
    'InitialInventoryStaging' AS TableName,
    '初期在庫一時保存テーブル' AS Purpose,
    CASE 
        WHEN OBJECT_ID('dbo.InitialInventoryStaging') IS NOT NULL THEN '✅ 存在'
        ELSE '❌ 存在しない'
    END AS Status;
GO

PRINT '===== 確認完了 ====='