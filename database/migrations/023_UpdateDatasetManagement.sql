-- =============================================================================
-- マイグレーション: 023_UpdateDataSetManagement.sql
-- 説明: DataSetManagementテーブルのスキーマ不一致を解消
-- 作成日: 2025-07-12
-- 更新: 2025-07-15 - 不足カラムの追加処理を追加
-- =============================================================================

-- 0-1. ProcessTypeカラムの追加（存在しない場合）- 他のカラムが依存するため最初に追加
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'ProcessType')
BEGIN
    ALTER TABLE DataSetManagement
    ADD ProcessType NVARCHAR(50) NULL;
    
    -- デフォルト値の設定
    UPDATE DataSetManagement
    SET ProcessType = CASE 
        WHEN ImportType = 'INIT' THEN 'INITIAL_INVENTORY'
        WHEN ImportType = 'CARRYOVER' THEN 'CARRYOVER'
        ELSE 'IMPORT'
    END
    WHERE ProcessType IS NULL;
END

-- 0-2. TotalRecordCountカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'TotalRecordCount')
BEGIN
    ALTER TABLE DataSetManagement
    ADD TotalRecordCount INT NOT NULL DEFAULT 0;
    
    -- 既存のRecordCountから値をコピー（RecordCountが既に存在する場合）
    IF EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'RecordCount')
    BEGIN
        UPDATE DataSetManagement
        SET TotalRecordCount = RecordCount
        WHERE TotalRecordCount = 0 AND RecordCount > 0;
    END
END

-- 0-3. Departmentカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'Department')
BEGIN
    ALTER TABLE DataSetManagement
    ADD Department NVARCHAR(50) NULL;
    
    -- デフォルト値の設定
    UPDATE DataSetManagement
    SET Department = 'DeptA'
    WHERE Department IS NULL;
END

-- 0-4. ImportedFilesカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'ImportedFiles')
BEGIN
    ALTER TABLE DataSetManagement
    ADD ImportedFiles NVARCHAR(MAX) NULL;
END

-- 0-5. UpdatedAtカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE DataSetManagement
    ADD UpdatedAt DATETIME2 NULL;
    
    -- 初期値としてCreatedAtの値を設定
    UPDATE DataSetManagement
    SET UpdatedAt = CreatedAt
    WHERE UpdatedAt IS NULL;
END

-- 1. ImportTypeカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'ImportType')
BEGIN
    ALTER TABLE DataSetManagement
    ADD ImportType NVARCHAR(20) NOT NULL DEFAULT 'IMPORT';
END

-- 2. RecordCountカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'RecordCount')
BEGIN
    ALTER TABLE DataSetManagement
    ADD RecordCount INT NOT NULL DEFAULT 0;
END

-- 3. IsActiveカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'IsActive')
BEGIN
    ALTER TABLE DataSetManagement
    ADD IsActive BIT NOT NULL DEFAULT 1;
END

-- 4. IsArchivedカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'IsArchived')
BEGIN
    ALTER TABLE DataSetManagement
    ADD IsArchived BIT NOT NULL DEFAULT 0;
END

-- 5. ParentDataSetIdカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'ParentDataSetId')
BEGIN
    ALTER TABLE DataSetManagement
    ADD ParentDataSetId NVARCHAR(100) NULL;
END

-- 6. Notesカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'Notes')
BEGIN
    ALTER TABLE DataSetManagement
    ADD Notes NVARCHAR(MAX) NULL;
END

-- 7. CreatedByカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'CreatedBy')
BEGIN
    ALTER TABLE DataSetManagement
    ADD CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'System';
END

-- 8. 既存データのImportType設定（ProcessTypeに基づいて）
-- 注意: この処理は不要。ProcessTypeは既に0-1で設定済み
-- UPDATE DataSetManagement
-- SET ImportType = CASE 
--     WHEN ProcessType = 'INITIAL_INVENTORY' THEN 'INIT'
--     WHEN ProcessType = 'CARRYOVER' THEN 'CARRYOVER'
--     ELSE 'IMPORT'
-- END
-- WHERE ImportType = 'IMPORT';

-- 9. RecordCountの設定（TotalRecordCountから）
-- 注意: この処理は不要。TotalRecordCountは既に0-2で設定済み
-- UPDATE DataSetManagement
-- SET RecordCount = ISNULL(TotalRecordCount, 0)
-- WHERE RecordCount = 0 AND TotalRecordCount > 0;

-- 10. 実行結果の確認
SELECT 
    COUNT(*) as TotalRecords,
    SUM(CASE WHEN ImportType = 'INIT' THEN 1 ELSE 0 END) as InitRecords,
    SUM(CASE WHEN ImportType = 'IMPORT' THEN 1 ELSE 0 END) as ImportRecords,
    SUM(CASE WHEN ImportType = 'CARRYOVER' THEN 1 ELSE 0 END) as CarryoverRecords
FROM DataSetManagement;

PRINT '===== 023_UpdateDataSetManagement.sql 実行完了 =====';
PRINT 'DataSetManagementテーブルのスキーマを更新しました。';
PRINT '追加されたカラム: ProcessType, TotalRecordCount, Department, ImportedFiles, UpdatedAt';