#Requires -Version 5.1
<#
.SYNOPSIS
    在庫管理システムの実行スクリプト
.DESCRIPTION
    FastReportを使用してPDF生成を行うため、必要な設定を自動的に適用します
.EXAMPLE
    .\run.ps1 create-unmatch-list 2025-06-27
.EXAMPLE
    .\run.ps1 create-daily-report 2025-06-27
.EXAMPLE
    .\run.ps1 import-csv
#>
[CmdletBinding()]
param(
    [Parameter(Position=0, Mandatory=$true)]
    [ValidateSet('create-unmatch-list', 'create-daily-report', 'import-csv', 'import-folder', 'check-daily-close', 'daily-close')]
    [string]$Command,
    
    [Parameter(Position=1, ValueFromRemainingArguments=$true)]
    [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'
$originalLocation = Get-Location

try {
    # スクリプトのルートディレクトリを取得
    $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    $consolePath = Join-Path $scriptPath "src\InventorySystem.Console"
    
    if (-not (Test-Path $consolePath)) {
        throw "Console プロジェクトが見つかりません: $consolePath"
    }
    
    Set-Location $consolePath
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "在庫管理システム" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "コマンド: $Command $($Arguments -join ' ')" -ForegroundColor White
    Write-Host ""
    
    # ビルド済みかチェック
    $exePath = ".\bin\Debug\net8.0-windows7.0\InventorySystem.Console.exe"
    if (-not (Test-Path $exePath)) {
        Write-Host "ビルドが必要です。ビルドを開始します..." -ForegroundColor Yellow
        & dotnet build -c Debug -p:DefineConstants="WINDOWS"
        if ($LASTEXITCODE -ne 0) {
            throw "ビルドに失敗しました"
        }
    }
    
    # FastReport DLLの存在確認
    $fastReportDll = ".\bin\Debug\net8.0-windows7.0\FastReport.dll"
    if (Test-Path $fastReportDll) {
        Write-Host "✓ FastReport.dll 確認済み" -ForegroundColor Green
    } else {
        Write-Host "⚠ FastReport.dll が見つかりません" -ForegroundColor Yellow
    }
    
    # 実行
    Write-Host ""
    Write-Host "実行中..." -ForegroundColor Cyan
    & dotnet run -c Debug -p:DefineConstants="WINDOWS" -- $Command $Arguments
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✓ 正常に完了しました" -ForegroundColor Green
    } else {
        throw "エラーが発生しました (終了コード: $LASTEXITCODE)"
    }
    
} catch {
    Write-Host ""
    Write-Host "✗ エラー: $_" -ForegroundColor Red
    exit 1
} finally {
    Set-Location $originalLocation
}