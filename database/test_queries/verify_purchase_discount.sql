-- ===================================================================
-- 仕入値引データ検証用SQL
-- ファイル: verify_purchase_discount.sql
-- 作成日: 2025-07-23
-- 目的: DailyPurchaseDiscountAmount実装後の動作検証
-- ===================================================================

PRINT '=== 仕入値引データ検証開始 ===';
PRINT CONCAT('検証日時: ', FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm:ss'));
PRINT '';

-- 1. 基本データ確認
PRINT '1. 基本データ確認';
PRINT '==================';

SELECT 
    ProductCode,
    DailyPurchaseDiscountAmount as 仕入値引,
    DailyDiscountAmount as 歩引額,
    JobDate
FROM CpInventoryMaster
WHERE JobDate = '2025-06-02'
    AND (DailyPurchaseDiscountAmount != 0 OR DailyDiscountAmount != 0)
ORDER BY ProductCode;

PRINT '';

-- 2. 商品15020の詳細確認
PRINT '2. 商品15020の詳細確認';
PRINT '======================';

SELECT 
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
    DailyPurchaseDiscountAmount as 仕入値引,
    DailyDiscountAmount as 歩引額,
    JobDate
FROM CpInventoryMaster
WHERE ProductCode = '15020' AND JobDate = '2025-06-02'
ORDER BY GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName;

PRINT '';

-- 3. 仕入伝票の明細種別3との照合
PRINT '3. 仕入伝票との照合確認';
PRINT '========================';

SELECT 
    pv.ProductCode,
    SUM(pv.Amount) as 仕入伝票_値引,
    SUM(cp.DailyPurchaseDiscountAmount) as CP在庫_仕入値引,
    CASE 
        WHEN SUM(pv.Amount) = SUM(cp.DailyPurchaseDiscountAmount) THEN '✅ 一致'
        ELSE '❌ 不一致'
    END as 整合性
FROM PurchaseVouchers pv
LEFT JOIN CpInventoryMaster cp 
    ON cp.ProductCode = pv.ProductCode 
    AND cp.GradeCode = pv.GradeCode
    AND cp.ClassCode = pv.ClassCode
    AND cp.ShippingMarkCode = pv.ShippingMarkCode
    AND cp.ShippingMarkName = pv.ShippingMarkName
    AND cp.JobDate = pv.JobDate
WHERE pv.JobDate = '2025-06-02' AND pv.DetailType = '3'
GROUP BY pv.ProductCode
ORDER BY pv.ProductCode;

PRINT '';

-- 4. 統計情報
PRINT '4. 統計情報';
PRINT '===========';

DECLARE @PurchaseDiscountCount INT, @WalkingDiscountCount INT, @TotalRecords INT;

SELECT @PurchaseDiscountCount = COUNT(*)
FROM CpInventoryMaster
WHERE JobDate = '2025-06-02' AND DailyPurchaseDiscountAmount > 0;

SELECT @WalkingDiscountCount = COUNT(*)
FROM CpInventoryMaster
WHERE JobDate = '2025-06-02' AND DailyDiscountAmount > 0;

SELECT @TotalRecords = COUNT(*)
FROM CpInventoryMaster
WHERE JobDate = '2025-06-02';

PRINT CONCAT('総レコード数: ', @TotalRecords);
PRINT CONCAT('仕入値引データ件数: ', @PurchaseDiscountCount);
PRINT CONCAT('歩引額データ件数: ', @WalkingDiscountCount);

-- 5. データ整合性チェック
PRINT '';
PRINT '5. データ整合性チェック';
PRINT '======================';

-- 異常データの確認
DECLARE @AnomalyCount INT;
SELECT @AnomalyCount = COUNT(*)
FROM CpInventoryMaster
WHERE JobDate = '2025-06-02'
  AND (
    DailyPurchaseDiscountAmount < 0 OR  -- 仕入値引がマイナス（通常はプラス値）
    DailyDiscountAmount < 0 OR          -- 歩引額がマイナス
    (DailyPurchaseDiscountAmount > 0 AND DailyDiscountAmount > 0) -- 両方に値がある
  );

IF @AnomalyCount = 0
BEGIN
    PRINT '✅ データ整合性チェック完了（異常なし）';
END
ELSE
BEGIN
    PRINT CONCAT('⚠️ 警告: 異常データが ', @AnomalyCount, '件見つかりました');
    
    -- 異常データの詳細表示
    SELECT TOP 10
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
        DailyPurchaseDiscountAmount as 仕入値引,
        DailyDiscountAmount as 歩引額,
        CASE 
            WHEN DailyPurchaseDiscountAmount < 0 THEN '仕入値引がマイナス'
            WHEN DailyDiscountAmount < 0 THEN '歩引額がマイナス'
            WHEN DailyPurchaseDiscountAmount > 0 AND DailyDiscountAmount > 0 THEN '両方に値あり'
            ELSE '不明'
        END as 異常内容
    FROM CpInventoryMaster
    WHERE JobDate = '2025-06-02'
      AND (
        DailyPurchaseDiscountAmount < 0 OR
        DailyDiscountAmount < 0 OR
        (DailyPurchaseDiscountAmount > 0 AND DailyDiscountAmount > 0)
      );
END

-- 6. 商品日報で表示されるべき金額の確認
PRINT '';
PRINT '6. 商品日報表示予定金額';
PRINT '======================';

-- 商品ごとの集計（DailyReportServiceと同じロジック）
SELECT 
    ProductCode,
    SUM(DailyPurchaseDiscountAmount) as 商品日報_仕入値引表示予定額,
    COUNT(*) as 明細数
FROM CpInventoryMaster
WHERE JobDate = '2025-06-02'
  AND DailyPurchaseDiscountAmount > 0
GROUP BY ProductCode
ORDER BY ProductCode;

-- 7. 特定商品15020の詳細分析
PRINT '';
PRINT '7. 商品15020の詳細分析';
PRINT '=====================';

-- 15020の全明細
SELECT 
    ProductCode + '_' + GradeCode + '_' + ClassCode + '_' + ShippingMarkCode + '_' + ShippingMarkName as 複合キー,
    DailyPurchaseDiscountAmount as 仕入値引,
    DailyDiscountAmount as 歩引額
FROM CpInventoryMaster
WHERE ProductCode = '15020' AND JobDate = '2025-06-02'
ORDER BY GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName;

-- 15020の合計額
SELECT 
    ProductCode,
    SUM(DailyPurchaseDiscountAmount) as 仕入値引合計,
    SUM(DailyDiscountAmount) as 歩引額合計
FROM CpInventoryMaster
WHERE ProductCode = '15020' AND JobDate = '2025-06-02'
GROUP BY ProductCode;

PRINT '';
PRINT '=== 仕入値引データ検証完了 ===';

-- 8. 実行結果サマリー
PRINT '';
PRINT '8. 実行結果サマリー';
PRINT '==================';
PRINT '期待される結果:';
PRINT '- 商品15020の仕入値引合計: 19,900円';
PRINT '- 仕入値引と歩引額の分離: 同一商品で両方に値がないこと';
PRINT '- 商品日報での表示: DailyPurchaseDiscountAmountから取得';
PRINT '';