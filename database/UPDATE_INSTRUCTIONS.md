# InventoryMasterテーブル更新手順書

## 概要
このドキュメントは、InventoryMasterテーブルのスキーマ更新手順を説明します。
「Invalid column name 'ProductName'」エラーの解決を目的としています。

## 更新内容
InventoryMasterテーブルに以下のカラムを追加/確認します：
- ProductName (NVARCHAR(100))
- DailyGrossProfit (DECIMAL(18,4))
- DailyAdjustmentAmount (DECIMAL(18,4))
- DailyProcessingCost (DECIMAL(18,4))
- FinalGrossProfit (DECIMAL(18,4))
- DataSetId (NVARCHAR(50))
- PreviousMonthQuantity (DECIMAL(18,4))
- PreviousMonthAmount (DECIMAL(18,4))

## 前提条件
- SQL Server 2019以上
- InventoryManagementDBデータベースへのアクセス権限
- ALTER TABLE権限

## 実行手順

### Windows環境

#### 1. コマンドプロンプトまたはPowerShellを管理者権限で起動

#### 2. プロジェクトルートディレクトリに移動
```bash
cd C:\Development\InventoryManagementSystem
```

#### 3. スキーマ更新スクリプトの実行

**SQL Server認証を使用する場合：**
```bash
sqlcmd -S localhost\SQLEXPRESS -U sa -P yourpassword -d InventoryManagementDB -i database\migrations\AddMissingColumnsToInventoryMaster.sql
```

**Windows統合認証を使用する場合：**
```bash
sqlcmd -S localhost\SQLEXPRESS -E -d InventoryManagementDB -i database\migrations\AddMissingColumnsToInventoryMaster.sql
```

#### 4. 実行結果の確認
スクリプトは以下のようなメッセージを出力します：
```
ProductNameカラムは既に存在します。
DailyGrossProfitカラムは既に存在します。
...
===== スクリプト実行完了 =====
全てのカラムが正常に確認/追加されました。
```

### Linux環境

#### 1. ターミナルを開く

#### 2. プロジェクトルートディレクトリに移動
```bash
cd ~/projects/InventoryManagementSystem
```

#### 3. スキーマ更新スクリプトの実行
```bash
sqlcmd -S localhost -U sa -P yourpassword -d InventoryManagementDB -i database/migrations/AddMissingColumnsToInventoryMaster.sql
```

### SQL Server Management Studio (SSMS) を使用する場合

1. SSMSを起動し、SQL Serverに接続
2. InventoryManagementDBデータベースを選択
3. 新しいクエリウィンドウを開く
4. `database/migrations/AddMissingColumnsToInventoryMaster.sql`の内容をコピー＆ペースト
5. 実行（F5キー）

## 検証方法

### 1. テーブル構造の確認
以下のSQLを実行して、全てのカラムが存在することを確認：

```sql
USE InventoryManagementDB;
GO

SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'InventoryMaster'
ORDER BY ORDINAL_POSITION;
```

### 2. アプリケーションの動作確認
```bash
cd src/InventorySystem.Console
dotnet run unmatch-list 2025-06-13
```

エラーが発生しなければ、更新は成功です。

## トラブルシューティング

### エラー: "Invalid column name 'ProductName'"
スキーマ更新スクリプトが正常に実行されていない可能性があります。
1. 上記の検証SQLでProductNameカラムの存在を確認
2. 存在しない場合は、スクリプトを再実行

### エラー: "Login failed for user"
データベース接続情報を確認してください：
- サーバー名
- ユーザー名とパスワード
- データベース名

### エラー: "Permission denied"
ALTER TABLE権限が不足しています。管理者に権限付与を依頼してください。

## ロールバック手順

万が一問題が発生した場合のロールバック手順：

```sql
-- 注意：既存データがある場合、データ損失の可能性があります
-- バックアップを取ってから実行してください

ALTER TABLE InventoryMaster DROP COLUMN ProductName;
ALTER TABLE InventoryMaster DROP COLUMN DailyGrossProfit;
ALTER TABLE InventoryMaster DROP COLUMN DailyAdjustmentAmount;
ALTER TABLE InventoryMaster DROP COLUMN DailyProcessingCost;
ALTER TABLE InventoryMaster DROP COLUMN FinalGrossProfit;
ALTER TABLE InventoryMaster DROP COLUMN DataSetId;
ALTER TABLE InventoryMaster DROP COLUMN PreviousMonthQuantity;
ALTER TABLE InventoryMaster DROP COLUMN PreviousMonthAmount;
```

## 注意事項

1. **本番環境での実行前に必ずバックアップを取得してください**
2. スクリプトは冪等性を持っており、複数回実行しても安全です
3. 既存データがある場合、ProductNameカラムは`'商' + ProductCode`で自動補完されます

## 関連ファイル

- `/database/migrations/AddMissingColumnsToInventoryMaster.sql` - スキーマ更新スクリプト
- `/src/InventorySystem.Data/Repositories/InventoryRepository.cs` - 修正されたリポジトリ
- `/src/InventorySystem.Core/Entities/InventoryMaster.cs` - エンティティクラス