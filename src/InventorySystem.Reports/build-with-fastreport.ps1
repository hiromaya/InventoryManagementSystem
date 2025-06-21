# FastReportビルドスクリプト
Write-Host "FastReportプロジェクトのビルド開始..." -ForegroundColor Green

# NuGetパッケージの復元
Write-Host "パッケージを復元しています..." -ForegroundColor Yellow
dotnet restore

# クリーンビルド
Write-Host "クリーンビルドを実行しています..." -ForegroundColor Yellow
dotnet clean
dotnet build --configuration Debug

# ビルド結果の確認
if ($LASTEXITCODE -eq 0) {
    Write-Host "ビルド成功！" -ForegroundColor Green
    
    # DLLがコピーされているか確認
    $outputPath = ".\bin\Debug\net8.0"
    $fastReportDll = Join-Path $outputPath "FastReport.dll"
    
    if (Test-Path $fastReportDll) {
        Write-Host "FastReport.dllが出力ディレクトリにコピーされました" -ForegroundColor Green
    } else {
        Write-Host "警告: FastReport.dllが出力ディレクトリに見つかりません" -ForegroundColor Yellow
    }
} else {
    Write-Host "ビルドエラーが発生しました" -ForegroundColor Red
}