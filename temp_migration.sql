USE InventoryManagementDB; -- ============================================================================
-- Migration: 039_AddWalkingDiscountToSalesVouchers.sql
-- 目的: SalesVouchersテーブルにWalkingDiscountカラムを追加
-- 作成日: 2025-07-23
-- 
-- 問題: GetByJobDateAndDataSetIdAsyncメソッドでWalkingDiscountカラムが存在しないエラー
-- 解決: WalkingDiscount DECIMAL(12,4) NULL DEFAULT 0 カラムを追加
-- ============================================================================

PRINT '=== Migration 039: SalesVouchersテーブルにWalkingDiscountカラムを追加 ===';

-- WalkingDiscountカラムが存在しない場合のみ追加
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('SalesVouchers') 
    AND name = 'WalkingDiscount'
)
BEGIN
    PRINT '  → WalkingDiscountカラムを追加中...';
    
    ALTER TABLE SalesVouchers
    ADD WalkingDiscount DECIMAL(12,4) NULL DEFAULT 0;
    
    PRINT '  ✅ WalkingDiscountカラムを追加しました';
    
    -- 既存データに対してデフォルト値0を設定
    UPDATE SalesVouchers 
    SET WalkingDiscount = 0 
    WHERE WalkingDiscount IS NULL;
    
    PRINT '  ✅ 既存データにデフォルト値0を設定しました';
    
    -- インデックス作成（必要に応じて）
    -- CREATE INDEX IX_SalesVouchers_WalkingDiscount ON SalesVouchers(WalkingDiscount);
    
END
ELSE
BEGIN
    PRINT '  ℹ️  WalkingDiscountカラムは既に存在します';
END

PRINT '=== Migration 039 完了 ===';
GO
