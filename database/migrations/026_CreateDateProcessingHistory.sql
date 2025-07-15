-- =============================================
-- DateProcessingHistory テーブル作成
-- 作成日: 2025-07-15
-- 説明: 日付別の処理履歴を管理するテーブル
-- =============================================

USE InventoryManagementDB;
GO

-- テーブルが存在しない場合のみ作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DateProcessingHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE DateProcessingHistory (
        -- 自動採番ID
        Id INT IDENTITY(1,1) PRIMARY KEY,
        
        -- FileProcessingHistoryとの関連
        FileHistoryId INT NOT NULL,
        
        -- 処理情報
        JobDate DATE NOT NULL,
        ProcessType NVARCHAR(50) NOT NULL,
        ProcessedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        RecordCount INT NOT NULL DEFAULT 0,
        DataSetId NVARCHAR(100),
        Department NVARCHAR(50) NOT NULL DEFAULT 'DeptA',
        
        -- 実行情報
        ExecutedBy NVARCHAR(50) NOT NULL DEFAULT SYSTEM_USER,
        ProcessingDuration INT, -- 処理時間（秒）
        
        -- ステータス
        Status NVARCHAR(20) NOT NULL DEFAULT 'Completed',
        ErrorMessage NVARCHAR(MAX) NULL,
        
        -- 外部キー制約
        CONSTRAINT FK_DateProcessingHistory_FileProcessingHistory 
        FOREIGN KEY (FileHistoryId) REFERENCES FileProcessingHistory(Id)
    );
    
    PRINT 'DateProcessingHistory テーブルを作成しました。';
END
ELSE
BEGIN
    PRINT 'DateProcessingHistory テーブルは既に存在します。';
END
GO

-- インデックス作成
-- JobDateによる検索用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DateProcessingHistory]') AND name = N'IX_DateProcessingHistory_JobDate')
BEGIN
    CREATE INDEX IX_DateProcessingHistory_JobDate 
    ON DateProcessingHistory(JobDate);
    
    PRINT 'IX_DateProcessingHistory_JobDate インデックスを作成しました。';
END
GO

-- ProcessTypeによる検索用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DateProcessingHistory]') AND name = N'IX_DateProcessingHistory_ProcessType')
BEGIN
    CREATE INDEX IX_DateProcessingHistory_ProcessType 
    ON DateProcessingHistory(ProcessType);
    
    PRINT 'IX_DateProcessingHistory_ProcessType インデックスを作成しました。';
END
GO

-- Departmentによる検索用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DateProcessingHistory]') AND name = N'IX_DateProcessingHistory_Department')
BEGIN
    CREATE INDEX IX_DateProcessingHistory_Department 
    ON DateProcessingHistory(Department);
    
    PRINT 'IX_DateProcessingHistory_Department インデックスを作成しました。';
END
GO

-- 重複防止用のユニークインデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DateProcessingHistory]') AND name = N'IX_DateProcessingHistory_Unique')
BEGIN
    CREATE UNIQUE INDEX IX_DateProcessingHistory_Unique 
    ON DateProcessingHistory(FileHistoryId, JobDate, ProcessType, Department);
    
    PRINT 'IX_DateProcessingHistory_Unique インデックスを作成しました。';
END
GO

PRINT 'Migration 026: DateProcessingHistory テーブルとインデックスの作成が完了しました。';
GO