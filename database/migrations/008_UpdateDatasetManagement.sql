-- =============================================================================
-- マイグレーション: 008_UpdateDatasetManagement.sql
-- 説明: DatasetManagementテーブルのスキーマ不一致を解消
-- 作成日: 2025-07-12
-- =============================================================================

-- 1. ImportTypeカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') 
               AND name = 'ImportType')
BEGIN
    ALTER TABLE DatasetManagement
    ADD ImportType NVARCHAR(20) NOT NULL DEFAULT 'IMPORT';
END

-- 2. RecordCountカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') 
               AND name = 'RecordCount')
BEGIN
    ALTER TABLE DatasetManagement
    ADD RecordCount INT NOT NULL DEFAULT 0;
END

-- 3. IsActiveカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') 
               AND name = 'IsActive')
BEGIN
    ALTER TABLE DatasetManagement
    ADD IsActive BIT NOT NULL DEFAULT 1;
END

-- 4. IsArchivedカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') 
               AND name = 'IsArchived')
BEGIN
    ALTER TABLE DatasetManagement
    ADD IsArchived BIT NOT NULL DEFAULT 0;
END

-- 5. ParentDataSetIdカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') 
               AND name = 'ParentDataSetId')
BEGIN
    ALTER TABLE DatasetManagement
    ADD ParentDataSetId NVARCHAR(100) NULL;
END

-- 6. Notesカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') 
               AND name = 'Notes')
BEGIN
    ALTER TABLE DatasetManagement
    ADD Notes NVARCHAR(MAX) NULL;
END

-- 7. CreatedByカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') 
               AND name = 'CreatedBy')
BEGIN
    ALTER TABLE DatasetManagement
    ADD CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'System';
END

-- 8. 既存データのImportType設定（ProcessTypeに基づいて）
UPDATE DatasetManagement
SET ImportType = CASE 
    WHEN ProcessType = 'INITIAL_INVENTORY' THEN 'INIT'
    WHEN ProcessType = 'CARRYOVER' THEN 'CARRYOVER'
    ELSE 'IMPORT'
END
WHERE ImportType = 'IMPORT';

-- 9. RecordCountの設定（TotalRecordCountから）
UPDATE DatasetManagement
SET RecordCount = ISNULL(TotalRecordCount, 0)
WHERE RecordCount = 0 AND TotalRecordCount > 0;

-- 10. 実行結果の確認
SELECT 
    COUNT(*) as TotalRecords,
    SUM(CASE WHEN ImportType = 'INIT' THEN 1 ELSE 0 END) as InitRecords,
    SUM(CASE WHEN ImportType = 'IMPORT' THEN 1 ELSE 0 END) as ImportRecords,
    SUM(CASE WHEN ImportType = 'CARRYOVER' THEN 1 ELSE 0 END) as CarryoverRecords
FROM DatasetManagement;

PRINT '===== 008_UpdateDatasetManagement.sql 実行完了 =====';
PRINT 'DatasetManagementテーブルのスキーマを更新しました。';