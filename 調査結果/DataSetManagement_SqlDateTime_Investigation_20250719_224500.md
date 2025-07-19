# DataSetManagement SqlDateTime Overflow エラー調査結果

実行日時: 2025-07-19 22:45:00

## 1. エンティティクラス分析

### DataSetManagement.cs
**ファイルパス**: `/src/InventorySystem.Core/Entities/DataSetManagement.cs`

#### DateTime型プロパティの一覧

| プロパティ名 | 型 | Nullable | デフォルト値 | 問題レベル |
|-------------|----|---------|-----------|-----------| 
| JobDate | DateTime | ❌ | なし | ⚠️ **高リスク** |
| CreatedAt | DateTime | ❌ | なし | ⚠️ **高リスク** |
| UpdatedAt | DateTime | ❌ | なし | ⚠️ **高リスク** |
| DeactivatedAt | DateTime? | ✅ | null | ✅ 安全 |
| ArchivedAt | DateTime? | ✅ | null | ✅ 安全 |

#### 🚨 **重大な問題発見**

以下の3つのDateTime型プロパティが**non-nullable**であるにも関わらず、**デフォルト値が設定されていません**：

```csharp
public DateTime JobDate { get; set; }        // デフォルト値なし → DateTime.MinValue
public DateTime CreatedAt { get; set; }      // デフォルト値なし → DateTime.MinValue  
public DateTime UpdatedAt { get; set; }      // デフォルト値なし → DateTime.MinValue
```

**C#の動作**: non-nullableなDateTime型プロパティは、明示的に値を設定しなければ `DateTime.MinValue` (0001-01-01 00:00:00) になります。

**SQL Serverの制限**: `DATETIME`型は `1753-01-01` が最小値であり、`DateTime.MinValue`をINSERTしようとすると **SqlDateTime overflow** エラーが発生します。

## 2. サービスクラス分析

### UnifiedDataSetService.cs
**ファイルパス**: `/src/InventorySystem.Core/Services/UnifiedDataSetService.cs`

#### CreateDataSetAsyncメソッドの該当部分（line 80-96）

```csharp
var dataSetManagement = new DataSetManagement
{
    DataSetId = dataSetId,
    JobDate = info.JobDate,              // ✅ 明示的に設定
    ProcessType = info.ProcessType,
    ImportType = info.ImportType ?? "IMPORT",
    RecordCount = 0,
    TotalRecordCount = 0,
    IsActive = true,
    IsArchived = false,
    ParentDataSetId = null,
    ImportedFiles = info.FilePath != null ? Path.GetFileName(info.FilePath) : null,
    CreatedAt = createdAt,               // ✅ 明示的に設定
    CreatedBy = info.CreatedBy ?? "system",
    Department = info.Department ?? "Unknown",
    Notes = info.Description
    // ❌ UpdatedAt が未設定！
};
```

#### 🚨 **未初期化フィールドの特定**

**UpdatedAt**プロパティが設定されていません。この結果、`DateTime.MinValue`がデータベースに送信され、SqlDateTime overflowエラーの原因となります。

## 3. データベーススキーマ分析

### DataSetManagementテーブル定義

#### 006_AddDataSetManagement.sql（line 73-96）
```sql
CREATE TABLE DataSetManagement (
    DataSetId NVARCHAR(100) PRIMARY KEY,
    JobDate DATE NOT NULL,
    ImportType NVARCHAR(20) NOT NULL,
    RecordCount INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    IsArchived BIT NOT NULL DEFAULT 0,
    ParentDataSetId NVARCHAR(100) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),  -- ✅ DATETIME2
    CreatedBy NVARCHAR(50) NULL,
    DeactivatedAt DATETIME2 NULL,                    -- ✅ DATETIME2
    DeactivatedBy NVARCHAR(50) NULL,
    ArchivedAt DATETIME2 NULL,                       -- ✅ DATETIME2
    ArchivedBy NVARCHAR(50) NULL,
    Notes NVARCHAR(500) NULL
);
```

#### 🔍 **重要な発見**

1. **DATETIME2を使用**: 新しいマイグレーションでは`DATETIME2`型を使用しており、これは`DateTime.MinValue`を受け入れます
2. **DEFAULT制約**: `CreatedAt`には`DEFAULT GETDATE()`が設定されています
3. **NULLポリシー**: 古いマイグレーションとの間で制約の不整合が存在する可能性があります

#### 036_MigrateDataSetsToDataSetManagement.sql の追加カラム
```sql
-- Phase 0で追加されたカラム
ALTER TABLE DataSetManagement ADD Name NVARCHAR(255) NULL;
ALTER TABLE DataSetManagement ADD Description NVARCHAR(MAX) NULL;
ALTER TABLE DataSetManagement ADD ErrorMessage NVARCHAR(MAX) NULL;
ALTER TABLE DataSetManagement ADD FilePath NVARCHAR(500) NULL;
ALTER TABLE DataSetManagement ADD Status NVARCHAR(20) NULL;
-- UpdatedAtカラムは明示的に追加されていない
```

#### ⚠️ **スキーマ不整合の問題**

**UpdatedAt**カラムがマイグレーション中で追加されていないか、または異なるデータ型で定義されている可能性があります。

## 4. リポジトリクラス分析

### DataSetManagementRepository.cs
**ファイルパス**: `/src/InventorySystem.Data/Repositories/DataSetManagementRepository.cs`

#### INSERT文とパラメータ設定（line 22-31）

```sql
INSERT INTO DataSetManagement (
    DatasetId, Name, Description, FilePath, Status, ErrorMessage, JobDate, ProcessType, ImportType, 
    RecordCount, TotalRecordCount, IsActive, IsArchived, ParentDataSetId, ImportedFiles, 
    CreatedAt, UpdatedAt, CreatedBy, Notes, Department  -- ✅ UpdatedAtが含まれている
) VALUES (
    @DatasetId, @Name, @Description, @FilePath, @Status, @ErrorMessage, @JobDate, @ProcessType, @ImportType,
    @RecordCount, @TotalRecordCount, @IsActive, @IsArchived, @ParentDataSetId, @ImportedFiles, 
    @CreatedAt, @UpdatedAt, @CreatedBy, @Notes, @Department  -- ✅ @UpdatedAtパラメータ使用
)
```

#### 🚨 **パラメータマッピングの問題**

1. **SQL文でUpdatedAtを期待**: INSERT文は`@UpdatedAt`パラメータを使用
2. **エンティティで未設定**: `UnifiedDataSetService`では`UpdatedAt`を設定していない
3. **結果**: `DateTime.MinValue`が`@UpdatedAt`パラメータに渡される

## 5. 問題の特定

### 原因となっているフィールド

**UpdatedAt**フィールドが主要な原因です：

1. **UnifiedDataSetService**: `UpdatedAt`プロパティを初期化していない
2. **エンティティ**: non-nullableで、デフォルト値が`DateTime.MinValue`
3. **リポジトリ**: `@UpdatedAt`パラメータとして`DateTime.MinValue`を送信
4. **データベース**: 古い`DATETIME`型カラムが存在する場合、`DateTime.MinValue`を受け入れられない

### エラー発生メカニズム

```
UnifiedDataSetService.CreateDataSetAsync()
    ↓
new DataSetManagement { /* UpdatedAt未設定 */ }
    ↓  
UpdatedAt = DateTime.MinValue (C#のデフォルト)
    ↓
DataSetManagementRepository.CreateAsync()
    ↓
@UpdatedAt = DateTime.MinValue (0001-01-01)
    ↓
SQL Server INSERT (DTATEIMEカラムの場合)
    ↓
SqlDateTime overflow エラー
```

## 6. 影響範囲

### 直接的な影響
- `UnifiedDataSetService.CreateDataSetAsync()`メソッドが失敗
- DataSetManagement テーブルへの新規レコード作成が不可能
- データセット管理機能全体が動作しない

### 間接的な影響  
- CSV インポート処理の失敗
- データセット作成を伴うすべての機能が影響
- フィーチャーフラグによるDataSetManagement移行が進められない

### 他の潜在的問題
以下のDateTime型フィールドも同様のリスクがあります：
- **JobDate**: `info.JobDate`がDateTime.MinValueの場合
- **CreatedAt**: `createdAt`変数がDateTime.MinValueの場合

## 7. データベーススキーマの詳細調査が必要

### 未確認事項
1. **UpdatedAtカラムの実際のデータ型**: DATETIME vs DATETIME2
2. **NULL制約の設定**: NOT NULL vs NULL
3. **デフォルト値制約**: DEFAULT制約の有無
4. **マイグレーション履歴**: 実際にどのマイグレーションが実行されているか

### 推奨される確認SQL
```sql
-- DataSetManagementテーブルの実際の構造確認
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'DataSetManagement'
ORDER BY ORDINAL_POSITION;

-- UpdatedAtカラムの詳細確認  
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT,
    t.name as TypeName
FROM INFORMATION_SCHEMA.COLUMNS c
INNER JOIN sys.columns sc ON sc.object_id = OBJECT_ID('DataSetManagement') AND sc.name = c.COLUMN_NAME
INNER JOIN sys.types t ON t.system_type_id = sc.system_type_id AND t.user_type_id = sc.user_type_id
WHERE c.TABLE_NAME = 'DataSetManagement' AND c.COLUMN_NAME = 'UpdatedAt';
```

## 8. 推奨される修正方針（実装はしない）

### 短期的修正（緊急）
1. **UnifiedDataSetService修正**: `UpdatedAt`プロパティを明示的に設定
   ```csharp
   var dataSetManagement = new DataSetManagement
   {
       // ... 他のプロパティ
       CreatedAt = createdAt,
       UpdatedAt = createdAt,  // ← 追加
       // ...
   };
   ```

### 中期的修正（安全）
2. **エンティティクラス修正**: DateTime型プロパティにデフォルト値を設定
   ```csharp
   public DateTime UpdatedAt { get; set; } = DateTime.Now;
   ```

3. **データベーススキーマ修正**: UpdatedAtカラムにDEFAULT制約を追加
   ```sql
   ALTER TABLE DataSetManagement 
   ADD CONSTRAINT DF_DataSetManagement_UpdatedAt 
   DEFAULT GETDATE() FOR UpdatedAt;
   ```

### 長期的修正（根本的）
4. **nullable化**: 必須でない DateTime プロパティを nullable に変更
5. **バリデーション強化**: DateTime.MinValue の検証追加
6. **マイグレーション統合**: 複数のマイグレーションファイルを整理

## 9. 他の関連調査項目

### 同様の問題が発生する可能性のあるエンティティ
- **DataSet.cs**: ProcessType, CreatedAt, UpdatedAt
- **SalesVoucher.cs**: VoucherDate, JobDate  
- **PurchaseVoucher.cs**: VoucherDate, JobDate
- **InventoryAdjustment.cs**: VoucherDate, JobDate

### 調査が必要な他のサービス
- **DataSetManagementService.cs**: DataSetManagement専用サービス
- **SalesVoucherImportService.cs**: JobDate設定ロジック
- **PurchaseVoucherImportService.cs**: JobDate設定ロジック

---

## 結論

**UpdatedAt**フィールドの未初期化が SqlDateTime overflow エラーの主要原因です。`UnifiedDataSetService.CreateDataSetAsync()`メソッドで`UpdatedAt`プロパティが設定されていないため、`DateTime.MinValue`がデータベースに送信され、古い`DATETIME`型カラムでエラーが発生しています。

**緊急度**: 🔴 **高** - DataSetManagement機能全体が動作不能
**修正優先度**: 🔴 **最高** - 即座の対応が必要

次のステップとして、実際のデータベーススキーマの確認と、`UnifiedDataSetService`の修正を推奨します。