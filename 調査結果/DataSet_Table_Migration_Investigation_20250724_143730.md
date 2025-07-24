# DataSetsテーブルとDataSetManagementテーブルの混在使用問題の調査結果

## エグゼクティブサマリー

### 問題の概要
移行完了と思われていた在庫管理システムにおいて、古いDataSetsテーブルと新しいDataSetManagementテーブルの混在使用により、以下の重大な問題が発生している：

```
✅ 仕入伝票として処理完了 - データセットID: 4abb8dcb-c4f1-485b-b8ac-91ba8c66bd78
❌ エラー: 仕入伝票.csv - データセットが見つかりません: 4abb8dcb-c4f1-485b-b8ac-91ba8c66bd78
```

### 根本原因
- **データ保存**：新システム（DataSetManagementテーブル）で実行
- **データ取得**：旧システム（DataSetsテーブル）から実行
- **結果**：完全な不整合により、保存されたデータを取得不可

### 影響範囲
- 全ImportService（売上・仕入・在庫調整）で同様の問題発生
- `GetImportResultAsync`メソッドがすべて機能不全状態
- インポート処理の結果確認・エラー調査が不可能

## テーブル使用状況マトリックス

| サービス | データ保存先 | データ取得先 | GetImportResultAsync | 問題レベル |
|---------|-------------|--------------|---------------------|------------|
| PurchaseVoucherImportService | DataSetManagement | DataSets (旧) | ❌ 機能不全 | 🔴 重大 |
| SalesVoucherImportService | DataSetManagement | DataSets (旧) | ❌ 機能不全 | 🔴 重大 |
| InventoryAdjustmentImportService | DataSetManagement | DataSets (旧) | ❌ 機能不全 | 🔴 重大 |

## 詳細な問題分析

### 1. テーブル構造と実データの確認

#### 事前確認SQLの結果（クエリ２フォルダ）
```sql
-- テーブル構造
DataSetManagement: 25列（新テーブル）
DataSets: 10列（旧テーブル）

-- データ存在状況
DataSetManagement: 5件のデータセット
DataSets: 0件（空、ヘッダーのみ）

-- 特定DataSetIdの所在確認
4abb8dcb-c4f1-485b-b8ac-91ba8c66bd78: DataSetManagementに存在、DataSetsには不存在
```

### 2. ImportServiceの実装分析

#### 共通パターン（全サービス共通）
```csharp
public class [Service]ImportService
{
    private readonly IDataSetRepository _dataSetRepository;          // ❌ 旧リポジトリ
    private readonly IDataSetService _unifiedDataSetService;        // ✅ 新サービス
    
    // データ保存処理（新システム使用）
    public async Task<string> ImportAsync(...)
    {
        // DataSetManagementテーブルに保存（正常）
        dataSetId = await _unifiedDataSetService.CreateDataSetAsync(...);
        return dataSetId;
    }
    
    // データ取得処理（旧システム使用）❌
    public async Task<ImportResult> GetImportResultAsync(string dataSetId)
    {
        var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);  // ❌ DataSetsテーブル参照
        if (dataSet == null)
        {
            throw new InvalidOperationException($"データセットが見つかりません: {dataSetId}");
        }
        // ...
    }
}
```

#### 問題の詳細
1. **保存時**：`_unifiedDataSetService` → DataSetManagementテーブル（正常）
2. **取得時**：`_dataSetRepository` → DataSetsテーブル（エラー）
3. **結果**：保存されたデータを取得できない

### 3. DI設定の確認（Program.cs）

#### 正しく設定されている部分
```csharp
// 新システム（正常）
builder.Services.AddScoped<IDataSetManagementRepository>(provider => 
    new DataSetManagementRepository(connectionString, ...));

// DataSetService（正常）
builder.Services.AddScoped<IDataSetService, DataSetManagementService>();
Console.WriteLine("🔄 DataSetManagement専用モードで起動");
```

#### 問題のある設定
```csharp
// ❌ 旧リポジトリが依然として登録されている
builder.Services.AddScoped<IDataSetRepository>(provider => 
    new DataSetRepository(connectionString, ...));  // 削除が必要
```

### 4. 各ImportServiceの依存性注入分析

#### PurchaseVoucherImportService（23行目）
```csharp
private readonly IDataSetRepository _dataSetRepository;  // ❌ 旧リポジトリ
```

#### SalesVoucherImportService（48行目）
```csharp
private readonly IDataSetRepository _dataSetRepository;  // ❌ 旧リポジトリ
```

#### InventoryAdjustmentImportService（22行目）
```csharp
private readonly IDataSetRepository _dataSetRepository;  // ❌ 旧リポジトリ
```

## コードの依存関係

### DataSetRepository使用箇所一覧
1. `/src/InventorySystem.Import/Services/PurchaseVoucherImportService.cs:23`
2. `/src/InventorySystem.Import/Services/SalesVoucherImportService.cs:48`
3. `/src/InventorySystem.Import/Services/InventoryAdjustmentImportService.cs:22`
4. `/src/InventorySystem.Import/Services/Masters/SupplierMasterImportService.cs`
5. `/src/InventorySystem.Import/Services/Masters/ProductMasterImportService.cs`
6. `/src/InventorySystem.Import/Services/Masters/CustomerMasterImportService.cs`

### DataSetManagementRepository使用箇所
1. `/src/InventorySystem.Console/Program.cs:203` - DI登録（正常）
2. `/src/InventorySystem.Import/Services/DataSetManagementService.cs` - 実装（正常）
3. `/src/InventorySystem.Data/Repositories/DataSetManagementRepository.cs` - リポジトリ実装（正常）

## 移行に必要な修正箇所リスト

### 優先度1：緊急修正（重大な機能不全）
1. **PurchaseVoucherImportService.cs**
   - 行23: `IDataSetRepository` → `IDataSetManagementRepository`に変更
   - 行365-384: `GetImportResultAsync`メソッドの完全書き換え

2. **SalesVoucherImportService.cs**
   - 行48: `IDataSetRepository` → `IDataSetManagementRepository`に変更
   - 行471-491: `GetImportResultAsync`メソッドの完全書き換え

3. **InventoryAdjustmentImportService.cs**
   - 行22: `IDataSetRepository` → `IDataSetManagementRepository`に変更
   - 行338-358: `GetImportResultAsync`メソッドの完全書き換え

### 優先度2：マスターサービス修正
4. **SupplierMasterImportService.cs** - DataSetRepository依存除去
5. **ProductMasterImportService.cs** - DataSetRepository依存除去
6. **CustomerMasterImportService.cs** - DataSetRepository依存除去

### 優先度3：DI設定クリーンアップ
7. **Program.cs:133-134** - 旧DataSetRepository登録の削除

## リスク評価

### 現在の影響
- **機能影響**：ImportResult取得不可により、エラー調査・デバッグ不可能
- **データ整合性**：データ保存は正常、取得のみ問題
- **運用影響**：インポート成功確認ができない

### 放置した場合のリスク
- **エラー調査不可**：ImportResultが取得できないため、問題特定が困難
- **監査証跡不備**：インポート結果の確認・検証ができない
- **運用効率低下**：成功/失敗判定が手動確認に依存

### 修正時の注意点
1. **段階的修正**：全サービスを同時修正せず、1つずつテスト実行
2. **互換性確保**：既存のDataSetManagementServiceとの連携確認
3. **テストデータ準備**：実際のCSVファイルでの動作確認必須

## GetImportResultAsync修正例

### 修正前（問題のあるコード）
```csharp
public async Task<ImportResult> GetImportResultAsync(string dataSetId)
{
    var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);  // ❌ DataSetsテーブル
    if (dataSet == null)
    {
        throw new InvalidOperationException($"データセットが見つかりません: {dataSetId}");
    }
    // ...
}
```

### 修正後（推奨実装）
```csharp
public async Task<ImportResult> GetImportResultAsync(string dataSetId)
{
    // DataSetManagementテーブルから取得
    var dataSetMgmt = await _dataSetManagementRepository.GetByDataSetIdAsync(dataSetId);
    if (dataSetMgmt == null)
    {
        throw new InvalidOperationException($"データセットが見つかりません: {dataSetId}");
    }
    
    var importedData = await _purchaseVoucherRepository.GetByDataSetIdAsync(dataSetId);
    
    return new ImportResult
    {
        DataSetId = dataSetId,
        Status = dataSetMgmt.Status,
        ImportedCount = dataSetMgmt.RecordCount ?? 0,
        ErrorMessage = dataSetMgmt.ErrorMessage,
        FilePath = dataSetMgmt.SourceFilePath,
        CreatedAt = dataSetMgmt.CreatedAt,
        ImportedData = importedData.Cast<object>().ToList()
    };
}
```

## 結論

この問題は **移行作業の不完全実装** により発生した重大な設計不整合である。データ保存（新システム）とデータ取得（旧システム）が分離されているため、システムの基本機能が破綻している。

**即座の対応が必要** であり、特に以下の修正を最優先で実行すべき：
1. 全ImportServiceの`GetImportResultAsync`メソッド修正
2. 旧DataSetRepository依存の除去
3. DataSetManagementRepositoryへの完全移行

修正完了により、インポート処理の結果確認・エラー調査が正常化し、システムの安定性が大幅に向上する。

---

**調査実施日時**: 2025-07-24 14:37:30  
**調査者**: Claude Code  
**対象システム**: InventoryManagementSystem v2.0  
**重要度**: 🔴 Critical - 即座の修正が必要