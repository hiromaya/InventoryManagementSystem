@echo off
REM Windows環境用テストスクリプト
echo =================================
echo 在庫管理システム Windows テスト
echo =================================

REM 現在のディレクトリを確認
echo 現在のディレクトリ: %CD%

REM プロジェクトディレクトリに移動
cd /d "%~dp0.."

REM ビルド実行
echo.
echo === プロジェクトビルド中... ===
dotnet build
if %ERRORLEVEL% neq 0 (
    echo ビルドに失敗しました。エラーを確認してください。
    pause
    exit /b 1
)

echo.
echo === ビルド成功 ===

REM テスト用CSVファイルが存在するか確認
if not exist "test_sales.csv" (
    echo テスト用売上CSVファイルを作成中...
    echo V001,2025-06-18,2025-06-18,51,1,C001,得意先1,P001,りんご,A,1,M001,荷印1,10,100,1000,1,2,3 > test_sales.csv
    echo V002,2025-06-18,2025-06-18,51,1,C002,得意先2,P002,みかん,B,2,M002,荷印2,5,150,750,1,2,3 >> test_sales.csv
)

if not exist "test_purchase.csv" (
    echo テスト用仕入CSVファイルを作成中...
    echo V101,2025-06-18,2025-06-18,61,1,S001,仕入先1,P001,りんご,A,1,M001,荷印1,20,80,1600,1,2,3 > test_purchase.csv
    echo V102,2025-06-18,2025-06-18,61,1,S002,仕入先2,P002,みかん,B,2,M002,荷印2,15,100,1500,1,2,3 >> test_purchase.csv
)

REM コンソールアプリのディレクトリに移動
cd src\InventorySystem.Console

echo.
echo === 利用可能なコマンド ===
echo 1. 売上伝票取込テスト: dotnet run import-sales ..\..\test_sales.csv 2025-06-18
echo 2. 仕入伝票取込テスト: dotnet run import-purchase ..\..\test_purchase.csv 2025-06-18
echo 3. PDFテスト: dotnet run test-pdf
echo.

REM PDFテストを実行
echo === PDFテスト実行 ===
dotnet run test-pdf
if %ERRORLEVEL% neq 0 (
    echo PDFテストに失敗しました。
) else (
    echo PDFテスト成功
)

echo.
echo === テスト完了 ===
echo データベースのセットアップが完了している場合は、以下のコマンドでCSV取込をテストできます：
echo   dotnet run import-sales ..\..\test_sales.csv 2025-06-18
echo   dotnet run import-purchase ..\..\test_purchase.csv 2025-06-18
echo.
pause