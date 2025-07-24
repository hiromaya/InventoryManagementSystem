-- =============================================
-- InventoryMaster主キー変更前のデータ分析スクリプト（修正版）
-- 作成日: 2025-07-20
-- 目的: 主キー変更（6項目→5項目）の影響を事前に分析
-- 修正: データが0件でもエラーにならないように修正
-- =============================================

USE InventoryManagementDB;
GO

PRINT '========== InventoryMaster データ分析開始 ==========';
PRINT '';

-- 1. 全レコード数の確認
PRINT '1. 全レコード数';
DECLARE @RecordCount INT;
SELECT @RecordCount = COUNT(*) FROM InventoryMaster;
SELECT @RecordCount as TotalRecords;

IF @RecordCount = 0
BEGIN
    PRINT '';
    PRINT '【警告】InventoryMasterテーブルにデータが存在しません。';
    PRINT 'テストデータを投入してから再度実行してください。';
    PRINT '';
    PRINT '========== 分析終了（データなし） ==========';
    RETURN;
END

PRINT '';

-- 2. JobDate別のレコード数
PRINT '2. JobDate別のレコード数（上位10件）';
SELECT TOP 10
    JobDate,
    COUNT(*) as RecordCount
FROM InventoryMaster
GROUP BY JobDate
ORDER BY JobDate DESC;
PRINT '';

-- 3. 5項目キーの重複状況（JobDateを除いた場合）
PRINT '3. 5項目キーで見た場合の重複状況';
WITH DuplicateKeys AS (
    SELECT 
        ProductCode, 
        GradeCode, 
        ClassCode, 
        ShippingMarkCode, 
        ShippingMarkName,
        COUNT(DISTINCT JobDate) as JobDateCount,
        COUNT(*) as RecordCount
    FROM InventoryMaster
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    HAVING COUNT(*) > 1
)
SELECT 
    ISNULL(COUNT(*), 0) as DuplicateKeyCount,
    ISNULL(SUM(RecordCount), 0) as TotalDuplicateRecords,
    ISNULL(MAX(JobDateCount), 0) as MaxJobDatesPerKey,
    ISNULL(AVG(CAST(JobDateCount as FLOAT)), 0) as AvgJobDatesPerKey
FROM DuplicateKeys;
PRINT '';

-- 4. 重複キーの詳細（上位20件）
PRINT '4. 重複キーの詳細（上位20件）';
SELECT TOP 20
    ProductCode, 
    GradeCode, 
    ClassCode, 
    ShippingMarkCode, 
    LEFT(ShippingMarkName, 20) as ShippingMarkName_Short,
    COUNT(DISTINCT JobDate) as JobDateCount,
    MIN(JobDate) as MinJobDate,
    MAX(JobDate) as MaxJobDate,
    COUNT(*) as RecordCount
FROM InventoryMaster
GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC, ProductCode;

IF @@ROWCOUNT = 0
BEGIN
    PRINT '※ 重複キーは存在しません。';
END
PRINT '';

-- 5. 在庫数量の累積状況確認（重複キーでの比較）
PRINT '5. 重複キーでの在庫数量比較（サンプル5件）';
WITH DuplicateSample AS (
    SELECT TOP 5
        ProductCode, 
        GradeCode, 
        ClassCode, 
        ShippingMarkCode, 
        ShippingMarkName
    FROM InventoryMaster
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    HAVING COUNT(*) > 1
)
SELECT 
    im.ProductCode,
    im.JobDate,
    im.CurrentStock,
    im.CurrentStockAmount,
    im.DailyStock,
    im.DailyStockAmount
FROM InventoryMaster im
INNER JOIN DuplicateSample ds
    ON im.ProductCode = ds.ProductCode
    AND im.GradeCode = ds.GradeCode
    AND im.ClassCode = ds.ClassCode
    AND im.ShippingMarkCode = ds.ShippingMarkCode
    AND im.ShippingMarkName = ds.ShippingMarkName
ORDER BY im.ProductCode, im.JobDate;

IF @@ROWCOUNT = 0
BEGIN
    PRINT '※ 比較対象の重複キーは存在しません。';
END
PRINT '';

-- 6. 最新JobDateの分布
PRINT '6. 各5項目キーの最新JobDate分布';
WITH LatestJobDates AS (
    SELECT 
        ProductCode, 
        GradeCode, 
        ClassCode, 
        ShippingMarkCode, 
        ShippingMarkName,
        MAX(JobDate) as LatestJobDate
    FROM InventoryMaster
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
)
SELECT 
    LatestJobDate,
    COUNT(*) as KeyCount
FROM LatestJobDates
GROUP BY LatestJobDate
ORDER BY LatestJobDate DESC;
PRINT '';

-- 7. データサイズの見積もり
PRINT '7. データサイズ見積もり';
SELECT 
    'Current' as Status,
    COUNT(*) as RecordCount,
    ISNULL(SUM(DATALENGTH(ProductCode) + DATALENGTH(GradeCode) + DATALENGTH(ClassCode) + 
        DATALENGTH(ShippingMarkCode) + DATALENGTH(ShippingMarkName)) / 1024.0 / 1024.0, 0) as EstimatedSizeMB
FROM InventoryMaster
UNION ALL
SELECT 
    'After PK Change' as Status,
    COUNT(DISTINCT ProductCode + '|' + GradeCode + '|' + ClassCode + '|' + 
           ShippingMarkCode + '|' + ShippingMarkName) as RecordCount,
    NULL as EstimatedSizeMB
FROM InventoryMaster;
PRINT '';

-- 8. 履歴保持が必要なデータの特定
PRINT '8. 複数JobDateを持つキーのカテゴリ別分布';
WITH MultiDateKeys AS (
    SELECT 
        ProductCode, 
        ProductCategory1,
        ProductCategory2,
        COUNT(DISTINCT JobDate) as JobDateCount
    FROM InventoryMaster
    GROUP BY ProductCode, ProductCategory1, ProductCategory2
    HAVING COUNT(DISTINCT JobDate) > 1
)
SELECT 
    ProductCategory1,
    ProductCategory2,
    COUNT(*) as ProductCount,
    AVG(CAST(JobDateCount as FLOAT)) as AvgJobDates,
    MAX(JobDateCount) as MaxJobDates
FROM MultiDateKeys
GROUP BY ProductCategory1, ProductCategory2
ORDER BY COUNT(*) DESC;

IF @@ROWCOUNT = 0
BEGIN
    PRINT '※ 複数JobDateを持つキーは存在しません。';
END
PRINT '';

-- 9. 処理頻度の高い商品の特定
PRINT '9. 更新頻度の高い商品TOP10';
SELECT TOP 10
    ProductCode,
    ProductName,
    COUNT(DISTINCT JobDate) as UpdateCount,
    MIN(JobDate) as FirstDate,
    MAX(JobDate) as LastDate,
    DATEDIFF(day, MIN(JobDate), MAX(JobDate)) as ActiveDays
FROM InventoryMaster
GROUP BY ProductCode, ProductName
HAVING COUNT(DISTINCT JobDate) > 1
ORDER BY COUNT(DISTINCT JobDate) DESC;

IF @@ROWCOUNT = 0
BEGIN
    PRINT '※ 複数回更新された商品は存在しません。';
END
PRINT '';

-- 10. 推奨事項の提示
PRINT '10. 分析結果サマリー';
DECLARE @TotalRecords INT;
DECLARE @UniqueKeys INT;
DECLARE @DuplicateKeys INT;
DECLARE @MaxDuplicates INT;

SELECT @TotalRecords = COUNT(*) FROM InventoryMaster;
SELECT @UniqueKeys = COUNT(DISTINCT ProductCode + '|' + GradeCode + '|' + ClassCode + '|' + 
                            ShippingMarkCode + '|' + ShippingMarkName) FROM InventoryMaster;
SET @DuplicateKeys = @TotalRecords - @UniqueKeys;

SELECT @MaxDuplicates = ISNULL(MAX(cnt), 0) FROM (
    SELECT COUNT(*) as cnt
    FROM InventoryMaster
    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
) t;

PRINT '- 総レコード数: ' + CAST(@TotalRecords as VARCHAR(20));
PRINT '- ユニークキー数: ' + CAST(@UniqueKeys as VARCHAR(20));
PRINT '- 削減されるレコード数: ' + CAST(@DuplicateKeys as VARCHAR(20));
IF @TotalRecords > 0
BEGIN
    PRINT '- 削減率: ' + CAST(CAST(@DuplicateKeys as FLOAT) / @TotalRecords * 100 as VARCHAR(10)) + '%';
END
ELSE
BEGIN
    PRINT '- 削減率: 0%（データなし）';
END
PRINT '- 最大重複数: ' + CAST(@MaxDuplicates as VARCHAR(20));
PRINT '';

PRINT '========== 分析完了 ==========';
PRINT '';
PRINT '【重要】この分析結果を基に、以下を検討してください：';
PRINT '1. 履歴データの保存が必要かどうか';
PRINT '2. 最新データのみで業務に影響がないか';
PRINT '3. 削減されるデータ量が許容範囲内か';

-- テストデータ投入用のサンプルクエリ
PRINT '';
PRINT '【参考】テストデータ投入サンプル：';
PRINT '/*';
PRINT '-- サンプルデータ投入（同じ5項目キーで異なるJobDateのデータ）';
PRINT 'INSERT INTO InventoryMaster (';
PRINT '    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate,';
PRINT '    PreviousStock, PreviousAmount, CurrentStock, CurrentStockAmount,';
PRINT '    DailyStock, DailyStockAmount, ProductName, ProductCategory1';
PRINT ') VALUES';
PRINT '(''1001'', ''A'', ''01'', ''100'', ''テスト荷印1'', ''2025-06-01'', 100, 10000, 120, 12000, 20, 2000, ''テスト商品1'', 1),';
PRINT '(''1001'', ''A'', ''01'', ''100'', ''テスト荷印1'', ''2025-06-02'', 120, 12000, 150, 15000, 30, 3000, ''テスト商品1'', 1),';
PRINT '(''1001'', ''A'', ''01'', ''100'', ''テスト荷印1'', ''2025-06-03'', 150, 15000, 180, 18000, 30, 3000, ''テスト商品1'', 1),';
PRINT '(''1002'', ''B'', ''02'', ''200'', ''テスト荷印2'', ''2025-06-01'', 200, 20000, 250, 25000, 50, 5000, ''テスト商品2'', 2),';
PRINT '(''1002'', ''B'', ''02'', ''200'', ''テスト荷印2'', ''2025-06-02'', 250, 25000, 300, 30000, 50, 5000, ''テスト商品2'', 2);';
PRINT '*/';