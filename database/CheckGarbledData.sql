-- 文字化けデータ確認用SQLスクリプト
-- 在庫管理システム - CP在庫マスタの文字化け調査

-- =========================================
-- 1. 在庫マスタの文字化け確認
-- =========================================
-- 文字化けしている荷印名を持つレコードを表示
SELECT TOP 20
    ShippingMarkCode,
    ManualShippingMark,
    CAST(ManualShippingMark AS VARBINARY(MAX)) as BinaryData,
    ProductCode,
    GradeCode,
    ClassCode
FROM InventoryMaster
WHERE ManualShippingMark LIKE '%?%'
ORDER BY ShippingMarkCode;

-- =========================================
-- 2. CP在庫マスタの文字化け確認
-- =========================================
-- 最新のデータセットIDを取得
DECLARE @LatestDataSetId NVARCHAR(50);
SELECT TOP 1 @LatestDataSetId = DataSetId 
FROM CpInventoryMaster 
ORDER BY CreatedDate DESC;

PRINT 'Latest DataSetId: ' + @LatestDataSetId;

-- CP在庫マスタの文字化けデータを表示
SELECT TOP 20
    DataSetId,
    ShippingMarkCode,
    ManualShippingMark,
    CAST(ManualShippingMark AS VARBINARY(MAX)) as BinaryData,
    ProductCode,
    GradeCode,
    ClassCode
FROM CpInventoryMaster
WHERE DataSetId = @LatestDataSetId
    AND ManualShippingMark LIKE '%?%'
ORDER BY ShippingMarkCode;

-- =========================================
-- 3. 文字化けデータの統計
-- =========================================
-- 在庫マスタの文字化け件数
SELECT 
    COUNT(*) as GarbledCount,
    COUNT(DISTINCT ShippingMarkCode) as UniqueShippingMarkCodes
FROM InventoryMaster
WHERE ManualShippingMark LIKE '%?%';

-- CP在庫マスタの文字化け件数（最新データセット）
SELECT 
    COUNT(*) as GarbledCount,
    COUNT(DISTINCT ShippingMarkCode) as UniqueShippingMarkCodes
FROM CpInventoryMaster
WHERE DataSetId = @LatestDataSetId
    AND ManualShippingMark LIKE '%?%';

-- =========================================
-- 4. 売上伝票との比較
-- =========================================
-- 売上伝票の荷印名と在庫マスタの荷印名を比較
SELECT TOP 10
    sv.ShippingMarkCode,
    sv.ManualShippingMark as SalesManualShippingMark,
    im.ManualShippingMark as InventoryManualShippingMark,
    CASE 
        WHEN im.ManualShippingMark LIKE '%?%' THEN '文字化け'
        WHEN im.ManualShippingMark IS NULL THEN 'マスタなし'
        ELSE '正常'
    END as Status
FROM SalesVouchers sv
LEFT JOIN InventoryMaster im 
    ON sv.ShippingMarkCode = im.ShippingMarkCode
    AND sv.ProductCode = im.ProductCode
    AND sv.GradeCode = im.GradeCode
    AND sv.ClassCode = im.ClassCode
WHERE sv.JobDate = CAST(GETDATE() AS DATE)
    AND sv.ManualShippingMark IS NOT NULL
    AND sv.ManualShippingMark != ''
ORDER BY sv.ShippingMarkCode;

-- =========================================
-- 5. 修復候補の確認
-- =========================================
-- 在庫マスタで修復可能なデータを確認
SELECT TOP 10
    cp.DataSetId,
    cp.ProductCode,
    cp.ShippingMarkCode,
    cp.ManualShippingMark as CP_ManualShippingMark,
    im.ManualShippingMark as IM_ManualShippingMark,
    CASE 
        WHEN im.ManualShippingMark IS NULL THEN '在庫マスタなし'
        WHEN im.ManualShippingMark LIKE '%?%' THEN '在庫マスタも文字化け'
        ELSE '修復可能'
    END as RepairStatus
FROM CpInventoryMaster cp
LEFT JOIN InventoryMaster im 
    ON cp.ProductCode = im.ProductCode 
    AND cp.GradeCode = im.GradeCode 
    AND cp.ClassCode = im.ClassCode 
    AND cp.ShippingMarkCode = im.ShippingMarkCode
WHERE cp.DataSetId = @LatestDataSetId
    AND cp.ManualShippingMark LIKE '%?%'
ORDER BY RepairStatus, cp.ShippingMarkCode;

-- =========================================
-- 6. 照合順序の確認
-- =========================================
-- データベースとテーブルの照合順序を確認
SELECT 
    DB_NAME() as DatabaseName,
    DATABASEPROPERTYEX(DB_NAME(), 'Collation') as DatabaseCollation;

-- 各テーブルのカラム照合順序を確認
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    COLLATION_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('InventoryMaster', 'CpInventoryMaster', 'SalesVouchers')
    AND COLUMN_NAME = 'ManualShippingMark'
    AND COLLATION_NAME IS NOT NULL;