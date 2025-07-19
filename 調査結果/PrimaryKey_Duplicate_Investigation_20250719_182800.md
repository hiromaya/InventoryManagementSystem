# InventoryMaster 主キー重複問題 調査報告書

## 調査日時
2025-07-19 18:28:00

## エラー概要
- エラーメッセージ: Violation of PRIMARY KEY constraint 'PK_InventoryMaster'
- 重複キー値: (00104, 000, 000, 5106,         )
- 発生箇所: InheritPreviousDayInventoryAsync
- 発生コマンド: dotnet run import-folder DeptA 2025-06-02

## 1. InventoryMasterテーブルの主キー構成

### docs/database/01_create_tables.sqlからの主キー定義

```sql
CONSTRAINT PK_InventoryMaster PRIMARY KEY (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate
)
```

### 主キーを構成するカラム
1. **ProductCode** (NVARCHAR(15)) - 商品コード
2. **GradeCode** (NVARCHAR(15)) - 等級コード  
3. **ClassCode** (NVARCHAR(15)) - 階級コード
4. **ShippingMarkCode** (NVARCHAR(15)) - 荷印コード
5. **ShippingMarkName** (NVARCHAR(50)) - 荷印名
6. **JobDate** (DATE) - 汎用日付2（ジョブデート）

**重要**: 6項目による複合主キー構成

## 2. 現在のInheritPreviousDayInventoryAsync実装

### SQL文の構造

```sql
INSERT INTO InventoryMaster (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
    ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
    JobDate, CreatedDate, UpdatedDate,
    CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount, DailyFlag,
    PreviousMonthQuantity, PreviousMonthAmount
)
SELECT 
    prev.ProductCode, prev.GradeCode, prev.ClassCode, 
    prev.ShippingMarkCode, prev.ShippingMarkName,
    prev.ProductName, prev.Unit, prev.StandardPrice, 
    prev.ProductCategory1, prev.ProductCategory2,
    @JobDate, GETDATE(), GETDATE(),
    prev.CurrentStock, prev.CurrentStockAmount,
    prev.CurrentStock, prev.CurrentStockAmount,
    '9',
    prev.PreviousMonthQuantity, prev.PreviousMonthAmount
FROM InventoryMaster prev
WHERE CAST(prev.JobDate AS DATE) = CAST(@PreviousDate AS DATE)
    AND NOT EXISTS (
        SELECT 1 FROM InventoryMaster curr
        WHERE curr.ProductCode = prev.ProductCode
            AND curr.GradeCode = prev.GradeCode
            AND curr.ClassCode = prev.ClassCode
            AND curr.ShippingMarkCode = prev.ShippingMarkCode
            AND curr.ShippingMarkName = prev.ShippingMarkName
            AND CAST(curr.JobDate AS DATE) = CAST(@JobDate AS DATE)
    );
```

### 重複チェックの現状
- [x] **重複チェックあり** - NOT EXISTS句で実装
- [ ] 重複チェックなし

### 重複チェックの問題点
NOT EXISTS句は5項目キー（ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName）+ JobDateで重複をチェックしているが、以下の問題がある：

1. **ShippingMarkNameの正規化処理が不統一**
   - 前日データ取得時：`prev.ShippingMarkName`（生の値）
   - 重複チェック時：`curr.ShippingMarkName`（生の値）
   - 他の箇所では：`LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8)`

2. **データ正規化の不整合**
   - 前日引き継ぎ時に8桁固定長処理が行われていない
   - 他のメソッドでは一貫して8桁固定長処理を実施

## 3. 他のメソッドでの重複処理パターン

### MergeInventoryMasterAsync
- ストアドプロシージャ`sp_MergeInventoryMasterCumulative`を使用
- MERGE文による重複処理（詳細はストアドプロシージャ内で実装）

### HandleMonthStartInventoryAsync（月初処理）
```sql
-- 前月末在庫のみ存在する商品を新規追加
INSERT INTO InventoryMaster (...)
SELECT ...
FROM PreviousMonthInventory pmi
WHERE ...
    AND NOT EXISTS (
        SELECT 1 FROM InventoryMaster im
        WHERE im.ProductCode = pmi.ProductCode
            AND im.GradeCode = pmi.GradeCode
            AND im.ClassCode = pmi.ClassCode
            AND im.ShippingMarkCode = pmi.ShippingMarkCode
            AND LEFT(RTRIM(COALESCE(im.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = 
                 LEFT(RTRIM(COALESCE(pmi.ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
            AND CAST(im.JobDate AS DATE) = CAST(@JobDate AS DATE)
    );
```

**重要**: 月初処理では正しく8桁固定長の正規化処理を実装している

### 他の商品取得メソッド（GetSalesProductsAsync等）
```sql
SELECT DISTINCT 
    ProductCode, GradeCode, ClassCode, ShippingMarkCode,
    LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName
FROM SalesVouchers
WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)
```

**重要**: すべての商品取得メソッドで8桁固定長処理を一貫して実装

## 4. 5番目のキー値が空白の理由

### エラーメッセージの分析
重複キー値: `(00104, 000, 000, 5106,         )`

5番目の値（ShippingMarkName）が8文字の空白スペースになっている理由：

1. **ShippingMarkNameの8桁固定長仕様**
   - 荷印名は必ず8桁固定長で管理される
   - 空の場合は8文字の空白スペースで埋められる

2. **正規化処理の仕様**
   ```sql
   LEFT(RTRIM(COALESCE(ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
   ```
   - 空の場合：`LEFT('' + '        ', 8)` = `'        '`（8文字の空白）
   - この正規化処理がInheritPreviousDayInventoryAsyncでは適用されていない

3. **データ不整合の発生**
   - 前日データでShippingMarkNameが空の場合
   - 他の処理で同じキーが8桁空白で作成される
   - 前日引き継ぎ時に生の空文字と8桁空白で重複が発生

## 5. OptimizeAsyncの処理フロー

### 処理順序
1. **売上商品の取得** (`GetSalesProductsAsync`) ✓8桁正規化済み
2. **仕入商品の取得** (`GetPurchaseProductsAsync`) ✓8桁正規化済み  
3. **在庫調整商品の取得** (`GetAdjustmentProductsAsync`) ✓8桁正規化済み
4. **商品の統合** (重複除去)
5. **前日在庫の引き継ぎ** (`InheritPreviousDayInventoryAsync`) ❌8桁正規化なし
6. **MERGE処理** (`MergeInventoryMasterAsync`)

### 問題の発生タイミング
- ステップ5の前日引き継ぎ時に、正規化されていないShippingMarkNameで重複エラーが発生
- ステップ1-3で既に正規化されたキーが在庫マスタに存在する可能性

### 既存データのクリア処理
**なし** - 前日引き継ぎ前に当日データをクリアする処理は実装されていない

## 6. 類似の引き継ぎ処理の実装確認

### 月初処理との比較
| 項目 | 月初処理 | 前日引き継ぎ |
|------|----------|-------------|
| ShippingMarkName正規化 | ✓実装済み | ❌未実装 |
| 重複チェック方法 | 正規化後で比較 | 生の値で比較 |
| データソース | PreviousMonthInventory | InventoryMaster |

### 重複チェック方法の違い
- **月初処理**: 正規化されたShippingMarkNameで重複チェック（推奨）
- **前日引き継ぎ**: 生のShippingMarkNameで重複チェック（問題の原因）

## 7. 推奨される修正方針

### 方法1: ShippingMarkNameの正規化処理を追加（推奨）
**概要**: InheritPreviousDayInventoryAsyncメソッドで8桁固定長処理を適用

**修正内容**:
```sql
-- SELECT句での正規化
LEFT(RTRIM(COALESCE(prev.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName

-- NOT EXISTS句での正規化
AND LEFT(RTRIM(COALESCE(curr.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = 
    LEFT(RTRIM(COALESCE(prev.ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
```

**理由**:
- 他のメソッドとの実装一貫性を保つ
- 月初処理と同じパターンを適用
- 根本的な不整合を解決

### 方法2: 既存データクリア後の引き継ぎ
**概要**: 前日引き継ぎ前に当日の在庫マスタをクリア

**修正内容**:
```sql
-- 当日データを削除
DELETE FROM InventoryMaster 
WHERE CAST(JobDate AS DATE) = CAST(@JobDate AS DATE);

-- その後、前日データを引き継ぎ
INSERT INTO InventoryMaster (...)
SELECT ... FROM InventoryMaster prev
WHERE CAST(prev.JobDate AS DATE) = CAST(@PreviousDate AS DATE);
```

**理由**:
- 確実に重複を回避
- シンプルな実装

**リスク**:
- 既存の当日データが完全に失われる
- 月初処理との競合リスク

## 8. リスクと考慮事項

### 方法1のリスク
- 既存の前日データでShippingMarkNameが不正な形式の場合、引き継ぎ対象から除外される可能性
- 過去データの不整合が顕在化する可能性

### 方法2のリスク  
- 月初処理と前日引き継ぎの実行順序依存
- データロスの可能性
- パフォーマンスへの影響（大量データ削除）

### 共通考慮事項
- トランザクション管理の重要性
- ログ出力での追跡可能性
- 既存データの事前バックアップ

## 9. 実装例（参考）

### 月初処理の正しい実装パターン
```sql
-- HandleMonthStartInventoryAsyncより抜粋
SELECT 
    pmi.ProductCode, pmi.GradeCode, pmi.ClassCode, 
    pmi.ShippingMarkCode, 
    LEFT(RTRIM(COALESCE(pmi.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName,
    -- その他のカラム
FROM PreviousMonthInventory pmi
WHERE CAST(pmi.JobDate AS DATE) = CAST(@JobDate AS DATE)
    AND NOT EXISTS (
        SELECT 1 FROM InventoryMaster im
        WHERE im.ProductCode = pmi.ProductCode
            AND im.GradeCode = pmi.GradeCode  
            AND im.ClassCode = pmi.ClassCode
            AND im.ShippingMarkCode = pmi.ShippingMarkCode
            AND LEFT(RTRIM(COALESCE(im.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = 
                 LEFT(RTRIM(COALESCE(pmi.ShippingMarkName, '')) + REPLICATE(' ', 8), 8)
            AND CAST(im.JobDate AS DATE) = CAST(@JobDate AS DATE)
    );
```

この実装パターンを前日引き継ぎ処理に適用することで、主キー重複問題を根本的に解決できる見込みです。

## 10. 結論

**主要な問題**: InheritPreviousDayInventoryAsyncメソッドでShippingMarkNameの8桁固定長正規化処理が実装されておらず、他の処理で作成された正規化済みデータと重複エラーが発生

**推奨修正方法**: 方法1（ShippingMarkNameの正規化処理追加）
- 実装一貫性の確保
- 根本原因の解決
- 他の処理への影響が最小限

**優先度**: 高 - 日次処理の安定性に直結する重要な問題