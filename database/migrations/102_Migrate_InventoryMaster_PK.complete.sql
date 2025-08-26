-- =============================================
-- InventoryMaster 主キー変更マイグレーションスクリプト（修正版）
-- 作成日: 2025-07-20
-- 修正日: 2025-07-30
-- 目的: 主キーを6項目から5項目に変更（JobDateを除外）
-- 修正内容: 存在しないカラムのエラーハンドリング
-- =============================================

USE InventoryManagementDB;
GO

-- 既に実行済みかチェック
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND CONSTRAINT_NAME = 'PK_InventoryMaster'
    AND COLUMN_NAME NOT IN ('ProductCode', 'GradeCode', 'ClassCode', 'ShippingMarkCode', 'ManualShippingMark')
)
BEGIN
    PRINT '102_Migrate_InventoryMaster_PK.sql は既に実行済みです。スキップします。';
    RETURN;
END

-- 5項目の主キーが既に存在するかチェック
DECLARE @PKColumnCount INT;
SELECT @PKColumnCount = COUNT(*)
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
WHERE TABLE_NAME = 'InventoryMaster' 
AND CONSTRAINT_NAME = 'PK_InventoryMaster';

IF @PKColumnCount = 5
BEGIN
    PRINT '主キーは既に5項目に変更済みです。スキップします。';
    RETURN;
END

PRINT '========== InventoryMaster 主キー変更開始 ==========';
PRINT '';
PRINT '【警告】このスクリプトは既存データを変更します。';
PRINT '実行前に必ずバックアップ（101_Create_InventoryMaster_Backup.sql）が完了していることを確認してください。';
PRINT '';

-- トランザクション開始
BEGIN TRANSACTION;

BEGIN TRY
    -- 1. 一時テーブルの作成（最新JobDateのデータのみを保持）
    PRINT '1. 一時テーブルを作成します...';
    
    -- 現在のテーブル構造を確認して動的に一時テーブルを作成
    DECLARE @CreateTempTableSQL NVARCHAR(MAX) = '
    CREATE TABLE #TempInventoryMaster (
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ManualShippingMark NVARCHAR(50) NOT NULL,
        ProductName NVARCHAR(100) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        StandardPrice DECIMAL(18,4) NOT NULL,
        ProductCategory1 NVARCHAR(10) NOT NULL,
        ProductCategory2 NVARCHAR(10) NOT NULL,
        JobDate DATE NOT NULL,
        CreatedDate DATETIME2 NOT NULL,
        UpdatedDate DATETIME2 NOT NULL,
        CurrentStock DECIMAL(18,4) NOT NULL,
        CurrentStockAmount DECIMAL(18,4) NOT NULL,
        DailyStock DECIMAL(18,4) NOT NULL,
        DailyStockAmount DECIMAL(18,4) NOT NULL,
        DailyFlag CHAR(1) NOT NULL,
        DataSetId NVARCHAR(50) NOT NULL';

    -- 粗利計算関連カラムの確認と追加
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'DailyGrossProfit')
    BEGIN
        SET @CreateTempTableSQL = @CreateTempTableSQL + ',
        DailyGrossProfit DECIMAL(18,4) NOT NULL,
        DailyAdjustmentAmount DECIMAL(18,4) NOT NULL,
        DailyProcessingCost DECIMAL(18,4) NOT NULL,
        FinalGrossProfit DECIMAL(18,4) NOT NULL';
    END

    -- 前月繰越関連カラムの確認と追加
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'PreviousMonthQuantity')
    BEGIN
        SET @CreateTempTableSQL = @CreateTempTableSQL + ',
        PreviousMonthQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        PreviousMonthAmount DECIMAL(18,4) NOT NULL DEFAULT 0';
    END

    SET @CreateTempTableSQL = @CreateTempTableSQL + ');';

    -- 一時テーブルを作成
    EXEC sp_executesql @CreateTempTableSQL;

    -- 2. 最新JobDateのデータを一時テーブルに移行
    PRINT '2. 最新JobDateのデータを抽出します...';
    
    -- 動的SQLでデータを挿入
    DECLARE @InsertSQL NVARCHAR(MAX) = '
    INSERT INTO #TempInventoryMaster
    SELECT 
        im.ProductCode,
        im.GradeCode,
        im.ClassCode,
        im.ShippingMarkCode,
        im.ManualShippingMark,
        im.ProductName,
        im.Unit,
        im.StandardPrice,
        im.ProductCategory1,
        im.ProductCategory2,
        im.JobDate,
        im.CreatedDate,
        GETDATE() as UpdatedDate,
        im.CurrentStock,
        im.CurrentStockAmount,
        im.DailyStock,
        im.DailyStockAmount,
        im.DailyFlag,
        im.DataSetId';

    -- カラムが存在する場合のみ追加
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'DailyGrossProfit')
    BEGIN
        SET @InsertSQL = @InsertSQL + ',
        im.DailyGrossProfit,
        im.DailyAdjustmentAmount,
        im.DailyProcessingCost,
        im.FinalGrossProfit';
    END

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'PreviousMonthQuantity')
    BEGIN
        SET @InsertSQL = @InsertSQL + ',
        im.PreviousMonthQuantity,
        im.PreviousMonthAmount';
    END

    SET @InsertSQL = @InsertSQL + '
    FROM InventoryMaster im
    INNER JOIN (
        SELECT 
            ProductCode,
            GradeCode,
            ClassCode,
            ShippingMarkCode,
            ManualShippingMark,
            MAX(JobDate) as LatestJobDate
        FROM InventoryMaster
        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
    ) latest
    ON im.ProductCode = latest.ProductCode
        AND im.GradeCode = latest.GradeCode
        AND im.ClassCode = latest.ClassCode
        AND im.ShippingMarkCode = latest.ShippingMarkCode
        AND im.ManualShippingMark = latest.ManualShippingMark
        AND im.JobDate = latest.LatestJobDate;';

    -- 動的SQLを実行
    EXEC sp_executesql @InsertSQL;

    DECLARE @TempCount INT;
    SELECT @TempCount = COUNT(*) FROM #TempInventoryMaster;
    PRINT '   抽出されたレコード数: ' + CAST(@TempCount AS NVARCHAR(20));

    -- 3. 既存の主キー制約を削除
    PRINT '3. 既存の主キー制約を削除します...';
    IF EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_InventoryMaster' AND parent_object_id = OBJECT_ID('InventoryMaster'))
    BEGIN
        ALTER TABLE InventoryMaster DROP CONSTRAINT PK_InventoryMaster;
        PRINT '   主キー制約を削除しました';
    END
    ELSE
    BEGIN
        PRINT '   主キー制約は既に削除されています';
    END

    -- 4. 既存データをすべて削除
    PRINT '4. 既存データを削除します...';
    TRUNCATE TABLE InventoryMaster;

    -- 5. JobDateカラムを主キーから除外した新しい主キー制約を作成
    PRINT '5. 新しい主キー制約（5項目）を作成します...';
    ALTER TABLE InventoryMaster 
    ADD CONSTRAINT PK_InventoryMaster PRIMARY KEY CLUSTERED (
        ProductCode, 
        GradeCode, 
        ClassCode, 
        ShippingMarkCode, 
        ManualShippingMark
    );

    -- 6. JobDateに非クラスター化インデックスを作成（検索パフォーマンス用）
    PRINT '6. JobDateに非クラスター化インデックスを作成します...';
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_JobDate' AND object_id = OBJECT_ID('InventoryMaster'))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_InventoryMaster_JobDate 
        ON InventoryMaster (JobDate);
        PRINT '   IX_InventoryMaster_JobDateインデックスを作成しました';
    END
    ELSE
    BEGIN
        PRINT '   IX_InventoryMaster_JobDateインデックスは既に存在します';
    END

    -- 7. DataSetIdにもインデックスを作成
    PRINT '7. DataSetIdにインデックスを作成します...';
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_DataSetId' AND object_id = OBJECT_ID('InventoryMaster'))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_InventoryMaster_DataSetId 
        ON InventoryMaster (DataSetId);
        PRINT '   IX_InventoryMaster_DataSetIdインデックスを作成しました';
    END
    ELSE
    BEGIN
        PRINT '   IX_InventoryMaster_DataSetIdインデックスは既に存在します';
    END

    -- 8. 一時テーブルからデータを戻す
    PRINT '8. データを新しい構造に移行します...';
    
    -- 動的SQLでデータを戻す
    DECLARE @InsertBackSQL NVARCHAR(MAX) = '
    INSERT INTO InventoryMaster
    SELECT * FROM #TempInventoryMaster;';
    
    EXEC sp_executesql @InsertBackSQL;

    DECLARE @FinalCount INT;
    SELECT @FinalCount = COUNT(*) FROM InventoryMaster;
    PRINT '   移行されたレコード数: ' + CAST(@FinalCount AS NVARCHAR(20));

    -- 9. 一時テーブルを削除
    DROP TABLE #TempInventoryMaster;

    -- 10. 結果の確認
    PRINT '';
    PRINT '========== 移行結果 ==========';
    
    -- バックアップテーブルが存在する場合のみ比較
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InventoryMaster_Backup_20250720')
    BEGIN
        DECLARE @OriginalCount INT;
        SELECT @OriginalCount = COUNT(*) FROM InventoryMaster_Backup_20250720;
        
        PRINT 'バックアップテーブルのレコード数: ' + CAST(@OriginalCount AS NVARCHAR(20));
        PRINT '新しいテーブルのレコード数: ' + CAST(@FinalCount AS NVARCHAR(20));
        
        IF @OriginalCount > 0
        BEGIN
            PRINT '削減されたレコード数: ' + CAST(@OriginalCount - @FinalCount AS NVARCHAR(20));
            PRINT '削減率: ' + CAST(CAST((@OriginalCount - @FinalCount) AS FLOAT) / @OriginalCount * 100 AS NVARCHAR(10)) + '%';
        END
    END
    ELSE
    BEGIN
        PRINT '新しいテーブルのレコード数: ' + CAST(@FinalCount AS NVARCHAR(20));
        PRINT '注意: バックアップテーブルが存在しないため、比較できません';
    END

    -- 現在のテーブル構造を表示
    PRINT '';
    PRINT '現在のInventoryMasterテーブル構造:';
    SELECT 
        c.COLUMN_NAME,
        c.DATA_TYPE,
        c.CHARACTER_MAXIMUM_LENGTH,
        c.IS_NULLABLE,
        CASE 
            WHEN kcu.COLUMN_NAME IS NOT NULL THEN 'PRIMARY KEY' 
            ELSE '' 
        END as KEY_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS c
    LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
        ON c.TABLE_NAME = kcu.TABLE_NAME 
        AND c.COLUMN_NAME = kcu.COLUMN_NAME
        AND kcu.CONSTRAINT_NAME = 'PK_InventoryMaster'
    WHERE c.TABLE_NAME = 'InventoryMaster'
    ORDER BY c.ORDINAL_POSITION;

    -- コミット
    COMMIT TRANSACTION;
    
    PRINT '';
    PRINT '✅ 主キー変更が正常に完了しました';
    PRINT '';
    PRINT '【次のステップ】';
    PRINT '1. アプリケーションコードの修正';
    PRINT '   - InventoryMasterOptimizationService.csの修正';
    PRINT '   - sp_MergeInventoryMasterSnapshotの作成';
    PRINT '2. アプリケーションの動作確認';
    PRINT '3. 必要に応じて履歴管理機能の実装';

END TRY
BEGIN CATCH
    -- エラー時はロールバック
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    PRINT '';
    PRINT '❌ エラーが発生しました';
    PRINT 'エラーメッセージ: ' + ERROR_MESSAGE();
    PRINT 'エラー行: ' + CAST(ERROR_LINE() AS NVARCHAR(20));
    
    -- エラーを再スロー
    THROW;
END CATCH
GO