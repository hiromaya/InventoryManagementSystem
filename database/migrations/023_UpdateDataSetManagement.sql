-- =============================================================================
-- マイグレーション: 023_UpdateDataSetManagement.sql
-- 説明: DataSetManagementテーブルに不足しているカラムを追加
-- 作成日: 2025-07-15
-- 注意: テーブル名は大文字S（DataSetManagement）
--       動的SQLを使用してカラム参照エラーを回避
-- =============================================================================

USE InventoryManagementDB;
GO

-- 実行前の状態確認
PRINT '===== 023_UpdateDataSetManagement.sql 開始 =====';
PRINT '現在時刻: ' + CONVERT(VARCHAR, GETDATE(), 120);

-- テーブルの存在確認
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DataSetManagement')
BEGIN
    PRINT 'DataSetManagementテーブルが存在します。カラムを追加します。';
    
    DECLARE @sql NVARCHAR(MAX);
    DECLARE @updateCount INT;
    
    -- 1. ProcessTypeカラムの追加
    IF NOT EXISTS (SELECT * FROM sys.columns 
                   WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
                   AND name = 'ProcessType')
    BEGIN
        ALTER TABLE DataSetManagement
        ADD ProcessType NVARCHAR(50) NULL;
        PRINT '✓ ProcessTypeカラムを追加しました';
        
        -- デフォルト値の設定（動的SQL使用）
        SET @sql = '
        UPDATE DataSetManagement
        SET ProcessType = CASE 
            WHEN ImportType = ''INIT'' THEN ''INITIAL_INVENTORY''
            WHEN ImportType = ''CARRYOVER'' THEN ''CARRYOVER''
            WHEN ImportType = ''IMPORT'' THEN ''IMPORT''
            ELSE ImportType
        END
        WHERE ProcessType IS NULL';
        
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ ProcessTypeのデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    ELSE
    BEGIN
        PRINT '- ProcessTypeカラムは既に存在します';
    END

    -- 2. TotalRecordCountカラムの追加
    IF NOT EXISTS (SELECT * FROM sys.columns 
                   WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
                   AND name = 'TotalRecordCount')
    BEGIN
        ALTER TABLE DataSetManagement
        ADD TotalRecordCount INT NOT NULL DEFAULT 0;
        PRINT '✓ TotalRecordCountカラムを追加しました';
        
        -- RecordCountから値をコピー（動的SQL使用）
        SET @sql = '
        UPDATE DataSetManagement
        SET TotalRecordCount = RecordCount
        WHERE TotalRecordCount = 0 AND RecordCount > 0';
        
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ TotalRecordCountの初期値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    ELSE
    BEGIN
        PRINT '- TotalRecordCountカラムは既に存在します';
    END

    -- 3. ImportedFilesカラムの追加
    IF NOT EXISTS (SELECT * FROM sys.columns 
                   WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
                   AND name = 'ImportedFiles')
    BEGIN
        ALTER TABLE DataSetManagement
        ADD ImportedFiles NVARCHAR(MAX) NULL;
        PRINT '✓ ImportedFilesカラムを追加しました';
    END
    ELSE
    BEGIN
        PRINT '- ImportedFilesカラムは既に存在します';
    END

    -- 4. Departmentカラムの追加
    IF NOT EXISTS (SELECT * FROM sys.columns 
                   WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
                   AND name = 'Department')
    BEGIN
        ALTER TABLE DataSetManagement
        ADD Department NVARCHAR(50) NULL;
        PRINT '✓ Departmentカラムを追加しました';
        
        -- デフォルト値の設定（動的SQL使用）
        SET @sql = '
        UPDATE DataSetManagement
        SET Department = ''DeptA''
        WHERE Department IS NULL';
        
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ Departmentのデフォルト値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    ELSE
    BEGIN
        PRINT '- Departmentカラムは既に存在します';
    END

    -- 5. UpdatedAtカラムの追加
    IF NOT EXISTS (SELECT * FROM sys.columns 
                   WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
                   AND name = 'UpdatedAt')
    BEGIN
        ALTER TABLE DataSetManagement
        ADD UpdatedAt DATETIME2 NULL;
        PRINT '✓ UpdatedAtカラムを追加しました';
        
        -- 初期値としてCreatedAtの値を設定（動的SQL使用）
        SET @sql = '
        UPDATE DataSetManagement
        SET UpdatedAt = CreatedAt
        WHERE UpdatedAt IS NULL';
        
        EXEC sp_executesql @sql;
        SET @updateCount = @@ROWCOUNT;
        PRINT '  └ UpdatedAtの初期値を設定しました (' + CAST(@updateCount AS VARCHAR) + '件)';
    END
    ELSE
    BEGIN
        PRINT '- UpdatedAtカラムは既に存在します';
    END

END
ELSE
BEGIN
    PRINT 'エラー: DataSetManagementテーブルが存在しません。';
    PRINT '006_AddDataSetManagement.sqlを先に実行してください。';
END
GO

-- 最終的なカラム構成を確認（GOの後に配置）
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DataSetManagement')
BEGIN
    PRINT '';
    PRINT '=== 最終的なカラム構成 ===';
    SELECT 
        COLUMN_NAME as カラム名,
        DATA_TYPE as データ型,
        CHARACTER_MAXIMUM_LENGTH as 最大長,
        IS_NULLABLE as NULL許可,
        COLUMN_DEFAULT as デフォルト値
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DataSetManagement'
    ORDER BY ORDINAL_POSITION;
    
    -- レコード件数の確認
    DECLARE @totalCount INT;
    SELECT @totalCount = COUNT(*) FROM DataSetManagement;
    PRINT '';
    PRINT 'DataSetManagementテーブルの総レコード数: ' + CAST(@totalCount AS VARCHAR);
END

PRINT '';
PRINT '===== 023_UpdateDataSetManagement.sql 完了 =====';
GO