-- =============================================
-- CP在庫マスタ ManualShippingMarkカラム追加
-- 作成日: 2025-08-26
-- 目的: ShippingMarkNameを荷印マスタ名、ManualShippingMarkを手入力値として分離
-- =============================================

USE InventoryManagementDB;
GO

-- CP在庫マスタにManualShippingMarkカラム追加
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('CpInventoryMaster') 
    AND name = 'ManualShippingMark'
)
BEGIN
    ALTER TABLE CpInventoryMaster
    ADD ManualShippingMark NVARCHAR(8) NOT NULL DEFAULT '';
    
    PRINT 'CpInventoryMasterテーブルにManualShippingMarkカラムを追加しました';
END
ELSE
BEGIN
    PRINT 'ManualShippingMarkカラムは既に存在します';
END
GO

-- 既存データの移行（ShippingMarkNameの値をManualShippingMarkにコピー）
UPDATE CpInventoryMaster
SET ManualShippingMark = ShippingMarkName
WHERE ManualShippingMark = '';
GO

PRINT 'ManualShippingMarkカラムへのデータ移行が完了しました';
GO