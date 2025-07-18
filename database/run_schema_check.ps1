# 実際のデータベーススキーマ確認スクリプト
$serverInstance = "localhost\SQLEXPRESS"
$database = "InventoryManagementDB"

Write-Host "=== 実際のデータベーススキーマ確認 ===" -ForegroundColor Green
Write-Host "接続先: $serverInstance" -ForegroundColor Yellow
Write-Host "データベース: $database" -ForegroundColor Yellow

# SQL実行
$query = @"
-- ProductMasterテーブルの構造確認
PRINT '=== ProductMaster テーブル構造 ==='
SELECT 
    c.COLUMN_NAME as 'カラム名',
    c.DATA_TYPE as 'データ型',
    c.CHARACTER_MAXIMUM_LENGTH as '最大長',
    c.IS_NULLABLE as 'NULL許可'
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'ProductMaster'
ORDER BY c.ORDINAL_POSITION;

-- CustomerMasterテーブルの構造確認
PRINT ''
PRINT '=== CustomerMaster テーブル構造 ==='
SELECT 
    c.COLUMN_NAME as 'カラム名',
    c.DATA_TYPE as 'データ型',
    c.CHARACTER_MAXIMUM_LENGTH as '最大長',
    c.IS_NULLABLE as 'NULL許可'
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'CustomerMaster'
ORDER BY c.ORDINAL_POSITION;

-- SupplierMasterテーブルの構造確認
PRINT ''
PRINT '=== SupplierMaster テーブル構造 ==='
SELECT 
    c.COLUMN_NAME as 'カラム名',
    c.DATA_TYPE as 'データ型',
    c.CHARACTER_MAXIMUM_LENGTH as '最大長',
    c.IS_NULLABLE as 'NULL許可'
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'SupplierMaster'
ORDER BY c.ORDINAL_POSITION;

-- 特に日付カラムの確認
PRINT ''
PRINT '=== 日付カラムの確認 ==='
SELECT 
    TABLE_NAME as 'テーブル名',
    COLUMN_NAME as 'カラム名',
    DATA_TYPE as 'データ型'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
AND (COLUMN_NAME LIKE '%Date%' OR COLUMN_NAME LIKE '%At%')
ORDER BY TABLE_NAME, COLUMN_NAME;
"@

try {
    # sqlcmdで実行
    $result = sqlcmd -S $serverInstance -d $database -Q $query -E -s "," -W
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n実行結果:" -ForegroundColor Green
        $result
    } else {
        Write-Host "エラーが発生しました (Exit Code: $LASTEXITCODE)" -ForegroundColor Red
        $result
    }
} catch {
    Write-Host "エラー: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== 完了 ===" -ForegroundColor Green