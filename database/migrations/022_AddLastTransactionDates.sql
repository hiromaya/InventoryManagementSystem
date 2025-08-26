-- 022_AddLastTransactionDates.sql
-- 最小限版：カラム追加とデータ更新のみ

-- カラムの追加（存在チェック付き）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'LastSalesDate')
BEGIN
    ALTER TABLE InventoryMaster ADD LastSalesDate DATE NULL;
    PRINT 'LastSalesDateカラムを追加しました。';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'LastPurchaseDate')
BEGIN
    ALTER TABLE InventoryMaster ADD LastPurchaseDate DATE NULL;
    PRINT 'LastPurchaseDateカラムを追加しました。';
END

GO

-- 売上伝票から最終売上日を更新（サブクエリ方式）
PRINT '売上伝票データを更新中...';
UPDATE InventoryMaster
SET LastSalesDate = (
    SELECT MAX(JobDate)
    FROM SalesVouchers
    WHERE ProductCode = InventoryMaster.ProductCode
      AND GradeCode = InventoryMaster.GradeCode
      AND ClassCode = InventoryMaster.ClassCode
      AND ShippingMarkCode = InventoryMaster.ShippingMarkCode
      AND ManualShippingMark = InventoryMaster.ManualShippingMark
)
WHERE LastSalesDate IS NULL
  AND EXISTS (
    SELECT 1
    FROM SalesVouchers
    WHERE ProductCode = InventoryMaster.ProductCode
      AND GradeCode = InventoryMaster.GradeCode
      AND ClassCode = InventoryMaster.ClassCode
      AND ShippingMarkCode = InventoryMaster.ShippingMarkCode
      AND ManualShippingMark = InventoryMaster.ManualShippingMark
  );
PRINT '売上伝票データ更新完了: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + '件';

GO

-- 仕入伝票から最終仕入日を更新（サブクエリ方式）
PRINT '仕入伝票データを更新中...';
UPDATE InventoryMaster
SET LastPurchaseDate = (
    SELECT MAX(JobDate)
    FROM PurchaseVouchers
    WHERE ProductCode = InventoryMaster.ProductCode
      AND GradeCode = InventoryMaster.GradeCode
      AND ClassCode = InventoryMaster.ClassCode
      AND ShippingMarkCode = InventoryMaster.ShippingMarkCode
      AND ManualShippingMark = InventoryMaster.ManualShippingMark
)
WHERE LastPurchaseDate IS NULL
  AND EXISTS (
    SELECT 1
    FROM PurchaseVouchers
    WHERE ProductCode = InventoryMaster.ProductCode
      AND GradeCode = InventoryMaster.GradeCode
      AND ClassCode = InventoryMaster.ClassCode
      AND ShippingMarkCode = InventoryMaster.ShippingMarkCode
      AND ManualShippingMark = InventoryMaster.ManualShippingMark
  );
PRINT '仕入伝票データ更新完了: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + '件';

GO

PRINT '処理完了';