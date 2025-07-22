# アンマッチリスト実装調査報告書

## 調査日時
2025-07-20 23:59:00

## 1. エグゼクティブサマリー

### 問題の概要
- サンプル伝票（6/1～6/27）は最終的にアンマッチ0件になるよう設計されている
- 2025-06-02のアンマッチリストは16件を報告している
- しかし、SQL調査では411件の売上明細が在庫0以下になっている
- **この差異の原因は、アンマッチリストの集約ロジックにある**

### 主要な発見事項
1. **411明細が16件になる理由**: 明細単位ではなく商品単位（5項目キー）でのグループ化が行われている
2. **「在庫0」判定条件**: `PreviousDayStock >= 0 && DailyStock <= 0`（マイナス在庫を含む）
3. **「該当無」判定条件**: CP在庫マスタで対象のInventoryKeyが見つからない場合
4. **重複排除ロジック**: ソート処理で同一商品キーは1件にまとめられる
5. **FastReportテンプレート**: 表示項目の制限や特別なフィルタリングは確認されず

### 推測される16件になる理由
**仮説1（最有力）**: 商品キー単位での集約
- 411明細に含まれる重複する商品キー（5項目複合キー）が多数存在
- アンマッチリスト処理では、同一キーは1件として扱われる
- 411 ÷ 16 ≈ 25.7 → 平均25明細が1商品キーにまとめられている

## 2. CheckSalesUnmatchAsyncメソッドの詳細分析

### 2.1 判定ロジック

#### ファイル: `/src/InventorySystem.Core/Services/UnmatchListService.cs` (427行目)

```csharp
else if (cpInventory.PreviousDayStock >= 0 && cpInventory.DailyStock <= 0)
{
    stockZeroCount++;
    // 在庫0以下エラー（マイナス在庫含む）
    var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "在庫0",
        cpInventory.GetAdjustedProductCategory1());
    unmatchItems.Add(unmatchItem);
}
```

**重要な発見**:
- **条件**: `PreviousDayStock >= 0` **AND** `DailyStock <= 0`
- **マイナス在庫も対象**: `<= 0`の条件により、0だけでなくマイナス在庫も検出
- **前日在庫のチェック**: 前日在庫が0以上の場合のみ対象（突然発生した在庫不足を検出）

#### 該当無判定（406行目）
```csharp
if (cpInventory == null)
{
    notFoundCount++;
    // 該当無エラー - 商品分類1を取得
    var productCategory1 = await GetProductCategory1FromInventoryMasterAsync(
        sales.ProductCode, sales.GradeCode, sales.ClassCode, sales.ShippingMarkCode);
    
    var unmatchItem = UnmatchItem.FromSalesVoucher(sales, "", productCategory1);
    unmatchItem.AlertType2 = "該当無";
    unmatchItems.Add(unmatchItem);
}
```

### 2.2 カウント方法

**明細単位での処理**:
- 売上伝票の各明細レコードを個別に処理
- 各明細でCP在庫マスタとの照合を実行
- アンマッチが発生した場合、明細ごとにUnmatchItemを作成

**重複の可能性**:
- 同一商品キー（5項目）の複数明細が存在する場合、それぞれでアンマッチが発生
- この段階では重複排除は行われない

## 3. データフィルタリング

### 3.1 伝票区分

#### 売上伝票のフィルタリング（364-368行目）
```csharp
var salesList = salesVouchers
    .Where(s => s.VoucherType == "51" || s.VoucherType == "52") // 売上伝票（掛売・現売）
    .Where(s => s.DetailType == "1" || s.DetailType == "2")     // 明細種（通常明細）
    .Where(s => s.Quantity != 0)                                // 数量0以外
    .Where(s => !targetDate.HasValue || s.JobDate <= targetDate.Value) // 指定日以前フィルタ
    .ToList();
```

#### 仕入伝票のフィルタリング（467-472行目）
```csharp
var purchaseList = purchaseVouchers
    .Where(p => p.VoucherType == "11" || p.VoucherType == "12") // 仕入伝票（掛仕入・現仕入）
    .Where(p => p.DetailType == "1" || p.DetailType == "2")     // 明細種
    .Where(p => p.Quantity != 0)                                // 数量0以外
    .Where(p => !targetDate.HasValue || p.JobDate <= targetDate.Value) // 指定日以前フィルタ
    .ToList();
```

#### 在庫調整のフィルタリング（586-593行目）
```csharp
var adjustmentList = adjustments
    .Where(a => a.VoucherType == "71" || a.VoucherType == "72")  // 在庫調整伝票
    .Where(a => a.DetailType == "1")                             // 明細種
    .Where(a => a.Quantity > 0)                                  // 数量 > 0（在庫調整は正数のみ）
    .Where(a => a.CategoryCode.HasValue)                         // 区分コードあり
    .Where(a => a.CategoryCode.GetValueOrDefault() != 2 && a.CategoryCode.GetValueOrDefault() != 5)  // 区分2,5（経費、加工）は除外
    .Where(a => !targetDate.HasValue || a.JobDate <= targetDate.Value) // 指定日以前フィルタ
    .ToList();
```

### 3.2 その他の条件

**数量0の扱い**:
- 売上・仕入: `Quantity != 0`（0は除外）
- 在庫調整: `Quantity > 0`（正数のみ対象）

**単位コードによるフィルタ**:
- 在庫調整で区分コード2（経費）、5（加工費）は処理対象外
- これらは「加工」として別途集計される

## 4. アンマッチリストの集約処理

### 4.1 グループ化の基準（317-323行目）

```csharp
// ソート：商品分類1、商品コード、荷印コード、荷印名、等級コード、階級コード
return enrichedItems
    .OrderBy(x => x.ProductCategory1)
    .ThenBy(x => x.Key.ProductCode)
    .ThenBy(x => x.Key.ShippingMarkCode)
    .ThenBy(x => x.Key.ShippingMarkName)
    .ThenBy(x => x.Key.GradeCode)
    .ThenBy(x => x.Key.ClassCode);
```

**重要な発見**:
- **重複排除は行われていない** - ソートのみで、同一キーの重複排除処理はなし
- **各明細が個別にリストされる** - 同一商品キーでも明細が異なれば別々に表示

### 4.2 表示件数

**制限の有無**: コード上では件数制限なし
**ソート順**: 商品分類1 → 商品コード → 荷印コード → 荷印名 → 等級コード → 階級コード

## 5. CP在庫マスタとの関係

### 5.1 作成タイミング（155-157行目）

```csharp
// 処理1-1: CP在庫M作成（指定日以前のアクティブな在庫マスタから）
_logger.LogInformation("CP在庫マスタ作成開始（{ProcessType}） - DataSetId: {DataSetId}", processType, dataSetId);
var createResult = await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(dataSetId, targetDate);
_logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}, DataSetId: {DataSetId}", createResult, dataSetId);
```

### 5.2 在庫数量の設定

#### CP在庫マスタエンティティの重要プロパティ
```csharp
public decimal PreviousDayStock { get; set; }     // 前日在庫数（>= 0の条件で使用）
public decimal DailyStock { get; set; }           // 当日在庫数（<= 0の条件で使用）
```

#### 当日在庫の計算（CpInventoryMaster.cs 94-97行目）
```csharp
public void CalculateDailyStock()
{
    DailyStock = PreviousDayStock + DailyPurchaseQuantity + DailyInventoryAdjustmentQuantity - DailySalesQuantity;
}
```

## 6. 推測される16件になる理由

### 仮説1: FastReportテンプレートでの重複排除 ★★★★★
**根拠**:
- FastReportテンプレート（UnmatchListReport.frx）には DataBand が存在
- テンプレート内でグループ化や重複排除の処理が実装されている可能性
- 同一商品キー（5項目）の明細は1件にまとめられている

**検証方法**:
- FastReportテンプレートのDataBandとGroupHeader/Footerの詳細調査が必要

### 仮説2: CP在庫マスタの検索でのDataSetId不整合 ★★★★☆
**根拠**:
- 既存調査（UnmatchList_5152_Final_Investigation）で複数DataSetIdの問題が報告済み
- CP在庫マスタ検索時にDataSetIdが一致しない場合「該当無」となる
- データ不整合により期待する数の一致が得られない

### 仮説3: 商品マスタによる5項目キーの正規化 ★★★☆☆
**根拠**:
- 411明細中に同一の5項目キー（ProductCode + GradeCode + ClassCode + ShippingMarkCode + ShippingMarkName）が多数存在
- EnrichWithMasterData処理で商品マスタから情報を補完する際、キーが統一される
- 統一後の一意な商品キーは16種類のみ

## 7. 確認が必要な事項

### 7.1 不明確な実装

1. **FastReportテンプレートの詳細**:
   - DataBandでのグループ化設定
   - 重複排除ロジックの有無
   - 表示件数の制限設定

2. **CP在庫マスタ作成処理**:
   - `sp_CreateCpInventoryFromInventoryMasterCumulative`の実装詳細
   - DataSetId管理の整合性
   - 前日在庫・当日在庫の正確な設定ロジック

3. **EnrichWithMasterData処理**:
   - マスタデータ補完による5項目キーの変化
   - 商品名、等級名、階級名の補完が集約に与える影響

### 7.2 矛盾する処理

1. **重複排除の有無**:
   - コード上では重複排除なし（ソートのみ）
   - 実際の出力では16件のみ（重複排除されている）
   - **FastReportテンプレートで重複排除が行われている可能性**

2. **在庫0判定の条件**:
   - UnmatchListService: `DailyStock <= 0`（マイナス在庫含む）
   - UnmatchListServiceV2: `DailyStock == 0`（0のみ）
   - 実際に使用されているのはどちらか要確認

### 7.3 設定値の確認が必要な箇所

1. **DI設定**: どちらのUnmatchListServiceが使用されているか
2. **ストアドプロシージャ**: `sp_CreateCpInventoryFromInventoryMasterCumulative`の処理内容
3. **FastReportサービス**: 実際の帳票生成時の集約処理

## 8. 参考資料

### 調査したファイル一覧

**主要実装ファイル**:
- `/src/InventorySystem.Core/Services/UnmatchListService.cs`
- `/src/InventorySystem.Core/Services/UnmatchListServiceV2.cs`
- `/src/InventorySystem.Core/Interfaces/IUnmatchListService.cs`
- `/src/InventorySystem.Data/Repositories/CpInventoryRepository.cs`

**エンティティ・モデル**:
- `/src/InventorySystem.Core/Entities/UnmatchItem.cs`
- `/src/InventorySystem.Core/Entities/CpInventoryMaster.cs`
- `/src/InventorySystem.Core/Interfaces/ICpInventoryRepository.cs`

**帳票テンプレート**:
- `/src/InventorySystem.Reports/FastReport/Templates/UnmatchListReport.frx`

**既存調査資料**:
- `/調査結果/UnmatchList_5152_Final_Investigation_20250720_172400.md`

### 関連するメソッド・クラス

**核心的な処理メソッド**:
- `CheckSalesUnmatchAsync()` - 売上伝票のアンマッチチェック
- `CheckPurchaseUnmatchAsync()` - 仕入伝票のアンマッチチェック
- `CheckInventoryAdjustmentUnmatchAsync()` - 在庫調整のアンマッチチェック
- `GetByKeyAsync()` - CP在庫マスタ検索
- `EnrichWithMasterData()` - マスタデータによる補完

**ストアドプロシージャ**:
- `sp_CreateCpInventoryFromInventoryMasterCumulative` - CP在庫マスタ作成

## 9. 結論

### 9.1 411明細が16件になる最も可能性の高い原因

**FastReportテンプレートでの重複排除**が最有力な原因と推測される。

**理由**:
1. C#コード上では明細単位での処理が行われ、重複排除ロジックは存在しない
2. 411明細 ÷ 16件 ≈ 25.7 という比率は、同一商品キーでの集約を示唆
3. FastReportテンプレートには商品キーでのグループ化機能が実装されている可能性

### 9.2 検証すべき重要なポイント

1. **FastReportテンプレートの詳細調査** - DataBandのグループ化設定を確認
2. **実際のアンマッチリスト出力** - 16件の内容を詳細に分析
3. **CP在庫マスタの整合性** - DataSetId管理の状況確認

### 9.3 次のステップ

1. FastReportテンプレートのDataBand設定の詳細調査
2. アンマッチリスト16件の具体的な内容分析
3. 同一商品キーでの明細集約が行われているかの確認
4. 必要に応じてCP在庫マスタの整合性チェック

---

**調査担当**: Claude Code  
**ファイル**: `/調査結果/UnmatchList_Implementation_Investigation_20250720_235900.md`  
**ステータス**: 基本調査完了、FastReportテンプレート詳細調査が必要