-- =============================================
-- 在庫マスタ重複レコードクリーンアップ
-- 作成日: 2025-07-10
-- 説明: ManualShippingMark正規化不足による重複レコードを削除
-- =============================================

USE InventoryManagementDB;
GO

-- 重複レコードの確認
SELECT 
    ProductCode, 
    GradeCode, 
    ClassCode, 
    ShippingMarkCode, 
    LEFT(RTRIM(COALESCE(ManualShippingMark, '')) + REPLICATE(' ', 8), 8) as NormalizedManualShippingMark,
    COUNT(*) as RecordCount
FROM InventoryMaster
GROUP BY 
    ProductCode, 
    GradeCode, 
    ClassCode, 
    ShippingMarkCode, 
    LEFT(RTRIM(COALESCE(ManualShippingMark, '')) + REPLICATE(' ', 8), 8)
HAVING COUNT(*) > 1
ORDER BY RecordCount DESC;

-- 重複レコードの削除（最新のJobDateとUpdatedDateのみ残す）
WITH DuplicateRecords AS (
    SELECT *,
        ROW_NUMBER() OVER (
            PARTITION BY 
                ProductCode, 
                GradeCode, 
                ClassCode, 
                ShippingMarkCode, 
                LEFT(RTRIM(COALESCE(ManualShippingMark, '')) + REPLICATE(' ', 8), 8)
            ORDER BY 
                JobDate DESC, 
                UpdatedDate DESC,
                CreatedDate DESC
        ) as rn
    FROM InventoryMaster
)
DELETE FROM InventoryMaster
WHERE EXISTS (
    SELECT 1 
    FROM DuplicateRecords dr
    WHERE dr.ProductCode = InventoryMaster.ProductCode
      AND dr.GradeCode = InventoryMaster.GradeCode
      AND dr.ClassCode = InventoryMaster.ClassCode
      AND dr.ShippingMarkCode = InventoryMaster.ShippingMarkCode
      AND dr.ManualShippingMark = InventoryMaster.ManualShippingMark
      AND dr.JobDate = InventoryMaster.JobDate
      AND dr.CreatedDate = InventoryMaster.CreatedDate
      AND dr.rn > 1
);

-- 削除結果の確認
SELECT 
    '削除後の重複チェック' as CheckResult,
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT CONCAT(ProductCode, GradeCode, ClassCode, ShippingMarkCode, LEFT(RTRIM(COALESCE(ManualShippingMark, '')) + REPLICATE(' ', 8), 8))) as UniqueKeys
FROM InventoryMaster;

PRINT N'重複レコードのクリーンアップが完了しました。';