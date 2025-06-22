# 在庫管理システム開発環境セットアップガイド

## プロジェクト概要
- **プロジェクト名**: InventoryManagementSystem
- **GitHubリポジトリ**: https://github.com/hiromaya/InventoryManagementSystem
- **開発環境**: Linux (Claude Code)
- **実行環境**: Windows Server 2022
- **主要技術**: C# (.NET 8.0), SQL Server 2022 Express, FastReport.NET

## 開発環境構成

### Linux環境（メイン開発環境）
- **用途**: 主要な開発作業、コーディング、単体テスト
- **ツール**: Claude Code / Cursor
- **制限**: FastReport.NETは動作しない（プレースホルダー実装使用）

### Windows環境（テスト環境）
- **用途**: FastReport関連のテスト、帳票生成確認、統合テスト
- **必須ソフトウェア**:
  - Git for Windows
  - .NET 8.0 SDK
  - SQL Server 2022 Express
  - Visual Studio 2022 (推奨)

## Windows環境セットアップ手順

### 1. Git for Windowsインストール
```powershell
# インストール確認
git --version
```

### 2. 作業ディレクトリ作成
```powershell
mkdir C:\Development
cd C:\Development
```

### 3. SSH鍵の生成と設定

#### SSH鍵生成
```powershell
# .sshディレクトリ作成
mkdir ~\.ssh

# SSH鍵生成（パスフレーズなし推奨）
ssh-keygen -t ed25519 -C "info@aberkam.com"
# Enter passphrase: [何も入力せずEnter]
# Enter same passphrase again: [何も入力せずEnter]

# 公開鍵表示
cat ~/.ssh/id_ed25519.pub
```

#### GitHub設定
1. GitHub → Settings → SSH and GPG keys
2. New SSH key
3. Title: "Windows Development"
4. Key: 公開鍵を貼り付け
5. Add SSH key

#### SSH接続テスト
```powershell
ssh -T git@github.com
# 初回は "yes" を入力
```

### 4. リポジトリクローン
```powershell
cd C:\Development
git clone git@github.com:hiromaya/InventoryManagementSystem.git
cd InventoryManagementSystem
```

### 5. Git設定
```powershell
# ユーザー情報設定
git config user.name "Hiroki Tsukiyama"
git config user.email "info@aberkam.com"

# 改行コード設定（Windows）
git config --global core.autocrlf true
```

## ワークフロー

### Linux環境での開発
```bash
# 作業開始
git pull origin main

# 開発作業...

# コミット＆プッシュ
git add .
git commit -m "feat: 機能説明"
git push origin main
```

### Windows環境での確認
```powershell
# 最新を取得
git pull origin main

# ビルド＆テスト
dotnet build
dotnet test

# FastReport関連のテスト実行

# 変更がある場合
git add .
git commit -m "fix: Windows環境での修正"
git push origin main
```

## トラブルシューティング

### 問題1: Zone.Identifierファイルエラー
Windows環境でクローン時に`invalid path 'filename:Zone.Identifier'`エラーが発生する場合。

**解決策**: Linux環境で不要ファイルを削除
```bash
# Linux環境で実行
find . -name "*:Zone.Identifier" -delete
find . -name "*:com.dropbox.attrs" -delete

# .gitignoreに追加
echo "*:Zone.Identifier" >> .gitignore
echo "*:com.dropbox.attrs" >> .gitignore

git add .
git commit -m "fix: Windows互換性のため不要なメタデータファイルを削除"
git push origin main
```

### 問題2: SSH認証エラー
```powershell
# SSH設定確認
ls ~/.ssh/

# リモートURL確認
git remote -v

# HTTPSからSSHに変更
git remote set-url origin git@github.com:hiromaya/InventoryManagementSystem.git
```

### 問題3: FastReport実行エラー
Windows環境でのみFastReportが動作します。Linux環境ではプレースホルダー実装が使用されます。

## プロジェクト構造
```
InventoryManagementSystem/
├── src/
│   ├── InventorySystem.Core/        # ビジネスロジック
│   ├── InventorySystem.Data/        # データアクセス層
│   ├── InventorySystem.Import/      # CSV取込処理
│   ├── InventorySystem.Reports/     # 帳票生成
│   ├── InventorySystem.Console/     # バッチ処理
│   └── InventorySystem.FileWatcher/ # ファイル監視サービス
├── database/                        # データベーススクリプト
├── 大臣出力ファイル/                  # サンプルCSVファイル
└── 開発資料/                         # 仕様書・設計書
```

## ビルドコマンド

### 開発ビルド（両環境共通）
```bash
dotnet build
```

### リリースビルド（Windows）
```powershell
dotnet build -c Release -r win-x64
```

### テスト実行
```bash
dotnet test
```

## 重要な注意事項

1. **FastReport.NET**
   - Windows環境でのみ動作
   - Linux環境ではプレースホルダー実装を使用
   - 本番デプロイはWindows Server必須

2. **データベース接続**
   - 開発環境: LocalDB または SQL Server Express
   - 接続文字列は`appsettings.json`で管理

3. **文字コード**
   - ソースコード: UTF-8
   - CSV入出力: UTF-8
   - 改行コード: CRLF (Windows)

4. **セキュリティ**
   - GitHubトークンは絶対に公開しない
   - 接続文字列等の機密情報は環境変数で管理

## 参考リンク
- [.NET 8.0 ダウンロード](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads)
- [FastReport.NET](https://www.fast-report.com/en/product/fast-report-net/)

---
最終更新日: 2025年6月22日