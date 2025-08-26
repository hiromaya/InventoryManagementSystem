-- =============================================
-- 在庫管理システム データセット管理機能追加
-- 作成日: 2025-01-11
-- 
-- 変更内容:
-- 1. InventoryMasterテーブルへの列追加
-- 2. DataSetManagementテーブルの作成
-- 3. フィルタ化インデックスの作成
-- =============================================

USE InventoryManagementDB;
GO

-- =============================================
-- 1. InventoryMasterテーブルの拡張
-- =============================================
-- IsActive列の追加（デフォルト値1で既存データもアクティブとする）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'IsActive')
BEGIN
    ALTER TABLE InventoryMaster ADD IsActive BIT NOT NULL DEFAULT 1;
END
GO

-- ParentDataSetId列の追加（親データセットの参照用）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'ParentDataSetId')
BEGIN
    ALTER TABLE InventoryMaster ADD ParentDataSetId NVARCHAR(100) NULL;
END
GO

-- ImportType列の追加（インポート種別）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'ImportType')
BEGIN
    ALTER TABLE InventoryMaster ADD ImportType NVARCHAR(20) NOT NULL DEFAULT 'UNKNOWN';
END
GO

-- CreatedBy列の追加（作成者）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'CreatedBy')
BEGIN
    ALTER TABLE InventoryMaster ADD CreatedBy NVARCHAR(50) NULL;
END
GO

-- CreatedAt列の追加（作成日時）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE InventoryMaster ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
END
GO

-- UpdatedAt列の追加（更新日時）
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryMaster') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE InventoryMaster ADD UpdatedAt DATETIME2 NULL;
END
GO

-- ImportTypeの制約追加
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_InventoryMaster_ImportType')
BEGIN
    ALTER TABLE InventoryMaster 
    ADD CONSTRAINT CK_InventoryMaster_ImportType 
    CHECK (ImportType IN ('INIT', 'IMPORT', 'CARRYOVER', 'MANUAL', 'UNKNOWN'));
END
GO

-- =============================================
-- 2. DataSetManagementテーブルの作成
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('DataSetManagement') AND type = 'U')
BEGIN
    CREATE TABLE DataSetManagement (
        DataSetId NVARCHAR(100) PRIMARY KEY,
        JobDate DATE NOT NULL,
        ImportType NVARCHAR(20) NOT NULL,
        RecordCount INT NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        IsArchived BIT NOT NULL DEFAULT 0, -- Gemini推奨の論理アーカイブフラグ
        ParentDataSetId NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        CreatedBy NVARCHAR(50) NULL,
        DeactivatedAt DATETIME2 NULL,
        DeactivatedBy NVARCHAR(50) NULL,
        ArchivedAt DATETIME2 NULL,
        ArchivedBy NVARCHAR(50) NULL,
        Notes NVARCHAR(500) NULL,
        
        -- 制約
        CONSTRAINT CK_DataSetManagement_ImportType 
        CHECK (ImportType IN ('INIT', 'IMPORT', 'CARRYOVER', 'MANUAL', 'UNKNOWN')),
        
        -- 外部キー（自己参照）
        CONSTRAINT FK_DataSetManagement_Parent 
        FOREIGN KEY (ParentDataSetId) REFERENCES DataSetManagement(DataSetId)
    );
END
GO

-- =============================================
-- 3. インデックスの作成
-- =============================================

-- DataSetIdとIsActiveの複合インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_DataSetId_IsActive')
BEGIN
    CREATE INDEX IX_InventoryMaster_DataSetId_IsActive 
    ON InventoryMaster(DataSetId, IsActive) 
    INCLUDE (JobDate, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark);
END
GO

-- フィルタ化インデックス（アクティブなレコードのみ）- Gemini推奨
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_Active_Filtered')
BEGIN
    CREATE INDEX IX_InventoryMaster_Active_Filtered
    ON InventoryMaster (JobDate, ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark)
    WHERE IsActive = 1;
END
GO

-- JobDateとIsActiveの複合インデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventoryMaster_JobDate_IsActive')
BEGIN
    CREATE INDEX IX_InventoryMaster_JobDate_IsActive
    ON InventoryMaster (JobDate, IsActive)
    WHERE IsActive = 1;
END
GO

-- DataSetManagementテーブルのインデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DataSetManagement_JobDate_IsActive')
BEGIN
    CREATE INDEX IX_DataSetManagement_JobDate_IsActive
    ON DataSetManagement (JobDate, IsActive)
    WHERE IsActive = 1;
END
GO

-- ParentDataSetIdのインデックス
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DataSetManagement_ParentDataSetId')
BEGIN
    CREATE INDEX IX_DataSetManagement_ParentDataSetId
    ON DataSetManagement (ParentDataSetId);
END
GO

-- CreatedAtのインデックス（アーカイブ処理用）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DataSetManagement_CreatedAt')
BEGIN
    CREATE INDEX IX_DataSetManagement_CreatedAt
    ON DataSetManagement (CreatedAt)
    WHERE IsArchived = 0;
END
GO

-- =============================================
-- 4. 既存データの移行
-- =============================================

-- 既存のInventoryMasterデータにデフォルト値を設定
UPDATE InventoryMaster
SET ImportType = CASE 
    WHEN DataSetId LIKE 'INIT_%' THEN 'INIT'
    WHEN DataSetId = '' THEN 'INIT'  -- 空文字列は前月末在庫として扱う
    ELSE 'IMPORT'
END
WHERE ImportType = 'UNKNOWN';
GO

-- DataSetManagementテーブルに既存のDataSetIdを登録
INSERT INTO DataSetManagement (DataSetId, JobDate, ImportType, RecordCount, IsActive, CreatedAt)
SELECT DISTINCT 
    CASE WHEN DataSetId = '' THEN 'INIT_LEGACY_' + CONVERT(NVARCHAR(8), JobDate, 112) ELSE DataSetId END AS DataSetId,
    JobDate,
    CASE 
        WHEN DataSetId LIKE 'INIT_%' THEN 'INIT'
        WHEN DataSetId = '' THEN 'INIT'
        ELSE 'IMPORT'
    END AS ImportType,
    COUNT(*) AS RecordCount,
    1 AS IsActive,
    GETDATE() AS CreatedAt
FROM InventoryMaster
WHERE DataSetId IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 FROM DataSetManagement dm 
        WHERE dm.DataSetId = CASE WHEN InventoryMaster.DataSetId = '' THEN 'INIT_LEGACY_' + CONVERT(NVARCHAR(8), InventoryMaster.JobDate, 112) ELSE InventoryMaster.DataSetId END
    )
GROUP BY DataSetId, JobDate;
GO

-- 空のDataSetIdを持つレコードを更新
UPDATE InventoryMaster
SET DataSetId = 'INIT_LEGACY_' + CONVERT(NVARCHAR(8), JobDate, 112)
WHERE DataSetId = '';
GO

-- =============================================
-- 5. 循環参照チェック用のストアドプロシージャ
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('sp_CheckDataSetCircularReference') AND type = 'P')
    DROP PROCEDURE sp_CheckDataSetCircularReference;
GO

CREATE PROCEDURE sp_CheckDataSetCircularReference
    @NewDataSetId NVARCHAR(100),
    @ProposedParentId NVARCHAR(100),
    @HasCircularReference BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Gemini推奨の再帰CTEによる循環参照チェック
    WITH Ancestors AS (
        SELECT DataSetId, ParentDataSetId
        FROM DataSetManagement
        WHERE DataSetId = @ProposedParentId
        
        UNION ALL
        
        SELECT d.DataSetId, d.ParentDataSetId
        FROM DataSetManagement d
        INNER JOIN Ancestors a ON d.DataSetId = a.ParentDataSetId
    )
    SELECT @HasCircularReference = CASE 
        WHEN EXISTS (SELECT 1 FROM Ancestors WHERE DataSetId = @NewDataSetId) THEN 1
        ELSE 0
    END;
END
GO

PRINT '=== データセット管理機能の追加が完了しました ===';
PRINT '';
PRINT '追加された機能:';
PRINT '1. InventoryMasterテーブル: IsActive, ParentDataSetId, ImportType列';
PRINT '2. DataSetManagementテーブル: データセット世代管理';
PRINT '3. フィルタ化インデックス: アクティブデータの高速検索';
PRINT '4. 循環参照チェック: sp_CheckDataSetCircularReference';