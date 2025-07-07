-- CpInventoryMasterテーブルに月計カラムを追加
USE InventoryManagementDB;
GO

-- 月計売上関連
ALTER TABLE CpInventoryMaster ADD MonthlySalesQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlySalesAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlySalesReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlySalesReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0;

-- 月計仕入関連
ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlyPurchaseReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0;

-- 月計在庫調整関連
ALTER TABLE CpInventoryMaster ADD MonthlyInventoryAdjustmentQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlyInventoryAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0;

-- 月計加工・振替関連
ALTER TABLE CpInventoryMaster ADD MonthlyProcessingQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlyProcessingAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlyTransferQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE CpInventoryMaster ADD MonthlyTransferAmount DECIMAL(18,4) NOT NULL DEFAULT 0;

-- 月計粗利益
ALTER TABLE CpInventoryMaster ADD MonthlyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0;

PRINT 'CpInventoryMasterテーブルに月計カラムを追加しました。';
GO