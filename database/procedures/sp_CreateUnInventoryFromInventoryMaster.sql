-- =============================================
-- UN在庫マスタ作成ストアドプロシージャ（最終修正版）
-- 作成日: 2025-07-28
-- 説明: InventoryMasterの正しいカラム名を使用
-- =============================================
USE InventoryManagementDB;
GO

-- 既存のストアドプロシージャを削除
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateUnInventoryFromInventoryMaster')
BEGIN
    DROP PROCEDURE sp_CreateUnInventoryFromInventoryMaster;
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
    DECLARE @ErrorMessage NVARCHAR(4000);
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Step 1: 既存データ削除
        DELETE FROM dbo.UnInventoryMaster WHERE DataSetId = @DataSetId;
        SET @DeletedCount = @@ROWCOUNT;
        
        -- Step 2: UN在庫マスタ作成
        
        -- 在庫マスタに存在する商品のみを登録
        -- （売上伝票にあって在庫マスタにない商品は登録されない = アンマッチとして検出される）
        INSERT INTO dbo.UnInventoryMaster (
            ProductCode, 
            GradeCode, 
            ClassCode, 
            ShippingMarkCode, 
            ShippingMarkName,
            DataSetId, 
            PreviousDayStock,    -- CurrentStockを前日在庫として使用
            DailyStock,          -- DailyStockをそのまま使用
            DailyFlag, 
            JobDate, 
            CreatedDate, 
            UpdatedDate
        )
        SELECT 
            im.ProductCode, 
            im.GradeCode, 
            im.ClassCode, 
            im.ShippingMarkCode, 
            im.ShippingMarkName,
            @DataSetId, 
            im.CurrentStock,     -- 現在在庫を前日在庫として扱う
            im.DailyStock,       -- 当日在庫
            im.DailyFlag,        -- 当日発生フラグ
            @JobDate, 
            GETDATE(), 
            GETDATE()
        FROM dbo.InventoryMaster im
        WHERE im.JobDate = @JobDate
        AND im.IsActive = 1;     -- アクティブなレコードのみ
        
        SET @CreatedCount = @@ROWCOUNT;
        
        -- コミット
        COMMIT TRANSACTION;
        
        -- 結果を返す
        SELECT 
            @DeletedCount as DeletedCount,
            @CreatedCount as CreatedCount,
            'UN在庫マスタ作成完了' as Message;
        
        -- デバッグ情報
        PRINT 'UN在庫マスタ作成完了: 削除=' + CAST(@DeletedCount AS NVARCHAR(10)) + 
              '件, 作成=' + CAST(@CreatedCount AS NVARCHAR(10)) + '件';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        SET @ErrorMessage = ERROR_MESSAGE();
        PRINT 'エラー発生: ' + @ErrorMessage;
        THROW;
    END CATCH
END
GO

-- 権限付与
GRANT EXECUTE ON sp_CreateUnInventoryFromInventoryMaster TO [public];
GO

PRINT '';
PRINT '=== sp_CreateUnInventoryFromInventoryMaster（最終修正版）作成完了 ===';
GO

-- 作成確認
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateUnInventoryFromInventoryMaster')
    PRINT '✅ ストアドプロシージャが正常に作成されました';
ELSE
    PRINT '❌ ストアドプロシージャの作成に失敗しました';
GO