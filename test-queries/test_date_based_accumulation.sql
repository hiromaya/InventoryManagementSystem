-- 日付別累積在庫管理のテストクエリ
-- 実行日: 2025-07-13

USE InventoryManagementDB;
GO

-- 1. 修正前の状態確認
PRINT '=== 修正前の在庫マスタ状態 ===';
SELECT 
    JobDate,
    COUNT(*) as RecordCount,
    MIN(CreatedDate) as FirstCreated,
    MAX(UpdatedDate) as LastUpdated
FROM InventoryMaster
GROUP BY JobDate
ORDER BY JobDate DESC;

-- 2. 特定商品の履歴確認（例：商品コード10001）
PRINT '';
PRINT '=== 商品10001の在庫履歴 ===';
SELECT 
    JobDate,
    ProductCode,
    CurrentStock,
    DailyStock,
    PreviousMonthQuantity,
    DataSetId,
    IsActive,
    UpdatedDate
FROM InventoryMaster
WHERE ProductCode = '10001'
ORDER BY JobDate;

-- 3. 6月1日と6月2日のデータ比較
PRINT '';
PRINT '=== 6月1日の在庫件数 ===';
SELECT COUNT(*) as June1Count
FROM InventoryMaster
WHERE JobDate = '2025-06-01';

PRINT '';
PRINT '=== 6月2日の在庫件数 ===';
SELECT COUNT(*) as June2Count
FROM InventoryMaster
WHERE JobDate = '2025-06-02';

-- 4. 同一商品キーで複数日付のレコードが存在するか確認
PRINT '';
PRINT '=== 同一商品キーで複数日付のレコード数 ===';
WITH ProductKeys AS (
    SELECT 
        ProductCode, 
        GradeCode, 
        ClassCode, 
        ShippingMarkCode, 
        ShippingMarkName,
        COUNT(DISTINCT JobDate) as DateCount
    FROM InventoryMaster
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    HAVING COUNT(DISTINCT JobDate) > 1
)
SELECT 
    COUNT(*) as ProductsWithMultipleDates,
    MAX(DateCount) as MaxDateCount
FROM ProductKeys;

-- 5. DataSetId別の在庫レコード数
PRINT '';
PRINT '=== DataSetId別レコード数（最新10件）===';
SELECT TOP 10
    DataSetId,
    COUNT(*) as RecordCount,
    MIN(JobDate) as MinJobDate,
    MAX(JobDate) as MaxJobDate
FROM InventoryMaster
WHERE DataSetId != ''
GROUP BY DataSetId
ORDER BY MIN(CreatedDate) DESC;

-- 6. 前日在庫引継の確認（CurrentStockとDailyStockの差分）
PRINT '';
PRINT '=== 前日在庫引継の確認（6月2日データ）===';
SELECT TOP 10
    ProductCode,
    JobDate,
    CurrentStock,
    DailyStock,
    CurrentStock - DailyStock as CarryOverStock,
    CASE 
        WHEN CurrentStock - DailyStock != 0 THEN '引継あり'
        ELSE '引継なし'
    END as CarryOverStatus
FROM InventoryMaster
WHERE JobDate = '2025-06-02'
    AND DailyStock != 0
ORDER BY ProductCode;