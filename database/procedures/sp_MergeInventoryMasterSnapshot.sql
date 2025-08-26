-- =============================================
-- スナップショット管理用 MERGE ストアドプロシージャ
-- 作成日: 2025-07-20
-- 説明: 5項目主キーでのスナップショット管理に対応
-- 変更内容:
--   - JobDate条件を削除（主キーから除外）
--   - 累積計算を削除（当日の在庫状態のみ管理）
-- =============================================

USE InventoryManagementDB;
GO

-- 既存のストアドプロシージャが存在する場合は削除
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_MergeInventoryMasterSnapshot')
BEGIN
    DROP PROCEDURE sp_MergeInventoryMasterSnapshot;
END
GO

CREATE PROCEDURE sp_MergeInventoryMasterSnapshot
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
        -- ManualShippingMarkの空白を統一的に処理するためのCTE
        WITH CurrentDayTransactions AS (
            -- 当日の取引データを集計（ManualShippingMarkをトリミング）
            SELECT 
                ProductCode,
                GradeCode,
                ClassCode,
                ShippingMarkCode,
                LEFT(RTRIM(COALESCE(ManualShippingMark, '')) + REPLICATE(' ', 8), 8) as ManualShippingMark,  -- 8桁固定長に正規化
                SUM(SalesQty) as TotalSalesQty,
                SUM(PurchaseQty) as TotalPurchaseQty,
                SUM(AdjustmentQty) as TotalAdjustmentQty,
                SUM(SalesAmount) as TotalSalesAmount,
                SUM(PurchaseAmount) as TotalPurchaseAmount,
                SUM(AdjustmentAmount) as TotalAdjustmentAmount
            FROM (
                -- 売上データ
                SELECT 
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
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
                    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
                    0 as SalesQty,
                    0 as PurchaseQty,
                    Quantity as AdjustmentQty,
                    0 as SalesAmount,
                    0 as PurchaseAmount,
                    Amount as AdjustmentAmount
                FROM InventoryAdjustments 
                WHERE CAST(JobDate AS DATE) = CAST(@JobDate AS DATE)
            ) AS AllTransactions
            GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, LEFT(RTRIM(COALESCE(ManualShippingMark, '')) + REPLICATE(' ', 8), 8)
        )
        
        -- MERGE文で在庫マスタを更新（スナップショット管理）
        -- ☆マスタ存在チェックを追加☆
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
            -- ☆マスタ存在チェック条件を追加☆
            WHERE EXISTS (SELECT 1 FROM ProductMaster pmc WHERE pmc.ProductCode = t.ProductCode)
                AND (t.GradeCode = '000' OR EXISTS (SELECT 1 FROM GradeMaster gm WHERE gm.GradeCode = t.GradeCode))
                AND (t.ClassCode = '000' OR EXISTS (SELECT 1 FROM ClassMaster cm WHERE cm.ClassCode = t.ClassCode))
                AND (t.ShippingMarkCode = '0000' OR EXISTS (SELECT 1 FROM ShippingMarkMaster sm WHERE sm.ShippingMarkCode = t.ShippingMarkCode))
        ) AS source
        ON (
            target.ProductCode = source.ProductCode
            AND target.GradeCode = source.GradeCode
            AND target.ClassCode = source.ClassCode
            AND target.ShippingMarkCode = source.ShippingMarkCode
            AND LEFT(RTRIM(COALESCE(target.ManualShippingMark, '')) + REPLICATE(' ', 8), 8) = source.ManualShippingMark  -- 8桁固定長で比較
            -- JobDate条件は削除（5項目主キーのみ）
        )
        
        -- 既存レコード：現在の状態に更新（累積ではなくスナップショット）
        WHEN MATCHED THEN
            UPDATE SET
                -- 現在在庫を新しい値で上書き（累積しない）
                CurrentStock = source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
                CurrentStockAmount = source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
                
                -- 当日在庫も同じ値
                DailyStock = source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
                DailyStockAmount = source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
                
                -- JobDateを最新の日付に更新
                JobDate = @JobDate,
                
                -- メタデータの更新
                UpdatedDate = GETDATE(),
                DataSetId = @DataSetId,
                DailyFlag = N'0'  -- データありフラグ
        
        -- 新規レコード：新規作成
        WHEN NOT MATCHED THEN
            INSERT (
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
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
                source.ShippingMarkCode, source.ManualShippingMark,
                source.ProductName,
                source.UnitName,
                source.StandardPrice,
                source.ProductCategory1,
                source.ProductCategory2,
                @JobDate, GETDATE(), GETDATE(),
                -- 新規商品の在庫数量
                source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
                source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
                -- 当日在庫（同じ値）
                source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
                source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
                N'0',  -- データありフラグ
                @DataSetId,
                0, 0,  -- 前月末在庫は0（スナップショット管理のため）
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
        
        PRINT N'在庫マスタMERGE完了（スナップショット）: 新規=' + CAST(@InsertCount AS NVARCHAR(10)) + N'件, 更新=' + CAST(@UpdateCount AS NVARCHAR(10)) + N'件';
        
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
GRANT EXECUTE ON sp_MergeInventoryMasterSnapshot TO [public];
GO

PRINT N'ストアドプロシージャ sp_MergeInventoryMasterSnapshot を作成しました。';