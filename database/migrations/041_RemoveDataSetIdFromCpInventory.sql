-- 041_RemoveDataSetIdFromCpInventory.sql
-- CpInventoryMasterからDataSetIdを削除（仮テーブル設計への変更）
-- 作成日: 2025-07-30

PRINT '041_RemoveDataSetIdFromCpInventory: 開始';

-- ===================================================
-- CpInventoryMasterテーブルの存在確認
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND type in (N'U'))
BEGIN
    PRINT '⚠️ CpInventoryMasterテーブルが存在しません。スキップします。';
    RETURN;
END

PRINT '✓ CpInventoryMasterテーブルが存在します。DataSetId削除処理を開始します。';

-- ===================================================
-- 現在のテーブル構造確認
-- ===================================================
PRINT '=== CpInventoryMasterテーブルの現在の構造 ===';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'CpInventoryMaster' 
ORDER BY ORDINAL_POSITION;

-- ===================================================
-- DataSetIdカラムの存在確認と削除処理
-- ===================================================
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'DataSetId')
BEGIN
    PRINT '=== DataSetId削除処理開始 ===';
    
    -- Step 1: データの全削除（仮テーブルなので安全）
    PRINT 'CP在庫マスタの全データを削除中...';
    TRUNCATE TABLE CpInventoryMaster;
    PRINT '✓ CP在庫マスタの全データを削除しました。';
    
    -- Step 2: DataSetId関連のインデックス削除
    IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('CpInventoryMaster') AND name = 'IX_CpInventoryMaster_DataSetId')
    BEGIN
        DROP INDEX IX_CpInventoryMaster_DataSetId ON CpInventoryMaster;
        PRINT '✓ IX_CpInventoryMaster_DataSetIdインデックスを削除しました';
    END
    
    -- 複合インデックスの削除（DataSetIdを含む可能性のあるインデックス）
    DECLARE @IndexName NVARCHAR(128);
    DECLARE index_cursor CURSOR FOR
        SELECT DISTINCT i.name
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE i.object_id = OBJECT_ID('CpInventoryMaster')
          AND c.name = 'DataSetId'
          AND i.name IS NOT NULL
          AND i.name != 'PK_CpInventoryMaster';  -- 主キーは後で削除
    
    OPEN index_cursor;
    FETCH NEXT FROM index_cursor INTO @IndexName;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        PRINT 'DataSetIdを含むインデックス ' + @IndexName + ' を削除中...';
        EXEC('DROP INDEX [' + @IndexName + '] ON CpInventoryMaster');
        PRINT '✓ インデックス ' + @IndexName + ' を削除しました。';
        FETCH NEXT FROM index_cursor INTO @IndexName;
    END
    
    CLOSE index_cursor;
    DEALLOCATE index_cursor;
    
    -- Step 3: プライマリキー制約の削除
    IF EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_CpInventoryMaster' AND parent_object_id = OBJECT_ID('CpInventoryMaster'))
    BEGIN
        ALTER TABLE CpInventoryMaster DROP CONSTRAINT PK_CpInventoryMaster;
        PRINT '✓ 既存のプライマリキー制約を削除しました';
    END
    
    -- Step 4: DataSetIdカラムの削除
    ALTER TABLE CpInventoryMaster DROP COLUMN DataSetId;
    PRINT '✓ DataSetIdカラムを削除しました';
    
    -- Step 5: 新しいプライマリキー制約の作成（5項目複合キーのみ）
    ALTER TABLE CpInventoryMaster ADD CONSTRAINT PK_CpInventoryMaster 
        PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);
    PRINT '✓ 新しいプライマリキー制約を作成しました（5項目複合キー）';
    
    PRINT '=== DataSetId削除処理完了 ===';
END
ELSE
BEGIN
    PRINT '⚠️ DataSetIdカラムは既に存在しません。スキップします。';
END

-- ===================================================
-- 削除後のテーブル構造確認
-- ===================================================
PRINT '';
PRINT '=== 削除後のCpInventoryMasterテーブル構造 ===';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'CpInventoryMaster' 
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
WHERE tc.TABLE_NAME = 'CpInventoryMaster' 
    AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY kcu.ORDINAL_POSITION;

-- ===================================================
-- 処理完了メッセージ
-- ===================================================
PRINT '';
PRINT '=== CpInventoryMaster仮テーブル設計への変更完了 ===';
PRINT 'CpInventoryMasterテーブルはDataSetId管理から完全に独立しました。';
PRINT 'このテーブルは以下の特徴を持ちます：';
PRINT '- 商品勘定処理専用の一時作業テーブル';
PRINT '- TRUNCATEによる高速全削除';
PRINT '- DataSetId依存なしの純粋な仮テーブル';
PRINT '- 5項目複合キーのみで管理（DataSetId不要）';
PRINT '';
PRINT '【重要】アプリケーションコードの更新も必要です：';
PRINT '- CpInventoryMaster.cs: DataSetIdプロパティの削除';
PRINT '- CpInventoryMasterRepository.cs: DataSetId関連の処理削除';
PRINT '- CpInventoryMasterService.cs: DataSetIdを使用しない実装への変更';
PRINT '- ストアドプロシージャ: DataSetIdパラメータの削除';

PRINT '041_RemoveDataSetIdFromCpInventory: 完了';
GO