# GetByDataSetId実行時調査報告書

作成日時: 2025-07-20 17:10:00

## 1. エグゼクティブサマリー

### 調査結果の要約
GetByDataSetIdAsyncメソッドの実装を詳細に調査した結果、**実装自体は正常**であることが判明しました。しかし、**実行時の条件分岐とデータの不整合**により、期待される動作が実現されていない可能性が高いです。

### 主な発見事項
1. **GetByDataSetIdAsyncの実装は完全**：SQLクエリ、パラメータ、マッピングすべて適切
2. **GetDataSetIdByJobDateAsyncも正常**：適切なSQL実行とnull処理
3. **条件分岐ロジックは理論上正常**：targetDateとdataSetIdの処理が適切
4. **問題は実行時の状況**：複数のDataSetIdが存在する環境での動作異常

## 2. GetByDataSetIdAsyncの実装詳細

### 2.1 完全なメソッド実装

**ファイル**: `src/InventorySystem.Data/Repositories/SalesVoucherRepository.cs` (220-259行目)

```csharp
public async Task<IEnumerable<SalesVoucher>> GetByDataSetIdAsync(string dataSetId)
{
    const string sql = @"
        SELECT 
            VoucherId,
            LineNumber,
            VoucherNumber,
            VoucherDate,
            VoucherType,
            CustomerCode,
            CustomerName,
            ProductCode,
            GradeCode,
            ClassCode,
            ShippingMarkCode,
            ShippingMarkName,
            Quantity,
            UnitPrice as SalesUnitPrice,
            Amount as SalesAmount,
            InventoryUnitPrice,
            JobDate,
            DetailType,
            DataSetId
        FROM SalesVouchers
        WHERE DataSetId = @dataSetId
        ORDER BY VoucherNumber, LineNumber";

    try
    {
        using var connection = CreateConnection();
        var vouchers = await connection.QueryAsync<dynamic>(sql, new { dataSetId });
        
        return vouchers.Select(MapToSalesVoucher);
    }
    catch (Exception ex)
    {
        LogError(ex, nameof(GetByDataSetIdAsync), new { dataSetId });
        throw;
    }
}
```

### 2.2 実装分析

#### ✅ SQLクエリの正確性
- **適切なWHERE句**: `DataSetId = @dataSetId` による厳密なフィルタリング
- **完全なカラム選択**: 必要なすべてのカラムを選択
- **適切なORDER BY**: `VoucherNumber, LineNumber` による順序付け

#### ✅ パラメータの渡し方
- **匿名オブジェクト**: `new { dataSetId }` による適切なパラメータ設定
- **SQL Injection対策**: パラメータ化クエリによる安全な実装

#### ✅ 戻り値の型とマッピング処理
- **dynamic型の使用**: フレキシブルなデータ取得
- **MapToSalesVoucherメソッド**: 適切なエンティティマッピング

#### ✅ エラーハンドリング
- **try-catch構造**: 例外の適切な捕捉
- **LogError**: 詳細なログ出力とdataSetIdの記録
- **例外の再スロー**: 呼び出し元での適切な処理

### 2.3 MapToSalesVoucherメソッド

**ファイル**: `src/InventorySystem.Data/Repositories/SalesVoucherRepository.cs` (117-141行目)

```csharp
private static SalesVoucher MapToSalesVoucher(dynamic row)
{
    return new SalesVoucher
    {
        VoucherId = row.VoucherId?.ToString() ?? string.Empty,
        LineNumber = row.LineNumber ?? 0,
        VoucherNumber = row.VoucherNumber?.ToString() ?? string.Empty,
        VoucherDate = row.VoucherDate,
        JobDate = row.JobDate,
        VoucherType = row.VoucherType?.ToString() ?? string.Empty,
        DetailType = row.DetailType?.ToString() ?? string.Empty,
        CustomerCode = row.CustomerCode?.ToString(),
        CustomerName = row.CustomerName?.ToString(),
        ProductCode = row.ProductCode?.ToString() ?? string.Empty,
        GradeCode = row.GradeCode?.ToString() ?? string.Empty,
        ClassCode = row.ClassCode?.ToString() ?? string.Empty,
        ShippingMarkCode = row.ShippingMarkCode?.ToString() ?? string.Empty,
        ShippingMarkName = row.ShippingMarkName?.ToString() ?? string.Empty,
        Quantity = row.Quantity ?? 0m,
        UnitPrice = row.SalesUnitPrice ?? 0m,
        Amount = row.SalesAmount ?? 0m,
        InventoryUnitPrice = row.InventoryUnitPrice ?? 0m,
        DataSetId = row.DataSetId?.ToString() ?? string.Empty  // ←重要：DataSetIdも正しくマッピング
    };
}
```

**分析結果**:
✅ **完全なマッピング**: すべてのプロパティが適切にマッピングされている
✅ **null安全**: `?.ToString() ?? string.Empty` によるnull安全な処理
✅ **DataSetIdマッピング**: DataSetIdが確実にエンティティに設定される

## 3. GetDataSetIdByJobDateAsyncの実装詳細

### 3.1 完全なメソッド実装

**ファイル**: `src/InventorySystem.Data/Repositories/SalesVoucherRepository.cs` (261-279行目)

```csharp
public async Task<string?> GetDataSetIdByJobDateAsync(DateTime jobDate)
{
    const string sql = @"
        SELECT TOP 1 DataSetId 
        FROM SalesVouchers 
        WHERE JobDate = @jobDate 
        AND DataSetId IS NOT NULL";

    try
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { jobDate });
    }
    catch (Exception ex)
    {
        LogError(ex, nameof(GetDataSetIdByJobDateAsync), new { jobDate });
        throw;
    }
}
```

### 3.2 null返却の条件分析

#### null返却シナリオ
1. **該当データなし**: 指定JobDateのレコードが存在しない
2. **DataSetIdがnull**: レコードは存在するがDataSetIdがnull
3. **SQL実行エラー**: データベース接続やクエリ実行時のエラー

#### ✅ 適切な実装
- **TOP 1の使用**: 複数のDataSetIdがある場合の最初の1件取得
- **IS NOT NULL条件**: DataSetIdがnullのレコードを除外
- **QueryFirstOrDefaultAsync**: 該当なしの場合はnull返却

## 4. 実行パスの分析

### 4.1 Program.csからの呼び出し

**ファイル**: `src/InventorySystem.Console/Program.cs` (545-550行目)

```csharp
DateTime? targetDate = null;
if (args.Length >= 2 && DateTime.TryParse(args[1], out var parsedDate))
{
    targetDate = parsedDate;
    logger.LogInformation("指定された対象日: {TargetDate:yyyy-MM-dd}", targetDate);
}
```

**分析結果**:
✅ **適切な引数解析**: `DateTime.TryParse` による安全な日付変換
✅ **ログ出力**: targetDateの設定状況が記録される

### 4.2 UnmatchListServiceでの条件分岐

**ファイル**: `src/InventorySystem.Core/Services/UnmatchListService.cs` (74-97行目)

```csharp
// 既存の伝票データからDataSetIdを取得（優先順位: 売上→仕入→在庫調整）
string? existingDataSetId = null;
if (targetDate.HasValue)
{
    existingDataSetId = await _salesVoucherRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
    if (string.IsNullOrEmpty(existingDataSetId))
    {
        existingDataSetId = await _purchaseVoucherRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
    }
    if (string.IsNullOrEmpty(existingDataSetId))
    {
        existingDataSetId = await _inventoryAdjustmentRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
    }
}

// 既存DataSetIdが見つかった場合は置き換える
if (!string.IsNullOrEmpty(existingDataSetId))
{
    dataSetId = existingDataSetId;
    _logger.LogInformation("既存のDataSetIdを使用します: {DataSetId}", dataSetId);
}
else
{
    _logger.LogWarning("指定日の既存DataSetIdが見つからないため新規生成したDataSetIdを使用: {DataSetId}", dataSetId);
}
```

### 4.3 伝票取得の条件分岐

**ファイル**: `src/InventorySystem.Core/Services/UnmatchListService.cs` (285-297行目)

```csharp
if (!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue)
{
    // 指定日処理：DataSetIdでフィルタリング
    salesVouchers = await _salesVoucherRepository.GetByDataSetIdAsync(dataSetId);
    _logger.LogInformation("売上伝票取得（DataSetIdフィルタ）: DataSetId={DataSetId}, 件数={Count}", 
        dataSetId, salesVouchers.Count());
}
else
{
    // 全期間処理：従来通り全件取得
    salesVouchers = await _salesVoucherRepository.GetAllAsync();
    _logger.LogDebug("売上伝票取得（全件）: 総件数={TotalCount}", salesVouchers.Count());
}
```

## 5. 問題の特定

### 5.1 実行パターン分析

#### パターン1: `dotnet run unmatch-list 2025-06-02`
```
1. targetDate = 2025-06-02 ✅
2. GetDataSetIdByJobDateAsync(2025-06-02) 実行
   → "cd9cf402-413e-41b1-9e5f-73eace6bf4d1" 取得 ✅
3. dataSetId = "cd9cf402-413e-41b1-9e5f-73eace6bf4d1" ✅
4. 条件: !string.IsNullOrEmpty("cd9cf402-...") && true → true ✅
5. GetByDataSetIdAsync("cd9cf402-...") 実行 ✅
6. ログ: "売上伝票取得（DataSetIdフィルタ）: DataSetId=cd9cf402-..., 件数=469" ✅
```

#### パターン2: `dotnet run unmatch-list`
```
1. targetDate = null ✅
2. DataSetId取得処理スキップ（targetDate.HasValue = false）
3. dataSetId = 新規GUID ✅
4. 条件: !string.IsNullOrEmpty("新規GUID") && false → false ✅
5. GetAllAsync() 実行 ✅
6. ログ: "売上伝票取得（全件）: 総件数=全件数" ✅
```

### 5.2 理論的には正常動作のはず

**上記の分析により、コード実装は理論的に正常に動作するはず**です。しかし、5152件の「該当無」が発生している事実から、以下のいずれかの問題が発生している可能性があります：

## 6. 疑われる問題箇所

### 6.1 クエリ結果から推測される問題

**クエリ結果（5.json）の分析**:
- DataSetId `cd9cf402-413e-41b1-9e5f-73eace6bf4d1`: 158件
- 他の4つのDataSetId: 各212件

**推測される問題**:
1. **GetByDataSetIdAsyncは正常動作**している可能性が高い
2. **問題はCP在庫マスタ側**：複数のDataSetIdでCP在庫マスタが作成されている
3. **アンマッチの原因**：伝票は正しいDataSetIdで取得されるが、CP在庫マスタに複数バージョンが存在

### 6.2 具体的な問題シナリオ

```
1. 売上伝票取得: GetByDataSetIdAsync("cd9cf402-...") → 469件取得 ✅
2. CP在庫マスタ検索: GetByKeyAsync(key, "cd9cf402-...") 
   → しかし、他のDataSetId（212件×4）のCP在庫マスタも存在
   → 古いDataSetIdのCP在庫マスタとの重複・競合？
3. 結果: 期待するCP在庫マスタが見つからず「該当無」発生
```

### 6.3 CP在庫マスタ作成処理の問題

**疑われる問題**:
1. **以前の実行でのCP在庫マスタが残存**
2. **異なるDataSetIdでの重複作成**
3. **削除処理の不完全実行**

### 6.4 実際のログ確認が必要な項目

実行時に以下のログが出力されているかどうかの確認が必要：

```
1. "既存のDataSetIdを使用します: cd9cf402-413e-41b1-9e5f-73eace6bf4d1"
2. "売上伝票取得（DataSetIdフィルタ）: DataSetId=cd9cf402-..., 件数=469"
3. "CP在庫マスタ作成完了 - 作成件数: 158, DataSetId: cd9cf402-..."
```

## 7. 証拠コード

### 7.1 GetByDataSetIdAsyncは正常実装

```csharp
// 適切なSQLクエリ
WHERE DataSetId = @dataSetId

// 適切なパラメータ設定
new { dataSetId }

// 適切なマッピング
DataSetId = row.DataSetId?.ToString() ?? string.Empty
```

### 7.2 条件分岐は理論上正常

```csharp
// 適切な条件設定
if (!string.IsNullOrEmpty(dataSetId) && targetDate.HasValue)

// 適切なDataSetId使用
salesVouchers = await _salesVoucherRepository.GetByDataSetIdAsync(dataSetId);
```

### 7.3 問題は環境・データ状態

```sql
-- CP在庫マスタに複数のDataSetIdが存在（クエリ結果5.jsonより）
SELECT DataSetId, COUNT(*) FROM CpInventoryMaster GROUP BY DataSetId
-- 結果：
-- cd9cf402-413e-41b1-9e5f-73eace6bf4d1: 158件
-- その他4つのDataSetId: 各212件
```

## 8. 修正提案

### 8.1 即効性のある修正

#### A. CP在庫マスタのクリーンアップ
```csharp
// 古いCP在庫マスタを削除してから新規作成
await _cpInventoryRepository.DeleteAllAsync();
await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(dataSetId, targetDate);
```

#### B. CP在庫マスタ作成前の確認
```csharp
// 既存のCP在庫マスタ件数を確認
var existingCount = await _cpInventoryRepository.GetCountByDataSetIdAsync(dataSetId);
if (existingCount > 0)
{
    _logger.LogWarning("既存のCP在庫マスタが{Count}件存在します。DataSetId: {DataSetId}", existingCount, dataSetId);
}
```

### 8.2 根本的な修正

#### A. DataSetId管理の強化
```csharp
// CP在庫マスタ作成時にDataSetIdの一意性を保証
await _cpInventoryRepository.DeleteByDataSetIdAsync(dataSetId);
await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(dataSetId, targetDate);
```

#### B. ログ出力の詳細化
```csharp
_logger.LogInformation("CP在庫マスタ検索: Key={Key}, DataSetId={DataSetId}, 結果={Result}", 
    inventoryKey, dataSetId, cpInventory != null ? "見つかった" : "該当無");
```

## 9. 結論

### 9.1 実装自体は正常

- **GetByDataSetIdAsync**: 完全かつ正確な実装
- **GetDataSetIdByJobDateAsync**: 適切なnull処理とエラーハンドリング
- **条件分岐ロジック**: 理論上正常に動作するはず

### 9.2 問題は環境・データ状態

- **複数のCP在庫マスタDataSetId**が根本原因の可能性が高い
- **データの不整合**により期待される動作が実現されていない
- **ログ出力の詳細確認**が問題特定に必要

### 9.3 優先対応事項

1. **実際のログ確認**: 次回実行時のDataSetId使用状況
2. **CP在庫マスタのクリーンアップ**: 古いDataSetIdの残存データ削除
3. **DataSetId管理の改善**: 一意性保証とライフサイクル管理

**GetByDataSetIdAsync自体は正常であり、問題は複数DataSetIdの管理不備にある**と結論されます。