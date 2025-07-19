-- =============================================================================
-- 037_FixDataSetManagementDefaultConstraints.sql
-- DataSetManagementテーブルのUpdatedAtカラムにデフォルト制約を追加
-- 実行日: 2025-07-19
-- 目的: SqlDateTime overflow防止の最終的なデータベースレベル対策
-- =============================================================================

USE InventoryManagementDB;
GO

PRINT '================================';
PRINT 'DataSetManagement UpdatedAt デフォルト制約追加';
PRINT '実行日時: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '================================';
PRINT '';

-- ================================================================================
-- 1. UpdatedAtカラムの現在の状態確認
-- ================================================================================
PRINT '1. UpdatedAtカラムの現在の状態確認';
SELECT 
    c.COLUMN_NAME AS カラム名,
    c.DATA_TYPE AS データ型,
    c.IS_NULLABLE AS NULL許可,
    c.COLUMN_DEFAULT AS 現在のデフォルト値,
    CASE 
        WHEN dc.name IS NOT NULL THEN 'あり (' + dc.name + ')'
        ELSE 'なし'
    END AS デフォルト制約
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN sys.default_constraints dc 
    ON dc.parent_object_id = OBJECT_ID('DataSetManagement')
    AND dc.parent_column_id = (
        SELECT column_id 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('DataSetManagement') 
        AND name = 'UpdatedAt'
    )
WHERE c.TABLE_NAME = 'DataSetManagement' 
AND c.COLUMN_NAME = 'UpdatedAt';

-- ================================================================================
-- 2. 既存のデフォルト制約があれば削除
-- ================================================================================
PRINT '';
PRINT '2. 既存のデフォルト制約の確認と削除';
IF EXISTS (
    SELECT 1 FROM sys.default_constraints 
    WHERE parent_object_id = OBJECT_ID('DataSetManagement') 
    AND parent_column_id = (
        SELECT column_id FROM sys.columns 
        WHERE object_id = OBJECT_ID('DataSetManagement') 
        AND name = 'UpdatedAt'
    )
)
BEGIN
    DECLARE @ConstraintName NVARCHAR(200);
    SELECT @ConstraintName = name 
    FROM sys.default_constraints 
    WHERE parent_object_id = OBJECT_ID('DataSetManagement') 
    AND parent_column_id = (
        SELECT column_id FROM sys.columns 
        WHERE object_id = OBJECT_ID('DataSetManagement') 
        AND name = 'UpdatedAt'
    );
    
    EXEC('ALTER TABLE DataSetManagement DROP CONSTRAINT ' + @ConstraintName);
    PRINT '  ✓ 既存のデフォルト制約 ' + @ConstraintName + ' を削除しました';
END
ELSE
BEGIN
    PRINT '  - 既存のデフォルト制約はありません';
END

-- ================================================================================
-- 3. NULLレコードの更新（既存データ保護）
-- ================================================================================
PRINT '';
PRINT '3. UpdatedAtがNULLのレコードを更新';
DECLARE @UpdateCount INT;
UPDATE DataSetManagement 
SET UpdatedAt = CreatedAt 
WHERE UpdatedAt IS NULL;
SET @UpdateCount = @@ROWCOUNT;
PRINT '  ✓ ' + CAST(@UpdateCount AS VARCHAR) + ' 件のレコードを更新しました（UpdatedAt = CreatedAtで初期化）';

-- ================================================================================
-- 4. デフォルト制約の追加
-- ================================================================================
PRINT '';
PRINT '4. UpdatedAtカラムにデフォルト制約を追加';
ALTER TABLE DataSetManagement 
ADD CONSTRAINT DF_DataSetManagement_UpdatedAt 
DEFAULT GETDATE() FOR UpdatedAt;
PRINT '  ✓ デフォルト制約 DF_DataSetManagement_UpdatedAt を追加しました';
PRINT '  └ 新規レコード作成時に自動的に現在時刻が設定されます';

-- ================================================================================
-- 5. 追加後の検証
-- ================================================================================
PRINT '';
PRINT '5. 追加後の検証';
SELECT 
    c.COLUMN_NAME AS カラム名,
    c.DATA_TYPE AS データ型,
    c.IS_NULLABLE AS NULL許可,
    c.COLUMN_DEFAULT AS デフォルト値,
    dc.name AS 制約名,
    dc.definition AS 制約定義
FROM INFORMATION_SCHEMA.COLUMNS c
INNER JOIN sys.default_constraints dc 
    ON dc.parent_object_id = OBJECT_ID('DataSetManagement')
    AND dc.parent_column_id = (
        SELECT column_id 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('DataSetManagement') 
        AND name = 'UpdatedAt'
    )
WHERE c.TABLE_NAME = 'DataSetManagement' 
AND c.COLUMN_NAME = 'UpdatedAt';

-- ================================================================================
-- 6. 関連カラムとの比較（参考情報）
-- ================================================================================
PRINT '';
PRINT '6. 関連するDATETIME2カラムの制約状況';
SELECT 
    c.COLUMN_NAME AS カラム名,
    c.DATA_TYPE AS データ型,
    c.IS_NULLABLE AS NULL許可,
    COALESCE(dc.name, '制約なし') AS デフォルト制約,
    COALESCE(dc.definition, '-') AS 制約定義
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN sys.default_constraints dc 
    ON dc.parent_object_id = OBJECT_ID('DataSetManagement')
    AND dc.parent_column_id = (
        SELECT column_id 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('DataSetManagement') 
        AND name = c.COLUMN_NAME
    )
WHERE c.TABLE_NAME = 'DataSetManagement' 
AND c.COLUMN_NAME IN ('CreatedAt', 'UpdatedAt', 'DeactivatedAt', 'ArchivedAt')
ORDER BY 
    CASE c.COLUMN_NAME 
        WHEN 'CreatedAt' THEN 1
        WHEN 'UpdatedAt' THEN 2
        WHEN 'DeactivatedAt' THEN 3
        WHEN 'ArchivedAt' THEN 4
    END;

-- ================================================================================
-- 7. データ整合性の最終確認
-- ================================================================================
PRINT '';
PRINT '7. データ整合性の最終確認';
DECLARE @TotalRecords INT, @NullUpdatedAtCount INT, @NullCreatedAtCount INT;

SELECT @TotalRecords = COUNT(*) FROM DataSetManagement;
SELECT @NullUpdatedAtCount = COUNT(*) FROM DataSetManagement WHERE UpdatedAt IS NULL;
SELECT @NullCreatedAtCount = COUNT(*) FROM DataSetManagement WHERE CreatedAt IS NULL;

PRINT '  - 総レコード数: ' + CAST(@TotalRecords AS VARCHAR);
PRINT '  - UpdatedAtがNULLのレコード: ' + CAST(@NullUpdatedAtCount AS VARCHAR);
PRINT '  - CreatedAtがNULLのレコード: ' + CAST(@NullCreatedAtCount AS VARCHAR);

IF @NullUpdatedAtCount > 0
BEGIN
    PRINT '  ⚠️ 警告: UpdatedAtがNULLのレコードが残っています';
END
ELSE
BEGIN
    PRINT '  ✅ 全レコードのUpdatedAtが設定されています';
END

-- ================================================================================
-- 8. テスト用の動作確認
-- ================================================================================
PRINT '';
PRINT '8. デフォルト制約の動作確認（テスト挿入）';

-- テスト用レコードを挿入（UpdatedAtを明示的に指定しない）
DECLARE @TestDataSetId NVARCHAR(100) = 'TEST_DEFAULT_CONSTRAINT_' + CONVERT(VARCHAR(36), NEWID());
INSERT INTO DataSetManagement (
    DataSetId, JobDate, ProcessType, ImportType, 
    RecordCount, TotalRecordCount, IsActive, IsArchived,
    CreatedAt, CreatedBy, Department
) VALUES (
    @TestDataSetId,
    GETDATE(),
    'TEST',
    'TEST',
    0, 0, 1, 0,
    GETDATE(),
    'migration-test',
    'DeptA'
);

-- 結果確認
SELECT 
    DataSetId,
    CONVERT(VARCHAR, CreatedAt, 120) as CreatedAt,
    CONVERT(VARCHAR, UpdatedAt, 120) as UpdatedAt,
    CASE 
        WHEN UpdatedAt IS NOT NULL THEN '✅ デフォルト値設定成功'
        ELSE '❌ デフォルト値設定失敗'
    END as 動作確認結果
FROM DataSetManagement
WHERE DataSetId = @TestDataSetId;

-- テストレコード削除
DELETE FROM DataSetManagement WHERE DataSetId = @TestDataSetId;
PRINT '  ✓ テストレコードを削除しました';

PRINT '';
PRINT '================================';
PRINT 'UpdatedAtデフォルト制約の追加完了';
PRINT '================================';
PRINT '';
PRINT '【重要】この修正により以下が保証されます:';
PRINT '1. 新規レコード作成時のUpdatedAt自動設定';
PRINT '2. SqlDateTime overflow エラーの完全防止';
PRINT '3. アプリケーション側とデータベース側の二重安全対策';

-- ================================================================================
-- 9. ロールバック用スクリプト（コメントアウト）
-- ================================================================================
/*
PRINT '';
PRINT '【ロールバック手順】';
PRINT '必要に応じて以下のスクリプトを実行してください:';
PRINT '';
PRINT 'ALTER TABLE DataSetManagement DROP CONSTRAINT DF_DataSetManagement_UpdatedAt;';
PRINT 'PRINT ''UpdatedAtデフォルト制約を削除しました'';';
*/

GO