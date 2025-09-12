-- =============================================
-- Windows環境SQL Server調査用クエリ
-- ProductAccount_StoredProcedure_Investigation
-- =============================================

-- 1. データベース接続確認
SELECT 
    DB_NAME() as CurrentDatabase,
    GETDATE() as CurrentDateTime,
    SYSTEM_USER as CurrentUser,
    @@VERSION as SQLServerVersion;

-- 2. ストアドプロシージャsp_CreateProductLedgerDataの存在確認
SELECT 
    name AS ProcedureName,
    object_id AS ObjectId,
    create_date AS CreatedDate,
    modify_date AS ModifiedDate,
    type_desc AS ObjectType,
    is_ms_shipped AS IsSystemProcedure
FROM sys.procedures 
WHERE name = 'sp_CreateProductLedgerData'
ORDER BY name;

-- 3. すべてのストアドプロシージャリスト
SELECT 
    name AS ProcedureName,
    create_date AS CreatedDate,
    modify_date AS ModifiedDate,
    type_desc AS ObjectType
FROM sys.procedures 
WHERE is_ms_shipped = 0  -- ユーザー定義のみ
ORDER BY name;

-- 4. 参照されるテーブルの存在確認
SELECT 
    table_name AS TableName,
    table_schema AS SchemaName,
    table_type AS TableType
FROM INFORMATION_SCHEMA.TABLES
WHERE table_name IN (
    'SalesVouchers', 'PurchaseVouchers', 'InventoryAdjustments', 'CpInventoryMaster'
)
ORDER BY table_name;

-- 5. すべてのユーザーテーブル一覧
SELECT 
    table_schema AS SchemaName,
    table_name AS TableName,
    table_type AS TableType
FROM INFORMATION_SCHEMA.TABLES
WHERE table_type = 'BASE TABLE'
ORDER BY table_name;

-- 6. マイグレーション履歴の確認
IF OBJECT_ID('__SchemaVersions') IS NOT NULL
BEGIN
    SELECT 
        MigrationId,
        AppliedDate,
        AppliedBy,
        ExecutionTimeMs
    FROM __SchemaVersions
    WHERE MigrationId LIKE '%procedures%' 
       OR MigrationId LIKE '%sp_CreateProductLedgerData%'
    ORDER BY AppliedDate DESC;
END
ELSE
BEGIN
    SELECT 'Migration history table does not exist' as Warning;
END

-- 7. 全マイグレーション履歴（最新10件）
IF OBJECT_ID('__SchemaVersions') IS NOT NULL
BEGIN
    SELECT TOP 10
        MigrationId,
        AppliedDate,
        AppliedBy,
        ExecutionTimeMs
    FROM __SchemaVersions
    ORDER BY AppliedDate DESC;
END

-- 8. エラーログからの情報収集（可能であれば）
SELECT 
    OBJECT_NAME(object_id) AS ObjectName,
    name AS ProcedureName,
    definition AS ProcedureDefinition
FROM sys.sql_modules sm
INNER JOIN sys.procedures p ON sm.object_id = p.object_id
WHERE p.name = 'sp_CreateProductLedgerData';

-- 9. 権限確認
SELECT 
    dp.name AS PrincipalName,
    dp.type_desc AS PrincipalType,
    o.name AS ObjectName,
    p.permission_name,
    p.state_desc AS PermissionState
FROM sys.database_permissions p
LEFT JOIN sys.objects o ON p.major_id = o.object_id
LEFT JOIN sys.database_principals dp ON p.grantee_principal_id = dp.principal_id
WHERE o.name = 'sp_CreateProductLedgerData' OR o.name IS NULL
ORDER BY dp.name;

-- 10. データベース内のすべてのオブジェクトの確認
SELECT 
    type_desc AS ObjectType,
    COUNT(*) AS ObjectCount
FROM sys.objects
WHERE is_ms_shipped = 0  -- ユーザー定義のみ
GROUP BY type_desc
ORDER BY type_desc;

-- 11. proceduresフォルダ関連のマイグレーション履歴詳細
IF OBJECT_ID('__SchemaVersions') IS NOT NULL
BEGIN
    SELECT 
        MigrationId,
        AppliedDate,
        AppliedBy,
        ExecutionTimeMs,
        CASE 
            WHEN MigrationId LIKE 'procedures/%' THEN 'YES'
            ELSE 'NO'
        END as IsProcedureScript
    FROM __SchemaVersions
    WHERE MigrationId LIKE '%procedure%' OR MigrationId LIKE '%sp_%'
    ORDER BY AppliedDate;
END

-- 12. SQL Server コンフィグレーション確認
SELECT 
    name AS ConfigurationName,
    value_in_use AS CurrentValue,
    description AS Description
FROM sys.configurations
WHERE name IN ('clr enabled', 'database compatibility level', 'user connections')
ORDER BY name;