-- =============================================
-- Migration: 065_UnifyCategoryCodeDataType.sql
-- 作成日: 2025-08-28
-- 目的: 全分類マスタのCategoryCodeをINTからNVARCHAR(3)に統一し、3桁0埋め形式に変換
-- 説明: CpInventoryMasterとのJOIN条件を統一し、商品勘定帳票の担当者名表示問題を解決
-- =============================================

USE InventoryManagementDB;
GO

PRINT '=== Migration 065: 分類マスタのCategoryCode型統一開始 ===';

-- ===== Phase 1: バックアップテーブル作成 =====
PRINT '--- Phase 1: バックアップテーブル作成 ---';

-- ProductCategory1Masterのバックアップ
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory1Master') AND type in (N'U'))
AND NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory1Master_Backup_065') AND type in (N'U'))
BEGIN
    SELECT * INTO ProductCategory1Master_Backup_065 FROM ProductCategory1Master;
    PRINT '  ✅ ProductCategory1Master バックアップ作成完了';
END

-- ProductCategory2Masterのバックアップ
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory2Master') AND type in (N'U'))
AND NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory2Master_Backup_065') AND type in (N'U'))
BEGIN
    SELECT * INTO ProductCategory2Master_Backup_065 FROM ProductCategory2Master;
    PRINT '  ✅ ProductCategory2Master バックアップ作成完了';
END

-- ProductCategory3Masterのバックアップ
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory3Master') AND type in (N'U'))
AND NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory3Master_Backup_065') AND type in (N'U'))
BEGIN
    SELECT * INTO ProductCategory3Master_Backup_065 FROM ProductCategory3Master;
    PRINT '  ✅ ProductCategory3Master バックアップ作成完了';
END

PRINT '=== Phase 1 完了 ===';
GO

-- ===== Phase 2: ProductCategory1Master変更 =====
PRINT '--- Phase 2: ProductCategory1Master変更 ---';

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory1Master') AND type in (N'U'))
BEGIN
    -- インデックス削除（外部キー制約がある場合）
    IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('ProductCategory1Master') AND name = 'IX_ProductCategory1Master_SearchKana')
        DROP INDEX IX_ProductCategory1Master_SearchKana ON ProductCategory1Master;
    
    -- 一時カラム追加
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductCategory1Master') AND name = 'CategoryCode_New')
    BEGIN
        ALTER TABLE ProductCategory1Master ADD CategoryCode_New NVARCHAR(3) NOT NULL DEFAULT '';
        
        -- データ変換：INT → 3桁0埋めNVARCHAR
        UPDATE ProductCategory1Master 
        SET CategoryCode_New = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
        
        PRINT '  ✅ ProductCategory1Master: データ変換完了';
        
        -- 主キー制約削除
        IF EXISTS (SELECT * FROM sys.key_constraints WHERE object_id = OBJECT_ID('ProductCategory1Master') AND type = 'PK')
        BEGIN
            DECLARE @pkName1 NVARCHAR(100);
            SELECT @pkName1 = name FROM sys.key_constraints WHERE object_id = OBJECT_ID('ProductCategory1Master') AND type = 'PK';
            EXEC('ALTER TABLE ProductCategory1Master DROP CONSTRAINT ' + @pkName1);
        END
        
        -- 古いカラム削除、新カラム名変更
        ALTER TABLE ProductCategory1Master DROP COLUMN CategoryCode;
        EXEC sp_rename 'ProductCategory1Master.CategoryCode_New', 'CategoryCode', 'COLUMN';
        
        -- 主キー制約再作成
        ALTER TABLE ProductCategory1Master ADD CONSTRAINT PK_ProductCategory1Master PRIMARY KEY (CategoryCode);
        
        -- インデックス再作成
        CREATE INDEX IX_ProductCategory1Master_SearchKana ON ProductCategory1Master(SearchKana);
        
        PRINT '  ✅ ProductCategory1Master: 構造変更完了';
    END
END
ELSE
BEGIN
    PRINT '  ℹ️  ProductCategory1Master テーブルが存在しません';
END

PRINT '=== Phase 2 完了 ===';
GO

-- ===== Phase 3: ProductCategory2Master変更 =====
PRINT '--- Phase 3: ProductCategory2Master変更 ---';

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory2Master') AND type in (N'U'))
BEGIN
    -- インデックス削除
    IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('ProductCategory2Master') AND name = 'IX_ProductCategory2Master_SearchKana')
        DROP INDEX IX_ProductCategory2Master_SearchKana ON ProductCategory2Master;
    
    -- 一時カラム追加
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductCategory2Master') AND name = 'CategoryCode_New')
    BEGIN
        ALTER TABLE ProductCategory2Master ADD CategoryCode_New NVARCHAR(3) NOT NULL DEFAULT '';
        
        -- データ変換
        UPDATE ProductCategory2Master 
        SET CategoryCode_New = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
        
        PRINT '  ✅ ProductCategory2Master: データ変換完了';
        
        -- 主キー制約削除
        IF EXISTS (SELECT * FROM sys.key_constraints WHERE object_id = OBJECT_ID('ProductCategory2Master') AND type = 'PK')
        BEGIN
            DECLARE @pkName2 NVARCHAR(100);
            SELECT @pkName2 = name FROM sys.key_constraints WHERE object_id = OBJECT_ID('ProductCategory2Master') AND type = 'PK';
            EXEC('ALTER TABLE ProductCategory2Master DROP CONSTRAINT ' + @pkName2);
        END
        
        -- 古いカラム削除、新カラム名変更
        ALTER TABLE ProductCategory2Master DROP COLUMN CategoryCode;
        EXEC sp_rename 'ProductCategory2Master.CategoryCode_New', 'CategoryCode', 'COLUMN';
        
        -- 主キー制約再作成
        ALTER TABLE ProductCategory2Master ADD CONSTRAINT PK_ProductCategory2Master PRIMARY KEY (CategoryCode);
        
        -- インデックス再作成
        CREATE INDEX IX_ProductCategory2Master_SearchKana ON ProductCategory2Master(SearchKana);
        
        PRINT '  ✅ ProductCategory2Master: 構造変更完了';
    END
END
ELSE
BEGIN
    PRINT '  ℹ️  ProductCategory2Master テーブルが存在しません';
END

PRINT '=== Phase 3 完了 ===';
GO

-- ===== Phase 4: ProductCategory3Master変更 =====
PRINT '--- Phase 4: ProductCategory3Master変更 ---';

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory3Master') AND type in (N'U'))
BEGIN
    -- インデックス削除
    IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('ProductCategory3Master') AND name = 'IX_ProductCategory3Master_SearchKana')
        DROP INDEX IX_ProductCategory3Master_SearchKana ON ProductCategory3Master;
    
    -- 一時カラム追加
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductCategory3Master') AND name = 'CategoryCode_New')
    BEGIN
        ALTER TABLE ProductCategory3Master ADD CategoryCode_New NVARCHAR(3) NOT NULL DEFAULT '';
        
        -- データ変換
        UPDATE ProductCategory3Master 
        SET CategoryCode_New = RIGHT('000' + CAST(CategoryCode AS NVARCHAR), 3);
        
        PRINT '  ✅ ProductCategory3Master: データ変換完了';
        
        -- 主キー制約削除
        IF EXISTS (SELECT * FROM sys.key_constraints WHERE object_id = OBJECT_ID('ProductCategory3Master') AND type = 'PK')
        BEGIN
            DECLARE @pkName3 NVARCHAR(100);
            SELECT @pkName3 = name FROM sys.key_constraints WHERE object_id = OBJECT_ID('ProductCategory3Master') AND type = 'PK';
            EXEC('ALTER TABLE ProductCategory3Master DROP CONSTRAINT ' + @pkName3);
        END
        
        -- 古いカラム削除、新カラム名変更
        ALTER TABLE ProductCategory3Master DROP COLUMN CategoryCode;
        EXEC sp_rename 'ProductCategory3Master.CategoryCode_New', 'CategoryCode', 'COLUMN';
        
        -- 主キー制約再作成
        ALTER TABLE ProductCategory3Master ADD CONSTRAINT PK_ProductCategory3Master PRIMARY KEY (CategoryCode);
        
        -- インデックス再作成
        CREATE INDEX IX_ProductCategory3Master_SearchKana ON ProductCategory3Master(SearchKana);
        
        PRINT '  ✅ ProductCategory3Master: 構造変更完了';
    END
END
ELSE
BEGIN
    PRINT '  ℹ️  ProductCategory3Master テーブルが存在しません';
END

PRINT '=== Phase 4 完了 ===';
GO

-- ===== Phase 5: 検証とクリーンアップ =====
PRINT '--- Phase 5: 検証とクリーンアップ ---';

-- データ確認
PRINT '=== 変更後のデータ確認 ===';

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory1Master') AND type in (N'U'))
BEGIN
    DECLARE @count1 INT;
    SELECT @count1 = COUNT(*) FROM ProductCategory1Master;
    PRINT '  ProductCategory1Master: ' + CAST(@count1 AS VARCHAR) + ' 件';
    
    -- サンプルデータ表示
    IF @count1 > 0
    BEGIN
        PRINT '  サンプル:';
        SELECT TOP 3 CategoryCode, CategoryName FROM ProductCategory1Master ORDER BY CategoryCode;
    END
END

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory2Master') AND type in (N'U'))
BEGIN
    DECLARE @count2 INT;
    SELECT @count2 = COUNT(*) FROM ProductCategory2Master;
    PRINT '  ProductCategory2Master: ' + CAST(@count2 AS VARCHAR) + ' 件';
END

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'ProductCategory3Master') AND type in (N'U'))
BEGIN
    DECLARE @count3 INT;
    SELECT @count3 = COUNT(*) FROM ProductCategory3Master;
    PRINT '  ProductCategory3Master: ' + CAST(@count3 AS VARCHAR) + ' 件';
END

PRINT '=== Migration 065 完了 ===';
PRINT '⚠️  注意: バックアップテーブル（*_Backup_065）は手動で削除してください';
GO