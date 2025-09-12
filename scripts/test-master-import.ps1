# マスタデータ取込テストスクリプト
$projectRoot = Get-Location

Write-Host "=== マスタデータ取込テスト ===" -ForegroundColor Green
Write-Host "プロジェクトルート: $projectRoot"
Write-Host ""

# CSV存在確認
$csvFiles = @{
    "得意先マスタ" = @("得意先.csv", "大臣出力ファイル\得意先.csv")
    "商品マスタ" = @("商品.csv", "大臣出力ファイル\商品.csv")
    "仕入先マスタ" = @("仕入先.csv", "大臣出力ファイル\仕入先.csv")
    "等級マスタ" = @("等級汎用マスター1.csv", "大臣出力ファイル\等級汎用マスター１.csv")
    "階級マスタ" = @("階級汎用マスター2.csv", "大臣出力ファイル\階級汎用マスター２.csv")
    "荷印マスタ" = @("荷印汎用マスター3.csv", "大臣出力ファイル\荷印汎用マスター３.csv")
}

Write-Host "■ CSVファイル確認" -ForegroundColor Cyan
foreach ($master in $csvFiles.Keys) {
    Write-Host "`n[$master]" -ForegroundColor Yellow
    $found = $false
    
    foreach ($path in $csvFiles[$master]) {
        $fullPath = Join-Path $projectRoot $path
        if (Test-Path $fullPath) {
            Write-Host "  ✓ $path が見つかりました" -ForegroundColor Green
            
            # ヘッダー行を表示（UTF-8とShift-JIS両方試す）
            try {
                $header = Get-Content $fullPath -TotalCount 1 -Encoding UTF8
                Write-Host "    ヘッダー(UTF-8): $header" -ForegroundColor Gray
            } catch {
                try {
                    $header = Get-Content $fullPath -TotalCount 1 -Encoding Default
                    Write-Host "    ヘッダー(Shift-JIS): $header" -ForegroundColor Gray
                } catch {
                    Write-Host "    ヘッダー読み取り失敗" -ForegroundColor Red
                }
            }
            
            # ファイルサイズと行数
            $fileInfo = Get-Item $fullPath
            $lineCount = (Get-Content $fullPath).Count
            Write-Host "    サイズ: $($fileInfo.Length) bytes, 行数: $lineCount" -ForegroundColor Gray
            
            $found = $true
            break
        }
    }
    
    if (-not $found) {
        Write-Host "  ✗ CSVファイルが見つかりません" -ForegroundColor Red
        Write-Host "    探した場所:" -ForegroundColor Gray
        foreach ($path in $csvFiles[$master]) {
            Write-Host "      - $path" -ForegroundColor Gray
        }
    }
}

Write-Host "`n■ データベース接続確認" -ForegroundColor Cyan
Write-Host "実行コマンド: dotnet run test-connection" -ForegroundColor Gray

Write-Host "`n■ マスタ取込コマンド例" -ForegroundColor Cyan
Write-Host "dotnet run import-customers '大臣出力ファイル\得意先.csv'" -ForegroundColor Yellow
Write-Host "dotnet run import-products '大臣出力ファイル\商品.csv'" -ForegroundColor Yellow
Write-Host "dotnet run import-suppliers '大臣出力ファイル\仕入先.csv'" -ForegroundColor Yellow

Write-Host "`n■ 注意事項" -ForegroundColor Cyan
Write-Host "- CSVファイルのエンコーディングは自動判定されます（UTF-8 BOM付き優先、次にShift-JIS）" -ForegroundColor Gray
Write-Host "- ヘッダー名が一致しない場合、詳細なエラーログが出力されます" -ForegroundColor Gray
Write-Host "- 既存データは全て削除されてから新規データが投入されます" -ForegroundColor Gray

Write-Host "`n=== テストスクリプト完了 ===" -ForegroundColor Green