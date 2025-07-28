-- 商品コード88888のアンマッチテスト用SQLクエリ
-- 2025-06-01のデータを確認

USE InventoryManagementDB;

PRINT '=== アンマッチテストSQL実行開始 ===';
PRINT '対象日: 2025-06-01';
PRINT '対象商品: 88888';

-- 1. 売上伝票データの確認
PRINT '';
PRINT '1. 売上伝票データ（商品コード88888）:';
SELECT 
    JobDate,
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ShippingMarkName,
    Quantity,
    DataSetId
FROM SalesVouchers 
WHERE JobDate = '2025-06-01' 
AND ProductCode = '88888'
AND VoucherType IN ('51', '52')
AND DetailType = '1'
AND Quantity > 0;

-- 2. 在庫マスタの確認（商品コード88888）
PRINT '';
PRINT '2. 在庫マスタ（商品コード88888）:';
SELECT 
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ShippingMarkName,
    CurrentStock,
    JobDate,
    DataSetId
FROM InventoryMaster 
WHERE ProductCode = '88888'
ORDER BY JobDate DESC;

-- 3. UN在庫マスタテーブルの存在確認
PRINT '';
PRINT '3. UN在庫マスタテーブルの存在確認:';
IF OBJECT_ID('UnInventoryMaster', 'U') IS NOT NULL
BEGIN
    PRINT 'UnInventoryMasterテーブルが存在します';
    
    -- UN在庫マスタのデータ確認
    PRINT '';
    PRINT '4. UN在庫マスタデータ（2025-06-01）:';
    SELECT 
        ProductCode,
        GradeCode,
        ClassCode,
        ShippingMarkCode,
        ShippingMarkName,
        PreviousDayStock,
        DailyStock,
        JobDate,
        DataSetId
    FROM UnInventoryMaster 
    WHERE JobDate = '2025-06-01'
    ORDER BY ProductCode;
    
    -- 商品コード88888の状況確認
    PRINT '';
    PRINT '5. UN在庫マスタ（商品コード88888）:';
    SELECT 
        ProductCode,
        GradeCode,
        ClassCode,
        ShippingMarkCode,
        ShippingMarkName,
        PreviousDayStock,
        DailyStock,
        JobDate,
        DataSetId
    FROM UnInventoryMaster 
    WHERE ProductCode = '88888';
END
ELSE
BEGIN
    PRINT 'UnInventoryMasterテーブルが存在しません';
END

-- 6. DataSetManagementの確認
PRINT '';
PRINT '6. DataSetManagement（2025-06-01）:';
SELECT 
    DataSetId,
    JobDate,
    ImportedAt,
    UnmatchListCreatedAt,
    ProcessStatus
FROM DataSetManagement 
WHERE JobDate = '2025-06-01'
ORDER BY ImportedAt DESC;

-- 7. 予想されるアンマッチ結果
PRINT '';
PRINT '7. 予想されるアンマッチ結果:';
PRINT '- 商品コード88888の売上伝票が存在する場合';
PRINT '- 在庫マスタに該当商品が存在しない場合';
PRINT '- UN在庫マスタで該当商品の在庫が0の場合';
PRINT '- アンマッチリストに「該当無」エラーが1件検出されるべき';

PRINT '';
PRINT '=== アンマッチテストSQL実行完了 ===';