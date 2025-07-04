-- import-folderコマンド実行後の検証用SQL

-- 1. 前月末在庫が正しく設定されているか確認
SELECT 
    COUNT(*) as 総件数,
    COUNT(CASE WHEN PreviousMonthQuantity > 0 THEN 1 END) as 前月末在庫設定済み,
    COUNT(CASE WHEN PreviousMonthAmount > 0 THEN 1 END) as 前月末金額設定済み
FROM InventoryMaster
WHERE JobDate = '2025-06-27';  -- 実行時のジョブ日付に変更

-- 2. 前月末在庫の設定状況詳細
SELECT 
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ShippingMarkName,
    PreviousMonthQuantity,
    PreviousMonthAmount,
    CurrentStock,
    CurrentStockAmount
FROM InventoryMaster
WHERE JobDate = '2025-06-27'
  AND PreviousMonthQuantity > 0
ORDER BY ProductCode;

-- 3. 商品別の在庫状況サマリー
SELECT 
    ProductCode,
    COUNT(*) as レコード数,
    SUM(PreviousMonthQuantity) as 前月末在庫数量合計,
    SUM(CurrentStock) as 現在在庫数量合計
FROM InventoryMaster
WHERE JobDate = '2025-06-27'
GROUP BY ProductCode
ORDER BY ProductCode;

-- 4. 前月末在庫データのインポート状況確認
SELECT 
    ds.DataSetId,
    ds.ImportType,
    ds.ImportDate,
    COUNT(adj.Id) as レコード数
FROM DataSets ds
LEFT JOIN InventoryAdjustments adj ON ds.DataSetId = adj.DataSetId
WHERE ds.ImportType = 'InventoryAdjustment'
    AND ds.ImportDate >= CAST(GETDATE() AS DATE)
GROUP BY ds.DataSetId, ds.ImportType, ds.ImportDate
ORDER BY ds.ImportDate DESC;

-- 5. アンマッチリストの状況（初期在庫設定後の改善状況）
SELECT 
    UnmatchType,
    COUNT(*) as 件数,
    SUM(Quantity) as 数量合計,
    SUM(Amount) as 金額合計
FROM UnmatchList
WHERE JobDate = '2025-06-27'
GROUP BY UnmatchType;

-- 6. 在庫マスタのJobDate分布（複数日付のレコードがないか確認）
SELECT 
    JobDate,
    COUNT(*) as レコード数,
    COUNT(DISTINCT ProductCode + GradeCode + ClassCode + ShippingMarkCode + ShippingMarkName) as ユニークキー数
FROM InventoryMaster
GROUP BY JobDate
ORDER BY JobDate DESC;