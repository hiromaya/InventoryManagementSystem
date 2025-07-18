# 実際のデータベーススキーマ確認スクリプト
# Geminiの指示に基づいて作成

# 接続文字列の定義
$serverInstance = "localhost\SQLEXPRESS"
$database = "InventoryManagementDB"
$sqlFile = "database/check_actual_schema.sql"

Write-Host "=== 実際のデータベーススキーマ確認開始 ===" -ForegroundColor Green
Write-Host "接続先: $serverInstance" -ForegroundColor Yellow
Write-Host "データベース: $database" -ForegroundColor Yellow
Write-Host "SQLファイル: $sqlFile" -ForegroundColor Yellow

# sqlcmdが利用可能かチェック
try {
    $null = Get-Command sqlcmd -ErrorAction Stop
    Write-Host "sqlcmd が利用可能です" -ForegroundColor Green
} catch {
    Write-Host "エラー: sqlcmd が見つかりません" -ForegroundColor Red
    Write-Host "SQL Server Command Line Utilities をインストールしてください" -ForegroundColor Red
    exit 1
}

# SQLファイルの存在確認
if (-not (Test-Path $sqlFile)) {
    Write-Host "エラー: SQLファイルが見つかりません: $sqlFile" -ForegroundColor Red
    exit 1
}

# SQL実行
Write-Host "`n=== SQL実行開始 ===" -ForegroundColor Green
try {
    # Windows認証を使用してSQL実行
    $result = sqlcmd -S $serverInstance -d $database -i $sqlFile -E -b
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SQL実行成功" -ForegroundColor Green
        Write-Host "`n=== 実行結果 ===" -ForegroundColor Cyan
        $result | Write-Host
    } else {
        Write-Host "SQL実行エラー (Exit Code: $LASTEXITCODE)" -ForegroundColor Red
        $result | Write-Host
    }
} catch {
    Write-Host "SQL実行中にエラーが発生しました: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== スキーマ確認完了 ===" -ForegroundColor Green