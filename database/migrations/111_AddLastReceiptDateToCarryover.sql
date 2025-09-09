-- 111: InventoryCarryoverMaster に最終入荷日カラムを追加
IF COL_LENGTH('dbo.InventoryCarryoverMaster', 'LastReceiptDate') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryCarryoverMaster
        ADD LastReceiptDate DATE NULL;
    PRINT 'InventoryCarryoverMaster.LastReceiptDate を追加しました';
END
ELSE
BEGIN
    PRINT 'InventoryCarryoverMaster.LastReceiptDate は既に存在します';
END

