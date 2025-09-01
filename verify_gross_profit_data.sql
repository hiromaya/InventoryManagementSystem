-- =============================================
-- 商品勘定粗利益データ確認用SQL
-- 修正前最終確認調査
-- =============================================

PRINT '=== 商品勘定粗利益データ確認調査 ===';
PRINT CONCAT('確認日時: ', FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm:ss'));
PRINT '';

-- 1. Process 2-5実行状況確認（売上伝票の汎用数値1）
PRINT '1. 売上伝票の汎用数値1（粗利益）データ確認';
PRINT '=======================================';

SELECT TOP 10 
    VoucherNumber as 伝票番号,
    ProductCode as 商品コード,
    GenericNumeric1 as 汎用数値1_粗利益,
    GenericNumeric2 as 汎用数値2_歩引金,
    InventoryUnitPrice as 在庫単価,
    Amount as 売上金額,
    Quantity as 数量
FROM SalesVouchers 
WHERE JobDate = '2025-06-01'
ORDER BY VoucherNumber;

PRINT '';

-- 統計: 汎用数値1に値が入っている件数
DECLARE @SalesWithGrossProfit INT, @TotalSales INT;
SELECT @SalesWithGrossProfit = COUNT(*) FROM SalesVouchers WHERE JobDate = '2025-06-01' AND GenericNumeric1 IS NOT NULL AND GenericNumeric1 != 0;
SELECT @TotalSales = COUNT(*) FROM SalesVouchers WHERE JobDate = '2025-06-01';

PRINT CONCAT('売上伝票総件数: ', @TotalSales);
PRINT CONCAT('粗利益データ有件数: ', @SalesWithGrossProfit);
PRINT CONCAT('粗利益設定率: ', CAST(CASE WHEN @TotalSales > 0 THEN (@SalesWithGrossProfit * 100.0 / @TotalSales) ELSE 0 END AS DECIMAL(5,2)), '%');
PRINT '';

-- 2. CP在庫マスタの粗利データ確認
PRINT '2. CP在庫マスタの粗利データ確認';
PRINT '==============================';

SELECT TOP 10 
    ProductCode as 商品コード,
    DailyGrossProfit as 当日粗利益,
    CurrentDayGrossProfit as カレント粗利益,
    DailySalesAmount as 当日売上金額,
    JobDate
FROM CpInventoryMaster 
WHERE JobDate = '2025-06-01'
  AND (DailyGrossProfit != 0 OR CurrentDayGrossProfit != 0)
ORDER BY ProductCode;

-- 統計: CP在庫マスタの粗利データ件数
DECLARE @CpWithGrossProfit INT, @TotalCp INT;
SELECT @CpWithGrossProfit = COUNT(*) FROM CpInventoryMaster WHERE JobDate = '2025-06-01' AND (DailyGrossProfit != 0 OR CurrentDayGrossProfit != 0);
SELECT @TotalCp = COUNT(*) FROM CpInventoryMaster WHERE JobDate = '2025-06-01';

PRINT CONCAT('CP在庫マスタ総件数: ', @TotalCp);
PRINT CONCAT('粗利データ有件数: ', @CpWithGrossProfit);
PRINT '';

-- 3. 商品勘定用ストアドプロシージャ実行テスト
PRINT '3. 商品勘定用データ生成テスト（先頭10件）';
PRINT '=====================================';

-- sp_CreateProductLedgerDataの結果から粗利関連項目を確認
EXEC sp_CreateProductLedgerData @JobDate = '2025-06-01', @DepartmentCode = NULL;

-- 結果の先頭10件のみ表示（粗利関連項目に注目）
SELECT TOP 10
    ProductCode as 商品コード,
    DisplayCategory as 区分,
    Amount as 金額,
    GrossProfit as 粗利益,
    RecordType as レコード種別,
    VoucherType as 伝票種別
FROM (
    -- sp_CreateProductLedgerDataの結果を再現
    SELECT 
        s.ProductCode,
        CASE s.VoucherType
            WHEN '51' THEN '掛売'
            WHEN '52' THEN '現売'
            ELSE s.VoucherType
        END as DisplayCategory,
        s.Amount,
        0 as GrossProfit,  -- ← 現在の問題箇所
        'Sales' as RecordType,
        s.VoucherType
    FROM SalesVouchers s
    WHERE s.JobDate = '2025-06-01'
) AS TestResult
ORDER BY ProductCode;

PRINT '';

-- 4. 修正案の比較（実際の値と比較）
PRINT '4. 修正案の比較確認';
PRINT '==================';

-- 現在のSQL（固定値0）vs 修正案1（汎用数値1）vs 修正案2（計算式）
SELECT TOP 5
    s.ProductCode as 商品コード,
    s.VoucherNumber as 伝票番号,
    s.Amount as 売上金額,
    s.Quantity as 数量,
    s.UnitPrice as 売上単価,
    s.InventoryUnitPrice as 在庫単価,
    -- 現在のSQL
    0 as 現在_固定値0,
    -- 修正案1: 汎用数値1を参照
    ISNULL(s.GenericNumeric1, 0) as 修正案1_汎用数値1,
    -- 修正案2: 計算式
    s.Amount - (s.Quantity * ISNULL(s.InventoryUnitPrice, 0)) as 修正案2_計算式,
    -- 差異確認
    ISNULL(s.GenericNumeric1, 0) - (s.Amount - (s.Quantity * ISNULL(s.InventoryUnitPrice, 0))) as 案1と案2の差異
FROM SalesVouchers s
WHERE s.JobDate = '2025-06-01'
  AND s.GenericNumeric1 IS NOT NULL 
  AND s.GenericNumeric1 != 0
ORDER BY s.ProductCode;

PRINT '';

-- 5. データ整合性確認
PRINT '5. データ整合性確認';
PRINT '==================';

-- Process 2-5実行確認（在庫単価が設定されているか）
DECLARE @InventoryPriceSetCount INT;
SELECT @InventoryPriceSetCount = COUNT(*) 
FROM SalesVouchers 
WHERE JobDate = '2025-06-01' AND InventoryUnitPrice IS NOT NULL AND InventoryUnitPrice != 0;

PRINT CONCAT('在庫単価設定済み売上伝票件数: ', @InventoryPriceSetCount);

-- CP在庫マスタと売上伝票の整合性
DECLARE @MatchingCount INT;
SELECT @MatchingCount = COUNT(*)
FROM SalesVouchers s
INNER JOIN CpInventoryMaster cp ON 
    s.ProductCode = cp.ProductCode AND
    s.GradeCode = cp.GradeCode AND
    s.ClassCode = cp.ClassCode AND
    s.ShippingMarkCode = cp.ShippingMarkCode AND
    s.ManualShippingMark = cp.ManualShippingMark AND
    s.JobDate = cp.JobDate
WHERE s.JobDate = '2025-06-01';

PRINT CONCAT('CP在庫マスタとの連携可能件数: ', @MatchingCount);

PRINT '';
PRINT '=== 商品勘定粗利益データ確認調査完了 ===';