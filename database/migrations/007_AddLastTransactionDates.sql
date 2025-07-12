-- =============================================================================
-- マイグレーション: 007_AddLastTransactionDates.sql
-- 説明: InventoryMasterテーブルに最終取引日カラムを追加
-- 作成日: 2025-07-12
-- =============================================================================

-- 1. InventoryMasterテーブルにLastSalesDateとLastPurchaseDateカラムを追加
ALTER TABLE InventoryMaster
ADD LastSalesDate DATE NULL,
    LastPurchaseDate DATE NULL;

-- インデックスを作成（パフォーマンス向上のため）
CREATE INDEX IX_InventoryMaster_LastSalesDate ON InventoryMaster(LastSalesDate);
CREATE INDEX IX_InventoryMaster_LastPurchaseDate ON InventoryMaster(LastPurchaseDate);

-- 2. 既存データの最終取引日を初期設定（売上伝票から）
UPDATE im
SET im.LastSalesDate = sv.MaxJobDate
FROM InventoryMaster im
INNER JOIN (
    SELECT 
        ProductCode, 
        GradeCode, 
        ClassCode, 
        ShippingMarkCode, 
        ShippingMarkName,
        MAX(JobDate) as MaxJobDate
    FROM SalesVouchers
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
) sv ON 
    im.ProductCode = sv.ProductCode AND
    im.GradeCode = sv.GradeCode AND
    im.ClassCode = sv.ClassCode AND
    im.ShippingMarkCode = sv.ShippingMarkCode AND
    im.ShippingMarkName = sv.ShippingMarkName;

-- 3. 既存データの最終取引日を初期設定（仕入伝票から）
UPDATE im
SET im.LastPurchaseDate = pv.MaxJobDate
FROM InventoryMaster im
INNER JOIN (
    SELECT 
        ProductCode, 
        GradeCode, 
        ClassCode, 
        ShippingMarkCode, 
        ShippingMarkName,
        MAX(JobDate) as MaxJobDate
    FROM PurchaseVouchers
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
) pv ON 
    im.ProductCode = pv.ProductCode AND
    im.GradeCode = pv.GradeCode AND
    im.ClassCode = pv.ClassCode AND
    im.ShippingMarkCode = pv.ShippingMarkCode AND
    im.ShippingMarkName = pv.ShippingMarkName;

-- 4. Notesカラムが存在しない場合は追加（非アクティブ化理由記録用）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
               AND name = 'Notes')
BEGIN
    ALTER TABLE InventoryMaster
    ADD Notes NVARCHAR(MAX) NULL;
END

-- 5. 実行結果の確認
SELECT 
    COUNT(*) as TotalRecords,
    COUNT(LastSalesDate) as RecordsWithSalesDate,
    COUNT(LastPurchaseDate) as RecordsWithPurchaseDate
FROM InventoryMaster;

PRINT '===== 007_AddLastTransactionDates.sql 実行完了 =====';
PRINT '最終売上日と最終仕入日のカラムを追加し、既存データの初期設定を完了しました。';