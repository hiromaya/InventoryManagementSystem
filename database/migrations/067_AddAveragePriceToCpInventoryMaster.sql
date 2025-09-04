-- CpInventoryMasterテーブルにAveragePriceカラムを追加
-- 作成日: 2025-09-04
-- 目的: 在庫単価（DailyUnitPrice）と平均単価（AveragePrice）の同期管理

-- カラムの存在確認と追加
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') 
    AND name = 'AveragePrice'
)
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD AveragePrice DECIMAL(18, 4) NULL DEFAULT 0;
    
    PRINT 'AveragePriceカラムを追加しました';
END
ELSE
BEGIN
    PRINT 'AveragePriceカラムは既に存在します';
END
GO

-- 既存データの初期値設定（DailyUnitPriceと同じ値に設定）
UPDATE [dbo].[CpInventoryMaster]
SET AveragePrice = DailyUnitPrice
WHERE AveragePrice IS NULL OR AveragePrice = 0;
GO

PRINT 'CpInventoryMasterテーブルのAveragePrice初期値設定完了';