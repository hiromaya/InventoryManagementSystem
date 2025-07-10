# LocalDB を使用した修正スクリプト
Write-Host '=== LocalDB を使用した修正スクリプト ===' -ForegroundColor Cyan
Write-Host ''

# 1. LocalDBインスタンスの開始
Write-Host '1. LocalDBインスタンスを開始します...' -ForegroundColor Yellow
sqllocaldb start MSSQLLocalDB

# 2. データベース作成（存在しない場合）
Write-Host '2. データベースを作成します...' -ForegroundColor Yellow
$createDbSql = @'
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'InventoryDB')
BEGIN
    CREATE DATABASE InventoryDB;
    PRINT 'データベース InventoryDB を作成しました。';
END
ELSE
BEGIN
    PRINT 'データベース InventoryDB は既に存在します。';
END
GO
'@

$createDbSql | sqlcmd -S '(localdb)\MSSQLLocalDB' -E

# 3. データベース初期化
Write-Host ''
Write-Host '3. データベースを初期化します...' -ForegroundColor Yellow
Set-Location -Path 'C:\Development\InventoryManagementSystem\src\InventorySystem.Console'

# appsettings.json のバックアップ
Copy-Item -Path 'appsettings.json' -Destination 'appsettings.json.backup' -Force
Write-Host 'appsettings.json をバックアップしました。' -ForegroundColor Green

# 接続文字列を一時的に変更するための環境変数設定
$env:ConnectionStrings__DefaultConnection = "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=InventoryDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True"

dotnet run init-database --force

# 4. ストアドプロシージャ作成
Write-Host ''
Write-Host '4. ストアドプロシージャを作成します...' -ForegroundColor Yellow
Set-Location -Path 'C:\Development\InventoryManagementSystem'
sqlcmd -S '(localdb)\MSSQLLocalDB' -d InventoryDB -E -i database\procedures\sp_CreateCpInventoryFromInventoryMasterCumulative.sql

# 5. インデックス作成
Write-Host ''
Write-Host '5. インデックスを作成します...' -ForegroundColor Yellow
sqlcmd -S '(localdb)\MSSQLLocalDB' -d InventoryDB -E -i database\indexes\create_inventory_composite_index.sql

Write-Host ''
Write-Host '=== 修正完了 ===' -ForegroundColor Green
Write-Host ''
Write-Host '次の手順:' -ForegroundColor Yellow
Write-Host '1. appsettings.json の接続文字列を以下に更新してください:' -ForegroundColor White
Write-Host '   "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=InventoryDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True"' -ForegroundColor Cyan
Write-Host ''
Write-Host '2. アンマッチリスト処理を実行:' -ForegroundColor White
Write-Host '   dotnet run unmatch-list 2025-06-01' -ForegroundColor Cyan