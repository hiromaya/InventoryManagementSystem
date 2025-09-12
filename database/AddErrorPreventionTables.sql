-- ===================================================
-- 誤操作防止機能用テーブル作成スクリプト
-- ===================================================

USE InventoryManagementDB;
GO

-- ===================================================
-- 1. DatasetManagement テーブル（データセット管理）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') AND type in (N'U'))
BEGIN
    CREATE TABLE DatasetManagement (
        DatasetId NVARCHAR(50) PRIMARY KEY,
        JobDate DATE NOT NULL,
        ProcessType NVARCHAR(50) NOT NULL,
        ImportedFiles NVARCHAR(MAX), -- JSON形式
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        CreatedBy NVARCHAR(50) NOT NULL
    );
    PRINT 'DatasetManagement テーブルを作成しました';
END

-- ===================================================
-- 2. ProcessHistory テーブル（処理履歴）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE ProcessHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DatasetId NVARCHAR(50) NOT NULL,
        JobDate DATE NOT NULL,
        ProcessType NVARCHAR(50) NOT NULL,
        StartTime DATETIME2 NOT NULL,
        EndTime DATETIME2,
        Status INT NOT NULL, -- 1:Running, 2:Completed, 3:Failed, 4:Cancelled
        ErrorMessage NVARCHAR(MAX),
        ExecutedBy NVARCHAR(50) NOT NULL,
        FOREIGN KEY (DatasetId) REFERENCES DatasetManagement(DatasetId)
    );
    PRINT 'ProcessHistory テーブルを作成しました';
    
    -- インデックス作成
    CREATE INDEX IX_ProcessHistory_JobDate_ProcessType ON ProcessHistory(JobDate, ProcessType);
END

-- ===================================================
-- 3. DailyCloseManagement テーブル（日次終了管理）
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DailyCloseManagement]') AND type in (N'U'))
BEGIN
    CREATE TABLE DailyCloseManagement (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        JobDate DATE NOT NULL UNIQUE,
        DatasetId NVARCHAR(50) NOT NULL,
        DailyReportDatasetId NVARCHAR(50) NOT NULL,
        BackupPath NVARCHAR(500),
        ProcessedAt DATETIME2 NOT NULL,
        ProcessedBy NVARCHAR(50) NOT NULL
    );
    PRINT 'DailyCloseManagement テーブルを作成しました';
END

-- ===================================================
-- 4. インデックス作成
-- ===================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DatasetManagement_JobDate')
BEGIN
    CREATE INDEX IX_DatasetManagement_JobDate ON DatasetManagement(JobDate);
    PRINT 'IX_DatasetManagement_JobDate インデックスを作成しました';
END

PRINT '=== 誤操作防止機能用テーブルのセットアップが完了しました ===';
GO