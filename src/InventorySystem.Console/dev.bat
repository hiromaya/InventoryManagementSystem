@echo off
echo === 開発環境モードで実行 ===
set DOTNET_ENVIRONMENT=Development
set ASPNETCORE_ENVIRONMENT=Development
echo 環境変数を設定しました: DOTNET_ENVIRONMENT=Development
echo.
dotnet run %*