# PowerShell script for running in development mode
Write-Host "=== Running in Development Mode ===" -ForegroundColor Green
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "Environment variables set: DOTNET_ENVIRONMENT=Development" -ForegroundColor Yellow
Write-Host ""
dotnet run $args