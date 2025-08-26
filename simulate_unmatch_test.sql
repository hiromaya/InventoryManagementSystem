-- アンマッチリスト処理のシミュレーションテスト
-- 商品コード88888でのテスト
USE InventoryManagementDB;

PRINT '=== アンマッチリスト処理シミュレーション ===';
PRINT '対象: 商品コード88888, 日付: 2025-06-01';

DECLARE @TestDataSetId NVARCHAR(100) = 'TEST_88888_' + FORMAT(GETDATE(), 'yyyyMMdd_HHmmss');
DECLARE @JobDate DATE = '2025-06-01';

-- Step 1: テスト用売上伝票データの確認
PRINT '';
PRINT 'Step 1: テスト用売上伝票データの確認';
SELECT 
    COUNT(*) as 売上伝票件数,
    SUM(CASE WHEN ProductCode = '88888' THEN 1 ELSE 0 END) as 商品88888件数
FROM SalesVouchers 
WHERE JobDate = @JobDate 
AND VoucherType IN ('51', '52')
AND DetailType = '1'
AND Quantity > 0
AND ProductCode != '00000';

-- Step 2: UN在庫マスタテーブルの存在確認
IF OBJECT_ID('UnInventoryMaster', 'U') IS NULL
BEGIN
    PRINT '';
    PRINT 'エラー: UnInventoryMasterテーブルが存在しません';
    PRINT '対処: database/tables/UnInventoryMaster.sql を実行してください';
    RETURN;
END

-- Step 3: ストアドプロシージャの存在確認
IF NOT EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateUnInventoryFromInventoryMaster')
BEGIN
    PRINT '';
    PRINT 'エラー: sp_CreateUnInventoryFromInventoryMaster ストアドプロシージャが存在しません';
    PRINT '対処: database/procedures/sp_CreateUnInventoryFromInventoryMaster.sql を実行してください';
    RETURN;
END

-- Step 4: UN在庫マスタ作成のシミュレーション
PRINT '';
PRINT 'Step 4: UN在庫マスタ作成シミュレーション';

-- 4.1: 伝票に存在する5項目キーを抽出（アンマッチ対象のみ）
WITH VoucherKeys AS (
    -- 売上伝票（出荷データのみ: 数量>0）
    SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark 
    FROM SalesVouchers 
    WHERE JobDate = @JobDate 
    AND VoucherType IN ('51', '52')  -- 掛売・現売
    AND DetailType = '1'             -- 商品明細のみ
    AND Quantity > 0                 -- 出荷データ（通常売上）
    AND ProductCode != '00000'       -- 商品コード「00000」除外
    
    UNION
    
    -- 仕入伝票（仕入返品のみ: 数量<0）
    SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark 
    FROM PurchaseVouchers 
    WHERE JobDate = @JobDate 
    AND VoucherType IN ('11', '12')  -- 掛仕入・現金仕入
    AND DetailType = '1'             -- 商品明細のみ
    AND Quantity < 0                 -- 仕入返品（出荷データ）
    AND ProductCode != '00000'       -- 商品コード「00000」除外
    
    UNION
    
    -- 在庫調整（出荷データのみ: 数量>0）
    SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark 
    FROM InventoryAdjustments 
    WHERE JobDate = @JobDate
    AND VoucherType = '71'           -- 在庫調整伝票
    AND DetailType = '1'             -- 明細種1のみ
    AND Quantity > 0                 -- 出荷データのみ
    AND ProductCode != '00000'       -- 商品コード「00000」除外
    AND UnitCode NOT IN ('02', '05') -- ギフト経費・加工費B除外
)
SELECT 
    '対象キー数' as 項目,
    COUNT(*) as 件数
FROM VoucherKeys;

-- 4.2: 商品コード88888の5項目キーを確認
PRINT '';
PRINT 'Step 4.2: 商品コード88888の5項目キー';
WITH VoucherKeys AS (
    -- 同じクエリ
    SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark 
    FROM SalesVouchers 
    WHERE JobDate = @JobDate 
    AND VoucherType IN ('51', '52')
    AND DetailType = '1'
    AND Quantity > 0
    AND ProductCode != '00000'
    
    UNION
    
    SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark 
    FROM PurchaseVouchers 
    WHERE JobDate = @JobDate 
    AND VoucherType IN ('11', '12')
    AND DetailType = '1'
    AND Quantity < 0
    AND ProductCode != '00000'
    
    UNION
    
    SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark 
    FROM InventoryAdjustments 
    WHERE JobDate = @JobDate
    AND VoucherType = '71'
    AND DetailType = '1'
    AND Quantity > 0
    AND ProductCode != '00000'
    AND UnitCode NOT IN ('02', '05')
)
SELECT 
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark,
    '伝票存在' as 状態
FROM VoucherKeys
WHERE ProductCode = '88888';

-- Step 5: 在庫マスタでの該当確認
PRINT '';
PRINT 'Step 5: 在庫マスタでの該当確認（商品コード88888）';
SELECT 
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark,
    CurrentStock,
    JobDate,
    IsActive,
    '在庫マスタ存在' as 状態
FROM InventoryMaster 
WHERE ProductCode = '88888'
AND JobDate <= @JobDate
AND IsActive = 1
ORDER BY JobDate DESC;

-- Step 6: アンマッチ検出のシミュレーション
PRINT '';
PRINT 'Step 6: アンマッチ検出シミュレーション';

WITH VoucherKeys AS (
    SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark 
    FROM SalesVouchers 
    WHERE JobDate = @JobDate 
    AND VoucherType IN ('51', '52')
    AND DetailType = '1'
    AND Quantity > 0
    AND ProductCode != '00000'
    AND ProductCode = '88888'  -- 商品コード88888のみ
)
SELECT 
    vk.ProductCode,
    vk.GradeCode,
    vk.ClassCode,
    vk.ShippingMarkCode,
    vk.ManualShippingMark,
    CASE 
        WHEN im.ProductCode IS NULL THEN 'アンマッチ: 在庫マスタ該当無'
        WHEN ISNULL(im.CurrentStock, 0) = 0 THEN 'アンマッチ: 在庫0（旧仕様）'
        ELSE '正常: 在庫あり'
    END as 判定結果,
    ISNULL(im.CurrentStock, 0) as 在庫数量
FROM VoucherKeys vk
LEFT JOIN InventoryMaster im ON (
    im.ProductCode = vk.ProductCode
    AND im.GradeCode = vk.GradeCode
    AND im.ClassCode = vk.ClassCode
    AND im.ShippingMarkCode = vk.ShippingMarkCode
    AND im.ManualShippingMark = vk.ManualShippingMark
    AND im.JobDate <= @JobDate
    AND im.IsActive = 1
);

PRINT '';
PRINT '=== シミュレーション完了 ===';
PRINT '期待結果: 商品コード88888で「在庫マスタ該当無」のアンマッチが検出される';
PRINT '新仕様: 在庫0は正常（マイナス在庫許容）';