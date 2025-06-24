-- 部門別管理機能のための部門コードカラム追加
-- 実行日: 2025-06-24

-- 部門コードカラムの追加
ALTER TABLE DataSets ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
ALTER TABLE SalesVouchers ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
ALTER TABLE PurchaseVouchers ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
ALTER TABLE InventoryAdjustments ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';
ALTER TABLE CpInventoryMaster ADD DepartmentCode NVARCHAR(10) NOT NULL DEFAULT 'DeptA';

-- インデックスの追加（検索性能向上）
CREATE INDEX IX_DataSets_DepartmentCode ON DataSets(DepartmentCode);
CREATE INDEX IX_SalesVouchers_DepartmentCode ON SalesVouchers(DepartmentCode);
CREATE INDEX IX_PurchaseVouchers_DepartmentCode ON PurchaseVouchers(DepartmentCode);
CREATE INDEX IX_InventoryAdjustments_DepartmentCode ON InventoryAdjustments(DepartmentCode);
CREATE INDEX IX_CpInventoryMaster_DepartmentCode ON CpInventoryMaster(DepartmentCode);

-- 複合インデックス（部門とジョブデートでの検索用）
CREATE INDEX IX_DataSets_DepartmentCode_JobDate ON DataSets(DepartmentCode, JobDate);
CREATE INDEX IX_SalesVouchers_DepartmentCode_JobDate ON SalesVouchers(DepartmentCode, JobDate);
CREATE INDEX IX_PurchaseVouchers_DepartmentCode_JobDate ON PurchaseVouchers(DepartmentCode, JobDate);
CREATE INDEX IX_InventoryAdjustments_DepartmentCode_JobDate ON InventoryAdjustments(DepartmentCode, JobDate);

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