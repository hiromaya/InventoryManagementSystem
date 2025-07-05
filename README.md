# 在庫管理システム

販売大臣から出力されたCSVファイルを基に在庫管理を行うシステムです。

## 機能

- CSV取込機能（売上・仕入・在庫調整・各種マスタ）
- アンマッチリスト処理
- 商品日報出力
- データセット管理
- 日次終了処理

## 必要環境

- Windows 10/11
- .NET 8.0 SDK
- SQL Server 2019以降
- FastReport .NET Trial（PDF生成用）

## セットアップ

1. FastReport .NET Trialをインストール
2. データベースを作成（スクリプトは`database/scripts`フォルダ参照）
3. `appsettings.json`でデータベース接続文字列を設定

## 実行方法

### Windows環境での実行（FastReport使用）

プロジェクトには専用の実行スクリプトが含まれています：

```powershell
# PowerShellの場合
.\run.ps1 create-unmatch-list 2025-06-27

# コマンドプロンプトの場合  
run.bat create-unmatch-list 2025-06-27
```

**注意**: `dotnet run`を直接使用する場合は、必ず以下のように実行してください：
```powershell
dotnet run -c Debug -p:DefineConstants="WINDOWS" -- create-unmatch-list 2025-06-27
```

### 利用可能なコマンド

- `import-csv` - CSVファイルのインポート
- `import-folder` - フォルダ内のCSVを一括インポート
- `create-unmatch-list [日付]` - アンマッチリストの作成
- `create-daily-report [日付]` - 商品日報の作成
- `check-daily-close [日付]` - 日次終了処理の事前確認
- `daily-close [日付]` - 日次終了処理の実行

## ビルド

Windows環境専用のビルドスクリプトを使用：

```powershell
# クリーンビルド
.\build-windows.ps1 -Clean

# 通常ビルド
.\build-windows.ps1
```

## トラブルシューティング

PDF生成に問題がある場合は診断スクリプトを実行：

```powershell
.\diagnose-pdf.ps1
```

## ライセンス

このプロジェクトは内部使用専用です。