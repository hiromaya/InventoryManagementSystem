-- =============================================
-- CpInventoryMasterにGradeName/ClassNameカラムを追加
-- SE3: 商品勘定・在庫表担当
-- 作成日: 2025-08-25
-- =============================================

USE InventoryManagementDB;
GO

-- CpInventoryMasterテーブルにGradeName/ClassNameカラムを追加
IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CpInventoryMaster' 
    AND COLUMN_NAME = 'GradeName'
)
BEGIN
    ALTER TABLE CpInventoryMaster
    ADD GradeName NVARCHAR(50) NOT NULL DEFAULT '';
    
    PRINT 'CpInventoryMasterにGradeNameカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'CpInventoryMaster.GradeNameカラムは既に存在します。';
END
GO

IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CpInventoryMaster' 
    AND COLUMN_NAME = 'ClassName'
)
BEGIN
    ALTER TABLE CpInventoryMaster
    ADD ClassName NVARCHAR(50) NOT NULL DEFAULT '';
    
    PRINT 'CpInventoryMasterにClassNameカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'CpInventoryMaster.ClassNameカラムは既に存在します。';
END
GO

-- インデックス追加（検索性能向上）
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('CpInventoryMaster') 
    AND name = 'IX_CpInventoryMaster_GradeName'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CpInventoryMaster_GradeName
    ON CpInventoryMaster (GradeName);
    
    PRINT 'CpInventoryMaster.GradeNameインデックスを作成しました。';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('CpInventoryMaster') 
    AND name = 'IX_CpInventoryMaster_ClassName'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CpInventoryMaster_ClassName
    ON CpInventoryMaster (ClassName);
    
    PRINT 'CpInventoryMaster.ClassNameインデックスを作成しました。';
END
GO

PRINT '061_AddGradeClassNamesToCpInventoryMaster.sql実行完了';
GO