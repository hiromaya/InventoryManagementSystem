# PowerShell script for running in development mode
Write-Host "=== 開発環境モードで実行 ===" -ForegroundColor Green
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "環境変数を設定しました: DOTNET_ENVIRONMENT=Development" -ForegroundColor Yellow
Write-Host ""
dotnet run $args