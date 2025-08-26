-- =============================================
-- UN在庫マスタ売上データ集計ストアドプロシージャ
-- 作成日: 2025-07-27
-- 説明: 売上返品データ（数量<0）をUN在庫マスタに集計
-- =============================================
USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AggregateUnInventorySalesData')
BEGIN
    DROP PROCEDURE sp_AggregateUnInventorySalesData;
END
GO

CREATE PROCEDURE sp_AggregateUnInventorySalesData
    @DataSetId NVARCHAR(100),
    @JobDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UpdatedCount INT = 0;
    
    BEGIN TRY
        -- 売上返品データ（数量<0）をUN在庫マスタに集計
        -- 返品は在庫増加要因のため、DailyStockに加算
        UPDATE un
        SET un.DailyStock = un.DailyStock + ISNULL(sales_summary.TotalQuantity, 0),
            un.UpdatedDate = GETDATE()
        FROM UnInventoryMaster un
        INNER JOIN (
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                SUM(ABS(Quantity)) as TotalQuantity  -- 返品（マイナス）の絶対値で加算
            FROM SalesVouchers
            WHERE DataSetId = @DataSetId
            AND (@JobDate IS NULL OR JobDate = @JobDate)
            AND VoucherType IN ('51', '52')   -- 掛売・現売
            AND DetailType = '2'              -- 返品明細
            AND Quantity < 0                  -- 返品（マイナス数量）
            AND ProductCode != '00000'        -- 商品コード「00000」除外
            GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
        ) sales_summary ON (
            un.ProductCode = sales_summary.ProductCode
            AND un.GradeCode = sales_summary.GradeCode
            AND un.ClassCode = sales_summary.ClassCode
            AND un.ShippingMarkCode = sales_summary.ShippingMarkCode
            AND un.ManualShippingMark = sales_summary.ManualShippingMark
        )
        WHERE un.DataSetId = @DataSetId;
        
        SET @UpdatedCount = @@ROWCOUNT;
        
        SELECT @UpdatedCount as UpdatedCount, 'UN在庫マスタ売上返品集計完了' as Message;
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END
GO