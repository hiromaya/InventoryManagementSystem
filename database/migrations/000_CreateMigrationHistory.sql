-- マイグレーション履歴テーブルの作成
-- このテーブルは適用済みのマイグレーションを追跡するために使用されます
IF NOT EXISTS (
    SELECT * FROM sysobjects 
    WHERE name='__SchemaVersions' AND xtype='U'
)
BEGIN
    CREATE TABLE __SchemaVersions (
        MigrationId NVARCHAR(255) NOT NULL PRIMARY KEY,
        AppliedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
        AppliedBy NVARCHAR(100) NOT NULL DEFAULT SYSTEM_USER,
        ScriptContent NVARCHAR(MAX) NULL,
        ExecutionTimeMs INT NULL
    );
    
    -- インデックスの作成
    CREATE INDEX IX_SchemaVersions_AppliedDate ON __SchemaVersions(AppliedDate DESC);
    
    PRINT 'マイグレーション履歴テーブル [__SchemaVersions] を作成しました';
END
ELSE
BEGIN
    PRINT 'マイグレーション履歴テーブル [__SchemaVersions] は既に存在します';
END
GO