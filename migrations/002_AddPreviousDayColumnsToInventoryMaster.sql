-- 前日在庫カラムをInventoryMasterテーブルに追加
-- 実行日: 2025-06-30

-- PreviousDayQuantity カラムの追加
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
    AND name = 'PreviousDayQuantity'
)
BEGIN
    ALTER TABLE [dbo].[InventoryMaster]
    ADD [PreviousDayQuantity] decimal(18, 2) NOT NULL DEFAULT 0;
    
    PRINT 'PreviousDayQuantity カラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'PreviousDayQuantity カラムは既に存在します。';
END

-- PreviousDayAmount カラムの追加
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
    AND name = 'PreviousDayAmount'
)
BEGIN
    ALTER TABLE [dbo].[InventoryMaster]
    ADD [PreviousDayAmount] decimal(18, 2) NOT NULL DEFAULT 0;
    
    PRINT 'PreviousDayAmount カラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'PreviousDayAmount カラムは既に存在します。';
END

-- 初期値として前月末在庫の値を前日在庫にコピー
UPDATE [dbo].[InventoryMaster]
SET [PreviousDayQuantity] = [PreviousMonthQuantity],
    [PreviousDayAmount] = [PreviousMonthAmount]
WHERE [PreviousDayQuantity] = 0 AND [PreviousDayAmount] = 0;

PRINT '前日在庫の初期値を設定しました。';

-- 確認用クエリ
SELECT 
    COUNT(*) as 総レコード数,
    COUNT(CASE WHEN PreviousDayQuantity > 0 THEN 1 END) as 前日在庫数量設定済み,
    COUNT(CASE WHEN PreviousDayAmount > 0 THEN 1 END) as 前日在庫金額設定済み
FROM [dbo].[InventoryMaster];