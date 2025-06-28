# RegionMasterテーブル作成手順

## エラーの原因
`Invalid column name 'RegionCode'` エラーは、RegionMasterテーブルがデータベースに存在しないことが原因です。

## 解決方法

### 1. SQL Server Management Studio (SSMS) を使用する場合

1. SSMSを開き、データベースに接続します
2. `InventoryDB`データベースを選択します
3. 以下のSQLスクリプトを実行します：
   ```
   C:\Development\InventoryManagementSystem\database\07_create_shipping_region_masters.sql
   ```

### 2. コマンドラインから実行する場合

```powershell
# PowerShellから実行
sqlcmd -S .\SQLEXPRESS -d InventoryDB -i "C:\Development\InventoryManagementSystem\database\07_create_shipping_region_masters.sql"
```

### 3. Visual Studio から実行する場合

1. Visual Studioでプロジェクトを開く
2. `データベース` > `新しいクエリ` を選択
3. SQLファイルの内容をコピー＆ペースト
4. 実行ボタンをクリック

## 確認方法

テーブルが正しく作成されたか確認するには：

```sql
-- テーブルの存在確認
SELECT * FROM sys.tables WHERE name IN ('RegionMaster', 'ShippingMarkMaster');

-- テーブル構造の確認
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length,
    c.is_nullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('RegionMaster')
ORDER BY c.column_id;

-- データ件数の確認
SELECT COUNT(*) AS RegionCount FROM RegionMaster;
SELECT COUNT(*) AS ShippingMarkCount FROM ShippingMarkMaster;
```

## 実行後の期待される結果

1. `RegionMaster`テーブルが作成される（カラム: RegionCode, RegionName, SearchKana, NumericValue1-5, DateValue1-5, TextValue1-5）
2. `ShippingMarkMaster`テーブルが作成される（同様のカラム構造）
3. 各テーブルに「未設定」レコードが1件ずつ挿入される
4. 検索用インデックスが作成される

## トラブルシューティング

### エラー: "テーブルは既に存在します"
既存のテーブルがある場合は、スクリプトは自動的にスキップします。

### エラー: "データベース 'InventoryDB' が見つかりません"
先に基本的なデータベーススクリプト（01_create_database.sql など）を実行してください。

### 権限エラー
管理者権限でSQL Serverに接続していることを確認してください。