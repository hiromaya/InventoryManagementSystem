-- =============================================
-- 初期在庫 → 移行用在庫マスタ 取込MERGEストアド
-- 説明: InitialInventory_Staging から InventoryCarryoverMaster へ統合
-- 入力: @ProcessId = INITIAL_YYYYMMDD_HHmmss, @JobDate = スナップショット日
-- 出力: 影響件数はPRINTとSELECTで返す（簡易）
-- =============================================

USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_MergeInitialInventoryToCarryover')
BEGIN
    DROP PROCEDURE sp_MergeInitialInventoryToCarryover;
END
GO

CREATE PROCEDURE sp_MergeInitialInventoryToCarryover
    @ProcessId NVARCHAR(50),
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Inserted INT = 0, @Updated INT = 0;

    BEGIN TRANSACTION;
    BEGIN TRY
        ;WITH StagingData AS (
            SELECT 
                s.ProcessId,
                -- 5キー正規化
                ProductCode        = REPLICATE('0', 5 - LEN(s.ProductCode)) + s.ProductCode,
                GradeCode          = REPLICATE('0', 3 - LEN(s.GradeCode)) + s.GradeCode,
                ClassCode          = REPLICATE('0', 3 - LEN(s.ClassCode)) + s.ClassCode,
                ShippingMarkCode   = REPLICATE('0', 4 - LEN(s.ShippingMarkCode)) + s.ShippingMarkCode,
                ManualShippingMark = LEFT(RTRIM(COALESCE(s.ManualShippingMark, '')) + REPLICATE(' ', 8), 8),

                -- 商品属性はマスタから補完
                ISNULL(pm.ProductName, N'商品' + s.ProductCode) as ProductName,
                ISNULL(pm.UnitCode, N'PCS') as Unit,
                -- 初期在庫は担当者コード（PersonInChargeCode）を商品分類1として保存
                CONVERT(NVARCHAR(10), s.PersonInChargeCode) as ProductCategory1,
                ISNULL(pm.ProductCategory2, N'') as ProductCategory2,

                -- Carryover（優先順位: 当日>前日。数量が0の系は無視）
                CarryoverQuantity  = CASE 
                                        WHEN ISNULL(s.CurrentStockQuantity, 0) > 0 THEN s.CurrentStockQuantity
                                        WHEN ISNULL(s.PreviousStockQuantity, 0) > 0 THEN s.PreviousStockQuantity
                                        ELSE 0
                                     END,
                CarryoverAmount    = CASE 
                                        WHEN ISNULL(s.CurrentStockQuantity, 0) > 0 THEN s.CurrentStockAmount
                                        WHEN ISNULL(s.PreviousStockQuantity, 0) > 0 THEN s.PreviousStockAmount
                                        ELSE 0
                                     END,
                CarryoverUnitPrice = CASE 
                                        WHEN ISNULL(s.CurrentStockQuantity, 0) > 0 AND ISNULL(s.CurrentStockAmount, 0) > 0
                                            THEN s.CurrentStockAmount / NULLIF(s.CurrentStockQuantity, 0)
                                        WHEN ISNULL(s.PreviousStockQuantity, 0) > 0 AND ISNULL(s.PreviousStockAmount, 0) > 0
                                            THEN s.PreviousStockAmount / NULLIF(s.PreviousStockQuantity, 0)
                                        ELSE ISNULL(s.StandardPrice, 0)
                                     END
            FROM InitialInventory_Staging s
            LEFT JOIN ProductMaster pm ON pm.ProductCode = s.ProductCode
            WHERE s.ProcessId = @ProcessId
              AND s.ProcessStatus = 'PENDING'
        )
        MERGE InventoryCarryoverMaster AS target
        USING (
            SELECT * FROM StagingData
        ) AS source
        ON (
            target.ProductCode = source.ProductCode AND
            target.GradeCode = source.GradeCode AND
            target.ClassCode = source.ClassCode AND
            target.ShippingMarkCode = source.ShippingMarkCode AND
            target.ManualShippingMark = source.ManualShippingMark AND
            target.JobDate = @JobDate AND
            target.DataSetId = @ProcessId
        )
        WHEN MATCHED THEN
            UPDATE SET 
                target.ProductName = source.ProductName,
                target.Unit = source.Unit,
                target.ProductCategory1 = source.ProductCategory1,
                target.ProductCategory2 = source.ProductCategory2,
                target.CarryoverQuantity = source.CarryoverQuantity,
                target.CarryoverAmount = source.CarryoverAmount,
                target.CarryoverUnitPrice = source.CarryoverUnitPrice,
                target.LastReceiptDate = @JobDate,
                target.ImportType = 'INIT',
                target.Origin = 'INITIAL_IMPORT',
                target.UpdatedAt = GETDATE(),
                target.CreatedBy = 'import-initial-inventory'
        WHEN NOT MATCHED THEN
            INSERT (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                ProductName, Unit, ProductCategory1, ProductCategory2,
                CarryoverQuantity, CarryoverAmount, CarryoverUnitPrice,
                CurrentStockQuantity, CurrentStockAmount, CurrentStockUnitPrice,
                JobDate, DataSetId, ImportType, Origin, CreatedAt, UpdatedAt, CreatedBy,
                LastReceiptDate
            ) VALUES (
                source.ProductCode, source.GradeCode, source.ClassCode, source.ShippingMarkCode, source.ManualShippingMark,
                source.ProductName, source.Unit, source.ProductCategory1, source.ProductCategory2,
                source.CarryoverQuantity, source.CarryoverAmount, source.CarryoverUnitPrice,
                0, 0, 0,
                @JobDate, @ProcessId, 'INIT', 'INITIAL_IMPORT', GETDATE(), GETDATE(), 'import-initial-inventory',
                @JobDate
            );

        SET @Inserted = @Inserted + (CASE WHEN @@ROWCOUNT > 0 THEN @@ROWCOUNT ELSE 0 END);

        -- ステージングの処理状態を更新
        UPDATE InitialInventory_Staging
        SET ProcessStatus = 'PROCESSED'
        WHERE ProcessId = @ProcessId AND ProcessStatus = 'PENDING';

        COMMIT TRANSACTION;

        PRINT N'Carryoverへ取込完了: ProcessId=' + @ProcessId;
        SELECT @Inserted AS AffectedRows;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @Err NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@Err, 16, 1);
    END CATCH
END
GO

GRANT EXECUTE ON sp_MergeInitialInventoryToCarryover TO [public];
GO
