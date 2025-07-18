-- =====================================================
-- フェーズ5: クリーンアップ（古いスキーマの削除）
-- 実行日: 2025-07-18
-- 目的: 移行完了後の古いカラムとトリガーの削除
-- =====================================================

USE InventoryManagementDB;
GO

PRINT '================================';
PRINT 'フェーズ5: クリーンアップ開始';
PRINT '================================';

-- 安全確認
PRINT '⚠️  注意: このスクリプトは古いカラムを削除します';
PRINT '    実行前に以下を確認してください:';
PRINT '    1. アプリケーションが新しいスキーマで正常動作している';
PRINT '    2. import-folderコマンドが成功している';
PRINT '    3. データベースの完全バックアップを取得済み';
PRINT '';

-- トランザクション開始
BEGIN TRANSACTION;

BEGIN TRY
    -- =====================================================
    -- 1. 最終検証（削除前の安全確認）
    -- =====================================================
    
    PRINT '1. 削除前の最終検証...';
    
    -- 新しいカラムが存在し、データが入っていることを確認
    DECLARE @ProductCreatedAtCount INT, @ProductUpdatedAtCount INT;
    DECLARE @CustomerCreatedAtCount INT, @CustomerUpdatedAtCount INT;
    DECLARE @SupplierCreatedAtCount INT, @SupplierUpdatedAtCount INT;
    
    SELECT @ProductCreatedAtCount = COUNT(*) FROM ProductMaster WHERE CreatedAt IS NOT NULL;
    SELECT @ProductUpdatedAtCount = COUNT(*) FROM ProductMaster WHERE UpdatedAt IS NOT NULL;
    SELECT @CustomerCreatedAtCount = COUNT(*) FROM CustomerMaster WHERE CreatedAt IS NOT NULL;
    SELECT @CustomerUpdatedAtCount = COUNT(*) FROM CustomerMaster WHERE UpdatedAt IS NOT NULL;
    SELECT @SupplierCreatedAtCount = COUNT(*) FROM SupplierMaster WHERE CreatedAt IS NOT NULL;
    SELECT @SupplierUpdatedAtCount = COUNT(*) FROM SupplierMaster WHERE UpdatedAt IS NOT NULL;
    
    PRINT '  新しいカラムのデータ確認:';
    PRINT '    ProductMaster.CreatedAt: ' + CAST(@ProductCreatedAtCount AS VARCHAR) + ' 件';
    PRINT '    ProductMaster.UpdatedAt: ' + CAST(@ProductUpdatedAtCount AS VARCHAR) + ' 件';
    PRINT '    CustomerMaster.CreatedAt: ' + CAST(@CustomerCreatedAtCount AS VARCHAR) + ' 件';
    PRINT '    CustomerMaster.UpdatedAt: ' + CAST(@CustomerUpdatedAtCount AS VARCHAR) + ' 件';
    PRINT '    SupplierMaster.CreatedAt: ' + CAST(@SupplierCreatedAtCount AS VARCHAR) + ' 件';
    PRINT '    SupplierMaster.UpdatedAt: ' + CAST(@SupplierUpdatedAtCount AS VARCHAR) + ' 件';
    
    -- 各テーブルのレコード数
    DECLARE @ProductTotalCount INT, @CustomerTotalCount INT, @SupplierTotalCount INT;
    SELECT @ProductTotalCount = COUNT(*) FROM ProductMaster;
    SELECT @CustomerTotalCount = COUNT(*) FROM CustomerMaster;
    SELECT @SupplierTotalCount = COUNT(*) FROM SupplierMaster;
    
    PRINT '  テーブル全体のレコード数:';
    PRINT '    ProductMaster: ' + CAST(@ProductTotalCount AS VARCHAR) + ' 件';
    PRINT '    CustomerMaster: ' + CAST(@CustomerTotalCount AS VARCHAR) + ' 件';
    PRINT '    SupplierMaster: ' + CAST(@SupplierTotalCount AS VARCHAR) + ' 件';
    
    -- 安全チェック: 新しいカラムにデータが十分にあるか確認
    IF @ProductCreatedAtCount < @ProductTotalCount * 0.9 
       OR @CustomerCreatedAtCount < @CustomerTotalCount * 0.9 
       OR @SupplierCreatedAtCount < @SupplierTotalCount * 0.9
    BEGIN
        PRINT '';
        PRINT '❌ 警告: 新しいカラムに十分なデータがありません';
        PRINT '   移行が完了していない可能性があります';
        PRINT '   フェーズ3を再実行してください';
        ROLLBACK TRANSACTION;
        RETURN;
    END
    
    PRINT '  ✅ 新しいカラムのデータ確認OK';
    
    -- =====================================================
    -- 2. 同期トリガーの削除
    -- =====================================================
    
    PRINT '';
    PRINT '2. 同期トリガーの削除...';
    
    -- ProductMaster トリガー削除
    IF OBJECT_ID('TRG_ProductMaster_SyncDateColumns', 'TR') IS NOT NULL
    BEGIN
        DROP TRIGGER TRG_ProductMaster_SyncDateColumns;
        PRINT '  ✓ ProductMaster 同期トリガーを削除しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ ProductMaster 同期トリガーは存在しません';
    END
    
    -- CustomerMaster トリガー削除
    IF OBJECT_ID('TRG_CustomerMaster_SyncDateColumns', 'TR') IS NOT NULL
    BEGIN
        DROP TRIGGER TRG_CustomerMaster_SyncDateColumns;
        PRINT '  ✓ CustomerMaster 同期トリガーを削除しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ CustomerMaster 同期トリガーは存在しません';
    END
    
    -- SupplierMaster トリガー削除
    IF OBJECT_ID('TRG_SupplierMaster_SyncDateColumns', 'TR') IS NOT NULL
    BEGIN
        DROP TRIGGER TRG_SupplierMaster_SyncDateColumns;
        PRINT '  ✓ SupplierMaster 同期トリガーを削除しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ SupplierMaster 同期トリガーは存在しません';
    END
    
    -- =====================================================
    -- 3. 古いカラムの削除
    -- =====================================================
    
    PRINT '';
    PRINT '3. 古いカラムの削除...';
    
    -- ProductMaster の古いカラム削除
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'CreatedDate')
    BEGIN
        ALTER TABLE dbo.ProductMaster DROP COLUMN CreatedDate;
        PRINT '  ✓ ProductMaster.CreatedDate カラムを削除しました';
    END
    
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'UpdatedDate')
    BEGIN
        ALTER TABLE dbo.ProductMaster DROP COLUMN UpdatedDate;
        PRINT '  ✓ ProductMaster.UpdatedDate カラムを削除しました';
    END
    
    -- CustomerMaster の古いカラム削除
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'CreatedDate')
    BEGIN
        ALTER TABLE dbo.CustomerMaster DROP COLUMN CreatedDate;
        PRINT '  ✓ CustomerMaster.CreatedDate カラムを削除しました';
    END
    
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'UpdatedDate')
    BEGIN
        ALTER TABLE dbo.CustomerMaster DROP COLUMN UpdatedDate;
        PRINT '  ✓ CustomerMaster.UpdatedDate カラムを削除しました';
    END
    
    -- SupplierMaster の古いカラム削除
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'CreatedDate')
    BEGIN
        ALTER TABLE dbo.SupplierMaster DROP COLUMN CreatedDate;
        PRINT '  ✓ SupplierMaster.CreatedDate カラムを削除しました';
    END
    
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'UpdatedDate')
    BEGIN
        ALTER TABLE dbo.SupplierMaster DROP COLUMN UpdatedDate;
        PRINT '  ✓ SupplierMaster.UpdatedDate カラムを削除しました';
    END
    
    -- =====================================================
    -- 4. 新しいカラムを NOT NULL に変更
    -- =====================================================
    
    PRINT '';
    PRINT '4. 新しいカラムのNOT NULL制約設定...';
    
    -- NULL値を持つレコードがないことを確認してからNOT NULL制約を設定
    DECLARE @NullCount INT;
    
    -- ProductMaster
    SELECT @NullCount = COUNT(*) FROM ProductMaster WHERE CreatedAt IS NULL OR UpdatedAt IS NULL;
    IF @NullCount = 0
    BEGIN
        ALTER TABLE dbo.ProductMaster ALTER COLUMN CreatedAt DATETIME2 NOT NULL;
        ALTER TABLE dbo.ProductMaster ALTER COLUMN UpdatedAt DATETIME2 NOT NULL;
        PRINT '  ✓ ProductMaster カラムをNOT NULLに変更しました';
    END
    ELSE
    BEGIN
        PRINT '  ⚠️ ProductMaster にNULL値があるため、NOT NULL制約をスキップしました (' + CAST(@NullCount AS VARCHAR) + ' 件)';
    END
    
    -- CustomerMaster
    SELECT @NullCount = COUNT(*) FROM CustomerMaster WHERE CreatedAt IS NULL OR UpdatedAt IS NULL;
    IF @NullCount = 0
    BEGIN
        ALTER TABLE dbo.CustomerMaster ALTER COLUMN CreatedAt DATETIME2 NOT NULL;
        ALTER TABLE dbo.CustomerMaster ALTER COLUMN UpdatedAt DATETIME2 NOT NULL;
        PRINT '  ✓ CustomerMaster カラムをNOT NULLに変更しました';
    END
    ELSE
    BEGIN
        PRINT '  ⚠️ CustomerMaster にNULL値があるため、NOT NULL制約をスキップしました (' + CAST(@NullCount AS VARCHAR) + ' 件)';
    END
    
    -- SupplierMaster
    SELECT @NullCount = COUNT(*) FROM SupplierMaster WHERE CreatedAt IS NULL OR UpdatedAt IS NULL;
    IF @NullCount = 0
    BEGIN
        ALTER TABLE dbo.SupplierMaster ALTER COLUMN CreatedAt DATETIME2 NOT NULL;
        ALTER TABLE dbo.SupplierMaster ALTER COLUMN UpdatedAt DATETIME2 NOT NULL;
        PRINT '  ✓ SupplierMaster カラムをNOT NULLに変更しました';
    END
    ELSE
    BEGIN
        PRINT '  ⚠️ SupplierMaster にNULL値があるため、NOT NULL制約をスキップしました (' + CAST(@NullCount AS VARCHAR) + ' 件)';
    END
    
    -- =====================================================
    -- 5. インデックスの再構築
    -- =====================================================
    
    PRINT '';
    PRINT '5. インデックスの再構築...';
    
    -- 統計情報の更新とインデックスの再構築
    UPDATE STATISTICS ProductMaster;
    UPDATE STATISTICS CustomerMaster;
    UPDATE STATISTICS SupplierMaster;
    
    PRINT '  ✓ 統計情報を更新しました';
    
    -- 主要なインデックスを再構築（断片化解消）
    ALTER INDEX ALL ON ProductMaster REBUILD;
    ALTER INDEX ALL ON CustomerMaster REBUILD;
    ALTER INDEX ALL ON SupplierMaster REBUILD;
    
    PRINT '  ✓ インデックスを再構築しました';
    
    -- =====================================================
    -- 6. 最終確認
    -- =====================================================
    
    PRINT '';
    PRINT '6. 最終確認...';
    
    -- 移行後のカラム構成を表示
    SELECT 
        TABLE_NAME,
        COLUMN_NAME,
        DATA_TYPE,
        IS_NULLABLE,
        COLUMN_DEFAULT
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
    AND (COLUMN_NAME LIKE '%Created%' OR COLUMN_NAME LIKE '%Updated%')
    ORDER BY TABLE_NAME, COLUMN_NAME;
    
    -- コミット
    COMMIT TRANSACTION;
    
    PRINT '';
    PRINT '================================';
    PRINT 'フェーズ5: クリーンアップ完了';
    PRINT '================================';
    PRINT '';
    PRINT '✅ スキーマ移行が完全に完了しました！';
    PRINT '';
    PRINT '次のステップ:';
    PRINT '  1. import-folderコマンドの動作確認';
    PRINT '  2. アプリケーションの全機能テスト';
    PRINT '  3. 本番環境での運用開始';
    PRINT '';
    PRINT '注意: 古いマイグレーションファイル024を無効化することを推奨';
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
    PRINT '古いカラムとトリガーは保持されています';
    
    -- エラーを再発生させて上位に伝播
    THROW;
END CATCH

GO