-- =============================================
-- UN在庫マスタ作成ストアドプロシージャ（修正版）
-- 作成日: 2025-07-28
-- 説明: GETDATE()関数のエラーを修正
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
    
    DECLARE @sql NVARCHAR(MAX);
    DECLARE @params NVARCHAR(500);
    DECLARE @CreatedCount INT = 0;
    DECLARE @DeletedCount INT = 0;
    DECLARE @CurrentDateTime DATETIME = GETDATE(); -- 現在時刻を変数に格納
    
    -- テーブル存在確認
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UnInventoryMaster')
    BEGIN
        RAISERROR('UnInventoryMasterテーブルが存在しません', 16, 1);
        RETURN;
    END
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Step 1: 既存データ削除
        SET @sql = N'DELETE FROM dbo.UnInventoryMaster WHERE DataSetId = @DataSetId';
        SET @params = N'@DataSetId NVARCHAR(100)';
        EXEC sp_executesql @sql, @params, @DataSetId = @DataSetId;
        SET @DeletedCount = @@ROWCOUNT;
        
        -- Step 2: 売上伝票から5項目キーを抽出してUN在庫マスタ作成
        SET @sql = N'
        ;WITH VoucherKeys AS (
            -- 売上伝票（出荷データのみ）
            SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName 
            FROM dbo.SalesVouchers 
            WHERE JobDate = @JobDate 
            AND VoucherType IN (''51'', ''52'')
            AND DetailType = ''1''
            AND Quantity > 0
            AND ProductCode != ''00000''
            
            UNION
            
            -- 仕入伝票（仕入返品のみ）
            SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName 
            FROM dbo.PurchaseVouchers 
            WHERE JobDate = @JobDate 
            AND VoucherType IN (''11'', ''12'')
            AND DetailType = ''1''
            AND Quantity < 0
            AND ProductCode != ''00000''
            
            UNION
            
            -- 在庫調整（出荷データのみ）
            SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName 
            FROM dbo.InventoryAdjustments 
            WHERE JobDate = @JobDate
            AND VoucherType = ''71''
            AND DetailType = ''1''
            AND Quantity > 0
            AND ProductCode != ''00000''
            AND (CategoryCode IS NULL OR CategoryCode NOT IN (2, 5))
        )
        INSERT INTO dbo.UnInventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            DataSetId, PreviousDayStock, DailyStock, DailyFlag, JobDate, CreatedDate, UpdatedDate
        )
        SELECT DISTINCT
            vk.ProductCode, vk.GradeCode, vk.ClassCode, vk.ShippingMarkCode, vk.ShippingMarkName,
            @DataSetId, 
            ISNULL(im.CurrentStock, 0),
            ISNULL(im.CurrentStock, 0),
            ''9'',
            @JobDate, 
            @CurrentDateTime, 
            @CurrentDateTime
        FROM VoucherKeys vk
        LEFT JOIN dbo.InventoryMaster im ON (
            im.ProductCode = vk.ProductCode
            AND im.GradeCode = vk.GradeCode
            AND im.ClassCode = vk.ClassCode
            AND im.ShippingMarkCode = vk.ShippingMarkCode
            AND im.ShippingMarkName = vk.ShippingMarkName
            AND im.JobDate <= @JobDate
            AND im.IsActive = 1
        )';
        
        SET @params = N'@DataSetId NVARCHAR(100), @JobDate DATE, @CurrentDateTime DATETIME';
        EXEC sp_executesql @sql, @params, 
            @DataSetId = @DataSetId, 
            @JobDate = @JobDate,
            @CurrentDateTime = @CurrentDateTime;
        SET @CreatedCount = @@ROWCOUNT;
        
        COMMIT TRANSACTION;
        
        -- 結果を返す
        SELECT 
            @DeletedCount as DeletedCount,
            @CreatedCount as CreatedCount,
            'UN在庫マスタ作成完了' as Message;
        
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

PRINT 'sp_CreateUnInventoryFromInventoryMaster（修正版）を作成しました';
GO

-- テスト実行（コメントアウトして実行）
-- EXEC sp_CreateUnInventoryFromInventoryMaster 
--     @DataSetId = 'TEST_' + FORMAT(GETDATE(), 'yyyyMMddHHmmss'), 
--     @JobDate = '2025-06-01';

-- 結果確認（コメントアウトして実行）
-- SELECT TOP 10 * FROM dbo.UnInventoryMaster ORDER BY CreatedDate DESC;