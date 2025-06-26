-- アンマッチリスト処理の完全自動化テスト用SQL
-- =========================================

-- 1. 処理前の状態確認
-- =========================================

-- 在庫マスタの件数とJobDate確認
SELECT 
    JobDate,
    COUNT(*) as RecordCount
FROM InventoryMaster
WHERE JobDate >= '2025-06-01'
GROUP BY JobDate
ORDER BY JobDate DESC;

-- 売上伝票の件数確認（処理対象日）
SELECT 
    JobDate,
    COUNT(*) as SalesCount,
    COUNT(DISTINCT ProductCode + '-' + GradeCode + '-' + ClassCode + '-' + ShippingMarkCode + '-' + ShippingMarkName) as UniqueProducts
FROM SalesVouchers
WHERE JobDate = '2025-06-13'
GROUP BY JobDate;

-- 仕入伝票の件数確認（処理対象日）
SELECT 
    JobDate,
    COUNT(*) as PurchaseCount,
    COUNT(DISTINCT ProductCode + '-' + GradeCode + '-' + ClassCode + '-' + ShippingMarkCode + '-' + ShippingMarkName) as UniqueProducts
FROM PurchaseVouchers
WHERE JobDate = '2025-06-13'
GROUP BY JobDate;

-- =========================================
-- 2. アンマッチリスト処理実行後の確認
-- =========================================

-- 最新のCP在庫マスタデータセット取得
DECLARE @LatestDataSetId NVARCHAR(50);
SELECT TOP 1 @LatestDataSetId = DataSetId 
FROM CpInventoryMaster 
ORDER BY CreatedDate DESC;

PRINT 'Latest DataSetId: ' + @LatestDataSetId;

-- CP在庫マスタの作成件数確認
SELECT 
    COUNT(*) as TotalRecords,
    SUM(CASE WHEN DailyFlag = '0' THEN 1 ELSE 0 END) as ProcessedRecords,
    SUM(CASE WHEN DailySalesQuantity > 0 THEN 1 ELSE 0 END) as SalesRecords,
    SUM(CASE WHEN DailyPurchaseQuantity > 0 THEN 1 ELSE 0 END) as PurchaseRecords,
    SUM(CASE WHEN DailyStock != 0 THEN 1 ELSE 0 END) as StockRecords
FROM CpInventoryMaster
WHERE DataSetId = @LatestDataSetId;

-- 当日データ集計の確認（サンプル10件）
SELECT TOP 10
    ProductCode,
    ShippingMarkCode,
    ShippingMarkName,
    PreviousDayStock,
    DailySalesQuantity,
    DailyPurchaseQuantity,
    DailyStock,
    DailyFlag
FROM CpInventoryMaster
WHERE DataSetId = @LatestDataSetId
    AND (DailySalesQuantity > 0 OR DailyPurchaseQuantity > 0)
ORDER BY ProductCode;

-- =========================================
-- 3. 在庫マスタ最適化の確認
-- =========================================

-- JobDate='2025-06-13'の在庫マスタ件数
SELECT COUNT(*) as InventoryCount
FROM InventoryMaster
WHERE JobDate = '2025-06-13';

-- 新規登録された商品の確認（作成日が今日）
SELECT TOP 10
    ProductCode,
    ShippingMarkCode,
    ShippingMarkName,
    ProductName,
    JobDate,
    CreatedDate
FROM InventoryMaster
WHERE JobDate = '2025-06-13'
    AND CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE)
ORDER BY CreatedDate DESC;

-- =========================================
-- 4. アンマッチ検証
-- =========================================

-- 売上伝票と在庫マスタの不一致確認
SELECT COUNT(*) as UnmatchedSalesCount
FROM SalesVouchers sv
WHERE sv.JobDate = '2025-06-13'
    AND sv.VoucherType IN ('51', '52')
    AND sv.DetailType = '1'
    AND NOT EXISTS (
        SELECT 1 FROM InventoryMaster im
        WHERE im.ProductCode = sv.ProductCode
            AND im.GradeCode = sv.GradeCode
            AND im.ClassCode = sv.ClassCode
            AND im.ShippingMarkCode = sv.ShippingMarkCode
            AND im.ShippingMarkName = sv.ShippingMarkName
            AND im.JobDate = sv.JobDate
    );

-- 売上伝票とCP在庫マスタの不一致確認
SELECT COUNT(*) as UnmatchedCpSalesCount
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
            AND cp.ShippingMarkName = sv.ShippingMarkName
            AND cp.DataSetId = @LatestDataSetId
    );

-- =========================================
-- 5. 処理履歴の確認（誤操作防止機能）
-- =========================================

-- 最新の処理履歴確認
SELECT TOP 5
    ProcessType,
    JobDate,
    DatasetId,
    StartTime,
    EndTime,
    Status,
    ErrorMessage
FROM ProcessHistory
ORDER BY StartTime DESC;

-- データセット管理の確認
SELECT TOP 5
    DatasetId,
    CreatedAt,
    ImportedFiles,
    RecordCount,
    Description
FROM DatasetManagement
ORDER BY CreatedAt DESC;