-- Windows環境用データベースセットアップスクリプト
-- SQL Server Management Studio または sqlcmd で実行してください

-- データベース作成
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'InventoryManagement')
BEGIN
    CREATE DATABASE InventoryManagement;
END
GO

USE InventoryManagement;
GO

-- DataSetsテーブル作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND type in (N'U'))
BEGIN
    CREATE TABLE DataSets (
        Id NVARCHAR(100) NOT NULL PRIMARY KEY,
        DataSetType NVARCHAR(50) NOT NULL,
        ImportedAt DATETIME2 NOT NULL,
        RecordCount INT NOT NULL DEFAULT 0,
        Status NVARCHAR(50) NOT NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        FilePath NVARCHAR(500) NULL,
        JobDate DATE NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    
    CREATE INDEX IX_DataSets_JobDate ON DataSets(JobDate);
    CREATE INDEX IX_DataSets_Status ON DataSets(Status);
    CREATE INDEX IX_DataSets_DataSetType ON DataSets(DataSetType);
    
    PRINT 'DataSetsテーブルを作成しました。';
END
ELSE
BEGIN
    PRINT 'DataSetsテーブルは既に存在します。';
END
GO

-- SalesVouchersテーブル作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SalesVouchers]') AND type in (N'U'))
BEGIN
    CREATE TABLE SalesVouchers (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DataSetId NVARCHAR(100) NOT NULL,
        VoucherNumber NVARCHAR(50) NOT NULL,
        VoucherDate DATE NOT NULL,
        JobDate DATE NOT NULL,
        VoucherType NVARCHAR(10) NOT NULL,
        DetailType NVARCHAR(10) NOT NULL,
        CustomerCode NVARCHAR(20) NULL,
        CustomerName NVARCHAR(100) NULL,
        ProductCode NVARCHAR(15) NOT NULL,
        ProductName NVARCHAR(100) NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ManualShippingMark NVARCHAR(50) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        ProductCategory1 NVARCHAR(10) NULL,
        ProductCategory2 NVARCHAR(10) NULL,
        ProductCategory3 NVARCHAR(10) NULL,
        GrossProfit DECIMAL(18,2) NULL,
        IsExcluded BIT NOT NULL DEFAULT 0,
        ExcludeReason NVARCHAR(100) NULL,
        ImportedAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL
    );
    
    -- インデックス作成
    CREATE INDEX IX_SalesVouchers_DataSetId ON SalesVouchers(DataSetId);
    CREATE INDEX IX_SalesVouchers_JobDate ON SalesVouchers(JobDate);
    CREATE INDEX IX_SalesVouchers_VoucherNumber ON SalesVouchers(VoucherNumber);
    CREATE INDEX IX_SalesVouchers_InventoryKey ON SalesVouchers(ProductCode, GradeCode, ClassCode, ShippingMarkCode);
    
    -- 外部キー制約
    ALTER TABLE SalesVouchers ADD CONSTRAINT FK_SalesVouchers_DataSets 
        FOREIGN KEY (DataSetId) REFERENCES DataSets(Id);
    
    PRINT 'SalesVouchersテーブルを作成しました。';
END
ELSE
BEGIN
    PRINT 'SalesVouchersテーブルは既に存在します。';
END
GO

-- PurchaseVouchersテーブル作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseVouchers]') AND type in (N'U'))
BEGIN
    CREATE TABLE PurchaseVouchers (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DataSetId NVARCHAR(100) NOT NULL,
        VoucherNumber NVARCHAR(50) NOT NULL,
        VoucherDate DATE NOT NULL,
        JobDate DATE NOT NULL,
        VoucherType NVARCHAR(10) NOT NULL,
        DetailType NVARCHAR(10) NOT NULL,
        SupplierCode NVARCHAR(20) NULL,
        SupplierName NVARCHAR(100) NULL,
        ProductCode NVARCHAR(15) NOT NULL,
        ProductName NVARCHAR(100) NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ManualShippingMark NVARCHAR(50) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        ProductCategory1 NVARCHAR(10) NULL,
        ProductCategory2 NVARCHAR(10) NULL,
        ProductCategory3 NVARCHAR(10) NULL,
        IsExcluded BIT NOT NULL DEFAULT 0,
        ExcludeReason NVARCHAR(100) NULL,
        ImportedAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL
    );
    
    -- インデックス作成
    CREATE INDEX IX_PurchaseVouchers_DataSetId ON PurchaseVouchers(DataSetId);
    CREATE INDEX IX_PurchaseVouchers_JobDate ON PurchaseVouchers(JobDate);
    CREATE INDEX IX_PurchaseVouchers_VoucherNumber ON PurchaseVouchers(VoucherNumber);
    CREATE INDEX IX_PurchaseVouchers_InventoryKey ON PurchaseVouchers(ProductCode, GradeCode, ClassCode, ShippingMarkCode);
    
    -- 外部キー制約
    ALTER TABLE PurchaseVouchers ADD CONSTRAINT FK_PurchaseVouchers_DataSets 
        FOREIGN KEY (DataSetId) REFERENCES DataSets(Id);
    
    PRINT 'PurchaseVouchersテーブルを作成しました。';
END
ELSE
BEGIN
    PRINT 'PurchaseVouchersテーブルは既に存在します。';
END
GO

-- InventoryAdjustmentsテーブル作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventoryAdjustments]') AND type in (N'U'))
BEGIN
    CREATE TABLE InventoryAdjustments (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DataSetId NVARCHAR(100) NOT NULL,
        VoucherNumber NVARCHAR(50) NOT NULL,
        VoucherDate DATE NOT NULL,
        JobDate DATE NOT NULL,
        VoucherType NVARCHAR(10) NOT NULL,
        DetailType NVARCHAR(10) NOT NULL,
        UnitCode NVARCHAR(10) NULL,
        ProductCode NVARCHAR(15) NOT NULL,
        ProductName NVARCHAR(100) NULL,
        GradeCode NVARCHAR(15) NOT NULL,
        ClassCode NVARCHAR(15) NOT NULL,
        ShippingMarkCode NVARCHAR(15) NOT NULL,
        ManualShippingMark NVARCHAR(50) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        ProductCategory1 NVARCHAR(10) NULL,
        ProductCategory2 NVARCHAR(10) NULL,
        ProductCategory3 NVARCHAR(10) NULL,
        IsExcluded BIT NOT NULL DEFAULT 0,
        ExcludeReason NVARCHAR(100) NULL,
        ImportedAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL
    );
    
    -- インデックス作成
    CREATE INDEX IX_InventoryAdjustments_DataSetId ON InventoryAdjustments(DataSetId);
    CREATE INDEX IX_InventoryAdjustments_JobDate ON InventoryAdjustments(JobDate);
    CREATE INDEX IX_InventoryAdjustments_VoucherNumber ON InventoryAdjustments(VoucherNumber);
    CREATE INDEX IX_InventoryAdjustments_InventoryKey ON InventoryAdjustments(ProductCode, GradeCode, ClassCode, ShippingMarkCode);
    
    -- 外部キー制約
    ALTER TABLE InventoryAdjustments ADD CONSTRAINT FK_InventoryAdjustments_DataSets 
        FOREIGN KEY (DataSetId) REFERENCES DataSets(Id);
    
    PRINT 'InventoryAdjustmentsテーブルを作成しました。';
END
ELSE
BEGIN
    PRINT 'InventoryAdjustmentsテーブルは既に存在します。';
END
GO

PRINT '=== データベースセットアップ完了 ===';
PRINT 'データベース名: InventoryManagement';
PRINT '作成されたテーブル:';
PRINT '- DataSets (データセット管理)';
PRINT '- SalesVouchers (売上伝票)';
PRINT '- PurchaseVouchers (仕入伝票)';
PRINT '- InventoryAdjustments (在庫調整)';
GO