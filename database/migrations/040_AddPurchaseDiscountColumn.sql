-- ===================================================================
-- 仕入値引専用カラムの追加とデータ移行
-- ファイル: 040_AddPurchaseDiscountColumn.sql
-- 作成日: 2025-07-23
-- 目的: DailyDiscountAmountを歩引額専用とし、仕入値引専用カラムを追加
-- ===================================================================

PRINT '=== 仕入値引専用カラム追加処理開始 ===';

-- 1. カラム存在チェックと追加
IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CpInventoryMaster' 
    AND COLUMN_NAME = 'DailyPurchaseDiscountAmount'
)
BEGIN
    PRINT '1. DailyPurchaseDiscountAmount カラムを追加しています...';
    
    ALTER TABLE CpInventoryMaster 
    ADD DailyPurchaseDiscountAmount DECIMAL(12,4) NOT NULL DEFAULT 0;
    
    PRINT '✅ DailyPurchaseDiscountAmount カラムが追加されました';
END
ELSE
BEGIN
    PRINT '⚠️ DailyPurchaseDiscountAmount カラムは既に存在します（処理をスキップ）';
END

-- 2. 既存データの移行処理（冪等性確保）
PRINT '2. 既存データの移行処理を開始しています...';

-- 移行対象データの確認
DECLARE @TargetCount INT;
SELECT @TargetCount = COUNT(*)
FROM CpInventoryMaster
WHERE JobDate >= '2025-06-01' 
  AND DailyDiscountAmount < 0  -- 仕入値引は通常マイナス値
  AND DailyPurchaseDiscountAmount = 0;  -- まだ移行されていない

IF @TargetCount > 0
BEGIN
    PRINT CONCAT('移行対象データ: ', @TargetCount, '件');
    
    -- 実際の移行処理（バッチ処理で実行）
    DECLARE @BatchSize INT = 1000;
    DECLARE @ProcessedCount INT = 0;
    
    WHILE @ProcessedCount < @TargetCount
    BEGIN
        -- バッチ単位で更新
        UPDATE TOP (@BatchSize) CpInventoryMaster
        SET 
            DailyPurchaseDiscountAmount = ABS(DailyDiscountAmount), -- マイナス値をプラスに変換
            DailyDiscountAmount = 0  -- 歩引額用フィールドは0にリセット
        FROM CpInventoryMaster
        WHERE JobDate >= '2025-06-01' 
          AND DailyDiscountAmount < 0
          AND DailyPurchaseDiscountAmount = 0;
        
        SET @ProcessedCount = @ProcessedCount + @@ROWCOUNT;
        
        -- 進捗表示
        IF @ProcessedCount % 5000 = 0 OR @ProcessedCount >= @TargetCount
        BEGIN
            PRINT CONCAT('移行済み: ', @ProcessedCount, '/', @TargetCount, '件');
        END
        
        -- 無限ループ防止
        IF @@ROWCOUNT = 0 BREAK;
    END
    
    PRINT '✅ データ移行が完了しました';
END
ELSE
BEGIN
    PRINT '⚠️ 移行対象データがありません（処理をスキップ）';
END

-- 3. データ整合性チェック
PRINT '3. データ整合性チェックを実行しています...';

-- 移行後の統計情報
DECLARE @PurchaseDiscountCount INT, @WalkingDiscountCount INT;

SELECT @PurchaseDiscountCount = COUNT(*)
FROM CpInventoryMaster
WHERE DailyPurchaseDiscountAmount > 0;

SELECT @WalkingDiscountCount = COUNT(*)
FROM CpInventoryMaster
WHERE DailyDiscountAmount > 0;

PRINT CONCAT('仕入値引データ件数: ', @PurchaseDiscountCount);
PRINT CONCAT('歩引額データ件数: ', @WalkingDiscountCount);

-- データ整合性の警告チェック
DECLARE @AnomalyCount INT;
SELECT @AnomalyCount = COUNT(*)
FROM CpInventoryMaster
WHERE DailyDiscountAmount < 0  -- 歩引額がマイナスになっている異常データ
   OR (DailyPurchaseDiscountAmount > 0 AND DailyDiscountAmount > 0);  -- 両方に値がある異常データ

IF @AnomalyCount > 0
BEGIN
    PRINT CONCAT('⚠️ 警告: 異常データが ', @AnomalyCount, '件見つかりました');
    PRINT '詳細確認のため以下のクエリを実行してください:';
    PRINT 'SELECT TOP 10 * FROM CpInventoryMaster WHERE DailyDiscountAmount < 0 OR (DailyPurchaseDiscountAmount > 0 AND DailyDiscountAmount > 0);';
END
ELSE
BEGIN
    PRINT '✅ データ整合性チェック完了（異常なし）';
END

-- 4. インデックスの追加（検索パフォーマンス向上）
PRINT '4. インデックスの追加を確認しています...';

IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('CpInventoryMaster') 
    AND name = 'IX_CpInventoryMaster_DailyPurchaseDiscountAmount'
)
BEGIN
    PRINT 'DailyPurchaseDiscountAmount用のインデックスを作成しています...';
    
    CREATE NONCLUSTERED INDEX IX_CpInventoryMaster_DailyPurchaseDiscountAmount
    ON CpInventoryMaster (JobDate, DailyPurchaseDiscountAmount)
    WHERE DailyPurchaseDiscountAmount != 0;
    
    PRINT '✅ インデックスが作成されました';
END
ELSE
BEGIN
    PRINT '⚠️ インデックスは既に存在します（処理をスキップ）';
END

-- 5. 検証用サンプルデータの表示
PRINT '5. 検証用サンプルデータ:';

SELECT TOP 5
    ProductCode,
    JobDate,
    DailyPurchaseDiscountAmount as 仕入値引,
    DailyDiscountAmount as 歩引額
FROM CpInventoryMaster
WHERE DailyPurchaseDiscountAmount > 0
ORDER BY JobDate DESC, ProductCode;

PRINT '=== 仕入値引専用カラム追加処理完了 ===';

-- 6. マイグレーション履歴への記録
IF NOT EXISTS (SELECT * FROM MigrationHistory WHERE MigrationId = '040_AddPurchaseDiscountColumn')
BEGIN
    INSERT INTO MigrationHistory (MigrationId, AppliedAt, Description)
    VALUES ('040_AddPurchaseDiscountColumn', GETDATE(), '仕入値引専用カラム(DailyPurchaseDiscountAmount)の追加とデータ移行');
    
    PRINT '✅ マイグレーション履歴に記録されました';
END