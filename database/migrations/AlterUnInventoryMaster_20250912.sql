-- =============================================
-- AlterUnInventoryMaster_20250912.sql
-- 目的: UnInventoryMaster テーブルを InventoryMaster に合わせて調整
-- 変更点:
--  1) ShippingMarkCode を NVARCHAR(4) に変更
--  2) ManualShippingMark を NVARCHAR(8) に変更
--  3) ShippingMarkName カラムの追加（存在しない場合）
--  4) CarryoverQuantity / CarryoverAmount / CarryoverUnitPrice の追加（存在しない場合）
-- 備考: 既存データに合わせて冪等に実行可能
-- =============================================

USE InventoryManagementDB;
GO

-- 1) ShippingMarkCode を NVARCHAR(4) に変更（存在時のみ）
IF EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'ShippingMarkCode')
BEGIN
    ALTER TABLE UnInventoryMaster ALTER COLUMN ShippingMarkCode NVARCHAR(4) NOT NULL;
    PRINT 'UnInventoryMaster.ShippingMarkCode を NVARCHAR(4) に変更しました。';
END
GO

-- 2) ManualShippingMark を NVARCHAR(8) に変更（存在時のみ）
IF EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'ManualShippingMark')
BEGIN
    ALTER TABLE UnInventoryMaster ALTER COLUMN ManualShippingMark NVARCHAR(8) NOT NULL;
    PRINT 'UnInventoryMaster.ManualShippingMark を NVARCHAR(8) に変更しました。';
END
GO

-- 3) ShippingMarkName の追加（存在しない場合）
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'ShippingMarkName')
BEGIN
    ALTER TABLE UnInventoryMaster ADD ShippingMarkName NVARCHAR(100) NOT NULL DEFAULT '';
    PRINT 'UnInventoryMaster に ShippingMarkName を追加しました。';
END
GO

-- 4) Carryover* 列の追加（存在しない場合）
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'CarryoverQuantity')
BEGIN
    ALTER TABLE UnInventoryMaster ADD CarryoverQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
    PRINT 'UnInventoryMaster に CarryoverQuantity を追加しました。';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'CarryoverAmount')
BEGIN
    ALTER TABLE UnInventoryMaster ADD CarryoverAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
    PRINT 'UnInventoryMaster に CarryoverAmount を追加しました。';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('UnInventoryMaster') 
      AND name = 'CarryoverUnitPrice')
BEGIN
    ALTER TABLE UnInventoryMaster ADD CarryoverUnitPrice DECIMAL(18,4) NOT NULL DEFAULT 0;
    PRINT 'UnInventoryMaster に CarryoverUnitPrice を追加しました。';
END
GO

PRINT 'AlterUnInventoryMaster_20250912.sql 実行完了';
GO

