-- =============================================
-- DataSetsテーブルの削除（DataSetManagement完全移行のため）
-- =============================================

-- DataSetsテーブルが存在する場合のみ削除
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DataSets]') AND type in (N'U'))
BEGIN
    PRINT 'DataSetsテーブルを削除します...'
    DROP TABLE [DataSets];
    PRINT 'DataSetsテーブルが削除されました。'
END
ELSE
BEGIN
    PRINT 'DataSetsテーブルは既に存在しません。'
END

-- 依存関係のチェック（参考情報として出力）
SELECT 
    OBJECT_NAME(parent_object_id) as 'Table',
    name as 'Foreign_Key'
FROM sys.foreign_keys 
WHERE referenced_object_id = OBJECT_ID('DataSets');

PRINT '=== DataSetsテーブル削除完了 ==='
GO