-- =============================================
-- CP在庫 → 移行用在庫マスタ 取込MERGEストアド
-- 説明: 指定日のCpInventoryMaster（当日スナップショット）を InventoryCarryoverMaster に保存
-- 入力: @JobDate, @DataSetId（商品日報と同一IDで可）
-- =============================================

USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_MergeCarryoverFromCpInventory')
BEGIN
    DROP PROCEDURE sp_MergeCarryoverFromCpInventory;
END
GO

CREATE PROCEDURE sp_MergeCarryoverFromCpInventory
    @JobDate DATE,
    @DataSetId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        ;WITH SourceData AS (
            SELECT 
                -- 5キー
                cp.ProductCode,
                cp.GradeCode,
                cp.ClassCode,
                cp.ShippingMarkCode,
                -- ManualShippingMarkは固定8桁の前提（念のため右詰め空白）
                ManualShippingMark = LEFT(RTRIM(COALESCE(cp.ManualShippingMark, '')) + REPLICATE(' ', 8), 8),
                
                -- 属性
                ISNULL(cp.ProductName, N'') as ProductName,
                ISNULL(cp.Unit, N'PCS') as Unit,
                ISNULL(cp.ProductCategory1, N'') as ProductCategory1,
                ISNULL(cp.ProductCategory2, N'') as ProductCategory2,
                
                -- Carryover: 当日の在庫をスナップショット
                CarryoverQuantity  = ISNULL(cp.DailyStock, 0),
                CarryoverAmount    = ISNULL(cp.DailyStockAmount, 0),
                CarryoverUnitPrice = ISNULL(cp.DailyUnitPrice, 0),
                LastReceiptDate    = cp.LastReceiptDate
            FROM CpInventoryMaster cp
            WHERE cp.JobDate = @JobDate
        )
        MERGE InventoryCarryoverMaster AS target
        USING (SELECT * FROM SourceData) AS source
        ON (
            target.ProductCode = source.ProductCode AND
            target.GradeCode = source.GradeCode AND
            target.ClassCode = source.ClassCode AND
            target.ShippingMarkCode = source.ShippingMarkCode AND
            target.ManualShippingMark = source.ManualShippingMark AND
            target.JobDate = @JobDate AND
            target.DataSetId = @DataSetId
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
                target.LastReceiptDate = source.LastReceiptDate,
                target.ImportType = 'CARRYOVER',
                target.Origin = 'DAILY_CLOSE',
                target.UpdatedAt = GETDATE(),
                target.CreatedBy = 'daily-close'
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
                @JobDate, @DataSetId, 'CARRYOVER', 'DAILY_CLOSE', GETDATE(), GETDATE(), 'daily-close',
                source.LastReceiptDate
            );

        -- アクティブ制御（全体0→今回DataSetId=1）
        UPDATE InventoryCarryoverMaster SET IsActive = 0;
        UPDATE InventoryCarryoverMaster SET IsActive = 1 WHERE DataSetId = @DataSetId;

        PRINT N'Carryoverへ取込完了（CP→Carryover）: ' + CONVERT(NVARCHAR, @JobDate, 120) + N' / ' + @DataSetId;
    END TRY
    BEGIN CATCH
        DECLARE @Err NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@Err, 16, 1);
    END CATCH
END
GO

GRANT EXECUTE ON sp_MergeCarryoverFromCpInventory TO [public];
GO
