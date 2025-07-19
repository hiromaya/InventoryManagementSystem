-- DataSetManagementテーブルのスキーマ詳細確認スクリプト
-- SqlDateTime overflow エラー調査用

PRINT '=== DataSetManagementテーブル スキーマ確認 ===';
PRINT '';

-- 1. テーブル存在確認
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DataSetManagement' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    PRINT '✓ DataSetManagementテーブルが存在します';
    PRINT '';
    
    -- 2. 全カラム情報の詳細表示
    PRINT '=== カラム詳細情報 ===';
    SELECT 
        c.COLUMN_NAME as カラム名,
        c.DATA_TYPE as データ型,
        c.IS_NULLABLE as NULL許可,
        c.COLUMN_DEFAULT as デフォルト値,
        c.CHARACTER_MAXIMUM_LENGTH as 最大長,
        t.name as SQL型名
    FROM INFORMATION_SCHEMA.COLUMNS c
    INNER JOIN sys.columns sc ON sc.object_id = OBJECT_ID('DataSetManagement') AND sc.name = c.COLUMN_NAME
    INNER JOIN sys.types t ON t.system_type_id = sc.system_type_id AND t.user_type_id = sc.user_type_id
    WHERE c.TABLE_NAME = 'DataSetManagement' AND c.TABLE_SCHEMA = 'dbo'
    ORDER BY c.ORDINAL_POSITION;
    
    PRINT '';
    
    -- 3. DateTime型カラムの特別確認
    PRINT '=== DateTime型カラムの詳細 ===';
    SELECT 
        c.COLUMN_NAME as カラム名,
        c.DATA_TYPE as データ型,
        t.name as SQL型名,
        c.IS_NULLABLE as NULL許可,
        c.COLUMN_DEFAULT as デフォルト値,
        CASE 
            WHEN t.name = 'datetime' THEN '1753-01-01 ～ 9999-12-31 (3.33ms精度)'
            WHEN t.name = 'datetime2' THEN '0001-01-01 ～ 9999-12-31 (100ns精度)'
            WHEN t.name = 'date' THEN '0001-01-01 ～ 9999-12-31 (日付のみ)'
            ELSE 'その他'
        END as 有効範囲
    FROM INFORMATION_SCHEMA.COLUMNS c
    INNER JOIN sys.columns sc ON sc.object_id = OBJECT_ID('DataSetManagement') AND sc.name = c.COLUMN_NAME
    INNER JOIN sys.types t ON t.system_type_id = sc.system_type_id AND t.user_type_id = sc.user_type_id
    WHERE c.TABLE_NAME = 'DataSetManagement' 
    AND c.TABLE_SCHEMA = 'dbo'
    AND t.name IN ('datetime', 'datetime2', 'date')
    ORDER BY c.COLUMN_NAME;
    
    PRINT '';
    
    -- 4. 制約情報
    PRINT '=== 制約情報 ===';
    
    -- デフォルト制約
    PRINT 'デフォルト制約:';
    SELECT 
        dc.name as 制約名,
        c.name as カラム名,
        dc.definition as デフォルト値
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('DataSetManagement')
    ORDER BY c.name;
    
    PRINT '';
    
    -- CHECK制約
    PRINT 'CHECK制約:';
    SELECT 
        cc.name as 制約名,
        cc.definition as 制約定義
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID('DataSetManagement')
    ORDER BY cc.name;
    
    PRINT '';
    
    -- 5. レコード数とサンプルデータ
    DECLARE @RecordCount INT;
    SELECT @RecordCount = COUNT(*) FROM DataSetManagement;
    PRINT CONCAT('=== データ確認 (総件数: ', @RecordCount, '件) ===');
    
    IF @RecordCount > 0
    BEGIN
        PRINT '最新5件のDateTime型フィールドサンプル:';
        SELECT TOP 5
            DataSetId,
            JobDate,
            CreatedAt,
            UpdatedAt,
            DeactivatedAt,
            ArchivedAt
        FROM DataSetManagement
        ORDER BY CreatedAt DESC;
    END
    ELSE
    BEGIN
        PRINT 'データがありません';
    END
    
    PRINT '';
    
    -- 6. 問題となりうるデータの確認
    PRINT '=== 潜在的問題データの確認 ===';
    
    -- DateTime.MinValue相当のデータ確認
    DECLARE @MinValueCount INT;
    SELECT @MinValueCount = COUNT(*) 
    FROM DataSetManagement 
    WHERE CreatedAt < '1753-01-01' OR UpdatedAt < '1753-01-01';
    
    PRINT CONCAT('1753年以前の日付を持つレコード: ', @MinValueCount, '件');
    
    IF @MinValueCount > 0
    BEGIN
        PRINT '問題のあるレコード:';
        SELECT DataSetId, CreatedAt, UpdatedAt
        FROM DataSetManagement 
        WHERE CreatedAt < '1753-01-01' OR UpdatedAt < '1753-01-01';
    END
    
END
ELSE
BEGIN
    PRINT '❌ DataSetManagementテーブルが存在しません';
END

PRINT '';
PRINT '=== 推奨アクション ===';
PRINT '1. DATETIME型のカラムがある場合: DATETIME2への変更を検討';
PRINT '2. NULL許可されていないDateTime型: nullable型への変更を検討'; 
PRINT '3. デフォルト制約なし: GETDATE()デフォルト値の追加を検討';
PRINT '4. 1753年以前のデータ: アプリケーション側でのDateTime.MinValue対策が必要';