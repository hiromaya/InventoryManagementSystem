-- =============================================
-- Migration: 064_AddShippingMarkNameToCpInventoryMaster.sql
-- 作成日: 2025-08-27
-- 目的: 商品勘定帳票の荷印名表示問題を解決
-- 説明: CP在庫マスタにShippingMarkNameカラムを追加し、商品勘定帳票で荷印名が正常に表示されるようにする
-- =============================================

USE InventoryManagementDB;
GO

PRINT '=== Migration 064: CP在庫マスタにShippingMarkNameカラムを追加 ===';

-- ShippingMarkNameカラムが存在しない場合のみ追加
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('CpInventoryMaster') 
    AND name = 'ShippingMarkName'
)
BEGIN
    ALTER TABLE CpInventoryMaster
    ADD ShippingMarkName NVARCHAR(100) NOT NULL DEFAULT '';
    
    PRINT '  ✅ ShippingMarkNameカラムを追加しました';
    
    -- 既存データの初期値設定（安全対策）
    -- ShippingMarkMasterから荷印名を取得し、見つからない場合は荷印コードを使用
    UPDATE CpInventoryMaster 
    SET ShippingMarkName = ISNULL(
        (SELECT ShippingMarkName FROM ShippingMarkMaster sm 
         WHERE sm.ShippingMarkCode = CpInventoryMaster.ShippingMarkCode), 
        ShippingMarkCode
    )
    WHERE ShippingMarkName = '';
    
    PRINT '  ✅ 既存データの初期値を設定しました (' + CAST(@@ROWCOUNT AS VARCHAR) + '件)';
    
    -- インデックス作成（パフォーマンス向上）
    CREATE NONCLUSTERED INDEX IX_CpInventoryMaster_ShippingMarkName 
    ON CpInventoryMaster(ShippingMarkName);
    
    PRINT '  ✅ ShippingMarkName用インデックスを作成しました';
END
ELSE
BEGIN
    PRINT '  ℹ️  ShippingMarkNameカラムは既に存在します';
END

PRINT '=== Migration 064 完了 ===';
GO