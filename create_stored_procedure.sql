-- =============================================
-- 商品勘定ストアドプロシージャの手動作成用スクリプト
-- Windows環境でSQL Server Management Studioまたはsqlcmdで実行してください
-- =============================================

-- 正しいデータベースを使用
USE InventoryManagementDB;
GO

-- ストアドプロシージャの存在確認
IF OBJECT_ID('sp_CreateProductLedgerData', 'P') IS NOT NULL
BEGIN
    PRINT 'ストアドプロシージャ sp_CreateProductLedgerData は既に存在します。更新します。';
    DROP PROCEDURE sp_CreateProductLedgerData;
END
ELSE
BEGIN
    PRINT 'ストアドプロシージャ sp_CreateProductLedgerData を新規作成します。';
END
GO

-- 実際のストアドプロシージャを作成
-- database/procedures/sp_CreateProductLedgerData.sqlの内容をここにコピーして実行してください

-- 作成結果の確認
SELECT 
    name, 
    create_date, 
    modify_date,
    OBJECT_ID(name) as object_id
FROM sys.procedures 
WHERE name = 'sp_CreateProductLedgerData';

-- 確認後にテスト実行（例）
-- EXEC sp_CreateProductLedgerData @JobDate = '2025-06-02';

PRINT '=== ストアドプロシージャの作成/更新が完了しました ===';
GO