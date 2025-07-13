-- =============================================
-- 累積在庫管理用 MERGE ストアドプロシージャ
-- 作成日: 2025-07-10
-- 説明: 当日の伝票データから在庫マスタを累積更新
-- =============================================

USE InventoryManagementDB;
GO

-- 既存のストアドプロシージャが存在する場合は削除
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_MergeInventoryMasterCumulative')
BEGIN
    DROP PROCEDURE sp_MergeInventoryMasterCumulative;
END
GO

CREATE PROCEDURE sp_MergeInventoryMasterCumulative
    @JobDate DATE,
    @DataSetId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @InsertCount INT = 0;
    DECLARE @UpdateCount INT = 0;
    DECLARE @ErrorMessage NVARCHAR(4000);
    
    -- OUTPUT句の結果を格納するテーブル変数
    DECLARE @MergeOutput TABLE (
        ActionType NVARCHAR(10)
    );
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- ShippingMarkNameの空白を統一的に処理するためのCTE
        WITH CurrentDayTransactions AS (
            -- 当日の取引データを集計（ShippingMarkNameをトリミング）
            SELECT 
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName,  -- 8桁固定長に正規化
                SUM(SalesQty) as TotalSalesQty,
                SUM(PurchaseQty) as TotalPurchaseQty,
                SUM(AdjustmentQty) as TotalAdjustmentQty,
                SUM(SalesAmount) as TotalSalesAmount,
                SUM(PurchaseAmount) as TotalPurchaseAmount,
                SUM(AdjustmentAmount) as TotalAdjustmentAmount
            FROM (
                -- 売上データ
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    -Quantity as SalesQty,  -- 売上はマイナス
                    0 as PurchaseQty,
                    0 as AdjustmentQty,
                    -Amount as SalesAmount,
                    0 as PurchaseAmount,
                    0 as AdjustmentAmount
                FROM SalesVouchers 
                WHERE CAST(JobDate AS DATE) = CAST(@JobDate AS DATE)
                
                UNION ALL
                
                -- 仕入データ
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    0 as SalesQty,
                    Quantity as PurchaseQty,  -- 仕入はプラス
                    0 as AdjustmentQty,
                    0 as SalesAmount,
                    Amount as PurchaseAmount,
                    0 as AdjustmentAmount
                FROM PurchaseVouchers 
                WHERE CAST(JobDate AS DATE) = CAST(@JobDate AS DATE)
                
                UNION ALL
                
                -- 在庫調整データ
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                    0 as SalesQty,
                    0 as PurchaseQty,
                    Quantity as AdjustmentQty,
                    0 as SalesAmount,
                    0 as PurchaseAmount,
                    Amount as AdjustmentAmount
                FROM InventoryAdjustments 
                WHERE CAST(JobDate AS DATE) = CAST(@JobDate AS DATE)
            ) AS AllTransactions
            GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
        )
        
        -- MERGE文で在庫マスタを更新
        MERGE InventoryMaster AS target
        USING (
            SELECT 
                t.*,
                ISNULL(pm.ProductName, N'商' + t.ProductCode) as ProductName,
                ISNULL(u.UnitName, N'PCS') as UnitName,
                ISNULL(pm.StandardPrice, 0) as StandardPrice,
                ISNULL(pm.ProductCategory1, N'') as ProductCategory1,
                ISNULL(pm.ProductCategory2, N'') as ProductCategory2
            FROM CurrentDayTransactions t
            LEFT JOIN ProductMaster pm ON t.ProductCode = pm.ProductCode
            LEFT JOIN UnitMaster u ON pm.UnitCode = u.UnitCode
        ) AS source
        ON (
            target.ProductCode = source.ProductCode
            AND target.GradeCode = source.GradeCode
            AND target.ClassCode = source.ClassCode
            AND target.ShippingMarkCode = source.ShippingMarkCode
            AND LEFT(RTRIM(COALESCE(target.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = source.ShippingMarkName  -- 8桁固定長で比較
        )
        
        -- 既存レコード：在庫を累積更新
        WHEN MATCHED THEN
            UPDATE SET
                -- 在庫数量の累積更新
                CurrentStock = ISNULL(target.CurrentStock, 0) + source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
                CurrentStockAmount = ISNULL(target.CurrentStockAmount, 0) + source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
                
                -- 当日在庫の更新
                DailyStock = source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
                DailyStockAmount = source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
                
                -- メタデータの更新
                JobDate = @JobDate,  -- 最終更新日として使用
                UpdatedDate = GETDATE(),
                DataSetId = @DataSetId,
                DailyFlag = N'0'  -- データありフラグ
        
        -- 新規レコード：前月末在庫を考慮して作成
        WHEN NOT MATCHED THEN
            INSERT (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
                JobDate, CreatedDate, UpdatedDate,
                CurrentStock, CurrentStockAmount, 
                DailyStock, DailyStockAmount,
                DailyFlag, DataSetId,
                PreviousMonthQuantity, PreviousMonthAmount,
                DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit
            )
            VALUES (
                source.ProductCode, source.GradeCode, source.ClassCode, 
                source.ShippingMarkCode, source.ShippingMarkName,
                source.ProductName,
                source.UnitName,
                source.StandardPrice,
                source.ProductCategory1,
                source.ProductCategory2,
                @JobDate, GETDATE(), GETDATE(),
                -- 新規商品の在庫数量
                source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
                source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
                -- 当日在庫
                source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
                source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
                N'0',  -- データありフラグ
                @DataSetId,
                0, 0,  -- 前月末在庫は0（新規商品のため）
                0, 0, 0, 0  -- 粗利関連は0で初期化
            )
        OUTPUT $action INTO @MergeOutput(ActionType);
        
        -- 処理件数を計算
        SELECT 
            @InsertCount = COUNT(CASE WHEN ActionType = 'INSERT' THEN 1 END),
            @UpdateCount = COUNT(CASE WHEN ActionType = 'UPDATE' THEN 1 END)
        FROM @MergeOutput;
        
        COMMIT TRANSACTION;
        
        -- 結果を返す
        SELECT @InsertCount AS InsertedCount, @UpdateCount AS UpdatedCount;
        
        PRINT N'在庫マスタMERGE完了: 新規=' + CAST(@InsertCount AS NVARCHAR(10)) + N'件, 更新=' + CAST(@UpdateCount AS NVARCHAR(10)) + N'件';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END
        
        SET @ErrorMessage = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END
GO

-- 権限設定
GRANT EXECUTE ON sp_MergeInventoryMasterCumulative TO [public];
GO

PRINT N'ストアドプロシージャ sp_MergeInventoryMasterCumulative を作成しました。';