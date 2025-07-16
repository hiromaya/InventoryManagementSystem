-- DataSetsテーブルのスキーマ修正（エラー対応版）
-- 作成日: 2025-07-16
-- 目的: Windows環境でのSqlException解決
-- 修正版: カラム参照エラーを回避

SET NOCOUNT ON;

PRINT '=== DataSetsテーブルスキーマ修正開始 ===';

-- 現在のテーブル構造を確認（カラム名を直接参照しない）
PRINT '現在のDataSetsテーブルの列情報を確認中...';
SELECT COUNT(*) as '総カラム数' FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DataSets';

-- DataSetTypeカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'DataSetType')
BEGIN
    ALTER TABLE DataSets ADD DataSetType NVARCHAR(50) NOT NULL DEFAULT 'Unknown';
    PRINT '✓ DataSetTypeカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetTypeカラムは既に存在します';
END

-- ImportedAtカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'ImportedAt')
BEGIN
    ALTER TABLE DataSets ADD ImportedAt DATETIME NOT NULL DEFAULT GETDATE();
    PRINT '✓ ImportedAtカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- ImportedAtカラムは既に存在します';
END

-- RecordCountカラムの追加（念のため）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'RecordCount')
BEGIN
    ALTER TABLE DataSets ADD RecordCount INT NOT NULL DEFAULT 0;
    PRINT '✓ RecordCountカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- RecordCountカラムは既に存在します';
END

-- FilePathカラムの追加（念のため）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'FilePath')
BEGIN
    ALTER TABLE DataSets ADD FilePath NVARCHAR(500);
    PRINT '✓ FilePathカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- FilePathカラムは既に存在します';
END

-- CreatedAtカラムの追加（念のため）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE DataSets ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();
    PRINT '✓ CreatedAtカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- CreatedAtカラムは既に存在します';
END

-- UpdatedAtカラムの追加（念のため）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE DataSets ADD UpdatedAt DATETIME NOT NULL DEFAULT GETDATE();
    PRINT '✓ UpdatedAtカラムを追加しました';
END
ELSE
BEGIN
    PRINT '- UpdatedAtカラムは既に存在します';
END

-- 既存レコードのNULL値を更新（動的SQLを使用してカラム存在を確認）
PRINT '';
PRINT '既存レコードのデフォルト値設定...';

DECLARE @sql NVARCHAR(MAX);
DECLARE @updateCount INT;

-- DataSetTypeのデフォルト値設定（動的SQL使用）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'DataSetType')
BEGIN
    SET @sql = N'UPDATE DataSets SET DataSetType = ''Unknown'' WHERE DataSetType IS NULL';
    EXEC sp_executesql @sql;
    SET @updateCount = @@ROWCOUNT;
    IF @updateCount > 0
        PRINT '  └ DataSetTypeのデフォルト値を設定しました (' + CAST(@updateCount AS NVARCHAR(10)) + '件)';
END

-- ImportedAtのデフォルト値設定（動的SQL使用）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'ImportedAt')
BEGIN
    SET @sql = N'UPDATE DataSets SET ImportedAt = GETDATE() WHERE ImportedAt IS NULL';
    EXEC sp_executesql @sql;
    SET @updateCount = @@ROWCOUNT;
    IF @updateCount > 0
        PRINT '  └ ImportedAtのデフォルト値を設定しました (' + CAST(@updateCount AS NVARCHAR(10)) + '件)';
END

-- RecordCountのデフォルト値設定（動的SQL使用）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'RecordCount')
BEGIN
    SET @sql = N'UPDATE DataSets SET RecordCount = 0 WHERE RecordCount IS NULL';
    EXEC sp_executesql @sql;
    SET @updateCount = @@ROWCOUNT;
    IF @updateCount > 0
        PRINT '  └ RecordCountのデフォルト値を設定しました (' + CAST(@updateCount AS NVARCHAR(10)) + '件)';
END

-- CreatedAtのデフォルト値設定（動的SQL使用）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'CreatedAt')
BEGIN
    SET @sql = N'UPDATE DataSets SET CreatedAt = GETDATE() WHERE CreatedAt IS NULL';
    EXEC sp_executesql @sql;
    SET @updateCount = @@ROWCOUNT;
    IF @updateCount > 0
        PRINT '  └ CreatedAtのデフォルト値を設定しました (' + CAST(@updateCount AS NVARCHAR(10)) + '件)';
END

-- UpdatedAtのデフォルト値設定（動的SQL使用）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'UpdatedAt')
BEGIN
    SET @sql = N'UPDATE DataSets SET UpdatedAt = GETDATE() WHERE UpdatedAt IS NULL';
    EXEC sp_executesql @sql;
    SET @updateCount = @@ROWCOUNT;
    IF @updateCount > 0
        PRINT '  └ UpdatedAtのデフォルト値を設定しました (' + CAST(@updateCount AS NVARCHAR(10)) + '件)';
END

-- 修正後のテーブル構造を確認（動的SQLで実行）
PRINT '';
PRINT '修正後のDataSetsテーブルの列一覧:';
PRINT '（INFORMATION_SCHEMA.COLUMNSから取得）';

-- 列情報を表示（動的SQL使用）
DECLARE @columnInfo NVARCHAR(MAX);
SET @columnInfo = N'
SELECT 
    COLUMN_NAME as [カラム名],
    DATA_TYPE as [データ型],
    CHARACTER_MAXIMUM_LENGTH as [最大長],
    IS_NULLABLE as [NULL許可],
    COLUMN_DEFAULT as [デフォルト値]
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = ''DataSets'' 
ORDER BY ORDINAL_POSITION';

EXEC sp_executesql @columnInfo;

PRINT '';
PRINT '=== DataSetsテーブルスキーマ修正完了 ===';
PRINT 'import-initial-inventoryコマンドを再実行してください。';

SET NOCOUNT OFF;