-- =============================================
-- UN在庫マスタ作成ストアドプロシージャ
-- 作成日: 2025-07-27
-- 説明: 伝票に存在する5項目キーのみでUN在庫マスタを作成
--       アンマッチチェック専用の一時的な在庫データ
-- =============================================
USE InventoryManagementDB;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateUnInventoryFromInventoryMaster')
BEGIN
    DROP PROCEDURE sp_CreateUnInventoryFromInventoryMaster;
    PRINT 'sp_CreateUnInventoryFromInventoryMaster を削除しました';
END
GO

CREATE PROCEDURE sp_CreateUnInventoryFromInventoryMaster
    @DataSetId NVARCHAR(100),
    @JobDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CreatedCount INT = 0;
    DECLARE @DeletedCount INT = 0;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Step 1: 既存のUN在庫マスタを完全削除（同一DataSetId）
        DELETE FROM UnInventoryMaster WHERE DataSetId = @DataSetId;
        SET @DeletedCount = @@ROWCOUNT;
        
        -- Step 2: 当日の伝票に存在する5項目キーを取得
        WITH VoucherKeys AS (
            -- 売上伝票（出荷データのみ: 数量>0）
            SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName 
            FROM SalesVouchers 
            WHERE JobDate = @JobDate 
            AND VoucherType IN ('51', '52')  -- 掛売・現売
            AND DetailType = '1'             -- 商品明細のみ
            AND Quantity > 0                 -- 出荷データ（通常売上）
            AND ProductCode != '00000'       -- 商品コード「00000」除外
            
            UNION
            
            -- 仕入伝票（仕入返品のみ: 数量<0）
            SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName 
            FROM PurchaseVouchers 
            WHERE JobDate = @JobDate 
            AND VoucherType IN ('11', '12')  -- 掛仕入・現金仕入
            AND DetailType = '1'             -- 商品明細のみ
            AND Quantity < 0                 -- 仕入返品（出荷データ）
            AND ProductCode != '00000'       -- 商品コード「00000」除外
            
            UNION
            
            -- 在庫調整（出荷データのみ: 数量>0）
            SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName 
            FROM InventoryAdjustments 
            WHERE JobDate = @JobDate
            AND VoucherType = '71'           -- 在庫調整伝票
            AND DetailType = '1'             -- 明細種1のみ
            AND Quantity > 0                 -- 出荷データのみ
            AND ProductCode != '00000'       -- 商品コード「00000」除外
            AND UnitCode NOT IN ('02', '05') -- ギフト経費・加工費B除外
        )
        -- Step 3: 伝票に存在する5項目キーの在庫マスタレコードのみをUN在庫マスタに作成
        INSERT INTO UnInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            DataSetId, PreviousDayStock, DailyStock, DailyFlag, JobDate, CreatedDate, UpdatedDate
        )
        SELECT DISTINCT
            vk.ProductCode, vk.GradeCode, vk.ClassCode, vk.ShippingMarkCode, vk.ShippingMarkName,
            @DataSetId, 
            ISNULL(im.CurrentStock, 0) as PreviousDayStock,   -- 前日残高
            ISNULL(im.CurrentStock, 0) as DailyStock,         -- 当日残高（初期値）
            '9' as DailyFlag,                                 -- 未処理フラグ
            @JobDate, 
            GETDATE(), 
            GETDATE()
        FROM VoucherKeys vk
        LEFT JOIN InventoryMaster im ON (
            im.ProductCode = vk.ProductCode
            AND im.GradeCode = vk.GradeCode
            AND im.ClassCode = vk.ClassCode
            AND im.ShippingMarkCode = vk.ShippingMarkCode
            AND im.ShippingMarkName = vk.ShippingMarkName
            AND im.JobDate <= @JobDate     -- 指定日以前の最新データ
            AND im.IsActive = 1            -- アクティブなレコードのみ
        );
        
        SET @CreatedCount = @@ROWCOUNT;
        
        COMMIT TRANSACTION;
        
        -- 結果を返す
        SELECT 
            @DeletedCount as DeletedCount,
            @CreatedCount as CreatedCount,
            'UN在庫マスタ作成完了: 伝票キー対象のみ' as Message;
            
        PRINT 'UN在庫マスタ作成完了';
        PRINT '削除件数: ' + CAST(@DeletedCount AS NVARCHAR(10));
        PRINT '作成件数: ' + CAST(@CreatedCount AS NVARCHAR(10));
        PRINT '対象: 伝票に存在する5項目キーのみ';
        
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        PRINT 'UN在庫マスタ作成エラー: ' + @ErrorMessage;
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

PRINT 'sp_CreateUnInventoryFromInventoryMaster ストアドプロシージャを作成しました';
PRINT '機能: 伝票に存在する5項目キーのみでUN在庫マスタを作成';
PRINT '用途: アンマッチチェック専用の一時在庫データ';
PRINT '特徴: 出荷データ（売上・仕入返品・在庫調整）のみ対象';
GO