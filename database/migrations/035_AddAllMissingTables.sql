-- 035_AddAllMissingTables.sql
-- すべての未実装テーブルとステージングテーブルを作成（Gemini改良版設計対応）

-- ========== 分類マスタ系 ==========

-- 1. 単位マスタ
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UnitMaster' AND xtype='U')
BEGIN
    CREATE TABLE UnitMaster (
        UnitCode INT NOT NULL PRIMARY KEY,
        UnitName NVARCHAR(50) NOT NULL,
        SearchKana NVARCHAR(50),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_UnitMaster_SearchKana ON UnitMaster(SearchKana);
    
    PRINT 'UnitMasterテーブル作成完了';
END
GO

-- 2. 商品分類マスタ（1-3）
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProductCategory1Master' AND xtype='U')
BEGIN
    CREATE TABLE ProductCategory1Master (
        CategoryCode INT NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL,
        SearchKana NVARCHAR(100),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_ProductCategory1Master_SearchKana ON ProductCategory1Master(SearchKana);
    
    PRINT 'ProductCategory1Masterテーブル作成完了';
END
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProductCategory2Master' AND xtype='U')
BEGIN
    CREATE TABLE ProductCategory2Master (
        CategoryCode INT NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL,
        SearchKana NVARCHAR(100),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_ProductCategory2Master_SearchKana ON ProductCategory2Master(SearchKana);
    
    PRINT 'ProductCategory2Masterテーブル作成完了';
END
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProductCategory3Master' AND xtype='U')
BEGIN
    CREATE TABLE ProductCategory3Master (
        CategoryCode INT NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL,
        SearchKana NVARCHAR(100),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_ProductCategory3Master_SearchKana ON ProductCategory3Master(SearchKana);
    
    PRINT 'ProductCategory3Masterテーブル作成完了';
END
GO

-- 3. 得意先分類マスタ（1-5）
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CustomerCategory1Master' AND xtype='U')
BEGIN
    CREATE TABLE CustomerCategory1Master (
        CategoryCode INT NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL,
        SearchKana NVARCHAR(100),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_CustomerCategory1Master_SearchKana ON CustomerCategory1Master(SearchKana);
    
    PRINT 'CustomerCategory1Masterテーブル作成完了';
END
GO

-- CustomerCategory2Master～CustomerCategory5Master
DECLARE @i INT = 2;
WHILE @i <= 5
BEGIN
    DECLARE @tableName NVARCHAR(100) = 'CustomerCategory' + CAST(@i AS NVARCHAR(1)) + 'Master';
    DECLARE @indexName NVARCHAR(100) = 'IX_CustomerCategory' + CAST(@i AS NVARCHAR(1)) + 'Master_SearchKana';
    
    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = @tableName AND xtype = 'U')
    BEGIN
        DECLARE @sql NVARCHAR(MAX) = '
        CREATE TABLE ' + @tableName + ' (
            CategoryCode INT NOT NULL PRIMARY KEY,
            CategoryName NVARCHAR(100) NOT NULL,
            SearchKana NVARCHAR(100),
            CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
            UpdatedAt DATETIME2
        );
        CREATE INDEX ' + @indexName + ' ON ' + @tableName + '(SearchKana);';
        
        EXEC sp_executesql @sql;
        PRINT @tableName + 'テーブル作成完了';
    END
    
    SET @i = @i + 1;
END
GO

-- 4. 仕入先分類マスタ（1-3）
DECLARE @i INT = 1;
WHILE @i <= 3
BEGIN
    DECLARE @tableName NVARCHAR(100) = 'SupplierCategory' + CAST(@i AS NVARCHAR(1)) + 'Master';
    DECLARE @indexName NVARCHAR(100) = 'IX_SupplierCategory' + CAST(@i AS NVARCHAR(1)) + 'Master_SearchKana';
    
    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = @tableName AND xtype = 'U')
    BEGIN
        DECLARE @sql NVARCHAR(MAX) = '
        CREATE TABLE ' + @tableName + ' (
            CategoryCode INT NOT NULL PRIMARY KEY,
            CategoryName NVARCHAR(100) NOT NULL,
            SearchKana NVARCHAR(100),
            CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
            UpdatedAt DATETIME2
        );
        CREATE INDEX ' + @indexName + ' ON ' + @tableName + '(SearchKana);';
        
        EXEC sp_executesql @sql;
        PRINT @tableName + 'テーブル作成完了';
    END
    
    SET @i = @i + 1;
END
GO

-- 5. 担当者マスタ
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='StaffMaster' AND xtype='U')
BEGIN
    CREATE TABLE StaffMaster (
        StaffCode INT NOT NULL PRIMARY KEY,
        StaffName NVARCHAR(100) NOT NULL,
        SearchKana NVARCHAR(100),
        Category1Code INT,
        Category2Code INT,
        Category3Code INT,
        DepartmentCode INT,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_StaffMaster_SearchKana ON StaffMaster(SearchKana);
    CREATE INDEX IX_StaffMaster_DepartmentCode ON StaffMaster(DepartmentCode);
    
    PRINT 'StaffMasterテーブル作成完了';
END
GO

-- 6. 担当者分類マスタ
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='StaffCategory1Master' AND xtype='U')
BEGIN
    CREATE TABLE StaffCategory1Master (
        CategoryCode INT NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL,
        SearchKana NVARCHAR(100),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_StaffCategory1Master_SearchKana ON StaffCategory1Master(SearchKana);
    
    PRINT 'StaffCategory1Masterテーブル作成完了';
END
GO

-- ========== 伝票系 ==========

-- 7. 入金伝票
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ReceiptVouchers' AND xtype='U')
BEGIN
    CREATE TABLE ReceiptVouchers (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        VoucherDate DATE NOT NULL,
        VoucherNumber NVARCHAR(10) NOT NULL,
        CustomerCode NVARCHAR(10) NOT NULL,
        CustomerName NVARCHAR(100),
        BillingCode NVARCHAR(10),
        JobDate DATE NOT NULL,
        LineNumber INT NOT NULL,
        PaymentType INT NOT NULL,
        OffsetCode NVARCHAR(10),
        Amount DECIMAL(18, 4) NOT NULL,
        BillDueDate DATE,
        BillNumber NVARCHAR(50),
        CorporateBankCode NVARCHAR(10),
        DepositAccountNumber INT,
        RemitterName NVARCHAR(100),
        Remarks NVARCHAR(500),
        DataSetId NVARCHAR(50),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_ReceiptVouchers_VoucherDate ON ReceiptVouchers(VoucherDate);
    CREATE INDEX IX_ReceiptVouchers_JobDate ON ReceiptVouchers(JobDate);
    CREATE INDEX IX_ReceiptVouchers_CustomerCode ON ReceiptVouchers(CustomerCode);
    CREATE INDEX IX_ReceiptVouchers_DataSetId ON ReceiptVouchers(DataSetId);
    
    PRINT 'ReceiptVouchersテーブル作成完了';
END
GO

-- 8. 支払伝票
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PaymentVouchers' AND xtype='U')
BEGIN
    CREATE TABLE PaymentVouchers (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        VoucherDate DATE NOT NULL,
        VoucherNumber NVARCHAR(10) NOT NULL,
        SupplierCode NVARCHAR(10) NOT NULL,
        SupplierName NVARCHAR(100),
        PayeeCode NVARCHAR(10),
        JobDate DATE NOT NULL,
        LineNumber INT NOT NULL,
        PaymentType INT NOT NULL,
        OffsetCode NVARCHAR(10),
        Amount DECIMAL(18, 4) NOT NULL,
        BillDueDate DATE,
        BillNumber NVARCHAR(50),
        TransferFeeBearer INT,
        CorporateBankCode NVARCHAR(10),
        TransferBankCode NVARCHAR(10),
        TransferBranchCode NVARCHAR(10),
        TransferAccountType INT,
        TransferAccountNumber NVARCHAR(20),
        TransferDesignation INT,
        Remarks NVARCHAR(500),
        DataSetId NVARCHAR(50),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2
    );
    CREATE INDEX IX_PaymentVouchers_VoucherDate ON PaymentVouchers(VoucherDate);
    CREATE INDEX IX_PaymentVouchers_JobDate ON PaymentVouchers(JobDate);
    CREATE INDEX IX_PaymentVouchers_SupplierCode ON PaymentVouchers(SupplierCode);
    CREATE INDEX IX_PaymentVouchers_DataSetId ON PaymentVouchers(DataSetId);
    
    PRINT 'PaymentVouchersテーブル作成完了';
END
GO

-- ========== ステージングテーブル領域作成 ==========

-- ステージング専用スキーマが存在しない場合は作成
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Staging')
BEGIN
    EXEC('CREATE SCHEMA Staging');
    PRINT 'Stagingスキーマ作成完了';
END
GO

-- ========== 汎用エラーテーブル ==========

-- インポートエラー記録用テーブル
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ImportErrors' AND xtype='U')
BEGIN
    CREATE TABLE ImportErrors (
        ErrorId BIGINT IDENTITY(1,1) PRIMARY KEY,
        ImportTimestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
        FileName NVARCHAR(255) NOT NULL,
        TableName NVARCHAR(100),
        RowNumber INT NOT NULL,
        ColumnName NVARCHAR(100),
        ErrorMessage NVARCHAR(MAX) NOT NULL,
        CsvRowData NVARCHAR(MAX),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_ImportErrors_FileName ON ImportErrors(FileName);
    CREATE INDEX IX_ImportErrors_TableName ON ImportErrors(TableName);
    CREATE INDEX IX_ImportErrors_ImportTimestamp ON ImportErrors(ImportTimestamp);
    
    PRINT 'ImportErrorsテーブル作成完了';
END
GO

-- ========== ステージングテーブル作成 ==========

-- 各種マスタ用ステージングテーブル（汎用フォーマット）
DECLARE @stagingTables TABLE (
    TableName NVARCHAR(100),
    DisplayName NVARCHAR(100)
);

INSERT INTO @stagingTables VALUES
    ('Staging.UnitMaster', '単位マスタ'),
    ('Staging.ProductCategory1Master', '商品分類1マスタ'),
    ('Staging.ProductCategory2Master', '商品分類2マスタ'),
    ('Staging.ProductCategory3Master', '商品分類3マスタ'),
    ('Staging.CustomerCategory1Master', '得意先分類1マスタ'),
    ('Staging.CustomerCategory2Master', '得意先分類2マスタ'),
    ('Staging.CustomerCategory3Master', '得意先分類3マスタ'),
    ('Staging.CustomerCategory4Master', '得意先分類4マスタ'),
    ('Staging.CustomerCategory5Master', '得意先分類5マスタ'),
    ('Staging.SupplierCategory1Master', '仕入先分類1マスタ'),
    ('Staging.SupplierCategory2Master', '仕入先分類2マスタ'),
    ('Staging.SupplierCategory3Master', '仕入先分類3マスタ'),
    ('Staging.StaffCategory1Master', '担当者分類1マスタ');

DECLARE @tableName NVARCHAR(100), @displayName NVARCHAR(100);
DECLARE staging_cursor CURSOR FOR SELECT TableName, DisplayName FROM @stagingTables;

OPEN staging_cursor;
FETCH NEXT FROM staging_cursor INTO @tableName, @displayName;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(@tableName) AND type = 'U')
    BEGIN
        DECLARE @createSql NVARCHAR(MAX) = '
        CREATE TABLE ' + @tableName + ' (
            _FileName NVARCHAR(255) NOT NULL,
            _RowNumber INT NOT NULL,
            _ImportTimestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
            Code NVARCHAR(MAX),
            Name NVARCHAR(MAX),
            SearchKana NVARCHAR(MAX)
        );
        CREATE INDEX IX_' + REPLACE(@tableName, '.', '_') + '_FileName ON ' + @tableName + '(_FileName);
        CREATE INDEX IX_' + REPLACE(@tableName, '.', '_') + '_RowNumber ON ' + @tableName + '(_RowNumber);';
        
        EXEC sp_executesql @createSql;
        PRINT @displayName + 'ステージングテーブル作成完了: ' + @tableName;
    END
    
    FETCH NEXT FROM staging_cursor INTO @tableName, @displayName;
END

CLOSE staging_cursor;
DEALLOCATE staging_cursor;
GO

-- 担当者マスタ用ステージングテーブル（専用フォーマット）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('Staging.StaffMaster') AND type = 'U')
BEGIN
    CREATE TABLE Staging.StaffMaster (
        _FileName NVARCHAR(255) NOT NULL,
        _RowNumber INT NOT NULL,
        _ImportTimestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
        Code NVARCHAR(MAX),
        Name NVARCHAR(MAX),
        SearchKana NVARCHAR(MAX),
        Category1Code NVARCHAR(MAX),
        Category2Code NVARCHAR(MAX),
        Category3Code NVARCHAR(MAX),
        DepartmentCode NVARCHAR(MAX)
    );
    CREATE INDEX IX_Staging_StaffMaster_FileName ON Staging.StaffMaster(_FileName);
    CREATE INDEX IX_Staging_StaffMaster_RowNumber ON Staging.StaffMaster(_RowNumber);
    
    PRINT '担当者マスタステージングテーブル作成完了: Staging.StaffMaster';
END
GO

-- 入金伝票・支払伝票用ステージングテーブル
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('Staging.ReceiptVouchers') AND type = 'U')
BEGIN
    CREATE TABLE Staging.ReceiptVouchers (
        _FileName NVARCHAR(255) NOT NULL,
        _RowNumber INT NOT NULL,
        _ImportTimestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
        VoucherDate NVARCHAR(MAX),
        VoucherNumber NVARCHAR(MAX),
        CustomerCode NVARCHAR(MAX),
        CustomerName NVARCHAR(MAX),
        BillingCode NVARCHAR(MAX),
        JobDate NVARCHAR(MAX),
        LineNumber NVARCHAR(MAX),
        PaymentType NVARCHAR(MAX),
        OffsetCode NVARCHAR(MAX),
        Amount NVARCHAR(MAX),
        BillDueDate NVARCHAR(MAX),
        BillNumber NVARCHAR(MAX),
        CorporateBankCode NVARCHAR(MAX),
        DepositAccountNumber NVARCHAR(MAX),
        RemitterName NVARCHAR(MAX),
        Remarks NVARCHAR(MAX)
    );
    CREATE INDEX IX_Staging_ReceiptVouchers_FileName ON Staging.ReceiptVouchers(_FileName);
    CREATE INDEX IX_Staging_ReceiptVouchers_RowNumber ON Staging.ReceiptVouchers(_RowNumber);
    
    PRINT '入金伝票ステージングテーブル作成完了: Staging.ReceiptVouchers';
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('Staging.PaymentVouchers') AND type = 'U')
BEGIN
    CREATE TABLE Staging.PaymentVouchers (
        _FileName NVARCHAR(255) NOT NULL,
        _RowNumber INT NOT NULL,
        _ImportTimestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
        VoucherDate NVARCHAR(MAX),
        VoucherNumber NVARCHAR(MAX),
        SupplierCode NVARCHAR(MAX),
        SupplierName NVARCHAR(MAX),
        PayeeCode NVARCHAR(MAX),
        JobDate NVARCHAR(MAX),
        LineNumber NVARCHAR(MAX),
        PaymentType NVARCHAR(MAX),
        OffsetCode NVARCHAR(MAX),
        Amount NVARCHAR(MAX),
        BillDueDate NVARCHAR(MAX),
        BillNumber NVARCHAR(MAX),
        TransferFeeBearer NVARCHAR(MAX),
        CorporateBankCode NVARCHAR(MAX),
        TransferBankCode NVARCHAR(MAX),
        TransferBranchCode NVARCHAR(MAX),
        TransferAccountType NVARCHAR(MAX),
        TransferAccountNumber NVARCHAR(MAX),
        TransferDesignation NVARCHAR(MAX),
        Remarks NVARCHAR(MAX)
    );
    CREATE INDEX IX_Staging_PaymentVouchers_FileName ON Staging.PaymentVouchers(_FileName);
    CREATE INDEX IX_Staging_PaymentVouchers_RowNumber ON Staging.PaymentVouchers(_RowNumber);
    
    PRINT '支払伝票ステージングテーブル作成完了: Staging.PaymentVouchers';
END
GO

PRINT '========== 035_AddAllMissingTables.sql 実行完了 ==========';
PRINT 'すべての未実装テーブルとステージングテーブルが作成されました。';