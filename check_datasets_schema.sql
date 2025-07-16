-- DataSetsテーブルのスキーマを確認するSQLクエリ
USE InventoryManagementDB;
GO

-- 1. DataSetsテーブルの存在確認
PRINT '=== DataSetsテーブルの存在確認 ===';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DataSets')
    PRINT '✓ DataSetsテーブルが存在します';
ELSE
    PRINT '✗ DataSetsテーブルが存在しません';

-- 2. DataSetsテーブルの列構造確認
PRINT '';
PRINT '=== DataSetsテーブルの列構造 ===';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DataSets')
BEGIN
    SELECT 
        COLUMN_NAME as [カラム名],
        DATA_TYPE as [データ型],
        CHARACTER_MAXIMUM_LENGTH as [最大長],
        IS_NULLABLE as [NULL許可],
        COLUMN_DEFAULT as [デフォルト値]
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DataSets'
    ORDER BY ORDINAL_POSITION;
END

-- 3. DataSetManagementテーブルとの比較
PRINT '';
PRINT '=== DataSetManagementテーブルとの比較 ===';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DataSetManagement')
BEGIN
    PRINT '✓ DataSetManagementテーブルが存在します';
    
    -- DataSetManagementテーブルの列構造
    PRINT '';
    PRINT '--- DataSetManagementテーブルの列構造 ---';
    SELECT 
        COLUMN_NAME as [カラム名],
        DATA_TYPE as [データ型],
        CHARACTER_MAXIMUM_LENGTH as [最大長],
        IS_NULLABLE as [NULL許可],
        COLUMN_DEFAULT as [デフォルト値]
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DataSetManagement'
    ORDER BY ORDINAL_POSITION;
END
ELSE
    PRINT '✗ DataSetManagementテーブルが存在しません';

-- 4. 外部キー制約の確認
PRINT '';
PRINT '=== 外部キー制約の確認 ===';
SELECT 
    fk.name AS [制約名],
    t.name AS [テーブル名],
    c.name AS [カラム名],
    rt.name AS [参照テーブル名],
    rc.name AS [参照カラム名]
FROM sys.foreign_keys fk
INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
WHERE rt.name = 'DataSets' OR rt.name = 'DataSetManagement'
ORDER BY fk.name;

-- 5. レコード数の確認
PRINT '';
PRINT '=== レコード数の確認 ===';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DataSets')
BEGIN
    DECLARE @DataSetsCount INT;
    SELECT @DataSetsCount = COUNT(*) FROM DataSets;
    PRINT 'DataSets: ' + CAST(@DataSetsCount AS VARCHAR(10)) + '件';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DataSetManagement')
BEGIN
    DECLARE @DataSetManagementCount INT;
    SELECT @DataSetManagementCount = COUNT(*) FROM DataSetManagement;
    PRINT 'DataSetManagement: ' + CAST(@DataSetManagementCount AS VARCHAR(10)) + '件';
END

PRINT '';
PRINT '=== 調査完了 ===';