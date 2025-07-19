-- =============================================
-- InventoryMaster バックアップテーブル作成スクリプト
-- 作成日: 2025-07-20
-- 目的: 主キー変更前の既存データを保存
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

-- 2. バックアップテーブルの作成（既存テーブルの完全コピー）
PRINT 'バックアップテーブルを作成します...';

-- 既存のInventoryMasterテーブルから構造とデータをコピー
SELECT * 
INTO InventoryMaster_Backup_20250720
FROM InventoryMaster;
GO

-- 3. バックアップテーブルの情報を追加
ALTER TABLE InventoryMaster_Backup_20250720
ADD 
    BackupDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    BackupReason NVARCHAR(200) NOT NULL DEFAULT 'Primary Key Change - 6 items to 5 items migration';
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
    ShippingMarkName, 
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

-- JobDate範囲の確認
PRINT '';
PRINT 'バックアップデータのJobDate範囲:';
SELECT 
    MIN(JobDate) as MinJobDate,
    MAX(JobDate) as MaxJobDate,
    COUNT(DISTINCT JobDate) as UniqueJobDates
FROM InventoryMaster_Backup_20250720;

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
    ShippingMarkName,
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