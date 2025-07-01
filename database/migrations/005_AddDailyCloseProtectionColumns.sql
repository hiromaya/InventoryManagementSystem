-- 日次終了処理の誤操作防止機能用カラム追加
-- 実行日: 2025-07-01

-- DailyCloseManagementテーブルにカラム追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailyCloseManagement') AND name = 'DataHash')
BEGIN
    ALTER TABLE DailyCloseManagement
    ADD DataHash NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailyCloseManagement') AND name = 'ValidationStatus')
BEGIN
    ALTER TABLE DailyCloseManagement
    ADD ValidationStatus NVARCHAR(20) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailyCloseManagement') AND name = 'Remarks')
BEGIN
    ALTER TABLE DailyCloseManagement
    ADD Remarks NVARCHAR(500) NULL;
END
GO

-- ProcessHistoryテーブルにカラム追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProcessHistory') AND name = 'DataHash')
BEGIN
    ALTER TABLE ProcessHistory
    ADD DataHash NVARCHAR(100) NULL;
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
UPDATE DailyCloseManagement
SET ValidationStatus = 'PASSED'
WHERE ValidationStatus IS NULL;
GO

PRINT 'Migration 005: 日次終了処理の誤操作防止機能用カラムを追加しました。';
GO