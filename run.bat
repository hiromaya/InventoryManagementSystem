@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo 在庫管理システム
echo ========================================
echo.

cd /d "%~dp0src\InventorySystem.Console"

echo 実行中: %*
echo.

dotnet run -c Debug -p:DefineConstants="WINDOWS" -- %*

if !errorlevel! equ 0 (
    echo.
    echo ✓ 正常に完了しました
) else (
    echo.
    echo ✗ エラーが発生しました
    exit /b !errorlevel!
)

endlocal