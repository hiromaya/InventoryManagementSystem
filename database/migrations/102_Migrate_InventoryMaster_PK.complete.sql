-- =============================================
-- InventoryMaster 主キー変更マイグレーションスクリプト
-- 作成日: 2025-07-20
-- 目的: 主キーを6項目から5項目に変更（JobDateを除外）
-- =============================================

USE InventoryManagementDB;
GO

-- 既に実行済みかチェック
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
    WHERE TABLE_NAME = 'InventoryMaster' 
    AND CONSTRAINT_NAME = 'PK_InventoryMaster'
    AND COLUMN_NAME NOT IN ('ProductCode', 'GradeCode', 'ClassCode', 'ShippingMarkCode', 'ShippingMarkName')
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
    
    CREATE TABLE #TempInventoryMaster (
        ProductCode NVARCHAR(15) NOT NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ShippingMarkName NVARCHAR(50) NOT NULL,
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
        DailyGrossProfit DECIMAL(18,4) NOT NULL,
        DailyAdjustmentAmount DECIMAL(18,4) NOT NULL,
        DailyProcessingCost DECIMAL(18,4) NOT NULL,
        FinalGrossProfit DECIMAL(18,4) NOT NULL,
        DataSetId NVARCHAR(50) NOT NULL,
        PreviousMonthQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
        PreviousMonthAmount DECIMAL(18,4) NOT NULL DEFAULT 0
    );

    -- 2. 最新JobDateのデータを一時テーブルに移行
    PRINT '2. 最新JobDateのデータを抽出します...';
    
    INSERT INTO #TempInventoryMaster
    SELECT 
        im.ProductCode,
        im.GradeCode,
        im.ClassCode,
        im.ShippingMarkCode,
        im.ShippingMarkName,
        im.ProductName,
        im.Unit,
        im.StandardPrice,
        im.ProductCategory1,
        im.ProductCategory2,
        im.JobDate,
        im.CreatedDate,
        GETDATE() as UpdatedDate,  -- 更新日時を現在時刻に設定
        im.CurrentStock,
        im.CurrentStockAmount,
        im.DailyStock,
        im.DailyStockAmount,
        im.DailyFlag,
        im.DailyGrossProfit,
        im.DailyAdjustmentAmount,
        im.DailyProcessingCost,
        im.FinalGrossProfit,
        im.DataSetId,
        im.PreviousMonthQuantity,
        im.PreviousMonthAmount
    FROM InventoryMaster im
    INNER JOIN (
        SELECT 
            ProductCode,
            GradeCode,
            ClassCode,
            ShippingMarkCode,
            ShippingMarkName,
            MAX(JobDate) as LatestJobDate
        FROM InventoryMaster
        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    ) latest
    ON im.ProductCode = latest.ProductCode
        AND im.GradeCode = latest.GradeCode
        AND im.ClassCode = latest.ClassCode
        AND im.ShippingMarkCode = latest.ShippingMarkCode
        AND im.ShippingMarkName = latest.ShippingMarkName
        AND im.JobDate = latest.LatestJobDate;

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
        ShippingMarkName
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
    INSERT INTO InventoryMaster
    SELECT * FROM #TempInventoryMaster;

    DECLARE @FinalCount INT;
    SELECT @FinalCount = COUNT(*) FROM InventoryMaster;
    PRINT '   移行されたレコード数: ' + CAST(@FinalCount AS NVARCHAR(20));

    -- 9. 一時テーブルを削除
    DROP TABLE #TempInventoryMaster;

    -- 10. 結果の確認
    PRINT '';
    PRINT '========== 移行結果 ==========';
    
    -- レコード数の比較
    DECLARE @OriginalCount INT;
    SELECT @OriginalCount = COUNT(*) FROM InventoryMaster_Backup_20250720;
    
    PRINT 'バックアップテーブルのレコード数: ' + CAST(@OriginalCount AS NVARCHAR(20));
    PRINT '新しいテーブルのレコード数: ' + CAST(@FinalCount AS NVARCHAR(20));
    PRINT '削減されたレコード数: ' + CAST(@OriginalCount - @FinalCount AS NVARCHAR(20));
    PRINT '削減率: ' + CAST(CAST((@OriginalCount - @FinalCount) AS FLOAT) / @OriginalCount * 100 AS NVARCHAR(10)) + '%';

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