-- DataSetsテーブルのスキーマ修正（緊急対応）
-- 作成日: 2025-07-16
-- 目的: Windows環境でのSqlException解決

SET NOCOUNT ON;

PRINT '=== DataSetsテーブルスキーマ修正開始 ===';

-- 現在のテーブル構造を確認
PRINT '現在のDataSetsテーブルの列一覧:';
SELECT 
    COLUMN_NAME as 'カラム名',
    DATA_TYPE as 'データ型',
    IS_NULLABLE as 'NULL許可',
    COLUMN_DEFAULT as 'デフォルト値'
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'DataSets' 
ORDER BY ORDINAL_POSITION;

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

-- 既存レコードのNULL値を更新（カラムが存在する場合のみ）
PRINT '';
PRINT '既存レコードのデフォルト値設定...';

-- DataSetTypeのデフォルト値設定（カラムが存在する場合のみ）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'DataSetType')
BEGIN
    UPDATE DataSets SET DataSetType = 'Unknown' WHERE DataSetType IS NULL;
    PRINT '  └ DataSetTypeのデフォルト値を設定しました';
END

-- ImportedAtのデフォルト値設定（カラムが存在する場合のみ）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'ImportedAt')
BEGIN
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'CreatedAt')
    BEGIN
        UPDATE DataSets SET ImportedAt = ISNULL(CreatedAt, GETDATE()) WHERE ImportedAt IS NULL;
    END
    ELSE
    BEGIN
        UPDATE DataSets SET ImportedAt = GETDATE() WHERE ImportedAt IS NULL;
    END
    PRINT '  └ ImportedAtのデフォルト値を設定しました';
END

-- RecordCountのデフォルト値設定（カラムが存在する場合のみ）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'RecordCount')
BEGIN
    UPDATE DataSets SET RecordCount = 0 WHERE RecordCount IS NULL;
    PRINT '  └ RecordCountのデフォルト値を設定しました';
END

-- CreatedAtのデフォルト値設定（カラムが存在する場合のみ）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'CreatedAt')
BEGIN
    UPDATE DataSets SET CreatedAt = GETDATE() WHERE CreatedAt IS NULL;
    PRINT '  └ CreatedAtのデフォルト値を設定しました';
END

-- UpdatedAtのデフォルト値設定（カラムが存在する場合のみ）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'UpdatedAt')
BEGIN
    UPDATE DataSets SET UpdatedAt = GETDATE() WHERE UpdatedAt IS NULL;
    PRINT '  └ UpdatedAtのデフォルト値を設定しました';
END

-- 修正後のテーブル構造を確認
PRINT '';
PRINT '修正後のDataSetsテーブルの列一覧:';
SELECT 
    COLUMN_NAME as 'カラム名',
    DATA_TYPE as 'データ型',
    IS_NULLABLE as 'NULL許可',
    COLUMN_DEFAULT as 'デフォルト値'
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'DataSets' 
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT '=== DataSetsテーブルスキーマ修正完了 ===';
PRINT 'import-initial-inventoryコマンドを再実行してください。';

SET NOCOUNT OFF;