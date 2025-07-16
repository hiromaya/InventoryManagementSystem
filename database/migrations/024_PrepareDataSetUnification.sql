-- =====================================================
-- Phase 1: DataSetManagement統一のための準備
-- 作成日: 2025-01-16
-- 目的: 既存のDataSetsデータをDataSetManagementに移行
-- =====================================================

PRINT '=========================================='
PRINT 'DataSet統一準備スクリプト開始'
PRINT '実行日時: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT '=========================================='

-- 移行前のデータ件数確認
DECLARE @DataSetsCount INT
DECLARE @DataSetManagementCount INT

SELECT @DataSetsCount = COUNT(*) FROM DataSets
SELECT @DataSetManagementCount = COUNT(*) FROM DataSetManagement

PRINT '移行前のデータ件数:'
PRINT '  DataSets: ' + CAST(@DataSetsCount AS NVARCHAR(10)) + '件'
PRINT '  DataSetManagement: ' + CAST(@DataSetManagementCount AS NVARCHAR(10)) + '件'
PRINT ''

-- DataSetsが空の場合の処理
IF @DataSetsCount = 0
BEGIN
    PRINT 'DataSetsテーブルは空です。移行するデータがありません。'
    PRINT ''
    PRINT '=========================================='
    PRINT 'DataSet統一準備スクリプト正常終了'
    PRINT '=========================================='
END
ELSE
BEGIN
    PRINT 'DataSetsにデータが存在します。移行処理を実行します。'
    -- ここに移行処理を記述（必要な場合）
END