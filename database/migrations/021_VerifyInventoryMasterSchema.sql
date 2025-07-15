-- =====================================================
-- スクリプト名: VerifyInventoryMasterSchema.sql
-- 説明: InventoryMasterテーブルのスキーマを検証
-- 作成日: 2025-01-30
-- =====================================================

USE InventoryManagementDB;
GO

PRINT '===== InventoryMasterテーブル スキーマ検証開始 =====';
PRINT '';

-- 必須カラムのリスト
DECLARE @RequiredColumns TABLE (
    ColumnName NVARCHAR(100),
    DataType NVARCHAR(50),
    MaxLength INT,
    IsNullable BIT,
    DefaultValue NVARCHAR(100),
    Status NVARCHAR(20) DEFAULT 'NOT CHECKED'
);

-- 必須カラムの定義
INSERT INTO @RequiredColumns (ColumnName, DataType, MaxLength, IsNullable, DefaultValue)
VALUES 
    ('ProductCode', 'nvarchar', 15, 0, NULL),
    ('GradeCode', 'nvarchar', 15, 0, NULL),
    ('ClassCode', 'nvarchar', 15, 0, NULL),
    ('ShippingMarkCode', 'nvarchar', 15, 0, NULL),
    ('ShippingMarkName', 'nvarchar', 50, 0, NULL),
    ('ProductName', 'nvarchar', 100, 0, NULL),
    ('Unit', 'nvarchar', 10, 0, NULL),
    ('StandardPrice', 'decimal', NULL, 0, '((0))'),
    ('ProductCategory1', 'nvarchar', 10, 0, '('''')'),
    ('ProductCategory2', 'nvarchar', 10, 0, '('''')'),
    ('JobDate', 'date', NULL, 0, NULL),
    ('CreatedDate', 'datetime2', NULL, 0, '(getdate())'),
    ('UpdatedDate', 'datetime2', NULL, 0, '(getdate())'),
    ('CurrentStock', 'decimal', NULL, 0, '((0))'),
    ('CurrentStockAmount', 'decimal', NULL, 0, '((0))'),
    ('DailyStock', 'decimal', NULL, 0, '((0))'),
    ('DailyStockAmount', 'decimal', NULL, 0, '((0))'),
    ('DailyFlag', 'nchar', 1, 0, '(''9'')'),
    ('DataSetId', 'nvarchar', 50, 0, '('''')'),
    ('DailyGrossProfit', 'decimal', NULL, 0, '((0))'),
    ('DailyAdjustmentAmount', 'decimal', NULL, 0, '((0))'),
    ('DailyProcessingCost', 'decimal', NULL, 0, '((0))'),
    ('FinalGrossProfit', 'decimal', NULL, 0, '((0))'),
    ('PreviousMonthQuantity', 'decimal', NULL, 0, '((0))'),
    ('PreviousMonthAmount', 'decimal', NULL, 0, '((0))');

-- 実際のカラム情報を確認
DECLARE @ActualColumns TABLE (
    ColumnName NVARCHAR(100),
    DataType NVARCHAR(50),
    MaxLength INT,
    IsNullable BIT,
    DefaultValue NVARCHAR(100)
);

INSERT INTO @ActualColumns
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventoryMaster';

-- 検証結果の更新
UPDATE rc
SET Status = CASE 
    WHEN ac.ColumnName IS NULL THEN 'MISSING'
    WHEN ac.DataType != rc.DataType THEN 'TYPE MISMATCH'
    WHEN rc.MaxLength IS NOT NULL AND ac.MaxLength != rc.MaxLength THEN 'LENGTH MISMATCH'
    WHEN ac.IsNullable != rc.IsNullable THEN 'NULLABLE MISMATCH'
    ELSE 'OK'
END
FROM @RequiredColumns rc
LEFT JOIN @ActualColumns ac ON rc.ColumnName = ac.ColumnName;

-- 結果の表示
PRINT '===== 必須カラムの検証結果 =====';
SELECT 
    ColumnName AS [カラム名],
    DataType AS [データ型],
    CASE WHEN MaxLength IS NOT NULL THEN CAST(MaxLength AS NVARCHAR(10)) ELSE 'N/A' END AS [最大長],
    CASE WHEN IsNullable = 0 THEN 'NOT NULL' ELSE 'NULL' END AS [NULL許可],
    ISNULL(DefaultValue, 'なし') AS [デフォルト値],
    Status AS [状態]
FROM @RequiredColumns
ORDER BY 
    CASE Status 
        WHEN 'MISSING' THEN 1
        WHEN 'TYPE MISMATCH' THEN 2
        WHEN 'LENGTH MISMATCH' THEN 3
        WHEN 'NULLABLE MISMATCH' THEN 4
        ELSE 5
    END,
    ColumnName;

-- サマリー
DECLARE @MissingCount INT = (SELECT COUNT(*) FROM @RequiredColumns WHERE Status = 'MISSING');
DECLARE @MismatchCount INT = (SELECT COUNT(*) FROM @RequiredColumns WHERE Status LIKE '%MISMATCH%');
DECLARE @OkCount INT = (SELECT COUNT(*) FROM @RequiredColumns WHERE Status = 'OK');
DECLARE @TotalCount INT = (SELECT COUNT(*) FROM @RequiredColumns);

PRINT '';
PRINT '===== 検証サマリー =====';
PRINT '総カラム数: ' + CAST(@TotalCount AS NVARCHAR(10));
PRINT '正常: ' + CAST(@OkCount AS NVARCHAR(10));
PRINT '不足: ' + CAST(@MissingCount AS NVARCHAR(10));
PRINT '不一致: ' + CAST(@MismatchCount AS NVARCHAR(10));
PRINT '';

IF @MissingCount = 0 AND @MismatchCount = 0
BEGIN
    PRINT '✓ 全てのカラムが正常に存在します。';
END
ELSE
BEGIN
    PRINT '✗ スキーマに問題があります。';
    PRINT '  AddMissingColumnsToInventoryMaster.sqlを実行してください。';
END

-- テーブルのレコード数も確認
DECLARE @RecordCount INT = (SELECT COUNT(*) FROM InventoryMaster);
PRINT '';
PRINT '===== テーブル情報 =====';
PRINT 'レコード数: ' + CAST(@RecordCount AS NVARCHAR(10));

-- 最新のレコードを表示（存在する場合）
IF @RecordCount > 0
BEGIN
    PRINT '';
    PRINT '===== 最新5件のレコード（ProductCode, ProductName, JobDate） =====';
    SELECT TOP 5 
        ProductCode,
        ProductName,
        CONVERT(NVARCHAR(10), JobDate, 120) AS JobDate,
        CONVERT(NVARCHAR(19), CreatedDate, 120) AS CreatedDate
    FROM InventoryMaster
    ORDER BY CreatedDate DESC;
END

PRINT '';
PRINT '===== スキーマ検証完了 =====';