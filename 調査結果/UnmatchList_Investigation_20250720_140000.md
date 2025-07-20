# アンマッチリスト異常動作調査報告書

作成日時: 2025-07-20 14:00:00

## 1. エグゼクティブサマリー

### 問題の概要
- **症状**: アンマッチリスト機能で異常な「該当無」件数（5152件）が発生
- **発生時期**: InventoryMaster主キー変更（6項目→5項目）後
- **影響範囲**: アンマッチリスト処理全体、CP在庫マスタとの照合処理

### 推定される原因
1. **主キー変更に伴うデータ不整合**: 6項目から5項目への主キー変更により、CP在庫マスタと伝票データの照合で大量の「該当無」が発生
2. **在庫マスタの最適化処理の問題**: `OptimizeInventoryMasterAsync`メソッドで使用される`sp_UpdateOrCreateInventoryMasterCumulative`ストアドプロシージャが5項目主キーに対応していない可能性
3. **CP在庫マスタ作成処理の不備**: `sp_CreateCpInventoryFromInventoryMasterCumulative`ストアドプロシージャがアクティブな在庫マスタから適切にデータを抽出できていない

### 影響範囲
- アンマッチリスト出力機能
- 日次終了処理
- 商品日報作成処理

## 2. 調査結果詳細

### 2.1 UnmatchListServiceの実装分析

#### 主要な処理フロー
```csharp
// UnmatchListService.cs の主要処理
1. OptimizeInventoryMasterAsync() - 在庫マスタ最適化
2. CreateCpInventoryFromInventoryMasterAsync() - CP在庫マスタ作成  
3. ClearDailyAreaAsync() - 当日エリアクリア
4. AggregateDailyDataWithValidationAsync() - データ集計
5. GenerateUnmatchListAsync() - アンマッチリスト生成
```

#### 重要な実装上の問題点

**1. CP在庫マスタとの照合処理（行286）**
```csharp
var cpInventory = await _cpInventoryRepository.GetByKeyAsync(inventoryKey, dataSetId);
if (cpInventory == null)
{
    // 該当無エラー - 商品分類1を取得
    var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
        sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode);
    
    var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "", productCategory1);
    unmatchItem.AlertType2 = "該当無";
    unmatchItems.Add(unmatchItem);
}
```

**2. 在庫マスタ最適化処理（行618）**
```csharp
processedCount = await _inventoryRepository.UpdateOrCreateFromVouchersAsync(latestJobDate, dataSetId);
```
この処理で使用される`sp_UpdateOrCreateInventoryMasterCumulative`が5項目主キーに対応していない可能性

### 2.2 ストアドプロシージャの調査結果

#### sp_CreateCpInventoryFromInventoryMasterCumulative.sql
- **目的**: 在庫マスタからCP在庫マスタにデータをコピー
- **重要な条件**:
  ```sql
  WHERE im.IsActive = 1  -- アクティブな在庫のみ対象
  AND (@JobDate IS NULL OR im.JobDate <= @JobDate)  -- 指定日以前の在庫のみ
  ```
- **問題点**: 主キー変更後、`IsActive`フィールドや`JobDate`条件が正しく機能していない可能性

#### 伝票との照合条件
```sql
EXISTS (
    SELECT 1 FROM SalesVouchers sv 
    WHERE (@JobDate IS NULL OR sv.JobDate <= @JobDate) 
    AND sv.ProductCode = im.ProductCode
    AND sv.GradeCode = im.GradeCode
    AND sv.ClassCode = im.ClassCode
    AND sv.ShippingMarkCode = im.ShippingMarkCode
    AND sv.ShippingMarkName = im.ShippingMarkName
    -- 5項目での完全一致が必要
)
```

### 2.3 主キー変更の影響調査

#### 変更前後の比較
```
変更前（6項目主キー）:
- ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate

変更後（5項目主キー）:
- ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
```

#### マイグレーション処理（102_Migrate_InventoryMaster_PK.complete.sql）
- **実行内容**: 各5項目キーの最新JobDateデータのみを保持
- **潜在的問題**: 
  1. データ削減により一部の商品データが失われた可能性
  2. `IsActive`フラグの更新が不完全
  3. 新しいインデックス構造への移行が不完全

### 2.4 CpInventoryRepositoryの実装調査

#### GetByKeyAsyncメソッド（行66-92）
```csharp
const string sql = """
    SELECT * FROM CpInventoryMaster 
    WHERE ProductCode = @ProductCode 
        AND GradeCode = @GradeCode 
        AND ClassCode = @ClassCode 
        AND ShippingMarkCode = @ShippingMarkCode 
        AND ShippingMarkName COLLATE Japanese_CI_AS = @ShippingMarkName COLLATE Japanese_CI_AS
        AND DataSetId = @DataSetId
    """;
```

**潜在的問題**:
- 文字コード照合（Japanese_CI_AS）が原因で一致しない可能性
- ShippingMarkNameの8桁固定フォーマットの不一致

## 3. 問題の根本原因

### 3.1 主要原因の特定

**根本原因**: InventoryMaster主キー変更（6項目→5項目）に伴うデータ整合性の問題

**具体的な問題**:
1. **在庫マスタの不完全性**: 主キー変更により、一部の商品の在庫マスタレコードが失われたか、`IsActive=0`になっている
2. **CP在庫マスタ作成の失敗**: `sp_CreateCpInventoryFromInventoryMasterCumulative`が期待される件数のCPレコードを作成できていない
3. **文字列照合の問題**: ShippingMarkNameの8桁固定フォーマットや文字コード照合の不一致

### 3.2 推定されるデータ状況

以下のSQLを実行して確認が必要:

```sql
-- 1. 在庫マスタの状況確認
SELECT 
    COUNT(*) as TotalCount,
    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) as ActiveCount,
    SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) as InactiveCount
FROM InventoryMaster;

-- 2. JobDate分布の確認
SELECT TOP 10 JobDate, COUNT(*) as Count 
FROM InventoryMaster 
WHERE IsActive = 1
GROUP BY JobDate 
ORDER BY JobDate DESC;

-- 3. 重複確認（5項目キーで）
SELECT 
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, 
    COUNT(*) as DuplicateCount
FROM InventoryMaster
WHERE IsActive = 1
GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
HAVING COUNT(*) > 1
ORDER BY DuplicateCount DESC;
```

## 4. 推奨される修正方針

### 4.1 immediate（即座の対応）

1. **在庫マスタの整合性確認**
   ```sql
   -- 伝票に存在するが在庫マスタにない5項目キーを特定
   SELECT DISTINCT 
       sv.ProductCode, sv.GradeCode, sv.ClassCode, 
       sv.ShippingMarkCode, sv.ShippingMarkName
   FROM SalesVouchers sv
   LEFT JOIN InventoryMaster im ON 
       sv.ProductCode = im.ProductCode
       AND sv.GradeCode = im.GradeCode
       AND sv.ClassCode = im.ClassCode
       AND sv.ShippingMarkCode = im.ShippingMarkCode
       AND sv.ShippingMarkName = im.ShippingMarkName
       AND im.IsActive = 1
   WHERE im.ProductCode IS NULL;
   ```

2. **欠損データの補完**
   - バックアップテーブル`InventoryMaster_Backup_20250720`から欠損データを復元
   - 新しい5項目主キー構造で再挿入

### 4.2 Short-term（短期的な修正）

1. **sp_UpdateOrCreateInventoryMasterCumulativeの修正**
   - 5項目主キーに対応したロジックに更新
   - `IsActive`フラグの適切な管理

2. **CP在庫マスタ作成処理の改善**
   - `sp_CreateCpInventoryFromInventoryMasterCumulative`の条件見直し
   - 文字列照合の問題解決

### 4.3 Long-term（長期的な改善）

1. **データ整合性チェック機能の実装**
   - 定期的な在庫マスタと伝票データの整合性確認
   - 自動修復機能の追加

2. **ログ機能の強化**
   - CP在庫マスタ作成時の詳細ログ
   - アンマッチ発生時の原因特定ログ

## 5. 添付資料

### 5.1 関連するコードスニペット

#### UnmatchListService.cs（主要部分）
```csharp
// 行286-296: 該当無判定ロジック
var cpInventory = await _cpInventoryRepository.GetByKeyAsync(inventoryKey, dataSetId);
if (cpInventory == null)
{
    // 該当無エラー - 商品分類1を取得
    var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
        sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode);
    
    var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "", productCategory1);
    unmatchItem.AlertType2 = "該当無";
    unmatchItems.Add(unmatchItem);
}
```

#### CpInventoryRepository.cs（GetByKeyAsync）
```csharp
// 行66-92: CP在庫マスタ検索
const string sql = """
    SELECT * FROM CpInventoryMaster 
    WHERE ProductCode = @ProductCode 
        AND GradeCode = @GradeCode 
        AND ClassCode = @ClassCode 
        AND ShippingMarkCode = @ShippingMarkCode 
        AND ShippingMarkName COLLATE Japanese_CI_AS = @ShippingMarkName COLLATE Japanese_CI_AS
        AND DataSetId = @DataSetId
    """;
```

### 5.2 関連マイグレーションファイル

1. `/database/migrations/102_Migrate_InventoryMaster_PK.complete.sql` - 主キー変更スクリプト
2. `/database/procedures/sp_CreateCpInventoryFromInventoryMasterCumulative.sql` - CP在庫マスタ作成
3. `/database/procedures/sp_UpdateOrCreateInventoryMasterCumulative.sql` - 在庫マスタ更新

### 5.3 既知の関連問題

過去の調査結果から以下の関連問題が確認されている:
- `InventoryMaster_PrimaryKey_Migration_Report_20250720.md` - 主キー変更の詳細
- `InventoryMaster_PrimaryKey_Detail_Investigation_20250720_073055.md` - 主キー変更の影響調査

---

**調査実施者**: Claude Code  
**調査完了日時**: 2025-07-20 14:00:00  
**推奨される次のアクション**: 在庫マスタの整合性確認SQLの実行とデータ補完作業の実施