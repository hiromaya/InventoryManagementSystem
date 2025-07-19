-- 036_MigrateDataSetsToDataSetManagement.sql
-- DataSetsからDataSetManagementへの完全統合マイグレーション
-- 実行日: 2025-07-18

PRINT '================================';
PRINT 'DataSetManagement統合マイグレーション開始';
PRINT '================================';
PRINT '';

-- ========================================
-- Phase 0: 必要なカラムの追加（最優先）
-- ========================================
PRINT 'Phase 0: DataSetManagementテーブルへの必要なカラム追加...';

-- DataSetsから移行するカラムを先に追加
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'DataSetType')
BEGIN
    ALTER TABLE DataSetManagement ADD DataSetType NVARCHAR(20) NULL;
    PRINT '✓ DataSetManagement.DataSetTypeカラムを追加しました';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'Name')
BEGIN
    ALTER TABLE DataSetManagement ADD Name NVARCHAR(255) NULL;
    PRINT '✓ DataSetManagement.Nameカラムを追加しました';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'Description')
BEGIN
    ALTER TABLE DataSetManagement ADD Description NVARCHAR(MAX) NULL;
    PRINT '✓ DataSetManagement.Descriptionカラムを追加しました';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'ErrorMessage')
BEGIN
    ALTER TABLE DataSetManagement ADD ErrorMessage NVARCHAR(MAX) NULL;
    PRINT '✓ DataSetManagement.ErrorMessageカラムを追加しました';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'FilePath')
BEGIN
    ALTER TABLE DataSetManagement ADD FilePath NVARCHAR(500) NULL;
    PRINT '✓ DataSetManagement.FilePathカラムを追加しました';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DataSetManagement') AND name = 'Status')
BEGIN
    ALTER TABLE DataSetManagement ADD Status NVARCHAR(20) NULL;
    PRINT '✓ DataSetManagement.Statusカラムを追加しました';
END
GO

-- 追加カラムの確認
PRINT '';
PRINT '追加したカラムの確認...';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'DataSetManagement'
AND COLUMN_NAME IN ('DataSetType', 'Name', 'Description', 'ErrorMessage', 'FilePath', 'Status')
ORDER BY COLUMN_NAME;

-- ========================================
-- Phase 1: データ型の拡張と制約の調整
-- ========================================
PRINT '';
PRINT 'Phase 1: データ型の拡張と制約の調整開始...';

-- ProcessTypeをNOT NULLに変更（デフォルト値を設定）
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'DataSetManagement' 
           AND COLUMN_NAME = 'ProcessType' 
           AND IS_NULLABLE = 'YES')
BEGIN
    -- まずデフォルト値を設定
    UPDATE DataSetManagement SET ProcessType = 'UNKNOWN' WHERE ProcessType IS NULL;
    -- NOT NULL制約を追加
    ALTER TABLE DataSetManagement ALTER COLUMN ProcessType NVARCHAR(50) NOT NULL;
    PRINT '✓ ProcessTypeをNOT NULLに変更しました';
END
ELSE
BEGIN
    PRINT '- ProcessTypeは既にNOT NULLです';
END

-- DepartmentをNOT NULLに変更（デフォルト値を設定）
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'DataSetManagement' 
           AND COLUMN_NAME = 'Department' 
           AND IS_NULLABLE = 'YES')
BEGIN
    -- まずデフォルト値を設定
    UPDATE DataSetManagement SET Department = 'DeptA' WHERE Department IS NULL;
    -- NOT NULL制約を追加
    ALTER TABLE DataSetManagement ALTER COLUMN Department NVARCHAR(50) NOT NULL;
    PRINT '✓ DepartmentをNOT NULLに変更しました';
END
ELSE
BEGIN
    PRINT '- Departmentは既にNOT NULLです';
END

-- CreatedByをNOT NULLに変更（デフォルト値を設定）
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'DataSetManagement' 
           AND COLUMN_NAME = 'CreatedBy' 
           AND IS_NULLABLE = 'YES')
BEGIN
    -- まずデフォルト値を設定
    UPDATE DataSetManagement SET CreatedBy = 'system' WHERE CreatedBy IS NULL;
    -- NOT NULL制約を追加
    ALTER TABLE DataSetManagement ALTER COLUMN CreatedBy NVARCHAR(100) NOT NULL;
    PRINT '✓ CreatedByをNOT NULLに変更しました';
END
ELSE
BEGIN
    PRINT '- CreatedByは既にNOT NULLです';
END

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
ELSE
BEGIN
    PRINT '- SalesVouchers.DataSetIdは既に適切なサイズです';
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
ELSE
BEGIN
    PRINT '- PurchaseVouchers.DataSetIdは既に適切なサイズです';
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
ELSE
BEGIN
    PRINT '- InventoryAdjustments.DataSetIdは既に適切なサイズです';
END
GO

-- ========================================
-- Phase 2: データ移行
-- ========================================
PRINT '';
PRINT 'Phase 2: DataSetsからDataSetManagementへのデータ移行...';

-- 移行前のレコード数を記録
DECLARE @DataSetsCount INT;
DECLARE @ExistingCount INT;
DECLARE @MigratedCount INT;

SELECT @DataSetsCount = COUNT(*) FROM DataSets;
SELECT @ExistingCount = COUNT(*) FROM DataSetManagement WHERE DataSetId IN (SELECT Id FROM DataSets);

PRINT CONCAT('DataSetsレコード数: ', @DataSetsCount);
PRINT CONCAT('既存のDataSetManagementレコード数: ', @ExistingCount);

-- データ移行（必須フィールドのみで最初に挿入）
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
    Department
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
    COALESCE(ds.CreatedDate, ds.CreatedAt, GETDATE()) AS CreatedAt,
    'migration' AS CreatedBy,
    'DeptA' AS Department
FROM DataSets ds
WHERE NOT EXISTS (
    SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = ds.Id
);

SET @MigratedCount = @@ROWCOUNT;
PRINT CONCAT('✓ ', @MigratedCount, '件のレコードを移行しました（基本情報）');

-- 追加カラムの更新
PRINT '';
PRINT '追加カラムの更新...';

-- Notesの更新
UPDATE dsm
SET dsm.Notes = CASE 
    WHEN ds.Name IS NOT NULL AND ds.Description IS NOT NULL 
        THEN CONCAT('Name: ', ds.Name, CHAR(13) + CHAR(10), 'Description: ', ds.Description)
    WHEN ds.Name IS NOT NULL 
        THEN CONCAT('Name: ', ds.Name)
    WHEN ds.Description IS NOT NULL 
        THEN CONCAT('Description: ', ds.Description)
    ELSE dsm.Notes
END
FROM DataSetManagement dsm
INNER JOIN DataSets ds ON dsm.DataSetId = ds.Id
WHERE dsm.ImportType = 'LEGACY';
PRINT '✓ Notes情報を更新しました';

-- 他のカラムの更新
UPDATE dsm
SET 
    dsm.ErrorMessage = ds.ErrorMessage,
    dsm.FilePath = ds.FilePath,
    dsm.Status = ds.Status,
    dsm.UpdatedAt = COALESCE(ds.UpdatedDate, ds.UpdatedAt),
    dsm.DataSetType = ds.DataSetType,
    dsm.Name = ds.Name,
    dsm.Description = ds.Description
FROM DataSetManagement dsm
INNER JOIN DataSets ds ON dsm.DataSetId = ds.Id
WHERE dsm.ImportType = 'LEGACY';
PRINT '✓ 追加カラム情報を更新しました';

-- ArchivedAtの設定（エラーステータスの場合）
UPDATE DataSetManagement
SET ArchivedAt = CreatedAt,
    ArchivedBy = 'migration'
WHERE IsArchived = 1 AND ArchivedAt IS NULL;
PRINT '✓ アーカイブ情報を設定しました';
GO

-- ========================================
-- Phase 3: 外部キー制約の再作成
-- ========================================
PRINT '';
PRINT 'Phase 3: 外部キー制約の再作成...';

-- SalesVouchersの外部キー制約
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_SalesVouchers_DataSetManagement')
BEGIN
    -- 孤立レコードの確認
    DECLARE @OrphanSalesCheck INT;
    SELECT @OrphanSalesCheck = COUNT(*) 
    FROM SalesVouchers sv
    WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = sv.DataSetId);
    
    IF @OrphanSalesCheck > 0
    BEGIN
        PRINT CONCAT('⚠️ 警告: ', @OrphanSalesCheck, '件の孤立した売上伝票があります');
        -- 孤立レコード用のダミーDataSetを作成
        INSERT INTO DataSetManagement (DataSetId, JobDate, ProcessType, ImportType, RecordCount, TotalRecordCount, IsActive, IsArchived, CreatedAt, CreatedBy, Department)
        SELECT DISTINCT sv.DataSetId, GETDATE(), 'ORPHAN', 'LEGACY', 0, 0, 0, 1, GETDATE(), 'migration', 'DeptA'
        FROM SalesVouchers sv
        WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = sv.DataSetId);
        PRINT '✓ 孤立レコード用のDataSetManagementレコードを作成しました';
    END
    
    ALTER TABLE SalesVouchers WITH NOCHECK 
    ADD CONSTRAINT FK_SalesVouchers_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);
    
    -- 制約を有効化
    ALTER TABLE SalesVouchers CHECK CONSTRAINT FK_SalesVouchers_DataSetManagement;
    PRINT '✓ FK_SalesVouchers_DataSetManagement制約を作成しました';
END

-- PurchaseVouchersの外部キー制約（同様の処理）
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PurchaseVouchers_DataSetManagement')
BEGIN
    -- 孤立レコードの確認
    DECLARE @OrphanPurchaseCheck INT;
    SELECT @OrphanPurchaseCheck = COUNT(*) 
    FROM PurchaseVouchers pv
    WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = pv.DataSetId);
    
    IF @OrphanPurchaseCheck > 0
    BEGIN
        PRINT CONCAT('⚠️ 警告: ', @OrphanPurchaseCheck, '件の孤立した仕入伝票があります');
        -- 孤立レコード用のダミーDataSetを作成
        INSERT INTO DataSetManagement (DataSetId, JobDate, ProcessType, ImportType, RecordCount, TotalRecordCount, IsActive, IsArchived, CreatedAt, CreatedBy, Department)
        SELECT DISTINCT pv.DataSetId, GETDATE(), 'ORPHAN', 'LEGACY', 0, 0, 0, 1, GETDATE(), 'migration', 'DeptA'
        FROM PurchaseVouchers pv
        WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = pv.DataSetId);
        PRINT '✓ 孤立レコード用のDataSetManagementレコードを作成しました';
    END
    
    ALTER TABLE PurchaseVouchers WITH NOCHECK 
    ADD CONSTRAINT FK_PurchaseVouchers_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);
    
    -- 制約を有効化
    ALTER TABLE PurchaseVouchers CHECK CONSTRAINT FK_PurchaseVouchers_DataSetManagement;
    PRINT '✓ FK_PurchaseVouchers_DataSetManagement制約を作成しました';
END

-- InventoryAdjustmentsの外部キー制約（同様の処理）
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_InventoryAdjustments_DataSetManagement')
BEGIN
    -- 孤立レコードの確認
    DECLARE @OrphanAdjustmentCheck INT;
    SELECT @OrphanAdjustmentCheck = COUNT(*) 
    FROM InventoryAdjustments ia
    WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = ia.DataSetId);
    
    IF @OrphanAdjustmentCheck > 0
    BEGIN
        PRINT CONCAT('⚠️ 警告: ', @OrphanAdjustmentCheck, '件の孤立した在庫調整があります');
        -- 孤立レコード用のダミーDataSetを作成
        INSERT INTO DataSetManagement (DataSetId, JobDate, ProcessType, ImportType, RecordCount, TotalRecordCount, IsActive, IsArchived, CreatedAt, CreatedBy, Department)
        SELECT DISTINCT ia.DataSetId, GETDATE(), 'ORPHAN', 'LEGACY', 0, 0, 0, 1, GETDATE(), 'migration', 'DeptA'
        FROM InventoryAdjustments ia
        WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = ia.DataSetId);
        PRINT '✓ 孤立レコード用のDataSetManagementレコードを作成しました';
    END
    
    ALTER TABLE InventoryAdjustments WITH NOCHECK 
    ADD CONSTRAINT FK_InventoryAdjustments_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);
    
    -- 制約を有効化
    ALTER TABLE InventoryAdjustments CHECK CONSTRAINT FK_InventoryAdjustments_DataSetManagement;
    PRINT '✓ FK_InventoryAdjustments_DataSetManagement制約を作成しました';
END
GO

-- ========================================
-- Phase 4: インデックスの作成
-- ========================================
PRINT '';
PRINT 'Phase 4: パフォーマンス最適化インデックスの作成...';

-- Statusインデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DataSetManagement_Status' AND object_id = OBJECT_ID('DataSetManagement'))
BEGIN
    CREATE INDEX IX_DataSetManagement_Status ON DataSetManagement(Status);
    PRINT '✓ IX_DataSetManagement_Statusインデックスを作成しました';
END

-- ProcessType + JobDateの複合インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DataSetManagement_ProcessType_JobDate' AND object_id = OBJECT_ID('DataSetManagement'))
BEGIN
    CREATE INDEX IX_DataSetManagement_ProcessType_JobDate ON DataSetManagement(ProcessType, JobDate);
    PRINT '✓ IX_DataSetManagement_ProcessType_JobDateインデックスを作成しました';
END
GO

-- ========================================
-- 移行結果の検証
-- ========================================
PRINT '';
PRINT '================================';
PRINT '移行結果の検証';
PRINT '================================';

-- 最終統計
DECLARE @FinalDataSetsCount INT;
DECLARE @FinalDataSetManagementCount INT;
DECLARE @OrphanSales INT;
DECLARE @OrphanPurchase INT;
DECLARE @OrphanAdjustment INT;

SELECT @FinalDataSetsCount = COUNT(*) FROM DataSets;
SELECT @FinalDataSetManagementCount = COUNT(*) FROM DataSetManagement;

SELECT @OrphanSales = COUNT(*) 
FROM SalesVouchers sv
WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = sv.DataSetId);

SELECT @OrphanPurchase = COUNT(*) 
FROM PurchaseVouchers pv
WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = pv.DataSetId);

SELECT @OrphanAdjustment = COUNT(*) 
FROM InventoryAdjustments ia
WHERE NOT EXISTS (SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = ia.DataSetId);

PRINT CONCAT('DataSetsテーブル: ', @FinalDataSetsCount, '件');
PRINT CONCAT('DataSetManagementテーブル: ', @FinalDataSetManagementCount, '件');
PRINT '';
PRINT CONCAT('孤立した売上伝票: ', @OrphanSales, '件');
PRINT CONCAT('孤立した仕入伝票: ', @OrphanPurchase, '件');
PRINT CONCAT('孤立した在庫調整: ', @OrphanAdjustment, '件');

IF @OrphanSales = 0 AND @OrphanPurchase = 0 AND @OrphanAdjustment = 0
BEGIN
    PRINT '';
    PRINT '✓ すべての外部キー参照が正常です';
END

-- 最終的な同期状況の表示
PRINT '';
PRINT '================================';
PRINT 'データ同期状況';
PRINT '================================';

SELECT 
    'DataSets' as SourceTable,
    COUNT(*) as RecordCount,
    'DataSetManagement' as TargetTable,
    (SELECT COUNT(*) FROM DataSetManagement WHERE ImportType = 'LEGACY') as MigratedCount
FROM DataSets;

-- ProcessType別の移行状況
PRINT '';
SELECT 
    ProcessType,
    COUNT(*) as Count,
    'From DataSets' as Source
FROM DataSets
GROUP BY ProcessType

UNION ALL

SELECT 
    ProcessType,
    COUNT(*) as Count,
    'In DataSetManagement (LEGACY)' as Source
FROM DataSetManagement
WHERE ImportType = 'LEGACY'
GROUP BY ProcessType
ORDER BY ProcessType, Source;

PRINT '';
PRINT '================================';
PRINT 'DataSetManagement統合マイグレーション完了';
PRINT '================================';
PRINT '';
PRINT '注意: DataSetsテーブルはまだ削除されていません。';
PRINT 'アプリケーションの動作確認後、別途削除してください。';
PRINT '';
PRINT '次のステップ:';
PRINT '1. verify-dataset-migration.sqlで詳細な検証を実行';
PRINT '2. アプリケーションのフィーチャーフラグを有効化';
PRINT '3. 十分なテスト期間（1-2週間）を設ける';
PRINT '4. 問題がなければDataSetsテーブルを削除';
GO