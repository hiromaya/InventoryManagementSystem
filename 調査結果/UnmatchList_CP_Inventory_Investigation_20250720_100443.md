# UnmatchListService CP在庫マスタ作成処理調査報告書

作成日時: 2025-07-20 10:04:43

## 1. エグゼクティブサマリー

### 問題の概要
- 手動でストアドプロシージャを実行すると158件のCP在庫マスタが作成される
- UnmatchListServiceから実行した場合、「該当無」が5152件発生
- CP在庫マスタの作成処理自体は実行されているが、アンマッチリスト生成時にCP在庫マスタが正しく検索できていない

### 調査で判明した根本原因
**DataSetIdの不整合問題**: UnmatchListServiceでは新しいDataSetIdを生成してCP在庫マスタを作成するが、アンマッチリスト生成時に使用する売上・仕入・在庫調整伝票は既存の別のDataSetIdを持っており、CP在庫マスタとの紐付けが正しく行われていない。

### 影響範囲
- アンマッチリスト処理全体が正常に動作しない
- 在庫チェック機能が機能不全状態
- 日次業務に重大な影響

## 2. CP在庫マスタ作成処理の実装

### 2.1 CreateCpInventoryFromInventoryMasterAsyncメソッド

**ファイル**: `/src/InventorySystem.Data/Repositories/CpInventoryRepository.cs` (16-27行目)

```csharp
public async Task<int> CreateCpInventoryFromInventoryMasterAsync(string dataSetId, DateTime? jobDate)
{
    // 累積管理対応版：在庫マスタのレコードをCP在庫マスタにコピー
    // jobDateがnullの場合は全期間対象
    using var connection = new SqlConnection(_connectionString);
    var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
        "sp_CreateCpInventoryFromInventoryMasterCumulative",
        new { DataSetId = dataSetId, JobDate = jobDate },
        commandType: CommandType.StoredProcedure);
    
    return result?.CreatedCount ?? 0;
}
```

**実装内容**:
- ストアドプロシージャ `sp_CreateCpInventoryFromInventoryMasterCumulative` を正しく呼び出している
- DataSetIdとJobDateを適切にパラメータとして渡している
- 戻り値（CreatedCount）を正しく処理している
- エラーハンドリングは実装されていない（ストアドプロシージャ内でTRY-CATCHを実装）

### 2.2 UnmatchListServiceでの呼び出し

**ファイル**: `/src/InventorySystem.Core/Services/UnmatchListService.cs` (90-93行目)

```csharp
// 処理1-1: CP在庫M作成（指定日以前のアクティブな在庫マスタから）
_logger.LogInformation("CP在庫マスタ作成開始（{ProcessType}）", processType);
var createResult = await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(dataSetId, targetDate);
_logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}", createResult);
```

**処理順序確認**:
1. ✅ `OptimizeInventoryMasterAsync(dataSetId)` (75行目)
2. ✅ `CreateCpInventoryFromInventoryMasterAsync(dataSetId, targetDate)` (92行目)
3. ✅ `ClearDailyAreaAsync(dataSetId)` (107行目)
4. ✅ `AggregateDailyDataWithValidationAsync(dataSetId, targetDate)` (121行目)
5. ✅ `GenerateUnmatchListAsync(dataSetId, targetDate)` (139-141行目)

**CP在庫マスタ作成処理は正常に実行されている**

## 3. DataSetIdの取り扱い

### 3.1 DataSetIdの生成

**UnmatchListService.ProcessUnmatchListInternalAsync** (63行目):
```csharp
var dataSetId = Guid.NewGuid().ToString();
```
- 新しいDataSetIdを毎回生成
- このDataSetIdでCP在庫マスタを作成

### 3.2 伝票データの取得方法

**売上伝票取得** (255行目):
```csharp
var salesVouchers = await _salesVoucherRepository.GetAllAsync();
```

**仕入伝票取得** (320行目):
```csharp
var purchaseVouchers = await _purchaseVoucherRepository.GetAllAsync();
```

**在庫調整取得** (426行目):
```csharp
var adjustments = await _inventoryAdjustmentRepository.GetAllAsync();
```

**重大な問題**: 伝票データの取得時にDataSetIdによるフィルタリングが行われていない

### 3.3 CP在庫マスタ検索での問題

**GetByKeyAsyncメソッド** (CpInventoryRepository.cs 66-92行目):
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
            AND DataSetId = @DataSetId  -- ←ここでDataSetIdによる絞り込み
        """;
    // ...
}
```

**アンマッチ発生のメカニズム**:
1. UnmatchListServiceが新しいDataSetId（例: "abc123"）を生成
2. このDataSetIdでCP在庫マスタを作成（158件作成成功）
3. 売上伝票は別のDataSetId（例: "xyz789"）を持っている
4. アンマッチチェック時、売上伝票のキーで検索するが、DataSetId="abc123"のCP在庫マスタしか存在しない
5. 結果として「該当無」となり、5152件のアンマッチが発生

## 4. 証拠となるコード

### 4.1 ストアドプロシージャの動作確認

**sp_CreateCpInventoryFromInventoryMasterCumulative.sql** (108-133行目):
```sql
AND EXISTS (
    -- 伝票に存在する5項目キーのみ（指定日以前の期間対象）
    SELECT 1 FROM SalesVouchers sv 
    WHERE (@JobDate IS NULL OR sv.JobDate <= @JobDate) 
    AND sv.ProductCode = im.ProductCode
    AND sv.GradeCode = im.GradeCode
    AND sv.ClassCode = im.ClassCode
    AND sv.ShippingMarkCode = im.ShippingMarkCode
    AND sv.ShippingMarkName = im.ShippingMarkName
    -- ...
);
```

- ストアドプロシージャは伝票に存在する商品キーでCP在庫マスタを作成している
- JobDateでの絞り込みは正しく実装されている
- **DataSetIdによる伝票の絞り込みは行われていない**

### 4.2 ログ出力の不備

UnmatchListServiceでは以下のログが出力されるが、DataSetIdの詳細は記録されていない:
```csharp
_logger.LogInformation("CP在庫マスタ作成完了 - 作成件数: {Count}", createResult);
```

**改善すべき点**: 使用したDataSetIdもログに記録すべき

## 5. 問題の根本原因

### 5.1 設計上の問題

1. **DataSetIdの管理方針が不明確**
   - 新しいDataSetIdを生成すべきか、既存の売上伝票のDataSetIdを使用すべきか不明
   - CP在庫マスタと伝票データの関連付けが考慮されていない

2. **伝票データの取得方法が不適切**
   - `GetAllAsync()`ですべての伝票を取得している
   - 特定のDataSetIdまたは期間での絞り込みが行われていない

3. **アンマッチチェックロジックの不整合**
   - CP在庫マスタは新しいDataSetIdで作成
   - 検索時は同じDataSetIdで検索するが、伝票は別のDataSetIdを持つ

### 5.2 具体的な不整合の例

```
売上伝票: DataSetId="DS_20250620_001", ProductCode="12345", GradeCode="001", ...
CP在庫マスタ: DataSetId="新GUID", ProductCode="12345", GradeCode="001", ...

GetByKeyAsync(key, "新GUID") → null（DataSetIdが一致しないため）
→ 結果: 「該当無」のアンマッチが発生
```

## 6. 推奨される修正方針

### 6.1 短期的な修正（即効性重視）

1. **伝票データのDataSetIdを取得して使用**
   ```csharp
   // 売上伝票から最新のDataSetIdを取得
   var salesDataSetId = await _salesVoucherRepository.GetLatestDataSetIdAsync();
   var dataSetId = salesDataSetId ?? Guid.NewGuid().ToString();
   ```

2. **CP在庫マスタ検索時のDataSetId統一**
   - アンマッチチェック時に伝票のDataSetIdを使用してCP在庫マスタを検索

### 6.2 中期的な修正（根本解決）

1. **DataSetId管理の統一化**
   - すべての処理で同一のDataSetIdを使用する仕組みの構築
   - DataSetIdの生成と管理を一元化

2. **伝票取得メソッドの改修**
   ```csharp
   // 期間とDataSetIdで絞り込み
   var salesVouchers = await _salesVoucherRepository.GetByDataSetIdAsync(dataSetId);
   ```

3. **CP在庫マスタ作成ロジックの見直し**
   - 既存のDataSetIdを再利用する仕組み
   - DataSetId重複時の処理方針の明確化

### 6.3 長期的な改善（設計改善）

1. **DataSetIdの概念整理**
   - 処理単位としてのDataSetIdの定義明確化
   - 関連データの整合性保証メカニズム

2. **トランザクション境界の見直し**
   - CP在庫マスタ作成から削除までの一連の処理を単一トランザクションで管理

3. **テストケースの充実**
   - DataSetId関連の各種パターンのテストケース作成
   - 統合テストでの動作確認

## 7. 緊急対応の必要性

この問題は在庫管理システムの核心機能に関わる重大な欠陥です。以下の理由により緊急対応が必要です：

1. **業務影響**: アンマッチリストが正常に機能せず、在庫管理業務に支障
2. **データ整合性**: 不正確なアンマッチデータによる業務判断の誤り
3. **システム信頼性**: 基本機能の不具合による全体的な信頼性の低下

**推奨**: DataSetIdの統一化を最優先で実装し、その後根本的な設計見直しを実施