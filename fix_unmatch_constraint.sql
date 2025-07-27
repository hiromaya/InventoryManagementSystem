-- UnmatchCheckResultテーブルの制約修正
-- エラー時の IsPassed=0, UnmatchCount=0 の組み合わせを許可

USE InventoryManagementDB;

-- 既存の制約を削除
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_UnmatchCheckResult_PassedConsistency')
BEGIN
    PRINT '既存の制約 CK_UnmatchCheckResult_PassedConsistency を削除します...';
    ALTER TABLE UnmatchCheckResult DROP CONSTRAINT CK_UnmatchCheckResult_PassedConsistency;
    PRINT '制約を削除しました。';
END

-- 新しい制約を追加（エラー時の IsPassed=0, UnmatchCount=0 を許可）
ALTER TABLE UnmatchCheckResult
ADD CONSTRAINT CK_UnmatchCheckResult_PassedConsistency 
CHECK (
    (IsPassed = 1 AND UnmatchCount = 0 AND CheckStatus = 'Passed') OR  -- 成功時
    (IsPassed = 0 AND UnmatchCount > 0 AND CheckStatus = 'Failed') OR  -- アンマッチあり
    (IsPassed = 0 AND CheckStatus = 'Error')                           -- エラー時（UnmatchCountは任意）
);

PRINT '新しい制約 CK_UnmatchCheckResult_PassedConsistency を追加しました。';
PRINT 'エラー時の IsPassed=0, UnmatchCount=0 の組み合わせが許可されました。';