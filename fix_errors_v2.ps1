# 在庫管理システムエラー修正スクリプト
# 実行日: 2025-07-10

Write-Host "=== 在庫管理システム エラー修正スクリプト ===" -ForegroundColor Cyan
Write-Host ""

# 1. データベース初期化（PreviousMonthInventoryテーブルを作成）
Write-Host "1. データベースを初期化します（PreviousMonthInventoryテーブルを含む）" -ForegroundColor Yellow
Set-Location -Path "C:\Development\InventoryManagementSystem\src\InventorySystem.Console"
dotnet run init-database --force

Write-Host ""
Write-Host "2. ストアドプロシージャを作成します" -ForegroundColor Yellow

# 2. ストアドプロシージャの作成 - SQLファイルを直接実行
$spPath = "C:\Development\InventoryManagementSystem\database\procedures\sp_CreateCpInventoryFromInventoryMasterCumulative.sql"
if (Test-Path $spPath) {
    sqlcmd -S localhost -d InventoryDB -E -i $spPath
    Write-Host "ストアドプロシージャを作成しました。" -ForegroundColor Green
} else {
    Write-Host "ストアドプロシージャファイルが見つかりません: $spPath" -ForegroundColor Red
}

Write-Host ""
Write-Host "3. 5項目キー複合インデックスを作成します" -ForegroundColor Yellow

# 3. インデックス作成
$indexPath = "C:\Development\InventoryManagementSystem\database\indexes\create_inventory_composite_index.sql"
if (Test-Path $indexPath) {
    sqlcmd -S localhost -d InventoryDB -E -i $indexPath
    Write-Host "インデックスを作成しました。" -ForegroundColor Green
} else {
    Write-Host "インデックスファイルが見つかりません: $indexPath" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== 修正が完了しました ===" -ForegroundColor Green
Write-Host ""
Write-Host "以下のコマンドでアンマッチリスト処理を再実行してください:" -ForegroundColor Cyan
Write-Host "dotnet run unmatch-list 2025-06-01" -ForegroundColor White