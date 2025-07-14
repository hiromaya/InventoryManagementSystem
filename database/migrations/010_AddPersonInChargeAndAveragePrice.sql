-- =============================================
-- InventoryMasterテーブルにPersonInChargeCodeとAveragePriceを追加
-- 作成日: 2025-07-14
-- 説明: 初期在庫インポート機能で必要なカラムを追加
-- =============================================

USE InventoryManagementDB;
GO

-- PersonInChargeCodeカラムの追加（存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.InventoryMaster') 
    AND name = 'PersonInChargeCode'
)
BEGIN
    ALTER TABLE dbo.InventoryMaster
    ADD PersonInChargeCode INT NOT NULL DEFAULT 0;
    
    PRINT 'PersonInChargeCodeカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'PersonInChargeCodeカラムは既に存在します。';
END
GO

-- AveragePriceカラムの追加（存在しない場合のみ）
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.InventoryMaster') 
    AND name = 'AveragePrice'
)
BEGIN
    ALTER TABLE dbo.InventoryMaster
    ADD AveragePrice DECIMAL(18,4) NOT NULL DEFAULT 0;
    
    PRINT 'AveragePriceカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'AveragePriceカラムは既に存在します。';
END
GO

-- インデックスの作成（PersonInChargeCodeでのフィルタリング用）
IF NOT EXISTS (
    SELECT * 
    FROM sys.indexes 
    WHERE object_id = OBJECT_ID('dbo.InventoryMaster') 
    AND name = 'IX_InventoryMaster_PersonInChargeCode'
)
BEGIN
    CREATE INDEX IX_InventoryMaster_PersonInChargeCode 
    ON dbo.InventoryMaster (PersonInChargeCode)
    INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);
    
    PRINT 'IX_InventoryMaster_PersonInChargeCodeインデックスを作成しました。';
END
GO

PRINT '010_AddPersonInChargeAndAveragePrice.sql の実行が完了しました。';