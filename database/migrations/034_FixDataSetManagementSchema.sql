-- =====================================================
-- 034_FixDataSetManagementSchema.sql
-- 作成日: 2025-07-17
-- 目的: DataSetManagementテーブルのカラムサイズ統一
-- 説明: 調査結果で特定されたカラムサイズ不整合の修正
-- 修正内容:
--   1. ParentDataSetId: NVARCHAR(50) -> NVARCHAR(100)
--   2. Department: NVARCHAR(20) -> NVARCHAR(50)
--   3. CreatedBy: NVARCHAR(50) -> NVARCHAR(100)
--   4. Notes: NVARCHAR(500) -> NVARCHAR(MAX)
--   5. 制約とインデックスの確認
-- =====================================================

USE InventoryManagementDB;
GO

-- スクリプト実行オプションを設定
SET NOCOUNT ON;      -- "xx rows affected"メッセージを抑制
SET XACT_ABORT ON;   -- エラー発生時にトランザクションを自動的にロールバック

PRINT '=== 034_FixDataSetManagementSchema.sql 実行開始 ===';

-- ================================================================================
-- メインロジック（トランザクション制御付き）
-- ================================================================================
BEGIN TRY
    -- トランザクションを開始
    BEGIN TRANSACTION;

    PRINT 'DataSetManagementテーブルのカラムサイズ統一を開始します...';

    -- テーブルの存在チェック
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DataSetManagement' AND SCHEMA_NAME(schema_id) = 'dbo')
    BEGIN
        PRINT 'エラー: DataSetManagementテーブルが存在しません。スクリプトを中断します。';
        THROW 50000, 'Table [dbo].[DataSetManagement] does not exist. Script cannot continue.', 1;
    END

    -- ----------------------------------------------------------------------------
    -- Step 1: ParentDataSetIdカラムサイズの統一（NVARCHAR(50) -> NVARCHAR(100)）
    -- ----------------------------------------------------------------------------
    PRINT 'Step 1: ParentDataSetId カラムサイズの確認と修正...';

    DECLARE @ParentDataSetIdCurrentSize INT;
    SELECT @ParentDataSetIdCurrentSize = CHARACTER_MAXIMUM_LENGTH 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'DataSetManagement' 
    AND TABLE_SCHEMA = 'dbo' 
    AND COLUMN_NAME = 'ParentDataSetId';

    IF @ParentDataSetIdCurrentSize IS NOT NULL AND @ParentDataSetIdCurrentSize < 100
    BEGIN
        PRINT CONCAT('  - ParentDataSetId のサイズを ', @ParentDataSetIdCurrentSize, ' から 100 に変更中...');
        ALTER TABLE dbo.DataSetManagement ALTER COLUMN ParentDataSetId NVARCHAR(100);
        PRINT '  ✓ ParentDataSetId カラムサイズを NVARCHAR(100) に変更しました';
    END
    ELSE IF @ParentDataSetIdCurrentSize IS NOT NULL
    BEGIN
        PRINT CONCAT('  - ParentDataSetId は既に適切なサイズです (', @ParentDataSetIdCurrentSize, ')');
    END
    ELSE
    BEGIN
        PRINT '  - ParentDataSetId カラムが存在しません';
    END

    -- ----------------------------------------------------------------------------
    -- Step 2: Departmentカラムサイズの統一（NVARCHAR(20) -> NVARCHAR(50)）
    -- ----------------------------------------------------------------------------
    PRINT 'Step 2: Department カラムサイズの確認と修正...';

    DECLARE @DepartmentCurrentSize INT;
    SELECT @DepartmentCurrentSize = CHARACTER_MAXIMUM_LENGTH 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'DataSetManagement' 
    AND TABLE_SCHEMA = 'dbo' 
    AND COLUMN_NAME = 'Department';

    IF @DepartmentCurrentSize IS NOT NULL AND @DepartmentCurrentSize < 50
    BEGIN
        PRINT CONCAT('  - Department のサイズを ', @DepartmentCurrentSize, ' から 50 に変更中...');
        ALTER TABLE dbo.DataSetManagement ALTER COLUMN Department NVARCHAR(50);
        PRINT '  ✓ Department カラムサイズを NVARCHAR(50) に変更しました';
    END
    ELSE IF @DepartmentCurrentSize IS NOT NULL
    BEGIN
        PRINT CONCAT('  - Department は既に適切なサイズです (', @DepartmentCurrentSize, ')');
    END
    ELSE
    BEGIN
        PRINT '  - Department カラムが存在しません';
    END

    -- ----------------------------------------------------------------------------
    -- Step 3: CreatedByカラムサイズの統一（NVARCHAR(50) -> NVARCHAR(100)）
    -- ----------------------------------------------------------------------------
    PRINT 'Step 3: CreatedBy カラムサイズの確認と修正...';

    DECLARE @CreatedByCurrentSize INT;
    SELECT @CreatedByCurrentSize = CHARACTER_MAXIMUM_LENGTH 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'DataSetManagement' 
    AND TABLE_SCHEMA = 'dbo' 
    AND COLUMN_NAME = 'CreatedBy';

    IF @CreatedByCurrentSize IS NOT NULL AND @CreatedByCurrentSize < 100
    BEGIN
        PRINT CONCAT('  - CreatedBy のサイズを ', @CreatedByCurrentSize, ' から 100 に変更中...');
        ALTER TABLE dbo.DataSetManagement ALTER COLUMN CreatedBy NVARCHAR(100);
        PRINT '  ✓ CreatedBy カラムサイズを NVARCHAR(100) に変更しました';
    END
    ELSE IF @CreatedByCurrentSize IS NOT NULL
    BEGIN
        PRINT CONCAT('  - CreatedBy は既に適切なサイズです (', @CreatedByCurrentSize, ')');
    END
    ELSE
    BEGIN
        PRINT '  - CreatedBy カラムが存在しません';
    END

    -- ----------------------------------------------------------------------------
    -- Step 4: Notesカラムサイズの統一（NVARCHAR(500) -> NVARCHAR(MAX)）
    -- ----------------------------------------------------------------------------
    PRINT 'Step 4: Notes カラムサイズの確認と修正...';

    DECLARE @NotesCurrentSize INT;
    SELECT @NotesCurrentSize = CHARACTER_MAXIMUM_LENGTH 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'DataSetManagement' 
    AND TABLE_SCHEMA = 'dbo' 
    AND COLUMN_NAME = 'Notes';

    -- CHARACTER_MAXIMUM_LENGTH が -1 の場合は MAX を示す
    IF @NotesCurrentSize IS NOT NULL AND @NotesCurrentSize != -1
    BEGIN
        PRINT CONCAT('  - Notes のサイズを ', @NotesCurrentSize, ' から MAX に変更中...');
        ALTER TABLE dbo.DataSetManagement ALTER COLUMN Notes NVARCHAR(MAX);
        PRINT '  ✓ Notes カラムサイズを NVARCHAR(MAX) に変更しました';
    END
    ELSE IF @NotesCurrentSize = -1
    BEGIN
        PRINT '  - Notes は既に NVARCHAR(MAX) です';
    END
    ELSE
    BEGIN
        PRINT '  - Notes カラムが存在しません';
    END

    -- ----------------------------------------------------------------------------
    -- Step 5: 他の重要カラムサイズの確認（DeactivatedBy, ArchivedByなど）
    -- ----------------------------------------------------------------------------
    PRINT 'Step 5: その他の重要カラムサイズの確認...';

    -- DeactivatedBy と ArchivedBy のサイズ確認（50が適切）
    DECLARE @DeactivatedBySize INT, @ArchivedBySize INT;
    
    SELECT @DeactivatedBySize = CHARACTER_MAXIMUM_LENGTH 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'DataSetManagement' 
    AND TABLE_SCHEMA = 'dbo' 
    AND COLUMN_NAME = 'DeactivatedBy';

    SELECT @ArchivedBySize = CHARACTER_MAXIMUM_LENGTH 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'DataSetManagement' 
    AND TABLE_SCHEMA = 'dbo' 
    AND COLUMN_NAME = 'ArchivedBy';

    IF @DeactivatedBySize IS NOT NULL
        PRINT CONCAT('  - DeactivatedBy サイズ: NVARCHAR(', @DeactivatedBySize, ')');
    IF @ArchivedBySize IS NOT NULL
        PRINT CONCAT('  - ArchivedBy サイズ: NVARCHAR(', @ArchivedBySize, ')');

    -- ----------------------------------------------------------------------------
    -- Step 6: 制約とインデックスの確認
    -- ----------------------------------------------------------------------------
    PRINT 'Step 6: 制約とインデックスの確認...';

    -- ImportType制約の確認
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints 
                   WHERE name = 'CK_DataSetManagement_ImportType' 
                   AND parent_object_id = OBJECT_ID('dbo.DataSetManagement'))
    BEGIN
        PRINT '  - ImportType チェック制約を作成中...';
        ALTER TABLE dbo.DataSetManagement 
        ADD CONSTRAINT CK_DataSetManagement_ImportType 
        CHECK (ImportType IN ('INIT', 'IMPORT', 'CARRYOVER', 'MANUAL', 'UNKNOWN'));
        PRINT '  ✓ ImportType チェック制約を作成しました';
    END
    ELSE
    BEGIN
        PRINT '  - ImportType チェック制約は既に存在します';
    END

    -- 外部キー制約の確認（自己参照）
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys 
                   WHERE name = 'FK_DataSetManagement_Parent' 
                   AND parent_object_id = OBJECT_ID('dbo.DataSetManagement'))
    BEGIN
        PRINT '  - 自己参照外部キー制約を作成中...';
        ALTER TABLE dbo.DataSetManagement 
        ADD CONSTRAINT FK_DataSetManagement_Parent 
        FOREIGN KEY (ParentDataSetId) REFERENCES dbo.DataSetManagement(DatasetId);
        PRINT '  ✓ 自己参照外部キー制約を作成しました';
    END
    ELSE
    BEGIN
        PRINT '  - 自己参照外部キー制約は既に存在します';
    END

    -- パフォーマンス向上のためのインデックス確認
    IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                   WHERE object_id = OBJECT_ID('dbo.DataSetManagement') 
                   AND name = 'IX_DataSetManagement_JobDate_IsActive')
    BEGIN
        PRINT '  - JobDate_IsActive インデックスを作成中...';
        CREATE NONCLUSTERED INDEX IX_DataSetManagement_JobDate_IsActive 
        ON dbo.DataSetManagement(JobDate, IsActive) 
        WHERE IsActive = 1;
        PRINT '  ✓ JobDate_IsActive インデックスを作成しました';
    END
    ELSE
    BEGIN
        PRINT '  - JobDate_IsActive インデックスは既に存在します';
    END

    -- ----------------------------------------------------------------------------
    -- Step 7: データ整合性チェックと統計情報
    -- ----------------------------------------------------------------------------
    PRINT 'Step 7: データ整合性チェック...';

    DECLARE @TotalDataSetMgmtCount INT, @ActiveCount INT, @ArchivedCount INT, @InvalidImportTypeCount INT;
    
    SELECT @TotalDataSetMgmtCount = COUNT(*) FROM dbo.DataSetManagement;
    SELECT @ActiveCount = COUNT(*) FROM dbo.DataSetManagement WHERE IsActive = 1;
    SELECT @ArchivedCount = COUNT(*) FROM dbo.DataSetManagement WHERE IsArchived = 1;
    SELECT @InvalidImportTypeCount = COUNT(*) 
    FROM dbo.DataSetManagement 
    WHERE ImportType NOT IN ('INIT', 'IMPORT', 'CARRYOVER', 'MANUAL', 'UNKNOWN');

    PRINT CONCAT('  - 総レコード数: ', @TotalDataSetMgmtCount);
    PRINT CONCAT('  - アクティブレコード数: ', @ActiveCount);
    PRINT CONCAT('  - アーカイブレコード数: ', @ArchivedCount);
    PRINT CONCAT('  - 無効なImportType: ', @InvalidImportTypeCount);

    -- 無効なImportTypeのチェック
    IF @InvalidImportTypeCount > 0
    BEGIN
        PRINT '警告: 無効なImportTypeが見つかりました。修正を推奨します。';
        -- エラーにはしない（警告のみ）
    END

    -- ----------------------------------------------------------------------------
    -- Step 8: 修正完了の確認とスキーマ情報出力
    -- ----------------------------------------------------------------------------
    PRINT 'Step 8: 修正完了の確認...';

    -- 最終的なスキーマ情報を出力（確認用）
    PRINT '  - 最終的なDataSetManagementテーブル構造:';
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        CHARACTER_MAXIMUM_LENGTH,
        IS_NULLABLE,
        COLUMN_DEFAULT
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'DataSetManagement' AND TABLE_SCHEMA = 'dbo'
    ORDER BY ORDINAL_POSITION;

    -- すべて成功した場合、トランザクションをコミット
    COMMIT TRANSACTION;

    PRINT '';
    PRINT '✅ DataSetManagementテーブルの修正が正常に完了しました';
    PRINT 'Migration 034: DataSetManagementテーブルカラムサイズ統一完了';

END TRY
BEGIN CATCH
    -- エラーが発生した場合の処理
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT '';
        PRINT '❌ エラーが発生しました。すべての変更をロールバックしました。';
    END

    -- 詳細なエラー情報を表示
    PRINT CONCAT('エラー番号: ', ERROR_NUMBER());
    PRINT CONCAT('エラーメッセージ: ', ERROR_MESSAGE());
    PRINT CONCAT('エラー行: ', ERROR_LINE());
    PRINT CONCAT('エラー状態: ', ERROR_STATE());
    PRINT CONCAT('エラー重要度: ', ERROR_SEVERITY());

    -- 元のエラーを再スローして、呼び出し元に失敗を通知
    THROW;
END CATCH

PRINT '';
PRINT '=== 034_FixDataSetManagementSchema.sql 実行完了 ===';
GO