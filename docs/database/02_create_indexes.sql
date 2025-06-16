-- ==================================================
-- インデックス作成スクリプト
-- ==================================================

USE InventoryManagementDB;
GO

-- ==================================================
-- InventoryMasterのインデックス
-- ==================================================

-- JobDateでの検索用（最も頻繁に使用）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_JobDate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_InventoryMaster_JobDate
    ON InventoryMaster (JobDate)
    INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, DailyFlag);
END
GO

-- 当日発生フラグでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_JobDate_DailyFlag')
BEGIN
    CREATE NONCLUSTERED INDEX IX_InventoryMaster_JobDate_DailyFlag
    ON InventoryMaster (JobDate, DailyFlag)
    INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);
END
GO

-- データセットIDでの検索用（ロールバック時）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_DataSetId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_InventoryMaster_DataSetId
    ON InventoryMaster (DataSetId);
END
GO

-- 商品コードでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_ProductCode')
BEGIN
    CREATE NONCLUSTERED INDEX IX_InventoryMaster_ProductCode
    ON InventoryMaster (ProductCode)
    INCLUDE (JobDate, DailyFlag);
END
GO

-- ==================================================
-- SalesVoucherのインデックス
-- ==================================================

-- JobDateでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesVoucher_JobDate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesVoucher_JobDate
    ON SalesVoucher (JobDate)
    INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, Quantity, SalesUnitPrice, InventoryUnitPrice);
END
GO

-- 在庫キーでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesVoucher_InventoryKey')
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesVoucher_InventoryKey
    ON SalesVoucher (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate);
END
GO

-- データセットIDでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SalesVoucher_DataSetId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_SalesVoucher_DataSetId
    ON SalesVoucher (DataSetId);
END
GO

-- ==================================================
-- PurchaseVoucherのインデックス
-- ==================================================

-- JobDateでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseVoucher_JobDate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PurchaseVoucher_JobDate
    ON PurchaseVoucher (JobDate)
    INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, Quantity, PurchaseUnitPrice);
END
GO

-- 在庫キーでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseVoucher_InventoryKey')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PurchaseVoucher_InventoryKey
    ON PurchaseVoucher (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate);
END
GO

-- データセットIDでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseVoucher_DataSetId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PurchaseVoucher_DataSetId
    ON PurchaseVoucher (DataSetId);
END
GO

-- ==================================================
-- ProcessingHistoryのインデックス
-- ==================================================

-- データセットIDでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessingHistory_DataSetId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProcessingHistory_DataSetId
    ON ProcessingHistory (DataSetId)
    INCLUDE (ProcessType, JobDate, ProcessedAt, Status);
END
GO

-- JobDateとProcessTypeでの検索用
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessingHistory_JobDate_ProcessType')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProcessingHistory_JobDate_ProcessType
    ON ProcessingHistory (JobDate, ProcessType)
    INCLUDE (DataSetId, ProcessedAt, Status, ProcessedRecords);
END
GO

-- 処理日時での検索用（履歴参照）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessingHistory_ProcessedAt')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProcessingHistory_ProcessedAt
    ON ProcessingHistory (ProcessedAt DESC)
    INCLUDE (DataSetId, ProcessType, JobDate, Status);
END
GO