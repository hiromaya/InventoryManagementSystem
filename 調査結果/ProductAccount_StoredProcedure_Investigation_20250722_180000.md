# 商品勘定ストアドプロシージャ未作成問題 調査結果

**調査日時**: 2025年7月22日 18:00:00  
**調査対象**: ストアドプロシージャ `sp_CreateProductLedgerData` が見つからないエラー (2812)  
**現象**: `procedures/sp_CreateProductLedgerData.sql` が実行されているにも関わらずストアドプロシージャが見つからない

## 🔍 調査結果サマリー

### 重大な発見事項

1. **DataSets/DataSetManagement二重管理モードで起動している問題**
   - `appsettings.json` で `"UseDataSetManagementOnly": false` が設定されている
   - これにより古いDataSetsテーブルとの二重管理モードで動作
   - DataSetManagementへの完全移行が未完了

2. **ストアドプロシージャ作成ログは出力されているが実際には見つからない矛盾**
   - DatabaseInitializationServiceで「✅ ストアドプロシージャ作成完了: sp_CreateProductLedgerData (22ms)」とログ出力
   - しかし実行時に「Could not find stored procedure 'sp_CreateProductLedgerData'」エラー

## 🔧 詳細調査結果

### 1. 起動時ログの分析

```
🔄 DataSets/DataSetManagement二重管理モードで起動
info: InventorySystem.Data.Services.Development.DatabaseInitializationService[0]
      🔧 ストアドプロシージャを作成中: sp_CreateProductLedgerData
info: InventorySystem.Data.Services.Development.DatabaseInitializationService[0]
      マイグレーション実行中: procedures/sp_CreateProductLedgerData.sql
info: InventorySystem.Data.Services.Development.DatabaseInitializationService[0]
      マイグレーション完了: procedures/sp_CreateProductLedgerData.sql (22ms)
info: InventorySystem.Data.Services.Development.DatabaseInitializationService[0]
      ✅ ストアドプロシージャ作成完了: sp_CreateProductLedgerData (22ms)
```

**矛盾点**: 作成ログは成功しているが、実行時に見つからない

### 2. 接続文字列の分析

ProductAccountFastReportServiceで使用されている接続文字列:
```csharp
private string GetConnectionString()
{
    return Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
        ?? "Server=(localdb)\\mssqllocaldb;Database=InventoryManagementDB;Trusted_Connection=true;";
}
```

DatabaseInitializationServiceでの接続文字列との差異可能性あり。

### 3. 設定ファイルの問題

`appsettings.json`:
```json
"Features": {
    "UseDataSetManagementOnly": false,
    "EnableDataSetsMigrationLog": true
}
```

**問題**: `UseDataSetManagementOnly` が `false` のため二重管理モードで動作している

### 4. ストアドプロシージャファイルの状態

- ファイル存在: ✅ `/database/procedures/sp_CreateProductLedgerData.sql`
- 実際のテーブル構造対応済み: ✅
- DatabaseInitializationServiceの_migrationOrderに登録済み: ✅

### 5. 考えられる原因

#### A. データベースコンテキストの不整合
- DatabaseInitializationServiceと実行時で異なるデータベースに接続している可能性
- `master` データベースでストアドプロシージャが作成され、`InventoryManagementDB` では参照できない状況

#### B. スキーマの問題
- ストアドプロシージャが `dbo` 以外のスキーマに作成されている可能性
- 実行時にスキーマ名の明示が必要

#### C. 権限問題
- ストアドプロシージャの作成権限はあるが実行権限がない
- 実行ユーザーとストアドプロシージャの所有者が異なる

#### D. トランザクションの問題
- ストアドプロシージャ作成がコミットされていない
- 別のコネクションから参照しようとしている

## 🎯 推奨解決策

### 優先度1: DataSets完全削除とDataSetManagement専用化

1. `appsettings.json` を修正:
```json
"Features": {
    "UseDataSetManagementOnly": true,
    "EnableDataSetsMigrationLog": false
}
```

2. DataSetsテーブルの完全削除:
- `DataSets` テーブルをDROP
- `LegacyDataSetService` クラスの削除
- `IDataSetService` の旧実装削除

### 優先度2: ストアドプロシージャ作成の検証強化

1. DatabaseInitializationServiceの修正:
- ストアドプロシージャ作成後に存在確認クエリを実行
- 実際のデータベースコンテキストを確認

2. 接続文字列の統一:
- 全サービスで同一の接続文字列使用を保証
- IConfigurationを使用した統一的な接続文字列管理

### 優先度3: デバッグ用確認クエリの追加

```sql
-- ストアドプロシージャの存在確認
SELECT 
    name, 
    schema_name(schema_id) as schema_name,
    create_date, 
    modify_date,
    OBJECT_ID(name) as object_id
FROM sys.procedures 
WHERE name = 'sp_CreateProductLedgerData';

-- 現在のデータベースコンテキスト確認
SELECT DB_NAME() as current_database;
```

## 🚨 緊急対応が必要な理由

1. **DataSets二重管理**: 完全移行したはずのDataSetManagementが未完了
2. **ログと実際の動作の乖離**: 作成成功ログが出ているのに実行で失敗
3. **データ整合性リスク**: 二重管理により予期しないデータ不整合の可能性

## 📋 次のアクションアイテム

1. `UseDataSetManagementOnly: true` に変更
2. DataSetsテーブルと関連コードの削除
3. ストアドプロシージャ作成の検証強化
4. 接続文字列の統一確認
5. デバッグ用確認クエリの実行

## 📊 影響範囲

- **直接影響**: 商品勘定帳票機能が使用不可
- **間接影響**: DataSets二重管理によるデータ不整合リスク
- **システム全体**: データセット管理の信頼性低下

---

**調査者**: Claude Code  
**調査完了時刻**: 2025-07-22 18:00:00  
**推奨アクション**: 即座にDataSetManagement専用モードに移行し、ストアドプロシージャ作成の検証を強化する