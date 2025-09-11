-- 部門別管理機能のための部門コードカラム追加
-- 実行日: 2025-06-24

-- 部門コードカラムの追加
IF COL_LENGTH('DataSets', 'DepartmentCode') IS NULL
    ALTER TABLE DataSets ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
GO
IF COL_LENGTH('SalesVouchers', 'DepartmentCode') IS NULL
    ALTER TABLE SalesVouchers ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
GO
IF COL_LENGTH('PurchaseVouchers', 'DepartmentCode') IS NULL
    ALTER TABLE PurchaseVouchers ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
GO
IF COL_LENGTH('InventoryAdjustments', 'DepartmentCode') IS NULL
    ALTER TABLE InventoryAdjustments ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
GO
IF COL_LENGTH('CpInventoryMaster', 'DepartmentCode') IS NULL
    ALTER TABLE CpInventoryMaster ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
GO

-- インデックスの追加（検索性能向上）
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DataSets_DepartmentCode' AND object_id = OBJECT_ID('DataSets'))
    CREATE INDEX IX_DataSets_DepartmentCode ON DataSets(DepartmentCode);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesVouchers_DepartmentCode' AND object_id = OBJECT_ID('SalesVouchers'))
    CREATE INDEX IX_SalesVouchers_DepartmentCode ON SalesVouchers(DepartmentCode);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseVouchers_DepartmentCode' AND object_id = OBJECT_ID('PurchaseVouchers'))
    CREATE INDEX IX_PurchaseVouchers_DepartmentCode ON PurchaseVouchers(DepartmentCode);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryAdjustments_DepartmentCode' AND object_id = OBJECT_ID('InventoryAdjustments'))
    CREATE INDEX IX_InventoryAdjustments_DepartmentCode ON InventoryAdjustments(DepartmentCode);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CpInventoryMaster_DepartmentCode' AND object_id = OBJECT_ID('CpInventoryMaster'))
    CREATE INDEX IX_CpInventoryMaster_DepartmentCode ON CpInventoryMaster(DepartmentCode);
GO

-- 複合インデックス（部門とジョブデートでの検索用）
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DataSets_DepartmentCode_JobDate' AND object_id = OBJECT_ID('DataSets'))
    CREATE INDEX IX_DataSets_DepartmentCode_JobDate ON DataSets(DepartmentCode, JobDate);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesVouchers_DepartmentCode_JobDate' AND object_id = OBJECT_ID('SalesVouchers'))
    CREATE INDEX IX_SalesVouchers_DepartmentCode_JobDate ON SalesVouchers(DepartmentCode, JobDate);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseVouchers_DepartmentCode_JobDate' AND object_id = OBJECT_ID('PurchaseVouchers'))
    CREATE INDEX IX_PurchaseVouchers_DepartmentCode_JobDate ON PurchaseVouchers(DepartmentCode, JobDate);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryAdjustments_DepartmentCode_JobDate' AND object_id = OBJECT_ID('InventoryAdjustments'))
    CREATE INDEX IX_InventoryAdjustments_DepartmentCode_JobDate ON InventoryAdjustments(DepartmentCode, JobDate);
GO

-- 部門マスタテーブルの作成（将来の拡張用）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DepartmentMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE DepartmentMaster (
        DepartmentCode NVARCHAR(10) NOT NULL PRIMARY KEY,
        DepartmentName NVARCHAR(50) NOT NULL,
        DisplayName NVARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );

    -- 初期データの投入
    INSERT INTO DepartmentMaster (DepartmentCode, DepartmentName, DisplayName, IsActive)
    VALUES 
        ('DeptA', '部門A', '部門A', 1),
        ('DeptB', '部門B', '部門B', 0),
        ('DeptC', '部門C', '部門C', 0);
END
