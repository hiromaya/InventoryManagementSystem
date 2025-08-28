-- =============================================
-- CategoryCodeをINTからNVARCHAR(3)に変換
-- 全ての分類マスタテーブルを3桁0埋め文字列に統一
-- =============================================

USE InventoryManagementDB;
GO

PRINT '=== Migration 066: CategoryCodeをNVARCHAR(3)に変換開始 ===';
GO

-- ===== ProductCategory1Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'ProductCategory1Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- ProductCategory1Master変換開始 ---';
    
    -- 新しいカラムを追加
    ALTER TABLE ProductCategory1Master ADD CategoryCodeNew NVARCHAR(3);
    
    -- データを3桁0埋めで変換
    UPDATE ProductCategory1Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    -- 古いカラムを削除
    ALTER TABLE ProductCategory1Master DROP COLUMN CategoryCode;
    
    -- カラム名を変更
    EXEC sp_rename 'ProductCategory1Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ ProductCategory1Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ ProductCategory1Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== ProductCategory2Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'ProductCategory2Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- ProductCategory2Master変換開始 ---';
    
    ALTER TABLE ProductCategory2Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE ProductCategory2Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE ProductCategory2Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'ProductCategory2Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ ProductCategory2Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ ProductCategory2Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== ProductCategory3Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'ProductCategory3Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- ProductCategory3Master変換開始 ---';
    
    ALTER TABLE ProductCategory3Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE ProductCategory3Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE ProductCategory3Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'ProductCategory3Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ ProductCategory3Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ ProductCategory3Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== CustomerCategory1Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory1Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- CustomerCategory1Master変換開始 ---';
    
    ALTER TABLE CustomerCategory1Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE CustomerCategory1Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE CustomerCategory1Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'CustomerCategory1Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ CustomerCategory1Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ CustomerCategory1Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== CustomerCategory2Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory2Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- CustomerCategory2Master変換開始 ---';
    
    ALTER TABLE CustomerCategory2Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE CustomerCategory2Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE CustomerCategory2Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'CustomerCategory2Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ CustomerCategory2Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ CustomerCategory2Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== CustomerCategory3Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory3Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- CustomerCategory3Master変換開始 ---';
    
    ALTER TABLE CustomerCategory3Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE CustomerCategory3Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE CustomerCategory3Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'CustomerCategory3Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ CustomerCategory3Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ CustomerCategory3Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== CustomerCategory4Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory4Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- CustomerCategory4Master変換開始 ---';
    
    ALTER TABLE CustomerCategory4Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE CustomerCategory4Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE CustomerCategory4Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'CustomerCategory4Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ CustomerCategory4Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ CustomerCategory4Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== CustomerCategory5Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory5Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- CustomerCategory5Master変換開始 ---';
    
    ALTER TABLE CustomerCategory5Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE CustomerCategory5Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE CustomerCategory5Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'CustomerCategory5Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ CustomerCategory5Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ CustomerCategory5Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== SupplierCategory1Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'SupplierCategory1Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- SupplierCategory1Master変換開始 ---';
    
    ALTER TABLE SupplierCategory1Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE SupplierCategory1Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE SupplierCategory1Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'SupplierCategory1Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ SupplierCategory1Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ SupplierCategory1Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== SupplierCategory2Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'SupplierCategory2Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- SupplierCategory2Master変換開始 ---';
    
    ALTER TABLE SupplierCategory2Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE SupplierCategory2Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE SupplierCategory2Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'SupplierCategory2Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ SupplierCategory2Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ SupplierCategory2Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== SupplierCategory3Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'SupplierCategory3Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- SupplierCategory3Master変換開始 ---';
    
    ALTER TABLE SupplierCategory3Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE SupplierCategory3Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE SupplierCategory3Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'SupplierCategory3Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ SupplierCategory3Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ SupplierCategory3Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- ===== StaffCategory1Master =====
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'StaffCategory1Master' 
           AND COLUMN_NAME = 'CategoryCode'
           AND DATA_TYPE = 'int')
BEGIN
    PRINT '--- StaffCategory1Master変換開始 ---';
    
    ALTER TABLE StaffCategory1Master ADD CategoryCodeNew NVARCHAR(3);
    
    UPDATE StaffCategory1Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    
    ALTER TABLE StaffCategory1Master DROP COLUMN CategoryCode;
    
    EXEC sp_rename 'StaffCategory1Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    
    PRINT '✅ StaffCategory1Master.CategoryCodeをNVARCHAR(3)に変換完了';
END
ELSE
BEGIN
    PRINT '⚠️ StaffCategory1Master.CategoryCodeは既にNVARCHAR型です';
END
GO

-- 最終確認
PRINT '';
PRINT '=== Migration 066 最終確認 ===';

-- 全テーブルの型を確認
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME = 'CategoryCode'
  AND TABLE_NAME LIKE '%Category%Master'
ORDER BY TABLE_NAME;

PRINT '';
PRINT '✅ Migration 066完了！全ての分類マスタのCategoryCodeをNVARCHAR(3)に変換しました';
GO