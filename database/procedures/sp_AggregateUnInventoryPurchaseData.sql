-- =============================================
-- UN在庫マスタ仕入データ集計ストアドプロシージャ
-- 作成日: 2025-07-27
-- 説明: 通常仕入データ（数量>0）をUN在庫マスタに集計
-- =============================================
USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AggregateUnInventoryPurchaseData')
BEGIN
    DROP PROCEDURE sp_AggregateUnInventoryPurchaseData;
END
GO

CREATE PROCEDURE sp_AggregateUnInventoryPurchaseData
    @DataSetId NVARCHAR(100),
    @JobDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UpdatedCount INT = 0;
    
    BEGIN TRY
        -- 通常仕入データ（数量>0）をUN在庫マスタに集計
        -- 仕入は在庫増加要因のため、DailyStockに加算
        UPDATE un
        SET un.DailyStock = un.DailyStock + ISNULL(purchase_summary.TotalQuantity, 0),
            un.UpdatedDate = GETDATE()
        FROM UnInventoryMaster un
        INNER JOIN (
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                SUM(Quantity) as TotalQuantity
            FROM PurchaseVouchers
            WHERE DataSetId = @DataSetId
            AND (@JobDate IS NULL OR JobDate = @JobDate)
            AND VoucherType IN ('11', '12')   -- 掛仕入・現金仕入
            AND DetailType = '1'              -- 商品明細
            AND Quantity > 0                  -- 通常仕入（プラス数量）
            AND ProductCode != '00000'        -- 商品コード「00000」除外
            GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
        ) purchase_summary ON (
            un.ProductCode = purchase_summary.ProductCode
            AND un.GradeCode = purchase_summary.GradeCode
            AND un.ClassCode = purchase_summary.ClassCode
            AND un.ShippingMarkCode = purchase_summary.ShippingMarkCode
            AND un.ManualShippingMark = purchase_summary.ManualShippingMark
        )
        WHERE un.DataSetId = @DataSetId;
        
        SET @UpdatedCount = @@ROWCOUNT;
        
        SELECT @UpdatedCount as UpdatedCount, 'UN在庫マスタ仕入集計完了' as Message;
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END
GO