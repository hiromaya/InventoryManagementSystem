-- 日次終了処理の誤操作防止機能用カラム追加
-- 実行日: 2025-07-01

-- DailyCloseManagementテーブルの作成（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('DailyCloseManagement') AND type = 'U')
BEGIN
    CREATE TABLE DailyCloseManagement (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProcessDate DATE NOT NULL,
        ProcessType NVARCHAR(50) NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        StartTime DATETIME2 NOT NULL,
        EndTime DATETIME2 NULL,
        RecordCount INT NULL,
        ErrorCount INT NULL,
        DatasetId NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'System',
        ErrorDetails NVARCHAR(MAX) NULL
    );
    
    -- インデックスの作成
    CREATE UNIQUE INDEX IX_DailyCloseManagement_ProcessDate_ProcessType 
    ON DailyCloseManagement(ProcessDate, ProcessType);
END
GO

-- ProcessHistoryテーブルの作成（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('ProcessHistory') AND type = 'U')
BEGIN
    CREATE TABLE ProcessHistory (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProcessType NVARCHAR(50) NOT NULL,
        JobDate DATE NOT NULL,
        DataSetId NVARCHAR(100) NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        StartTime DATETIME2 NOT NULL DEFAULT GETDATE(),
        EndTime DATETIME2 NULL,
        RecordCount INT NULL,
        ErrorCount INT NULL,
        ExecutedBy NVARCHAR(100) NOT NULL DEFAULT 'System',
        ErrorMessage NVARCHAR(MAX) NULL,
        Department NVARCHAR(20) NULL
    );
    
    -- インデックスの作成
    CREATE INDEX IX_ProcessHistory_JobDate_ProcessType 
    ON ProcessHistory(JobDate, ProcessType);
    
    CREATE INDEX IX_ProcessHistory_DataSetId 
    ON ProcessHistory(DataSetId);
END
GO

-- DailyCloseManagementテーブルにカラム追加
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('DailyCloseManagement') AND type = 'U')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailyCloseManagement') AND name = 'DataHash')
    BEGIN
        ALTER TABLE DailyCloseManagement
        ADD DataHash NVARCHAR(100) NULL;
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailyCloseManagement') AND name = 'ValidationStatus')
    BEGIN
        ALTER TABLE DailyCloseManagement
        ADD ValidationStatus NVARCHAR(20) NULL;
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailyCloseManagement') AND name = 'Remarks')
    BEGIN
        ALTER TABLE DailyCloseManagement
        ADD Remarks NVARCHAR(500) NULL;
    END
END
GO

-- ProcessHistoryテーブルにカラム追加
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('ProcessHistory') AND type = 'U')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProcessHistory') AND name = 'DataHash')
    BEGIN
        ALTER TABLE ProcessHistory
        ADD DataHash NVARCHAR(100) NULL;
    END
END
GO

-- 監査ログテーブルの作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('AuditLogs') AND type = 'U')
BEGIN
    CREATE TABLE AuditLogs (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProcessType NVARCHAR(50) NOT NULL,
        JobDate DATE NOT NULL,
        DatasetId NVARCHAR(100) NOT NULL,
        ExecutedBy NVARCHAR(100) NOT NULL,
        ExecutedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        Result NVARCHAR(20) NOT NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        Details NVARCHAR(MAX) NULL
    );
    
    -- インデックスの作成
    CREATE INDEX IX_AuditLogs_JobDate ON AuditLogs(JobDate);
    CREATE INDEX IX_AuditLogs_ProcessType ON AuditLogs(ProcessType);
    CREATE INDEX IX_AuditLogs_ExecutedAt ON AuditLogs(ExecutedAt DESC);
END
GO

-- 既存データの更新（ValidationStatusのデフォルト値設定）
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('DailyCloseManagement') AND type = 'U')
BEGIN
    UPDATE DailyCloseManagement
    SET ValidationStatus = 'PASSED'
    WHERE ValidationStatus IS NULL;
END
GO

PRINT 'Migration 005: 日次終了処理の誤操作防止機能用カラムを追加しました。';
GO