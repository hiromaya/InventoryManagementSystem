-- パラメータテスト用SQL
USE InventoryManagementDB;
GO

-- プロシージャの存在確認
SELECT name, create_date, modify_date
FROM sys.procedures 
WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative';

-- パラメータ確認
SELECT PARAMETER_NAME, DATA_TYPE, PARAMETER_MODE
FROM INFORMATION_SCHEMA.PARAMETERS
WHERE SPECIFIC_NAME = 'sp_CreateCpInventoryFromInventoryMasterCumulative'
ORDER BY ORDINAL_POSITION;

-- 実行テスト（ドライラン）
-- EXEC sp_CreateCpInventoryFromInventoryMasterCumulative @JobDate = '2025-06-30';
