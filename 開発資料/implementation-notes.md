# 実装時の重要な注意事項

## 1. 処理順序の厳守

```
1. CSV取込（売上・仕入・在庫調整）
2. 在庫マスタ最適化 ← 必須！
3. CP在庫マスタ生成
4. アンマッチリスト生成
```

**注意**: 在庫マスタ最適化を省略すると、CP在庫マスタが空になり、すべてが「該当無」エラーになります。

## 2. パフォーマンス対策

### バッチ処理の実装
```csharp
// 大量データの場合は分割処理
const int batchSize = 1000;
var productBatches = salesProducts.Chunk(batchSize);

foreach (var batch in productBatches)
{
    await ProcessBatchAsync(batch);
}
```

### インデックスの活用
```sql
-- 必須インデックス（既に存在するはず）
CREATE INDEX IX_SalesVouchers_JobDate ON SalesVouchers(JobDate);
CREATE INDEX IX_InventoryMaster_JobDate ON InventoryMaster(JobDate);
```

## 3. エラーハンドリング

### 主キー重複への対処
```csharp
try
{
    await InsertInventoryMasterAsync(product);
}
catch (SqlException ex) when (ex.Number == 2627) // Primary key violation
{
    // 既に存在する場合は更新
    await UpdateInventoryMasterAsync(product);
}
```

## 4. トランザクション管理

```csharp
using var transaction = await connection.BeginTransactionAsync();
try
{
    // すべての処理をトランザクション内で実行
    await OptimizeInventoryMasterAsync(connection, transaction);
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

## 5. ログ出力の重要性

```csharp
_logger.LogInformation("処理開始: JobDate={JobDate}, DataSetId={DataSetId}", jobDate, dataSetId);
_logger.LogInformation("売上商品数: {Count}", salesProducts.Count);
_logger.LogInformation("在庫マスタ更新: {Updated}件, 新規: {Inserted}件", updated, inserted);
_logger.LogWarning("在庫マスタ件数({Inventory})が売上商品数({Sales})と一致しません", 
    inventoryCount, salesCount);
```

## 6. 5項目キーの注意点

### 文字列の完全一致
```csharp
// ShippingMarkNameの空白に注意
AND target.ShippingMarkName = source.ShippingMarkName -- 末尾空白も含めて完全一致

// トリミングしてはいけない！
// NG: AND TRIM(target.ShippingMarkName) = TRIM(source.ShippingMarkName)
```

### NULL値の扱い
```csharp
// すべてのキー項目はNOT NULL
// 空文字列として扱う（NULLにしない）
ShippingMarkName = string.IsNullOrEmpty(value) ? "" : value;
```

## 7. JobDate更新の影響

### 在庫数量は変更しない
```sql
UPDATE InventoryMaster
SET 
    JobDate = @jobDate,
    UpdatedDate = GETDATE()
    -- CurrentStock, DailyStock等は変更しない！
WHERE ...
```

## 8. デバッグ用のチェックポイント

```csharp
// 処理前後で必ず確認
var beforeCount = await GetInventoryCountAsync(jobDate);
await OptimizeInventoryMasterAsync(jobDate);
var afterCount = await GetInventoryCountAsync(jobDate);

if (afterCount < salesProductCount)
{
    _logger.LogWarning("在庫マスタが不足しています: {After}/{Sales}", 
        afterCount, salesProductCount);
}
```

## 9. 既知の問題への対処

### 90件問題（売上84件に対して在庫90件）
```sql
-- 原因：複数日のデータが混在
-- 対処：該当日の売上がない商品は除外する処理を検討
```

### 等級コード「000」問題
```
- 72件の売上で使用されているが、マスタ未登録
- 暫定対処：在庫マスタには登録するが、商品名は「商品名未設定」
- 将来対処：クライアントに確認後、正式なマスタ登録
```

## 10. テスト方法

### 単体テスト
```csharp
[Fact]
public async Task OptimizeInventoryMaster_Should_Create_Missing_Products()
{
    // Arrange
    var jobDate = new DateTime(2025, 6, 12);
    
    // Act
    var result = await _service.OptimizeAsync(jobDate);
    
    // Assert
    Assert.Equal(84, result.SalesProductCount);
    Assert.Equal(84, result.ProcessedCount);
}
```

### 統合テスト
```bash
# 実際のデータでテスト
dotnet run import-folder "D:\InventoryImport\DeptA\Import" --date 2025-06-12

# 結果確認
dotnet run unmatch-list 2025-06-12
# 「該当無」エラーが0件になることを確認
```