# データベースセットアップガイド

在庫管理システムのデータベースセットアップ手順です。

## 📋 前提条件

以下のいずれかのSQL Serverが必要です：

1. **SQL Server Express LocalDB** (推奨)
2. **SQL Server Express**
3. **SQL Server Developer Edition**
4. **SQL Server Standard/Enterprise**

## 🚀 セットアップ手順

### 1. SQL Server Express LocalDB の場合

```bash
# LocalDBインスタンスの確認
sqllocaldb info

# インスタンスが存在しない場合は作成
sqllocaldb create MSSQLLocalDB

# インスタンスを開始
sqllocaldb start MSSQLLocalDB

# 接続テスト
sqllocaldb info MSSQLLocalDB
```

### 2. データベース作成

**SQL Server Management Studio (SSMS) を使用:**

1. SSMSを開く
2. 接続文字列に従ってSQL Serverに接続
3. `CreateDatabase.sql` を開いて実行

**コマンドラインを使用:**

```bash
# LocalDBの場合
sqlcmd -S "(localdb)\MSSQLLocalDB" -i database/CreateDatabase.sql

# SQL Server Expressの場合
sqlcmd -S ".\SQLEXPRESS" -E -i database/CreateDatabase.sql
```

### 3. テストデータ投入（オプション）

```bash
# LocalDBの場合
sqlcmd -S "(localdb)\MSSQLLocalDB" -i database/InsertTestData.sql

# SQL Server Expressの場合
sqlcmd -S ".\SQLEXPRESS" -E -i database/InsertTestData.sql
```

### 4. 接続確認

```bash
cd src/InventorySystem.Console
dotnet run test-connection
```

## ⚙️ 接続文字列の設定

### LocalDB (デフォルト)
```json
"DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=InventoryManagementDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False"
```

### SQL Server Express
```json
"DefaultConnection": "Data Source=.\\SQLEXPRESS;Initial Catalog=InventoryManagementDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False"
```

### ローカルSQL Server
```json
"DefaultConnection": "Data Source=localhost;Initial Catalog=InventoryManagementDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False"
```

### SQL Server認証
```json
"DefaultConnection": "Data Source=localhost;Initial Catalog=InventoryManagementDB;User ID=sa;Password=YourPassword;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False"
```

## 📊 作成されるテーブル

| テーブル名 | 説明 | 主キー |
|-----------|------|--------|
| InventoryMaster | 在庫マスタ | 5項目複合キー |
| CpInventoryMaster | CP在庫マスタ | 5項目複合キー + DataSetId |
| SalesVoucher | 売上伝票 | VoucherId + LineNumber |
| PurchaseVoucher | 仕入伝票 | VoucherId + LineNumber |
| InventoryAdjustment | 在庫調整 | VoucherId + LineNumber |
| DataSet | データセット管理 | Id |

## 🔧 トラブルシューティング

### LocalDB関連エラー

```bash
# エラー: "LocalDB is not supported on this platform"
# → SQL Server Express を使用してください

# エラー: "LocalDB インスタンスが見つからない"
sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

### 接続エラー

```bash
# サーバーが見つからない場合
# 1. SQL Server Browser サービスが起動していることを確認
# 2. TCP/IP プロトコルが有効になっていることを確認
# 3. ファイアウォール設定を確認
```

### 認証エラー

```bash
# Windows認証が使用できない場合
# → SQL Server認証を使用するか、接続文字列を変更
```

## 📝 設定ファイル

| ファイル | 用途 |
|---------|------|
| `appsettings.json` | Linux/LocalDB用設定 |
| `appsettings.windows.json` | Windows/SQL Server Express用設定 |

## 🧪 テスト

```bash
# データベース接続テスト
dotnet run test-connection

# PDF生成テスト（DB不要）
dotnet run test-pdf

# 実際の機能テスト
dotnet run unmatch-list
dotnet run daily-report
dotnet run inventory-list
```

## 📚 参考情報

- [SQL Server Express のダウンロード](https://www.microsoft.com/ja-jp/sql-server/sql-server-downloads)
- [SQL Server Management Studio (SSMS)](https://docs.microsoft.com/ja-jp/sql/ssms/download-sql-server-management-studio-ssms)
- [LocalDB について](https://docs.microsoft.com/ja-jp/sql/database-engine/configure-windows/sql-server-express-localdb)