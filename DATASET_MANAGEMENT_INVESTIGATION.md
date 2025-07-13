# データセット管理機能調査報告書

調査日: 2025-07-13  
調査者: Claude Code  
対象システム: 在庫管理システム（InventoryManagementSystem）

## 1. 調査概要

### 1.1 調査目的
- DataSetId の生成ロジックと管理方法の解明
- IsActive、ParentDataSetId、ImportType フィールドの使用状況確認
- 各種コマンドでのデータセット実装の調査
- 1日5-7回のアンマッチ処理をサポートする仕組みの確認

### 1.2 調査方法
- ソースコード解析
- データベーススキーマ分析
- 実装パターンの抽出と分類

## 2. データベーススキーマ分析

### 2.1 InventoryMaster テーブル
```sql
-- 基本スキーマ（create_schema.sql）
DataSetId NVARCHAR(50) NOT NULL DEFAULT ''

-- マイグレーション006で追加されたフィールド
IsActive BIT NOT NULL DEFAULT 1
ParentDataSetId NVARCHAR(50) NULL
ImportType NVARCHAR(20) NOT NULL DEFAULT 'UNKNOWN'
CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
UpdatedAt DATETIME2 NULL
```

### 2.2 DataSetManagement テーブル
```sql
CREATE TABLE DataSetManagement (
    DatasetId NVARCHAR(50) NOT NULL PRIMARY KEY,
    JobDate DATE NOT NULL,
    ProcessType NVARCHAR(50) NOT NULL,
    ImportType NVARCHAR(20) NOT NULL DEFAULT 'UNKNOWN',
    RecordCount INT NOT NULL DEFAULT 0,
    TotalRecordCount INT NOT NULL DEFAULT 0,  -- 新規追加
    IsActive BIT NOT NULL DEFAULT 1,
    IsArchived BIT NOT NULL DEFAULT 0,
    ParentDataSetId NVARCHAR(50) NULL,
    ImportedFiles NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'System',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL,
    Department NVARCHAR(10) NULL,  -- 新規追加
    Notes NVARCHAR(MAX) NULL,
    CONSTRAINT FK_DataSetManagement_Parent FOREIGN KEY (ParentDataSetId) 
        REFERENCES DataSetManagement(DatasetId)
);
```

## 3. DataSetId 生成ロジック

### 3.1 生成パターンの分類

#### パターン1: DatasetManagement.GenerateDataSetId（静的メソッド）
```csharp
// 形式: {ImportType}_{yyyyMMdd_HHmmss}_{random6}
var dataSetId = $"{importType}_{DateTime.Now:yyyyMMdd_HHmmss}_{GenerateRandomString(6)}";
```
使用箇所:
- ImportFolderCommand
- ImportWithCarryoverCommand

#### パターン2: DatasetManager.GenerateDatasetId（サービスメソッド）
```csharp
// 形式: DS_{yyyyMMdd}_{HHmmss}_{ProcessType}
var datasetId = $"DS_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}_{processType}";
```
使用箇所:
- DatasetManager サービス

#### パターン3: 各インポートサービス固有
```csharp
// SalesVoucherImportService
return $"SALES_{DateTime.Now:yyyyMMdd_HHmmss}_{guid.Substring(0, 8)}";

// PurchaseVoucherImportService
return $"PURCHASE_{DateTime.Now:yyyyMMdd_HHmmss}_{guid.Substring(0, 8)}";

// PreviousMonthInventoryImportService
return $"PREVINV_{DateTime.Now:yyyyMMdd_HHmmss}_{guid.Substring(0, 8)}";
```

### 3.2 生成ロジックの統一性
現状、DataSetId の生成ロジックが各サービスで独自実装されており、統一性に欠ける。

## 4. フィールド使用状況

### 4.1 IsActive フィールド
- **目的**: 同一 JobDate で複数のデータセットが存在する場合、最新の有効なデータセットを識別
- **デフォルト値**: true
- **使用例**:
  ```csharp
  // 日次終了処理での非アクティブ化
  await _inventoryRepository.DeactivateZeroStockItemsAsync(
      jobDate, 
      daysThreshold, 
      salesThresholdDays, 
      purchaseThresholdDays);
  ```

### 4.2 ParentDataSetId フィールド
- **目的**: データセット間の親子関係を記録（データリネージュ）
- **主な使用場面**: import-with-carryover コマンドでの前日在庫引継ぎ
- **実装例**:
  ```csharp
  // ImportWithCarryoverCommand での使用
  datasetManagement.ParentDataSetId = previousDatasetId;
  ```

### 4.3 ImportType フィールド
- **定義済み値**:
  - `INIT`: 初期在庫インポート
  - `IMPORT`: 通常のCSVインポート
  - `CARRYOVER`: 前日在庫引継ぎ
  - `MANUAL`: 手動調整
  - `UNKNOWN`: 未分類（デフォルト）
- **使用状況**: DatasetManagement.CreateDataset メソッドで ProcessType から自動設定

## 5. コマンド実装分析

### 5.1 import-folder コマンド
**実装ファイル**: `ImportFolderCommand.cs`

**処理フロー**:
1. DataSetId 生成: `IMPORT_{yyyyMMdd_HHmmss}_{random6}`
2. DatasetManagement レコード作成
3. ファイル処理順序:
   - マスタファイル（優先度1）
   - 前月末在庫（優先度2）
   - 伝票ファイル（優先度3）
4. 各ファイルに同一 DataSetId を使用
5. ImportedFiles フィールドに処理ファイルリストを JSON 形式で保存

**特徴**:
- 部門別フォルダ構造対応
- エラーファイルの自動移動
- 処理統計情報の記録

### 5.2 import-with-carryover コマンド
**実装ファイル**: `ImportWithCarryoverCommand.cs`

**処理フロー**:
1. 前日の在庫データ取得
2. 新規 DataSetId 生成: `CARRYOVER_{yyyyMMdd_HHmmss}_{random6}`
3. ParentDataSetId に前日の DataSetId を設定
4. CP在庫マスタへのコピーと在庫引継ぎ
5. 当日の伝票データインポート
6. 在庫再計算

**特徴**:
- 累積在庫管理の実現
- データリネージュの記録
- トランザクション保証

### 5.3 create-unmatch-list コマンド
**実装ファイル**: `CreateUnmatchListCommand.cs`

**処理フロー**:
1. BatchProcessBase を継承した標準化実装
2. ProcessHistory による処理履歴管理
3. DataSetId による処理データの特定
4. PDF帳票生成

**1日複数回実行のサポート**:
- 各実行に一意の DataSetId を付与
- CreatedAt フィールドで実行時刻を記録
- ProcessHistory で全実行履歴を管理

## 6. 1日5-7回のアンマッチ処理サポート

### 6.1 現在の実装状況
システムは以下の仕組みで複数回実行をサポート:

1. **一意性の確保**:
   - DataSetId にタイムスタンプを含む
   - ProcessHistory で各実行を個別管理

2. **データ識別**:
   - JobDate + DataSetId で特定処理のデータを識別
   - IsActive フラグで最新データを特定可能

3. **履歴管理**:
   - ProcessingHistory テーブルで全実行記録
   - 実行時刻、処理時間、レコード数を記録

### 6.2 課題と改善提案

**課題**:
1. 同一日の複数実行時のパフォーマンス
2. 最新データセットの特定ロジックが複雑
3. 差分処理の未実装

**改善提案**:
1. **バージョン番号の導入**:
   ```csharp
   DataSetId = $"UNMATCH_{jobDate:yyyyMMdd}_V{version}_{timestamp}";
   ```

2. **差分処理の実装**:
   - 前回処理からの変更分のみを対象
   - ParentDataSetId を活用した差分抽出

3. **インデックスの最適化**:
   ```sql
   CREATE INDEX IX_DataSetManagement_JobDate_IsActive 
   ON DataSetManagement (JobDate, IsActive) 
   INCLUDE (DatasetId, CreatedAt);
   ```

## 7. リポジトリメソッド分析

### 7.1 GetLatestDataSetIdAsync
**実装箇所**: 複数のリポジトリで実装

**標準的な実装**:
```csharp
public async Task<string?> GetLatestDataSetIdAsync(DateTime jobDate)
{
    using var connection = await _dbConnectionFactory.CreateConnectionAsync();
    var sql = @"
        SELECT TOP 1 DataSetId 
        FROM SalesVoucher 
        WHERE JobDate = @JobDate 
        ORDER BY VoucherId DESC";
    
    return await connection.QueryFirstOrDefaultAsync<string>(sql, new { JobDate = jobDate });
}
```

**課題**: 各テーブルで独自実装されており、統一的な管理が必要

## 8. 総合評価と提言

### 8.1 実装の完成度
- ✅ 基本的なデータセット管理機能は実装済み
- ✅ 1日複数回の処理実行をサポート
- ✅ データリネージュの基本実装あり
- ⚠️ DataSetId 生成ロジックの統一性に課題
- ⚠️ パフォーマンス最適化の余地あり

### 8.2 改善提言

1. **DataSetId 生成の統一**:
   - DatasetManager サービスを全コマンドで使用
   - 生成ルールの文書化

2. **メタデータの充実**:
   - 処理パラメータの記録
   - 結果サマリーの保存

3. **アーカイブ機能の実装**:
   - 古いデータセットの自動アーカイブ
   - 保持期間ポリシーの設定

4. **監視機能の強化**:
   - 異常な実行回数の検知
   - 処理時間の監視とアラート

## 9. 結論

データセット管理機能は基本的な要件を満たしており、1日5-7回のアンマッチ処理実行をサポートできる構造となっている。しかし、生成ロジックの統一化やパフォーマンス最適化など、運用効率を向上させるための改善余地が存在する。

特に、大量データを扱う本番環境での運用を考慮すると、差分処理の実装やインデックスの最適化は優先的に対応すべき課題である。