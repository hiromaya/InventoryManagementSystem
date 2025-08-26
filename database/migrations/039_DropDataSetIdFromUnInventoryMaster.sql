-- =============================================
-- Migration: 039_DropDataSetIdFromUnInventoryMaster.sql
-- 目的: UnInventoryMasterテーブルからDataSetId列を削除（使い捨てテーブル設計）
-- 作成日: 2025-07-28
-- 修正日: 2025-07-30
-- 修正内容: インデックス削除を追加
-- =============================================

USE InventoryManagementDB;
GO

-- Step 0: UnInventoryMasterテーブルの存在確認
PRINT '=== UnInventoryMasterテーブル存在確認 ===';
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UnInventoryMaster]') AND type in (N'U'))
BEGIN
    PRINT '⚠️ UnInventoryMasterテーブルが存在しません。';
    PRINT '   038_Create_UnInventoryMaster.sqlで作成される予定です。';
    PRINT '   このマイグレーションをスキップします。';
    RETURN;
END
ELSE
BEGIN
    PRINT '✓ UnInventoryMasterテーブルが存在します。DataSetId削除処理を続行します。';
END

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

-- Step 5: 主キー制約、インデックス、DataSetId列の削除
PRINT '=== 主キー制約、インデックス、DataSetId列の削除実行 ===';
BEGIN TRY
    -- DataSetId列が存在する場合のみ削除
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'UnInventoryMaster' 
          AND COLUMN_NAME = 'DataSetId'
    )
    BEGIN
        -- Step 5-1: 既存の主キー制約を削除
        IF EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_UnInventoryMaster' AND parent_object_id = OBJECT_ID('UnInventoryMaster'))
        BEGIN
            PRINT '主キー制約を削除中...';
            ALTER TABLE UnInventoryMaster DROP CONSTRAINT PK_UnInventoryMaster;
            PRINT '✅ 主キー制約 PK_UnInventoryMaster を削除しました。';
        END
        
        -- Step 5-2: DataSetId関連のインデックスを削除
        IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UnInventoryMaster_DataSetId' AND object_id = OBJECT_ID('UnInventoryMaster'))
        BEGIN
            PRINT 'DataSetIdインデックスを削除中...';
            DROP INDEX IX_UnInventoryMaster_DataSetId ON UnInventoryMaster;
            PRINT '✅ インデックス IX_UnInventoryMaster_DataSetId を削除しました。';
        END
        
        -- 複合インデックスもチェック（DataSetIdを含む可能性のあるインデックス）
        DECLARE @IndexName NVARCHAR(128);
        DECLARE index_cursor CURSOR FOR
            SELECT DISTINCT i.name
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.object_id = OBJECT_ID('UnInventoryMaster')
              AND c.name = 'DataSetId'
              AND i.name IS NOT NULL
              AND i.name != 'PK_UnInventoryMaster';  -- 主キーは既に削除済み
        
        OPEN index_cursor;
        FETCH NEXT FROM index_cursor INTO @IndexName;
        
        WHILE @@FETCH_STATUS = 0
        BEGIN
            PRINT 'DataSetIdを含むインデックス ' + @IndexName + ' を削除中...';
            EXEC('DROP INDEX ' + @IndexName + ' ON UnInventoryMaster');
            PRINT '✅ インデックス ' + @IndexName + ' を削除しました。';
            FETCH NEXT FROM index_cursor INTO @IndexName;
        END
        
        CLOSE index_cursor;
        DEALLOCATE index_cursor;
        
        -- Step 5-3: DataSetId列を削除
        PRINT 'DataSetId列を削除中...';
        ALTER TABLE UnInventoryMaster DROP COLUMN DataSetId;
        PRINT '✅ DataSetId列を正常に削除しました。';
        
        -- Step 5-4: 新しい主キー制約を作成（5項目複合キーのみ）
        PRINT '新しい主キー制約を作成中...';
        ALTER TABLE UnInventoryMaster ADD CONSTRAINT PK_UnInventoryMaster 
            PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
        PRINT '✅ 新しい主キー制約を作成しました（5項目複合キー）。';
    END
    ELSE
    BEGIN
        PRINT '⚠️ DataSetId列は既に存在しません。スキップします。';
    END
END TRY
BEGIN CATCH
    PRINT '❌ エラーが発生しました:';
    PRINT 'エラーメッセージ: ' + ERROR_MESSAGE();
    PRINT 'エラー番号: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10));
    PRINT 'エラー行: ' + CAST(ERROR_LINE() AS NVARCHAR(10));
    THROW;
END CATCH

-- Step 6: 削除後のテーブル構造確認
PRINT '';
PRINT '=== 削除後のUnInventoryMasterテーブル構造 ===';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'UnInventoryMaster' 
ORDER BY ORDINAL_POSITION;

-- 新しい主キー制約を確認
PRINT '';
PRINT '=== 新しい主キー制約確認 ===';
SELECT 
    tc.CONSTRAINT_NAME,
    kcu.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
    ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
WHERE tc.TABLE_NAME = 'UnInventoryMaster' 
    AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY kcu.ORDINAL_POSITION;

-- Step 7: 使い捨てテーブル設計の検証
PRINT '';
PRINT '=== 使い捨てテーブル設計の検証完了 ===';
PRINT 'UnInventoryMasterテーブルはDataSetId管理から完全に独立しました。';
PRINT 'このテーブルは以下の特徴を持ちます：';
PRINT '- TRUNCATEによる高速全削除';
PRINT '- 単一処理での一時データ格納';
PRINT '- DataSetId依存なしの純粋な作業テーブル';
PRINT '- 5項目複合キーのみで管理（DataSetId不要）';
PRINT '- アンマッチチェック専用の一時作業領域';
PRINT '';
PRINT '【重要】アプリケーションコードの更新も必要です：';
PRINT '- UnInventoryMaster.cs: DataSetIdプロパティの削除';
PRINT '- UnInventoryRepository.cs: DataSetId関連の処理削除';
PRINT '- UnmatchListService.cs: DataSetIdを使用しない実装への変更';

GO