-- DataSetManagement統合検証スクリプト
-- 実行日: 2025-07-18

PRINT '================================';
PRINT 'DataSetManagement統合検証';
PRINT '================================';
PRINT '';

-- 1. テーブル統計情報
PRINT '=== テーブル統計情報 ===';
SELECT 
    'DataSets' as TableName,
    COUNT(*) as RecordCount,
    COUNT(DISTINCT ProcessType) as ProcessTypes,
    COUNT(DISTINCT Status) as StatusTypes,
    MIN(CreatedAt) as OldestRecord,
    MAX(CreatedAt) as NewestRecord
FROM DataSets

UNION ALL

SELECT 
    'DataSetManagement' as TableName,
    COUNT(*) as RecordCount,
    COUNT(DISTINCT ProcessType) as ProcessTypes,
    COUNT(DISTINCT Status) as StatusTypes,
    MIN(CreatedAt) as OldestRecord,
    MAX(CreatedAt) as NewestRecord
FROM DataSetManagement;

-- 2. 外部キー整合性確認
PRINT '';
PRINT '=== 外部キー整合性確認 ===';

-- 売上伝票
SELECT 
    'SalesVouchers' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT DataSetId) as UniqueDataSets,
    SUM(CASE WHEN dsm.DataSetId IS NULL THEN 1 ELSE 0 END) as OrphanRecords
FROM SalesVouchers sv
LEFT JOIN DataSetManagement dsm ON sv.DataSetId = dsm.DataSetId;

-- 仕入伝票
SELECT 
    'PurchaseVouchers' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT DataSetId) as UniqueDataSets,
    SUM(CASE WHEN dsm.DataSetId IS NULL THEN 1 ELSE 0 END) as OrphanRecords
FROM PurchaseVouchers pv
LEFT JOIN DataSetManagement dsm ON pv.DataSetId = dsm.DataSetId;

-- 在庫調整
SELECT 
    'InventoryAdjustments' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT DataSetId) as UniqueDataSets,
    SUM(CASE WHEN dsm.DataSetId IS NULL THEN 1 ELSE 0 END) as OrphanRecords
FROM InventoryAdjustments ia
LEFT JOIN DataSetManagement dsm ON ia.DataSetId = dsm.DataSetId;

-- 3. データセット同期状況
PRINT '';
PRINT '=== データセット同期状況 ===';

SELECT 
    CASE 
        WHEN ds.Id IS NULL THEN 'DataSetManagementのみ'
        WHEN dsm.DataSetId IS NULL THEN 'DataSetsのみ'
        ELSE '両方に存在'
    END as SyncStatus,
    COUNT(*) as RecordCount
FROM (
    SELECT Id FROM DataSets
    UNION
    SELECT DataSetId FROM DataSetManagement
) all_ids
LEFT JOIN DataSets ds ON all_ids.Id = ds.Id
LEFT JOIN DataSetManagement dsm ON all_ids.Id = dsm.DataSetId
GROUP BY 
    CASE 
        WHEN ds.Id IS NULL THEN 'DataSetManagementのみ'
        WHEN dsm.DataSetId IS NULL THEN 'DataSetsのみ'
        ELSE '両方に存在'
    END;

-- 4. ステータス分布
PRINT '';
PRINT '=== ステータス分布 ===';

-- DataSets
SELECT 
    'DataSets' as TableName,
    Status,
    COUNT(*) as RecordCount
FROM DataSets
GROUP BY Status
ORDER BY Status;

-- DataSetManagement
SELECT 
    'DataSetManagement' as TableName,
    CASE 
        WHEN Status IS NOT NULL THEN Status
        WHEN IsActive = 1 AND IsArchived = 0 THEN 'Active'
        WHEN IsArchived = 1 THEN 'Archived'
        ELSE 'Unknown'
    END as Status,
    COUNT(*) as RecordCount
FROM DataSetManagement
GROUP BY 
    CASE 
        WHEN Status IS NOT NULL THEN Status
        WHEN IsActive = 1 AND IsArchived = 0 THEN 'Active'
        WHEN IsArchived = 1 THEN 'Archived'
        ELSE 'Unknown'
    END
ORDER BY Status;

-- 5. 最近のデータセット（直近10件）
PRINT '';
PRINT '=== 最近作成されたデータセット（直近10件） ===';

SELECT TOP 10
    ds.Id as DataSetId,
    ds.Name,
    ds.ProcessType,
    ds.Status as DSStatus,
    dsm.Status as DSMStatus,
    ds.JobDate,
    ds.RecordCount,
    ds.CreatedAt,
    CASE 
        WHEN dsm.DataSetId IS NULL THEN '未同期'
        ELSE '同期済'
    END as SyncStatus
FROM DataSets ds
LEFT JOIN DataSetManagement dsm ON ds.Id = dsm.DataSetId
ORDER BY ds.CreatedAt DESC;

PRINT '';
PRINT '================================';
PRINT '検証完了';
PRINT '================================';