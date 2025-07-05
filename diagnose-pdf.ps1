# -*- coding: utf-8 -*-
# 修正日: 2025-07-05
# 修正内容: シンタックスエラーとエンコーディング問題を修正

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# PDF生成問題診断スクリプト

Write-Host "PDF生成環境診断" -ForegroundColor Cyan
Write-Host "================" -ForegroundColor Cyan
Write-Host ""

# OS情報
Write-Host "OS情報:" -ForegroundColor Yellow
Write-Host "  OS: $([System.Environment]::OSVersion.VersionString)"
Write-Host "  .NET: $(dotnet --version)"
Write-Host ""

# WINDOWSシンボルの確認
Write-Host "コンパイルシンボル確認:" -ForegroundColor Yellow
$testCode = @'
#if WINDOWS
Console.WriteLine("  WINDOWS: 定義済み ✓");
#else
Console.WriteLine("  WINDOWS: 未定義 ✗");
#endif
'@

$tempFile = [System.IO.Path]::GetTempFileName() + ".cs"
$testCode | Out-File -FilePath $tempFile -Encoding UTF8

dotnet script eval $testCode 2>$null
if ($LASTEXITCODE -ne 0) {
    # dotnet-scriptがインストールされていない場合の代替方法
    Write-Host "  （診断ツールが利用できません）" -ForegroundColor DarkGray
}
Remove-Item $tempFile -ErrorAction SilentlyContinue
Write-Host ""

# FastReportのインストール確認
Write-Host "FastReport確認:" -ForegroundColor Yellow
$fastReportPath = "C:\Program Files (x86)\FastReports\FastReport .NET Trial\FastReport.dll"
if (Test-Path $fastReportPath) {
    Write-Host "  FastReport .NET Trial: インストール済み ✓" -ForegroundColor Green
    $fileInfo = Get-Item $fastReportPath
    Write-Host "  バージョン: $($fileInfo.VersionInfo.FileVersion)" -ForegroundColor DarkGray
} else {
    Write-Host "  FastReport .NET Trial: 未インストール ✗" -ForegroundColor Red
}
Write-Host ""

# プロジェクト設定の確認
Write-Host "プロジェクト設定:" -ForegroundColor Yellow
if (Test-Path "Directory.Build.props") {
    Write-Host "  Directory.Build.props: 存在する ✓" -ForegroundColor Green
} else {
    Write-Host "  Directory.Build.props: 存在しない ✗" -ForegroundColor Red
}
Write-Host ""

# 推奨事項
Write-Host "推奨される対処法:" -ForegroundColor Yellow
Write-Host "1. .\build-windows.ps1 -Clean を実行してクリーンビルド"
Write-Host "2. FastReport .NET Trial がインストールされていることを確認"
Write-Host "3. Windows環境で実行していることを確認"