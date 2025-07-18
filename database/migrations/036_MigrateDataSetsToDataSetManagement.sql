-- 036_MigrateDataSetsToDataSetManagement.sql
-- DataSetsからDataSetManagementへの完全統合マイグレーション
-- 実行日: 2025-07-18

PRINT '================================';
PRINT 'DataSetManagement統合マイグレーション開始';
PRINT '================================';
PRINT '';

-- ========================================
-- Phase 1: データ型の拡張
-- ========================================
PRINT 'Phase 1: データ型の拡張開始...';

-- SalesVouchersのDataSetId拡張
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'SalesVouchers' 
           AND COLUMN_NAME = 'DataSetId' 
           AND CHARACTER_MAXIMUM_LENGTH = 50)
BEGIN
    -- 外部キー制約を一時的に削除
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_SalesVouchers_DataSets')
    BEGIN
        ALTER TABLE SalesVouchers DROP CONSTRAINT FK_SalesVouchers_DataSets;
        PRINT '✓ FK_SalesVouchers_DataSets制約を削除しました';
    END
    
    -- データ型を拡張
    ALTER TABLE SalesVouchers ALTER COLUMN DataSetId NVARCHAR(100) NOT NULL;
    PRINT '✓ SalesVouchers.DataSetIdをNVARCHAR(100)に拡張しました';
END

-- PurchaseVouchersのDataSetId拡張
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'PurchaseVouchers' 
           AND COLUMN_NAME = 'DataSetId' 
           AND CHARACTER_MAXIMUM_LENGTH = 50)
BEGIN
    -- 外部キー制約を一時的に削除
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PurchaseVouchers_DataSets')
    BEGIN
        ALTER TABLE PurchaseVouchers DROP CONSTRAINT FK_PurchaseVouchers_DataSets;
        PRINT '✓ FK_PurchaseVouchers_DataSets制約を削除しました';
    END
    
    -- データ型を拡張
    ALTER TABLE PurchaseVouchers ALTER COLUMN DataSetId NVARCHAR(100) NOT NULL;
    PRINT '✓ PurchaseVouchers.DataSetIdをNVARCHAR(100)に拡張しました';
END

-- InventoryAdjustmentsのDataSetId拡張
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'InventoryAdjustments' 
           AND COLUMN_NAME = 'DataSetId' 
           AND CHARACTER_MAXIMUM_LENGTH = 50)
BEGIN
    -- 外部キー制約を一時的に削除
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_InventoryAdjustments_DataSets')
    BEGIN
        ALTER TABLE InventoryAdjustments DROP CONSTRAINT FK_InventoryAdjustments_DataSets;
        PRINT '✓ FK_InventoryAdjustments_DataSets制約を削除しました';
    END
    
    -- データ型を拡張
    ALTER TABLE InventoryAdjustments ALTER COLUMN DataSetId NVARCHAR(100) NOT NULL;
    PRINT '✓ InventoryAdjustments.DataSetIdをNVARCHAR(100)に拡張しました';
END

-- ========================================
-- Phase 2: DataSetManagementへの追加カラム
-- ========================================
PRINT '';
PRINT 'Phase 2: DataSetManagementへの追加カラム作成...';

-- DataSetTypeカラムの追加（DataSetsから移行）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'DataSetManagement' 
               AND COLUMN_NAME = 'DataSetType')
BEGIN
    ALTER TABLE DataSetManagement ADD DataSetType NVARCHAR(20) NULL;
    PRINT '✓ DataSetManagement.DataSetTypeカラムを追加しました';
END

-- Nameカラムの追加（DataSetsから移行）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'DataSetManagement' 
               AND COLUMN_NAME = 'Name')
BEGIN
    ALTER TABLE DataSetManagement ADD Name NVARCHAR(255) NULL;
    PRINT '✓ DataSetManagement.Nameカラムを追加しました';
END

-- Descriptionカラムの追加（DataSetsから移行）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'DataSetManagement' 
               AND COLUMN_NAME = 'Description')
BEGIN
    ALTER TABLE DataSetManagement ADD Description NVARCHAR(MAX) NULL;
    PRINT '✓ DataSetManagement.Descriptionカラムを追加しました';
END

-- ErrorMessageカラムの追加（DataSetsから移行）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'DataSetManagement' 
               AND COLUMN_NAME = 'ErrorMessage')
BEGIN
    ALTER TABLE DataSetManagement ADD ErrorMessage NVARCHAR(MAX) NULL;
    PRINT '✓ DataSetManagement.ErrorMessageカラムを追加しました';
END

-- FilePathカラムの追加（DataSetsから移行）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'DataSetManagement' 
               AND COLUMN_NAME = 'FilePath')
BEGIN
    ALTER TABLE DataSetManagement ADD FilePath NVARCHAR(500) NULL;
    PRINT '✓ DataSetManagement.FilePathカラムを追加しました';
END

-- Statusカラムの追加（互換性のため）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'DataSetManagement' 
               AND COLUMN_NAME = 'Status')
BEGIN
    ALTER TABLE DataSetManagement ADD Status NVARCHAR(20) NULL;
    PRINT '✓ DataSetManagement.Statusカラムを追加しました';
END

-- UpdatedAtカラムの追加（DataSetsから移行）
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'DataSetManagement' 
               AND COLUMN_NAME = 'UpdatedAt')
BEGIN
    ALTER TABLE DataSetManagement ADD UpdatedAt DATETIME2 NULL;
    PRINT '✓ DataSetManagement.UpdatedAtカラムを追加しました';
END

-- ========================================
-- Phase 3: データ移行
-- ========================================
PRINT '';
PRINT 'Phase 3: DataSetsからDataSetManagementへのデータ移行...';

-- 移行前のレコード数を記録
DECLARE @DataSetsCount INT;
DECLARE @ExistingCount INT;
DECLARE @MigratedCount INT;

SELECT @DataSetsCount = COUNT(*) FROM DataSets;
SELECT @ExistingCount = COUNT(*) FROM DataSetManagement WHERE DataSetId IN (SELECT Id FROM DataSets);

PRINT CONCAT('DataSetsレコード数: ', @DataSetsCount);
PRINT CONCAT('既存のDataSetManagementレコード数: ', @ExistingCount);

-- データ移行（重複を避ける）
INSERT INTO DataSetManagement (
    DataSetId, 
    JobDate, 
    ProcessType, 
    ImportType, 
    RecordCount, 
    TotalRecordCount,
    IsActive, 
    IsArchived, 
    CreatedAt, 
    CreatedBy, 
    Department, 
    Notes,
    ErrorMessage,
    FilePath,
    Status,
    UpdatedAt,
    DataSetType,
    Name,
    Description
)
SELECT 
    ds.Id AS DataSetId,
    ds.JobDate,
    CASE 
        WHEN ds.ProcessType IS NOT NULL AND ds.ProcessType != '' THEN ds.ProcessType
        WHEN ds.DataSetType IS NOT NULL AND ds.DataSetType != '' THEN ds.DataSetType
        ELSE 'UNKNOWN'
    END AS ProcessType,
    'LEGACY' AS ImportType,
    ISNULL(ds.RecordCount, 0) AS RecordCount,
    ISNULL(ds.RecordCount, 0) AS TotalRecordCount,
    CASE 
        WHEN ds.Status IN ('Completed', 'Imported') THEN 1 
        ELSE 0 
    END AS IsActive,
    CASE 
        WHEN ds.Status = 'Error' THEN 1 
        ELSE 0 
    END AS IsArchived,
    ds.CreatedAt,
    'migration' AS CreatedBy,
    'DeptA' AS Department,
    CASE 
        WHEN ds.Name IS NOT NULL AND ds.Description IS NOT NULL 
            THEN CONCAT('Name: ', ds.Name, CHAR(13) + CHAR(10), 'Description: ', ds.Description)
        WHEN ds.Name IS NOT NULL 
            THEN CONCAT('Name: ', ds.Name)
        WHEN ds.Description IS NOT NULL 
            THEN CONCAT('Description: ', ds.Description)
        ELSE NULL
    END AS Notes,
    ds.ErrorMessage,
    ds.FilePath,
    ds.Status,
    ds.UpdatedAt,
    ds.DataSetType,
    ds.Name,
    ds.Description
FROM DataSets ds
WHERE NOT EXISTS (
    SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = ds.Id
);

SET @MigratedCount = @@ROWCOUNT;
PRINT CONCAT('✓ ', @MigratedCount, '件のレコードを移行しました');

-- ArchivedAtの設定（エラーステータスの場合）
UPDATE DataSetManagement
SET ArchivedAt = CreatedAt,
    ArchivedBy = 'migration'
WHERE Status = 'Error' AND ArchivedAt IS NULL;

PRINT '✓ エラーステータスのレコードにArchivedAt情報を設定しました';

-- ========================================
-- Phase 4: 外部キー制約の再作成
-- ========================================
PRINT '';
PRINT 'Phase 4: 外部キー制約の再作成...';

-- SalesVouchersの外部キー制約
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_SalesVouchers_DataSetManagement')
BEGIN
    ALTER TABLE SalesVouchers WITH NOCHECK 
    ADD CONSTRAINT FK_SalesVouchers_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);
    
    -- 制約を有効化
    ALTER TABLE SalesVouchers CHECK CONSTRAINT FK_SalesVouchers_DataSetManagement;
    PRINT '✓ FK_SalesVouchers_DataSetManagement制約を作成しました';
END

-- PurchaseVouchersの外部キー制約
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PurchaseVouchers_DataSetManagement')
BEGIN
    ALTER TABLE PurchaseVouchers WITH NOCHECK 
    ADD CONSTRAINT FK_PurchaseVouchers_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);
    
    -- 制約を有効化
    ALTER TABLE PurchaseVouchers CHECK CONSTRAINT FK_PurchaseVouchers_DataSetManagement;
    PRINT '✓ FK_PurchaseVouchers_DataSetManagement制約を作成しました';
END

-- InventoryAdjustmentsの外部キー制約
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_InventoryAdjustments_DataSetManagement')
BEGIN
    ALTER TABLE InventoryAdjustments WITH NOCHECK 
    ADD CONSTRAINT FK_InventoryAdjustments_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);
    
    -- 制約を有効化
    ALTER TABLE InventoryAdjustments CHECK CONSTRAINT FK_InventoryAdjustments_DataSetManagement;
    PRINT '✓ FK_InventoryAdjustments_DataSetManagement制約を作成しました';
END

-- ========================================
-- Phase 5: インデックスの作成
-- ========================================
PRINT '';
PRINT 'Phase 5: パフォーマンス最適化インデックスの作成...';

-- Statusインデックス（互換性のため）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DataSetManagement_Status')
BEGIN
    CREATE INDEX IX_DataSetManagement_Status ON DataSetManagement(Status);
    PRINT '✓ IX_DataSetManagement_Statusインデックスを作成しました';
END

-- ProcessType + JobDateの複合インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DataSetManagement_ProcessType_JobDate')
BEGIN
    CREATE INDEX IX_DataSetManagement_ProcessType_JobDate ON DataSetManagement(ProcessType, JobDate);
    PRINT '✓ IX_DataSetManagement_ProcessType_JobDateインデックスを作成しました';
END

-- DataSetType + JobDateの複合インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DataSetManagement_DataSetType_JobDate')
BEGIN
    CREATE INDEX IX_DataSetManagement_DataSetType_JobDate ON DataSetManagement(DataSetType, JobDate);
    PRINT '✓ IX_DataSetManagement_DataSetType_JobDateインデックスを作成しました';
END

-- ========================================
-- 移行結果の検証
-- ========================================
PRINT '';
PRINT '================================';
PRINT '移行結果の検証';
PRINT '================================';

-- 外部キー整合性チェック
DECLARE @OrphanSales INT;
DECLARE @OrphanPurchase INT;
DECLARE @OrphanAdjustment INT;

SELECT @OrphanSales = COUNT(*) 
FROM SalesVouchers sv
WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = sv.DataSetId);

SELECT @OrphanPurchase = COUNT(*) 
FROM PurchaseVouchers pv
WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = pv.DataSetId);

SELECT @OrphanAdjustment = COUNT(*) 
FROM InventoryAdjustments ia
WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = ia.DataSetId);

PRINT CONCAT('孤立した売上伝票: ', @OrphanSales, '件');
PRINT CONCAT('孤立した仕入伝票: ', @OrphanPurchase, '件');
PRINT CONCAT('孤立した在庫調整: ', @OrphanAdjustment, '件');

IF @OrphanSales = 0 AND @OrphanPurchase = 0 AND @OrphanAdjustment = 0
BEGIN
    PRINT '';
    PRINT '✓ すべての外部キー参照が正常です';
END
ELSE
BEGIN
    PRINT '';
    PRINT '⚠️ 警告: 孤立したレコードが存在します。確認が必要です。';
END

-- 最終的な統計情報
PRINT '';
PRINT '================================';
PRINT '最終統計情報';
PRINT '================================';

SELECT 
    'DataSets' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT ProcessType) as ProcessTypes,
    COUNT(DISTINCT Status) as StatusTypes
FROM DataSets

UNION ALL

SELECT 
    'DataSetManagement' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT ProcessType) as ProcessTypes,
    COUNT(DISTINCT Status) as StatusTypes
FROM DataSetManagement;

PRINT '';
PRINT '================================';
PRINT 'DataSetManagement統合マイグレーション完了';
PRINT '================================';
PRINT '';
PRINT '注意: DataSetsテーブルはまだ削除されていません。';
PRINT 'アプリケーションの動作確認後、別途削除してください。';
PRINT '';
PRINT '次のステップ:';
PRINT '1. アプリケーションのフィーチャーフラグを有効化';
PRINT '2. 十分なテスト期間（1-2週間）を設ける';
PRINT '3. 問題がなければDataSetsテーブルを削除';
GO