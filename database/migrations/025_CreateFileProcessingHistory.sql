-- =============================================
-- FileProcessingHistory テーブル作成
-- 作成日: 2025-07-15
-- 説明: CSVファイルの処理履歴を管理するテーブル
-- =============================================

USE InventoryManagementDB;
GO

-- テーブルが存在しない場合のみ作成
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FileProcessingHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE FileProcessingHistory (
        -- 自動採番ID
        Id INT IDENTITY(1,1) PRIMARY KEY,
        
        -- ファイル情報
        FileName NVARCHAR(255) NOT NULL,
        FileHash NVARCHAR(64) NOT NULL,
        FileSize BIGINT NOT NULL,
        FilePath NVARCHAR(500) NOT NULL,
        
        -- 処理情報
        FirstProcessedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        LastProcessedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        TotalRecordCount INT NOT NULL DEFAULT 0,
        FileType NVARCHAR(50) NOT NULL,
        
        -- ステータスと監査情報
        Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
        CreatedBy NVARCHAR(50) NOT NULL DEFAULT SYSTEM_USER
    );
    
    PRINT 'FileProcessingHistory テーブルを作成しました。';
END
ELSE
BEGIN
    PRINT 'FileProcessingHistory テーブルは既に存在します。';
END
GO

-- インデックス作成
-- FileHashによる検索用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[FileProcessingHistory]') AND name = N'IX_FileProcessingHistory_FileHash')
BEGIN
    CREATE INDEX IX_FileProcessingHistory_FileHash 
    ON FileProcessingHistory(FileHash);
    
    PRINT 'IX_FileProcessingHistory_FileHash インデックスを作成しました。';
END
GO

-- FileNameによる検索用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[FileProcessingHistory]') AND name = N'IX_FileProcessingHistory_FileName')
BEGIN
    CREATE INDEX IX_FileProcessingHistory_FileName 
    ON FileProcessingHistory(FileName);
    
    PRINT 'IX_FileProcessingHistory_FileName インデックスを作成しました。';
END
GO

-- FileTypeによる検索用インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[FileProcessingHistory]') AND name = N'IX_FileProcessingHistory_FileType')
BEGIN
    CREATE INDEX IX_FileProcessingHistory_FileType 
    ON FileProcessingHistory(FileType);
    
    PRINT 'IX_FileProcessingHistory_FileType インデックスを作成しました。';
END
GO

PRINT 'Migration 025: FileProcessingHistory テーブルとインデックスの作成が完了しました。';
GO