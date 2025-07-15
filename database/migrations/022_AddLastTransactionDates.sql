-- =============================================================================
-- マイグレーション: 022_AddLastTransactionDates.sql
-- 説明: InventoryMasterテーブルに最終取引日カラムを追加
-- 作成日: 2025-07-12
-- 注意: 動的SQLを使用してカラム存在エラーを回避
-- =============================================================================

-- ステップ1: カラムの追加
PRINT '===== ステップ1: カラムの追加 =====';

-- 1-1. LastSalesDateカラムを追加
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
               AND name = 'LastSalesDate')
BEGIN
    ALTER TABLE InventoryMaster ADD LastSalesDate DATE NULL;
    PRINT 'LastSalesDateカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'LastSalesDateカラムは既に存在します。';
END

-- 1-2. LastPurchaseDateカラムを追加
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
               AND name = 'LastPurchaseDate')
BEGIN
    ALTER TABLE InventoryMaster ADD LastPurchaseDate DATE NULL;
    PRINT 'LastPurchaseDateカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'LastPurchaseDateカラムは既に存在します。';
END

-- 1-3. Notesカラムを追加
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
               AND name = 'Notes')
BEGIN
    ALTER TABLE InventoryMaster ADD Notes NVARCHAR(MAX) NULL;
    PRINT 'Notesカラムを追加しました。';
END
ELSE
BEGIN
    PRINT 'Notesカラムは既に存在します。';
END
GO

-- ステップ2: インデックスの作成（動的SQL使用）
PRINT '';
PRINT '===== ステップ2: インデックスの作成 =====';

DECLARE @sql NVARCHAR(MAX);

-- 2-1. LastSalesDateのインデックスを作成
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
           AND name = 'LastSalesDate')
   AND NOT EXISTS (SELECT * FROM sys.indexes 
                   WHERE name = 'IX_InventoryMaster_LastSalesDate' 
                   AND object_id = OBJECT_ID('InventoryMaster'))
BEGIN
    SET @sql = 'CREATE INDEX IX_InventoryMaster_LastSalesDate ON InventoryMaster(LastSalesDate)';
    EXEC sp_executesql @sql;
    PRINT 'IX_InventoryMaster_LastSalesDateインデックスを作成しました。';
END

-- 2-2. LastPurchaseDateのインデックスを作成
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
           AND name = 'LastPurchaseDate')
   AND NOT EXISTS (SELECT * FROM sys.indexes 
                   WHERE name = 'IX_InventoryMaster_LastPurchaseDate' 
                   AND object_id = OBJECT_ID('InventoryMaster'))
BEGIN
    SET @sql = 'CREATE INDEX IX_InventoryMaster_LastPurchaseDate ON InventoryMaster(LastPurchaseDate)';
    EXEC sp_executesql @sql;
    PRINT 'IX_InventoryMaster_LastPurchaseDateインデックスを作成しました。';
END
GO

-- ステップ3: データの更新（動的SQL使用）
PRINT '';
PRINT '===== ステップ3: データの更新 =====';

DECLARE @updateSql NVARCHAR(MAX);
DECLARE @rowCount INT;

-- 3-1. 売上伝票から最終売上日を設定
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
           AND name = 'LastSalesDate')
BEGIN
    PRINT '売上伝票から最終売上日を設定中...';
    
    SET @updateSql = '
    UPDATE im
    SET im.LastSalesDate = sv.MaxJobDate
    FROM InventoryMaster im
    INNER JOIN (
        SELECT 
            ProductCode, 
            GradeCode, 
            ClassCode, 
            ShippingMarkCode, 
            ShippingMarkName,
            MAX(JobDate) as MaxJobDate
        FROM SalesVouchers
        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    ) sv ON 
        im.ProductCode = sv.ProductCode AND
        im.GradeCode = sv.GradeCode AND
        im.ClassCode = sv.ClassCode AND
        im.ShippingMarkCode = sv.ShippingMarkCode AND
        im.ShippingMarkName = sv.ShippingMarkName
    WHERE im.LastSalesDate IS NULL';
    
    EXEC sp_executesql @updateSql;
    SET @rowCount = @@ROWCOUNT;
    PRINT CAST(@rowCount AS VARCHAR) + '件の最終売上日を更新しました。';
END

-- 3-2. 仕入伝票から最終仕入日を設定
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
           AND name = 'LastPurchaseDate')
BEGIN
    PRINT '仕入伝票から最終仕入日を設定中...';
    
    SET @updateSql = '
    UPDATE im
    SET im.LastPurchaseDate = pv.MaxJobDate
    FROM InventoryMaster im
    INNER JOIN (
        SELECT 
            ProductCode, 
            GradeCode, 
            ClassCode, 
            ShippingMarkCode, 
            ShippingMarkName,
            MAX(JobDate) as MaxJobDate
        FROM PurchaseVouchers
        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    ) pv ON 
        im.ProductCode = pv.ProductCode AND
        im.GradeCode = pv.GradeCode AND
        im.ClassCode = pv.ClassCode AND
        im.ShippingMarkCode = pv.ShippingMarkCode AND
        im.ShippingMarkName = pv.ShippingMarkName
    WHERE im.LastPurchaseDate IS NULL';
    
    EXEC sp_executesql @updateSql;
    SET @rowCount = @@ROWCOUNT;
    PRINT CAST(@rowCount AS VARCHAR) + '件の最終仕入日を更新しました。';
END
GO

-- ステップ4: 実行結果の確認（動的SQL使用）
PRINT '';
PRINT '===== ステップ4: 実行結果サマリー =====';

DECLARE @resultSql NVARCHAR(MAX);

IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[InventoryMaster]') 
           AND name IN ('LastSalesDate', 'LastPurchaseDate'))
BEGIN
    SET @resultSql = '
    SELECT 
        COUNT(*) as TotalRecords,
        COUNT(LastSalesDate) as RecordsWithSalesDate,
        COUNT(LastPurchaseDate) as RecordsWithPurchaseDate,
        COUNT(CASE WHEN LastSalesDate IS NOT NULL OR LastPurchaseDate IS NOT NULL THEN 1 END) as RecordsWithAnyDate
    FROM InventoryMaster';
    
    EXEC sp_executesql @resultSql;
END

-- カラムの確認
PRINT '';
PRINT '===== 追加されたカラムの確認 =====';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventoryMaster'
AND COLUMN_NAME IN ('LastSalesDate', 'LastPurchaseDate', 'Notes')
ORDER BY ORDINAL_POSITION;

-- インデックスの確認
PRINT '';
PRINT '===== 作成されたインデックス =====';
SELECT 
    i.name as IndexName,
    i.type_desc as IndexType
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('InventoryMaster')
AND i.name IN ('IX_InventoryMaster_LastSalesDate', 'IX_InventoryMaster_LastPurchaseDate');

PRINT '';
PRINT '===== 022_AddLastTransactionDates.sql 実行完了 =====';
PRINT '最終売上日と最終仕入日のカラムを追加し、既存データの初期設定を完了しました。';