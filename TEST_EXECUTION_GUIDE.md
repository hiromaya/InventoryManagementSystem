# 在庫管理システム テスト実行ガイド

## 📋 テスト実行環境

### ✅ 完了事項
- .NET 8.0 インストール完了
- 全プロジェクトのコンパイル成功
- QuestPDF ライセンス設定完了
- データベーススキーマ作成済み
- テストデータ準備済み

### 🔧 前提条件
- Windows環境（SQL Server LocalDB使用）
- .NET 8.0 ランタイム
- SQL Server LocalDB または SQL Server Express

## 🚀 テスト実行手順

### 1. データベースセットアップ（初回のみ）

```bash
# データベーススキーマ作成
sqlcmd -S "(localdb)\MSSQLLocalDB" -i database/create_schema.sql

# テストデータ投入
sqlcmd -S "(localdb)\MSSQLLocalDB" -i database/insert_test_data.sql
```

### 2. 基本動作確認

```bash
cd src/InventorySystem.Console

# ヘルプ表示
dotnet run

# 期待される出力:
# 使用方法:
#   dotnet run unmatch-list [YYYY-MM-DD]  - アンマッチリスト処理を実行
#   例: dotnet run unmatch-list 2025-06-16
```

### 3. アンマッチリスト処理テスト

```bash
# 本日日付でアンマッチリスト処理実行
dotnet run unmatch-list

# 指定日付でアンマッチリスト処理実行
dotnet run unmatch-list 2025-06-16
```

### 4. 期待される処理結果

#### 成功時の出力例:
```
=== アンマッチリスト処理開始 ===
ジョブ日付: 2025-06-16

=== 処理結果 ===
データセットID: 12345678-1234-1234-1234-123456789012
アンマッチ件数: 3
処理時間: 2.45秒

=== アンマッチ一覧 ===
掛売上 | 00008 | テスト商品H | 在庫0
掛売上 | 99999 | | 該当無
掛買 | 88888 | | 該当無

PDF生成中...
PDF出力完了: unmatch_list_20250616_143022.pdf
=== アンマッチリスト処理完了 ===
```

#### 想定されるアンマッチパターン:
1. **在庫0エラー** - 在庫数量が0なのに売上がある
2. **該当無エラー** - 在庫マスタに存在しない商品
3. **除外データ** - EXIT、9900番台荷印は集計対象外

### 5. PDF出力確認

- 実行ディレクトリに `unmatch_list_YYYYMMDD_HHMMSS.pdf` が生成される
- A3横向きレイアウト
- アンマッチデータの詳細表示
- 件数情報の表示

## 🧪 テストケース

### テストデータ構成

| 商品コード | 商品名 | 在庫数 | 特徴 | 期待結果 |
|------------|--------|--------|------|----------|
| 00001 | テスト商品A | 100.00 | 通常商品 | 正常処理 |
| 00002 | テスト商品B | 200.00 | 通常商品 | 正常処理 |
| 00004 | テスト商品D | 30.00 | EXIT荷印 | 除外処理 |
| 00005 | テスト商品E | 80.00 | 9910荷印 | 除外処理 |
| 00006 | テスト商品F | 60.00 | 9aaa特殊 | 分類1='8' |
| 00008 | テスト商品H | 0.00 | 在庫0 | 在庫0エラー |
| 99999 | - | - | 存在しない | 該当無エラー |

### 売上伝票テストパターン

| 伝票番号 | 商品コード | 数量 | 期待結果 |
|----------|------------|------|----------|
| S0001 | 00001 | -10.00 | 正常 |
| S0003 | 00008 | -5.00 | 在庫0エラー |
| S0004 | 99999 | -8.00 | 該当無エラー |

## 🐛 トラブルシューティング

### よくあるエラー

#### 1. データベース接続エラー
```
Error: Connection string 'DefaultConnection' not found
```
**解決方法**: appsettings.jsonの接続文字列を確認

#### 2. SQL Server LocalDB エラー
```
Error: Cannot connect to (localdb)\MSSQLLocalDB
```
**解決方法**: SQL Server LocalDBをインストール、または接続文字列を変更

#### 3. PDF生成エラー
```
PDF生成エラー: License error
```
**解決方法**: QuestPDF Community Licenseの設定を確認

### ログ確認
```bash
# ログファイル確認
tail -f logs/inventory-console-2025-06-16.log
```

## 📊 パフォーマンステスト

### 目標パフォーマンス
- CP在庫マスタ作成: 3秒以内
- データ集計処理: 2秒以内
- アンマッチ判定: 1秒以内
- PDF生成: 2秒以内
- **合計処理時間: 8秒以内**

### 大量データテスト
```sql
-- 大量テストデータ生成（10,000件）
INSERT INTO InventoryMaster (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, ...)
SELECT 
    FORMAT(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)), '00000'),
    '001', '001', '1001', 'テスト荷印' + CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS NVARCHAR(10)),
    ...
FROM sys.objects a, sys.objects b
WHERE ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) <= 10000;
```

## 🔄 継続的テスト

### 定期実行スクリプト
```bash
#!/bin/bash
# daily_test.sh

echo "=== 日次テスト開始 $(date) ==="

# 前日データクリア
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DELETE FROM CpInventoryMaster; DELETE FROM ProcessingHistory;"

# アンマッチリスト処理実行
cd /path/to/InventorySystem.Console
dotnet run unmatch-list $(date +%Y-%m-%d)

echo "=== 日次テスト完了 $(date) ==="
```

## 📋 テスト完了チェックリスト

- [ ] ビルド成功確認
- [ ] ヘルプ表示確認
- [ ] データベース接続確認
- [ ] CP在庫マスタ作成確認
- [ ] アンマッチ判定動作確認
- [ ] PDF生成確認
- [ ] ログ出力確認
- [ ] エラーハンドリング確認
- [ ] パフォーマンス確認

---

**最終更新**: 2025年6月16日  
**テスト環境**: .NET 8.0, SQL Server LocalDB