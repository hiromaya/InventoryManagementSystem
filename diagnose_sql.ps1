# SQL Server 診断スクリプト
Write-Host '=== SQL Server 診断スクリプト ===' -ForegroundColor Cyan
Write-Host ''

# 1. SQL Serverサービスの状態を確認
Write-Host '1. SQL Serverサービスの状態を確認中...' -ForegroundColor Yellow
$services = Get-Service -Name 'MSSQL*' -ErrorAction SilentlyContinue
if ($services) {
    $services | Format-Table Name, Status, DisplayName -AutoSize
} else {
    Write-Host 'SQL Serverサービスが見つかりません。' -ForegroundColor Red
}

# 2. LocalDBインスタンスの確認
Write-Host ''
Write-Host '2. LocalDBインスタンスを確認中...' -ForegroundColor Yellow
$localdbResult = & sqllocaldb info 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host $localdbResult
} else {
    Write-Host 'LocalDBが見つかりません。' -ForegroundColor Red
}

# 3. 接続テスト
Write-Host ''
Write-Host '3. SQL Server接続テスト...' -ForegroundColor Yellow

# テスト1: localhost\SQLEXPRESS
Write-Host '- localhost\SQLEXPRESS への接続テスト' -ForegroundColor Cyan
$test1 = sqlcmd -S 'localhost\SQLEXPRESS' -E -Q "SELECT @@VERSION" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host '  成功!' -ForegroundColor Green
} else {
    Write-Host '  失敗' -ForegroundColor Red
}

# テスト2: (localdb)\MSSQLLocalDB
Write-Host '- (localdb)\MSSQLLocalDB への接続テスト' -ForegroundColor Cyan
$test2 = sqlcmd -S '(localdb)\MSSQLLocalDB' -E -Q "SELECT @@VERSION" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host '  成功!' -ForegroundColor Green
} else {
    Write-Host '  失敗' -ForegroundColor Red
}

# テスト3: localhost (デフォルトインスタンス)
Write-Host '- localhost への接続テスト' -ForegroundColor Cyan
$test3 = sqlcmd -S 'localhost' -E -Q "SELECT @@VERSION" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host '  成功!' -ForegroundColor Green
} else {
    Write-Host '  失敗' -ForegroundColor Red
}

# 4. データベース確認
Write-Host ''
Write-Host '4. 使用可能な接続でデータベースを確認中...' -ForegroundColor Yellow

# SQLEXPRESS
$dbCheckSqlExpress = sqlcmd -S 'localhost\SQLEXPRESS' -E -Q "SELECT name FROM sys.databases WHERE name IN ('InventoryDB', 'InventoryManagementDB')" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host '- localhost\SQLEXPRESS のデータベース:' -ForegroundColor Cyan
    Write-Host $dbCheckSqlExpress
}

# LocalDB
$dbCheckLocalDB = sqlcmd -S '(localdb)\MSSQLLocalDB' -E -Q "SELECT name FROM sys.databases WHERE name IN ('InventoryDB', 'InventoryManagementDB')" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host '- (localdb)\MSSQLLocalDB のデータベース:' -ForegroundColor Cyan
    Write-Host $dbCheckLocalDB
}

Write-Host ''
Write-Host '=== 診断完了 ===' -ForegroundColor Green
Write-Host ''
Write-Host '推奨事項:' -ForegroundColor Yellow
Write-Host '1. 上記の結果で「成功」と表示された接続を使用してください。' -ForegroundColor White
Write-Host '2. appsettings.json の接続文字列を適切に更新してください。' -ForegroundColor White
Write-Host '3. データベース名も確認してください（InventoryDB または InventoryManagementDB）。' -ForegroundColor White