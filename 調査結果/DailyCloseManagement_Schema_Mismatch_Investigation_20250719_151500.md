# DailyCloseManagementテーブル構造不一致調査結果

実行日時: 2025-07-19 15:15:00

## エグゼクティブサマリー
- **問題**: DailyCloseManagementエンティティクラスと実際のテーブル構造が大幅に異なり、SQL Error 207（Invalid column name）が発生
- **根本原因**: 複数のマイグレーションスクリプトが異なるテーブル構造を定義しており、どれが実際に実行されたかが不明
- **影響**: 日次終了処理が完全に実行不可能
- **推奨対応**: 統一されたマイグレーションスクリプトの作成と実行

## 1. エンティティクラス vs 実テーブル比較

| プロパティ/カラム | エンティティクラス | AddErrorPreventionTables.sql | 005_AddDailyCloseProtectionColumns.sql | DatabaseInitializationService | 不一致 |
|-------------------|-------------------|------------------------------|----------------------------------------|-------------------------------|--------|
| Id | int (IDENTITY) | int IDENTITY(1,1) PRIMARY KEY | int IDENTITY(1,1) PRIMARY KEY | - | - |
| JobDate | DateTime | DATE NOT NULL UNIQUE | - | DATE NOT NULL PRIMARY KEY | ✓ |
| ProcessDate | - | - | DATE NOT NULL | - | ✓ |
| DataSetId | string | NVARCHAR(50) NOT NULL | NVARCHAR(100) NOT NULL | NVARCHAR(50) NOT NULL | ✓ |
| DailyReportDataSetId | string | NVARCHAR(50) NOT NULL | - | NVARCHAR(50) | ✓ |
| BackupPath | string? | NVARCHAR(500) | - | NVARCHAR(500) | ✓ |
| ProcessedAt | DateTime | DATETIME2 NOT NULL | - | DATETIME2 | ✓ |
| ProcessedBy | string | NVARCHAR(50) NOT NULL | - | NVARCHAR(50) | ✓ |
| DataHash | string? | - | NVARCHAR(100) NULL | NVARCHAR(100) | ✓ |
| ValidationStatus | string? | - | NVARCHAR(20) NULL | NVARCHAR(20) | ✓ |
| Remarks | string? | - | NVARCHAR(500) NULL | - | ✓ |
| ProcessType | - | - | NVARCHAR(50) NOT NULL | - | ✓ |
| Status | - | - | NVARCHAR(20) NOT NULL | - | ✓ |
| StartTime | - | - | DATETIME2 NOT NULL | - | ✓ |
| EndTime | - | - | DATETIME2 NULL | - | ✓ |
| RecordCount | - | - | INT NULL | - | ✓ |
| ErrorCount | - | - | INT NULL | - | ✓ |
| CreatedAt | - | - | DATETIME2 NOT NULL DEFAULT GETDATE() | DATETIME2 NOT NULL DEFAULT GETDATE() | ✓ |
| CreatedBy | - | - | NVARCHAR(100) NOT NULL DEFAULT 'System' | - | ✓ |
| ErrorDetails | - | - | NVARCHAR(MAX) NULL | - | ✓ |

## 2. リポジトリSQL文分析

### CreateAsyncメソッド
```sql
INSERT INTO DailyCloseManagement (
    JobDate, DatasetId, DailyReportDatasetId, BackupPath, ProcessedAt, ProcessedBy
) 
OUTPUT INSERTED.*
VALUES (
    @JobDate, @DatasetId, @DailyReportDatasetId, @BackupPath, @ProcessedAt, @ProcessedBy
)
```
**使用カラム**: JobDate, DatasetId, DailyReportDatasetId, BackupPath, ProcessedAt, ProcessedBy
**問題点**: これらのカラムがすべて実際のテーブルに存在しない可能性が高い

### GetByJobDateAsyncメソッド
```sql
SELECT * FROM DailyCloseManagement WHERE JobDate = @JobDate
```
**問題点**: JobDateカラムが存在しない場合、このクエリは失敗

### GetLatestAsyncメソッド
```sql
SELECT TOP 1 * FROM DailyCloseManagement 
ORDER BY JobDate DESC
```
**問題点**: 同様にJobDateカラムに依存

## 3. マイグレーションスクリプト競合分析

| スクリプト名 | 作成日 | テーブル操作 | 主要カラム | 実行順序 | 状態 |
|-------------|--------|-------------|-----------|----------|------|
| AddErrorPreventionTables.sql | 不明 | CREATE TABLE | JobDate, DatasetId, DailyReportDatasetId, BackupPath, ProcessedAt, ProcessedBy | 手動実行 | 単独実行可能 |
| 005_AddDailyCloseProtectionColumns.sql | 2025-07-01 | CREATE TABLE + ALTER TABLE | ProcessDate, ProcessType, Status, StartTime, EndTime, RecordCount, ErrorCount, DatasetId, CreatedAt, CreatedBy | 5番目 | マイグレーション順序内 |
| DatabaseInitializationService (テーブル定義) | 最新 | 理論的定義 | JobDate, DatasetId, DailyReportDatasetId, ProcessedAt, ProcessedBy, ValidationStatus, DataHash, BackupPath | 理論のみ | 実行されない |

### 競合点
1. **主キー定義の違い**: 
   - AddErrorPreventionTables.sql: `JobDate UNIQUE` + `Id PRIMARY KEY`
   - 005_AddDailyCloseProtectionColumns.sql: `Id PRIMARY KEY` のみ

2. **必須カラムの不一致**:
   - AddErrorPreventionTables.sql: JobDateが必須
   - 005_AddDailyCloseProtectionColumns.sql: ProcessDateが必須、JobDateなし

3. **データ型の不一致**:
   - DatasetIdサイズ: NVARCHAR(50) vs NVARCHAR(100)

## 4. 実際のCREATE TABLE文

**注意**: 実際のテーブル構造を確認するため、以下のSQLを実行する必要があります：

```sql
-- テーブル定義の確認
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'DailyCloseManagement'
ORDER BY c.ORDINAL_POSITION;

-- 制約の確認
SELECT 
    tc.CONSTRAINT_NAME,
    tc.CONSTRAINT_TYPE,
    kcu.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
    ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
WHERE tc.TABLE_NAME = 'DailyCloseManagement';
```

## 5. 問題の時系列

1. **2025-07-01** - 005_AddDailyCloseProtectionColumns.sqlで最初のDailyCloseManagementテーブル作成
2. **不明な時点** - AddErrorPreventionTables.sqlが手動実行され、異なる構造で再作成
3. **2025-07-18** - SQLエラー207でDailyCloseService実行時にカラム名エラーが発生
4. **現在** - エンティティクラスと実テーブルの完全な不一致

## 6. 解決策の評価

### 案1: テーブル再作成（推奨）
- **メリット**: 
  - 完全に統一された構造
  - エンティティクラスとの完全一致
  - 将来的な拡張性
- **デメリット**: 
  - 既存データの一時的な損失
  - ダウンタイムが必要
- **実装難易度**: 中

### 案2: エンティティ/リポジトリ修正
- **メリット**: 
  - 既存データを保持
  - 迅速な対応
- **デメリット**: 
  - 実際のテーブル構造に依存
  - 将来的な保守困難
- **実装難易度**: 低

### 案3: 統一マイグレーション作成
- **メリット**: 
  - 段階的な移行
  - データ保持
  - 適切なバージョン管理
- **デメリット**: 
  - 複雑な実装
  - テスト工数増加
- **実装難易度**: 高

## 7. 推奨アクションプラン

### 短期対応（即時）
1. **実際のテーブル構造を確認**
   ```bash
   dotnet run -- execute-sql "SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DailyCloseManagement'"
   ```

2. **エンティティクラスを実テーブルに合わせて一時修正**
   - JobDate → ProcessDate（実テーブルに合わせる）
   - 不足カラムの追加
   - 不要カラムの削除

### 中期対応（1週間以内）
1. **統一マイグレーションスクリプトの作成**
   ```sql
   -- 060_UnifyDailyCloseManagementSchema.sql
   IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('DailyCloseManagement'))
   BEGIN
       -- 既存データのバックアップ
       -- スキーマの統一
       -- データの移行
   END
   ```

2. **DatabaseInitializationServiceの更新**
   - 統一されたテーブル定義への変更
   - マイグレーション順序の整理

### 長期対応（改善案）
1. **スキーマ管理の改善**
   - Entity Frameworkのマイグレーション機能の導入検討
   - スキーマバージョニングの強化

2. **品質保証の強化**
   - 自動テストでのスキーマ検証
   - CI/CDパイプラインでのマイグレーション検証

## 8. リスク評価
- **データ損失リスク**: 中（バックアップ戦略により軽減可能）
- **ダウンタイムリスク**: 中（短時間のメンテナンス窓で対応可能）
- **他機能への影響**: 高（日次終了処理は他の多くの機能に影響）

## 9. 追加発見事項

### スキーマ管理の問題
1. **複数のテーブル作成スクリプト**: 同じテーブルに対して3つの異なる定義が存在
2. **マイグレーション順序の問題**: DatabaseInitializationServiceの実行順序に005_AddDailyCloseProtectionColumns.sqlが含まれている
3. **手動スクリプトとマイグレーションの混在**: AddErrorPreventionTables.sqlが手動実行される設計

### 設計上の課題
1. **エンティティファースト vs データベースファースト**: 開発フローが明確でない
2. **テーブル定義の重複**: 複数箇所での定義により一貫性が失われている
3. **バージョン管理の不備**: どのスクリプトがいつ実行されたかの追跡が困難

### 推奨する根本解決策
1. **統一されたスキーマ管理**: 1つのマイグレーションスクリプトでの完全な定義
2. **エンティティクラスとの同期**: コードファーストアプローチの採用検討
3. **自動テスト**: スキーマとエンティティの一致を検証する単体テスト

## 10. 緊急対応手順

### ステップ1: 現状確認（5分）
```bash
# 実際のテーブル構造を確認
dotnet run -- execute-sql "EXEC sp_help DailyCloseManagement"
```

### ステップ2: 一時修正（30分）
```csharp
// DailyCloseManagement.cs を実テーブルに合わせて修正
public class DailyCloseManagement
{
    public int Id { get; set; }
    public DateTime ProcessDate { get; set; }  // JobDate → ProcessDate
    public string ProcessType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    // ... 実テーブルに合わせて調整
}
```

### ステップ3: リポジトリ修正（30分）
```csharp
// DailyCloseManagementRepository.cs のSQL文を修正
const string sql = @"
    INSERT INTO DailyCloseManagement (
        ProcessDate, ProcessType, Status, StartTime, DatasetId, CreatedBy
    ) 
    OUTPUT INSERTED.*
    VALUES (
        @ProcessDate, @ProcessType, @Status, @StartTime, @DatasetId, @CreatedBy
    )";
```

### ステップ4: 動作確認（15分）
```bash
# 日次終了処理のテスト実行
dotnet run -- check-daily-close 2025-07-19
```

この緊急対応により、少なくとも日次終了処理を実行可能な状態に復旧できます。その後、中長期的な解決策を実装することを強く推奨します。