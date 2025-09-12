-- InventoryMasterにAveragePriceカラムを追加
-- 目的: SPやサービスコードで参照されるAveragePriceのスキーマ整合性を確保

USE InventoryManagementDB;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]')
      AND name = 'AveragePrice'
)
BEGIN
    ALTER TABLE [dbo].[InventoryMaster]
    ADD AveragePrice DECIMAL(18,4) NULL DEFAULT 0;
    PRINT 'InventoryMaster.AveragePrice を追加しました';
END
ELSE
BEGIN
    PRINT 'InventoryMaster.AveragePrice は既に存在します';
END
GO

-- 初期値の簡易同期（必要に応じて）
UPDATE [dbo].[InventoryMaster]
SET AveragePrice = COALESCE(AveragePrice, 0)
WHERE AveragePrice IS NULL;
GO

PRINT '068_AddAveragePriceToInventoryMaster.sql 完了';

