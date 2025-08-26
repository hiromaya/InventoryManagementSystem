-- =============================================
-- 063_AddShippingMarkNameToUnInventoryMaster.sql
-- SE1: CSV取込 + アンマッチリスト + 営業日報担当
-- 作成日: 2025-08-26
-- 目的: UnInventoryMasterテーブルにShippingMarkNameカラムを追加
-- 説明: アンマッチリストPDFで荷印名表示のため、荷印マスタから取得したShippingMarkNameを保存
-- =============================================

USE InventoryManagementDB;
GO

-- UnInventoryMasterテーブルにShippingMarkNameカラムを追加
IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'UnInventoryMaster' 
    AND COLUMN_NAME = 'ShippingMarkName'
)
BEGIN
    ALTER TABLE UnInventoryMaster
    ADD ShippingMarkName NVARCHAR(100) NOT NULL DEFAULT '';
    
    PRINT 'UnInventoryMasterにShippingMarkNameカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'UnInventoryMaster.ShippingMarkNameカラムは既に存在します。';
END
GO

-- パフォーマンス向上のためインデックスを追加
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
    AND name = 'IX_UnInventoryMaster_ShippingMarkName'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_UnInventoryMaster_ShippingMarkName
    ON UnInventoryMaster (ShippingMarkName);
    
    PRINT 'UnInventoryMaster.ShippingMarkNameインデックスを作成しました。';
END
GO

PRINT '063_AddShippingMarkNameToUnInventoryMaster.sql実行完了';
GO