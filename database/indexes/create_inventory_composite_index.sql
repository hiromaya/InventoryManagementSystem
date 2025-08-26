-- ====================================================================
-- 在庫マスタ 5項目キー複合インデックス作成
-- 作成日: 2025-07-10
-- 用途: 累積管理での高速検索のため
-- ====================================================================

-- 既存のインデックスを削除（存在する場合）
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_5Keys' AND object_id = OBJECT_ID('InventoryMaster'))
    DROP INDEX IX_InventoryMaster_5Keys ON InventoryMaster;
GO

-- 5項目キーの複合インデックスを作成
CREATE NONCLUSTERED INDEX IX_InventoryMaster_5Keys
ON InventoryMaster (
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark
)
INCLUDE (
    ProductName,
    Unit,
    StandardPrice,
    ProductCategory1,
    ProductCategory2,
    JobDate,
    CurrentStock,
    CurrentStockAmount,
    DailyStock,
    DailyStockAmount,
    DailyFlag,
    DataSetId,
    UpdatedDate,
    PreviousMonthQuantity,
    PreviousMonthAmount
);
GO

-- CP在庫マスタの5項目キー複合インデックス
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CpInventoryMaster_5Keys' AND object_id = OBJECT_ID('CpInventoryMaster'))
    DROP INDEX IX_CpInventoryMaster_5Keys ON CpInventoryMaster;
GO

CREATE NONCLUSTERED INDEX IX_CpInventoryMaster_5Keys
ON CpInventoryMaster (
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark,
    DataSetId
);
GO

-- 売上伝票の5項目キー複合インデックス
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesVouchers_5Keys' AND object_id = OBJECT_ID('SalesVouchers'))
    DROP INDEX IX_SalesVouchers_5Keys ON SalesVouchers;
GO

CREATE NONCLUSTERED INDEX IX_SalesVouchers_5Keys
ON SalesVouchers (
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark,
    JobDate
);
GO

-- 仕入伝票の5項目キー複合インデックス
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseVouchers_5Keys' AND object_id = OBJECT_ID('PurchaseVouchers'))
    DROP INDEX IX_PurchaseVouchers_5Keys ON PurchaseVouchers;
GO

CREATE NONCLUSTERED INDEX IX_PurchaseVouchers_5Keys
ON PurchaseVouchers (
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark,
    JobDate
);
GO

-- 在庫調整の5項目キー複合インデックス
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryAdjustments_5Keys' AND object_id = OBJECT_ID('InventoryAdjustments'))
    DROP INDEX IX_InventoryAdjustments_5Keys ON InventoryAdjustments;
GO

CREATE NONCLUSTERED INDEX IX_InventoryAdjustments_5Keys
ON InventoryAdjustments (
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark,
    JobDate
);
GO

-- インデックス作成後の統計情報更新
UPDATE STATISTICS InventoryMaster;
UPDATE STATISTICS CpInventoryMaster;
UPDATE STATISTICS SalesVouchers;
UPDATE STATISTICS PurchaseVouchers;
UPDATE STATISTICS InventoryAdjustments;
GO

PRINT '5項目キー複合インデックスの作成が完了しました。';
GO