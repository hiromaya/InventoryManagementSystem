-- =====================================================
-- スクリプト名: AddImportTypeToInventoryMaster.sql
-- 説明: InventoryMasterテーブルにImportTypeカラムとIsActiveカラムを追加
-- 作成日: 2025-07-11
-- 注意: このスクリプトは冪等性を持ち、複数回実行しても安全です
-- =====================================================

USE InventoryManagementDB;
GO

-- ImportTypeカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'ImportType'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD ImportType NVARCHAR(20) NOT NULL DEFAULT 'UNKNOWN';
    
    PRINT 'ImportTypeカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'ImportTypeカラムは既に存在します。';
END
GO

-- IsActiveカラムの追加（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND COLUMN_NAME = 'IsActive'
)
BEGIN
    ALTER TABLE InventoryMaster
    ADD IsActive BIT NOT NULL DEFAULT 1;
    
    PRINT 'IsActiveカラムを追加しました。';
    
    -- 既存のデータをすべてアクティブに設定
    UPDATE InventoryMaster
    SET IsActive = 1
    WHERE IsActive IS NULL;
    
    PRINT '既存データのIsActiveを1に設定しました。';
END
ELSE
BEGIN
    PRINT 'IsActiveカラムは既に存在します。';
END
GO

-- ImportTypeとIsActiveの複合インデックスを作成（既に存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM sys.indexes 
    WHERE name = 'IX_InventoryMaster_ImportType_IsActive'
    AND object_id = OBJECT_ID('InventoryMaster')
)
BEGIN
    CREATE INDEX IX_InventoryMaster_ImportType_IsActive
    ON InventoryMaster(ImportType, IsActive)
    INCLUDE (JobDate, DataSetId, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
    
    PRINT 'IX_InventoryMaster_ImportType_IsActiveインデックスを作成しました。';
END
ELSE
BEGIN
    PRINT 'IX_InventoryMaster_ImportType_IsActiveインデックスは既に存在します。';
END
GO

-- IsActiveとJobDateの複合インデックスを作成（パフォーマンス向上用）
IF NOT EXISTS (
    SELECT * 
    FROM sys.indexes 
    WHERE name = 'IX_InventoryMaster_IsActive_JobDate'
    AND object_id = OBJECT_ID('InventoryMaster')
)
BEGIN
    CREATE INDEX IX_InventoryMaster_IsActive_JobDate
    ON InventoryMaster(IsActive, JobDate)
    INCLUDE (DataSetId, ImportType);
    
    PRINT 'IX_InventoryMaster_IsActive_JobDateインデックスを作成しました。';
END
ELSE
BEGIN
    PRINT 'IX_InventoryMaster_IsActive_JobDateインデックスは既に存在します。';
END
GO

-- 既存データのImportTypeを設定（デフォルト値がUNKNOWNのものを適切に分類）
-- ParentDataSetIdがNULLまたは空で、DailyStockが0のデータを INIT として分類
UPDATE InventoryMaster
SET ImportType = 'INIT'
WHERE ImportType = 'UNKNOWN'
  AND (ParentDataSetId IS NULL OR ParentDataSetId = '')
  AND DailyStock = 0;

PRINT '初期在庫データのImportTypeをINITに設定しました。';
GO

-- ParentDataSetIdが設定されているデータを CARRYOVER として分類
UPDATE InventoryMaster
SET ImportType = 'CARRYOVER'
WHERE ImportType = 'UNKNOWN'
  AND ParentDataSetId IS NOT NULL
  AND ParentDataSetId != '';

PRINT '引継データのImportTypeをCARRYOVERに設定しました。';
GO

-- 通常のインポートデータを IMPORT に設定
UPDATE InventoryMaster
SET ImportType = 'IMPORT'
WHERE ImportType = 'UNKNOWN'
  AND DataSetId IS NOT NULL
  AND DataSetId != '';

PRINT '通常インポートデータのImportTypeをIMPORTに設定しました。';
GO

-- 更新結果の確認
PRINT '';
PRINT '===== ImportType別レコード数 =====';
SELECT 
    ImportType,
    COUNT(*) as RecordCount,
    MIN(JobDate) as MinJobDate,
    MAX(JobDate) as MaxJobDate
FROM InventoryMaster
GROUP BY ImportType
ORDER BY ImportType;
GO

PRINT '';
PRINT '===== InventoryMasterテーブルの構造（ImportType関連） =====';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventoryMaster'
  AND COLUMN_NAME IN ('ImportType', 'IsActive', 'DataSetId', 'ParentDataSetId')
ORDER BY ORDINAL_POSITION;
GO

PRINT '';
PRINT '===== インデックス情報 =====';
SELECT 
    i.name AS IndexName,
    OBJECT_NAME(i.object_id) AS TableName,
    COL_NAME(ic.object_id, ic.column_id) AS ColumnName,
    ic.key_ordinal,
    ic.is_included_column
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE i.object_id = OBJECT_ID('InventoryMaster')
  AND i.name IN ('IX_InventoryMaster_ImportType_IsActive', 'IX_InventoryMaster_IsActive_JobDate')
ORDER BY i.name, ic.key_ordinal, ic.index_column_id;
GO

PRINT '';
PRINT '===== スクリプト実行完了 =====';
PRINT 'ImportTypeカラムとIsActiveカラム、および関連インデックスが正常に確認/追加されました。';