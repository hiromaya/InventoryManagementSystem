-- ====================================================================
-- 既存のSalesVouchersテーブルにGrossProfitカラムを追加
-- 作成日: 2025-07-07
-- 用途: 商品日報の粗利計算処理で使用
-- ====================================================================

USE InventoryManagementDB;
GO

-- GrossProfitカラムが存在しない場合のみ追加
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'SalesVouchers' 
               AND COLUMN_NAME = 'GrossProfit')
BEGIN
    ALTER TABLE SalesVouchers ADD GrossProfit DECIMAL(16,4) NULL;
    PRINT 'SalesVouchersテーブルにGrossProfitカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'GrossProfitカラムは既に存在します。';
END
GO

-- 動作確認：カラムが正しく追加されたことを確認
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'SalesVouchers'
AND COLUMN_NAME = 'GrossProfit';
GO