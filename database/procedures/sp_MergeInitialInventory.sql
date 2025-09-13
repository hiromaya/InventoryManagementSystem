-- =============================================
-- 初期在庫マージストアドプロシージャ
-- 作成日: 2025-07-13
-- 説明: ステージングテーブルから在庫マスタへのマージ処理
-- =============================================

USE InventoryManagementDB;
GO

-- 既存のストアドプロシージャが存在する場合は削除
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_MergeInitialInventory')
BEGIN
    DROP PROCEDURE sp_MergeInitialInventory;
END
GO

CREATE PROCEDURE sp_MergeInitialInventory
    @ProcessId NVARCHAR(50),
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @InsertCount INT = 0;
    DECLARE @UpdateCount INT = 0;
    DECLARE @ErrorCount INT = 0;
    DECLARE @ErrorMessage NVARCHAR(4000);
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- 1. マスタデータとの結合準備
        WITH StagingData AS (
            SELECT 
                s.*,
                ISNULL(pm.ProductName, N'商品' + s.ProductCode) as ProductName,
                ISNULL(pm.UnitCode, N'PCS') as Unit,
                ISNULL(pm.ProductCategory1, N'') as ProductCategory1,
                ISNULL(pm.ProductCategory2, N'') as ProductCategory2
            FROM InitialInventory_Staging s
            LEFT JOIN ProductMaster pm ON s.ProductCode = pm.ProductCode
            WHERE s.ProcessId = @ProcessId
                AND s.ProcessStatus = 'PENDING'
        )
        
        -- 2. 在庫マスタへのMERGE処理
        MERGE InventoryMaster AS target
        USING (
            SELECT 
                ProductCode = REPLICATE('0', 5 - LEN(ProductCode)) + ProductCode,
                GradeCode = REPLICATE('0', 3 - LEN(GradeCode)) + GradeCode,
                ClassCode = REPLICATE('0', 3 - LEN(ClassCode)) + ClassCode,
                ShippingMarkCode = REPLICATE('0', 4 - LEN(ShippingMarkCode)) + ShippingMarkCode,
                ManualShippingMark = LEFT(RTRIM(COALESCE(ManualShippingMark, '')) + REPLICATE(' ', 8), 8),
                ProductName,
                Unit,
                ProductCategory1,
                ProductCategory2,
                PreviousStockQuantity,
                PreviousStockAmount,
                CurrentStockQuantity,
                StandardPrice,
                CurrentStockAmount,
                AveragePrice,
                StagingId
            FROM StagingData
        ) AS source
        ON (
            target.ProductCode = source.ProductCode
            AND target.GradeCode = source.GradeCode
            AND target.ClassCode = source.ClassCode
            AND target.ShippingMarkCode = source.ShippingMarkCode
            AND target.ManualShippingMark = source.ManualShippingMark
            AND target.JobDate = @JobDate
        )
        
        -- 既存レコードが存在する場合（通常は初期在庫では発生しないが念のため）
        WHEN MATCHED THEN
            UPDATE SET
                CarryoverQuantity = source.PreviousStockQuantity,
                CarryoverAmount = source.PreviousStockAmount,
                CarryoverUnitPrice = source.AveragePrice,
                CurrentStock = source.CurrentStockQuantity,
                CurrentStockAmount = source.CurrentStockAmount,
                StandardPrice = source.StandardPrice,
                -- 初期在庫スナップショット日を最終入荷日に設定（ファイル名由来の@JobDate）
                LastReceiptDate = @JobDate,
                UpdatedDate = GETDATE(),
                DataSetId = @ProcessId,
                ImportType = 'INIT',
                IsActive = 1
        
        -- 新規レコード作成
        WHEN NOT MATCHED THEN
            INSERT (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate, CreatedBy,
                CarryoverQuantity, CarryoverAmount, CarryoverUnitPrice,
                CurrentStock, CurrentStockAmount,
                DailyStock, DailyStockAmount,
                DailyFlag, DataSetId, ImportType, Origin, IsActive,
                LastReceiptDate
            )
            VALUES (
                source.ProductCode, source.GradeCode, source.ClassCode,
                source.ShippingMarkCode, source.ManualShippingMark,
                source.ProductName, source.Unit, source.StandardPrice,
                source.ProductCategory1, source.ProductCategory2,
                @JobDate, GETDATE(), GETDATE(), 'SYSTEM',
                source.PreviousStockQuantity, source.PreviousStockAmount, source.AveragePrice,
                source.CurrentStockQuantity, source.CurrentStockAmount,
                0, 0,  -- 初期在庫なのでDailyStockは0
                '9',   -- DailyFlag
                @ProcessId, 'INIT', 'INITIAL', 1,
                @JobDate  -- 最終入荷日を初期在庫日で設定
            );
        
        SET @InsertCount = @@ROWCOUNT;
        
        -- 3. 処理済みフラグの更新
        UPDATE InitialInventory_Staging
        SET ProcessStatus = 'PROCESSED'
        WHERE ProcessId = @ProcessId
            AND ProcessStatus = 'PENDING';
        
        -- 4. 重複等でマージされなかったレコードをエラーとして記録
        INSERT INTO InitialInventory_ErrorLog (
            ProcessId, StagingId, ProductCode, GradeCode, ClassCode,
            ShippingMarkCode, ManualShippingMark, ErrorType, ErrorMessage
        )
        SELECT 
            @ProcessId,
            s.StagingId,
            s.ProductCode,
            s.GradeCode,
            s.ClassCode,
            s.ShippingMarkCode,
            s.ManualShippingMark,
            'DUPLICATE',
            '既存の在庫マスタレコードと重複しています'
        FROM InitialInventory_Staging s
        WHERE s.ProcessId = @ProcessId
            AND s.ProcessStatus = 'PENDING'
            AND EXISTS (
                SELECT 1 
                FROM InventoryMaster im
                WHERE im.ProductCode = REPLICATE('0', 5 - LEN(s.ProductCode)) + s.ProductCode
                    AND im.GradeCode = REPLICATE('0', 3 - LEN(s.GradeCode)) + s.GradeCode
                    AND im.ClassCode = REPLICATE('0', 3 - LEN(s.ClassCode)) + s.ClassCode
                    AND im.ShippingMarkCode = REPLICATE('0', 4 - LEN(s.ShippingMarkCode)) + s.ShippingMarkCode
                    AND im.ManualShippingMark = LEFT(RTRIM(COALESCE(s.ManualShippingMark, '')) + REPLICATE(' ', 8), 8)
                    AND im.JobDate = @JobDate
            );
        
        SET @ErrorCount = @@ROWCOUNT;
        
        -- エラーレコードのステータス更新
        UPDATE s
        SET s.ProcessStatus = 'ERROR',
            s.ErrorMessage = 'DUPLICATE: 既存レコードと重複'
        FROM InitialInventory_Staging s
        WHERE s.ProcessId = @ProcessId
            AND s.ProcessStatus = 'PENDING';
        
        COMMIT TRANSACTION;
        
        -- 結果を返す
        SELECT 
            @InsertCount AS InsertedCount,
            @UpdateCount AS UpdatedCount,
            @ErrorCount AS ErrorCount;
        
        PRINT N'初期在庫マージ完了: 新規=' + CAST(@InsertCount AS NVARCHAR(10)) + 
              N'件, 更新=' + CAST(@UpdateCount AS NVARCHAR(10)) + 
              N'件, エラー=' + CAST(@ErrorCount AS NVARCHAR(10)) + N'件';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END
        
        SET @ErrorMessage = ERROR_MESSAGE();
        
        -- エラーログに記録
        INSERT INTO InitialInventory_ErrorLog (
            ProcessId, ErrorType, ErrorMessage
        )
        VALUES (
            @ProcessId,
            'SYSTEM_ERROR',
            @ErrorMessage
        );
        
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END
GO

-- 権限設定
GRANT EXECUTE ON sp_MergeInitialInventory TO [public];
GO

PRINT N'ストアドプロシージャ sp_MergeInitialInventory を作成しました。';
