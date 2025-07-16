# DataSetManagementへの統一に向けた詳細技術調査報告書

作成日: 2025-07-16  
調査実施者: Claude

## エグゼクティブサマリー

本調査では、現在並存している`DataSets`テーブルと`DataSetManagement`テーブルを`DataSetManagement`に統一することの技術的実現可能性とリスクを詳細に分析しました。

**結論**: 統一は技術的に可能ですが、外部キー制約の存在により慎重な移行計画が必要です。段階的移行アプローチを推奨します。

## 1. 処理フローの詳細分析

### 1.1 import-folderコマンドのDataSets使用状況

import-folderコマンドは以下の流れでDataSetsテーブルを使用しています：

```
1. 各CSVファイルインポート時にDataSetを作成
   - SalesVoucherImportService.ImportAsync()
   - PurchaseVoucherImportService.ImportAsync()
   - InventoryAdjustmentImportService.ImportAsync()

2. DataSet作成フロー:
   - DataSetId生成（GUID形式）
   - DataSetエンティティ作成
   - DataSetRepository.CreateAsync()でINSERT
   - 処理中: UpdateStatusAsync()でステータス更新
   - 完了時: UpdateRecordCountAsync()で件数更新
```

#### 主要な処理箇所

```csharp
// SalesVoucherImportService.cs (line 121-136)
var dataSet = new DataSet
{
    Id = dataSetId,
    ProcessType = "Sales",
    Name = $"売上伝票取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
    Description = $"売上伝票CSVファイル取込: {Path.GetFileName(filePath)}",
    CreatedAt = DateTime.Now,
    RecordCount = 0,
    Status = DataSetStatus.Processing,
    FilePath = filePath,
    JobDate = startDate ?? DateTime.Today,
    DepartmentCode = departmentCode,
    UpdatedAt = DateTime.Now
};

await _dataSetRepository.CreateAsync(dataSet);
```

### 1.2 import-initial-inventoryコマンドのDataSetManagement使用状況

初期在庫インポートは以下の流れでDataSetManagementテーブルを使用：

```
1. InitialInventoryImportService.ImportAsync()
2. DataSetId生成（形式: INITIAL_YYYYMMDD_HHmmss）
3. CSVデータ読み込み・検証
4. DataSetManagementエンティティ作成
5. トランザクション内で一括処理
```

#### 主要な処理箇所

```csharp
// InitialInventoryImportService.cs (line 384-399)
var dataSetManagement = new DataSetManagement
{
    DataSetId = dataSetId,
    JobDate = jobDate,
    ProcessType = "INITIAL_INVENTORY",
    ImportType = "INIT",
    RecordCount = inventories.Count,
    TotalRecordCount = inventories.Count,
    IsActive = true,
    IsArchived = false,
    Department = department,
    CreatedAt = DateTime.Now,
    CreatedBy = "import-initial-inventory",
    Notes = $"初期在庫インポート: {inventories.Count}件"
};
```

### 1.3 トランザクション境界とエラー処理

#### DataSets使用時のトランザクション
- 各サービスが個別にトランザクション管理
- DataSet作成とVoucher登録は別トランザクション
- エラー時はDataSetのステータスを"Error"に更新

#### DataSetManagement使用時のトランザクション
- `ProcessInitialInventoryInTransactionAsync`で統合的に処理
- DataSetManagement登録とInventory登録が同一トランザクション
- エラー時は全体がロールバック

## 2. 機能差分の詳細分析

### 2.1 DataSetsテーブルの列構成

| 列名 | 型 | 用途 | 必須 |
|------|-----|------|------|
| Id | NVARCHAR(100) | GUID形式のID | ✓ |
| Name | NVARCHAR(100) | データセット名 | ✓ |
| Description | NVARCHAR(500) | 説明 | |
| ProcessType | NVARCHAR(50) | Sales/Purchase/Adjustment | ✓ |
| Status | NVARCHAR(20) | Processing/Completed/Error | ✓ |
| JobDate | DATE | ジョブ日付 | ✓ |
| RecordCount | INT | レコード数 | |
| ErrorMessage | NVARCHAR(MAX) | エラーメッセージ | |
| FilePath | NVARCHAR(500) | 元ファイルパス | |
| CreatedDate | DATETIME2 | 作成日時 | ✓ |
| UpdatedDate | DATETIME2 | 更新日時 | ✓ |
| CompletedDate | DATETIME2 | 完了日時 | |
| DepartmentCode | NVARCHAR(10) | 部門コード | |

### 2.2 DataSetManagementテーブルの列構成

| 列名 | 型 | 用途 | DataSetsにない機能 |
|------|-----|------|-----|
| DataSetId | NVARCHAR(100) | データセットID | |
| JobDate | DATETIME | ジョブ日付 | |
| ProcessType | NVARCHAR(50) | 処理種別 | |
| ImportType | NVARCHAR(50) | INIT/IMPORT/CARRYOVER | ✓ |
| RecordCount | INT | レコード数 | |
| TotalRecordCount | INT | 総レコード数 | ✓ |
| IsActive | BIT | アクティブフラグ | ✓ |
| IsArchived | BIT | アーカイブフラグ | ✓ |
| ParentDataSetId | NVARCHAR(100) | 親データセットID | ✓ |
| ImportedFiles | NVARCHAR(MAX) | インポートファイル一覧(JSON) | ✓ |
| CreatedAt | DATETIME2 | 作成日時 | |
| CreatedBy | NVARCHAR(100) | 作成者 | ✓ |
| DeactivatedAt | DATETIME2 | 無効化日時 | ✓ |
| DeactivatedBy | NVARCHAR(100) | 無効化実行者 | ✓ |
| ArchivedAt | DATETIME2 | アーカイブ日時 | ✓ |
| ArchivedBy | NVARCHAR(100) | アーカイブ実行者 | ✓ |
| Notes | NVARCHAR(MAX) | 備考 | ✓ |
| Department | NVARCHAR(50) | 部門 | |
| UpdatedAt | DATETIME2 | 更新日時 | ✓ |

### 2.3 機能差分の影響分析

#### DataSetManagementの追加機能
1. **階層管理**: ParentDataSetIdによる親子関係
2. **ライフサイクル管理**: IsActive/IsArchivedフラグ
3. **監査証跡**: CreatedBy/DeactivatedBy等の実行者記録
4. **複数ファイル管理**: ImportedFilesでJSON形式で保持
5. **詳細な状態管理**: 無効化・アーカイブの日時と実行者

#### DataSetsの独自機能
1. **Name/Description**: 人が読みやすい名前と説明
2. **Status**: Processing/Completed/Errorの状態管理
3. **ErrorMessage**: エラー詳細の保持
4. **FilePath**: 単一ファイルパスの保持

## 3. データ整合性の課題

### 3.1 外部キー制約の詳細

以下のテーブルがDataSets.Idを外部キーとして参照：

```sql
-- SalesVouchers
CONSTRAINT FK_SalesVouchers_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)

-- PurchaseVouchers  
CONSTRAINT FK_PurchaseVouchers_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)

-- InventoryAdjustments
CONSTRAINT FK_InventoryAdjustments_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)
```

### 3.2 カスケード削除の影響

現在の制約にはON DELETE CASCADEが設定されていないため：
- DataSetsレコードを削除すると外部キー制約エラーが発生
- 関連する伝票データを先に削除する必要がある

### 3.3 データ移行時の課題

1. **ID形式の違い**
   - DataSets: GUID形式
   - DataSetManagement: INITIAL_YYYYMMDD_HHmmss形式など

2. **必須項目の不足**
   - DataSetManagementで必須のImportType、IsActive等がDataSetsにない
   - デフォルト値の設定が必要

## 4. コード変更の詳細見積もり

### 4.1 変更が必要なファイル一覧

#### リポジトリ層
- [ ] `/src/InventorySystem.Data/Repositories/DataSetRepository.cs` - 削除
- [ ] `/src/InventorySystem.Core/Interfaces/IDataSetRepository.cs` - 削除
- [ ] 各伝票リポジトリの外部キー参照部分の修正

#### サービス層
- [ ] `/src/InventorySystem.Import/Services/SalesVoucherImportService.cs`
- [ ] `/src/InventorySystem.Import/Services/PurchaseVoucherImportService.cs`
- [ ] `/src/InventorySystem.Import/Services/InventoryAdjustmentImportService.cs`
- [ ] `/src/InventorySystem.Import/Services/PreviousMonthInventoryImportService.cs`

#### エンティティ
- [ ] `/src/InventorySystem.Core/Entities/DataSet.cs` - 削除
- [ ] 各伝票エンティティのDataSetId参照の維持

#### その他
- [ ] Program.csのDI登録変更
- [ ] マイグレーションスクリプトの作成

### 4.2 各ファイルでの具体的な変更内容

#### SalesVoucherImportService.cs の変更例

```csharp
// 変更前
private readonly IDataSetRepository _dataSetRepository;

var dataSet = new DataSet { ... };
await _dataSetRepository.CreateAsync(dataSet);

// 変更後
private readonly IDataSetManagementRepository _dataSetRepository;

var dataSetManagement = new DataSetManagement
{
    DataSetId = dataSetId,
    JobDate = startDate ?? DateTime.Today,
    ProcessType = "Sales",
    ImportType = "IMPORT",
    RecordCount = 0,
    TotalRecordCount = 0,
    IsActive = true,
    IsArchived = false,
    Department = departmentCode,
    CreatedAt = DateTime.Now,
    CreatedBy = "sales-import",
    ImportedFiles = JsonSerializer.Serialize(new[] { filePath })
};
await _dataSetRepository.CreateAsync(dataSetManagement);
```

## 5. 移行シナリオとリスク分析

### 5.1 移行シナリオ

#### シナリオ1: ビッグバン移行
1. メンテナンスウィンドウの設定
2. 既存DataSetsデータをDataSetManagementに一括移行
3. 外部キー制約の削除と再作成
4. アプリケーションコードの一括更新
5. テストと本番デプロイ

**リスク**: 高（ダウンタイムが発生、ロールバックが困難）

#### シナリオ2: 段階的移行（推奨）
1. **Phase 1**: 両テーブルへの二重書き込み
   - 新規データは両方のテーブルに登録
   - 既存コードは変更なし
   
2. **Phase 2**: 読み取りの段階的切り替え
   - 一部機能からDataSetManagementを参照開始
   - 問題があればDataSetsにフォールバック
   
3. **Phase 3**: 完全切り替え
   - すべての参照をDataSetManagementに変更
   - DataSetsテーブルの削除

**リスク**: 低（段階的な検証が可能、ロールバックが容易）

### 5.2 リスクとその対策

| リスク | 影響度 | 発生確率 | 対策 |
|--------|--------|----------|------|
| 外部キー制約エラー | 高 | 高 | 事前の制約削除・再作成スクリプト準備 |
| データ不整合 | 高 | 中 | 二重書き込み期間中の整合性チェック |
| パフォーマンス劣化 | 中 | 低 | インデックスの最適化 |
| ロールバック失敗 | 高 | 低 | 各フェーズでのバックアップ取得 |

### 5.3 並行稼働の実現可能性

#### 実装方針
```csharp
public interface IDataSetService
{
    Task<string> CreateDataSetAsync(DataSetInfo info);
    Task UpdateStatusAsync(string id, string status);
}

public class DualDataSetService : IDataSetService
{
    public async Task<string> CreateDataSetAsync(DataSetInfo info)
    {
        // DataSetsに登録
        await _dataSetRepository.CreateAsync(ConvertToDataSet(info));
        
        // DataSetManagementにも登録
        await _dataSetManagementRepository.CreateAsync(ConvertToDataSetManagement(info));
        
        return info.Id;
    }
}
```

## 6. 推奨事項

### 6.1 短期的対応（1-2週間）
1. 影響分析の詳細化
2. 移行スクリプトのプロトタイプ作成
3. テスト環境での検証

### 6.2 中期的対応（1-2ヶ月）
1. 段階的移行の実装
2. 監視とログの強化
3. パフォーマンステスト

### 6.3 長期的対応（3ヶ月以降）
1. DataSetsテーブルの削除
2. コードのクリーンアップ
3. ドキュメント更新

## 7. 結論

DataSetManagementへの統一は以下の理由から推奨されます：

1. **機能の充実**: ライフサイクル管理、監査証跡など
2. **将来性**: 拡張性の高い設計
3. **保守性**: 単一のデータモデルによる複雑性の削減

ただし、外部キー制約の存在により慎重な移行が必要です。段階的移行アプローチを採用することで、リスクを最小限に抑えながら統一を実現できます。

## 付録: 技術詳細

### A. マイグレーションSQL例

```sql
-- Phase 1: DataSetManagementへのデータ移行
INSERT INTO DataSetManagement (
    DataSetId, JobDate, ProcessType, ImportType, RecordCount, 
    TotalRecordCount, IsActive, IsArchived, Department, 
    CreatedAt, CreatedBy, Notes
)
SELECT 
    Id, JobDate, ProcessType, 
    'IMPORT', -- デフォルト値
    RecordCount, RecordCount, 
    1, -- IsActive = true
    0, -- IsArchived = false
    ISNULL(DepartmentCode, 'DeptA'),
    CreatedDate, 
    'migration',
    CONCAT('Migrated from DataSets: ', Name)
FROM DataSets;

-- Phase 2: 外部キー制約の更新
ALTER TABLE SalesVouchers DROP CONSTRAINT FK_SalesVouchers_DataSets;
ALTER TABLE SalesVouchers ADD CONSTRAINT FK_SalesVouchers_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);
```

### B. 性能比較

| 操作 | DataSets | DataSetManagement |
|------|----------|-------------------|
| INSERT | 高速 | やや低速（追加列） |
| SELECT | 高速 | 同等 |
| UPDATE | 高速 | やや低速（監査列） |
| JOIN | 同等 | 同等 |

### C. 監視項目

1. 移行中のエラー率
2. 処理時間の変化
3. データ整合性チェック結果
4. アプリケーションログのエラー

---

以上