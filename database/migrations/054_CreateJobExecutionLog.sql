-- JobExecutionLogテーブル作成
-- DataSetIdの一元管理のため、JobDateとJobTypeに基づく実行ログを管理

-- テーブルが存在する場合は削除
IF OBJECT_ID('dbo.JobExecutionLog', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.JobExecutionLog;
    PRINT 'JobExecutionLogテーブルを削除しました。';
END

-- JobExecutionLogテーブル作成
CREATE TABLE dbo.JobExecutionLog (
    JobExecutionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    JobDate DATE NOT NULL,
    JobType NVARCHAR(100) NOT NULL,
    DataSetId NVARCHAR(100) NOT NULL, -- GUIDを文字列として格納
    CreatedAt DATETIME2(7) NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
    
    -- 同一JobDateとJobTypeの組み合わせは一意
    CONSTRAINT UK_JobExecutionLog_JobDate_JobType UNIQUE (JobDate, JobType),
    
    -- DataSetIdの一意性も保証
    CONSTRAINT UK_JobExecutionLog_DataSetId UNIQUE (DataSetId)
);

-- インデックス作成
CREATE INDEX IX_JobExecutionLog_JobDate ON dbo.JobExecutionLog (JobDate);
CREATE INDEX IX_JobExecutionLog_JobType ON dbo.JobExecutionLog (JobType);
CREATE INDEX IX_JobExecutionLog_CreatedAt ON dbo.JobExecutionLog (CreatedAt);

-- JobTypeの値を制限（オプション）
ALTER TABLE dbo.JobExecutionLog 
ADD CONSTRAINT CK_JobExecutionLog_JobType 
CHECK (JobType IN (
    'SalesVoucher', 
    'PurchaseVoucher', 
    'InventoryAdjustment',
    'CpInventoryMaster',
    'DailyReport',
    'UnmatchList',
    'ProductMaster',
    'CustomerMaster',
    'SupplierMaster',
    'MasterImport'
));

PRINT 'JobExecutionLogテーブルを作成しました。';

-- サンプルデータの挿入（テスト用）
-- INSERT INTO dbo.JobExecutionLog (JobDate, JobType, DataSetId)
-- VALUES 
--     ('2025-06-02', 'SalesVoucher', '36b121e7-d2ef-433a-bed3-b994447b66a0'),
--     ('2025-06-02', 'CpInventoryMaster', 'DS_20250602_230219_DAILY_REPORT');

PRINT 'JobExecutionLogテーブルの作成が完了しました。';