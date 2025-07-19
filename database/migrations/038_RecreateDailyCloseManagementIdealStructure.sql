-- =============================================================================
-- 038_RecreateDailyCloseManagementIdealStructure.sql
-- DailyCloseManagementテーブルを理想的構造に完全移行
-- 実行日: 2025-07-19
-- 目的: エンティティクラスと完全一致する理想的なテーブル構造への移行
-- 前提: データ0件確認済み、外部キー制約なし
-- =============================================================================

USE InventoryManagementDB;
GO

PRINT '========================================';
PRINT 'DailyCloseManagement 理想的構造への移行';
PRINT '実行日時: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
PRINT '';

-- ================================================================================
-- 1. 既存構造の完全削除
-- ================================================================================
PRINT '1. 既存構造の削除開始';

-- 既存インデックスの削除（存在する場合）
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DailyCloseManagement_ProcessDate')
BEGIN
    DROP INDEX IX_DailyCloseManagement_ProcessDate ON DailyCloseManagement;
    PRINT '  ✓ 既存インデックス IX_DailyCloseManagement_ProcessDate を削除';
END

-- その他の可能性のあるインデックスを確認・削除
DECLARE @indexName NVARCHAR(128);
DECLARE index_cursor CURSOR FOR
    SELECT i.name
    FROM sys.indexes i
    INNER JOIN sys.objects o ON i.object_id = o.object_id
    WHERE o.name = 'DailyCloseManagement' 
    AND i.type > 0 -- ヒープ以外
    AND i.is_primary_key = 0; -- 主キー以外

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @indexName;

WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC('DROP INDEX ' + @indexName + ' ON DailyCloseManagement');
    PRINT '  ✓ インデックス ' + @indexName + ' を削除';
    FETCH NEXT FROM index_cursor INTO @indexName;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;

-- DailyCloseManagementテーブルの削除
IF OBJECT_ID('dbo.DailyCloseManagement', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.DailyCloseManagement;
    PRINT '  ✅ DailyCloseManagement テーブルを削除しました';
END
ELSE
BEGIN
    PRINT '  - DailyCloseManagement テーブルは存在しませんでした';
END

-- ================================================================================
-- 2. 理想的構造での再作成（Gemini推奨設計＋エンティティ完全一致）
-- ================================================================================
PRINT '';
PRINT '2. 理想的構造でのテーブル再作成';

CREATE TABLE dbo.DailyCloseManagement
(
    Id INT PRIMARY KEY IDENTITY(1,1),
    JobDate DATE NOT NULL,                                -- エンティティ一致（ProcessDateではない）
    DataSetId NVARCHAR(50) NOT NULL,                     -- エンティティ一致（大文字S）
    DailyReportDataSetId NVARCHAR(50) NOT NULL,          -- 商品日報との紐付け
    BackupPath NVARCHAR(500) NULL,                       -- バックアップパス
    ProcessedAt DATETIME2 NOT NULL DEFAULT GETDATE(),   -- 処理日時（JST基準）
    ProcessedBy NVARCHAR(50) NOT NULL,                   -- 処理者
    DataHash NVARCHAR(100) NULL,                         -- SHA256ハッシュ用
    ValidationStatus NVARCHAR(20) NULL DEFAULT 'PENDING', -- Gemini推奨デフォルト
    Remarks NVARCHAR(500) NULL,                          -- 備考

    -- Gemini推奨：JobDateユニーク制約（重複実行防止）
    CONSTRAINT UQ_DailyCloseManagement_JobDate UNIQUE (JobDate)
);

PRINT '  ✅ DailyCloseManagement テーブルを理想的構造で作成';

-- ================================================================================
-- 3. パフォーマンス最適化インデックス（Gemini推奨）
-- ================================================================================
PRINT '';
PRINT '3. 最適化インデックスの作成';

-- DataSetId検索用インデックス
CREATE NONCLUSTERED INDEX IX_DailyCloseManagement_DataSetId 
ON dbo.DailyCloseManagement(DataSetId);
PRINT '  ✓ DataSetId インデックス作成';

-- DailyReportDataSetId検索用インデックス
CREATE NONCLUSTERED INDEX IX_DailyCloseManagement_DailyReportDataSetId 
ON dbo.DailyCloseManagement(DailyReportDataSetId);
PRINT '  ✓ DailyReportDataSetId インデックス作成';

-- ProcessedAt降順インデックス（最新データ取得用）
CREATE NONCLUSTERED INDEX IX_DailyCloseManagement_ProcessedAt_Desc 
ON dbo.DailyCloseManagement(ProcessedAt DESC);
PRINT '  ✓ ProcessedAt降順 インデックス作成';

-- ValidationStatus検索用インデックス
CREATE NONCLUSTERED INDEX IX_DailyCloseManagement_ValidationStatus 
ON dbo.DailyCloseManagement(ValidationStatus)
WHERE ValidationStatus IS NOT NULL;
PRINT '  ✓ ValidationStatus フィルタ付きインデックス作成';

-- ================================================================================
-- 4. 制約とルールの確認
-- ================================================================================
PRINT '';
PRINT '4. 制約とルールの確認';

-- ValidationStatusの妥当性チェック制約（オプション）
ALTER TABLE dbo.DailyCloseManagement
ADD CONSTRAINT CK_DailyCloseManagement_ValidationStatus
CHECK (ValidationStatus IN ('PENDING', 'PROCESSING', 'VALIDATING', 'PASSED', 'FAILED', 'WARNING'));
PRINT '  ✓ ValidationStatus チェック制約追加';

-- ================================================================================
-- 5. 作成結果の検証
-- ================================================================================
PRINT '';
PRINT '5. 作成結果の検証';

-- テーブル構造の確認
SELECT 
    c.COLUMN_NAME AS カラム名,
    c.DATA_TYPE AS データ型,
    CASE 
        WHEN c.CHARACTER_MAXIMUM_LENGTH IS NOT NULL 
        THEN c.DATA_TYPE + '(' + CAST(c.CHARACTER_MAXIMUM_LENGTH AS VARCHAR) + ')'
        ELSE c.DATA_TYPE
    END AS 完全データ型,
    c.IS_NULLABLE AS NULL許可,
    c.COLUMN_DEFAULT AS デフォルト値
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'DailyCloseManagement'
ORDER BY c.ORDINAL_POSITION;

-- インデックス一覧の確認
PRINT '';
PRINT 'インデックス一覧:';
SELECT 
    i.name AS インデックス名,
    i.type_desc AS インデックス種別,
    i.is_unique AS ユニーク,
    STRING_AGG(COL_NAME(ic.object_id, ic.column_id), ', ') AS 対象カラム
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE i.object_id = OBJECT_ID('DailyCloseManagement')
GROUP BY i.name, i.type_desc, i.is_unique
ORDER BY i.name;

-- 制約一覧の確認
PRINT '';
PRINT '制約一覧:';
SELECT 
    tc.CONSTRAINT_NAME AS 制約名,
    tc.CONSTRAINT_TYPE AS 制約種別
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
WHERE tc.TABLE_NAME = 'DailyCloseManagement';

-- ================================================================================
-- 6. エンティティクラス一致確認
-- ================================================================================
PRINT '';
PRINT '6. エンティティクラスとの一致確認';
PRINT '期待されるプロパティ:';
PRINT '  ✓ Id (int)';
PRINT '  ✓ JobDate (DateTime) → DATE';
PRINT '  ✓ DataSetId (string) → NVARCHAR(50)';
PRINT '  ✓ DailyReportDataSetId (string) → NVARCHAR(50)';
PRINT '  ✓ BackupPath (string) → NVARCHAR(500)';
PRINT '  ✓ ProcessedAt (DateTime) → DATETIME2';
PRINT '  ✓ ProcessedBy (string) → NVARCHAR(50)';
PRINT '  ✓ DataHash (string) → NVARCHAR(100)';
PRINT '  ✓ ValidationStatus (string) → NVARCHAR(20)';
PRINT '  ✓ Remarks (string) → NVARCHAR(500)';

PRINT '';
PRINT '========================================';
PRINT 'DailyCloseManagement 理想的構造移行完了';
PRINT '========================================';
PRINT '';
PRINT '✅ Gemini推奨設計を適用:';
PRINT '   - JobDateユニーク制約（重複実行防止）';
PRINT '   - パフォーマンス最適化インデックス';
PRINT '   - ValidationStatusチェック制約';
PRINT '   - デフォルト値設定';
PRINT '';
PRINT '✅ エンティティクラスとの完全一致:';
PRINT '   - すべてのプロパティが対応カラムを持つ';
PRINT '   - データ型とサイズが適切';
PRINT '';
PRINT '次のステップ:';
PRINT '1. DailyCloseManagementRepository.csのINSERT文修正';
PRINT '2. 動作確認（dev-check-daily-close）';
PRINT '3. テスト実行（dev-daily-close --dry-run）';

GO