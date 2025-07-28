-- =============================================
-- Migration: 039_DropDataSetIdFromUnInventoryMaster.sql
-- 目的: UnInventoryMasterテーブルからDataSetId列を削除（使い捨てテーブル設計）
-- 作成日: 2025-07-28
-- =============================================

USE InventoryManagementDB;
GO

-- Step 1: UnInventoryMasterテーブルの現在の構造を確認
PRINT '=== UnInventoryMasterテーブルの現在の構造 ===';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'UnInventoryMaster' 
ORDER BY ORDINAL_POSITION;

-- Step 2: 外部キー制約の確認（DataSetId関連）
PRINT '=== DataSetId関連の外部キー制約確認 ===';
SELECT 
    fk.name AS constraint_name,
    OBJECT_NAME(fk.parent_object_id) AS table_name,
    COL_NAME(fc.parent_object_id, fc.parent_column_id) AS column_name,
    OBJECT_NAME (fk.referenced_object_id) AS referenced_table_name,
    COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS referenced_column_name
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fc 
    ON fk.object_id = fc.constraint_object_id
WHERE OBJECT_NAME(fk.parent_object_id) = 'UnInventoryMaster'
  AND COL_NAME(fc.parent_object_id, fc.parent_column_id) = 'DataSetId';

-- Step 3: インデックス確認（DataSetId関連）
PRINT '=== DataSetId関連のインデックス確認 ===';
SELECT 
    i.name AS index_name,
    i.type_desc AS index_type,
    c.name AS column_name
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('UnInventoryMaster')
  AND c.name = 'DataSetId';

-- Step 4: DataSetId列の削除前にテーブルの全削除（使い捨てテーブルなので安全）
PRINT '=== UN在庫マスタの全データ削除（使い捨てテーブル設計） ===';
TRUNCATE TABLE UnInventoryMaster;
PRINT 'UN在庫マスタの全データを削除しました。';

-- Step 5: DataSetId列を削除
PRINT '=== DataSetId列の削除実行 ===';
BEGIN TRY
    -- DataSetId列が存在する場合のみ削除
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'UnInventoryMaster' 
          AND COLUMN_NAME = 'DataSetId'
    )
    BEGIN
        ALTER TABLE UnInventoryMaster DROP COLUMN DataSetId;
        PRINT '✅ DataSetId列を正常に削除しました。';
    END
    ELSE
    BEGIN
        PRINT '⚠️ DataSetId列は既に存在しません。スキップします。';
    END
END TRY
BEGIN CATCH
    PRINT '❌ DataSetId列の削除でエラーが発生しました:';
    PRINT ERROR_MESSAGE();
    THROW;
END CATCH

-- Step 6: 削除後のテーブル構造確認
PRINT '=== 削除後のUnInventoryMasterテーブル構造 ===';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'UnInventoryMaster' 
ORDER BY ORDINAL_POSITION;

-- Step 7: 使い捨てテーブル設計の検証
PRINT '=== 使い捨てテーブル設計の検証完了 ===';
PRINT 'UnInventoryMasterテーブルはDataSetId管理から完全に独立しました。';
PRINT 'このテーブルは以下の特徴を持ちます：';
PRINT '- TRUNCATEによる高速全削除';
PRINT '- 単一処理での一時データ格納';
PRINT '- DataSetId依存なしの純粋な作業テーブル';

GO