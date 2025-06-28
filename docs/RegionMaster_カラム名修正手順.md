# RegionMasterテーブル カラム名修正手順

## 概要
産地マスタインポート時に発生している「Invalid column name 'RegionCode'」エラーを解決するため、データベースのカラム名を修正します。

## エラーの原因
- **データベースのカラム名**: `OriginCode`, `OriginName`
- **アプリケーションが期待するカラム名**: `RegionCode`, `RegionName`

## 修正手順

### 1. SQLスクリプトの実行
以下のSQLスクリプトを実行してください：

```bash
# SQL Server Management Studio または sqlcmd を使用
sqlcmd -S [サーバー名] -d InventoryManagementDB -i database/08_fix_region_master_columns.sql
```

### 2. スクリプトの実行内容
1. 現在のカラム構造を表示
2. `OriginCode` → `RegionCode` に変更
3. `OriginName` → `RegionName` に変更
4. 変更後のカラム構造を確認
5. 動作確認クエリを実行

### 3. 実行後の確認
スクリプト実行後、以下を確認してください：

1. **カラム名の変更確認**
   ```sql
   SELECT COLUMN_NAME 
   FROM INFORMATION_SCHEMA.COLUMNS 
   WHERE TABLE_NAME = 'RegionMaster' 
   ORDER BY ORDINAL_POSITION;
   ```
   
   期待される結果：
   - RegionCode (旧: OriginCode)
   - RegionName (旧: OriginName)
   - SearchKana
   - NumericValue1～5
   - DateValue1～5
   - TextValue1～5
   - CreatedAt
   - UpdatedAt

2. **アプリケーションの動作確認**
   ```bash
   # 産地マスタのインポートを再実行
   dotnet run -- import-folder "産地マスタCSVファイルのパス"
   ```

## 注意事項
- このスクリプトは一度だけ実行してください
- 既にカラム名が変更されている場合は、スクリプトは何も変更しません
- バックアップを取ってから実行することを推奨します

## トラブルシューティング
もし問題が発生した場合：

1. **ロールバック方法**
   ```sql
   -- カラム名を元に戻す
   EXEC sp_rename 'RegionMaster.RegionCode', 'OriginCode', 'COLUMN';
   EXEC sp_rename 'RegionMaster.RegionName', 'OriginName', 'COLUMN';
   ```

2. **権限エラーの場合**
   - db_owner または ALTER 権限が必要です
   - DBA に連絡してスクリプトを実行してもらってください

## 完了後
カラム名の変更が完了したら、産地マスタのインポートが正常に動作することを確認してください。