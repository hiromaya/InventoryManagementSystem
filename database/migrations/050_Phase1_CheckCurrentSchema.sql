-- =====================================================
-- フェーズ1: 現在のテーブル構造確認スクリプト
-- 実行日: 2025-07-18
-- 目的: 移行前の現在のスキーマ状態を確認
-- =====================================================

USE InventoryManagementDB;
GO

PRINT '================================';
PRINT 'フェーズ1: 現在のテーブル構造確認';
PRINT '================================';

-- 1. 対象テーブルの存在確認
PRINT '1. テーブル存在確認';
SELECT 
    TABLE_NAME,
    CASE WHEN TABLE_NAME IS NOT NULL THEN '存在' ELSE '未作成' END AS STATUS
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
ORDER BY TABLE_NAME;

-- 2. 各テーブルのカラム構造確認
PRINT '';
PRINT '2. ProductMaster カラム構造';
IF OBJECT_ID('ProductMaster', 'U') IS NOT NULL
BEGIN
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        IS_NULLABLE,
        COLUMN_DEFAULT,
        CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ProductMaster'
    ORDER BY ORDINAL_POSITION;
    
    SELECT COUNT(*) AS ProductMaster_RecordCount FROM ProductMaster;
END
ELSE
BEGIN
    PRINT 'ProductMaster テーブルが存在しません';
END

PRINT '';
PRINT '3. CustomerMaster カラム構造';
IF OBJECT_ID('CustomerMaster', 'U') IS NOT NULL
BEGIN
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        IS_NULLABLE,
        COLUMN_DEFAULT,
        CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CustomerMaster'
    ORDER BY ORDINAL_POSITION;
    
    SELECT COUNT(*) AS CustomerMaster_RecordCount FROM CustomerMaster;
END
ELSE
BEGIN
    PRINT 'CustomerMaster テーブルが存在しません';
END

PRINT '';
PRINT '4. SupplierMaster カラム構造';
IF OBJECT_ID('SupplierMaster', 'U') IS NOT NULL
BEGIN
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        IS_NULLABLE,
        COLUMN_DEFAULT,
        CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'SupplierMaster'
    ORDER BY ORDINAL_POSITION;
    
    SELECT COUNT(*) AS SupplierMaster_RecordCount FROM SupplierMaster;
END
ELSE
BEGIN
    PRINT 'SupplierMaster テーブルが存在しません';
END

-- 3. 日付カラムの特定確認
PRINT '';
PRINT '5. 日付関連カラムの確認';
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
AND (COLUMN_NAME LIKE '%Created%' OR COLUMN_NAME LIKE '%Updated%' OR COLUMN_NAME LIKE '%Date%')
ORDER BY TABLE_NAME, COLUMN_NAME;

-- 4. インデックス情報の確認
PRINT '';
PRINT '6. インデックス情報';
SELECT 
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    c.name AS ColumnName
FROM sys.tables t
INNER JOIN sys.indexes i ON t.object_id = i.object_id
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE t.name IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
AND i.type > 0  -- インデックスのみ（ヒープは除外）
ORDER BY t.name, i.name, ic.key_ordinal;

-- 5. 外部キー制約の確認
PRINT '';
PRINT '7. 外部キー制約の確認';
SELECT 
    fk.name AS ForeignKeyName,
    tp.name AS ParentTable,
    cp.name AS ParentColumn,
    tr.name AS ReferencedTable,
    cr.name AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tp.name IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
   OR tr.name IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
ORDER BY tp.name, fk.name;

-- 6. 依存関係の確認（Geminiの提案）
PRINT '';
PRINT '8. テーブル依存関係の確認';
SELECT DISTINCT
    referencing_obj.name AS referencing_entity_name,
    referencing_obj.type_desc AS referencing_entity_type,
    referenced_obj.name AS referenced_entity_name
FROM sys.sql_expression_dependencies AS sed
INNER JOIN sys.objects AS referencing_obj ON sed.referencing_id = referencing_obj.object_id
INNER JOIN sys.objects AS referenced_obj ON sed.referenced_id = referenced_obj.object_id
WHERE referenced_obj.name IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
ORDER BY referenced_obj.name, referencing_obj.name;

PRINT '';
PRINT '================================';
PRINT 'フェーズ1確認完了';
PRINT '================================';
GO