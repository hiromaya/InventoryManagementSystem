-- 113: InventoryCarryoverMaster に IsActive カラムを追加し、インデックスを作成
USE InventoryManagementDB;
GO

-- カラム追加（存在しない場合のみ）
IF COL_LENGTH('dbo.InventoryCarryoverMaster', 'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryCarryoverMaster
        ADD IsActive BIT NOT NULL DEFAULT 0;
    PRINT 'InventoryCarryoverMaster.IsActive を追加しました (DEFAULT 0)';
END
ELSE
BEGIN
    PRINT 'InventoryCarryoverMaster.IsActive は既に存在します';
END
GO

-- インデックス（JobDate, IsActive）作成（存在しない場合）
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE name = 'IX_Carryover_JobDate_IsActive' 
      AND object_id = OBJECT_ID('dbo.InventoryCarryoverMaster')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Carryover_JobDate_IsActive
    ON dbo.InventoryCarryoverMaster(JobDate, IsActive)
    WHERE IsActive = 1;
    PRINT 'IX_Carryover_JobDate_IsActive を作成しました';
END
ELSE
BEGIN
    PRINT 'IX_Carryover_JobDate_IsActive は既に存在します';
END
GO

-- 初回アクティブ設定: まだIsActive=1が存在せず、データがある場合は最大DataSetIdのみをIsActive=1に設定
IF NOT EXISTS (SELECT 1 FROM dbo.InventoryCarryoverMaster WHERE IsActive = 1)
   AND EXISTS (SELECT 1 FROM dbo.InventoryCarryoverMaster)
BEGIN
    DECLARE @MaxDataSetId NVARCHAR(50);
    SELECT TOP 1 @MaxDataSetId = DataSetId FROM dbo.InventoryCarryoverMaster ORDER BY DataSetId DESC;
    UPDATE dbo.InventoryCarryoverMaster SET IsActive = CASE WHEN DataSetId = @MaxDataSetId THEN 1 ELSE 0 END;
    PRINT '初回アクティブ設定: 最新のDataSetIdをIsActive=1に設定しました (' + @MaxDataSetId + ')';
END
ELSE
BEGIN
    PRINT '初回アクティブ設定: 変更なし（既にIsActive=1のデータが存在するか、データがありません）';
END
GO
