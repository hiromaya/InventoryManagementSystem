# DataSetRepositoryエラー詳細調査結果
**日時**: 2025-07-23  
**調査対象**: import-folderコマンド実行時のSQLエラー

## 🔍 問題の概要

import-folderコマンド実行時に、DataSetRepositoryで以下のSQLエラーが発生：
- Invalid column name 'DataSetType'
- Invalid column name 'ImportedAt'
- Invalid column name 'RecordCount'
- Invalid column name 'FilePath'

## 📊 データベーステーブル構造比較

### 実際のDataSetsテーブル構造（クエリ/26.jsonより）
| カラム名 | データ型 | NULL許可 | 順序 |
|---------|----------|----------|------|
| Id | nvarchar(100) | NO | 1 |
| Name | nvarchar(100) | NO | 2 |
| Description | nvarchar(500) | YES | 3 |
| ProcessType | nvarchar(50) | NO | 4 |
| Status | nvarchar(20) | NO | 5 |
| JobDate | date | NO | 6 |
| CreatedDate | datetime2 | NO | 7 |
| UpdatedDate | datetime2 | NO | 8 |
| CompletedDate | datetime2 | YES | 9 |
| ErrorMessage | nvarchar(MAX) | YES | 10 |

### DataSetエンティティクラス
以下のプロパティがDataSetエンティティに存在するが、実際のテーブルには存在しない：
- `DataSetType` (string)
- `ImportedAt` (DateTime)
- `RecordCount` (int)
- `FilePath` (string?)
- `CreatedAt` (DateTime) ※テーブルには`CreatedDate`として存在
- `UpdatedAt` (DateTime) ※テーブルには`UpdatedDate`として存在

## 🚨 DataSetRepository.csの問題のあるSQLクエリ

### 1. CreateAsync メソッド（22-74行目）
**問題**: 存在しないカラムをINSERTしようとしている

```sql
INSERT INTO DataSets (
    Id, Name, Description, ProcessType, DataSetType, ImportedAt, 
    RecordCount, Status, ErrorMessage, FilePath, JobDate, 
    CreatedAt, UpdatedAt
) VALUES (
    @Id, @Name, @Description, @ProcessType, @DataSetType, @ImportedAt,
    @RecordCount, @Status, @ErrorMessage, @FilePath, @JobDate,
    @CreatedAt, @UpdatedAt
)
```

**存在しないカラム**:
- `DataSetType` 
- `ImportedAt`
- `RecordCount`
- `FilePath`
- `CreatedAt` (正しくは`CreatedDate`)
- `UpdatedAt` (正しくは`UpdatedDate`)

### 2. GetByIdAsync メソッド（79-115行目）
**問題**: 存在しないカラムをSELECTしようとしている

```sql
SELECT Id, Name, Description, ProcessType, DataSetType, ImportedAt, 
       RecordCount, Status, ErrorMessage, FilePath, JobDate, 
       CreatedAt, UpdatedAt
FROM DataSets 
WHERE Id = @Id
```

**存在しないカラム**: 上記と同じ

### 3. UpdateStatusAsync メソッド（120-161行目）
**問題**: 存在しないカラムをUPDATEしようとしている

```sql
UPDATE DataSets 
SET Status = @Status, 
    ErrorMessage = @ErrorMessage,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id
```

**存在しないカラム**: `UpdatedAt` (正しくは`UpdatedDate`)

### 4. UpdateRecordCountAsync メソッド（166-202行目）
**問題**: 存在しないカラムをUPDATEしようとしている

```sql
UPDATE DataSets 
SET RecordCount = @RecordCount,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id
```

**存在しないカラム**: `RecordCount`, `UpdatedAt`

### 5. GetByJobDateAsync メソッド（207-229行目）
**問題**: 存在しないカラムをSELECTしようとしている

```sql
SELECT Id, Name, Description, ProcessType, DataSetType, ImportedAt, 
       RecordCount, Status, ErrorMessage, FilePath, JobDate, 
       CreatedAt, UpdatedAt
FROM DataSets 
WHERE JobDate = @JobDate
ORDER BY ImportedAt DESC
```

**存在しないカラム**: 上記と同じ + ORDER BY句の`ImportedAt`

### 6. GetByStatusAsync メソッド（234-256行目）
**問題**: 存在しないカラムをSELECTしようとしている（GetByJobDateAsyncと同じ）

### 7. UpdateAsync メソッド（287-344行目）
**問題**: 存在しないカラムをUPDATEしようとしている

```sql
UPDATE DataSets 
SET Name = @Name,
    Description = @Description,
    ProcessType = @ProcessType,
    DataSetType = @DataSetType,
    ImportedAt = @ImportedAt,
    RecordCount = @RecordCount,
    Status = @Status,
    ErrorMessage = @ErrorMessage,
    FilePath = @FilePath,
    JobDate = @JobDate,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id
```

**存在しないカラム**: `DataSetType`, `ImportedAt`, `RecordCount`, `FilePath`, `UpdatedAt`

## 📁 影響を受けるサービス

### 直接的な影響
- **SalesVoucherImportService.cs** (47行目, 412行目)
- **PurchaseVoucherImportService.cs** 
- **InventoryAdjustmentImportService.cs**
- **ProductMasterImportService.cs**
- **CustomerMasterImportService.cs**
- **SupplierMasterImportService.cs**

### 使用されている箇所
これらのサービスは`IDataSetRepository`を注入し、主に以下のメソッドを使用：
- `GetByIdAsync()` - 97行目でエラー発生
- その他のメソッドも同様のスキーマ問題を抱えている

## 🎯 根本原因の分析

### 1. スキーマの不一致
- DataSetエンティティクラスとデータベーステーブルの構造が完全に一致していない
- 複数の異なるCREATE TABLE文が存在し、実装が混在している状態

### 2. 複数のテーブル定義の存在
調査により以下の3つの異なるDataSetsテーブル定義を発見：

1. **database/CreateDatabase.sql**: `CreatedDate`/`UpdatedDate`を使用
2. **scripts/windows-setup.sql**: `CreatedAt`/`UpdatedAt` + 追加カラム
3. **database/04_create_import_tables.sql**: `CreatedAt`/`UpdatedAt` + 追加カラム

### 3. 現在のシステム状態
- 実際のデータベースは`database/CreateDatabase.sql`の定義（`CreatedDate`/`UpdatedDate`）
- DataSetRepositoryは`scripts/windows-setup.sql`や`04_create_import_tables.sql`の定義を想定

## 📋 修正が必要な箇所

### 優先度：高（即座に修正が必要）

1. **DataSetRepository.cs** - 全メソッドのSQLクエリ修正
   - 行番号: 32-39, 88-92, 132, 173-174, 210-212, 237-239, 300-307
   - カラム名を実際のテーブル構造に合わせる

2. **DataSet.cs エンティティクラス**
   - 存在しないプロパティの削除または名前変更
   - `CreatedAt` → `CreatedDate`
   - `UpdatedAt` → `UpdatedDate`

### 優先度：中（システム安定後に対応）

3. **関連サービスの調整**
   - DataSetRepositoryを使用している全サービスの動作確認
   - エラーハンドリングの見直し

## 💡 推奨する修正方針

### Option 1: DataSetRepositoryの修正（推奨）
- 実際のテーブル構造に合わせてSQLクエリを修正
- エンティティクラスのプロパティ名を修正

### Option 2: テーブル構造の変更（非推奨）
- 既存データがある場合、マイグレーションが複雑
- 他のシステムへの影響が不明

## ⚠️ 注意事項

1. **データの整合性**: 修正時には既存データの保護が必要
2. **一貫性の確保**: 全ての関連ファイルで統一された定義を使用
3. **テスト**: 修正後は全てのインポート機能のテストが必要

## 📝 修正作業の推定工数

- **DataSetRepository.cs修正**: 2-3時間
- **エンティティクラス修正**: 1時間  
- **関連サービス調整**: 2-3時間
- **テスト・検証**: 3-4時間

**合計推定工数**: 8-11時間

---

**調査完了日時**: 2025-07-23  
**次のアクション**: DataSetRepository.csのSQLクエリ修正から開始することを推奨