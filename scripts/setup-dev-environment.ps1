# 開発環境のセットアップスクリプト
Write-Host "開発環境をセットアップしています..." -ForegroundColor Green

# プロジェクトルートの取得
$projectRoot = Get-Location

# .envファイルの作成（既存の場合はスキップ）
$envFile = Join-Path $projectRoot ".env"
if (-not (Test-Path $envFile)) {
    @"
ASPNETCORE_ENVIRONMENT=Development
"@ | Out-File -FilePath $envFile -Encoding UTF8
    Write-Host "✓ .envファイルを作成しました" -ForegroundColor Green
} else {
    Write-Host "- .envファイルは既に存在します" -ForegroundColor Yellow
}

# 開発用データフォルダの作成
$dataPath = Join-Path $projectRoot "data/InventoryImport"
$departments = @("DeptA", "DeptB", "DeptC")

foreach ($dept in $departments) {
    $importPath = Join-Path $dataPath "$dept/Import"
    $processedPath = Join-Path $dataPath "$dept/Processed"
    $errorPath = Join-Path $dataPath "$dept/Error"
    
    New-Item -ItemType Directory -Path $importPath -Force | Out-Null
    New-Item -ItemType Directory -Path $processedPath -Force | Out-Null
    New-Item -ItemType Directory -Path $errorPath -Force | Out-Null
    
    Write-Host "✓ 部門 $dept のフォルダを作成しました" -ForegroundColor Green
}

# gitignoreに開発用データフォルダを追加
$gitignore = Join-Path $projectRoot ".gitignore"
$ignoreEntry = "data/"
if (Test-Path $gitignore) {
    $content = Get-Content $gitignore
    if (-not ($content -contains $ignoreEntry)) {
        Add-Content -Path $gitignore -Value "`n$ignoreEntry"
        Write-Host "✓ .gitignoreに'data/'を追加しました" -ForegroundColor Green
    }
}

Write-Host "`n=== 開発環境のセットアップが完了しました ===" -ForegroundColor Green
Write-Host ""
Write-Host "フォルダ構造:" -ForegroundColor Cyan
Write-Host "  data/InventoryImport/"
Write-Host "  ├── DeptA/"
Write-Host "  │   ├── Import/       # CSVファイルをここに配置"
Write-Host "  │   ├── Processed/    # 処理済みファイル"
Write-Host "  │   └── Error/        # エラーファイル"
Write-Host "  ├── DeptB/ (同様の構造)"
Write-Host "  └── DeptC/ (同様の構造)"
Write-Host ""
Write-Host "使用方法:" -ForegroundColor Cyan
Write-Host "1. CSVファイルを対応する部門のImportフォルダに配置"
Write-Host "2. import-salesコマンドで取込処理を実行"
Write-Host "3. 処理済みファイルはProcessed、エラーはErrorフォルダへ自動移動"