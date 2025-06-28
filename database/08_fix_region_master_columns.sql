-- =====================================================
-- 産地マスタのカラム名修正スクリプト
-- 作成日: 2025-01-28
-- 内容: OriginCode/OriginNameをRegionCode/RegionNameに変更
-- =====================================================

USE InventoryManagementDB;
GO

-- 現在のカラム名を確認
PRINT '=== 現在のRegionMasterテーブルのカラム構造 ==='
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'RegionMaster'
ORDER BY ORDINAL_POSITION;
GO

-- カラム名の変更
PRINT ''
PRINT '=== カラム名の変更を開始 ==='

-- OriginCodeをRegionCodeに変更
IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'OriginCode' AND Object_ID = Object_ID(N'RegionMaster'))
BEGIN
    EXEC sp_rename 'RegionMaster.OriginCode', 'RegionCode', 'COLUMN';
    PRINT 'OriginCode を RegionCode に変更しました。'
END
ELSE IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'RegionCode' AND Object_ID = Object_ID(N'RegionMaster'))
BEGIN
    PRINT 'RegionCode カラムは既に存在します。'
END
ELSE
BEGIN
    PRINT 'エラー: OriginCode カラムが見つかりません。'
END
GO

-- OriginNameをRegionNameに変更
IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'OriginName' AND Object_ID = Object_ID(N'RegionMaster'))
BEGIN
    EXEC sp_rename 'RegionMaster.OriginName', 'RegionName', 'COLUMN';
    PRINT 'OriginName を RegionName に変更しました。'
END
ELSE IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'RegionName' AND Object_ID = Object_ID(N'RegionMaster'))
BEGIN
    PRINT 'RegionName カラムは既に存在します。'
END
ELSE
BEGIN
    PRINT 'エラー: OriginName カラムが見つかりません。'
END
GO

-- 変更後のカラム名を確認
PRINT ''
PRINT '=== 変更後のRegionMasterテーブルのカラム構造 ==='
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'RegionMaster'
ORDER BY ORDINAL_POSITION;
GO

-- 制約やインデックスの確認
PRINT ''
PRINT '=== 制約とインデックスの確認 ==='
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    COL_NAME(ic.object_id, ic.column_id) AS ColumnName
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE i.object_id = OBJECT_ID('RegionMaster')
ORDER BY i.name, ic.key_ordinal;
GO

-- テストデータで動作確認
PRINT ''
PRINT '=== 動作確認 ==='
IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'RegionCode' AND Object_ID = Object_ID(N'RegionMaster'))
BEGIN
    -- テストクエリを実行
    SELECT TOP 5 RegionCode, RegionName FROM RegionMaster;
    PRINT '動作確認: SELECT文が正常に実行されました。'
END
ELSE
BEGIN
    PRINT 'エラー: RegionCode カラムが存在しません。'
END
GO

PRINT ''
PRINT '=== カラム名変更処理が完了しました ==='
PRINT '注意: アプリケーションを再起動する前に、このスクリプトを実行してください。'
GO