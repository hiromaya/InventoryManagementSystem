-- CP在庫マスタに月計フィールドを追加
-- 実行日: 2025-07-05
-- 説明: 商品日報の月計表示機能のために月初から当日までの累計フィールドを追加

ALTER TABLE CP_InventoryMaster
ADD 
    MonthlySalesQuantity DECIMAL(18,2) NOT NULL DEFAULT 0,
    MonthlySalesAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    MonthlySalesReturnQuantity DECIMAL(18,2) NOT NULL DEFAULT 0,
    MonthlySalesReturnAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    MonthlyPurchaseQuantity DECIMAL(18,2) NOT NULL DEFAULT 0,
    MonthlyPurchaseAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    MonthlyGrossProfit DECIMAL(18,2) NOT NULL DEFAULT 0,
    MonthlyWalkingAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    MonthlyIncentiveAmount DECIMAL(18,2) NOT NULL DEFAULT 0;

-- 追加されたカラムの確認
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'CP_InventoryMaster'
    AND COLUMN_NAME LIKE 'Monthly%'
ORDER BY ORDINAL_POSITION;