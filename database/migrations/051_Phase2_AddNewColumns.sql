-- =====================================================
-- フェーズ2: 新しいカラムの追加（非破壊的変更）
-- 実行日: 2025-07-18
-- 目的: 既存テーブルに新しいCreatedAt/UpdatedAtカラムを追加
-- =====================================================

USE InventoryManagementDB;
GO

PRINT '================================';
PRINT 'フェーズ2: 新しいカラムの追加開始';
PRINT '================================';

-- トランザクション開始
BEGIN TRANSACTION;

BEGIN TRY
    -- =====================================================
    -- 1. ProductMaster への新カラム追加
    -- =====================================================
    
    PRINT '1. ProductMaster カラム追加確認中...';
    
    -- CreatedAt カラムの追加確認と追加
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'CreatedAt'
    )
    BEGIN
        ALTER TABLE dbo.ProductMaster ADD CreatedAt DATETIME2 NULL;
        PRINT '  ✓ ProductMaster.CreatedAt カラムを追加しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ ProductMaster.CreatedAt カラムは既に存在します';
    END
    
    -- UpdatedAt カラムの追加確認と追加
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'ProductMaster' AND COLUMN_NAME = 'UpdatedAt'
    )
    BEGIN
        ALTER TABLE dbo.ProductMaster ADD UpdatedAt DATETIME2 NULL;
        PRINT '  ✓ ProductMaster.UpdatedAt カラムを追加しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ ProductMaster.UpdatedAt カラムは既に存在します';
    END
    
    -- =====================================================
    -- 2. CustomerMaster への新カラム追加
    -- =====================================================
    
    PRINT '';
    PRINT '2. CustomerMaster カラム追加確認中...';
    
    -- CreatedAt カラムの追加確認と追加
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'CreatedAt'
    )
    BEGIN
        ALTER TABLE dbo.CustomerMaster ADD CreatedAt DATETIME2 NULL;
        PRINT '  ✓ CustomerMaster.CreatedAt カラムを追加しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ CustomerMaster.CreatedAt カラムは既に存在します';
    END
    
    -- UpdatedAt カラムの追加確認と追加
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'CustomerMaster' AND COLUMN_NAME = 'UpdatedAt'
    )
    BEGIN
        ALTER TABLE dbo.CustomerMaster ADD UpdatedAt DATETIME2 NULL;
        PRINT '  ✓ CustomerMaster.UpdatedAt カラムを追加しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ CustomerMaster.UpdatedAt カラムは既に存在します';
    END
    
    -- =====================================================
    -- 3. SupplierMaster への新カラム追加
    -- =====================================================
    
    PRINT '';
    PRINT '3. SupplierMaster カラム追加確認中...';
    
    -- CreatedAt カラムの追加確認と追加
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'CreatedAt'
    )
    BEGIN
        ALTER TABLE dbo.SupplierMaster ADD CreatedAt DATETIME2 NULL;
        PRINT '  ✓ SupplierMaster.CreatedAt カラムを追加しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ SupplierMaster.CreatedAt カラムは既に存在します';
    END
    
    -- UpdatedAt カラムの追加確認と追加
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'SupplierMaster' AND COLUMN_NAME = 'UpdatedAt'
    )
    BEGIN
        ALTER TABLE dbo.SupplierMaster ADD UpdatedAt DATETIME2 NULL;
        PRINT '  ✓ SupplierMaster.UpdatedAt カラムを追加しました';
    END
    ELSE
    BEGIN
        PRINT '  ○ SupplierMaster.UpdatedAt カラムは既に存在します';
    END
    
    -- =====================================================
    -- 4. 結果確認
    -- =====================================================
    
    PRINT '';
    PRINT '4. 追加結果確認';
    
    -- 各テーブルの日付カラム状況を確認
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
    
    -- 各テーブルのレコード数も確認
    DECLARE @ProductCount INT, @CustomerCount INT, @SupplierCount INT;
    
    SELECT @ProductCount = COUNT(*) FROM ProductMaster;
    SELECT @CustomerCount = COUNT(*) FROM CustomerMaster;
    SELECT @SupplierCount = COUNT(*) FROM SupplierMaster;
    
    PRINT '';
    PRINT '5. テーブルレコード数確認';
    PRINT '  ProductMaster: ' + CAST(@ProductCount AS VARCHAR) + ' 件';
    PRINT '  CustomerMaster: ' + CAST(@CustomerCount AS VARCHAR) + ' 件';
    PRINT '  SupplierMaster: ' + CAST(@SupplierCount AS VARCHAR) + ' 件';
    
    -- コミット
    COMMIT TRANSACTION;
    
    PRINT '';
    PRINT '================================';
    PRINT 'フェーズ2: 新しいカラム追加完了';
    PRINT '================================';
    PRINT '';
    PRINT '次のステップ:';
    PRINT '  1. フェーズ3でデータ移行を実行してください';
    PRINT '  2. コマンド: dotnet run migrate-phase3';
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