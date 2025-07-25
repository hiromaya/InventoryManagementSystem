-- Phase 1テスト用: クリーンアップ前の状態確認
-- 実行日: 2025-07-24

PRINT '=== Phase 1テスト: クリーンアップ前の状態確認 ==='

-- 1. 現在の重複状況を確認
PRINT '*** 重複データの確認 ***'
SELECT 
    JobDate, 
    ProcessType, 
    COUNT(*) as DuplicateCount,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as ActiveCount,
    COUNT(CASE WHEN IsActive = 0 THEN 1 END) as InactiveCount
FROM DataSetManagement 
GROUP BY JobDate, ProcessType 
HAVING COUNT(*) > 1
ORDER BY JobDate, ProcessType;

-- 2. JobDate = 2025-06-02の詳細確認
PRINT ''
PRINT '*** JobDate = 2025-06-02の詳細状況 ***'
SELECT 
    ProcessType,
    DataSetId,
    Status,
    IsActive,
    CreatedAt,
    CASE WHEN IsActive = 1 THEN 'ACTIVE' ELSE 'INACTIVE' END as ActiveStatus
FROM DataSetManagement 
WHERE JobDate = '2025-06-02'
ORDER BY ProcessType, CreatedAt DESC;

-- 3. 総統計
PRINT ''
PRINT '*** 総統計 ***'
SELECT 
    COUNT(*) as TotalRecords,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as ActiveRecords,
    COUNT(CASE WHEN IsActive = 0 THEN 1 END) as InactiveRecords,
    COUNT(DISTINCT CONCAT(CAST(JobDate as VARCHAR), '_', ProcessType)) as UniqueJobDateProcessType
FROM DataSetManagement;