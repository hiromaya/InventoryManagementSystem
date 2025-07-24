-- DataSetId重複データクリーンアップスクリプト
-- 実行日: 2025-07-24
-- 目的: DataSetManagementテーブルの重複レコードを安全にクリーンアップ

PRINT '===================================================='
PRINT 'DataSetId重複データクリーンアップスクリプト'
PRINT 'Version: 1.0'
PRINT 'Date: 2025-07-24'
PRINT '===================================================='

-- 1. 現在の重複状況を確認
PRINT ''
PRINT '=== 重複データの確認 ==='
SELECT 
    JobDate, 
    ProcessType, 
    COUNT(*) as DuplicateCount,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as ActiveCount,
    COUNT(CASE WHEN IsActive = 0 THEN 1 END) as InactiveCount,
    STRING_AGG(CAST(DataSetId as NVARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY CreatedAt DESC) as DataSetIds
FROM DataSetManagement 
GROUP BY JobDate, ProcessType 
HAVING COUNT(*) > 1
ORDER BY JobDate, ProcessType;

PRINT ''
PRINT '=== 総データ数 ==='
SELECT 
    COUNT(*) as TotalRecords,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as ActiveRecords,
    COUNT(CASE WHEN IsActive = 0 THEN 1 END) as InactiveRecords
FROM DataSetManagement;

-- 2. バックアップテーブル作成（安全のため）
PRINT ''
PRINT '=== バックアップテーブル作成 ==='
IF OBJECT_ID('DataSetManagement_Backup_20250724', 'U') IS NOT NULL
BEGIN
    DROP TABLE DataSetManagement_Backup_20250724;
    PRINT '既存のバックアップテーブルを削除しました'
END

SELECT * INTO DataSetManagement_Backup_20250724
FROM DataSetManagement;

DECLARE @BackupCount INT
SELECT @BackupCount = COUNT(*) FROM DataSetManagement_Backup_20250724;
PRINT 'バックアップテーブル作成完了: DataSetManagement_Backup_20250724 (' + CAST(@BackupCount as VARCHAR) + ' records)'

-- 3. 重複データの無効化（最新以外を無効化）
PRINT ''
PRINT '=== 重複データの無効化処理開始 ==='
BEGIN TRANSACTION CleanupTransaction;

BEGIN TRY
    -- 重複データを特定し、最新以外を無効化
    UPDATE dm
    SET 
        IsActive = 0,
        DeactivatedAt = GETDATE(),
        DeactivatedBy = 'SYSTEM_CLEANUP_20250724',
        Status = CASE 
            WHEN Status = 'Processing' THEN 'Cancelled'
            WHEN Status IS NULL THEN 'Cancelled'
            ELSE Status 
        END,
        Notes = ISNULL(Notes, '') + ' | Deactivated by cleanup script on ' + CONVERT(varchar, GETDATE(), 120)
    FROM DataSetManagement dm
    WHERE EXISTS (
        SELECT 1
        FROM (
            SELECT 
                JobDate,
                ProcessType,
                MAX(CreatedAt) as MaxCreatedAt,
                COUNT(*) as RecordCount
            FROM DataSetManagement
            WHERE IsActive = 1
            GROUP BY JobDate, ProcessType
            HAVING COUNT(*) > 1
        ) latest
        WHERE dm.JobDate = latest.JobDate
          AND dm.ProcessType = latest.ProcessType
          AND dm.CreatedAt < latest.MaxCreatedAt
          AND dm.IsActive = 1
    );

    DECLARE @DeactivatedCount INT = @@ROWCOUNT;
    PRINT '無効化されたレコード数: ' + CAST(@DeactivatedCount as varchar);

    -- 4. 結果確認
    PRINT ''
    PRINT '=== クリーンアップ後の状態確認 ==='
    
    -- アクティブなレコードの重複確認
    PRINT '*** アクティブなレコードの重複状況 ***'
    SELECT 
        JobDate, 
        ProcessType, 
        COUNT(*) as ActiveCount,
        STRING_AGG(CAST(DataSetId as NVARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY CreatedAt DESC) as ActiveDataSetIds
    FROM DataSetManagement 
    WHERE IsActive = 1
    GROUP BY JobDate, ProcessType 
    ORDER BY JobDate, ProcessType;
    
    -- 残り重複の確認
    DECLARE @RemainingDuplicates INT
    SELECT @RemainingDuplicates = COUNT(*)
    FROM (
        SELECT JobDate, ProcessType
        FROM DataSetManagement 
        WHERE IsActive = 1
        GROUP BY JobDate, ProcessType 
        HAVING COUNT(*) > 1
    ) duplicates;
    
    IF @RemainingDuplicates = 0
    BEGIN
        PRINT ''
        PRINT '✅ 重複解消完了: アクティブなレコードに重複はありません'
        COMMIT TRANSACTION CleanupTransaction;
        PRINT '✅ トランザクションをコミットしました'
    END
    ELSE
    BEGIN
        PRINT ''
        PRINT '⚠️ 警告: ' + CAST(@RemainingDuplicates as VARCHAR) + ' 個のJobDate+ProcessTypeでまだ重複があります'
        PRINT '詳細を確認してください'
        ROLLBACK TRANSACTION CleanupTransaction;
        PRINT '❌ トランザクションをロールバックしました'
    END

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION CleanupTransaction;
    
    PRINT '❌ エラーが発生しました:'
    PRINT 'Error Number: ' + CAST(ERROR_NUMBER() as VARCHAR)
    PRINT 'Error Message: ' + ERROR_MESSAGE()
    PRINT 'トランザクションをロールバックしました'
END CATCH

-- 5. 最終確認と統計情報
PRINT ''
PRINT '=== 最終統計情報 ==='
SELECT 
    'Total Records' as Category,
    COUNT(*) as Count
FROM DataSetManagement
UNION ALL
SELECT 
    'Active Records' as Category,
    COUNT(*) as Count
FROM DataSetManagement
WHERE IsActive = 1
UNION ALL
SELECT 
    'Inactive Records' as Category,
    COUNT(*) as Count
FROM DataSetManagement
WHERE IsActive = 0
UNION ALL
SELECT 
    'Backup Records' as Category,
    COUNT(*) as Count
FROM DataSetManagement_Backup_20250724;

PRINT ''
PRINT '=== JobDate別の統計 ==='
SELECT 
    JobDate,
    COUNT(*) as TotalRecords,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as ActiveRecords,
    COUNT(CASE WHEN IsActive = 0 THEN 1 END) as InactiveRecords
FROM DataSetManagement
GROUP BY JobDate
ORDER BY JobDate DESC;

PRINT ''
PRINT '===================================================='
PRINT 'クリーンアップスクリプト実行完了'
PRINT 'バックアップ: DataSetManagement_Backup_20250724'
PRINT '===================================================='