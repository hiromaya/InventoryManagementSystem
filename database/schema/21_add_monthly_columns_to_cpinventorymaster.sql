-- ====================================================================
-- CpInventoryMasterテーブルに月計カラムを追加
-- 作成日: 2025-07-07
-- 用途: 商品日報の月計データ処理対応
-- ====================================================================

USE InventoryManagementDB;
GO

-- 月計売上関連カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlySalesQuantity')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlySalesQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;        -- 月計売上数量
    PRINT 'MonthlySalesQuantityカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlySalesAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlySalesAmount DECIMAL(18,4) NOT NULL DEFAULT 0;          -- 月計売上金額
    PRINT 'MonthlySalesAmountカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlySalesReturnQuantity')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlySalesReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;  -- 月計売上返品数量
    PRINT 'MonthlySalesReturnQuantityカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlySalesReturnAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlySalesReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0;    -- 月計売上返品金額
    PRINT 'MonthlySalesReturnAmountカラムを追加しました。'
END

-- 月計仕入関連カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyPurchaseQuantity')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyPurchaseQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;     -- 月計仕入数量
    PRINT 'MonthlyPurchaseQuantityカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyPurchaseAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyPurchaseAmount DECIMAL(18,4) NOT NULL DEFAULT 0;       -- 月計仕入金額
    PRINT 'MonthlyPurchaseAmountカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyPurchaseReturnQuantity')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyPurchaseReturnQuantity DECIMAL(18,4) NOT NULL DEFAULT 0; -- 月計仕入返品数量
    PRINT 'MonthlyPurchaseReturnQuantityカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyPurchaseReturnAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyPurchaseReturnAmount DECIMAL(18,4) NOT NULL DEFAULT 0;   -- 月計仕入返品金額
    PRINT 'MonthlyPurchaseReturnAmountカラムを追加しました。'
END

-- 月計在庫調整関連カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyInventoryAdjustmentQuantity')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyInventoryAdjustmentQuantity DECIMAL(18,4) NOT NULL DEFAULT 0; -- 月計在庫調整数量
    PRINT 'MonthlyInventoryAdjustmentQuantityカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyInventoryAdjustmentAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyInventoryAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0;   -- 月計在庫調整金額
    PRINT 'MonthlyInventoryAdjustmentAmountカラムを追加しました。'
END

-- 月計加工・振替関連カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyProcessingQuantity')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyProcessingQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;   -- 月計加工数量
    PRINT 'MonthlyProcessingQuantityカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyProcessingAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyProcessingAmount DECIMAL(18,4) NOT NULL DEFAULT 0;     -- 月計加工金額
    PRINT 'MonthlyProcessingAmountカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyTransferQuantity')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyTransferQuantity DECIMAL(18,4) NOT NULL DEFAULT 0;     -- 月計振替数量
    PRINT 'MonthlyTransferQuantityカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyTransferAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyTransferAmount DECIMAL(18,4) NOT NULL DEFAULT 0;       -- 月計振替金額
    PRINT 'MonthlyTransferAmountカラムを追加しました。'
END

-- 月計粗利益関連カラムの追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyGrossProfit')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0;          -- 月計粗利益
    PRINT 'MonthlyGrossProfitカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyWalkingAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyWalkingAmount DECIMAL(18,4) NOT NULL DEFAULT 0;        -- 月計歩引き額
    PRINT 'MonthlyWalkingAmountカラムを追加しました。'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]') AND name = 'MonthlyIncentiveAmount')
BEGIN
    ALTER TABLE [dbo].[CpInventoryMaster]
    ADD MonthlyIncentiveAmount DECIMAL(18,4) NOT NULL DEFAULT 0;      -- 月計奨励金
    PRINT 'MonthlyIncentiveAmountカラムを追加しました。'
END

-- 変更確認クエリ
PRINT '';
PRINT '===================================================================';
PRINT 'カラム追加結果の確認:';
PRINT '===================================================================';

SELECT 
    c.name AS ColumnName,
    ty.name AS DataType,
    c.precision,
    c.scale,
    c.is_nullable
FROM sys.columns c
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = OBJECT_ID(N'[dbo].[CpInventoryMaster]')
    AND c.name LIKE 'Monthly%'
ORDER BY c.column_id;
GO