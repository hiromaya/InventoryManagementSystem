以下の追加情報をClaude Codeに提供することで、より正確な修正が可能になります：

# import-folderコマンド修正のための追加情報

## プロジェクト構造

```
InventoryManagementSystem/
├── src/
│   ├── InventorySystem.Core/
│   │   ├── Interfaces/
│   │   │   └── Repositories/
│   │   │       ├── ISalesVoucherRepository.cs
│   │   │       ├── IPurchaseVoucherRepository.cs
│   │   │       └── IInventoryAdjustmentRepository.cs
│   │   └── Entities/
│   │       └── Masters/
│   │           └── RegionMaster.cs
│   ├── InventorySystem.Data/
│   │   └── Repositories/
│   │       ├── SalesVoucherRepository.cs
│   │       ├── PurchaseVoucherRepository.cs
│   │       └── InventoryAdjustmentRepository.cs
│   └── InventorySystem.Console/
│       └── Program.cs
```

## 現在のデータベース状況

### テーブル名一覧
- `RegionMaster` (旧OriginMaster、変更済み)
- `GradeMaster` (219件)
- `ClassMaster` (198件)
- `ShippingMarkMaster` (784件)
- `ProductMaster` (1037件)
- `SalesVouchers`
- `PurchaseVouchers`
- `InventoryAdjustments`

### 問題の詳細
1. **データ重複**: 同一JobDateで3回データが登録されている
2. **処理順序**: マスタファイルが伝票より後に処理される場合がある
3. **産地マスタ**: テーブル名変更済み（OriginMaster → RegionMaster）

## CSVファイルの場所と形式

### ファイル配置
```
D:\InventoryImport\
└── DeptA\
    └── Import\
        ├── 等級汎用マスター１.csv      # 全角数字使用
        ├── 階級汎用マスター２.csv
        ├── 荷印汎用マスター３.csv
        ├── 産地汎用マスター４.csv
        ├── 商品.csv
        ├── 得意先.csv
        ├── 仕入先.csv
        ├── 単位.csv
        ├── 前月末在庫.csv
        ├── 売上伝票_20250628.csv     # 日付付き
        ├── 仕入伝票_20250628.csv
        └── 受注伝票.csv              # 在庫調整として処理
```

### CSV仕様
- 文字コード: UTF-8 with BOM
- 区切り文字: カンマ
- ヘッダー: あり（1行目）
- 改行コード: CRLF

## 既存の処理フロー

1. CSVファイルを読み込み
2. 各ファイルタイプに応じたImportServiceを使用
3. データベースに保存
4. 処理済みフォルダに移動

## 修正で期待される動作

### 1. 重複防止
- 同一JobDateの既存データを削除してから新規インポート
- トランザクション管理で一貫性を保証

### 2. 処理順序
```
Phase 1: マスタデータ（必須）
  └─ 等級、階級、荷印、産地、商品、得意先、仕入先、単位

Phase 2: 初期在庫（任意）
  └─ 前月末在庫

Phase 3: 伝票データ（必須）
  └─ 売上伝票、仕入伝票、在庫調整（受注伝票）
```

### 3. エラーハンドリング
- マスタファイルのエラー: 処理を中断
- 伝票ファイルのエラー: スキップして続行
- すべてのエラーをログに記録

## コマンド実行例

```bash
# 基本実行
dotnet run -- import-folder DeptA 2025-06-28

# 結果確認SQL
SELECT JobDate, COUNT(*) as RecordCount 
FROM SalesVouchers 
GROUP BY JobDate 
HAVING COUNT(*) > 1000;  -- 重複チェック
```

## 注意事項

1. **パフォーマンス**: 大量データ削除時は`TRUNCATE`より`DELETE`を使用（JobDate指定のため）
2. **並行処理**: 現時点では考慮不要（シングルスレッド処理）
3. **バックアップ**: 削除前のデータバックアップは別途実装済み