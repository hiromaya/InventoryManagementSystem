-- 実際のデータベーススキーマ確認用SQL
-- Geminiの指示に基づいて作成

USE InventoryManagementDB;

-- 1. ProductMasterテーブルのカラム構造確認
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ProductMaster'
    AND TABLE_SCHEMA = 'dbo'
    AND COLUMN_NAME IN ('CreatedAt', 'UpdatedAt', 'CreatedDate', 'UpdatedDate')
ORDER BY ORDINAL_POSITION;

-- 2. CustomerMasterテーブルのカラム構造確認
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'CustomerMaster'
    AND TABLE_SCHEMA = 'dbo'
    AND COLUMN_NAME IN ('CreatedAt', 'UpdatedAt', 'CreatedDate', 'UpdatedDate')
ORDER BY ORDINAL_POSITION;

-- 3. SupplierMasterテーブルのカラム構造確認
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'SupplierMaster'
    AND TABLE_SCHEMA = 'dbo'
    AND COLUMN_NAME IN ('CreatedAt', 'UpdatedAt', 'CreatedDate', 'UpdatedDate')
ORDER BY ORDINAL_POSITION;

-- 4. 全マスタテーブルの日付カラム一覧
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster', 'GradeMaster', 'ClassMaster', 'ShippingMarkMaster', 'OriginMaster', 'UnitMaster')
    AND COLUMN_NAME LIKE '%Created%' OR COLUMN_NAME LIKE '%Updated%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- 5. テーブル存在確認
SELECT 
    TABLE_NAME,
    TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
ORDER BY TABLE_NAME;

PRINT '=== 実際のデータベーススキーマ確認完了 ===';