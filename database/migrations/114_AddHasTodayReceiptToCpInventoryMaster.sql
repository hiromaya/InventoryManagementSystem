-- CpInventoryMaster に当日入荷フラグを追加（114）
IF COL_LENGTH('dbo.CpInventoryMaster', 'HasTodayReceipt') IS NULL
BEGIN
    ALTER TABLE dbo.CpInventoryMaster
        ADD HasTodayReceipt BIT NOT NULL CONSTRAINT DF_CpInventoryMaster_HasTodayReceipt DEFAULT 0;
    PRINT 'CpInventoryMaster.HasTodayReceipt を追加しました (114)';
END
