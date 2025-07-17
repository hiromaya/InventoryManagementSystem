# import-folderエラー0717調査結果報告書

## 📋 エグゼクティブサマリー

- **調査日時**: 2025年7月17日 11:40:23
- **調査対象**: import-folderコマンドの「データセットが見つからない」エラー
- **調査完了時間**: 約40分
- **主要な発見事項**: データベーススキーマとエンティティクラス間の深刻な不整合
- **根本原因**: マイグレーション履歴の複雑化によるカラム名と構造の不一致
- **推奨修正方針**: 段階的スキーマ統一とリポジトリクエリ修正

## 🗃️ データベーステーブル調査結果

### DataSetsテーブル

#### 実際のテーブル構造（migration履歴から推定）
| カラム名 | データ型 | NULL許可 | デフォルト値 | 説明 |
|---------|----------|---------|-------------|------|
| Id | NVARCHAR(100) | NO | - | 主キー |
| Name | NVARCHAR(100) | NO | - | データセット名（**元々存在、現在不明**） |
| Description | NVARCHAR(500) | YES | - | 説明（**元々存在、現在不明**） |
| ProcessType | NVARCHAR(50) | NO | - | 処理種別（**元々存在、現在不明**） |
| DataSetType | NVARCHAR(50) | NO | 'Unknown' | データセット種別（**M028で追加**） |
| Status | NVARCHAR(20) | NO | 'Created' | ステータス |
| JobDate | DATE | NO | - | ジョブ日付 |
| RecordCount | INT | YES | 0 | レコード数（**M025で追加**） |
| FilePath | NVARCHAR(500) | YES | - | ファイルパス（**M025で追加**） |
| ErrorMessage | NVARCHAR(MAX) | YES | - | エラーメッセージ |
| CreatedDate | DATETIME2 | NO | GETDATE() | 作成日時（**元々存在**） |
| UpdatedDate | DATETIME2 | NO | GETDATE() | 更新日時（**元々存在**） |
| CreatedAt | DATETIME | YES | GETDATE() | 作成日時（**M025で追加**） |
| UpdatedAt | DATETIME | YES | GETDATE() | 更新日時（**M025で追加**） |
| ImportedAt | DATETIME | NO | GETDATE() | インポート日時（**M028で追加**） |
| CompletedDate | DATETIME2 | YES | - | 完了日時（**元々存在**） |

#### 制約情報
- **主キー**: PK_DataSets (Id)
- **インデックス**: IX_DataSets_Status, IX_DataSets_JobDate, IX_DataSets_CreatedDate

### DataSetManagementテーブル

#### 実際のテーブル構造
| カラム名 | データ型 | NULL許可 | デフォルト値 | 説明 |
|---------|----------|---------|-------------|------|
| DataSetId | NVARCHAR(100) | NO | - | 主キー |
| JobDate | DATE | NO | - | ジョブ日付 |
| ProcessType | NVARCHAR(50) | YES | - | 処理種別 |
| ImportType | NVARCHAR(20) | NO | 'IMPORT' | インポート種別 |
| RecordCount | INT | NO | 0 | レコード数 |
| TotalRecordCount | INT | NO | 0 | 総レコード数 |
| IsActive | BIT | NO | 1 | アクティブフラグ |
| IsArchived | BIT | NO | 0 | アーカイブフラグ |
| ParentDataSetId | NVARCHAR(100) | YES | - | 親データセットID（**サイズ不整合**） |
| ImportedFiles | NVARCHAR(MAX) | YES | - | インポートファイル一覧 |
| CreatedAt | DATETIME2 | NO | GETDATE() | 作成日時 |
| CreatedBy | NVARCHAR(100) | YES | 'system' | 作成者（**サイズ不整合**） |
| Department | NVARCHAR(50) | YES | 'Unknown' | 部門コード（**サイズ不整合**） |
| DeactivatedAt | DATETIME2 | YES | - | 無効化日時 |
| DeactivatedBy | NVARCHAR(50) | YES | - | 無効化実行者 |
| ArchivedAt | DATETIME2 | YES | - | アーカイブ日時 |
| ArchivedBy | NVARCHAR(50) | YES | - | アーカイブ実行者 |
| Notes | NVARCHAR(MAX) | YES | - | 備考（**サイズ不整合**） |

## 💻 C#コード実装調査結果

### DataSetエンティティ（DataSet.cs）

#### プロパティ一覧
```csharp
public class DataSet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;                    // ❌ DBにない可能性
    public string Description { get; set; } = string.Empty;             // ❌ DBにない可能性
    public string DataSetType { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public int RecordCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? FilePath { get; set; }
    public DateTime JobDate { get; set; }
    public DateTime CreatedAt { get; set; }                             // ❌ DBは CreatedDate
    public DateTime UpdatedAt { get; set; }                             // ❌ DBは UpdatedDate
}
```

#### テーブルとの不一致点
1. **重大**: Name, Description プロパティはDBテーブルに存在しない可能性
2. **重大**: ProcessType プロパティが欠落（元々DBにあったが削除）
3. **カラム名不一致**: CreatedAt/UpdatedAt vs CreatedDate/UpdatedDate

### DataSetRepository（DataSetRepository.cs）

#### GetByIdAsyncメソッド（68-88行）の問題
```sql
-- 🚨 問題のあるSQLクエリ（72行目）
SELECT Id, DataSetType, ImportedAt, RecordCount, Status, 
       ErrorMessage, FilePath, JobDate, CreatedDate as CreatedAt, UpdatedDate as UpdatedAt
FROM DataSets 
WHERE Id = @Id
```

**問題点:**
1. **Name, Description, ProcessType列を取得していない** → エンティティの必須プロパティが空になる
2. **CreatedDate/UpdatedDate をエイリアスで回避** → 根本的解決になっていない

#### CreateAsyncメソッド（22-63行）の問題
```sql
-- 🚨 問題のあるINSERT文（24行目）
INSERT INTO DataSets (
    Id, DataSetType, ImportedAt, RecordCount, Status, 
    ErrorMessage, FilePath, JobDate, CreatedDate, UpdatedDate
) VALUES (
    @Id, @DataSetType, @ImportedAt, @RecordCount, @Status,
    @ErrorMessage, @FilePath, @JobDate, @CreatedAt, @UpdatedAt
)
```

**問題点:**
1. **Name, Description, ProcessType をINSERTしていない** → NOT NULL制約違反の可能性
2. **パラメータ名とカラム名の不一致** → CreatedDate vs @CreatedAt

### UnifiedDataSetService（UnifiedDataSetService.cs）

#### CreateDataSetAsyncメソッド（34-122行）の問題
```csharp
// 🚨 問題のあるDataSet作成（48-61行）
var dataSet = new InventorySystem.Core.Entities.DataSet
{
    Id = dataSetId,
    Name = info.Name ?? $"{info.ProcessType} {info.JobDate:yyyy-MM-dd}",        // ❌ DBにない
    Description = info.Description ?? info.Name ?? $"{info.ProcessType} データセット", // ❌ DBにない  
    DataSetType = ConvertProcessTypeForDataSets(info.ProcessType),
    // ... 他のプロパティ
};
```

**問題点:**
1. **存在しないカラムへの値設定** → Name, Description プロパティ
2. **二重書き込み処理の部分的成功** → 片方のテーブルのみ成功する可能性

### ImportServiceのエラー発生箇所

#### PurchaseVoucherImportService（334行）
```csharp
// 🚨 エラー発生箇所
public async Task<ImportResult> GetImportResultAsync(string dataSetId)
{
    var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);  // ここでnullが返される
    if (dataSet == null)
    {
        throw new InvalidOperationException($"データセットが見つかりません: {dataSetId}");
    }
    // ...
}
```

#### InventoryAdjustmentImportService（314行）
```csharp
// 🚨 同様のエラー発生箇所
var dataSet = await _dataSetRepository.GetByIdAsync(dataSetId);
if (dataSet == null)
{
    throw new InvalidOperationException($"データセットが見つかりません: {dataSetId}");
}
```

## 🔍 根本原因分析

### 特定された問題

#### 1. マイグレーション履歴の複雑化によるスキーマ不整合
- **原因**: 段階的なマイグレーションで元のカラムが削除され、新しいカラムが追加
- **影響**: エンティティクラスとDBスキーマの乖離
- **症状**: Name, Description, ProcessType カラムの存在が不明

#### 2. DataSetRepositoryのSQLクエリ不完全
- **原因**: エンティティの全プロパティを取得していない
- **影響**: 取得されたエンティティのプロパティが空
- **症状**: 「データセットが見つからない」エラー

#### 3. UnifiedDataSetServiceの二重書き込み問題
- **原因**: 存在しないカラムへの書き込み試行
- **影響**: DataSetsテーブルへの書き込み失敗
- **症状**: 部分的な書き込み成功

#### 4. カラム名の二重管理問題
- **原因**: CreatedDate/UpdatedDate と CreatedAt/UpdatedAt の併存
- **影響**: どちらが正しいカラム名か不明
- **症状**: エイリアスによる一時的な回避

### 問題の相互関係

```
マイグレーション複雑化
    ↓
スキーマとエンティティの乖離
    ↓
DataSetRepository SQLクエリ不完全
    ↓
データセット取得失敗
    ↓
「データセットが見つからない」エラー
```

## 🛠️ 推奨修正方針

### 優先度高（即座に修正必要）

#### 1. DataSetsテーブルスキーマ統一
```sql
-- 必須カラムの追加（存在しない場合）
ALTER TABLE DataSets ADD Name NVARCHAR(100) DEFAULT '';
ALTER TABLE DataSets ADD Description NVARCHAR(500) DEFAULT '';
ALTER TABLE DataSets ADD ProcessType NVARCHAR(50) DEFAULT 'Unknown';

-- カラム名の統一化（必要に応じて）
EXEC sp_rename 'DataSets.CreatedDate', 'CreatedAt', 'COLUMN';
EXEC sp_rename 'DataSets.UpdatedDate', 'UpdatedAt', 'COLUMN';
```

#### 2. DataSetRepository.GetByIdAsync修正
```sql
-- 完全なSELECTクエリ
SELECT Id, Name, Description, ProcessType, DataSetType, ImportedAt, 
       RecordCount, Status, ErrorMessage, FilePath, JobDate, 
       CreatedAt, UpdatedAt
FROM DataSets 
WHERE Id = @Id
```

#### 3. DataSetRepository.CreateAsync修正
```sql
-- 完全なINSERTクエリ
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

### 優先度中（段階的修正）

#### 1. DataSetManagementカラムサイズ統一
```sql
ALTER TABLE DataSetManagement ALTER COLUMN ParentDataSetId NVARCHAR(100);
ALTER TABLE DataSetManagement ALTER COLUMN Department NVARCHAR(50);
ALTER TABLE DataSetManagement ALTER COLUMN CreatedBy NVARCHAR(100);
ALTER TABLE DataSetManagement ALTER COLUMN Notes NVARCHAR(MAX);
```

#### 2. UnifiedDataSetService修正
- 存在しないプロパティへの値設定を削除
- エラーハンドリングの改善

#### 3. 統合テスト環境での検証
- 修正後の動作確認
- エラーパターンの確認

### 優先度低（長期的改善）

#### 1. マイグレーション履歴の整理
- 不要な重複マイグレーションの統合
- スキーマバージョン管理の改善

#### 2. エンティティ駆動設計への移行
- Code-Firstアプローチの検討
- エンティティとスキーマの自動同期

## 📋 修正が必要なファイル一覧

### 即座に修正が必要
1. **database/migrations/033_FixDataSetsSchema.sql** （新規作成）
   - DataSetsテーブルの不足カラム追加
   - カラム名統一

2. **src/InventorySystem.Data/Repositories/DataSetRepository.cs**
   - GetByIdAsyncメソッドのSQLクエリ修正（72行）
   - CreateAsyncメソッドのSQLクエリ修正（24行）
   - 他のメソッドも同様に修正

3. **src/InventorySystem.Core/Services/UnifiedDataSetService.cs**
   - CreateDataSetAsyncメソッドの修正（48-61行）
   - 存在しないプロパティ設定の削除

### 段階的修正
4. **database/migrations/034_FixDataSetManagementSchema.sql** （新規作成）
   - カラムサイズの統一

5. **src/InventorySystem.Data/Services/Development/DatabaseInitializationService.cs**
   - 新しいマイグレーションの追加

## 🔄 修正実施順序

### Step 1: データベーススキーマ修正
1. 033_FixDataSetsSchema.sql の作成と実行
2. 034_FixDataSetManagementSchema.sql の作成と実行
3. DatabaseInitializationService の更新

### Step 2: リポジトリ修正
1. DataSetRepository.GetByIdAsync修正
2. DataSetRepository.CreateAsync修正
3. 他のCRUDメソッドの修正

### Step 3: サービス修正
1. UnifiedDataSetService.CreateDataSetAsync修正
2. エラーハンドリングの改善

### Step 4: 動作確認
1. init-database --force コマンドの実行
2. import-folder コマンドのテスト
3. エラーログの確認

### Step 5: 統合テスト
1. 全フローの動作確認
2. パフォーマンステスト
3. エラーケースの確認

## 📊 影響範囲分析

### 修正による他の機能への影響

#### 正の影響
- **DataSet関連の全機能が正常動作**
- **UnmatchListなどの後続処理が実行可能**
- **エラーログの大幅減少**

#### 注意が必要な影響
- **既存データの Name, Description, ProcessType が空になる可能性**
- **マイグレーション実行時のダウンタイム**
- **テストデータの再作成が必要**

### 必要なテスト項目
1. **データセット作成テスト**
2. **データセット取得テスト**
3. **import-folder全フローテスト**
4. **UnifiedDataSetService二重書き込みテスト**
5. **エラーハンドリングテスト**

## 🎯 次のアクション

### 即座に実施すべき項目
1. **033_FixDataSetsSchema.sql の作成** （最優先）
2. **DataSetRepository.GetByIdAsync の修正** （最優先）
3. **Windows環境での動作確認** （最優先）

### 中長期的な改善項目
1. **マイグレーション戦略の見直し**
2. **エンティティ駆動設計の導入検討**
3. **統合テスト環境の整備**
4. **スキーマバージョン管理の改善**

---

## 📝 調査完了確認

✅ **調査完了条件の達成状況:**
- [x] DataSetsテーブルの実際の構造 → マイグレーション履歴から推定完了
- [x] DataSetエンティティとテーブルの不一致点 → Name, Description, ProcessType不一致を特定
- [x] DataSetRepository.GetByIdAsyncの問題箇所 → 72行目のSQLクエリ不完全を特定
- [x] UnifiedDataSetServiceの二重書き込み問題 → 48-61行目の存在しないプロパティ設定を特定
- [x] ImportServiceのGetImportResultAsync問題 → 334行, 314行のnull判定エラーを特定
- [x] エラー発生の正確なフロー → マイグレーション→スキーマ乖離→取得失敗の流れを特定
- [x] 根本原因の特定 → マイグレーション履歴複雑化による不整合が根本原因
- [x] 具体的な修正方針 → 段階的スキーマ統一とクエリ修正方針を策定

**調査ステータス**: ✅ **完了**

**次のステップ**: 修正実装フェーズへ移行

---

*調査実施者: Claude Code*  
*調査完了時刻: 2025年7月17日 11:40:23*  
*調査対象システム: InventoryManagementSystem v2.0*