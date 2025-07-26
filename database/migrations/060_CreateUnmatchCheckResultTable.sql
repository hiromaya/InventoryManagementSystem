-- ============================================================================
-- アンマッチチェック結果テーブル作成マイグレーション
-- ファイル: 060_CreateUnmatchCheckResultTable.sql
-- 目的: アンマッチチェック0件必須機能のためのテーブル作成
-- 作成日: 2025-07-25
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UnmatchCheckResult')
BEGIN
    PRINT '⚡ UnmatchCheckResultテーブルを作成しています...'
    
    CREATE TABLE UnmatchCheckResult (
        DataSetId NVARCHAR(50) NOT NULL PRIMARY KEY,
        CheckDateTime DATETIME2 NOT NULL,
        UnmatchCount INT NOT NULL,
        HasFullWidthError BIT NOT NULL DEFAULT 0,
        IsPassed BIT NOT NULL DEFAULT 0, -- 0件達成フラグ
        CheckStatus NVARCHAR(20) NOT NULL, -- 'Passed', 'Failed', 'Error'
        ErrorMessage NVARCHAR(MAX),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    )
    
    PRINT '✅ UnmatchCheckResultテーブルが作成されました'
END
ELSE
BEGIN
    PRINT '⚠️ UnmatchCheckResultテーブルは既に存在します'
END

-- インデックス作成
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UnmatchCheckResult_CheckDateTime')
BEGIN
    PRINT '⚡ インデックス IX_UnmatchCheckResult_CheckDateTime を作成しています...'
    CREATE INDEX IX_UnmatchCheckResult_CheckDateTime ON UnmatchCheckResult(CheckDateTime DESC)
    PRINT '✅ インデックス IX_UnmatchCheckResult_CheckDateTime が作成されました'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UnmatchCheckResult_IsPassed')
BEGIN
    PRINT '⚡ インデックス IX_UnmatchCheckResult_IsPassed を作成しています...'
    CREATE INDEX IX_UnmatchCheckResult_IsPassed ON UnmatchCheckResult(IsPassed)
    PRINT '✅ インデックス IX_UnmatchCheckResult_IsPassed が作成されました'
END

-- チェック制約の追加
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_UnmatchCheckResult_CheckStatus')
BEGIN
    PRINT '⚡ チェック制約 CK_UnmatchCheckResult_CheckStatus を作成しています...'
    ALTER TABLE UnmatchCheckResult
    ADD CONSTRAINT CK_UnmatchCheckResult_CheckStatus 
    CHECK (CheckStatus IN ('Passed', 'Failed', 'Error'))
    PRINT '✅ チェック制約 CK_UnmatchCheckResult_CheckStatus が作成されました'
END

-- UNmatchCountが負数でないことを保証
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_UnmatchCheckResult_UnmatchCount')
BEGIN
    PRINT '⚡ チェック制約 CK_UnmatchCheckResult_UnmatchCount を作成しています...'
    ALTER TABLE UnmatchCheckResult
    ADD CONSTRAINT CK_UnmatchCheckResult_UnmatchCount 
    CHECK (UnmatchCount >= 0)
    PRINT '✅ チェック制約 CK_UnmatchCheckResult_UnmatchCount が作成されました'
END

-- IsPassedとUnmatchCountの整合性チェック
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_UnmatchCheckResult_PassedConsistency')
BEGIN
    PRINT '⚡ チェック制約 CK_UnmatchCheckResult_PassedConsistency を作成しています...'
    ALTER TABLE UnmatchCheckResult
    ADD CONSTRAINT CK_UnmatchCheckResult_PassedConsistency 
    CHECK ((IsPassed = 1 AND UnmatchCount = 0) OR (IsPassed = 0 AND UnmatchCount > 0))
    PRINT '✅ チェック制約 CK_UnmatchCheckResult_PassedConsistency が作成されました'
END

PRINT ''
PRINT '🎯 UnmatchCheckResultテーブルの作成が完了しました'
PRINT '   - DataSetIdごとに最新の1件のみ保持（Upsert方式）'
PRINT '   - パフォーマンス最適化のためのインデックス付与済み'
PRINT '   - データ整合性保証のためのチェック制約付与済み'
PRINT ''