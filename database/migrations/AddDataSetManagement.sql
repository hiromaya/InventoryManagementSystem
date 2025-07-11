-- InventoryMasterテーブルの拡張
ALTER TABLE InventoryMaster ADD
    IsActive BIT NOT NULL DEFAULT 1,
    ParentDataSetId NVARCHAR(50) NULL,
    ImportType NVARCHAR(20) NOT NULL DEFAULT 'UNKNOWN',
    CreatedBy NVARCHAR(50) NULL,
    CONSTRAINT CK_ImportType CHECK (ImportType IN ('INIT', 'IMPORT', 'CARRYOVER', 'MANUAL', 'UNKNOWN'));

-- DataSetIdとIsActiveの複合インデックス
CREATE INDEX IX_InventoryMaster_DataSetId_IsActive 
ON InventoryMaster(DataSetId, IsActive) 
INCLUDE (JobDate, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);

-- DataSet管理テーブル
CREATE TABLE DataSetManagement (
    DataSetId NVARCHAR(50) PRIMARY KEY,
    JobDate DATE NOT NULL,
    ImportType NVARCHAR(20) NOT NULL,
    RecordCount INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    ParentDataSetId NVARCHAR(50) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(50) NULL,
    DeactivatedAt DATETIME NULL,
    DeactivatedBy NVARCHAR(50) NULL,
    Notes NVARCHAR(500) NULL
);