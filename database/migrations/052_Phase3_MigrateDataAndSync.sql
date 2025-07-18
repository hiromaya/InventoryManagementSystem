-- =====================================================
-- フェーズ3: データ移行と同期トリガー作成
-- 実行日: 2025-07-18
-- 目的: 既存データを新カラムに移行し、同期トリガーを作成
-- =====================================================

USE InventoryManagementDB;
GO

PRINT '================================';
PRINT 'フェーズ3: データ移行と同期開始';
PRINT '================================';

-- トランザクション開始
BEGIN TRANSACTION;

BEGIN TRY
    -- =====================================================
    -- 1. ProductMaster データ移行（バッチ処理）
    -- =====================================================
    
    PRINT '1. ProductMaster データ移行開始...';
    
    -- バッチサイズ設定（パフォーマンス調整）
    DECLARE @BatchSize INT = 5000;
    DECLARE @RowsAffected INT = @BatchSize;
    DECLARE @TotalUpdated INT = 0;
    
    -- ProductMaster の移行
    WHILE @RowsAffected = @BatchSize
    BEGIN
        UPDATE TOP (@BatchSize) dbo.ProductMaster
        SET
            CreatedAt = CASE 
                WHEN CreatedAt IS NULL AND CreatedDate IS NOT NULL THEN CreatedDate
                WHEN CreatedAt IS NULL THEN GETDATE()
                ELSE CreatedAt
            END,
            UpdatedAt = CASE 
                WHEN UpdatedAt IS NULL AND UpdatedDate IS NOT NULL THEN UpdatedDate
                WHEN UpdatedAt IS NULL THEN GETDATE()
                ELSE UpdatedAt
            END
        WHERE
            (CreatedAt IS NULL OR UpdatedAt IS NULL);
        
        SET @RowsAffected = @@ROWCOUNT;
        SET @TotalUpdated = @TotalUpdated + @RowsAffected;
        
        -- 進行状況表示
        IF @RowsAffected > 0
        BEGIN
            PRINT '  処理中... ' + CAST(@TotalUpdated AS VARCHAR) + ' 件完了';
        END
    END;
    
    PRINT '  ✓ ProductMaster データ移行完了: ' + CAST(@TotalUpdated AS VARCHAR) + ' 件';
    
    -- =====================================================
    -- 2. CustomerMaster データ移行（バッチ処理）
    -- =====================================================
    
    PRINT '';
    PRINT '2. CustomerMaster データ移行開始...';
    
    SET @RowsAffected = @BatchSize;
    SET @TotalUpdated = 0;
    
    WHILE @RowsAffected = @BatchSize
    BEGIN
        UPDATE TOP (@BatchSize) dbo.CustomerMaster
        SET
            CreatedAt = CASE 
                WHEN CreatedAt IS NULL AND CreatedDate IS NOT NULL THEN CreatedDate
                WHEN CreatedAt IS NULL THEN GETDATE()
                ELSE CreatedAt
            END,
            UpdatedAt = CASE 
                WHEN UpdatedAt IS NULL AND UpdatedDate IS NOT NULL THEN UpdatedDate
                WHEN UpdatedAt IS NULL THEN GETDATE()
                ELSE UpdatedAt
            END
        WHERE
            (CreatedAt IS NULL OR UpdatedAt IS NULL);
        
        SET @RowsAffected = @@ROWCOUNT;
        SET @TotalUpdated = @TotalUpdated + @RowsAffected;
        
        IF @RowsAffected > 0
        BEGIN
            PRINT '  処理中... ' + CAST(@TotalUpdated AS VARCHAR) + ' 件完了';
        END
    END;
    
    PRINT '  ✓ CustomerMaster データ移行完了: ' + CAST(@TotalUpdated AS VARCHAR) + ' 件';
    
    -- =====================================================
    -- 3. SupplierMaster データ移行（バッチ処理）
    -- =====================================================
    
    PRINT '';
    PRINT '3. SupplierMaster データ移行開始...';
    
    SET @RowsAffected = @BatchSize;
    SET @TotalUpdated = 0;
    
    WHILE @RowsAffected = @BatchSize
    BEGIN
        UPDATE TOP (@BatchSize) dbo.SupplierMaster
        SET
            CreatedAt = CASE 
                WHEN CreatedAt IS NULL AND CreatedDate IS NOT NULL THEN CreatedDate
                WHEN CreatedAt IS NULL THEN GETDATE()
                ELSE CreatedAt
            END,
            UpdatedAt = CASE 
                WHEN UpdatedAt IS NULL AND UpdatedDate IS NOT NULL THEN UpdatedDate
                WHEN UpdatedAt IS NULL THEN GETDATE()
                ELSE UpdatedAt
            END
        WHERE
            (CreatedAt IS NULL OR UpdatedAt IS NULL);
        
        SET @RowsAffected = @@ROWCOUNT;
        SET @TotalUpdated = @TotalUpdated + @RowsAffected;
        
        IF @RowsAffected > 0
        BEGIN
            PRINT '  処理中... ' + CAST(@TotalUpdated AS VARCHAR) + ' 件完了';
        END
    END;
    
    PRINT '  ✓ SupplierMaster データ移行完了: ' + CAST(@TotalUpdated AS VARCHAR) + ' 件';
    
    -- =====================================================
    -- 4. 同期トリガーの作成
    -- =====================================================
    
    PRINT '';
    PRINT '4. 同期トリガー作成開始...';
    
    -- ProductMaster 同期トリガー
    IF OBJECT_ID('TRG_ProductMaster_SyncDateColumns', 'TR') IS NOT NULL
        DROP TRIGGER TRG_ProductMaster_SyncDateColumns;
    
    EXEC('
    CREATE TRIGGER TRG_ProductMaster_SyncDateColumns
    ON dbo.ProductMaster
    AFTER INSERT, UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        
        -- 新しいレコードが挿入された場合
        IF EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
        BEGIN
            UPDATE t
            SET
                t.CreatedDate = ISNULL(t.CreatedDate, ISNULL(t.CreatedAt, GETDATE())),
                t.UpdatedDate = ISNULL(t.UpdatedDate, ISNULL(t.UpdatedAt, GETDATE())),
                t.CreatedAt = ISNULL(t.CreatedAt, ISNULL(t.CreatedDate, GETDATE())),
                t.UpdatedAt = ISNULL(t.UpdatedAt, ISNULL(t.UpdatedDate, GETDATE()))
            FROM
                dbo.ProductMaster t
            INNER JOIN
                inserted i ON t.ProductCode = i.ProductCode;
        END
        
        -- 既存のレコードが更新された場合
        IF EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
        BEGIN
            UPDATE t
            SET
                t.UpdatedDate = GETDATE(),
                t.UpdatedAt = GETDATE()
            FROM
                dbo.ProductMaster t
            INNER JOIN
                inserted i ON t.ProductCode = i.ProductCode;
        END
    END
    ');
    
    PRINT '  ✓ ProductMaster 同期トリガー作成完了';
    
    -- CustomerMaster 同期トリガー
    IF OBJECT_ID('TRG_CustomerMaster_SyncDateColumns', 'TR') IS NOT NULL
        DROP TRIGGER TRG_CustomerMaster_SyncDateColumns;
    
    EXEC('
    CREATE TRIGGER TRG_CustomerMaster_SyncDateColumns
    ON dbo.CustomerMaster
    AFTER INSERT, UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        
        IF EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
        BEGIN
            UPDATE t
            SET
                t.CreatedDate = ISNULL(t.CreatedDate, ISNULL(t.CreatedAt, GETDATE())),
                t.UpdatedDate = ISNULL(t.UpdatedDate, ISNULL(t.UpdatedAt, GETDATE())),
                t.CreatedAt = ISNULL(t.CreatedAt, ISNULL(t.CreatedDate, GETDATE())),
                t.UpdatedAt = ISNULL(t.UpdatedAt, ISNULL(t.UpdatedDate, GETDATE()))
            FROM
                dbo.CustomerMaster t
            INNER JOIN
                inserted i ON t.CustomerCode = i.CustomerCode;
        END
        
        IF EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
        BEGIN
            UPDATE t
            SET
                t.UpdatedDate = GETDATE(),
                t.UpdatedAt = GETDATE()
            FROM
                dbo.CustomerMaster t
            INNER JOIN
                inserted i ON t.CustomerCode = i.CustomerCode;
        END
    END
    ');
    
    PRINT '  ✓ CustomerMaster 同期トリガー作成完了';
    
    -- SupplierMaster 同期トリガー
    IF OBJECT_ID('TRG_SupplierMaster_SyncDateColumns', 'TR') IS NOT NULL
        DROP TRIGGER TRG_SupplierMaster_SyncDateColumns;
    
    EXEC('
    CREATE TRIGGER TRG_SupplierMaster_SyncDateColumns
    ON dbo.SupplierMaster
    AFTER INSERT, UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        
        IF EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
        BEGIN
            UPDATE t
            SET
                t.CreatedDate = ISNULL(t.CreatedDate, ISNULL(t.CreatedAt, GETDATE())),
                t.UpdatedDate = ISNULL(t.UpdatedDate, ISNULL(t.UpdatedAt, GETDATE())),
                t.CreatedAt = ISNULL(t.CreatedAt, ISNULL(t.CreatedDate, GETDATE())),
                t.UpdatedAt = ISNULL(t.UpdatedAt, ISNULL(t.UpdatedDate, GETDATE()))
            FROM
                dbo.SupplierMaster t
            INNER JOIN
                inserted i ON t.SupplierCode = i.SupplierCode;
        END
        
        IF EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
        BEGIN
            UPDATE t
            SET
                t.UpdatedDate = GETDATE(),
                t.UpdatedAt = GETDATE()
            FROM
                dbo.SupplierMaster t
            INNER JOIN
                inserted i ON t.SupplierCode = i.SupplierCode;
        END
    END
    ');
    
    PRINT '  ✓ SupplierMaster 同期トリガー作成完了';
    
    -- =====================================================
    -- 5. データ検証
    -- =====================================================
    
    PRINT '';
    PRINT '5. データ検証実行中...';
    
    -- 新旧カラムの一致状況を確認
    DECLARE @ProductMismatch INT, @CustomerMismatch INT, @SupplierMismatch INT;
    
    SELECT @ProductMismatch = COUNT(*)
    FROM ProductMaster 
    WHERE (CreatedAt IS NULL AND CreatedDate IS NOT NULL)
       OR (UpdatedAt IS NULL AND UpdatedDate IS NOT NULL)
       OR (CreatedAt IS NOT NULL AND CreatedDate IS NOT NULL AND CreatedAt != CreatedDate)
       OR (UpdatedAt IS NOT NULL AND UpdatedDate IS NOT NULL AND UpdatedAt != UpdatedDate);
    
    SELECT @CustomerMismatch = COUNT(*)
    FROM CustomerMaster 
    WHERE (CreatedAt IS NULL AND CreatedDate IS NOT NULL)
       OR (UpdatedAt IS NULL AND UpdatedDate IS NOT NULL)
       OR (CreatedAt IS NOT NULL AND CreatedDate IS NOT NULL AND CreatedAt != CreatedDate)
       OR (UpdatedAt IS NOT NULL AND UpdatedDate IS NOT NULL AND UpdatedAt != UpdatedDate);
    
    SELECT @SupplierMismatch = COUNT(*)
    FROM SupplierMaster 
    WHERE (CreatedAt IS NULL AND CreatedDate IS NOT NULL)
       OR (UpdatedAt IS NULL AND UpdatedDate IS NOT NULL)
       OR (CreatedAt IS NOT NULL AND CreatedDate IS NOT NULL AND CreatedAt != CreatedDate)
       OR (UpdatedAt IS NOT NULL AND UpdatedDate IS NOT NULL AND UpdatedAt != UpdatedDate);
    
    PRINT '  ProductMaster 不一致件数: ' + CAST(@ProductMismatch AS VARCHAR);
    PRINT '  CustomerMaster 不一致件数: ' + CAST(@CustomerMismatch AS VARCHAR);
    PRINT '  SupplierMaster 不一致件数: ' + CAST(@SupplierMismatch AS VARCHAR);
    
    IF @ProductMismatch + @CustomerMismatch + @SupplierMismatch = 0
    BEGIN
        PRINT '  ✅ データ検証OK: 新旧カラムの同期が正常です';
    END
    ELSE
    BEGIN
        PRINT '  ⚠️ 警告: 一部データに不一致があります';
    END
    
    -- コミット
    COMMIT TRANSACTION;
    
    PRINT '';
    PRINT '================================';
    PRINT 'フェーズ3: データ移行と同期完了';
    PRINT '================================';
    PRINT '';
    PRINT '次のステップ:';
    PRINT '  1. アプリケーションを新しいスキーマで動作確認';
    PRINT '  2. import-folderコマンドをテスト実行';
    PRINT '  3. 問題がなければフェーズ5でクリーンアップ';
    PRINT '';
    
END TRY
BEGIN CATCH
    -- エラー発生時はロールバック
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    -- エラー情報を表示
    PRINT '';
    PRINT '❌ エラーが発生しました';
    PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR);
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS VARCHAR);
    PRINT '';
    PRINT 'トランザクションをロールバックしました';
    
    -- エラーを再発生させて上位に伝播
    THROW;
END CATCH

GO