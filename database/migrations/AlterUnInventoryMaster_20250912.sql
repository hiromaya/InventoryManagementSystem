-- =============================================
-- AlterUnInventoryMaster_20250912_Final.sql
-- 目的: UnInventoryMaster テーブルの修正
-- 注意: DataSetIdカラムは存在しないため、Primary Keyに含めない
-- =============================================

USE InventoryManagementDB;
GO

-- 現在の構造を表示
-- 現在の構造を表示
PRINT '===== 修正前のUnInventoryMasterテーブル構造 =====';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    CASE 
        WHEN t.name IN ('nvarchar', 'nchar') THEN c.max_length / 2
        ELSE c.max_length 
    END AS Length,
    c.is_nullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('UnInventoryMaster')
    AND c.name IN ('JobDate', 'ProductCode', 'GradeCode', 'ClassCode', 
                   'ShippingMarkCode', 'ManualShippingMark')
ORDER BY c.column_id;
GO

-- 1. 既存のPrimary Key制約を削除（存在する場合）
IF EXISTS (
    SELECT * FROM sys.key_constraints 
    WHERE parent_object_id = OBJECT_ID('UnInventoryMaster') 
      AND type = 'PK')
BEGIN
    DECLARE @PKName NVARCHAR(256);
    SELECT @PKName = name 
    FROM sys.key_constraints 
    WHERE parent_object_id = OBJECT_ID('UnInventoryMaster') 
      AND type = 'PK';
    
    EXEC('ALTER TABLE UnInventoryMaster DROP CONSTRAINT ' + @PKName);
    PRINT 'Primary Key制約 ' + @PKName + ' を削除しました。';
END
GO

-- 2. JobDateに関連するインデックスを削除
IF EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'IX_UnInventoryMaster_JobDate')
BEGIN
    DROP INDEX IX_UnInventoryMaster_JobDate ON UnInventoryMaster;
    PRINT 'IX_UnInventoryMaster_JobDate インデックスを削除しました。';
END
GO

-- その他のJobDate関連インデックスも確認・削除
DECLARE @IndexName NVARCHAR(256);
DECLARE index_cursor CURSOR FOR 
    SELECT DISTINCT i.name
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    WHERE i.object_id = OBJECT_ID('UnInventoryMaster')
      AND c.name = 'JobDate'
      AND i.type > 0  -- heap以外
      AND i.is_primary_key = 0;  -- Primary Key以外

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @IndexName;

WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC('DROP INDEX ' + @IndexName + ' ON UnInventoryMaster');
    PRINT 'インデックス ' + @IndexName + ' を削除しました。';
    FETCH NEXT FROM index_cursor INTO @IndexName;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;
GO

-- 3. JobDateをNOT NULLに変更
IF EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'JobDate' 
      AND is_nullable = 1)
BEGIN
    -- NULL値を処理（デフォルト値を設定）
    UPDATE UnInventoryMaster 
    SET JobDate = CAST(GETDATE() AS DATE) 
    WHERE JobDate IS NULL;
    
    -- NOT NULLに変更
    ALTER TABLE UnInventoryMaster ALTER COLUMN JobDate DATE NOT NULL;
    PRINT 'JobDate を NOT NULL に変更しました。';
END
GO

-- 4. ShippingMarkCodeとManualShippingMarkの型確認（すでに変更済みのはず）
PRINT '';
PRINT '===== ShippingMarkCodeとManualShippingMarkの確認 =====';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    CASE 
        WHEN t.name IN ('nvarchar', 'nchar') THEN c.max_length / 2
        ELSE c.max_length 
    END AS Length,
    c.is_nullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('UnInventoryMaster')
    AND c.name IN ('ShippingMarkCode', 'ManualShippingMark');
GO

-- 5. Primary Key制約を作成（DataSetIdなしバージョン）
IF NOT EXISTS (
    SELECT * FROM sys.key_constraints 
    WHERE parent_object_id = OBJECT_ID('UnInventoryMaster') 
      AND type = 'PK')
BEGIN
    ALTER TABLE UnInventoryMaster
    ADD CONSTRAINT PK_UnInventoryMaster PRIMARY KEY CLUSTERED (
        JobDate,
        ProductCode,
        GradeCode,
        ClassCode,
        ShippingMarkCode,
        ManualShippingMark
    );
    PRINT 'PK_UnInventoryMaster を作成しました（DataSetIdなし）。';
END
GO

-- 6. パフォーマンス用インデックスの作成
-- JobDateインデックス
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'IX_UnInventoryMaster_JobDate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UnInventoryMaster_JobDate
    ON UnInventoryMaster (JobDate)
    INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
    PRINT 'IX_UnInventoryMaster_JobDate インデックスを作成しました。';
END
GO

-- 商品検索用インデックス
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'IX_UnInventoryMaster_Product')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UnInventoryMaster_Product
    ON UnInventoryMaster (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
    PRINT 'IX_UnInventoryMaster_Product インデックスを作成しました。';
END
GO

-- 7. 最終的な構造を確認
PRINT '';
PRINT '===== 修正後のUnInventoryMasterテーブル構造 =====';
SELECT 
    c.column_id,
    c.name AS ColumnName,
    t.name AS DataType,
    CASE 
        WHEN t.name IN ('nvarchar', 'nchar') THEN c.max_length / 2
        ELSE c.max_length 
    END AS Length,
    c.is_nullable,
    CASE WHEN pk.column_id IS NOT NULL THEN 'PK' ELSE '' END AS PrimaryKey
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN (
    SELECT ic.column_id
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    WHERE i.object_id = OBJECT_ID('UnInventoryMaster') AND i.is_primary_key = 1
) pk ON c.column_id = pk.column_id
WHERE c.object_id = OBJECT_ID('UnInventoryMaster')
ORDER BY c.column_id;
GO

-- 8. Primary Key制約の詳細を表示
PRINT '';
PRINT '===== Primary Key制約の詳細 =====';
SELECT 
    kc.name AS ConstraintName,
    STRING_AGG(COL_NAME(ic.object_id, ic.column_id), ', ') 
        WITHIN GROUP (ORDER BY ic.key_ordinal) AS PrimaryKeyColumns
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic 
    ON kc.parent_object_id = ic.object_id 
    AND kc.unique_index_id = ic.index_id
WHERE kc.parent_object_id = OBJECT_ID('UnInventoryMaster') 
    AND kc.type = 'PK'
GROUP BY kc.name;
GO
<<<<<<< HEAD

-- 9. すべてのインデックスを表示
PRINT '';
PRINT '===== すべてのインデックス =====';
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_primary_key AS IsPK,
    STRING_AGG(COL_NAME(ic.object_id, ic.column_id), ', ') 
        WITHIN GROUP (ORDER BY ic.key_ordinal) AS IndexColumns
FROM sys.indexes i
INNER JOIN sys.index_columns ic 
    ON i.object_id = ic.object_id 
    AND i.index_id = ic.index_id
WHERE i.object_id = OBJECT_ID('UnInventoryMaster')
    AND i.type > 0
    AND ic.is_included_column = 0
GROUP BY i.name, i.type_desc, i.is_primary_key
ORDER BY i.is_primary_key DESC, i.name;
GO

PRINT '';
PRINT 'AlterUnInventoryMaster_20250912_Final.sql 実行完了';
PRINT '================================================';
GO
=======
>>>>>>> a24d722 (fix(migration): avoid PK dependency error by skipping ALTER COLUMN in AlterUnInventoryMaster_20250912 (sizes handled by 100_Fix schema); keep only additive column changes)
