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
    
    -- 前提条件のチェック：移行元と移行先のカラムが両方存在するか
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'CreatedDate') AND
       EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'CreatedAt')
    BEGIN
        PRINT '  - 移行条件を満たしたため、データ移行を実行します。';
        
        -- 動的SQLでデータ移行を実行（コンパイル時エラー回避）
        DECLARE @sql NVARCHAR(MAX);
        DECLARE @BatchSize INT = 5000;
        DECLARE @RowsAffected INT = @BatchSize;
        DECLARE @TotalUpdated INT = 0;
        
        -- ProductMaster の移行（動的SQL）
        WHILE @RowsAffected = @BatchSize
        BEGIN
            SET @sql = N'
                UPDATE TOP (' + CAST(@BatchSize AS NVARCHAR) + N') dbo.ProductMaster
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
                    (CreatedAt IS NULL OR UpdatedAt IS NULL);';
            
            EXEC sp_executesql @sql;
            SET @RowsAffected = @@ROWCOUNT;
            SET @TotalUpdated = @TotalUpdated + @RowsAffected;
            
            -- 進行状況表示
            IF @RowsAffected > 0
            BEGIN
                PRINT '  処理中... ' + CAST(@TotalUpdated AS VARCHAR) + ' 件完了';
            END
        END;
        
        PRINT '  ✓ ProductMaster データ移行完了: ' + CAST(@TotalUpdated AS VARCHAR) + ' 件';
    END
    ELSE
    BEGIN
        PRINT '  - 移行元のCreatedDateカラムが存在しないため、ProductMasterのデータ移行をスキップしました。';
    END
    
    -- =====================================================
    -- 2. CustomerMaster データ移行（バッチ処理）
    -- =====================================================
    
    PRINT '';
    PRINT '2. CustomerMaster データ移行開始...';
    
    -- 前提条件のチェック：移行元と移行先のカラムが両方存在するか
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'CreatedDate') AND
       EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'CreatedAt')
    BEGIN
        PRINT '  - 移行条件を満たしたため、データ移行を実行します。';
        
        -- 動的SQLでデータ移行を実行（コンパイル時エラー回避）
        DECLARE @sql2 NVARCHAR(MAX);
        DECLARE @BatchSize2 INT = 5000;
        DECLARE @RowsAffected2 INT = @BatchSize2;
        DECLARE @TotalUpdated2 INT = 0;
        
        WHILE @RowsAffected2 = @BatchSize2
        BEGIN
            SET @sql2 = N'
                UPDATE TOP (' + CAST(@BatchSize2 AS NVARCHAR) + N') dbo.CustomerMaster
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
                    (CreatedAt IS NULL OR UpdatedAt IS NULL);';
            
            EXEC sp_executesql @sql2;
            SET @RowsAffected2 = @@ROWCOUNT;
            SET @TotalUpdated2 = @TotalUpdated2 + @RowsAffected2;
            
            IF @RowsAffected2 > 0
            BEGIN
                PRINT '  処理中... ' + CAST(@TotalUpdated2 AS VARCHAR) + ' 件完了';
            END
        END;
        
        PRINT '  ✓ CustomerMaster データ移行完了: ' + CAST(@TotalUpdated2 AS VARCHAR) + ' 件';
    END
    ELSE
    BEGIN
        PRINT '  - 移行元のCreatedDateカラムが存在しないため、CustomerMasterのデータ移行をスキップしました。';
    END
    
    -- =====================================================
    -- 3. SupplierMaster データ移行（バッチ処理）
    -- =====================================================
    
    PRINT '';
    PRINT '3. SupplierMaster データ移行開始...';
    
    -- 前提条件のチェック：移行元と移行先のカラムが両方存在するか
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'CreatedDate') AND
       EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'CreatedAt')
    BEGIN
        PRINT '  - 移行条件を満たしたため、データ移行を実行します。';
        
        -- 動的SQLでデータ移行を実行（コンパイル時エラー回避）
        DECLARE @sql3 NVARCHAR(MAX);
        DECLARE @BatchSize3 INT = 5000;
        DECLARE @RowsAffected3 INT = @BatchSize3;
        DECLARE @TotalUpdated3 INT = 0;
        
        WHILE @RowsAffected3 = @BatchSize3
        BEGIN
            SET @sql3 = N'
                UPDATE TOP (' + CAST(@BatchSize3 AS NVARCHAR) + N') dbo.SupplierMaster
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
                    (CreatedAt IS NULL OR UpdatedAt IS NULL);';
            
            EXEC sp_executesql @sql3;
            SET @RowsAffected3 = @@ROWCOUNT;
            SET @TotalUpdated3 = @TotalUpdated3 + @RowsAffected3;
            
            IF @RowsAffected3 > 0
            BEGIN
                PRINT '  処理中... ' + CAST(@TotalUpdated3 AS VARCHAR) + ' 件完了';
            END
        END;
        
        PRINT '  ✓ SupplierMaster データ移行完了: ' + CAST(@TotalUpdated3 AS VARCHAR) + ' 件';
    END
    ELSE
    BEGIN
        PRINT '  - 移行元のCreatedDateカラムが存在しないため、SupplierMasterのデータ移行をスキップしました。';
    END
    
    -- =====================================================
    -- 4. 同期トリガーの作成
    -- =====================================================
    
    PRINT '';
    PRINT '4. 同期トリガー作成開始...';
    
    -- ProductMaster 同期トリガー（新旧カラム同期対応）
    IF OBJECT_ID('TRG_ProductMaster_SyncDateColumns', 'TR') IS NOT NULL
        DROP TRIGGER TRG_ProductMaster_SyncDateColumns;
    
    -- 古いカラムと新しいカラムの両方が存在する場合のみ同期トリガーを作成
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'CreatedDate') AND
       EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'CreatedAt')
    BEGIN
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
        PRINT '  ✓ ProductMaster 同期トリガー作成完了（新旧カラム同期対応）';
    END
    ELSE
    BEGIN
        PRINT '  - ProductMaster: 古いカラムが存在しないため同期トリガーをスキップしました';
    END
    
    
    -- CustomerMaster 同期トリガー（新旧カラム同期対応）
    IF OBJECT_ID('TRG_CustomerMaster_SyncDateColumns', 'TR') IS NOT NULL
        DROP TRIGGER TRG_CustomerMaster_SyncDateColumns;
    
    -- 古いカラムと新しいカラムの両方が存在する場合のみ同期トリガーを作成
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'CreatedDate') AND
       EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'CreatedAt')
    BEGIN
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
        PRINT '  ✓ CustomerMaster 同期トリガー作成完了（新旧カラム同期対応）';
    END
    ELSE
    BEGIN
        PRINT '  - CustomerMaster: 古いカラムが存在しないため同期トリガーをスキップしました';
    END
    
    -- SupplierMaster 同期トリガー（新旧カラム同期対応）
    IF OBJECT_ID('TRG_SupplierMaster_SyncDateColumns', 'TR') IS NOT NULL
        DROP TRIGGER TRG_SupplierMaster_SyncDateColumns;
    
    -- 古いカラムと新しいカラムの両方が存在する場合のみ同期トリガーを作成
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'CreatedDate') AND
       EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'CreatedAt')
    BEGIN
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
        PRINT '  ✓ SupplierMaster 同期トリガー作成完了（新旧カラム同期対応）';
    END
    ELSE
    BEGIN
        PRINT '  - SupplierMaster: 古いカラムが存在しないため同期トリガーをスキップしました';
    END
    
    -- =====================================================
    -- 5. データ検証（動的SQL使用）
    -- =====================================================
    
    PRINT '';
    PRINT '5. データ検証実行中...';
    
    -- 新旧カラムの一致状況を確認（動的SQLで古いカラム参照エラーを回避）
    DECLARE @ProductMismatch INT = 0, @CustomerMismatch INT = 0, @SupplierMismatch INT = 0;
    DECLARE @validationSql NVARCHAR(MAX);
    
    -- ProductMaster検証（古いカラムが存在する場合のみ）
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'CreatedDate')
    BEGIN
        SET @validationSql = N'
            SELECT @mismatch = COUNT(*)
            FROM ProductMaster 
            WHERE (CreatedAt IS NULL AND CreatedDate IS NOT NULL)
               OR (UpdatedAt IS NULL AND UpdatedDate IS NOT NULL)
               OR (CreatedAt IS NOT NULL AND CreatedDate IS NOT NULL AND CreatedAt != CreatedDate)
               OR (UpdatedAt IS NOT NULL AND UpdatedDate IS NOT NULL AND UpdatedAt != UpdatedDate);';
        EXEC sp_executesql @validationSql, N'@mismatch INT OUTPUT', @mismatch = @ProductMismatch OUTPUT;
    END
    
    -- CustomerMaster検証（古いカラムが存在する場合のみ）
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'CreatedDate')
    BEGIN
        SET @validationSql = N'
            SELECT @mismatch = COUNT(*)
            FROM CustomerMaster 
            WHERE (CreatedAt IS NULL AND CreatedDate IS NOT NULL)
               OR (UpdatedAt IS NULL AND UpdatedDate IS NOT NULL)
               OR (CreatedAt IS NOT NULL AND CreatedDate IS NOT NULL AND CreatedAt != CreatedDate)
               OR (UpdatedAt IS NOT NULL AND UpdatedDate IS NOT NULL AND UpdatedAt != UpdatedDate);';
        EXEC sp_executesql @validationSql, N'@mismatch INT OUTPUT', @mismatch = @CustomerMismatch OUTPUT;
    END
    
    -- SupplierMaster検証（古いカラムが存在する場合のみ）
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'CreatedDate')
    BEGIN
        SET @validationSql = N'
            SELECT @mismatch = COUNT(*)
            FROM SupplierMaster 
            WHERE (CreatedAt IS NULL AND CreatedDate IS NOT NULL)
               OR (UpdatedAt IS NULL AND UpdatedDate IS NOT NULL)
               OR (CreatedAt IS NOT NULL AND CreatedDate IS NOT NULL AND CreatedAt != CreatedDate)
               OR (UpdatedAt IS NOT NULL AND UpdatedDate IS NOT NULL AND UpdatedAt != UpdatedDate);';
        EXEC sp_executesql @validationSql, N'@mismatch INT OUTPUT', @mismatch = @SupplierMismatch OUTPUT;
    END
    
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