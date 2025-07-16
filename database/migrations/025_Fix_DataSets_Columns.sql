-- DataSetsテーブルとDataSetManagementテーブルのカラム不足エラー修正
-- 作成日: 2025-07-16
-- 目的: import-folderコマンドのSqlExceptionエラー解決
-- 修正版: 動的SQL使用

SET NOCOUNT ON;

PRINT '=== 025_Fix_DataSets_Columns.sql 実行開始 ===';

-- 変数宣言
DECLARE @sql NVARCHAR(MAX);
DECLARE @updateCount INT;

-- DataSetsテーブルのカラム追加
PRINT 'DataSetsテーブルのカラム追加開始...';

-- DataSetsテーブルの存在確認
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DataSets')
BEGIN
    PRINT 'エラー: DataSetsテーブルが存在しません。CreateDatabase.sqlを先に実行してください。';
    RETURN;
END

-- RecordCountカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'RecordCount')
BEGIN
    ALTER TABLE DataSets ADD RecordCount INT DEFAULT 0;
    PRINT '✓ DataSets.RecordCount カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSets.RecordCount カラムは既に存在します';
END

-- FilePathカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'FilePath')
BEGIN
    ALTER TABLE DataSets ADD FilePath NVARCHAR(500);
    PRINT '✓ DataSets.FilePath カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSets.FilePath カラムは既に存在します';
END

-- CreatedAtカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE DataSets ADD CreatedAt DATETIME DEFAULT GETDATE();
    PRINT '✓ DataSets.CreatedAt カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSets.CreatedAt カラムは既に存在します';
END

-- UpdatedAtカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE DataSets ADD UpdatedAt DATETIME DEFAULT GETDATE();
    PRINT '✓ DataSets.UpdatedAt カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSets.UpdatedAt カラムは既に存在します';
END

PRINT 'DataSetsテーブルのカラム追加完了';

-- DataSetManagementテーブルのカラム追加
PRINT '';
PRINT 'DataSetManagementテーブルのカラム追加開始...';

-- DataSetManagementテーブルの存在確認
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DataSetManagement')
BEGIN
    PRINT 'エラー: DataSetManagementテーブルが存在しません。006_AddDataSetManagement.sqlを先に実行してください。';
    RETURN;
END

-- ImportTypeカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'ImportType')
BEGIN
    ALTER TABLE DataSetManagement ADD ImportType NVARCHAR(20) DEFAULT 'IMPORT';
    PRINT '✓ DataSetManagement.ImportType カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.ImportType カラムは既に存在します';
END

-- RecordCountカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'RecordCount')
BEGIN
    ALTER TABLE DataSetManagement ADD RecordCount INT DEFAULT 0;
    PRINT '✓ DataSetManagement.RecordCount カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.RecordCount カラムは既に存在します';
END

-- TotalRecordCountカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'TotalRecordCount')
BEGIN
    ALTER TABLE DataSetManagement ADD TotalRecordCount INT DEFAULT 0;
    PRINT '✓ DataSetManagement.TotalRecordCount カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.TotalRecordCount カラムは既に存在します';
END

-- IsActiveカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'IsActive')
BEGIN
    ALTER TABLE DataSetManagement ADD IsActive BIT DEFAULT 1;
    PRINT '✓ DataSetManagement.IsActive カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.IsActive カラムは既に存在します';
END

-- IsArchivedカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'IsArchived')
BEGIN
    ALTER TABLE DataSetManagement ADD IsArchived BIT DEFAULT 0;
    PRINT '✓ DataSetManagement.IsArchived カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.IsArchived カラムは既に存在します';
END

-- ParentDataSetIdカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'ParentDataSetId')
BEGIN
    ALTER TABLE DataSetManagement ADD ParentDataSetId NVARCHAR(50);
    PRINT '✓ DataSetManagement.ParentDataSetId カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.ParentDataSetId カラムは既に存在します';
END

-- ImportedFilesカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'ImportedFiles')
BEGIN
    ALTER TABLE DataSetManagement ADD ImportedFiles NVARCHAR(MAX);
    PRINT '✓ DataSetManagement.ImportedFiles カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.ImportedFiles カラムは既に存在します';
END

-- Notesカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'Notes')
BEGIN
    ALTER TABLE DataSetManagement ADD Notes NVARCHAR(MAX);
    PRINT '✓ DataSetManagement.Notes カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.Notes カラムは既に存在します';
END

-- CreatedByカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'CreatedBy')
BEGIN
    ALTER TABLE DataSetManagement ADD CreatedBy NVARCHAR(100);
    PRINT '✓ DataSetManagement.CreatedBy カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.CreatedBy カラムは既に存在します';
END

-- Departmentカラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'Department')
BEGIN
    ALTER TABLE DataSetManagement ADD Department NVARCHAR(20);
    PRINT '✓ DataSetManagement.Department カラムを追加しました';
END
ELSE
BEGIN
    PRINT '- DataSetManagement.Department カラムは既に存在します';
END

PRINT 'DataSetManagementテーブルのカラム追加完了';

-- 既存データのデフォルト値設定（動的SQL使用）
PRINT '';
PRINT '既存データのデフォルト値設定開始...';

-- DataSetManagementテーブルの既存データ更新
IF EXISTS (SELECT * FROM DataSetManagement)
BEGIN
    -- ImportType の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'ImportType')
    BEGIN
        SET @sql = 'UPDATE DataSetManagement SET ImportType = ''IMPORT'' WHERE ImportType IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ ImportType のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- RecordCount の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'RecordCount')
    BEGIN
        SET @sql = 'UPDATE DataSetManagement SET RecordCount = 0 WHERE RecordCount IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ RecordCount のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- TotalRecordCount の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'TotalRecordCount')
    BEGIN
        SET @sql = 'UPDATE DataSetManagement SET TotalRecordCount = 0 WHERE TotalRecordCount IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ TotalRecordCount のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- IsActive の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'IsActive')
    BEGIN
        SET @sql = 'UPDATE DataSetManagement SET IsActive = 1 WHERE IsActive IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ IsActive のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- IsArchived の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'IsArchived')
    BEGIN
        SET @sql = 'UPDATE DataSetManagement SET IsArchived = 0 WHERE IsArchived IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ IsArchived のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- CreatedBy の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'CreatedBy')
    BEGIN
        SET @sql = 'UPDATE DataSetManagement SET CreatedBy = ''system'' WHERE CreatedBy IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ CreatedBy のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- Department の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'Department')
    BEGIN
        SET @sql = 'UPDATE DataSetManagement SET Department = ''Unknown'' WHERE Department IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ Department のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    PRINT 'DataSetManagementテーブルの既存データ更新完了';
END
ELSE
BEGIN
    PRINT 'DataSetManagementテーブルにデータが存在しません';
END

-- DataSetsテーブルの既存データ更新
IF EXISTS (SELECT * FROM DataSets)
BEGIN
    -- RecordCount の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'RecordCount')
    BEGIN
        SET @sql = 'UPDATE DataSets SET RecordCount = 0 WHERE RecordCount IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ DataSets.RecordCount のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- CreatedAt の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'CreatedAt')
    BEGIN
        SET @sql = 'UPDATE DataSets SET CreatedAt = GETDATE() WHERE CreatedAt IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ DataSets.CreatedAt のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    -- UpdatedAt の更新
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSets') AND name = 'UpdatedAt')
    BEGIN
        SET @sql = 'UPDATE DataSets SET UpdatedAt = GETDATE() WHERE UpdatedAt IS NULL';
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ DataSets.UpdatedAt のデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    
    PRINT 'DataSetsテーブルの既存データ更新完了';
END
ELSE
BEGIN
    PRINT 'DataSetsテーブルにデータが存在しません';
END

PRINT '既存データのデフォルト値設定完了';

-- 完了メッセージ
PRINT '';
PRINT '=== 025_Fix_DataSets_Columns.sql 実行完了 ===';
PRINT 'DataSetsテーブルとDataSetManagementテーブルのカラム不足エラーが修正されました。';
PRINT 'import-folderコマンドが正常に動作するようになります。';

SET NOCOUNT OFF;