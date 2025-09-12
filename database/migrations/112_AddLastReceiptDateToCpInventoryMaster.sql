-- 112: CpInventoryMaster に最終入荷日カラムを追加
IF COL_LENGTH('dbo.CpInventoryMaster', 'LastReceiptDate') IS NULL
BEGIN
    ALTER TABLE dbo.CpInventoryMaster
        ADD LastReceiptDate DATE NULL;
    PRINT 'CpInventoryMaster.LastReceiptDate を追加しました';
END
ELSE
BEGIN
    PRINT 'CpInventoryMaster.LastReceiptDate は既に存在します';
END

