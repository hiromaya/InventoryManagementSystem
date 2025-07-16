# import-folderコマンドDataSetsテーブルスキーマエラー詳細調査報告書

作成日時: 2025-07-16 12:30:00

## エグゼクティブサマリー

**根本原因**: DataSetRepositoryのコードが実際のDataSetsテーブルのスキーマと一致していないため、存在しないカラム名にアクセスしようとしてSqlExceptionが発生している。

**主なエラー**: 
- Invalid column name 'RecordCount'
- Invalid column name 'FilePath'
- Invalid column name 'CreatedAt'
- Invalid column name 'UpdatedAt'

**修正すべき箇所**: `DataSetRepository.cs`のSQLクエリとテーブルスキーマの不整合修正

## 1. エラー発生箇所の詳細

### 1.1 エラーログ分析

**エラー発生場所**:
```
DataSetRepository.GetByIdAsync(String id) line 80
UnifiedDataSetService.UpdateRecordCountAsync(String dataSetId, Int32 recordCount) line 184
```

**エラーメッセージ**:
```
Microsoft.Data.SqlClient.SqlException (0x80131904): 
Invalid column name 'RecordCount'.
Invalid column name 'FilePath'.
Invalid column name 'CreatedAt'.
Invalid column name 'UpdatedAt'.
```

### 1.2 影響範囲

**影響を受けるコマンド**:
- `import-folder` コマンド全般
- UnifiedDataSetServiceを使用するすべてのインポート処理

**影響を受けるサービス**:
- InventoryAdjustmentImportService
- 他のCSVインポートサービス

## 2. 現在のテーブル構造と期待される構造の差異

### 2.1 実際のDataSetsテーブルスキーマ

**source**: `database/migrations/04_create_import_tables.sql`

```sql
CREATE TABLE DataSets (
    Id NVARCHAR(50) PRIMARY KEY,
    DataSetType NVARCHAR(20) NOT NULL,     -- ✅ 存在する
    ImportedAt DATETIME2 NOT NULL,         -- ✅ 存在する
    RecordCount INT NOT NULL,              -- ✅ 存在する
    Status NVARCHAR(20) NOT NULL,          -- ✅ 存在する
    ErrorMessage NVARCHAR(MAX),            -- ✅ 存在する
    FilePath NVARCHAR(500),                -- ✅ 存在する
    JobDate DATE NOT NULL,                 -- ✅ 存在する
    CreatedAt DATETIME2 DEFAULT GETDATE(), -- ✅ 存在する
    UpdatedAt DATETIME2 DEFAULT GETDATE()  -- ✅ 存在する
);
```

### 2.2 DataSetRepository.csでアクセスしようとしているカラム

**GetByIdAsyncメソッド（73-80行目）**:
```csharp
const string sql = @"
    SELECT Id, Name, Description, ProcessType, ImportedAt, RecordCount, Status, 
           ErrorMessage, FilePath, JobDate, CreatedAt, UpdatedAt
    FROM DataSets 
    WHERE Id = @Id";
```

**問題のあるカラム**:
- `Name` - ❌ テーブルに存在しない
- `Description` - ❌ テーブルに存在しない  
- `ProcessType` - ❌ テーブルに存在しない（実際は`DataSetType`）

**正しく存在するカラム**:
- `RecordCount` - ✅ 存在する
- `FilePath` - ✅ 存在する
- `CreatedAt` - ✅ 存在する
- `UpdatedAt` - ✅ 存在する

## 3. DataSetエンティティとテーブルスキーマの不整合

### 3.1 DataSetエンティティクラス

**source**: `src/InventorySystem.Core/Entities/DataSet.cs`

```csharp
public class DataSet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;           // ❌ テーブルに存在しない
    public string Description { get; set; } = string.Empty;    // ❌ テーブルに存在しない
    public string ProcessType { get; set; } = string.Empty;    // ❌ 実際は DataSetType
    public DateTime ImportedAt { get; set; }                   // ✅ 存在する
    public int RecordCount { get; set; }                       // ✅ 存在する
    public string Status { get; set; } = string.Empty;         // ✅ 存在する
    public string? ErrorMessage { get; set; }                  // ✅ 存在する
    public string? FilePath { get; set; }                      // ✅ 存在する
    public DateTime JobDate { get; set; }                      // ✅ 存在する
    public DateTime CreatedAt { get; set; }                    // ✅ 存在する
    public DateTime UpdatedAt { get; set; }                    // ✅ 存在する
}
```

### 3.2 不整合の詳細

| エンティティプロパティ | 実際のテーブルカラム | 状態 |
|-------------------|------------------|------|
| `Name` | - | ❌ 存在しない |
| `Description` | - | ❌ 存在しない |
| `ProcessType` | `DataSetType` | ❌ 名前が異なる |
| `ImportedAt` | `ImportedAt` | ✅ 一致 |
| `RecordCount` | `RecordCount` | ✅ 一致 |
| `Status` | `Status` | ✅ 一致 |
| `ErrorMessage` | `ErrorMessage` | ✅ 一致 |
| `FilePath` | `FilePath` | ✅ 一致 |
| `JobDate` | `JobDate` | ✅ 一致 |
| `CreatedAt` | `CreatedAt` | ✅ 一致 |
| `UpdatedAt` | `UpdatedAt` | ✅ 一致 |

## 4. マイグレーションスクリプトの確認

### 4.1 024_PrepareDataSetUnification.sqlの内容

```sql
-- DataSetsテーブルが空の場合の対応
IF NOT EXISTS (SELECT 1 FROM DataSets)
BEGIN
    PRINT 'DataSetsテーブルは空です。統合準備完了。'
END
ELSE
BEGIN
    PRINT 'DataSetsテーブルにデータが存在します。手動確認が必要です。'
END
```

**結果**: このマイグレーションスクリプトはDataSetsテーブルの構造を変更しない。

### 4.2 必要なマイグレーション

DataSetsテーブルに以下のカラムを追加する必要がある：
- `Name` NVARCHAR(200)
- `Description` NVARCHAR(500)
- `ProcessType` を `DataSetType` に統一するか、別途追加

## 5. UnifiedDataSetServiceでの使用状況

### 5.1 問題のあるメソッド

**UpdateRecordCountAsync（184行目）**:
```csharp
public async Task UpdateRecordCountAsync(string dataSetId, int recordCount)
{
    try
    {
        var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId); // ← ここでエラー
        // ...
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "DataSetsテーブルのレコード数更新失敗: ID={DataSetId}", dataSetId);
        throw;
    }
}
```

### 5.2 エラーの連鎖

1. `UnifiedDataSetService.UpdateRecordCountAsync`が呼び出される
2. `DataSetRepository.GetByIdAsync`が呼び出される
3. 存在しないカラム名を含むSQLクエリが実行される
4. `SqlException`が発生する
5. エラーがUnifiedDataSetServiceに伝播
6. 最終的にimport-folderコマンドが失敗

## 6. 修正方針の提案

### 6.1 Option 1: DataSetRepositoryのSQLクエリ修正（推奨）

**修正内容**:
```csharp
// 修正前
const string sql = @"
    SELECT Id, Name, Description, ProcessType, ImportedAt, RecordCount, Status, 
           ErrorMessage, FilePath, JobDate, CreatedAt, UpdatedAt
    FROM DataSets 
    WHERE Id = @Id";

// 修正後
const string sql = @"
    SELECT Id, DataSetType as ProcessType, ImportedAt, RecordCount, Status, 
           ErrorMessage, FilePath, JobDate, CreatedAt, UpdatedAt
    FROM DataSets 
    WHERE Id = @Id";
```

### 6.2 Option 2: DataSetsテーブルにカラム追加

**マイグレーションスクリプト**:
```sql
-- DataSetsテーブルにカラム追加
ALTER TABLE DataSets ADD Name NVARCHAR(200) NULL;
ALTER TABLE DataSets ADD Description NVARCHAR(500) NULL;
ALTER TABLE DataSets ADD ProcessType NVARCHAR(50) NULL;

-- 既存データの移行
UPDATE DataSets SET ProcessType = DataSetType;
```

### 6.3 Option 3: DataSetエンティティの修正

**修正内容**:
```csharp
public class DataSet
{
    public string Id { get; set; } = string.Empty;
    // Name, Description プロパティを削除
    public string DataSetType { get; set; } = string.Empty;    // ProcessType → DataSetType
    public DateTime ImportedAt { get; set; }
    // ... 他のプロパティ
}
```

## 7. 推奨修正順序

### 7.1 即座に実行すべき修正

1. **DataSetRepository.cs**のSQLクエリ修正
2. **DataSetエンティティ**の不要プロパティ削除
3. **UnifiedDataSetService**の対応メソッド修正

### 7.2 長期的な修正

1. DataSetsテーブルとDataSetManagementテーブルの統合設計見直し
2. 統一されたスキーマ定義の作成
3. 移行スクリプトの作成

## 8. 影響範囲の確認

### 8.1 修正が必要なファイル

- **主要修正**: `src/InventorySystem.Data/Repositories/DataSetRepository.cs`
- **エンティティ修正**: `src/InventorySystem.Core/Entities/DataSet.cs`
- **サービス修正**: `src/InventorySystem.Core/Services/UnifiedDataSetService.cs`

### 8.2 テスト確認項目

- import-folderコマンドの正常動作確認
- 他のCSVインポートコマンドへの影響確認
- DataSetsテーブルへの正常な書き込み確認

## 9. 関連ファイルのパス一覧

### 9.1 エラー発生箇所
- **DataSetRepository**: `src/InventorySystem.Data/Repositories/DataSetRepository.cs:80`
- **UnifiedDataSetService**: `src/InventorySystem.Core/Services/UnifiedDataSetService.cs:184`

### 9.2 関連ファイル
- **テーブル定義**: `database/migrations/04_create_import_tables.sql`
- **エンティティ定義**: `src/InventorySystem.Core/Entities/DataSet.cs`
- **マイグレーション**: `database/migrations/024_PrepareDataSetUnification.sql`

## 10. 緊急度と優先度

### 10.1 緊急度: 高
- import-folderコマンドが全く動作しない
- 本番環境での日常業務に影響する可能性

### 10.2 優先度: 最高
- DataSetManagement統合機能の前提条件
- 他のインポート機能にも影響する基盤部分

---

**調査完了時刻**: 2025-07-16 12:30:00  
**調査者**: Claude Code (Automated Investigation)  
**ステータス**: 修正方針確定・実装待ち  
**推奨修正方法**: Option 1 (DataSetRepositoryのSQLクエリ修正)