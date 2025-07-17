-- =====================================================
-- 033_FixDataSetsSchema.sql
-- 作成日: 2025-07-17
-- 目的: DataSetsテーブルの包括的スキーマ修正
-- 説明: Geminiとの相談結果を反映した堅牢なスキーマ修正
-- 修正内容:
--   1. Name, Description, ProcessType カラムの安全な追加
--   2. CreatedDate/UpdatedDate から CreatedAt/UpdatedAt への移行
--   3. 既存データの保護と段階的制約追加
--   4. 冪等性とトランザクション制御の実装
-- =====================================================

USE InventoryManagementDB;
GO

-- スクリプト実行オプションを設定（Gemini推奨設定）
SET NOCOUNT ON;      -- "xx rows affected"メッセージを抑制
SET XACT_ABORT ON;   -- エラー発生時にトランザクションを自動的にロールバック

PRINT '=== 033_FixDataSetsSchema.sql 実行開始 ===';

-- ================================================================================
-- メインロジック（トランザクション制御付き）
-- ================================================================================
BEGIN TRY
    -- トランザクションを開始
    BEGIN TRANSACTION;

    PRINT 'DataSetsテーブルの包括的修正を開始します...';

    -- テーブルの存在チェック
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DataSets' AND SCHEMA_NAME(schema_id) = 'dbo')
    BEGIN
        PRINT 'エラー: DataSetsテーブルが存在しません。スクリプトを中断します。';
        THROW 50000, 'Table [dbo].[DataSets] does not exist. Script cannot continue.', 1;
    END

    -- ----------------------------------------------------------------------------
    -- Step 1: 新しいカラムの安全な追加（段階的制約適用）
    -- ----------------------------------------------------------------------------
    PRINT 'Step 1: 新しいカラムの追加...';

    -- Name カラム（NOT NULL、デフォルト値付き）
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'Name')
    BEGIN
        PRINT '  - Name カラムを追加中（デフォルト値付き）...';
        ALTER TABLE dbo.DataSets 
        ADD Name NVARCHAR(255) NOT NULL 
        CONSTRAINT DF_DataSets_Name DEFAULT 'DataSet';
        PRINT '  ✓ Name カラムの追加完了';
    END
    ELSE
    BEGIN
        PRINT '  - Name カラムは既に存在します';
        -- 既存のNameカラムがNULLの場合はデフォルト値を設定
        UPDATE dbo.DataSets 
        SET Name = CONCAT('DataSet_', FORMAT(JobDate, 'yyyyMMdd'), '_', LEFT(Id, 8))
        WHERE Name IS NULL OR Name = '';
        PRINT '  ✓ 既存のNULL/空のNameに値を設定しました';
    END

    -- Description カラム（NULL許容）
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'Description')
    BEGIN
        PRINT '  - Description カラムを追加中（NULL許容）...';
        ALTER TABLE dbo.DataSets ADD Description NVARCHAR(MAX) NULL;
        PRINT '  ✓ Description カラムの追加完了';
    END
    ELSE
    BEGIN
        PRINT '  - Description カラムは既に存在します';
    END

    -- ProcessType カラム（段階的NOT NULL適用）
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'ProcessType')
    BEGIN
        PRINT '  - ProcessType カラムを追加中（段階的制約適用）...';
        
        -- Step 1a: NULL許容で追加
        ALTER TABLE dbo.DataSets ADD ProcessType NVARCHAR(100) NULL;
        
        -- Step 1b: 既存データにデフォルト値を設定
        UPDATE dbo.DataSets 
        SET ProcessType = CASE 
            WHEN DataSetType IS NOT NULL AND DataSetType != '' THEN DataSetType
            ELSE 'Unknown'
        END
        WHERE ProcessType IS NULL;
        
        -- Step 1c: NOT NULL制約を適用
        ALTER TABLE dbo.DataSets ALTER COLUMN ProcessType NVARCHAR(100) NOT NULL;
        
        PRINT '  ✓ ProcessType カラムの追加完了（段階的制約適用）';
    END
    ELSE
    BEGIN
        PRINT '  - ProcessType カラムは既に存在します';
        -- 既存のProcessTypeカラムがNULLの場合はデフォルト値を設定
        UPDATE dbo.DataSets 
        SET ProcessType = CASE 
            WHEN DataSetType IS NOT NULL AND DataSetType != '' THEN DataSetType
            ELSE 'Unknown'
        END
        WHERE ProcessType IS NULL OR ProcessType = '';
        PRINT '  ✓ 既存のNULL/空のProcessTypeに値を設定しました';
    END

    -- ----------------------------------------------------------------------------
    -- Step 2: 日付カラムの安全な移行（CreatedDate -> CreatedAt）
    -- ----------------------------------------------------------------------------
    PRINT 'Step 2: CreatedDate から CreatedAt への移行...';

    -- 古いカラム(CreatedDate)が存在し、新しいカラム(CreatedAt)が存在しない場合のみ移行
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'CreatedDate')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'CreatedAt')
    BEGIN
        PRINT '  - CreatedAt カラムを追加中（DATETIME2、NULL許容）...';
        ALTER TABLE dbo.DataSets ADD CreatedAt DATETIME2(7) NULL;
        
        PRINT '  - CreatedDate から CreatedAt へデータを移行中...';
        UPDATE dbo.DataSets 
        SET CreatedAt = CONVERT(DATETIME2(7), CreatedDate) 
        WHERE CreatedDate IS NOT NULL AND CreatedAt IS NULL;
        
        PRINT '  - CreatedAt の残りのNULL値を現在時刻で埋めています...';
        UPDATE dbo.DataSets 
        SET CreatedAt = SYSUTCDATETIME() 
        WHERE CreatedAt IS NULL;
        
        PRINT '  - CreatedAt にNOT NULL制約を適用中...';
        ALTER TABLE dbo.DataSets ALTER COLUMN CreatedAt DATETIME2(7) NOT NULL;
        
        PRINT '  ✓ CreatedDate から CreatedAt への移行完了';
    END
    ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'CreatedAt')
    BEGIN
        PRINT '  - CreatedAt カラムを新規作成中...';
        ALTER TABLE dbo.DataSets 
        ADD CreatedAt DATETIME2(7) NOT NULL 
        CONSTRAINT DF_DataSets_CreatedAt DEFAULT SYSUTCDATETIME();
        PRINT '  ✓ CreatedAt カラムの新規作成完了';
    END
    ELSE
    BEGIN
        PRINT '  - CreatedAt カラムは既に存在します';
    END

    -- ----------------------------------------------------------------------------
    -- Step 3: 日付カラムの安全な移行（UpdatedDate -> UpdatedAt）
    -- ----------------------------------------------------------------------------
    PRINT 'Step 3: UpdatedDate から UpdatedAt への移行...';

    -- 古いカラム(UpdatedDate)が存在し、新しいカラム(UpdatedAt)が存在しない場合のみ移行
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'UpdatedDate')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'UpdatedAt')
    BEGIN
        PRINT '  - UpdatedAt カラムを追加中（DATETIME2、NULL許容）...';
        ALTER TABLE dbo.DataSets ADD UpdatedAt DATETIME2(7) NULL;
        
        PRINT '  - UpdatedDate から UpdatedAt へデータを移行中...';
        UPDATE dbo.DataSets 
        SET UpdatedAt = CONVERT(DATETIME2(7), UpdatedDate) 
        WHERE UpdatedDate IS NOT NULL AND UpdatedAt IS NULL;
        
        PRINT '  - UpdatedAt の残りのNULL値を現在時刻で埋めています...';
        UPDATE dbo.DataSets 
        SET UpdatedAt = SYSUTCDATETIME() 
        WHERE UpdatedAt IS NULL;
        
        PRINT '  - UpdatedAt にNOT NULL制約を適用中...';
        ALTER TABLE dbo.DataSets ALTER COLUMN UpdatedAt DATETIME2(7) NOT NULL;
        
        PRINT '  ✓ UpdatedDate から UpdatedAt への移行完了';
    END
    ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'UpdatedAt')
    BEGIN
        PRINT '  - UpdatedAt カラムを新規作成中...';
        ALTER TABLE dbo.DataSets 
        ADD UpdatedAt DATETIME2(7) NOT NULL 
        CONSTRAINT DF_DataSets_UpdatedAt DEFAULT SYSUTCDATETIME();
        PRINT '  ✓ UpdatedAt カラムの新規作成完了';
    END
    ELSE
    BEGIN
        PRINT '  - UpdatedAt カラムは既に存在します';
    END

    -- ----------------------------------------------------------------------------
    -- Step 4: インデックスの作成
    -- ----------------------------------------------------------------------------
    PRINT 'Step 4: インデックスの作成...';

    -- ProcessType用インデックス
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'IX_DataSets_ProcessType')
    BEGIN
        CREATE NONCLUSTERED INDEX IX_DataSets_ProcessType 
        ON dbo.DataSets(ProcessType) 
        INCLUDE (JobDate, Status);
        PRINT '  ✓ ProcessType インデックスを作成しました';
    END
    ELSE
    BEGIN
        PRINT '  - ProcessType インデックスは既に存在します';
    END

    -- Name用インデックス
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.DataSets') AND name = 'IX_DataSets_Name')
    BEGIN
        CREATE NONCLUSTERED INDEX IX_DataSets_Name 
        ON dbo.DataSets(Name);
        PRINT '  ✓ Name インデックスを作成しました';
    END
    ELSE
    BEGIN
        PRINT '  - Name インデックスは既に存在します';
    END

    -- ----------------------------------------------------------------------------
    -- Step 5: データ整合性チェックと統計情報
    -- ----------------------------------------------------------------------------
    PRINT 'Step 5: データ整合性チェック...';

    DECLARE @TotalCount INT, @NullNameCount INT, @NullProcessTypeCount INT, @NullCreatedAtCount INT, @NullUpdatedAtCount INT;
    
    SELECT @TotalCount = COUNT(*) FROM dbo.DataSets;
    SELECT @NullNameCount = COUNT(*) FROM dbo.DataSets WHERE Name IS NULL OR Name = '';
    SELECT @NullProcessTypeCount = COUNT(*) FROM dbo.DataSets WHERE ProcessType IS NULL OR ProcessType = '';
    SELECT @NullCreatedAtCount = COUNT(*) FROM dbo.DataSets WHERE CreatedAt IS NULL;
    SELECT @NullUpdatedAtCount = COUNT(*) FROM dbo.DataSets WHERE UpdatedAt IS NULL;

    PRINT CONCAT('  - 総レコード数: ', @TotalCount);
    PRINT CONCAT('  - NULL/空のName: ', @NullNameCount);
    PRINT CONCAT('  - NULL/空のProcessType: ', @NullProcessTypeCount);
    PRINT CONCAT('  - NULL CreatedAt: ', @NullCreatedAtCount);
    PRINT CONCAT('  - NULL UpdatedAt: ', @NullUpdatedAtCount);

    -- 重要なNULL値チェック
    IF @NullNameCount > 0 OR @NullProcessTypeCount > 0 OR @NullCreatedAtCount > 0 OR @NullUpdatedAtCount > 0
    BEGIN
        PRINT 'エラー: 必須カラムにNULL値が残っています。';
        THROW 50001, 'Data integrity check failed: NULL values found in required columns.', 1;
    END

    -- ----------------------------------------------------------------------------
    -- Step 6: 修正完了の確認とスキーマ情報出力
    -- ----------------------------------------------------------------------------
    PRINT 'Step 6: 修正完了の確認...';

    -- 最終的なスキーマ情報を出力（確認用）
    PRINT '  - 最終的なDataSetsテーブル構造:';
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        CHARACTER_MAXIMUM_LENGTH,
        IS_NULLABLE,
        COLUMN_DEFAULT
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'DataSets' AND TABLE_SCHEMA = 'dbo'
    ORDER BY ORDINAL_POSITION;

    -- すべて成功した場合、トランザクションをコミット
    COMMIT TRANSACTION;

    PRINT '';
    PRINT '✅ DataSetsテーブルの修正が正常に完了しました';
    PRINT 'Migration 033: DataSetsテーブルスキーマ修正完了';

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
PRINT '=== 033_FixDataSetsSchema.sql 実行完了 ===';
GO