USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateCpInventoryFromCarryover')
BEGIN
    DROP PROCEDURE sp_CreateCpInventoryFromCarryover;
END
GO

CREATE PROCEDURE sp_CreateCpInventoryFromCarryover
    @JobDate DATE,  -- 設定用（検索条件ではない）
    @DepartmentCode NVARCHAR(10) = 'DeptA'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CreatedCount INT = 0;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- 1. CP在庫マスタを全削除（仮テーブル設計）
        TRUNCATE TABLE CpInventoryMaster;

        -- 2. アクティブな移行用在庫マスタから全件挿入（JobDateによる絞り込みなし）
        INSERT INTO CpInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            ShippingMarkName,
            ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
            GradeName, ClassName,
            JobDate, CreatedDate, UpdatedDate,
            PreviousDayStock, PreviousDayStockAmount, PreviousDayUnitPrice,
            DailyStock, DailyStockAmount, DailyUnitPrice,
            AveragePrice, DailyFlag,
            DailySalesQuantity, DailySalesAmount,
            DailySalesReturnQuantity, DailySalesReturnAmount,
            DailyPurchaseQuantity, DailyPurchaseAmount,
            DailyPurchaseReturnQuantity, DailyPurchaseReturnAmount,
            DailyInventoryAdjustmentQuantity, DailyInventoryAdjustmentAmount,
            DailyProcessingQuantity, DailyProcessingAmount,
            DailyTransferQuantity, DailyTransferAmount,
            DailyReceiptQuantity, DailyReceiptAmount,
            DailyShipmentQuantity, DailyShipmentAmount,
            DailyGrossProfit, DailyWalkingAmount, DailyIncentiveAmount, DailyDiscountAmount,
            MonthlySalesQuantity, MonthlySalesAmount,
            MonthlySalesReturnQuantity, MonthlySalesReturnAmount,
            MonthlyPurchaseQuantity, MonthlyPurchaseAmount,
            MonthlyPurchaseReturnQuantity, MonthlyPurchaseReturnAmount,
            MonthlyInventoryAdjustmentQuantity, MonthlyInventoryAdjustmentAmount,
            MonthlyProcessingQuantity, MonthlyProcessingAmount,
            MonthlyTransferQuantity, MonthlyTransferAmount,
            MonthlyGrossProfit, MonthlyWalkingAmount, MonthlyIncentiveAmount,
            DepartmentCode,
            LastReceiptDate
        )
        SELECT 
            icm.ProductCode,
            icm.GradeCode,
            icm.ClassCode,
            icm.ShippingMarkCode,
            icm.ManualShippingMark,
            ISNULL(sm.ShippingMarkName, '') AS ShippingMarkName,
            COALESCE(pm.ProductName, im.ProductName, ISNULL(icm.ProductName, '')) AS ProductName,
            COALESCE(icm.Unit, im.Unit, 'PCS') AS Unit,
            COALESCE(pm.StandardPrice, im.StandardPrice, 0) AS StandardPrice,
            COALESCE(icm.ProductCategory1, im.ProductCategory1, '') AS ProductCategory1,
            COALESCE(icm.ProductCategory2, im.ProductCategory2, '') AS ProductCategory2,
            ISNULL(gm.GradeName, '') AS GradeName,
            ISNULL(cm.ClassName, '') AS ClassName,
            @JobDate AS JobDate,
            GETDATE() AS CreatedDate,
            GETDATE() AS UpdatedDate,
            -- 前日在庫
            ISNULL(icm.CarryoverQuantity, 0) AS PreviousDayStock,
            ISNULL(icm.CarryoverAmount, 0) AS PreviousDayStockAmount,
            ISNULL(icm.CarryoverUnitPrice, 0) AS PreviousDayUnitPrice,
            -- 当日在庫（初期値は0、後続処理で更新）
            0 AS DailyStock,
            0 AS DailyStockAmount,
            0 AS DailyUnitPrice,
            -- 平均単価（同一で初期化）
            ISNULL(icm.CarryoverUnitPrice, 0) AS AveragePrice,
            '9' AS DailyFlag,
            -- 日計（ゼロ初期化）
            0,0, 0,0, 0,0, 0,0, 0,0,
            0,0, 0,0, 0,0, 0,0, 0,0,
            0,0,
            -- 月計（ゼロ初期化）
            0,0, 0,0, 0,0, 0,0, 0,0,
            0,0, 0,0, 0,0,
            -- 部門（設定値）
            @DepartmentCode AS DepartmentCode,
            icm.LastReceiptDate
        FROM InventoryCarryoverMaster icm
        LEFT JOIN GradeMaster gm ON gm.GradeCode = icm.GradeCode
        LEFT JOIN ClassMaster cm ON cm.ClassCode = icm.ClassCode
        LEFT JOIN ShippingMarkMaster sm ON sm.ShippingMarkCode = icm.ShippingMarkCode
        LEFT JOIN ProductMaster pm ON pm.ProductCode = icm.ProductCode
        OUTER APPLY (
            SELECT TOP 1 im.*
            FROM InventoryMaster im
            WHERE im.ProductCode = icm.ProductCode
              AND im.GradeCode = icm.GradeCode
              AND im.ClassCode = icm.ClassCode
              AND im.ShippingMarkCode = icm.ShippingMarkCode
              AND im.ManualShippingMark = icm.ManualShippingMark
            ORDER BY im.JobDate DESC
        ) im
        WHERE icm.IsActive = 1;

        SET @CreatedCount = @@ROWCOUNT;

        COMMIT TRANSACTION;

        SELECT @CreatedCount AS CreatedCount;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @Err NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@Err, 16, 1);
    END CATCH
END
GO
