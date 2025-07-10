# 開発環境での実行方法

## 問題と解決方法

開発用コマンド（`dev-daily-report`、`dev-check-daily-close`）を実行する際、環境変数が正しく設定されていないと、Production環境として認識され、日付・時間制限が適用されてしまいます。

## 実行方法

### 方法1: バッチファイルを使用（推奨）

**コマンドプロンプトの場合：**
```cmd
dev.bat dev-daily-report 2025-06-01
dev.bat dev-check-daily-close 2025-06-01
```

**PowerShellの場合：**
```powershell
.\dev.ps1 dev-daily-report 2025-06-01
.\dev.ps1 dev-check-daily-close 2025-06-01
```

### 方法2: 環境変数を手動設定

**コマンドプロンプトの場合：**
```cmd
set DOTNET_ENVIRONMENT=Development
set ASPNETCORE_ENVIRONMENT=Development
dotnet run dev-daily-report 2025-06-01
```

**PowerShellの場合：**
```powershell
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run dev-daily-report 2025-06-01
```

### 方法3: 一行で実行（PowerShell）

```powershell
$env:DOTNET_ENVIRONMENT="Development"; $env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run dev-daily-report 2025-06-01
```

## 開発用コマンド一覧

| コマンド | 説明 | 制限解除 |
|---------|------|----------|
| `dev-daily-report` | 商品日報作成 | 7日以内の日付制限を解除 |
| `dev-check-daily-close` | 日次終了確認 | 15:00以降の時間制限を解除 |
| `dev-daily-close` | 日次終了処理 | 検証スキップ可能 |

## 通常コマンドとの違い

通常コマンド（`daily-report`、`check-daily-close`）は本番環境での使用を想定しており、以下の制限があります：
- 日付制限：7日以内
- 時間制限：15:00以降

開発用コマンドはこれらの制限を解除し、過去データでのテストを可能にします。

## トラブルシューティング

### 「環境: Production」と表示される場合

環境変数が正しく設定されていません。以下を確認してください：
1. `dev.bat` または `dev.ps1` を使用しているか
2. 環境変数 `DOTNET_ENVIRONMENT` が "Development" に設定されているか

### 確認方法

```powershell
# 環境変数の確認
echo $env:DOTNET_ENVIRONMENT
echo $env:ASPNETCORE_ENVIRONMENT
```

正しく設定されていれば、両方とも "Development" と表示されます。