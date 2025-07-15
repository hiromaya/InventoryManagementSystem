-- DepartmentCodeカラムのサイズを拡張
-- 実行日: 2025-07-15
-- 説明: DepartmentCodeカラムが存在しない場合は追加、存在する場合はサイズ拡張

USE InventoryManagementDB;
GO

-- CpInventoryMasterテーブルの存在確認
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CpInventoryMaster')
BEGIN
    PRINT 'CpInventoryMasterテーブルが存在しません。処理をスキップします。';
    RETURN;
END

-- DepartmentCodeカラムの存在確認と処理
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('CpInventoryMaster') 
    AND name = 'DepartmentCode'
)
BEGIN
    -- カラムが存在しない場合は新規追加
    PRINT 'DepartmentCodeカラムが存在しないため、新規追加します。';
    ALTER TABLE CpInventoryMaster 
    ADD DepartmentCode NVARCHAR(50) NULL;
    PRINT 'DepartmentCodeカラムを追加しました。';
END
ELSE
BEGIN
    -- カラムが存在する場合はサイズ確認と変更
    DECLARE @CurrentSize INT;
    SELECT @CurrentSize = CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'CpInventoryMaster'
    AND COLUMN_NAME = 'DepartmentCode';
    
    IF @CurrentSize < 50
    BEGIN
        PRINT CONCAT('DepartmentCodeカラムのサイズを ', @CurrentSize, ' から 50 に拡張します。');
        ALTER TABLE CpInventoryMaster 
        ALTER COLUMN DepartmentCode NVARCHAR(50);
        PRINT 'DepartmentCodeカラムのサイズを拡張しました。';
    END
    ELSE
    BEGIN
        PRINT CONCAT('DepartmentCodeカラムは既に十分なサイズ（', @CurrentSize, '）です。');
    END
END
GO

-- 更新結果の確認
PRINT '';
PRINT '===== CpInventoryMasterテーブルのDepartmentCodeカラム情報 =====';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'CpInventoryMaster'
AND COLUMN_NAME = 'DepartmentCode';

-- 他の関連テーブルも確認（存在する場合）
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InventoryMaster')
BEGIN
    -- InventoryMasterテーブルにも同様の処理
    IF NOT EXISTS (
        SELECT * 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('InventoryMaster') 
        AND name = 'DepartmentCode'
    )
    BEGIN
        PRINT '';
        PRINT 'InventoryMasterテーブルにもDepartmentCodeカラムを追加します。';
        ALTER TABLE InventoryMaster 
        ADD DepartmentCode NVARCHAR(50) NULL;
        PRINT 'InventoryMasterテーブルにDepartmentCodeカラムを追加しました。';
    END
END

PRINT '';
PRINT 'Migration 019: DepartmentCodeカラムの処理が完了しました。';
GO