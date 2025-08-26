-- アンマッチリスト処理修正後の検証スクリプト
-- =========================================

-- 修正後のアンマッチリスト処理実行後に実行してください

PRINT '=== アンマッチリスト処理修正結果の検証 ===';

-- 1. CP在庫マスタの状況確認
-- =========================================
PRINT '1. CP在庫マスタの基本状況';

-- DataSetIdの一意性確認
SELECT 
    COUNT(DISTINCT DataSetId) as UniqueDataSetCount,
    COUNT(*) as TotalRecords
FROM CpInventoryMaster;

-- 期待値: UniqueDataSetCount = 1 (DataSetIdが1つだけ存在)

-- 最新DataSetIdの詳細
SELECT TOP 1
    DataSetId,
    COUNT(*) as RecordCount,
    MIN(CreatedDate) as CreatedAt,
    COUNT(CASE WHEN DailyFlag = '0' THEN 1 END) as ProcessedCount,
    COUNT(CASE WHEN DailyFlag = '9' THEN 1 END) as UnprocessedCount
FROM CpInventoryMaster
GROUP BY DataSetId, CreatedDate
ORDER BY CreatedDate DESC;

-- 期待値: ProcessedCount > 0, UnprocessedCount = 0

-- 2. 重複商品の確認
-- =========================================
PRINT '2. 商品の重複状況確認';

-- 同じ商品が複数のDataSetIdに存在しないか確認
SELECT 
    COUNT(*) as DuplicateProductCount
FROM (
    SELECT 
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
    FROM CpInventoryMaster
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
    HAVING COUNT(DISTINCT DataSetId) > 1
) AS duplicates;

-- 期待値: DuplicateProductCount = 0 (重複なし)

-- 3. 集計結果の確認
-- =========================================
PRINT '3. 当日データ集計結果の確認';

DECLARE @LatestDataSetId NVARCHAR(50);
SELECT TOP 1 @LatestDataSetId = DataSetId 
FROM CpInventoryMaster 
ORDER BY CreatedDate DESC;

SELECT 
    'CP在庫マスタ集計結果' as Category,
    COUNT(*) as TotalCount,
    SUM(CASE WHEN DailyFlag = '0' THEN 1 ELSE 0 END) as ProcessedCount,
    SUM(CASE WHEN DailyFlag = '9' THEN 1 ELSE 0 END) as UnprocessedCount,
    SUM(CASE WHEN DailyPurchaseQuantity > 0 THEN 1 ELSE 0 END) as HasPurchaseCount,
    SUM(CASE WHEN DailySalesQuantity > 0 THEN 1 ELSE 0 END) as HasSalesCount,
    SUM(CASE WHEN DailyPurchaseQuantity > 0 OR DailySalesQuantity > 0 THEN 1 ELSE 0 END) as HasTransactionCount
FROM CpInventoryMaster
WHERE DataSetId = @LatestDataSetId;

-- 期待値: ProcessedCount = TotalCount, UnprocessedCount = 0

-- 4. アンマッチ状況の確認
-- =========================================
PRINT '4. アンマッチ状況の確認';

-- 売上伝票でCP在庫マスタにマッチしないもの（「該当無」エラーの原因）
SELECT 
    COUNT(*) as UnmatchedSalesCount
FROM SalesVouchers sv
WHERE sv.JobDate = '2025-06-13'
    AND sv.VoucherType IN ('51', '52')
    AND sv.DetailType = '1'
    AND NOT EXISTS (
        SELECT 1 FROM CpInventoryMaster cp
        WHERE cp.ProductCode = sv.ProductCode
            AND cp.GradeCode = sv.GradeCode
            AND cp.ClassCode = sv.ClassCode
            AND cp.ShippingMarkCode = sv.ShippingMarkCode
            AND cp.ManualShippingMark = sv.ManualShippingMark
            AND cp.DataSetId = @LatestDataSetId
    );

-- 仕入伝票でCP在庫マスタにマッチしないもの
SELECT 
    COUNT(*) as UnmatchedPurchaseCount
FROM PurchaseVouchers pv
WHERE pv.JobDate = '2025-06-13'
    AND pv.VoucherType IN ('61', '62')
    AND pv.DetailType = '1'
    AND NOT EXISTS (
        SELECT 1 FROM CpInventoryMaster cp
        WHERE cp.ProductCode = pv.ProductCode
            AND cp.GradeCode = pv.GradeCode
            AND cp.ClassCode = pv.ClassCode
            AND cp.ShippingMarkCode = pv.ShippingMarkCode
            AND cp.ManualShippingMark = pv.ManualShippingMark
            AND cp.DataSetId = @LatestDataSetId
    );

-- 期待値: UnmatchedSalesCount = 0, UnmatchedPurchaseCount = 0

-- 5. 在庫ゼロエラーの確認
-- =========================================
PRINT '5. 在庫ゼロエラーの確認';

-- DailyStock = 0 で売上がある商品（「在庫0」エラーの原因）
SELECT 
    COUNT(*) as ZeroStockWithSalesCount
FROM CpInventoryMaster cp
WHERE cp.DataSetId = @LatestDataSetId
    AND cp.DailyStock = 0
    AND EXISTS (
        SELECT 1 FROM SalesVouchers sv
        WHERE sv.JobDate = '2025-06-13'
            AND sv.ProductCode = cp.ProductCode
            AND sv.GradeCode = cp.GradeCode
            AND sv.ClassCode = cp.ClassCode
            AND sv.ShippingMarkCode = cp.ShippingMarkCode
            AND sv.ManualShippingMark = cp.ManualShippingMark
            AND sv.Quantity > 0
    );

-- 期待値: ZeroStockWithSalesCount = 0 (在庫計算が正しく行われている)

-- 6. 総合判定
-- =========================================
PRINT '6. 総合判定';

DECLARE @DataSetCount INT, @DuplicateCount INT, @UnprocessedCount INT, @UnmatchedCount INT, @ZeroStockCount INT;

SELECT @DataSetCount = COUNT(DISTINCT DataSetId) FROM CpInventoryMaster;
SELECT @DuplicateCount = COUNT(*) FROM (
    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
    FROM CpInventoryMaster
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
    HAVING COUNT(DISTINCT DataSetId) > 1
) AS dup;
SELECT @UnprocessedCount = COUNT(*) FROM CpInventoryMaster WHERE DailyFlag = '9';
SELECT @UnmatchedCount = (
    SELECT COUNT(*) FROM SalesVouchers sv
    WHERE sv.JobDate = '2025-06-13' AND sv.VoucherType IN ('51', '52') AND sv.DetailType = '1'
    AND NOT EXISTS (
        SELECT 1 FROM CpInventoryMaster cp WHERE cp.DataSetId = @LatestDataSetId
        AND cp.ProductCode = sv.ProductCode AND cp.GradeCode = sv.GradeCode 
        AND cp.ClassCode = sv.ClassCode AND cp.ShippingMarkCode = sv.ShippingMarkCode
        AND cp.ManualShippingMark = sv.ManualShippingMark
    )
);
SELECT @ZeroStockCount = COUNT(*) FROM CpInventoryMaster cp
WHERE cp.DataSetId = @LatestDataSetId AND cp.DailyStock = 0
AND EXISTS (SELECT 1 FROM SalesVouchers sv WHERE sv.JobDate = '2025-06-13'
    AND sv.ProductCode = cp.ProductCode AND sv.GradeCode = cp.GradeCode 
    AND sv.ClassCode = cp.ClassCode AND sv.ShippingMarkCode = cp.ShippingMarkCode
    AND sv.ManualShippingMark = cp.ManualShippingMark AND sv.Quantity > 0);

PRINT '=== 検証結果 ===';
PRINT 'DataSetId数: ' + CAST(@DataSetCount AS NVARCHAR(10)) + ' (期待値: 1)';
PRINT '重複商品数: ' + CAST(@DuplicateCount AS NVARCHAR(10)) + ' (期待値: 0)';
PRINT '未処理レコード数: ' + CAST(@UnprocessedCount AS NVARCHAR(10)) + ' (期待値: 0)';
PRINT 'アンマッチ売上数: ' + CAST(@UnmatchedCount AS NVARCHAR(10)) + ' (期待値: 0)';
PRINT '在庫ゼロエラー数: ' + CAST(@ZeroStockCount AS NVARCHAR(10)) + ' (期待値: 0)';

IF @DataSetCount = 1 AND @DuplicateCount = 0 AND @UnprocessedCount = 0 AND @UnmatchedCount = 0 AND @ZeroStockCount = 0
    PRINT '✓ 修正が正常に完了しています！アンマッチ件数は0件になるはずです。';
ELSE
    PRINT '✗ まだ問題が残っています。詳細を確認してください。';

PRINT '=== 検証完了 ===';