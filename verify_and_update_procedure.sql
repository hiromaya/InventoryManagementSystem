-- 検証・更新用統合SQLスクリプト
USE InventoryManagementDB;
GO

PRINT '=== ストアドプロシージャ検証・更新開始 ===';

-- 1. 現状確認
PRINT '1. 現在のプロシージャ確認';
SELECT name, create_date, modify_date
FROM sys.procedures 
WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative';

-- 2. パラメータ確認
PRINT '2. 現在のパラメータ確認';
SELECT PARAMETER_NAME, DATA_TYPE, PARAMETER_MODE
FROM INFORMATION_SCHEMA.PARAMETERS
WHERE SPECIFIC_NAME = 'sp_CreateCpInventoryFromInventoryMasterCumulative'
ORDER BY ORDINAL_POSITION;

-- 3. 古いプロシージャの削除
PRINT '3. 古いプロシージャの削除';
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative')
BEGIN
    DROP PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative;
    PRINT '✓ 古いプロシージャを削除しました';
END
ELSE
BEGIN
    PRINT '\! プロシージャが存在しませんでした';
END

-- 4. 新しいプロシージャの作成
PRINT '4. 修正版プロシージャファイルを実行してください:';
PRINT 'sqlcmd -S localhost\SQLEXPRESS -d InventoryManagementDB -i database/procedures/sp_CreateCpInventoryFromInventoryMasterCumulative.sql';

-- 5. 作成後の確認（手動実行後）
/*
PRINT '5. 作成後の確認';
SELECT name, create_date, modify_date
FROM sys.procedures 
WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative';

SELECT PARAMETER_NAME, DATA_TYPE, PARAMETER_MODE
FROM INFORMATION_SCHEMA.PARAMETERS
WHERE SPECIFIC_NAME = 'sp_CreateCpInventoryFromInventoryMasterCumulative'
ORDER BY ORDINAL_POSITION;

-- 期待結果: @JobDate パラメータのみが表示されること
*/

PRINT '=== 検証・更新完了 ===';
GO
