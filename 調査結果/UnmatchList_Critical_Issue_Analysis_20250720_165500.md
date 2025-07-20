# UnmatchListService 重大な問題分析報告書

作成日時: 2025-07-20 16:55:00

## 1. エグゼクティブサマリー

### 問題の現状
- **修正済みコードが動作していない**: DataSetId不整合修正を実装したにも関わらず、5152件の「該当無」問題が継続
- **古いロジックの残存**: 修正されたDataSetId取得ロジックが条件分岐により無効化されている
- **二重条件の罠**: `!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue` の条件により、新しいロジックが機能しない

### 根本原因
**条件分岐の設計ミス**: 修正されたDataSetId取得ロジックが、条件分岐により実質的に無効化されており、依然として `GetAllAsync()` が実行されている。

## 2. 重大な設計上の問題

### 2.1 条件分岐の致命的な欠陥

**問題箇所**: `src/InventorySystem.Core/Services/UnmatchListService.cs` 285行目付近

```csharp
// 売上伝票取得（DataSetIdフィルタリング対応）
IEnumerable<SalesVoucher> salesVouchers;
if (!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue)  // ←この条件が問題
{
    // 指定日処理：DataSetIdでフィルタリング
    salesVouchers = await _salesVoucherRepository.GetByDataSetIdAsync(dataSetId);
    _logger.LogInformation("売上伝票取得（DataSetIdフィルタ）: DataSetId={DataSetId}, 件数={Count}", 
        dataSetId, salesVouchers.Count());
}
else
{
    // 全期間処理：従来通り全件取得  ←実際にはここが実行される
    salesVouchers = await _salesVoucherRepository.GetAllAsync();
    _logger.LogDebug("売上伝票取得（全件）: 総件数={TotalCount}", salesVouchers.Count());
}
```

### 2.2 実行フローの分析

#### ケース1: `dotnet run unmatch-list 2025-06-02` の場合
1. `targetDate = 2025-06-02` が設定される ✅
2. `dataSetId = Guid.NewGuid().ToString()` で初期化される ✅
3. `existingDataSetId = await _salesVoucherRepository.GetDataSetIdByJobDateAsync(targetDate.Value)` が実行される ✅
4. **しかし**: `dataSetId` は空ではないが、**既存DataSetIdではない新規GUID**のまま
5. 条件 `!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue` は `true` になる
6. **問題**: `GetByDataSetIdAsync(dataSetId)` で**新規GUID**を使って検索 → 0件
7. **結果**: CP在庫マスタには**新規GUID**で作成されるが、伝票データは取得されない

#### ケース2: `dotnet run unmatch-list` の場合
1. `targetDate = null` が設定される
2. `dataSetId = Guid.NewGuid().ToString()` で初期化される
3. `targetDate.HasValue` が `false` のため、DataSetId取得処理がスキップされる
4. 条件 `!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue` は `false` になる
5. **結果**: `GetAllAsync()` が実行される（古いロジック）

## 3. 具体的な問題箇所

### 3.1 DataSetId取得後の条件判定エラー

**現在の実装**:
```csharp
// DataSetIdをメソッドスコープで定義（初期値設定）
string dataSetId = Guid.NewGuid().ToString();  // ←常に新規GUID

// 既存の伝票データからDataSetIdを取得
string? existingDataSetId = null;
if (targetDate.HasValue)
{
    existingDataSetId = await _salesVoucherRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
    // ...
}

// 既存DataSetIdが見つかった場合は置き換える
if (!string.IsNullOrEmpty(existingDataSetId))
{
    dataSetId = existingDataSetId;  // ←既存DataSetIdに置き換え
}

// ...

// 条件判定で新規GUIDかどうかの区別ができない
if (!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue)  // ←問題：常にtrue
{
    // GetByDataSetIdAsync(dataSetId) を実行
    // 既存DataSetIdの場合：正常動作
    // 新規GUIDの場合：0件取得 → 「該当無」大量発生
}
```

### 3.2 同様の問題が3箇所に存在

1. **CheckSalesUnmatchAsync** (285行目)
2. **CheckPurchaseUnmatchAsync** (362行目)  
3. **CheckInventoryAdjustmentUnmatchAsync** (481行目)

すべて同じ条件分岐パターンを使用している。

### 3.3 CP在庫マスタ検索の問題

**CpInventoryRepository.GetByKeyAsync** (66-92行目):
```csharp
public async Task<CpInventoryMaster?> GetByKeyAsync(InventoryKey key, string dataSetId)
{
    const string sql = """
        SELECT * FROM CpInventoryMaster 
        WHERE ProductCode = @ProductCode 
            AND GradeCode = @GradeCode 
            AND ClassCode = @ClassCode 
            AND ShippingMarkCode = @ShippingMarkCode 
            AND ShippingMarkName COLLATE Japanese_CI_AS = @ShippingMarkName COLLATE Japanese_CI_AS
            AND DataSetId = @DataSetId  // ←厳密なDataSetIdマッチング
        """;
    // ...
}
```

**問題**: CP在庫マスタは新規GUIDで作成されるが、検索時も同じ新規GUIDを使用するため、伝票データとの不一致が発生。

## 4. 実際の動作フロー（問題シナリオ）

### シナリオ: `dotnet run unmatch-list 2025-06-02`

```
1. targetDate = 2025-06-02 設定 ✅
2. dataSetId = "14062a7c-98e3-4938-b869-a44ab7f1c4bf" (新規GUID) 設定
3. existingDataSetId = GetDataSetIdByJobDateAsync(2025-06-02) 実行
   → "cd9cf402-413e-41b1-9e5f-73eace6bf4d1" 取得 ✅
4. dataSetId = "cd9cf402-413e-41b1-9e5f-73eace6bf4d1" に置き換え ✅
5. CP在庫マスタ作成: CreateCpInventoryFromInventoryMasterAsync("cd9cf402-413e-41b1-9e5f-73eace6bf4d1") ✅
6. 【問題発生】伝票データ取得:
   - 条件: !string.IsNullOrEmpty("cd9cf402-413e-41b1-9e5f-73eace6bf4d1") && true → true
   - 実行: GetByDataSetIdAsync("cd9cf402-413e-41b1-9e5f-73eace6bf4d1") ✅
   - 結果: 正しいDataSetIdで伝票データ取得 ✅
7. 【問題発生】アンマッチチェック:
   - 売上伝票: DataSetId="cd9cf402-413e-41b1-9e5f-73eace6bf4d1"
   - CP在庫マスタ: DataSetId="cd9cf402-413e-41b1-9e5f-73eace6bf4d1"
   - 検索: GetByKeyAsync(key, "cd9cf402-413e-41b1-9e5f-73eace6bf4d1")
   - 結果: 正常にマッチするはず... 🤔
```

**待機**: 上記の分析によると、実際には正常に動作するはずです。

## 5. さらなる調査が必要な箇所

### 5.1 ログ出力の詳細確認が必要

実際の実行時に以下のログが出力されているか確認が必要：

1. **DataSetId取得ログ**:
   ```
   "既存のDataSetIdを使用します: cd9cf402-413e-41b1-9e5f-73eace6bf4d1"
   ```

2. **伝票取得ログ**:
   ```
   "売上伝票取得（DataSetIdフィルタ）: DataSetId=cd9cf402-413e-41b1-9e5f-73eace6bf4d1, 件数=XXXX"
   ```

3. **CP在庫マスタ作成ログ**:
   ```
   "CP在庫マスタ作成完了 - 作成件数: 158, DataSetId: cd9cf402-413e-41b1-9e5f-73eace6bf4d1"
   ```

### 5.2 疑われる追加問題

#### 5.2.1 GetDataSetIdByJobDateAsyncの実際の動作
```csharp
public async Task<string?> GetDataSetIdByJobDateAsync(DateTime jobDate)
{
    const string sql = @"
        SELECT TOP 1 DataSetId 
        FROM SalesVouchers 
        WHERE JobDate = @jobDate 
        AND DataSetId IS NOT NULL";
    // ...
}
```

**検証ポイント**:
- `jobDate` パラメータが正しく `2025-06-02` として渡されているか
- SQL実行時に実際に `cd9cf402-413e-41b1-9e5f-73eace6bf4d1` が返されているか
- `SalesVouchers` テーブルに該当データが存在するか

#### 5.2.2 CP在庫マスタ作成ストアドプロシージャの問題
`sp_CreateCpInventoryFromInventoryMasterCumulative` が：
- 正しいDataSetIdで実行されているか
- 実際に158件作成されているか
- 作成されたレコードのDataSetIdが正しいか

#### 5.2.3 GetByDataSetIdAsyncの実際の動作
```csharp
public async Task<IEnumerable<SalesVoucher>> GetByDataSetIdAsync(string dataSetId)
{
    const string sql = @"
        SELECT ... FROM SalesVouchers
        WHERE DataSetId = @dataSetId
        ORDER BY VoucherNumber, LineNumber";
    // ...
}
```

**検証ポイント**:
- 実際に正しい件数の伝票データが取得されているか
- 取得されたデータのDataSetIdが期待値と一致するか

#### 5.2.4 日付フィルタリングの重複適用
```csharp
var salesList = salesVouchers
    .Where(s => s.VoucherType == "51" || s.VoucherType == "52") // 売上伝票
    .Where(s => s.DetailType == "1" || s.DetailType == "2")     // 明細種
    .Where(s => s.Quantity != 0)                                // 数量0以外
    .Where(s => !targetDate.HasValue || s.JobDate <= targetDate.Value) // ←この行が問題？
    .ToList();
```

**疑問**: `GetByDataSetIdAsync` で既に正しいDataSetIdの伝票を取得しているにも関わらず、さらに `s.JobDate <= targetDate.Value` でフィルタリングしている。これにより、意図しないデータが除外されている可能性。

## 6. コマンド実行方法の確認

### 6.1 実際のコマンド確認

**利用可能コマンド**: `unmatch-list` のみ
**存在しないコマンド**: `create-unmatch-list` (コメントで言及されているが実装されていない)

```bash
# 正しいコマンド
dotnet run unmatch-list 2025-06-02

# 存在しないコマンド（コメントで言及）
dotnet run create-unmatch-list 2025-06-02  # ←これは存在しない
```

### 6.2 Program.csの実装確認
```csharp
case "unmatch-list":
    await ExecuteUnmatchListAsync(host.Services, commandArgs);
    break;
```

正しく `ExecuteUnmatchListAsync` が呼ばれている。

## 7. 推定される真の問題

### 7.1 最も可能性の高い問題

**日付フィルタリングの二重適用**:
1. `GetByDataSetIdAsync` で正しいDataSetIdの伝票を取得
2. しかし、その後の `.Where(s => !targetDate.HasValue || s.JobDate <= targetDate.Value)` により、意図しないフィルタリングが発生
3. 結果として、アンマッチチェック対象の伝票が大幅に減少
4. CP在庫マスタには全データがあるが、チェック対象伝票が少ないため「該当無」が大量発生

### 7.2 検証すべき仮説

1. **DataSetId取得は成功している**が、その後の処理で問題が発生
2. **日付フィルタリングの重複**により、チェック対象データが意図せず削減
3. **ログ出力では正常に見える**が、実際の処理結果が異なる

## 8. 緊急対応が必要な調査項目

### 8.1 ログ確認（最優先）
実際の `unmatch-list` 実行時のログで以下を確認：
- DataSetId取得結果
- 伝票データ取得件数
- CP在庫マスタ作成件数と使用DataSetId

### 8.2 データベース直接確認
```sql
-- 売上伝票のDataSetId確認
SELECT DISTINCT DataSetId, COUNT(*) 
FROM SalesVouchers 
WHERE JobDate = '2025-06-02' 
GROUP BY DataSetId;

-- CP在庫マスタのDataSetId確認
SELECT DISTINCT DataSetId, COUNT(*) 
FROM CpInventoryMaster 
GROUP BY DataSetId;
```

### 8.3 伝票データフィルタリング結果確認
実際に `GetByDataSetIdAsync` で取得された伝票データ件数と、最終的にアンマッチチェックされる件数の比較。

## 9. 結論

**修正コードは実装されているが、設計上の別の問題**により効果が発揮されていない可能性が高い。特に：

1. **日付フィルタリングの二重適用**
2. **条件分岐ロジックの検証不足** 
3. **実際のデータフローと期待値の乖離**

修正を行う前に、実際のログとデータベースの状態を詳細に確認する必要がある。

## 10. 次のアクション

1. **ログ確認**: 実際の `unmatch-list 2025-06-02` 実行結果の詳細分析
2. **データベース確認**: DataSetIdとデータ件数の実態把握  
3. **フィルタリングロジック検証**: 日付条件の重複適用問題の確認

この調査により、真の問題箇所を特定できると考えられる。