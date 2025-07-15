-- ===================================================
-- マイグレーション履歴テーブルの検証スクリプト
-- ===================================================

USE InventoryManagementDB;
GO

-- 1. マイグレーション履歴テーブルの存在確認
IF OBJECT_ID('dbo.__SchemaVersions') IS NOT NULL
    PRINT '✅ マイグレーション履歴テーブル [__SchemaVersions] が存在します'
ELSE
    PRINT '❌ マイグレーション履歴テーブル [__SchemaVersions] が存在しません'
GO

-- 2. 適用されたマイグレーションの一覧表示
PRINT '===== 適用されたマイグレーション一覧 ====='
SELECT 
    MigrationId,
    AppliedDate,
    AppliedBy,
    ExecutionTimeMs
FROM __SchemaVersions
ORDER BY AppliedDate ASC;
GO

-- 3. 期待されるマイグレーション一覧との比較
PRINT '===== 期待されるマイグレーション vs 実際 ====='
DECLARE @ExpectedMigrations TABLE (
    MigrationId NVARCHAR(255),
    ExpectedOrder INT
);

INSERT INTO @ExpectedMigrations VALUES
('000_CreateMigrationHistory.sql', 1),
('006_AddDataSetManagement.sql', 2),
('008_AddUnmatchOptimizationIndexes.sql', 3),
('009_CreateInitialInventoryStagingTable.sql', 4),
('010_AddPersonInChargeAndAveragePrice.sql', 5),
('012_AddGrossProfitColumnToSalesVouchers.sql', 6),
('014_AddMissingColumnsToInventoryMaster.sql', 7),
('017_Cleanup_Duplicate_InventoryMaster.sql', 8),
('020_Fix_MergeInventoryMaster_OutputClause.sql', 9),
('021_VerifyInventoryMasterSchema.sql', 10);

SELECT 
    e.MigrationId,
    e.ExpectedOrder,
    CASE 
        WHEN s.MigrationId IS NOT NULL THEN '✅ 適用済み'
        ELSE '❌ 未適用'
    END AS Status,
    s.AppliedDate
FROM @ExpectedMigrations e
LEFT JOIN __SchemaVersions s ON e.MigrationId = s.MigrationId
ORDER BY e.ExpectedOrder;
GO

-- 4. 予期しないマイグレーションの検出
PRINT '===== 予期しないマイグレーション ====='
DECLARE @ExpectedMigrations2 TABLE (
    MigrationId NVARCHAR(255)
);

INSERT INTO @ExpectedMigrations2 VALUES
('000_CreateMigrationHistory.sql'),
('006_AddDataSetManagement.sql'),
('008_AddUnmatchOptimizationIndexes.sql'),
('009_CreateInitialInventoryStagingTable.sql'),
('010_AddPersonInChargeAndAveragePrice.sql'),
('012_AddGrossProfitColumnToSalesVouchers.sql'),
('014_AddMissingColumnsToInventoryMaster.sql'),
('017_Cleanup_Duplicate_InventoryMaster.sql'),
('020_Fix_MergeInventoryMaster_OutputClause.sql'),
('021_VerifyInventoryMasterSchema.sql');

SELECT 
    s.MigrationId,
    s.AppliedDate,
    '⚠️ 予期しないマイグレーション' AS Warning
FROM __SchemaVersions s
LEFT JOIN @ExpectedMigrations2 e ON s.MigrationId = e.MigrationId
WHERE e.MigrationId IS NULL;
GO

-- 5. テーブル構造の検証
PRINT '===== 主要テーブルの存在確認 ====='
SELECT 
    TABLE_NAME,
    CASE 
        WHEN OBJECT_ID('dbo.' + TABLE_NAME) IS NOT NULL THEN '✅ 存在'
        ELSE '❌ 存在しない'
    END AS Status
FROM (VALUES 
    ('InventoryMaster'),
    ('CpInventoryMaster'),
    ('SalesVouchers'),
    ('PurchaseVouchers'),
    ('InventoryAdjustments'),
    ('DataSets'),
    ('DataSetManagement'),
    ('InitialInventoryStaging')
) AS Tables(TABLE_NAME);
GO

-- 6. 重要なカラムの存在確認
PRINT '===== InventoryMasterテーブルの重要カラム確認 ====='
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventoryMaster'
AND COLUMN_NAME IN (
    'IsActive',
    'ParentDataSetId',
    'ImportType',
    'PersonInChargeCode',
    'AveragePrice',
    'DailyGrossProfit',
    'PreviousMonthQuantity',
    'PreviousMonthAmount'
)
ORDER BY COLUMN_NAME;
GO

-- 7. インデックスの存在確認
PRINT '===== 重要なインデックスの存在確認 ====='
SELECT 
    i.name AS IndexName,
    t.name AS TableName,
    CASE 
        WHEN i.name IS NOT NULL THEN '✅ 存在'
        ELSE '❌ 存在しない'
    END AS Status
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name IN (
    'IX_InventoryMaster_DataSetId_IsActive',
    'IX_InventoryMaster_Active_Filtered',
    'IX_InventoryMaster_PersonInChargeCode'
);
GO

-- 8. マイグレーション実行統計
PRINT '===== マイグレーション実行統計 ====='
SELECT 
    COUNT(*) AS TotalMigrations,
    MIN(AppliedDate) AS FirstMigration,
    MAX(AppliedDate) AS LastMigration,
    AVG(ExecutionTimeMs) AS AverageExecutionTime,
    MAX(ExecutionTimeMs) AS MaxExecutionTime
FROM __SchemaVersions;
GO

PRINT '===== 検証完了 ====='