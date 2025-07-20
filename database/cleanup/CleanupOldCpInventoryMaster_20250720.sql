-- CP在庫マスタ古いDataSetId削除スクリプト
-- 作成日時: 2025-07-20 17:24:00
-- 目的: 5152件アンマッチ問題の緊急修正

-- 実行前の状況確認
PRINT '=== 実行前の状況確認 ===';
SELECT 
    DataSetId,
    COUNT(*) as RecordCount,
    MIN(CreatedAt) as EarliestCreated,
    MAX(CreatedAt) as LatestCreated
FROM CpInventoryMaster
GROUP BY DataSetId
ORDER BY COUNT(*) DESC;

PRINT '';
PRINT '=== 削除対象の特定 ===';

-- 最新のDataSetId（レコード数が最も少ない = 最新）を特定
DECLARE @LatestDataSetId NVARCHAR(100);
SELECT TOP 1 @LatestDataSetId = DataSetId
FROM CpInventoryMaster
GROUP BY DataSetId
ORDER BY COUNT(*) ASC, MAX(CreatedAt) DESC;

PRINT '保持するDataSetId: ' + ISNULL(@LatestDataSetId, 'NULL');

-- 削除対象を表示
SELECT 
    DataSetId,
    COUNT(*) as WillBeDeleted
FROM CpInventoryMaster
WHERE DataSetId != @LatestDataSetId
GROUP BY DataSetId;

PRINT '';
PRINT '=== バックアップ作成 ===';

-- バックアップテーブル作成
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CpInventoryMaster_Backup_20250720')
BEGIN
    SELECT * 
    INTO CpInventoryMaster_Backup_20250720
    FROM CpInventoryMaster;
    
    PRINT 'バックアップテーブル CpInventoryMaster_Backup_20250720 を作成しました';
END
ELSE
BEGIN
    PRINT 'バックアップテーブルは既に存在します';
END

PRINT '';
PRINT '=== 古いDataSetIdの削除実行 ===';

-- 削除実行
DECLARE @DeletedCount INT;

DELETE FROM CpInventoryMaster 
WHERE DataSetId != @LatestDataSetId;

SET @DeletedCount = @@ROWCOUNT;

PRINT '削除完了: ' + CAST(@DeletedCount AS NVARCHAR(10)) + ' 件';

PRINT '';
PRINT '=== 実行後の状況確認 ===';

-- 実行後の確認
SELECT 
    DataSetId,
    COUNT(*) as RecordCount,
    MIN(CreatedAt) as EarliestCreated,
    MAX(CreatedAt) as LatestCreated
FROM CpInventoryMaster
GROUP BY DataSetId
ORDER BY COUNT(*) DESC;

-- 期待結果のメッセージ
PRINT '';
PRINT '=== 期待結果 ===';
PRINT '1. CP在庫マスタは1つのDataSetIdのみ（約158件）';
PRINT '2. 次回アンマッチリスト実行で正常な件数に修正される';
PRINT '3. バックアップテーブルで復旧可能';

PRINT '';
PRINT '=== 次のステップ ===';
PRINT '1. dotnet run -- unmatch-list 2025-06-02 を実行';
PRINT '2. アンマッチリスト件数の正常化を確認';
PRINT '3. 問題が解決しない場合はバックアップから復旧';

-- 監査ログ記録
INSERT INTO AuditLogs (
    TableName, 
    Operation, 
    RecordId, 
    Changes, 
    CreatedBy, 
    CreatedAt
)
VALUES (
    'CpInventoryMaster',
    'BULK_DELETE',
    'CLEANUP_OLD_DATASETS',
    'Deleted ' + CAST(@DeletedCount AS NVARCHAR(10)) + ' old DataSetId records. Kept: ' + ISNULL(@LatestDataSetId, 'NULL'),
    'SYSTEM_CLEANUP',
    GETDATE()
);

PRINT 'クリーンアップ完了 - 監査ログに記録しました';