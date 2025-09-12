-- =============================================
-- UnInventoryMasterテーブルのカラムサイズ修正
-- 作成日: 2025-09-12
-- 目的: InventoryMasterテーブルとのサイズ一致
-- =============================================

USE InventoryManagementDB;
GO

PRINT '=== UnInventoryMasterテーブル修正開始 ===';
GO

-- Step 1: 一時的にデータをバックアップ（存在する場合）
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UnInventoryMaster]') AND type = 'U')
BEGIN
    IF EXISTS (SELECT 1 FROM UnInventoryMaster)
    BEGIN
        SELECT * INTO #TempUnInventory FROM UnInventoryMaster;
        TRUNCATE TABLE UnInventoryMaster;
        PRINT '既存データをバックアップしました';
    END
END
GO

-- Step 2: 主キー制約の削除
IF EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'PK_UnInventoryMaster')
BEGIN
    ALTER TABLE UnInventoryMaster DROP CONSTRAINT PK_UnInventoryMaster;
END
GO

-- Step 3: カラムサイズの修正
IF COL_LENGTH('UnInventoryMaster', 'ProductCode') IS NOT NULL
    ALTER TABLE UnInventoryMaster ALTER COLUMN ProductCode NVARCHAR(5) NOT NULL;
GO
IF COL_LENGTH('UnInventoryMaster', 'GradeCode') IS NOT NULL
    ALTER TABLE UnInventoryMaster ALTER COLUMN GradeCode NVARCHAR(3) NOT NULL;
GO
IF COL_LENGTH('UnInventoryMaster', 'ClassCode') IS NOT NULL
    ALTER TABLE UnInventoryMaster ALTER COLUMN ClassCode NVARCHAR(3) NOT NULL;
GO
IF COL_LENGTH('UnInventoryMaster', 'ShippingMarkCode') IS NOT NULL
    ALTER TABLE UnInventoryMaster ALTER COLUMN ShippingMarkCode NVARCHAR(4) NOT NULL;
GO
IF COL_LENGTH('UnInventoryMaster', 'ManualShippingMark') IS NOT NULL
    ALTER TABLE UnInventoryMaster ALTER COLUMN ManualShippingMark NVARCHAR(8) NOT NULL;
GO

-- Step 4: 主キー制約の再作成
ALTER TABLE UnInventoryMaster ADD CONSTRAINT PK_UnInventoryMaster 
PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
GO

-- Step 5: バックアップデータの復元（存在する場合）
IF OBJECT_ID('tempdb..#TempUnInventory') IS NOT NULL
BEGIN
    INSERT INTO UnInventoryMaster (
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
        ShippingMarkName, PreviousDayStock, DailyStock, DailyFlag, JobDate, CreatedDate, UpdatedDate
    )
    SELECT 
        LEFT(ProductCode, 5),
        LEFT(GradeCode, 3),
        LEFT(ClassCode, 3),
        LEFT(ShippingMarkCode, 4),
        LEFT(ManualShippingMark, 8),
        COALESCE(ShippingMarkName, ''),
        PreviousDayStock,
        DailyStock,
        DailyFlag,
        JobDate,
        CreatedDate,
        UpdatedDate
    FROM #TempUnInventory;

    DROP TABLE #TempUnInventory;
    PRINT 'データを復元しました';
END
GO

PRINT '=== UnInventoryMasterテーブル修正完了 ===';
GO

