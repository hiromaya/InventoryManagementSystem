-- =============================================
-- CP在庫マスタ作成（InventoryMaster統合版）
-- 作成日: 2025-09-11
-- 説明: InventoryMaster（IsActive=1）からCP在庫マスタを再構築
-- =============================================

USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromInventoryMaster')
BEGIN
    DROP PROCEDURE sp_CreateCpInventoryFromInventoryMaster;
END
GO

CREATE PROCEDURE sp_CreateCpInventoryFromInventoryMaster
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- 初期化
        TRUNCATE TABLE dbo.CpInventoryMaster;

        -- InventoryMasterから前残を設定（IsActive=1）
        INSERT INTO dbo.CpInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            ProductName, Unit, ProductCategory1, ProductCategory2,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
            DailyStock, DailyStockAmount, DailyUnitPrice,
            JobDate, DailyFlag, CreatedDate, UpdatedDate
        )
        SELECT 
            im.ProductCode, im.GradeCode, im.ClassCode, im.ShippingMarkCode, im.ManualShippingMark,
            im.ProductName, im.Unit, im.ProductCategory1, im.ProductCategory2,
            ISNULL(im.CarryoverQuantity, 0), ISNULL(im.CarryoverAmount, 0), ISNULL(im.CarryoverUnitPrice, 0),
            ISNULL(im.CarryoverQuantity, 0), ISNULL(im.CarryoverAmount, 0), ISNULL(im.CarryoverUnitPrice, 0),
            @JobDate, '9', GETDATE(), GETDATE()
        FROM dbo.InventoryMaster im WITH (NOLOCK)
        WHERE im.IsActive = 1;

        DECLARE @CreatedCount INT = @@ROWCOUNT;

        COMMIT TRANSACTION;
        SELECT @CreatedCount AS CreatedCount;
        PRINT N'CP在庫マスタ作成完了（InventoryMasterベース）: ' + CAST(@CreatedCount AS NVARCHAR(20)) + N'件';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @Err nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@Err, 16, 1);
    END CATCH
END
GO

GRANT EXECUTE ON sp_CreateCpInventoryFromInventoryMaster TO [public];
GO

