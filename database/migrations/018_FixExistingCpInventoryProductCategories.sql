-- ====================================================================
-- 既存のCP在庫マスタと在庫マスタの商品分類を修正
-- 作成日: 2025-01-07
-- 用途: 商品日報の大分類計表示のため、商品分類1を正しく設定する
-- ====================================================================

USE InventoryManagementDB;
GO

-- テーブルの存在確認
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CpInventoryMaster')
BEGIN
    PRINT 'CpInventoryMasterテーブルが存在しません。処理をスキップします。';
    RETURN;
END

-- 1. 既存のCP在庫マスタの商品分類を修正（荷印名のパターンで判定）
UPDATE CpInventoryMaster
SET 
    ProductCategory1 = CASE 
        WHEN LEFT(ManualShippingMark, 4) = '9aaa' THEN '8'
        WHEN LEFT(ManualShippingMark, 4) = '1aaa' THEN '6'
        WHEN LEFT(ManualShippingMark, 4) = '0999' THEN '6'
        WHEN ProductCategory1 IS NULL OR ProductCategory1 = '' THEN '00'
        ELSE ProductCategory1  -- 既存の値を保持
    END,
    ProductCategory2 = CASE 
        WHEN ProductCategory2 IS NULL OR ProductCategory2 = '' THEN '00'
        ELSE ProductCategory2  -- 既存の値を保持
    END,
    UpdatedDate = GETDATE()
WHERE 
    (LEFT(ManualShippingMark, 4) IN ('9aaa', '1aaa', '0999'))
    OR (ProductCategory1 IS NULL OR ProductCategory1 = '');

PRINT CONCAT('CP在庫マスタの商品分類を更新しました。影響行数: ', @@ROWCOUNT);

-- 2. 既存の在庫マスタの商品分類も修正
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InventoryMaster')
BEGIN
    UPDATE InventoryMaster
    SET 
        ProductCategory1 = CASE 
            WHEN LEFT(ManualShippingMark, 4) = '9aaa' THEN '8'
            WHEN LEFT(ManualShippingMark, 4) = '1aaa' THEN '6'
            WHEN LEFT(ManualShippingMark, 4) = '0999' THEN '6'
            WHEN ProductCategory1 IS NULL OR ProductCategory1 = '' THEN '00'
            ELSE ProductCategory1  -- 既存の値を保持
        END,
        ProductCategory2 = CASE 
            WHEN ProductCategory2 IS NULL OR ProductCategory2 = '' THEN '00'
            ELSE ProductCategory2  -- 既存の値を保持
        END,
        UpdatedDate = GETDATE()
    WHERE 
        (LEFT(ManualShippingMark, 4) IN ('9aaa', '1aaa', '0999'))
        OR (ProductCategory1 IS NULL OR ProductCategory1 = '');

    PRINT CONCAT('在庫マスタの商品分類を更新しました。影響行数: ', @@ROWCOUNT);
END

-- 3. 更新結果の確認
PRINT '';
PRINT '===== 商品分類1別データ件数（CP在庫マスタ） =====';
SELECT 
    ProductCategory1 AS 商品分類1,
    COUNT(*) AS 件数,
    SUM(CASE WHEN DailySalesAmount > 0 THEN DailySalesAmount ELSE 0 END) AS 売上金額合計
FROM CpInventoryMaster 
GROUP BY ProductCategory1
ORDER BY ProductCategory1;

-- 4. 荷印名パターン別の確認
PRINT '';
PRINT '===== 荷印名パターン別データ件数 =====';
SELECT 
    LEFT(ManualShippingMark, 4) AS 荷印パターン,
    ProductCategory1 AS 商品分類1,
    COUNT(*) AS 件数
FROM CpInventoryMaster
WHERE LEFT(ManualShippingMark, 4) IN ('9aaa', '1aaa', '0999')
GROUP BY LEFT(ManualShippingMark, 4), ProductCategory1
ORDER BY LEFT(ManualShippingMark, 4), ProductCategory1;

PRINT '';
PRINT 'Migration 018: 商品分類の更新が完了しました。';
GO