-- DataSetsテーブルにDataSetTypeとImportedAtカラムを追加
-- 作成日: 2025-07-16
-- 目的: fix_datasets_schema.sqlで追加していたカラムをマイグレーション化
-- 背景: init-database --forceで削除されないようにするため

SET NOCOUNT ON;

PRINT '=== 028_AddDataSetTypeAndImportedAt.sql 実行開始 ===';

-- DataSetsテーブルの存在確認
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DataSets')
BEGIN
    PRINT 'エラー: DataSetsテーブルが存在しません。CreateDatabase.sqlを先に実行してください。';
    RETURN;
END

-- DataSetTypeカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'DataSetType')
BEGIN
    ALTER TABLE DataSets ADD DataSetType NVARCHAR(50) NOT NULL DEFAULT 'Unknown';
    PRINT '✓ DataSets.DataSetType カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSets.DataSetType カラムは既に存在します';
END

-- ImportedAtカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'ImportedAt')
BEGIN
    ALTER TABLE DataSets ADD ImportedAt DATETIME NOT NULL DEFAULT GETDATE();
    PRINT '✓ DataSets.ImportedAt カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSets.ImportedAt カラムは既に存在します';
END

-- 既存データのデフォルト値設定（動的SQL使用）
PRINT '';
PRINT '既存データのデフォルト値設定開始...';

DECLARE @sql NVARCHAR(MAX);
DECLARE @updateCount INT;

-- DataSetsテーブルの既存データ更新
IF EXISTS (SELECT * FROM DataSets)
BEGIN
    -- DataSetType の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'DataSetType')
    BEGIN
        SET @sql = 'UPDATE DataSets SET DataSetType = ''Unknown'' WHERE DataSetType IS NULL OR DataSetType = ''''';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        IF @updateCount > 0
            PRINT '  └ DataSetType のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- ImportedAt の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'ImportedAt')
    BEGIN
        SET @sql = 'UPDATE DataSets SET ImportedAt = GETDATE() WHERE ImportedAt IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        IF @updateCount > 0
            PRINT '  └ ImportedAt のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    PRINT 'DataSetsテーブルの既存データ更新完了';
END
ELSE
BEGIN
    PRINT 'DataSetsテーブルにデータが存在しません';
END

-- 完了メッセージ
PRINT '';
PRINT '=== 028_AddDataSetTypeAndImportedAt.sql 実行完了 ===';
PRINT 'DataSetsテーブルにDataSetTypeとImportedAtカラムが追加されました。';
PRINT 'これらのカラムは今後 init-database --force で削除されることはありません。';

SET NOCOUNT OFF;