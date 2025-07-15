-- =============================================================================
-- DataSetManagementテーブルのデバッグ用スクリプト
-- 作成日: 2025-07-15
-- 用途: マイグレーション前後のテーブル構造確認
-- =============================================================================

PRINT '===== DataSetManagementテーブルの構造確認 =====';

-- 1. テーブルの存在確認
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DataSetManagement')
BEGIN
    PRINT 'DataSetManagementテーブルが存在します。';
    
    -- 2. カラム一覧の表示
    PRINT '';
    PRINT '=== カラム一覧 ===';
    SELECT 
        COLUMN_NAME as カラム名,
        DATA_TYPE as データ型,
        IS_NULLABLE as NULL許可,
        COLUMN_DEFAULT as デフォルト値,
        ORDINAL_POSITION as 順序
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DataSetManagement'
    ORDER BY ORDINAL_POSITION;
    
    -- 3. 必要なカラムの存在確認
    PRINT '';
    PRINT '=== 必要カラムの存在確認 ===';
    
    DECLARE @ProcessType BIT = 0;
    DECLARE @TotalRecordCount BIT = 0;
    DECLARE @Department BIT = 0;
    DECLARE @ImportedFiles BIT = 0;
    DECLARE @UpdatedAt BIT = 0;
    
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') AND name = 'ProcessType')
        SET @ProcessType = 1;
    
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') AND name = 'TotalRecordCount')
        SET @TotalRecordCount = 1;
    
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') AND name = 'Department')
        SET @Department = 1;
    
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') AND name = 'ImportedFiles')
        SET @ImportedFiles = 1;
    
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') AND name = 'UpdatedAt')
        SET @UpdatedAt = 1;
    
    SELECT 
        'ProcessType' as カラム名,
        CASE WHEN @ProcessType = 1 THEN '存在' ELSE '不存在' END as 状態
    UNION ALL
    SELECT 
        'TotalRecordCount',
        CASE WHEN @TotalRecordCount = 1 THEN '存在' ELSE '不存在' END
    UNION ALL
    SELECT 
        'Department',
        CASE WHEN @Department = 1 THEN '存在' ELSE '不存在' END
    UNION ALL
    SELECT 
        'ImportedFiles',
        CASE WHEN @ImportedFiles = 1 THEN '存在' ELSE '不存在' END
    UNION ALL
    SELECT 
        'UpdatedAt',
        CASE WHEN @UpdatedAt = 1 THEN '存在' ELSE '不存在' END;
    
    -- 4. 既存データのサンプル確認
    PRINT '';
    PRINT '=== 既存データのサンプル（最新5件）===';
    
    SELECT TOP 5 
        DataSetId,
        JobDate,
        ProcessType,
        ImportType,
        RecordCount,
        TotalRecordCount,
        Department,
        CreatedAt,
        CreatedBy
    FROM DataSetManagement
    ORDER BY CreatedAt DESC;
    
    -- 5. データ統計
    PRINT '';
    PRINT '=== データ統計 ===';
    
    SELECT 
        COUNT(*) as 総レコード数,
        SUM(CASE WHEN ImportType = 'INIT' THEN 1 ELSE 0 END) as INIT件数,
        SUM(CASE WHEN ImportType = 'IMPORT' THEN 1 ELSE 0 END) as IMPORT件数,
        SUM(CASE WHEN ImportType = 'CARRYOVER' THEN 1 ELSE 0 END) as CARRYOVER件数,
        SUM(CASE WHEN ProcessType = 'INITIAL_INVENTORY' THEN 1 ELSE 0 END) as 初期在庫件数,
        SUM(CASE WHEN ProcessType = 'IMPORT' THEN 1 ELSE 0 END) as インポート件数,
        SUM(CASE WHEN ProcessType = 'CARRYOVER' THEN 1 ELSE 0 END) as 繰越件数
    FROM DataSetManagement;
    
END
ELSE
BEGIN
    PRINT 'DataSetManagementテーブルが存在しません。';
END

PRINT '';
PRINT '===== デバッグ完了 =====';