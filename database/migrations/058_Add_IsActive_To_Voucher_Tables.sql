-- 058_Add_IsActive_To_Voucher_Tables.sql
-- 伝票テーブルにIsActiveカラムを追加し、DataSet単位での有効/無効管理を可能にする
-- すべてのDDL/DML操作を動的SQLで実行してコンパイル時エラーを完全回避

PRINT '058_Add_IsActive_To_Voucher_Tables: 伝票テーブルへのIsActive追加マイグレーション開始';

DECLARE @sql NVARCHAR(MAX);
DECLARE @UpdatedRows INT = 0;

-- 1. SalesVouchersテーブル
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SalesVouchers') AND name = 'IsActive')
BEGIN
    -- カラム追加（動的SQL）
    SET @sql = N'ALTER TABLE SalesVouchers ADD IsActive BIT NOT NULL DEFAULT 1;';
    EXEC sp_executesql @sql;
    PRINT '✓ SalesVouchers.IsActiveカラムを追加しました';
    
    -- インデックス作成（動的SQL）
    SET @sql = N'CREATE NONCLUSTERED INDEX IX_SalesVouchers_IsActive_JobDate 
                 ON SalesVouchers(IsActive, JobDate) 
                 INCLUDE (DataSetId);';
    EXEC sp_executesql @sql;
    PRINT '✓ IX_SalesVouchers_IsActive_JobDateインデックスを作成しました';
END
ELSE
BEGIN
    PRINT '- SalesVouchers.IsActiveカラムは既に存在します';
END

-- 2. PurchaseVouchersテーブル
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseVouchers') AND name = 'IsActive')
BEGIN
    -- カラム追加（動的SQL）
    SET @sql = N'ALTER TABLE PurchaseVouchers ADD IsActive BIT NOT NULL DEFAULT 1;';
    EXEC sp_executesql @sql;
    PRINT '✓ PurchaseVouchers.IsActiveカラムを追加しました';
    
    -- インデックス作成（動的SQL）
    SET @sql = N'CREATE NONCLUSTERED INDEX IX_PurchaseVouchers_IsActive_JobDate 
                 ON PurchaseVouchers(IsActive, JobDate) 
                 INCLUDE (DataSetId);';
    EXEC sp_executesql @sql;
    PRINT '✓ IX_PurchaseVouchers_IsActive_JobDateインデックスを作成しました';
END
ELSE
BEGIN
    PRINT '- PurchaseVouchers.IsActiveカラムは既に存在します';
END

-- 3. InventoryAdjustmentsテーブル
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InventoryAdjustments') AND name = 'IsActive')
BEGIN
    -- カラム追加（動的SQL）
    SET @sql = N'ALTER TABLE InventoryAdjustments ADD IsActive BIT NOT NULL DEFAULT 1;';
    EXEC sp_executesql @sql;
    PRINT '✓ InventoryAdjustments.IsActiveカラムを追加しました';
    
    -- インデックス作成（動的SQL）
    SET @sql = N'CREATE NONCLUSTERED INDEX IX_InventoryAdjustments_IsActive_JobDate 
                 ON InventoryAdjustments(IsActive, JobDate) 
                 INCLUDE (DataSetId);';
    EXEC sp_executesql @sql;
    PRINT '✓ IX_InventoryAdjustments_IsActive_JobDateインデックスを作成しました';
END
ELSE
BEGIN
    PRINT '- InventoryAdjustments.IsActiveカラムは既に存在します';
END

-- 4. 既存データの整合性確保（動的SQL使用）
PRINT '';
PRINT '既存データのIsActive同期処理を開始します...';

-- SalesVouchersの更新（動的SQL）
SET @sql = N'
UPDATE sv
SET sv.IsActive = CASE WHEN dm.IsActive = 1 THEN 1 ELSE 0 END
FROM SalesVouchers sv
INNER JOIN DataSetManagement dm ON sv.DataSetId = dm.DataSetId
WHERE EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(''SalesVouchers'') AND name = ''IsActive'')
  AND sv.IsActive != dm.IsActive;

SELECT @UpdatedRows = @@ROWCOUNT;';

EXEC sp_executesql @sql, N'@UpdatedRows INT OUTPUT', @UpdatedRows = @UpdatedRows OUTPUT;
PRINT '✓ SalesVouchers: ' + CAST(@UpdatedRows AS NVARCHAR(10)) + '件のIsActiveを同期しました';

-- PurchaseVouchersの更新（動的SQL）
SET @sql = N'
UPDATE pv
SET pv.IsActive = CASE WHEN dm.IsActive = 1 THEN 1 ELSE 0 END
FROM PurchaseVouchers pv
INNER JOIN DataSetManagement dm ON pv.DataSetId = dm.DataSetId
WHERE EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(''PurchaseVouchers'') AND name = ''IsActive'')
  AND pv.IsActive != dm.IsActive;

SELECT @UpdatedRows = @@ROWCOUNT;';

EXEC sp_executesql @sql, N'@UpdatedRows INT OUTPUT', @UpdatedRows = @UpdatedRows OUTPUT;
PRINT '✓ PurchaseVouchers: ' + CAST(@UpdatedRows AS NVARCHAR(10)) + '件のIsActiveを同期しました';

-- InventoryAdjustmentsの更新（動的SQL）
SET @sql = N'
UPDATE ia
SET ia.IsActive = CASE WHEN dm.IsActive = 1 THEN 1 ELSE 0 END
FROM InventoryAdjustments ia
INNER JOIN DataSetManagement dm ON ia.DataSetId = dm.DataSetId
WHERE EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(''InventoryAdjustments'') AND name = ''IsActive'')
  AND ia.IsActive != dm.IsActive;

SELECT @UpdatedRows = @@ROWCOUNT;';

EXEC sp_executesql @sql, N'@UpdatedRows INT OUTPUT', @UpdatedRows = @UpdatedRows OUTPUT;
PRINT '✓ InventoryAdjustments: ' + CAST(@UpdatedRows AS NVARCHAR(10)) + '件のIsActiveを同期しました';

-- 5. 統計情報の表示（動的SQL）
PRINT '';
PRINT '=== IsActive同期後の統計情報 ===';

-- 統計情報を一時テーブルに格納
CREATE TABLE #Statistics (
    TableName NVARCHAR(50),
    TotalRecords INT,
    ActiveRecords INT,
    InactiveRecords INT
);

-- SalesVouchersの統計（動的SQL）
SET @sql = N'
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(''SalesVouchers'') AND name = ''IsActive'')
BEGIN
    INSERT INTO #Statistics
    SELECT 
        ''SalesVouchers'' as TableName,
        COUNT(*) as TotalRecords,
        SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) as ActiveRecords,
        SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) as InactiveRecords
    FROM SalesVouchers;
END';
EXEC sp_executesql @sql;

-- PurchaseVouchersの統計（動的SQL）
SET @sql = N'
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(''PurchaseVouchers'') AND name = ''IsActive'')
BEGIN
    INSERT INTO #Statistics
    SELECT 
        ''PurchaseVouchers'' as TableName,
        COUNT(*) as TotalRecords,
        SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) as ActiveRecords,
        SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) as InactiveRecords
    FROM PurchaseVouchers;
END';
EXEC sp_executesql @sql;

-- InventoryAdjustmentsの統計（動的SQL）
SET @sql = N'
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(''InventoryAdjustments'') AND name = ''IsActive'')
BEGIN
    INSERT INTO #Statistics
    SELECT 
        ''InventoryAdjustments'' as TableName,
        COUNT(*) as TotalRecords,
        SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) as ActiveRecords,
        SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) as InactiveRecords
    FROM InventoryAdjustments;
END';
EXEC sp_executesql @sql;

-- DataSetManagementの統計
INSERT INTO #Statistics
SELECT 
    'DataSetManagement' as TableName,
    COUNT(*) as TotalRecords,
    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) as ActiveRecords,
    SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) as InactiveRecords
FROM DataSetManagement;

-- 統計情報を表示
SELECT * FROM #Statistics ORDER BY TableName;

-- 一時テーブルをクリーンアップ
DROP TABLE #Statistics;

PRINT '';
PRINT '058_Add_IsActive_To_Voucher_Tables: マイグレーション完了';