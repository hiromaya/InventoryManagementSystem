-- ===================================================================
-- 仕入値引専用カラムの追加とデータ移行
-- ファイル: 040_AddPurchaseDiscountColumn.sql
-- 作成日: 2025-07-23
-- 目的: DailyDiscountAmountを歩引額専用とし、仕入値引専用カラムを追加
-- ===================================================================

SET NOCOUNT ON;
GO

-- マイグレーション履歴テーブルの確認と作成
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MigrationHistory')
BEGIN
    CREATE TABLE MigrationHistory (
        MigrationId NVARCHAR(255) PRIMARY KEY,
        AppliedAt DATETIME NOT NULL,
        [Description] NVARCHAR(MAX) NULL
    );
    PRINT 'MigrationHistory テーブルを作成しました';
END
ELSE
BEGIN
    -- Description列が存在しない場合は追加
    IF NOT EXISTS (
        SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'MigrationHistory' 
        AND COLUMN_NAME = 'Description'
    )
    BEGIN
        ALTER TABLE MigrationHistory ADD [Description] NVARCHAR(MAX) NULL;
        PRINT 'MigrationHistory テーブルに Description 列を追加しました';
    END
END
GO

PRINT '=== 仕入値引専用カラム追加処理開始 ===';
PRINT '';

-- 処理実行用の動的SQL変数
DECLARE @SQL NVARCHAR(MAX);
DECLARE @ColumnExists BIT = 0;

-- カラムの存在確認
IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CpInventoryMaster' 
    AND COLUMN_NAME = 'DailyPurchaseDiscountAmount'
)
BEGIN
    SET @ColumnExists = 1;
END

-- 1. カラム追加処理
IF @ColumnExists = 0
BEGIN
    PRINT '1. DailyPurchaseDiscountAmount カラムを追加しています...';
    
    ALTER TABLE CpInventoryMaster 
    ADD DailyPurchaseDiscountAmount DECIMAL(12,4) NOT NULL DEFAULT 0;
    
    PRINT '✅ カラムが追加されました';
    PRINT '';
END
ELSE
BEGIN
    PRINT '1. DailyPurchaseDiscountAmount カラムは既に存在します';
    PRINT '';
END
GO

-- ここで必ずGOを入れてバッチを区切る

-- 2. データ移行処理（完全動的SQL）
PRINT '2. データ移行処理を確認しています...';

DECLARE @SQL NVARCHAR(MAX);

-- カラムが存在する場合のみ処理を実行
SET @SQL = N'
DECLARE @TargetCount INT = 0;
DECLARE @ProcessedCount INT = 0;
DECLARE @BatchSize INT = 1000;
DECLARE @RowsAffected INT;

-- カラムの存在を再確認
IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = ''CpInventoryMaster'' 
    AND COLUMN_NAME = ''DailyPurchaseDiscountAmount''
)
BEGIN
    -- 移行対象データの確認
    SELECT @TargetCount = COUNT(*)
    FROM CpInventoryMaster
    WHERE JobDate >= ''2025-06-01'' 
      AND DailyDiscountAmount < 0
      AND DailyPurchaseDiscountAmount = 0;
    
    IF @TargetCount > 0
    BEGIN
        PRINT CONCAT(''移行対象データ: '', @TargetCount, ''件'');
        PRINT ''移行処理を開始します...'';
        
        -- バッチ処理での移行
        WHILE @ProcessedCount < @TargetCount
        BEGIN
            UPDATE TOP (@BatchSize) CpInventoryMaster
            SET 
                DailyPurchaseDiscountAmount = ABS(DailyDiscountAmount),
                DailyDiscountAmount = 0
            WHERE JobDate >= ''2025-06-01'' 
              AND DailyDiscountAmount < 0
              AND DailyPurchaseDiscountAmount = 0;
            
            SET @RowsAffected = @@ROWCOUNT;
            SET @ProcessedCount = @ProcessedCount + @RowsAffected;
            
            -- 進捗表示
            IF @ProcessedCount % 5000 = 0 OR @ProcessedCount >= @TargetCount
            BEGIN
                PRINT CONCAT(''移行済み: '', @ProcessedCount, ''/'', @TargetCount, ''件'');
            END
            
            -- 無限ループ防止
            IF @RowsAffected = 0 BREAK;
        END
        
        PRINT ''✅ データ移行が完了しました'';
    END
    ELSE
    BEGIN
        PRINT ''移行対象データがありません'';
    END
END
ELSE
BEGIN
    PRINT ''❌ DailyPurchaseDiscountAmount カラムが見つかりません'';
    PRINT ''スクリプトを再実行してください'';
END
';

EXEC sp_executesql @SQL;
GO

-- 3. データ整合性チェック（動的SQL）
PRINT '';
PRINT '3. データ整合性チェックを実行しています...';

DECLARE @CheckSQL NVARCHAR(MAX);

SET @CheckSQL = N'
-- カラムの存在を確認
IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = ''CpInventoryMaster'' 
    AND COLUMN_NAME = ''DailyPurchaseDiscountAmount''
)
BEGIN
    DECLARE @PurchaseDiscountCount INT;
    DECLARE @WalkingDiscountCount INT;
    DECLARE @AnomalyCount INT;
    
    -- 統計情報の取得
    SELECT @PurchaseDiscountCount = COUNT(*)
    FROM CpInventoryMaster
    WHERE DailyPurchaseDiscountAmount > 0;
    
    SELECT @WalkingDiscountCount = COUNT(*)
    FROM CpInventoryMaster
    WHERE DailyDiscountAmount > 0;
    
    SELECT @AnomalyCount = COUNT(*)
    FROM CpInventoryMaster
    WHERE DailyDiscountAmount < 0
       OR (DailyPurchaseDiscountAmount > 0 AND DailyDiscountAmount > 0);
    
    PRINT CONCAT(''仕入値引データ件数: '', @PurchaseDiscountCount);
    PRINT CONCAT(''歩引額データ件数: '', @WalkingDiscountCount);
    
    IF @AnomalyCount > 0
    BEGIN
        PRINT '''';
        PRINT CONCAT(''⚠️ 警告: 異常データが '', @AnomalyCount, ''件見つかりました'');
    END
    ELSE
    BEGIN
        PRINT ''✅ データ整合性チェック完了（異常なし）'';
    END
    
    -- サンプルデータの表示
    IF @PurchaseDiscountCount > 0
    BEGIN
        PRINT '''';
        PRINT ''=== サンプルデータ（上位5件） ==='';
        
        SELECT TOP 5
            ProductCode as 商品コード,
            JobDate as 処理日,
            DailyPurchaseDiscountAmount as 仕入値引,
            DailyDiscountAmount as 歩引額
        FROM CpInventoryMaster
        WHERE DailyPurchaseDiscountAmount > 0
        ORDER BY JobDate DESC, ProductCode;
    END
END
';

EXEC sp_executesql @CheckSQL;
GO

-- 4. インデックスの作成（動的SQL）
PRINT '';
PRINT '4. インデックスの作成を確認しています...';

DECLARE @IndexSQL NVARCHAR(MAX);

SET @IndexSQL = N'
IF EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = ''CpInventoryMaster'' 
    AND COLUMN_NAME = ''DailyPurchaseDiscountAmount''
)
AND NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID(''CpInventoryMaster'') 
    AND name = ''IX_CpInventoryMaster_DailyPurchaseDiscountAmount''
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CpInventoryMaster_DailyPurchaseDiscountAmount
    ON CpInventoryMaster (JobDate, DailyPurchaseDiscountAmount)
    WHERE DailyPurchaseDiscountAmount != 0;
    
    PRINT ''✅ インデックスが作成されました'';
END
ELSE
BEGIN
    IF EXISTS (SELECT * FROM sys.indexes WHERE name = ''IX_CpInventoryMaster_DailyPurchaseDiscountAmount'')
        PRINT ''インデックスは既に存在します'';
    ELSE
        PRINT ''カラムが存在しないため、インデックスは作成されませんでした'';
END
';

EXEC sp_executesql @IndexSQL;
GO

-- 5. マイグレーション履歴への記録（動的SQL使用）
PRINT '';
PRINT '5. マイグレーション履歴を記録しています...';

DECLARE @HistorySQL NVARCHAR(MAX);

-- Descriptionカラムの存在を確認してから記録
SET @HistorySQL = N'
IF NOT EXISTS (SELECT * FROM MigrationHistory WHERE MigrationId = ''040_AddPurchaseDiscountColumn'')
BEGIN
    IF EXISTS (
        SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = ''MigrationHistory'' 
        AND COLUMN_NAME = ''Description''
    )
    BEGIN
        INSERT INTO MigrationHistory (MigrationId, AppliedAt, [Description])
        VALUES (''040_AddPurchaseDiscountColumn'', GETDATE(), ''仕入値引専用カラム(DailyPurchaseDiscountAmount)の追加とデータ移行'');
        
        PRINT ''✅ マイグレーション履歴に記録されました'';
    END
    ELSE
    BEGIN
        -- Descriptionカラムがない場合
        INSERT INTO MigrationHistory (MigrationId, AppliedAt)
        VALUES (''040_AddPurchaseDiscountColumn'', GETDATE());
        
        PRINT ''✅ マイグレーション履歴に記録されました（Description列なし）'';
    END
END
ELSE
BEGIN
    PRINT ''マイグレーション履歴は既に記録されています'';
END
';

EXEC sp_executesql @HistorySQL;

PRINT '';
PRINT '=== 仕入値引専用カラム追加処理完了 ===';
GO