-- DataSetsテーブルの更新スクリプト
-- 実行日: 2025-06-25

USE InventoryManagementDB;
GO

-- 1. 既存のDataSetsテーブルの構造を確認
PRINT '=== DataSetsテーブル更新開始 ===';
GO

-- 2. 必要なカラムを追加
-- DataSetType カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'DataSetType')
BEGIN
    ALTER TABLE DataSets ADD DataSetType NVARCHAR(50) NOT NULL DEFAULT 'Unknown';
    PRINT 'DataSetTypeカラムを追加しました';
END

-- ImportedAt カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'ImportedAt')
BEGIN
    ALTER TABLE DataSets ADD ImportedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    PRINT 'ImportedAtカラムを追加しました';
END

-- RecordCount カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'RecordCount')
BEGIN
    ALTER TABLE DataSets ADD RecordCount INT NOT NULL DEFAULT 0;
    PRINT 'RecordCountカラムを追加しました';
END

-- FilePath カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'FilePath')
BEGIN
    ALTER TABLE DataSets ADD FilePath NVARCHAR(500) NULL;
    PRINT 'FilePathカラムを追加しました';
END

-- CreatedAt カラムの追加（CreatedDateから名前変更または新規追加）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'CreatedDate')
BEGIN
    EXEC sp_rename 'DataSets.CreatedDate', 'CreatedAt', 'COLUMN';
    PRINT 'CreatedDateカラムをCreatedAtに名前変更しました';
END
ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE DataSets ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    PRINT 'CreatedAtカラムを追加しました';
END

-- UpdatedAt カラムの追加（UpdatedDateから名前変更または新規追加）
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'UpdatedDate')
BEGIN
    EXEC sp_rename 'DataSets.UpdatedDate', 'UpdatedAt', 'COLUMN';
    PRINT 'UpdatedDateカラムをUpdatedAtに名前変更しました';
END
ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE DataSets ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
    PRINT 'UpdatedAtカラムを追加しました';
END

-- 3. インデックスの追加
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('DataSets') AND name = 'IX_DataSets_DataSetType')
BEGIN
    CREATE INDEX IX_DataSets_DataSetType ON DataSets(DataSetType);
    PRINT 'DataSetTypeインデックスを作成しました';
END

PRINT '';
PRINT '=== DataSetsテーブル更新完了 ===';
GO

-- 更新後の構造を確認
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'DataSets'
ORDER BY ORDINAL_POSITION;
GO