# -*- coding: utf-8 -*-
# 修正日: 2025-07-05
# 修正内容: シンタックスエラーとエンコーディング問題を修正

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Windows環境用ビルドスクリプト
# FastReportを含むPDF生成機能を有効にしてビルドします

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean = $false
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Windows用在庫管理システムビルド" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 現在のディレクトリを保存
$originalLocation = Get-Location

try {
    # プロジェクトルートに移動
    $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    Set-Location $scriptPath

    # クリーンビルドの場合
    if ($Clean) {
        Write-Host "古いビルド成果物を削除中..." -ForegroundColor Yellow
        Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | ForEach-Object {
            Write-Host "  削除: $_" -ForegroundColor DarkGray
            Remove-Item $_ -Recurse -Force -ErrorAction SilentlyContinue
        }
        Write-Host "クリーンアップ完了" -ForegroundColor Green
        Write-Host ""
    }

    # NuGetパッケージの復元
    Write-Host "NuGetパッケージを復元中..." -ForegroundColor Yellow
    dotnet restore InventoryManagementSystem.sln
    if ($LASTEXITCODE -ne 0) {
        throw "NuGetパッケージの復元に失敗しました"
    }
    Write-Host "パッケージ復元完了" -ForegroundColor Green
    Write-Host ""

    # ビルド実行
    Write-Host "ビルド開始 (構成: $Configuration)..." -ForegroundColor Yellow
    dotnet build InventoryManagementSystem.sln -c $Configuration /p:DefineConstants="WINDOWS"
    if ($LASTEXITCODE -ne 0) {
        throw "ビルドに失敗しました"
    }
    Write-Host "ビルド完了" -ForegroundColor Green
    Write-Host ""

    # FastReport DLLの確認
    Write-Host "FastReport DLLの確認中..." -ForegroundColor Yellow
    $consoleOutputPath = "src\InventorySystem.Console\bin\$Configuration\net8.0-windows7.0"
    $fastReportDll = Join-Path $consoleOutputPath "FastReport.dll"
    
    if (Test-Path $fastReportDll) {
        $fileInfo = Get-Item $fastReportDll
        Write-Host "✓ FastReport.dll が見つかりました" -ForegroundColor Green
        Write-Host "  パス: $fastReportDll" -ForegroundColor DarkGray
        Write-Host "  サイズ: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor DarkGray
    } else {
        Write-Host "✗ FastReport.dll が見つかりません" -ForegroundColor Red
        Write-Host "  期待されるパス: $fastReportDll" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "FastReport .NET Trial がインストールされていることを確認してください" -ForegroundColor Yellow
    }
    Write-Host ""

    # 実行方法の案内
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "ビルド成功！" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "アプリケーションを実行するには:" -ForegroundColor Yellow
    Write-Host "  cd src\InventorySystem.Console" -ForegroundColor White
    Write-Host "  dotnet run -- create-unmatch-list 2025-06-27" -ForegroundColor White
    Write-Host ""
    Write-Host "または直接実行:" -ForegroundColor Yellow
    Write-Host "  .\$consoleOutputPath\InventorySystem.Console.exe create-unmatch-list 2025-06-27" -ForegroundColor White
    
} catch {
    Write-Host ""
    Write-Host "エラー: $_" -ForegroundColor Red
    exit 1
} finally {
    # 元のディレクトリに戻る
    Set-Location $originalLocation
}