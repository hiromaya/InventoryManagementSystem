-- CP在庫マスタ重複問題修正用 - データベースクリーンアップスクリプト
-- =========================================

-- 修正適用前に実行してください

-- 1. 現在の状況確認
-- =========================================
PRINT '=== 修正前の状況確認 ===';

-- CP在庫マスタのDataSetId別レコード数
SELECT 
    DataSetId,
    COUNT(*) as RecordCount,
    MIN(CreatedDate) as FirstCreated,
    MAX(CreatedDate) as LastCreated
FROM CpInventoryMaster
GROUP BY DataSetId
ORDER BY MAX(CreatedDate) DESC;

-- 重複商品の確認（同じ商品が複数のDataSetIdに存在）
SELECT 
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark,
    COUNT(DISTINCT DataSetId) as DataSetCount,
    COUNT(*) as TotalRecords
FROM CpInventoryMaster
GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
HAVING COUNT(DISTINCT DataSetId) > 1
ORDER BY COUNT(DISTINCT DataSetId) DESC;

-- DailyFlagの状況確認
SELECT 
    DataSetId,
    DailyFlag,
    COUNT(*) as RecordCount,
    AVG(CAST(DailyPurchaseQuantity AS FLOAT)) as AvgPurchaseQuantity,
    AVG(CAST(DailySalesQuantity AS FLOAT)) as AvgSalesQuantity
FROM CpInventoryMaster
GROUP BY DataSetId, DailyFlag
ORDER BY DataSetId, DailyFlag;

-- 2. バックアップ作成（念のため）
-- =========================================
PRINT '=== バックアップ作成 ===';

-- 修正前のCP在庫マスタをバックアップテーブルに保存
IF OBJECT_ID('CpInventoryMaster_Backup', 'U') IS NOT NULL
    DROP TABLE CpInventoryMaster_Backup;

SELECT * 
INTO CpInventoryMaster_Backup 
FROM CpInventoryMaster;

PRINT 'バックアップテーブル CpInventoryMaster_Backup を作成しました';

-- バックアップ件数確認
SELECT COUNT(*) as BackupRecordCount FROM CpInventoryMaster_Backup;

-- 3. クリーンアップ実行
-- =========================================
PRINT '=== CP在庫マスタクリーンアップ実行 ===';

-- 削除前の件数
DECLARE @BeforeCount INT;
SELECT @BeforeCount = COUNT(*) FROM CpInventoryMaster;
PRINT 'クリーンアップ前のレコード数: ' + CAST(@BeforeCount AS NVARCHAR(10));

-- CP在庫マスタの全レコードを削除
DELETE FROM CpInventoryMaster;

-- 削除後の確認
DECLARE @AfterCount INT;
SELECT @AfterCount = COUNT(*) FROM CpInventoryMaster;
PRINT 'クリーンアップ後のレコード数: ' + CAST(@AfterCount AS NVARCHAR(10));
PRINT '削除されたレコード数: ' + CAST(@BeforeCount - @AfterCount AS NVARCHAR(10));

-- 4. 検証
-- =========================================
PRINT '=== クリーンアップ結果検証 ===';

-- CP在庫マスタが空であることを確認
IF (SELECT COUNT(*) FROM CpInventoryMaster) = 0
    PRINT '✓ CP在庫マスタのクリーンアップが正常に完了しました';
ELSE
    PRINT '✗ CP在庫マスタにまだレコードが残っています';

-- バックアップが正常に作成されていることを確認
IF OBJECT_ID('CpInventoryMaster_Backup', 'U') IS NOT NULL
BEGIN
    DECLARE @BackupCount INT;
    SELECT @BackupCount = COUNT(*) FROM CpInventoryMaster_Backup;
    PRINT '✓ バックアップテーブルに ' + CAST(@BackupCount AS NVARCHAR(10)) + ' 件のレコードが保存されています';
END
ELSE
    PRINT '✗ バックアップテーブルが作成されていません';

PRINT '=== クリーンアップ完了 ===';
PRINT 'アンマッチリスト処理を実行してください: dotnet run -- unmatch-list 2025-06-13';

-- 5. 復元用SQLコメント
-- =========================================
/*
-- 必要に応じてバックアップから復元する場合（通常は不要）
-- INSERT INTO CpInventoryMaster SELECT * FROM CpInventoryMaster_Backup;

-- バックアップテーブルの削除（クリーンアップ完了後）
-- DROP TABLE CpInventoryMaster_Backup;
*/