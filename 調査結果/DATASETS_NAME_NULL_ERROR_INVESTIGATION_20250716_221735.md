# DataSets Name列NULL値エラー調査結果
実行日時: 2025-07-16 22:17:35

## 1. エラー概要
- **エラー内容**: `Cannot insert the value NULL into column 'Name', table 'InventoryManagementDB.dbo.DataSets'; column does not allow nulls. INSERT fails.`
- **発生箇所**: `DataSetRepository.cs:51` - `CreateAsync`メソッド内
- **影響範囲**: 初期在庫インポート機能（`import-initial-inventory`コマンド）

## 2. 原因分析

### 2.1 UnifiedDataSetService.CreateDataSetAsync
**ファイル**: `src/InventorySystem.Core/Services/UnifiedDataSetService.cs`

```csharp
// 48-62行目: DataSetエンティティの作成
var dataSet = new InventorySystem.Core.Entities.DataSet
{
    Id = dataSetId,
    DataSetType = ConvertProcessTypeForDataSets(info.ProcessType),
    JobDate = info.JobDate,
    Status = "Processing",
    RecordCount = 0,
    FilePath = info.FilePath,
    ImportedAt = createdAt,
    CreatedAt = createdAt,
    UpdatedAt = createdAt
};
```

**問題点**: 
- `Name`プロパティの設定が完全に**欠落**している
- UnifiedDataSetInfoから`Name`フィールドを取得せず、DataSetエンティティに設定していない

### 2.2 DataSetRepository.CreateAsync
**ファイル**: `src/InventorySystem.Data/Repositories/DataSetRepository.cs`

```csharp
// 24-31行目: INSERT文の定義
const string sql = @"
    INSERT INTO DataSets (
        Id, DataSetType, ImportedAt, Status, JobDate,
        RecordCount, ErrorMessage, FilePath, CreatedAt, UpdatedAt
    ) VALUES (
        @Id, @DataSetType, @ImportedAt, @Status, @JobDate,
        @RecordCount, @ErrorMessage, @FilePath, @CreatedAt, @UpdatedAt
    )";
```

**問題点**:
- INSERT文に`Name`列が**含まれていない**
- パラメータにも`Name`の設定がない

### 2.3 DataSetエンティティ
**ファイル**: `src/InventorySystem.Core/Entities/DataSet.cs`

```csharp
public class DataSet
{
    public string Id { get; set; } = string.Empty;
    public string DataSetType { get; set; } = string.Empty;
    // Name プロパティが存在しない
    public DateTime ImportedAt { get; set; }
    // ... その他のプロパティ
}
```

**問題点**:
- DataSetエンティティクラスに`Name`プロパティが**存在しない**
- データベーステーブルの構造とエンティティクラスの構造が不一致

## 3. データフロー分析

```
InitialInventoryImportService (385-398行目)
↓ UnifiedDataSetInfo作成（Name="初期在庫インポート 2025/07/16"）
UnifiedDataSetService.CreateDataSetAsync (48-62行目)
↓ DataSetエンティティ作成（Nameプロパティ設定なし）
DataSetRepository.CreateAsync (24-51行目)
↓ INSERT文実行（Name列なし）
❌ SQL Server エラー: Name列にNULL値を挿入しようとして失敗
```

## 4. 根本原因

**主要な原因**: エンティティクラスとデータベーステーブル構造の不一致

1. **データベーステーブル側**（CreateDatabase.sql）:
   ```sql
   CREATE TABLE DataSets (
       Id NVARCHAR(100) NOT NULL,
       Name NVARCHAR(100) NOT NULL,  -- NOT NULL制約あり
       Description NVARCHAR(500),
       ProcessType NVARCHAR(50) NOT NULL,
       ...
   )
   ```

2. **エンティティクラス側**（DataSet.cs）:
   - `Name`プロパティが存在しない
   - `DataSetType`プロパティはあるが、テーブルの`ProcessType`列に対応

3. **データマッピングの不整合**:
   - `ProcessType`列に`DataSetType`プロパティを設定
   - `Name`列に対応するプロパティが存在しない

## 5. 修正方針

### 5.1 即座の修正（推奨）
**DataSetエンティティクラスの修正**:
```csharp
public class DataSet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;  // 追加
    public string DataSetType { get; set; } = string.Empty;
    // ... その他のプロパティ
}
```

**DataSetRepository.CreateAsyncの修正**:
```csharp
const string sql = @"
    INSERT INTO DataSets (
        Id, Name, DataSetType, ImportedAt, Status, JobDate,
        RecordCount, ErrorMessage, FilePath, CreatedAt, UpdatedAt
    ) VALUES (
        @Id, @Name, @DataSetType, @ImportedAt, @Status, @JobDate,
        @RecordCount, @ErrorMessage, @FilePath, @CreatedAt, @UpdatedAt
    )";
```

**UnifiedDataSetService.CreateDataSetAsyncの修正**:
```csharp
var dataSet = new InventorySystem.Core.Entities.DataSet
{
    Id = dataSetId,
    Name = info.Name ?? "Unknown",  // 追加
    DataSetType = ConvertProcessTypeForDataSets(info.ProcessType),
    // ... その他の設定
};
```

### 5.2 代替案（データベース構造変更）
- `Name`列を`NULL`許可に変更
- ただし、これは仕様上好ましくない

## 6. 影響範囲

### 6.1 影響を受ける機能
- `import-initial-inventory`コマンド
- DataSetsテーブルを使用するすべての機能
- UnifiedDataSetServiceを使用するすべての処理

### 6.2 影響を受けない機能
- DataSetManagementテーブルを使用する機能（こちらは正常動作）
- 既存のDataSetsテーブルのレコード（Name列は既存データに存在）

## 7. 追加調査が必要な項目

### 7.1 整合性確認
- 他のエンティティクラスでも同様の問題が発生していないか
- DataSetManagementエンティティとテーブル構造の整合性

### 7.2 テスト確認
- 修正後の動作確認テスト
- 既存データへの影響確認

### 7.3 他のプロパティマッピング
- `ProcessType`列と`DataSetType`プロパティの関係
- `Description`列の対応プロパティ確認

## 8. 修正の優先度

**高**: DataSetエンティティクラスの修正
**中**: DataSetRepository.CreateAsyncの修正
**低**: UnifiedDataSetService.CreateDataSetAsyncの修正

## 9. 検証方法

1. 修正後に`import-initial-inventory DeptA`コマンドを実行
2. DataSetsテーブルにレコードが正常に挿入されることを確認
3. `Name`列に適切な値が設定されることを確認

---

**調査完了**: 2025-07-16 22:17:35
**結論**: エンティティクラスとデータベーステーブル構造の不一致が原因。DataSetエンティティにNameプロパティを追加し、対応するCRUD操作を修正することで解決可能。