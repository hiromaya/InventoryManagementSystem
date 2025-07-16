-- DataSetsテーブルとDataSetManagementテーブルのカラム不足エラー修正
-- 作成日: 2025-07-16
-- 目的: import-folderコマンドのSqlExceptionエラー解決

-- DataSetsテーブルのカラム追加
PRINT 'DataSetsテーブルのカラム追加開始...';

-- RecordCountカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'RecordCount')
BEGIN
    ALTER TABLE DataSets ADD RecordCount INT DEFAULT 0;
    PRINT 'DataSets.RecordCount カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSets.RecordCount カラムは既に存在します';
END

-- FilePathカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'FilePath')
BEGIN
    ALTER TABLE DataSets ADD FilePath NVARCHAR(500);
    PRINT 'DataSets.FilePath カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSets.FilePath カラムは既に存在します';
END

-- CreatedAtカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE DataSets ADD CreatedAt DATETIME DEFAULT GETDATE();
    PRINT 'DataSets.CreatedAt カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSets.CreatedAt カラムは既に存在します';
END

-- UpdatedAtカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE DataSets ADD UpdatedAt DATETIME DEFAULT GETDATE();
    PRINT 'DataSets.UpdatedAt カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSets.UpdatedAt カラムは既に存在します';
END

PRINT 'DataSetsテーブルのカラム追加完了';

-- DataSetManagementテーブルのカラム追加
PRINT 'DataSetManagementテーブルのカラム追加開始...';

-- ImportTypeカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'ImportType')
BEGIN
    ALTER TABLE DataSetManagement ADD ImportType NVARCHAR(20) DEFAULT 'IMPORT';
    PRINT 'DataSetManagement.ImportType カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.ImportType カラムは既に存在します';
END

-- RecordCountカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'RecordCount')
BEGIN
    ALTER TABLE DataSetManagement ADD RecordCount INT DEFAULT 0;
    PRINT 'DataSetManagement.RecordCount カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.RecordCount カラムは既に存在します';
END

-- TotalRecordCountカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'TotalRecordCount')
BEGIN
    ALTER TABLE DataSetManagement ADD TotalRecordCount INT DEFAULT 0;
    PRINT 'DataSetManagement.TotalRecordCount カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.TotalRecordCount カラムは既に存在します';
END

-- IsActiveカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'IsActive')
BEGIN
    ALTER TABLE DataSetManagement ADD IsActive BIT DEFAULT 1;
    PRINT 'DataSetManagement.IsActive カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.IsActive カラムは既に存在します';
END

-- IsArchivedカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'IsArchived')
BEGIN
    ALTER TABLE DataSetManagement ADD IsArchived BIT DEFAULT 0;
    PRINT 'DataSetManagement.IsArchived カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.IsArchived カラムは既に存在します';
END

-- ParentDataSetIdカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'ParentDataSetId')
BEGIN
    ALTER TABLE DataSetManagement ADD ParentDataSetId NVARCHAR(50);
    PRINT 'DataSetManagement.ParentDataSetId カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.ParentDataSetId カラムは既に存在します';
END

-- ImportedFilesカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'ImportedFiles')
BEGIN
    ALTER TABLE DataSetManagement ADD ImportedFiles NVARCHAR(MAX);
    PRINT 'DataSetManagement.ImportedFiles カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.ImportedFiles カラムは既に存在します';
END

-- Notesカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'Notes')
BEGIN
    ALTER TABLE DataSetManagement ADD Notes NVARCHAR(MAX);
    PRINT 'DataSetManagement.Notes カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.Notes カラムは既に存在します';
END

-- CreatedByカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'CreatedBy')
BEGIN
    ALTER TABLE DataSetManagement ADD CreatedBy NVARCHAR(100);
    PRINT 'DataSetManagement.CreatedBy カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.CreatedBy カラムは既に存在します';
END

-- Departmentカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'Department')
BEGIN
    ALTER TABLE DataSetManagement ADD Department NVARCHAR(20);
    PRINT 'DataSetManagement.Department カラムを追加しました';
END
ELSE
BEGIN
    PRINT 'DataSetManagement.Department カラムは既に存在します';
END

PRINT 'DataSetManagementテーブルのカラム追加完了';

-- 既存データのデフォルト値設定
PRINT '既存データのデフォルト値設定開始...';

-- DataSetManagementテーブルの既存データ更新
UPDATE DataSetManagement 
SET 
    ImportType = ISNULL(ImportType, 'IMPORT'),
    RecordCount = ISNULL(RecordCount, ISNULL(TotalRecordCount, 0)),
    TotalRecordCount = ISNULL(TotalRecordCount, 0),
    IsActive = ISNULL(IsActive, 1),
    IsArchived = ISNULL(IsArchived, 0),
    CreatedBy = ISNULL(CreatedBy, 'system'),
    Department = ISNULL(Department, 'Unknown')
WHERE ImportType IS NULL OR RecordCount IS NULL OR TotalRecordCount IS NULL;

-- DataSetsテーブルの既存データ更新
UPDATE DataSets 
SET 
    RecordCount = ISNULL(RecordCount, 0),
    CreatedAt = ISNULL(CreatedAt, GETDATE()),
    UpdatedAt = ISNULL(UpdatedAt, GETDATE())
WHERE RecordCount IS NULL OR CreatedAt IS NULL OR UpdatedAt IS NULL;

PRINT '既存データのデフォルト値設定完了';

-- 完了メッセージ
PRINT '=== 025_Fix_DataSets_Columns.sql 実行完了 ===';
PRINT 'DataSetsテーブルとDataSetManagementテーブルのカラム不足エラーが修正されました。';
PRINT 'import-folderコマンドが正常に動作するようになります。';