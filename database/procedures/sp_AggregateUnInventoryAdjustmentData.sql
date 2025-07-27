-- =============================================
-- UN在庫マスタ在庫調整データ集計ストアドプロシージャ
-- 作成日: 2025-07-27
-- 説明: 在庫調整入荷データ（数量>0）をUN在庫マスタに集計
-- =============================================
USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AggregateUnInventoryAdjustmentData')
BEGIN
    DROP PROCEDURE sp_AggregateUnInventoryAdjustmentData;
END
GO

CREATE PROCEDURE sp_AggregateUnInventoryAdjustmentData
    @DataSetId NVARCHAR(100),
    @JobDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UpdatedCount INT = 0;
    
    BEGIN TRY
        -- 在庫調整入荷データ（数量>0）をUN在庫マスタに集計
        -- 入荷調整は在庫増加要因のため、DailyStockに加算
        UPDATE un
        SET un.DailyStock = un.DailyStock + ISNULL(adjustment_summary.TotalQuantity, 0),
            un.UpdatedDate = GETDATE()
        FROM UnInventoryMaster un
        INNER JOIN (
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                SUM(Quantity) as TotalQuantity
            FROM InventoryAdjustments
            WHERE DataSetId = @DataSetId
            AND (@JobDate IS NULL OR JobDate = @JobDate)
            AND VoucherType = '71'            -- 在庫調整伝票
            AND DetailType = '1'              -- 明細種1のみ
            AND Quantity > 0                  -- 入荷調整（プラス数量）
            AND ProductCode != '00000'        -- 商品コード「00000」除外
            AND UnitCode NOT IN ('02', '05')  -- ギフト経費・加工費B除外
            GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
        ) adjustment_summary ON (
            un.ProductCode = adjustment_summary.ProductCode
            AND un.GradeCode = adjustment_summary.GradeCode
            AND un.ClassCode = adjustment_summary.ClassCode
            AND un.ShippingMarkCode = adjustment_summary.ShippingMarkCode
            AND un.ShippingMarkName = adjustment_summary.ShippingMarkName
        )
        WHERE un.DataSetId = @DataSetId;
        
        SET @UpdatedCount = @@ROWCOUNT;
        
        SELECT @UpdatedCount as UpdatedCount, 'UN在庫マスタ在庫調整集計完了' as Message;
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END
GO