-- ====================================================================
-- 既存のCP在庫マスタと在庫マスタの商品分類を修正
-- 作成日: 2025-01-07
-- 用途: 商品日報の大分類計表示のため、商品分類1を正しく設定する
-- ====================================================================

USE InventoryManagementDB;
GO

-- 1. 既存のCP在庫マスタの商品分類を修正
UPDATE cp
SET 
    cp.ProductCategory1 = CASE 
        WHEN LEFT(cp.ShippingMarkName, 4) = '9aaa' THEN '8'
        WHEN LEFT(cp.ShippingMarkName, 4) = '1aaa' THEN '6'
        WHEN LEFT(cp.ShippingMarkName, 4) = '0999' THEN '6'
        ELSE ISNULL(pm.ProductCategory1, '00')
    END,
    cp.ProductCategory2 = ISNULL(pm.ProductCategory2, '00'),
    cp.UpdatedDate = GETDATE()
FROM CpInventoryMaster cp
LEFT JOIN ProductMaster pm ON cp.ProductCode = pm.ProductCode;

PRINT '既存のCP在庫マスタの商品分類を更新しました。';

-- 2. 既存の在庫マスタの商品分類も修正
UPDATE im
SET 
    im.ProductCategory1 = CASE 
        WHEN LEFT(im.ShippingMarkName, 4) = '9aaa' THEN '8'
        WHEN LEFT(im.ShippingMarkName, 4) = '1aaa' THEN '6'
        WHEN LEFT(im.ShippingMarkName, 4) = '0999' THEN '6'
        ELSE ISNULL(pm.ProductCategory1, '00')
    END,
    im.ProductCategory2 = ISNULL(pm.ProductCategory2, '00'),
    im.UpdatedDate = GETDATE()
FROM InventoryMaster im
LEFT JOIN ProductMaster pm ON im.ProductCode = pm.ProductCode;

PRINT '既存の在庫マスタの商品分類を更新しました。';

-- 3. 更新結果の確認
SELECT 
    '商品分類1別データ件数' AS 情報,
    ProductCategory1 AS 商品分類1,
    COUNT(*) AS 件数,
    SUM(DailySalesAmount) AS 売上金額合計
FROM CpInventoryMaster 
WHERE DailySalesAmount > 0
GROUP BY ProductCategory1
ORDER BY ProductCategory1;

PRINT '更新完了';
GO