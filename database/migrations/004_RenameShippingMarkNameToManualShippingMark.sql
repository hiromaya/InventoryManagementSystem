-- =============================================
-- InventoryMaster ShippingMarkName → ManualShippingMark 変更
-- 作成日: 2025-08-26
-- 目的: 手入力荷印を正しい名称に変更し、システム全体の整合性を保つ
-- =============================================

USE InventoryManagementDB;
GO

PRINT '=== InventoryMaster ShippingMarkName → ManualShippingMark 変更開始 ===';

-- ステップ1: 既存データのバックアップ
DECLARE @BackupTableName NVARCHAR(100);
SET @BackupTableName = 'InventoryMaster_Backup_' + FORMAT(GETDATE(), 'yyyyMMdd_HHmmss');

DECLARE @BackupSQL NVARCHAR(MAX);
SET @BackupSQL = 'SELECT * INTO ' + @BackupTableName + ' FROM InventoryMaster';

EXEC sp_executesql @BackupSQL;
PRINT 'バックアップテーブル作成: ' + @BackupTableName;

-- ステップ2: 制約の確認と削除（プライマリキー）
DECLARE @PK_Name NVARCHAR(128);
SELECT @PK_Name = CONSTRAINT_NAME 
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
WHERE TABLE_NAME = 'InventoryMaster' 
  AND CONSTRAINT_TYPE = 'PRIMARY KEY';

IF @PK_Name IS NOT NULL
BEGIN
    DECLARE @DropPK_SQL NVARCHAR(MAX);
    SET @DropPK_SQL = 'ALTER TABLE InventoryMaster DROP CONSTRAINT [' + @PK_Name + ']';
    EXEC sp_executesql @DropPK_SQL;
    PRINT 'プライマリキー削除: ' + @PK_Name;
END

-- ステップ3: 関連するインデックスの削除
DECLARE @IndexName NVARCHAR(128);
DECLARE index_cursor CURSOR FOR
    SELECT i.name
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    WHERE i.object_id = OBJECT_ID('InventoryMaster')
      AND c.name = 'ShippingMarkName'
      AND i.name IS NOT NULL;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @IndexName;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @DropIndex_SQL NVARCHAR(MAX);
    SET @DropIndex_SQL = 'DROP INDEX [' + @IndexName + '] ON InventoryMaster';
    EXEC sp_executesql @DropIndex_SQL;
    PRINT 'インデックス削除: ' + @IndexName;
    
    FETCH NEXT FROM index_cursor INTO @IndexName;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;

-- ステップ4: カラム名変更
EXEC sp_rename 'InventoryMaster.ShippingMarkName', 'ManualShippingMark', 'COLUMN';
PRINT 'カラム名変更: ShippingMarkName → ManualShippingMark';

-- ステップ5: データ型と制約の調整（50文字→8文字）
ALTER TABLE InventoryMaster 
ALTER COLUMN ManualShippingMark NVARCHAR(8) NOT NULL;
PRINT 'データ型変更: NVARCHAR(50) → NVARCHAR(8)';

-- ステップ6: プライマリキーの再作成
ALTER TABLE InventoryMaster 
ADD CONSTRAINT PK_InventoryMaster PRIMARY KEY (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
);
PRINT 'プライマリキー再作成完了';

-- ステップ7: インデックスの再作成
CREATE INDEX IX_InventoryMaster_ProductCode ON InventoryMaster(ProductCode);
CREATE INDEX IX_InventoryMaster_ProductCategory1 ON InventoryMaster(ProductCategory1);
CREATE INDEX IX_InventoryMaster_JobDate ON InventoryMaster(JobDate);
CREATE INDEX IX_InventoryMaster_DataSetId ON InventoryMaster(DataSetId);
PRINT 'インデックス再作成完了';

-- ステップ8: データの検証
DECLARE @RowCount INT;
SELECT @RowCount = COUNT(*) FROM InventoryMaster;
PRINT '変更後のデータ件数: ' + CAST(@RowCount AS NVARCHAR(10));

-- ステップ9: ManualShippingMark の長さ検証
DECLARE @LongDataCount INT;
SELECT @LongDataCount = COUNT(*) 
FROM InventoryMaster 
WHERE LEN(ManualShippingMark) > 8;

IF @LongDataCount > 0
BEGIN
    PRINT '警告: 8文字を超えるManualShippingMarkが ' + CAST(@LongDataCount AS NVARCHAR(10)) + ' 件あります';
    
    -- 8文字を超えるデータを8文字に切り詰め
    UPDATE InventoryMaster 
    SET ManualShippingMark = LEFT(ManualShippingMark, 8)
    WHERE LEN(ManualShippingMark) > 8;
    
    PRINT '8文字を超えるデータを切り詰めました';
END

PRINT '=== InventoryMaster ShippingMarkName → ManualShippingMark 変更完了 ===';
GO