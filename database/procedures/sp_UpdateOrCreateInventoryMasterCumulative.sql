-- =============================================
-- 在庫マスタ累積更新ストアドプロシージャ（日付概念なし版）
-- 作成日: 2025-07-10
-- 説明: 伝票データから在庫マスタを累積更新（5項目キーのみで管理）
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
        -- Step 1: 当日の伝票に存在する商品のDailyFlagのみ更新（JobDateは更新しない）
        UPDATE im
        SET im.DailyFlag = '0',
            im.UpdatedDate = GETDATE()
        FROM InventoryMaster im
        WHERE EXISTS (
            SELECT 1 FROM (
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                FROM SalesVouchers WHERE JobDate = @JobDate
                UNION
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                FROM PurchaseVouchers WHERE JobDate = @JobDate
                UNION
                SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                FROM InventoryAdjustments WHERE JobDate = @JobDate
            ) v
            WHERE v.ProductCode = im.ProductCode
                AND v.GradeCode = im.GradeCode
                AND v.ClassCode = im.ClassCode
                AND v.ShippingMarkCode = im.ShippingMarkCode
                AND v.ManualShippingMark = im.ManualShippingMark
        );
        
        SET @UpdatedCount = @@ROWCOUNT;
        
        -- Step 2: 新規商品のみINSERT（既存の5項目キーと重複しないもののみ）
        INSERT INTO InventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
            ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
            JobDate, CreatedDate, UpdatedDate,
            CarryoverQuantity, CarryoverAmount, CarryoverUnitPrice,
            CurrentStock, CurrentStockAmount,
            DailyStock, DailyStockAmount, DailyFlag
        )
        SELECT DISTINCT
            v.ProductCode, v.GradeCode, v.ClassCode, v.ShippingMarkCode, v.ManualShippingMark,
            ISNULL(v.ProductName, ISNULL(pm.ProductName, '')), -- 伝票の商品名を優先、フォールバックとして商品マスタ
            ISNULL(u.UnitName, 'PCS'),
            ISNULL(pm.StandardPrice, 0),
            ISNULL(pm.ProductCategory1, ''), 
            ISNULL(pm.ProductCategory2, ''),
            @JobDate, GETDATE(), GETDATE(),
            0, 0, 0,  -- CarryoverQuantity, CarryoverAmount, CarryoverUnitPrice
            0, 0, 0, 0, '0'
        FROM (
            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, ProductName
            FROM SalesVouchers WHERE JobDate = @JobDate
            UNION
            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, ProductName
            FROM PurchaseVouchers WHERE JobDate = @JobDate
            UNION
            SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark, ProductName
            FROM InventoryAdjustments WHERE JobDate = @JobDate
        ) v
        LEFT JOIN ProductMaster pm ON v.ProductCode = pm.ProductCode
        LEFT JOIN UnitMaster u ON pm.UnitCode = u.UnitCode
        WHERE NOT EXISTS (
            -- 5項目キーでの存在チェック（日付は無視）
            SELECT 1 FROM InventoryMaster im
            WHERE im.ProductCode = v.ProductCode
                AND im.GradeCode = v.GradeCode
                AND im.ClassCode = v.ClassCode
                AND im.ShippingMarkCode = v.ShippingMarkCode
                AND im.ManualShippingMark = v.ManualShippingMark
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
