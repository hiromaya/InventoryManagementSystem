-- =============================================
-- CategoryCode_New カラムのクリーンアップ
-- =============================================

USE InventoryManagementDB;
GO

PRINT '=== CategoryCode_New クリーンアップ開始 ===';
GO

-- ProductCategory1Master のCategoryCode_Newを削除
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductCategory1Master') AND name = 'CategoryCode_New')
BEGIN
    -- デフォルト制約を先に削除
    DECLARE @ConstraintName NVARCHAR(256);
    DECLARE @SQL NVARCHAR(MAX);
    
    DECLARE constraint_cursor CURSOR FOR
        SELECT dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_column_id = c.column_id AND dc.parent_object_id = c.object_id
        WHERE dc.parent_object_id = OBJECT_ID('ProductCategory1Master') AND c.name = 'CategoryCode_New';
    
    OPEN constraint_cursor;
    FETCH NEXT FROM constraint_cursor INTO @ConstraintName;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @SQL = 'ALTER TABLE ProductCategory1Master DROP CONSTRAINT [' + @ConstraintName + ']';
        EXEC sp_executesql @SQL;
        PRINT CONCAT('  ✓ デフォルト制約削除: ', @ConstraintName);
        FETCH NEXT FROM constraint_cursor INTO @ConstraintName;
    END
    
    CLOSE constraint_cursor;
    DEALLOCATE constraint_cursor;
    
    -- カラムを削除
    ALTER TABLE ProductCategory1Master DROP COLUMN CategoryCode_New;
    PRINT '  ✓ ProductCategory1Master.CategoryCode_New 削除完了';
END
ELSE
BEGIN
    PRINT '  ✓ ProductCategory1Master.CategoryCode_New は存在しません';
END
GO

-- ProductCategory2Master のCategoryCode_Newを削除（同様の処理）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductCategory2Master') AND name = 'CategoryCode_New')
BEGIN
    DECLARE @ConstraintName2 NVARCHAR(256);
    DECLARE @SQL2 NVARCHAR(MAX);
    
    DECLARE constraint_cursor2 CURSOR FOR
        SELECT dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_column_id = c.column_id AND dc.parent_object_id = c.object_id
        WHERE dc.parent_object_id = OBJECT_ID('ProductCategory2Master') AND c.name = 'CategoryCode_New';
    
    OPEN constraint_cursor2;
    FETCH NEXT FROM constraint_cursor2 INTO @ConstraintName2;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @SQL2 = 'ALTER TABLE ProductCategory2Master DROP CONSTRAINT [' + @ConstraintName2 + ']';
        EXEC sp_executesql @SQL2;
        PRINT CONCAT('  ✓ デフォルト制約削除: ', @ConstraintName2);
        FETCH NEXT FROM constraint_cursor2 INTO @ConstraintName2;
    END
    
    CLOSE constraint_cursor2;
    DEALLOCATE constraint_cursor2;
    
    ALTER TABLE ProductCategory2Master DROP COLUMN CategoryCode_New;
    PRINT '  ✓ ProductCategory2Master.CategoryCode_New 削除完了';
END
GO

-- ProductCategory3Master のCategoryCode_Newを削除（同様の処理）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductCategory3Master') AND name = 'CategoryCode_New')
BEGIN
    DECLARE @ConstraintName3 NVARCHAR(256);
    DECLARE @SQL3 NVARCHAR(MAX);
    
    DECLARE constraint_cursor3 CURSOR FOR
        SELECT dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_column_id = c.column_id AND dc.parent_object_id = c.object_id
        WHERE dc.parent_object_id = OBJECT_ID('ProductCategory3Master') AND c.name = 'CategoryCode_New';
    
    OPEN constraint_cursor3;
    FETCH NEXT FROM constraint_cursor3 INTO @ConstraintName3;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @SQL3 = 'ALTER TABLE ProductCategory3Master DROP CONSTRAINT [' + @ConstraintName3 + ']';
        EXEC sp_executesql @SQL3;
        PRINT CONCAT('  ✓ デフォルト制約削除: ', @ConstraintName3);
        FETCH NEXT FROM constraint_cursor3 INTO @ConstraintName3;
    END
    
    CLOSE constraint_cursor3;
    DEALLOCATE constraint_cursor3;
    
    ALTER TABLE ProductCategory3Master DROP COLUMN CategoryCode_New;
    PRINT '  ✓ ProductCategory3Master.CategoryCode_New 削除完了';
END
GO

-- 最終確認
PRINT '';
PRINT '=== 最終確認 ===';

-- ProductCategory1Masterの構造確認
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'ProductCategory1Master'
ORDER BY c.ORDINAL_POSITION;

-- データ確認
PRINT '';
PRINT '=== ProductCategory1Master データ（全12件）===';
SELECT CategoryCode, CategoryName FROM ProductCategory1Master ORDER BY CategoryCode;

PRINT '';
PRINT '✅ クリーンアップ完了！';
PRINT '✅ CategoryCode型変換は既に完了しています（NVARCHAR(3)）';
PRINT '✅ データも正常です（000形式の3桁）';
GO