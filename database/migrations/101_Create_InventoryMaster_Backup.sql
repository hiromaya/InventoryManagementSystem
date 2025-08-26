-- =============================================
-- InventoryMaster バックアップテーブル作成スクリプト（最終版）
-- 作成日: 2025-07-30
-- 目的: 主キー変更前の既存データを保存
-- 修正内容: デバッグ版で確認した正しい動的SQL構築方法を適用
-- =============================================

USE InventoryManagementDB;
GO

PRINT '========== InventoryMaster バックアップテーブル作成開始 ==========';
PRINT '';

-- 1. バックアップテーブルの存在確認と削除
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'InventoryMaster_Backup_20250720')
BEGIN
    PRINT '既存のバックアップテーブルを削除します...';
    DROP TABLE InventoryMaster_Backup_20250720;
END
GO

-- 2. バックアップテーブルの作成
PRINT 'バックアップテーブルを作成します...';

CREATE TABLE InventoryMaster_Backup_20250720 (
    ProductCode NVARCHAR(15) NOT NULL,
    GradeCode NVARCHAR(15) NOT NULL,
    ClassCode NVARCHAR(15) NOT NULL,
    ShippingMarkCode NVARCHAR(15) NOT NULL,
    ManualShippingMark NVARCHAR(50) NOT NULL,
    ProductName NVARCHAR(100) NOT NULL,
    Unit NVARCHAR(20) NOT NULL,
    StandardPrice DECIMAL(18,4) NOT NULL,
    ProductCategory1 NVARCHAR(10) NOT NULL,
    ProductCategory2 NVARCHAR(10) NOT NULL,
    JobDate DATE NOT NULL,
    CreatedDate DATETIME2 NOT NULL,
    UpdatedDate DATETIME2 NOT NULL,
    CurrentStock DECIMAL(18,4) NOT NULL,
    CurrentStockAmount DECIMAL(18,4) NOT NULL,
    DailyStock DECIMAL(18,4) NOT NULL,
    DailyStockAmount DECIMAL(18,4) NOT NULL,
    DailyFlag CHAR(1) NOT NULL,
    DataSetId NVARCHAR(50) NOT NULL,
    -- 粗利計算関連カラム（デフォルト値付き）
    DailyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,
    DailyAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
    DailyProcessingCost DECIMAL(18,4) NOT NULL DEFAULT 0,
    FinalGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- 前月繰越関連カラム（デフォルト値付き）
    PreviousMonthQuantity DECIMAL(18,4) NOT NULL DEFAULT 0,
    PreviousMonthAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
    -- バックアップ情報
    BackupDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    BackupReason NVARCHAR(200) NOT NULL DEFAULT 'Primary Key Change - 6 items to 5 items migration'
);
GO

-- 3. 既存データのコピー
PRINT '既存データをバックアップテーブルにコピーします...';

DECLARE @sql NVARCHAR(MAX);

-- 固定のINSERT文とSELECT文を構築
SET @sql = '
INSERT INTO InventoryMaster_Backup_20250720 (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
    JobDate, CreatedDate, UpdatedDate, CurrentStock, CurrentStockAmount,
    DailyStock, DailyStockAmount, DailyFlag, DataSetId,
    DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,
    PreviousMonthQuantity, PreviousMonthAmount
)
SELECT 
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark,
    ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
    JobDate, CreatedDate, UpdatedDate, CurrentStock, CurrentStockAmount,
    DailyStock, DailyStockAmount, DailyFlag, DataSetId,';

-- 粗利計算カラムの処理
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'DailyGrossProfit')
BEGIN
    SET @sql = @sql + '
    DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit,';
END
ELSE
BEGIN
    SET @sql = @sql + '
    0, 0, 0, 0,';
END

-- 前月繰越カラムの処理
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'PreviousMonthQuantity')
BEGIN
    SET @sql = @sql + '
    PreviousMonthQuantity, PreviousMonthAmount';
END
ELSE
BEGIN
    SET @sql = @sql + '
    0, 0';
END

SET @sql = @sql + '
FROM InventoryMaster;';

-- 動的SQLの実行
EXEC sp_executesql @sql;
GO

-- 4. バックアップテーブルのインデックス作成
PRINT 'バックアップテーブルのインデックスを作成します...';

-- 元の主キーと同じインデックスを作成（バックアップ用）
CREATE NONCLUSTERED INDEX IX_InventoryMaster_Backup_OriginalPK
ON InventoryMaster_Backup_20250720 (
    ProductCode, 
    GradeCode, 
    ClassCode, 
    ShippingMarkCode, 
    ManualShippingMark, 
    JobDate
);
GO

-- JobDate単独のインデックス（検索用）
CREATE NONCLUSTERED INDEX IX_InventoryMaster_Backup_JobDate
ON InventoryMaster_Backup_20250720 (JobDate);
GO

-- 5. バックアップ結果の確認
PRINT '';
PRINT '========== バックアップ結果 ==========';

-- レコード数の確認
DECLARE @BackupCount INT;
DECLARE @OriginalCount INT;

SELECT @BackupCount = COUNT(*) FROM InventoryMaster_Backup_20250720;
SELECT @OriginalCount = COUNT(*) FROM InventoryMaster;

PRINT 'オリジナルテーブルのレコード数: ' + CAST(@OriginalCount AS NVARCHAR(20));
PRINT 'バックアップテーブルのレコード数: ' + CAST(@BackupCount AS NVARCHAR(20));

IF @BackupCount = @OriginalCount
BEGIN
    PRINT '✅ バックアップ成功: すべてのレコードがバックアップされました';
END
ELSE
BEGIN
    PRINT '❌ エラー: バックアップレコード数が一致しません';
END

-- JobDate範囲の確認（データがある場合のみ）
IF @BackupCount > 0
BEGIN
    PRINT '';
    PRINT 'バックアップデータのJobDate範囲:';
    SELECT 
        MIN(JobDate) as MinJobDate,
        MAX(JobDate) as MaxJobDate,
        COUNT(DISTINCT JobDate) as UniqueJobDates
    FROM InventoryMaster_Backup_20250720;
END

PRINT '';
PRINT '========== バックアップテーブル作成完了 ==========';
PRINT '';
PRINT '【次のステップ】';
PRINT '1. バックアップデータの整合性を確認';
PRINT '2. マイグレーションスクリプト（102_Migrate_InventoryMaster_PK.sql）を実行';
PRINT '3. アプリケーションコードの修正を適用';
GO

-- 6. 復元用ビューの作成（必要時にバックアップデータを参照）
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_InventoryMaster_History')
BEGIN
    DROP VIEW vw_InventoryMaster_History;
END
GO

CREATE VIEW vw_InventoryMaster_History
AS
SELECT 
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ManualShippingMark,
    JobDate,
    ProductName,
    Unit,
    StandardPrice,
    ProductCategory1,
    ProductCategory2,
    CurrentStock,
    CurrentStockAmount,
    DailyStock,
    DailyStockAmount,
    DailyFlag,
    DailyGrossProfit,
    DailyAdjustmentAmount,
    DailyProcessingCost,
    FinalGrossProfit,
    DataSetId,
    CreatedDate,
    UpdatedDate,
    PreviousMonthQuantity,
    PreviousMonthAmount,
    BackupDate,
    'Backup_20250720' as Source
FROM InventoryMaster_Backup_20250720;
GO

PRINT 'バックアップ参照用ビュー（vw_InventoryMaster_History）を作成しました';
PRINT '履歴データが必要な場合はこのビューを使用してください';
GO