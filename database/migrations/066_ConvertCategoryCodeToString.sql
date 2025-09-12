-- =============================================
-- CategoryCodeをINTからNVARCHAR(3)に変換
-- 全ての分類マスタテーブルを3桁0埋め文字列に統一
-- 修正版：NOT NULL設定を追加
-- =============================================

USE InventoryManagementDB;
GO

PRINT '=== Migration 066: CategoryCodeをNVARCHAR(3)に変換開始 ===';
PRINT '';
GO

-- ========== Step 1: 全テーブルにCategoryCodeNewカラムを追加（既に完了済みならスキップ） ==========
PRINT '--- Step 1: CategoryCodeNewカラムを追加 ---';

-- ProductCategory1Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'ProductCategory1Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE ProductCategory1Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ ProductCategory1Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - ProductCategory1Master.CategoryCodeNew既存';
GO

-- ProductCategory2Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'ProductCategory2Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE ProductCategory2Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ ProductCategory2Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - ProductCategory2Master.CategoryCodeNew既存';
GO

-- ProductCategory3Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'ProductCategory3Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE ProductCategory3Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ ProductCategory3Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - ProductCategory3Master.CategoryCodeNew既存';
GO

-- CustomerCategory1Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'CustomerCategory1Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory1Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ CustomerCategory1Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - CustomerCategory1Master.CategoryCodeNew既存';
GO

-- CustomerCategory2Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'CustomerCategory2Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory2Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ CustomerCategory2Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - CustomerCategory2Master.CategoryCodeNew既存';
GO

-- CustomerCategory3Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'CustomerCategory3Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory3Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ CustomerCategory3Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - CustomerCategory3Master.CategoryCodeNew既存';
GO

-- CustomerCategory4Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'CustomerCategory4Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory4Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ CustomerCategory4Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - CustomerCategory4Master.CategoryCodeNew既存';
GO

-- CustomerCategory5Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'CustomerCategory5Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory5Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ CustomerCategory5Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - CustomerCategory5Master.CategoryCodeNew既存';
GO

-- SupplierCategory1Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'SupplierCategory1Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE SupplierCategory1Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ SupplierCategory1Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - SupplierCategory1Master.CategoryCodeNew既存';
GO

-- SupplierCategory2Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'SupplierCategory2Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE SupplierCategory2Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ SupplierCategory2Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - SupplierCategory2Master.CategoryCodeNew既存';
GO

-- SupplierCategory3Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'SupplierCategory3Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE SupplierCategory3Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ SupplierCategory3Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - SupplierCategory3Master.CategoryCodeNew既存';
GO

-- StaffCategory1Master
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'StaffCategory1Master' 
               AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE StaffCategory1Master ADD CategoryCodeNew NVARCHAR(3);
    PRINT '  ✓ StaffCategory1Master.CategoryCodeNew追加';
END
ELSE
    PRINT '  - StaffCategory1Master.CategoryCodeNew既存';
GO

-- ========== Step 2: データを3桁0埋めで変換（既に完了済みならスキップ） ==========
PRINT '';
PRINT '--- Step 2: データを3桁0埋めで変換 ---';
GO

-- ProductCategory1Master
IF EXISTS (SELECT * FROM ProductCategory1Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE ProductCategory1Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ ProductCategory1Master データ変換完了';
END
ELSE
    PRINT '  - ProductCategory1Master データ変換済み';
GO

-- ProductCategory2Master
IF EXISTS (SELECT * FROM ProductCategory2Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE ProductCategory2Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ ProductCategory2Master データ変換完了';
END
ELSE
    PRINT '  - ProductCategory2Master データ変換済み';
GO

-- ProductCategory3Master
IF EXISTS (SELECT * FROM ProductCategory3Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE ProductCategory3Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ ProductCategory3Master データ変換完了';
END
ELSE
    PRINT '  - ProductCategory3Master データ変換済み';
GO

-- CustomerCategory1Master
IF EXISTS (SELECT * FROM CustomerCategory1Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE CustomerCategory1Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ CustomerCategory1Master データ変換完了';
END
ELSE
    PRINT '  - CustomerCategory1Master データ変換済み';
GO

-- CustomerCategory2Master
IF EXISTS (SELECT * FROM CustomerCategory2Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE CustomerCategory2Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ CustomerCategory2Master データ変換完了';
END
ELSE
    PRINT '  - CustomerCategory2Master データ変換済み';
GO

-- CustomerCategory3Master
IF EXISTS (SELECT * FROM CustomerCategory3Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE CustomerCategory3Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ CustomerCategory3Master データ変換完了';
END
ELSE
    PRINT '  - CustomerCategory3Master データ変換済み';
GO

-- CustomerCategory4Master
IF EXISTS (SELECT * FROM CustomerCategory4Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE CustomerCategory4Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ CustomerCategory4Master データ変換完了';
END
ELSE
    PRINT '  - CustomerCategory4Master データ変換済み';
GO

-- CustomerCategory5Master
IF EXISTS (SELECT * FROM CustomerCategory5Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE CustomerCategory5Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ CustomerCategory5Master データ変換完了';
END
ELSE
    PRINT '  - CustomerCategory5Master データ変換済み';
GO

-- SupplierCategory1Master
IF EXISTS (SELECT * FROM SupplierCategory1Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE SupplierCategory1Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ SupplierCategory1Master データ変換完了';
END
ELSE
    PRINT '  - SupplierCategory1Master データ変換済み';
GO

-- SupplierCategory2Master
IF EXISTS (SELECT * FROM SupplierCategory2Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE SupplierCategory2Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ SupplierCategory2Master データ変換完了';
END
ELSE
    PRINT '  - SupplierCategory2Master データ変換済み';
GO

-- SupplierCategory3Master
IF EXISTS (SELECT * FROM SupplierCategory3Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE SupplierCategory3Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ SupplierCategory3Master データ変換完了';
END
ELSE
    PRINT '  - SupplierCategory3Master データ変換済み';
GO

-- StaffCategory1Master
IF EXISTS (SELECT * FROM StaffCategory1Master WHERE CategoryCodeNew IS NULL)
BEGIN
    UPDATE StaffCategory1Master 
    SET CategoryCodeNew = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
    PRINT '  ✓ StaffCategory1Master データ変換完了';
END
ELSE
    PRINT '  - StaffCategory1Master データ変換済み';
GO

-- ========== Step 3: プライマリキー制約を削除 ==========
PRINT '';
PRINT '--- Step 3: プライマリキー制約を削除 ---';
GO

-- ProductCategory1Master
DECLARE @PKName1 NVARCHAR(200)
SELECT @PKName1 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('ProductCategory1Master') AND type = 'PK'
IF @PKName1 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE ProductCategory1Master DROP CONSTRAINT ' + @PKName1)
    PRINT '  ✓ ProductCategory1Master PK削除: ' + @PKName1
END
GO

-- ProductCategory2Master
DECLARE @PKName2 NVARCHAR(200)
SELECT @PKName2 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('ProductCategory2Master') AND type = 'PK'
IF @PKName2 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE ProductCategory2Master DROP CONSTRAINT ' + @PKName2)
    PRINT '  ✓ ProductCategory2Master PK削除: ' + @PKName2
END
GO

-- ProductCategory3Master
DECLARE @PKName3 NVARCHAR(200)
SELECT @PKName3 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('ProductCategory3Master') AND type = 'PK'
IF @PKName3 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE ProductCategory3Master DROP CONSTRAINT ' + @PKName3)
    PRINT '  ✓ ProductCategory3Master PK削除: ' + @PKName3
END
GO

-- CustomerCategory1Master
DECLARE @PKName4 NVARCHAR(200)
SELECT @PKName4 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('CustomerCategory1Master') AND type = 'PK'
IF @PKName4 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE CustomerCategory1Master DROP CONSTRAINT ' + @PKName4)
    PRINT '  ✓ CustomerCategory1Master PK削除: ' + @PKName4
END
GO

-- CustomerCategory2Master
DECLARE @PKName5 NVARCHAR(200)
SELECT @PKName5 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('CustomerCategory2Master') AND type = 'PK'
IF @PKName5 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE CustomerCategory2Master DROP CONSTRAINT ' + @PKName5)
    PRINT '  ✓ CustomerCategory2Master PK削除: ' + @PKName5
END
GO

-- CustomerCategory3Master
DECLARE @PKName6 NVARCHAR(200)
SELECT @PKName6 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('CustomerCategory3Master') AND type = 'PK'
IF @PKName6 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE CustomerCategory3Master DROP CONSTRAINT ' + @PKName6)
    PRINT '  ✓ CustomerCategory3Master PK削除: ' + @PKName6
END
GO

-- CustomerCategory4Master
DECLARE @PKName7 NVARCHAR(200)
SELECT @PKName7 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('CustomerCategory4Master') AND type = 'PK'
IF @PKName7 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE CustomerCategory4Master DROP CONSTRAINT ' + @PKName7)
    PRINT '  ✓ CustomerCategory4Master PK削除: ' + @PKName7
END
GO

-- CustomerCategory5Master
DECLARE @PKName8 NVARCHAR(200)
SELECT @PKName8 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('CustomerCategory5Master') AND type = 'PK'
IF @PKName8 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE CustomerCategory5Master DROP CONSTRAINT ' + @PKName8)
    PRINT '  ✓ CustomerCategory5Master PK削除: ' + @PKName8
END
GO

-- SupplierCategory1Master
DECLARE @PKName9 NVARCHAR(200)
SELECT @PKName9 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('SupplierCategory1Master') AND type = 'PK'
IF @PKName9 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE SupplierCategory1Master DROP CONSTRAINT ' + @PKName9)
    PRINT '  ✓ SupplierCategory1Master PK削除: ' + @PKName9
END
GO

-- SupplierCategory2Master
DECLARE @PKName10 NVARCHAR(200)
SELECT @PKName10 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('SupplierCategory2Master') AND type = 'PK'
IF @PKName10 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE SupplierCategory2Master DROP CONSTRAINT ' + @PKName10)
    PRINT '  ✓ SupplierCategory2Master PK削除: ' + @PKName10
END
GO

-- SupplierCategory3Master
DECLARE @PKName11 NVARCHAR(200)
SELECT @PKName11 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('SupplierCategory3Master') AND type = 'PK'
IF @PKName11 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE SupplierCategory3Master DROP CONSTRAINT ' + @PKName11)
    PRINT '  ✓ SupplierCategory3Master PK削除: ' + @PKName11
END
GO

-- StaffCategory1Master
DECLARE @PKName12 NVARCHAR(200)
SELECT @PKName12 = name FROM sys.key_constraints 
WHERE parent_object_id = OBJECT_ID('StaffCategory1Master') AND type = 'PK'
IF @PKName12 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE StaffCategory1Master DROP CONSTRAINT ' + @PKName12)
    PRINT '  ✓ StaffCategory1Master PK削除: ' + @PKName12
END
GO

-- ========== Step 4: 古いカラムを削除してリネーム ==========
PRINT '';
PRINT '--- Step 4: カラムの削除とリネーム ---';
GO

-- ProductCategory1Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'ProductCategory1Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE ProductCategory1Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'ProductCategory1Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ ProductCategory1Master 完了';
END
GO

-- ProductCategory2Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'ProductCategory2Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE ProductCategory2Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'ProductCategory2Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ ProductCategory2Master 完了';
END
GO

-- ProductCategory3Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'ProductCategory3Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE ProductCategory3Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'ProductCategory3Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ ProductCategory3Master 完了';
END
GO

-- CustomerCategory1Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory1Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory1Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'CustomerCategory1Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ CustomerCategory1Master 完了';
END
GO

-- CustomerCategory2Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory2Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory2Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'CustomerCategory2Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ CustomerCategory2Master 完了';
END
GO

-- CustomerCategory3Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory3Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory3Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'CustomerCategory3Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ CustomerCategory3Master 完了';
END
GO

-- CustomerCategory4Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory4Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory4Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'CustomerCategory4Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ CustomerCategory4Master 完了';
END
GO

-- CustomerCategory5Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'CustomerCategory5Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE CustomerCategory5Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'CustomerCategory5Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ CustomerCategory5Master 完了';
END
GO

-- SupplierCategory1Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'SupplierCategory1Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE SupplierCategory1Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'SupplierCategory1Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ SupplierCategory1Master 完了';
END
GO

-- SupplierCategory2Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'SupplierCategory2Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE SupplierCategory2Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'SupplierCategory2Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ SupplierCategory2Master 完了';
END
GO

-- SupplierCategory3Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'SupplierCategory3Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE SupplierCategory3Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'SupplierCategory3Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ SupplierCategory3Master 完了';
END
GO

-- StaffCategory1Master
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'StaffCategory1Master' 
           AND COLUMN_NAME = 'CategoryCodeNew')
BEGIN
    ALTER TABLE StaffCategory1Master DROP COLUMN CategoryCode;
    EXEC sp_rename 'StaffCategory1Master.CategoryCodeNew', 'CategoryCode', 'COLUMN';
    PRINT '  ✓ StaffCategory1Master 完了';
END
GO

-- ========== Step 4.5: CategoryCodeをNOT NULLに変更（重要！） ==========
PRINT '';
PRINT '--- Step 4.5: CategoryCodeをNOT NULLに変更 ---';
GO

-- ProductCategory1Master
ALTER TABLE ProductCategory1Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ ProductCategory1Master.CategoryCode NOT NULL設定';
GO

-- ProductCategory2Master
ALTER TABLE ProductCategory2Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ ProductCategory2Master.CategoryCode NOT NULL設定';
GO

-- ProductCategory3Master
ALTER TABLE ProductCategory3Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ ProductCategory3Master.CategoryCode NOT NULL設定';
GO

-- CustomerCategory1Master
ALTER TABLE CustomerCategory1Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ CustomerCategory1Master.CategoryCode NOT NULL設定';
GO

-- CustomerCategory2Master
ALTER TABLE CustomerCategory2Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ CustomerCategory2Master.CategoryCode NOT NULL設定';
GO

-- CustomerCategory3Master
ALTER TABLE CustomerCategory3Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ CustomerCategory3Master.CategoryCode NOT NULL設定';
GO

-- CustomerCategory4Master
ALTER TABLE CustomerCategory4Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ CustomerCategory4Master.CategoryCode NOT NULL設定';
GO

-- CustomerCategory5Master
ALTER TABLE CustomerCategory5Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ CustomerCategory5Master.CategoryCode NOT NULL設定';
GO

-- SupplierCategory1Master
ALTER TABLE SupplierCategory1Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ SupplierCategory1Master.CategoryCode NOT NULL設定';
GO

-- SupplierCategory2Master
ALTER TABLE SupplierCategory2Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ SupplierCategory2Master.CategoryCode NOT NULL設定';
GO

-- SupplierCategory3Master
ALTER TABLE SupplierCategory3Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ SupplierCategory3Master.CategoryCode NOT NULL設定';
GO

-- StaffCategory1Master
ALTER TABLE StaffCategory1Master ALTER COLUMN CategoryCode NVARCHAR(3) NOT NULL;
PRINT '  ✓ StaffCategory1Master.CategoryCode NOT NULL設定';
GO

-- ========== Step 5: プライマリキーを再作成 ==========
PRINT '';
PRINT '--- Step 5: プライマリキーを再作成 ---';
GO

ALTER TABLE ProductCategory1Master ADD CONSTRAINT PK_ProductCategory1Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ ProductCategory1Master PK再作成';
GO

ALTER TABLE ProductCategory2Master ADD CONSTRAINT PK_ProductCategory2Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ ProductCategory2Master PK再作成';
GO

ALTER TABLE ProductCategory3Master ADD CONSTRAINT PK_ProductCategory3Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ ProductCategory3Master PK再作成';
GO

ALTER TABLE CustomerCategory1Master ADD CONSTRAINT PK_CustomerCategory1Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ CustomerCategory1Master PK再作成';
GO

ALTER TABLE CustomerCategory2Master ADD CONSTRAINT PK_CustomerCategory2Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ CustomerCategory2Master PK再作成';
GO

ALTER TABLE CustomerCategory3Master ADD CONSTRAINT PK_CustomerCategory3Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ CustomerCategory3Master PK再作成';
GO

ALTER TABLE CustomerCategory4Master ADD CONSTRAINT PK_CustomerCategory4Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ CustomerCategory4Master PK再作成';
GO

ALTER TABLE CustomerCategory5Master ADD CONSTRAINT PK_CustomerCategory5Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ CustomerCategory5Master PK再作成';
GO

ALTER TABLE SupplierCategory1Master ADD CONSTRAINT PK_SupplierCategory1Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ SupplierCategory1Master PK再作成';
GO

ALTER TABLE SupplierCategory2Master ADD CONSTRAINT PK_SupplierCategory2Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ SupplierCategory2Master PK再作成';
GO

ALTER TABLE SupplierCategory3Master ADD CONSTRAINT PK_SupplierCategory3Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ SupplierCategory3Master PK再作成';
GO

ALTER TABLE StaffCategory1Master ADD CONSTRAINT PK_StaffCategory1Master PRIMARY KEY (CategoryCode);
PRINT '  ✓ StaffCategory1Master PK再作成';
GO

-- ========== 最終確認 ==========
PRINT '';
PRINT '=== Migration 066 最終確認 ===';
GO

-- 全テーブルの型を確認
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME = 'CategoryCode'
  AND TABLE_NAME LIKE '%Category%Master'
ORDER BY TABLE_NAME;
GO

-- プライマリキー制約の確認
SELECT 
    t.TABLE_NAME,
    kcu.CONSTRAINT_NAME,
    kcu.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
    ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
JOIN INFORMATION_SCHEMA.TABLES t 
    ON t.TABLE_NAME = tc.TABLE_NAME
WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND t.TABLE_NAME LIKE '%Category%Master'
ORDER BY t.TABLE_NAME;
GO

-- ProductCategory1Masterのデータ確認
PRINT '';
PRINT '--- ProductCategory1Master データ確認 ---';
SELECT CategoryCode, CategoryName FROM ProductCategory1Master ORDER BY CategoryCode;
GO

PRINT '';
PRINT '✅ Migration 066完了！';
PRINT '全ての分類マスタのCategoryCodeをNVARCHAR(3) NOT NULLに変換しました';
PRINT 'プライマリキー制約も正常に再作成されました';
GO