# Windows環境セットアップガイド

## 前提条件

1. **.NET 8.0 SDK** のインストール
   - [Microsoft公式サイト](https://dotnet.microsoft.com/download/dotnet/8.0)からダウンロードしてインストール

2. **SQL Server** のインストール
   - SQL Server 2019以降または SQL Server Express
   - SQL Server Management Studio (SSMS) 推奨

## セットアップ手順

### 1. プロジェクトの配置

```bash
# プロジェクトをダウンロード・展開
git clone [リポジトリURL]
cd InventoryManagementSystem
```

### 2. データベースのセットアップ

1. SQL Server Management Studio (SSMS) を開く
2. `scripts/windows-setup.sql` を開いて実行
3. データベース `InventoryManagement` が作成されることを確認

または、コマンドラインから：
```bash
sqlcmd -S localhost -E -i scripts\windows-setup.sql
```

### 3. 接続文字列の設定

`src/InventorySystem.Console/appsettings.json` を編集：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=localhost;Initial Catalog=InventoryManagement;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True"
  }
}
```

**注意**: SQL Server認証を使用する場合：
```json
"DefaultConnection": "Data Source=localhost;Initial Catalog=InventoryManagement;User ID=sa;Password=YourPassword;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True"
```

### 4. プロジェクトのビルドとテスト

```bash
# プロジェクトルートディレクトリで実行
dotnet build

# テストスクリプト実行
scripts\test-windows.bat
```

## テスト実行

### 自動テスト
```bash
scripts\test-windows.bat
```

### 手動テスト

1. **PDFテスト**
```bash
cd src\InventorySystem.Console
dotnet run test-pdf
```

2. **CSV取込テスト**
```bash
# 売上伝票取込
dotnet run import-sales ..\..\test_sales.csv 2025-06-18

# 仕入伝票取込  
dotnet run import-purchase ..\..\test_purchase.csv 2025-06-18
```

## 利用可能なコマンド

```bash
# ヘルプ表示
dotnet run

# 売上伝票CSV取込
dotnet run import-sales <csvファイル> [日付]

# 仕入伝票CSV取込
dotnet run import-purchase <csvファイル> [日付]

# 在庫調整CSV取込
dotnet run import-adjustment <csvファイル> [日付]

# アンマッチリスト処理
dotnet run unmatch-list [日付]

# PDFテスト
dotnet run test-pdf
```

## CSVファイル形式

### 売上伝票 (SalesVoucher)
```
伝票番号,伝票日付,ジョブ日付,伝票種別,明細種別,得意先コード,得意先名,商品コード,商品名,等級コード,階級コード,荷印コード,荷印名,数量,単価,金額,商品分類1,商品分類2,商品分類3
```

### 仕入伝票 (PurchaseVoucher)  
```
伝票番号,伝票日付,ジョブ日付,伝票種別,明細種別,仕入先コード,仕入先名,商品コード,商品名,等級コード,階級コード,荷印コード,荷印名,数量,単価,金額,商品分類1,商品分類2,商品分類3
```

## トラブルシューティング

### 1. ビルドエラー
```bash
# パッケージの復元
dotnet restore

# クリーンビルド
dotnet clean
dotnet build
```

### 2. データベース接続エラー
- SQL Serverサービスが起動していることを確認
- 接続文字列の確認
- ファイアウォール設定の確認

### 3. 日本語フォントの問題
- システムに日本語フォント（メイリオ、Yu Gothic等）がインストールされていることを確認

### 4. ログの確認
実行時のログは以下に出力されます：
- コンソール出力
- `logs/inventory-console-[日付].log`

## フォルダ構成

```
InventoryManagementSystem/
├── src/
│   ├── InventorySystem.Console/      # メインアプリケーション
│   ├── InventorySystem.Core/         # ビジネスロジック
│   ├── InventorySystem.Data/         # データアクセス層
│   ├── InventorySystem.Import/       # CSV取込機能
│   └── InventorySystem.Reports/      # レポート生成
├── scripts/
│   ├── windows-setup.sql            # データベースセットアップ
│   └── test-windows.bat             # テストスクリプト
├── test_sales.csv                   # テスト用売上CSV
├── test_purchase.csv                # テスト用仕入CSV
└── logs/                            # ログ出力先
```

## 本番運用について

1. **セキュリティ**
   - SQL Server認証の使用を推奨
   - 適切なユーザー権限の設定

2. **パフォーマンス**
   - 大量データ処理時はバッチサイズの調整
   - インデックスの最適化

3. **バックアップ**
   - 定期的なデータベースバックアップ
   - ログファイルのローテーション

4. **監視**
   - エラーログの監視
   - パフォーマンスメトリクスの収集