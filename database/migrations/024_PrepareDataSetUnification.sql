-- =====================================================
-- Phase 1: DataSetManagement統一のための準備
-- 作成日: 2025-01-16
-- 目的: 既存のDataSetsデータをDataSetManagementに移行
-- =====================================================

-- 開始メッセージ
PRINT '=========================================='
PRINT 'DataSet統一準備スクリプト開始'
PRINT '実行日時: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT '=========================================='

-- 移行前のデータ件数確認
DECLARE @DataSetsCount INT
DECLARE @DataSetManagementCount INT

SELECT @DataSetsCount = COUNT(*) FROM DataSets
SELECT @DataSetManagementCount = COUNT(*) FROM DataSetManagement

PRINT '移行前のデータ件数:'
PRINT '  DataSets: ' + CAST(@DataSetsCount AS NVARCHAR(10)) + '件'
PRINT '  DataSetManagement: ' + CAST(@DataSetManagementCount AS NVARCHAR(10)) + '件'
PRINT ''

-- =====================================================
-- Phase 1: 既存データの同期
-- DataSetsにあってDataSetManagementにないデータを移行
-- =====================================================

PRINT 'DataSetsからDataSetManagementへの既存データ移行開始...'

BEGIN TRANSACTION

BEGIN TRY
    -- 移行対象データの確認
    DECLARE @MigrationCount INT
    SELECT @MigrationCount = COUNT(*)
    FROM DataSets ds
    WHERE NOT EXISTS (
        SELECT 1 FROM DataSetManagement dsm 
        WHERE dsm.DataSetId = ds.Id
    )
    
    PRINT '移行対象データ: ' + CAST(@MigrationCount AS NVARCHAR(10)) + '件'
    
    -- データ移行実行
    INSERT INTO DataSetManagement (
        DataSetId, 
        JobDate, 
        ProcessType, 
        ImportType, 
        RecordCount, 
        TotalRecordCount, 
        IsActive, 
        IsArchived, 
        Department, 
        CreatedAt, 
        CreatedBy, 
        Notes, 
        ImportedFiles
    )
    SELECT 
        ds.Id,
        ds.JobDate,
        -- ProcessTypeの変換（DataSets形式 → DataSetManagement形式）
        CASE 
            WHEN ds.ProcessType = 'Sales' THEN 'SALES'
            WHEN ds.ProcessType = 'Purchase' THEN 'PURCHASE'
            WHEN ds.ProcessType = 'Adjustment' THEN 'ADJUSTMENT'
            WHEN ds.ProcessType = 'Product' THEN 'PRODUCT'
            WHEN ds.ProcessType = 'Customer' THEN 'CUSTOMER'
            WHEN ds.ProcessType = 'Supplier' THEN 'SUPPLIER'
            WHEN ds.ProcessType = 'InitialInventory' THEN 'INITIAL_INVENTORY'
            WHEN ds.ProcessType = 'PreviousInventory' THEN 'PREVIOUS_INVENTORY'
            WHEN ds.ProcessType = 'DailyReport' THEN 'DAILY_REPORT'
            ELSE UPPER(REPLACE(ds.ProcessType, ' ', '_'))
        END,
        'IMPORT', -- デフォルト値（実際のインポートタイプは不明）
        ISNULL(ds.RecordCount, 0),
        ISNULL(ds.RecordCount, 0), -- TotalRecordCountも同じ値で初期化
        CASE 
            WHEN ds.Status = 'Completed' THEN 1 
            WHEN ds.Status = 'Processing' THEN 1
            ELSE 0 
        END, -- IsActive
        0, -- IsArchived（新規移行データはアーカイブされていない）
        ISNULL(ds.DepartmentCode, 'Unknown'),
        ISNULL(ds.CreatedDate, GETDATE()),
        'migration-script',
        -- Notesに元の情報を記録
        CONCAT(
            'Migrated from DataSets. ',
            'Original: ', ISNULL(ds.Name, 'No Name'), 
            CASE 
                WHEN ds.Status = 'Failed' AND ds.ErrorMessage IS NOT NULL 
                THEN ' | Status: Failed | Error: ' + ds.ErrorMessage 
                WHEN ds.Status = 'Failed'
                THEN ' | Status: Failed'
                ELSE ' | Status: ' + ISNULL(ds.Status, 'Unknown')
            END
        ),
        -- ImportedFilesをJSON配列形式で格納
        CASE 
            WHEN ds.FilePath IS NOT NULL AND LEN(LTRIM(RTRIM(ds.FilePath))) > 0 
            THEN CONCAT('["', REPLACE(REPLACE(ds.FilePath, '\', '\\'), '"', '\"'), '"]') 
            ELSE NULL 
        END
    FROM DataSets ds
    WHERE NOT EXISTS (
        SELECT 1 FROM DataSetManagement dsm 
        WHERE dsm.DataSetId = ds.Id
    )
    
    PRINT '移行完了: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + '件'
    
    -- 移行後のデータ件数確認
    SELECT @DataSetManagementCount = COUNT(*) FROM DataSetManagement
    PRINT ''
    PRINT '移行後のDataSetManagement件数: ' + CAST(@DataSetManagementCount AS NVARCHAR(10)) + '件'
    
    -- データ整合性チェック
    DECLARE @UnmatchedCount INT
    SELECT @UnmatchedCount = COUNT(*)
    FROM DataSets ds
    WHERE NOT EXISTS (
        SELECT 1 FROM DataSetManagement dsm 
        WHERE dsm.DataSetId = ds.Id
    )
    
    IF @UnmatchedCount > 0
    BEGIN
        PRINT ''
        PRINT '警告: ' + CAST(@UnmatchedCount AS NVARCHAR(10)) + '件のDataSetsレコードが移行されていません'
        RAISERROR('データ移行が不完全です', 16, 1)
    END
    
    COMMIT TRANSACTION
    
    PRINT ''
    PRINT '=========================================='
    PRINT 'DataSet統一準備スクリプト正常終了'
    PRINT '=========================================='
    
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION
    
    PRINT ''
    PRINT 'エラーが発生しました:'
    PRINT '  エラー番号: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10))
    PRINT '  エラーメッセージ: ' + ERROR_MESSAGE()
    PRINT '  エラー行: ' + CAST(ERROR_LINE() AS NVARCHAR(10))
    PRINT ''
    PRINT '=========================================='
    PRINT 'DataSet統一準備スクリプト異常終了'
    PRINT '=========================================='
    
    -- エラーを再スロー
    THROW
END CATCH

-- =====================================================
-- 移行結果の確認クエリ（手動実行用）
-- =====================================================

/*
-- DataSetsとDataSetManagementの対応確認
SELECT 
    ds.Id AS DataSetsId,
    dsm.DataSetId AS DataSetManagementId,
    ds.ProcessType AS DS_ProcessType,
    dsm.ProcessType AS DSM_ProcessType,
    ds.Status AS DS_Status,
    dsm.IsActive AS DSM_IsActive,
    ds.RecordCount AS DS_RecordCount,
    dsm.RecordCount AS DSM_RecordCount,
    ds.JobDate AS DS_JobDate,
    dsm.JobDate AS DSM_JobDate
FROM DataSets ds
LEFT JOIN DataSetManagement dsm ON ds.Id = dsm.DataSetId
ORDER BY ds.CreatedDate DESC

-- 移行されていないデータの確認
SELECT * FROM DataSets ds
WHERE NOT EXISTS (
    SELECT 1 FROM DataSetManagement dsm 
    WHERE dsm.DataSetId = ds.Id
)
*/