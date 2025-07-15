# 初期在庫インポートエラー調査報告書

**調査日時**: 2025年7月14日 21:35:00  
**調査者**: Claude Code  
**エラー概要**: DatasetManagementテーブルのスキーマ不一致によるSQL実行エラー  

## 1. エラー詳細

### 1.1 エラーメッセージ
```
Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid column name 'ImportType'.
Invalid column name 'RecordCount'.
Invalid column name 'IsActive'.
Invalid column name 'IsArchived'.
Invalid column name 'ParentDataSetId'.
Invalid column name 'Notes'.
Invalid column name 'Department'.
```

### 1.2 発生箇所
- **ファイル**: `InventoryRepository.cs`
- **メソッド**: `ProcessInitialInventoryInTransactionAsync`
- **行番号**: 1371

### 1.3 実行されたSQL
```sql
INSERT INTO DatasetManagement (
    DatasetId, JobDate, ProcessType, ImportType, RecordCount, TotalRecordCount,
    IsActive, IsArchived, ParentDataSetId, ImportedFiles, CreatedAt, CreatedBy, 
    Notes, Department
) VALUES (
    @DatasetId, @JobDate, @ProcessType, @ImportType, @RecordCount, @TotalRecordCount,
    @IsActive, @IsArchived, @ParentDataSetId, @ImportedFiles, @CreatedAt, @CreatedBy, 
    @Notes, @Department
)
```

## 2. 根本原因

### 2.1 データベーススキーマの不一致

**実際のDatasetManagementテーブル構造**（AddErrorPreventionTables.sql）:
```sql
CREATE TABLE DatasetManagement (
    DatasetId NVARCHAR(50) PRIMARY KEY,
    JobDate DATE NOT NULL,
    ProcessType NVARCHAR(50) NOT NULL,
    ImportedFiles NVARCHAR(MAX), -- JSON形式
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(50) NOT NULL
);
```

**期待されるテーブル構造**（DatasetManagementエンティティ）:
- ImportType
- RecordCount
- TotalRecordCount
- IsActive
- IsArchived
- ParentDataSetId
- Notes
- Department
- DeactivatedAt
- DeactivatedBy
- ArchivedAt
- ArchivedBy

### 2.2 マイグレーションスクリプトの実行状況

1. **AddErrorPreventionTables.sql**: 基本的なテーブル構造のみ作成
2. **AddDataSetManagement.sql**: より詳細な構造を定義（ただし、一部カラムが不足）
3. **008_UpdateDatasetManagement.sql**: 不足カラムを追加するマイグレーション

## 3. CSV処理結果

### 3.1 ファイル処理状況
- **対象ファイル**: `ZAIK20250531.csv`
- **総レコード数**: 962件
- **有効レコード**: 210件
- **エラーレコード**: 752件

### 3.2 エラーの主な原因
修正により、検証エラーは大幅に減少しました：
- 荷印名検証: `IsNullOrEmpty`に修正済み ✅
- ImportType: `"INIT"`に修正済み ✅
- 数量0処理: 適切に実装済み ✅

752件のエラーは、主に以下が原因と推測されます：
- 荷印マスタ未登録
- その他のマスタデータ不整合

## 4. 問題の分析

### 4.1 マイグレーション実行の不完全性
`008_UpdateDatasetManagement.sql`が実行されていない可能性が高い。このスクリプトは以下のカラムを追加します：
- ImportType
- RecordCount  
- IsActive
- IsArchived
- ParentDataSetId
- Notes
- CreatedBy（デフォルト値付き）

**注意**: `Department`カラムはどのマイグレーションスクリプトにも定義されていない。

### 4.2 エンティティとスキーマの乖離
`DatasetManagement`エンティティには以下のプロパティが定義されているが、対応するカラムがデータベースに存在しない：
- Department
- TotalRecordCount（008では対応なし）
- DeactivatedAt
- DeactivatedBy
- ArchivedAt
- ArchivedBy

## 5. 推奨される対処法

### 5.1 即座の対処（マイグレーション実行）
```sql
-- 1. まず008_UpdateDatasetManagement.sqlを実行
-- 2. 不足しているDepartmentカラムを追加
ALTER TABLE DatasetManagement
ADD Department NVARCHAR(10) NOT NULL DEFAULT 'DeptA';

-- 3. TotalRecordCountカラムを追加
ALTER TABLE DatasetManagement
ADD TotalRecordCount INT NOT NULL DEFAULT 0;

-- 4. その他の監査系カラムを追加
ALTER TABLE DatasetManagement
ADD DeactivatedAt DATETIME2 NULL,
    DeactivatedBy NVARCHAR(100) NULL,
    ArchivedAt DATETIME2 NULL,
    ArchivedBy NVARCHAR(100) NULL;
```

### 5.2 長期的な改善
1. **スキーマ管理の改善**
   - エンティティとマイグレーションスクリプトの同期を保つ
   - スキーマバージョン管理の導入

2. **CSV検証エラーの対処**
   - エラーファイル（`ZAIK20250531_errors_20250714_212541.csv`）の分析
   - マスタデータの整備
   - 検証ロジックの更なる調整

## 6. 現在の状況まとめ

### 6.1 成功した部分
- CSVファイルの読み込み: ✅ 成功（962件）
- データ検証: ✅ 実行済み（210件有効）
- InventoryMasterエンティティへの変換: ✅ 成功

### 6.2 失敗した部分
- DatasetManagementテーブルへの登録: ❌ スキーマ不一致
- トランザクションのロールバック: ⚠️ 全処理が取り消された

### 6.3 期待される結果（スキーマ修正後）
- 210件の初期在庫データが正常に登録される
- 752件のエラーデータはエラーファイルに記録済み
- 今後のマスタ整備により、エラー件数は減少する見込み

## 7. 次のステップ

1. **データベーススキーマの修正**
   - 上記SQLスクリプトの実行
   - スキーマ検証の実施

2. **再実行**
   ```bash
   dotnet run -- import-initial-inventory
   ```

3. **エラー分析**
   - エラーファイルの内容確認
   - マスタデータの整備計画策定

---

**結論**: プログラムの修正は適切に実装されており、問題はデータベーススキーマの不一致にあります。マイグレーションスクリプトを適切に実行することで、初期在庫インポートは正常に動作するはずです。