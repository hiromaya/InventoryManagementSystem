-- VoucherIdとDataSetIdのサイズを拡張するスクリプト
USE InventoryManagementDB;
GO

-- SalesVouchersテーブル
ALTER TABLE SalesVouchers ALTER COLUMN VoucherId NVARCHAR(100) NOT NULL;
ALTER TABLE SalesVouchers ALTER COLUMN DataSetId NVARCHAR(100);

-- PurchaseVouchersテーブル
ALTER TABLE PurchaseVouchers ALTER COLUMN VoucherId NVARCHAR(100) NOT NULL;
ALTER TABLE PurchaseVouchers ALTER COLUMN DataSetId NVARCHAR(100);

-- InventoryAdjustmentsテーブル
ALTER TABLE InventoryAdjustments ALTER COLUMN VoucherId NVARCHAR(100) NOT NULL;
ALTER TABLE InventoryAdjustments ALTER COLUMN DataSetId NVARCHAR(100);

-- DataSetsテーブル
ALTER TABLE DataSets ALTER COLUMN Id NVARCHAR(100) NOT NULL;

-- InventoryMasterテーブル
-- DataSetIdカラムが存在しない場合は追加
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'InventoryMaster' AND COLUMN_NAME = 'DataSetId')
BEGIN
    ALTER TABLE InventoryMaster ADD DataSetId NVARCHAR(100);
    CREATE INDEX IX_InventoryMaster_DataSetId ON InventoryMaster(DataSetId);
    PRINT 'InventoryMasterテーブルにDataSetIdカラムを追加しました。';
END
ELSE
BEGIN
    ALTER TABLE InventoryMaster ALTER COLUMN DataSetId NVARCHAR(100);
    PRINT 'InventoryMasterテーブルのDataSetIdカラムサイズを更新しました。';
END

-- CpInventoryMasterテーブル
ALTER TABLE CpInventoryMaster ALTER COLUMN DataSetId NVARCHAR(100) NOT NULL;

PRINT 'VoucherIdとDataSetIdのサイズを100文字に拡張しました。';
GO