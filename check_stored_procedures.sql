-- ストアドプロシージャの存在確認
USE InventoryManagementDB;

PRINT '=== ストアドプロシージャ存在確認 ===';

-- 1. UN在庫マスタ関連のストアドプロシージャ確認
PRINT '';
PRINT '1. UN在庫マスタ関連ストアドプロシージャ:';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateUnInventoryFromInventoryMaster')
    PRINT '✅ sp_CreateUnInventoryFromInventoryMaster: 存在';
ELSE
    PRINT '❌ sp_CreateUnInventoryFromInventoryMaster: 未作成';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AggregateUnInventorySalesData')
    PRINT '✅ sp_AggregateUnInventorySalesData: 存在';
ELSE
    PRINT '❌ sp_AggregateUnInventorySalesData: 未作成';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AggregateUnInventoryPurchaseData')
    PRINT '✅ sp_AggregateUnInventoryPurchaseData: 存在';
ELSE
    PRINT '❌ sp_AggregateUnInventoryPurchaseData: 未作成';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AggregateUnInventoryAdjustmentData')
    PRINT '✅ sp_AggregateUnInventoryAdjustmentData: 存在';
ELSE
    PRINT '❌ sp_AggregateUnInventoryAdjustmentData: 未作成';

-- 2. テーブル存在確認
PRINT '';
PRINT '2. テーブル存在確認:';

IF OBJECT_ID('UnInventoryMaster', 'U') IS NOT NULL
    PRINT '✅ UnInventoryMaster: 存在';
ELSE
    PRINT '❌ UnInventoryMaster: 未作成';

IF OBJECT_ID('InventoryMaster', 'U') IS NOT NULL
    PRINT '✅ InventoryMaster: 存在';
ELSE
    PRINT '❌ InventoryMaster: 未作成';

-- 3. UnInventoryMasterテーブル構造確認
IF OBJECT_ID('UnInventoryMaster', 'U') IS NOT NULL
BEGIN
    PRINT '';
    PRINT '3. UnInventoryMasterテーブル構造:';
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        IS_NULLABLE,
        COLUMN_DEFAULT
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'UnInventoryMaster'
    ORDER BY ORDINAL_POSITION;
END

PRINT '';
PRINT '=== 確認完了 ===';