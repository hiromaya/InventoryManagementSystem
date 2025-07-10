-- =============================================
-- 在庫マスタ累積更新ストアドプロシージャ
-- 作成日: 2025-07-10
-- 説明: 伝票データから在庫マスタを累積更新（既存は更新、新規は追加）
-- =============================================
USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_UpdateOrCreateInventoryMasterCumulative')
BEGIN
    DROP PROCEDURE sp_UpdateOrCreateInventoryMasterCumulative;
END
GO

CREATE PROCEDURE sp_UpdateOrCreateInventoryMasterCumulative
    @JobDate DATE,
    @DatasetId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @InsertedCount INT = 0;
    DECLARE @UpdatedCount INT = 0;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Step 1: 当日の伝票に関連する商品の当日フラグを更新
        UPDATE im
        SET im.DailyFlag = '0',
            im.JobDate = @JobDate,
            im.UpdatedDate = GETDATE()
        FROM InventoryMaster im
        WHERE EXISTS (
            SELECT 1 FROM (
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                FROM SalesVouchers WHERE JobDate = @JobDate
                UNION
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                FROM PurchaseVouchers WHERE JobDate = @JobDate
                UNION
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
                FROM InventoryAdjustments WHERE JobDate = @JobDate
            ) v
            WHERE v.ProductCode = im.ProductCode
                AND v.GradeCode = im.GradeCode
                AND v.ClassCode = im.ClassCode
                AND v.ShippingMarkCode = im.ShippingMarkCode
                AND v.ShippingMarkName = im.ShippingMarkName
        );
        
        SET @UpdatedCount = @@ROWCOUNT;
        
        -- Step 2: 新規商品のみINSERT
        INSERT INTO InventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
            JobDate, CreatedDate, UpdatedDate,
            CurrentStock, CurrentStockAmount,
            DailyStock, DailyStockAmount, DailyFlag,
            PreviousMonthQuantity, PreviousMonthAmount
        )
        SELECT DISTINCT
            v.ProductCode, v.GradeCode, v.ClassCode, v.ShippingMarkCode, v.ShippingMarkName,
            ISNULL(pm.ProductName, ''), 
            ISNULL(u.UnitName, 'PCS'),  -- UnitMasterから単位名を取得
            ISNULL(pm.StandardPrice, 0),
            ISNULL(pm.ProductCategory1, ''), 
            ISNULL(pm.ProductCategory2, ''),
            @JobDate, GETDATE(), GETDATE(),
            0, 0, 0, 0, '0', 0, 0
        FROM (
            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            FROM SalesVouchers WHERE JobDate = @JobDate
            UNION
            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            FROM PurchaseVouchers WHERE JobDate = @JobDate
            UNION
            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
            FROM InventoryAdjustments WHERE JobDate = @JobDate
        ) v
        LEFT JOIN ProductMaster pm ON v.ProductCode = pm.ProductCode
        LEFT JOIN UnitMaster u ON pm.UnitCode = u.UnitCode  -- 正しい結合
        WHERE NOT EXISTS (
            SELECT 1 FROM InventoryMaster im
            WHERE im.ProductCode = v.ProductCode
                AND im.GradeCode = v.GradeCode
                AND im.ClassCode = v.ClassCode
                AND im.ShippingMarkCode = v.ShippingMarkCode
                AND im.ShippingMarkName = v.ShippingMarkName
        );
        
        SET @InsertedCount = @@ROWCOUNT;
        
        COMMIT TRANSACTION;
        
        SELECT @InsertedCount as InsertedCount, @UpdatedCount as UpdatedCount;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO