-- ストアドプロシージャの再作成（修正版の適用）
USE InventoryManagementDB;
GO

-- 現在のプロシージャを削除
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromInventoryMasterCumulative')
BEGIN
    DROP PROCEDURE sp_CreateCpInventoryFromInventoryMasterCumulative;
    PRINT '古いプロシージャを削除しました';
END
GO

-- 修正版ファイルの内容をここに貼り付けて実行する
-- または以下のコマンドでファイルから読み込み:
-- sqlcmd -S localhost\SQLEXPRESS -d InventoryManagementDB -i database/procedures/sp_CreateCpInventoryFromInventoryMasterCumulative.sql
