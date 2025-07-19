# sp_UpdateOrCreateInventoryMasterCumulative 不存在問題調査結果

実行日時: 2025-07-19 13:45:00

## 1. エラー発生箇所の詳細

### InventoryRepository.cs
**UpdateOrCreateFromVouchersAsync メソッド（812行目付近）**
```csharp
var result = await connection.QuerySingleAsync<dynamic>(
    "sp_UpdateOrCreateInventoryMasterCumulative",
    new { JobDate = jobDate, DatasetId = datasetId },
    commandType: CommandType.StoredProcedure
);
```

**呼び出しているストアドプロシージャ**: sp_UpdateOrCreateInventoryMasterCumulative
**パラメータ**: JobDate (DATE), DatasetId (NVARCHAR(50))

### UnmatchListService.cs
**OptimizeInventoryMasterAsync メソッド（618行目付近）**
```csharp
processedCount = await _inventoryRepository.UpdateOrCreateFromVouchersAsync(latestJobDate, dataSetId);
_logger.LogInformation("在庫マスタの更新または作成完了: {Count}件", processedCount);
```

## 2. ストアドプロシージャの存在状況

### 必要なプロシージャ
| プロシージャ名 | コード内での使用 | スクリプト存在 | DB存在 |
|--------------|----------------|--------------|--------|
| sp_UpdateOrCreateInventoryMasterCumulative | ✅ | ✅ | ❓ |

### 既存のプロシージャ
| プロシージャ名 | 定義ファイル | 作成日 |
|--------------|------------|--------|
| sp_CreateCpInventoryFromInventoryMasterCumulative | sp_CreateCpInventoryFromInventoryMasterCumulative.sql | 2025-07-11 |
| sp_MergeInventoryMasterCumulative | sp_MergeInventoryMasterCumulative.sql | 2025-07-19 |
| sp_MergeInitialInventory | sp_MergeInitialInventory.sql | 2025-07-18 |
| sp_CreateCpInventoryFromInventoryMasterCumulative_AllPeriods | sp_CreateCpInventoryFromInventoryMasterCumulative_AllPeriods.sql | 2025-07-12 |
| sp_CreateCpInventoryFromInventoryMasterWithProductInfo | sp_CreateCpInventoryFromInventoryMasterWithProductInfo.sql | 2025-07-10 |
| sp_UpdateOrCreateInventoryMasterCumulative | sp_UpdateOrCreateInventoryMasterCumulative.sql | 2025-07-10 |

## 3. 累積管理機能の実装状況

### 関連メソッド一覧
- `InventoryRepository.UpdateOrCreateFromVouchersAsync()` - 伝票から在庫マスタを累積更新
- `UnmatchListService.OptimizeInventoryMasterAsync()` - アンマッチリスト処理での在庫最適化
- `CpInventoryRepository` - 累積在庫データからCP在庫を作成

### 実装の流れ
1. UnmatchListService が OptimizeInventoryMasterAsync を呼び出し
2. InventoryRepository.UpdateOrCreateFromVouchersAsync が実行される
3. sp_UpdateOrCreateInventoryMasterCumulative ストアドプロシージャが呼び出される
4. 伝票データから在庫マスタを累積更新（5項目キーのみで管理）

## 4. 問題の原因分析

### 推定される原因
1. **マイグレーション実行順序の問題**: ストアドプロシージャがデータベース初期化時に作成されていない
2. **スクリプト実行タイミング**: procedures/ フォルダのスクリプトが _migrationOrder に含まれていない

### 名称の一致確認
- コード側: `sp_UpdateOrCreateInventoryMasterCumulative`
- 実際のスクリプト: `sp_UpdateOrCreateInventoryMasterCumulative.sql` ✅ (一致)
- 機能の違い: 
  - `sp_UpdateOrCreateInventoryMasterCumulative`: 伝票から累積更新
  - `sp_MergeInventoryMasterCumulative`: 在庫マスタのマージ処理

## 5. マイグレーション構成

### _migrationOrder の内容（関連部分のみ）
```csharp
private readonly List<string> _migrationOrder = new()
{
    // === 基本マイグレーション ===
    "000_CreateMigrationHistory.sql",
    
    // === データベース構造追加 ===
    "005_AddDailyCloseProtectionColumns.sql",
    "006_AddDataSetManagement.sql",
    ...
    "038_RecreateDailyCloseManagementIdealStructure.sql",
    
    // === CreatedAt/UpdatedAt移行フェーズ（05_create_master_tables.sqlで不要） ===
    "050_Phase1_CheckCurrentSchema.sql"
};
```

### ストアドプロシージャ関連スクリプト
**⚠️ 重要な発見**: procedures/ フォルダ内のスクリプトが _migrationOrder に含まれていない

| スクリプトファイル | _migrationOrder 含有 | 説明 |
|------------------|-------------------|------|
| sp_UpdateOrCreateInventoryMasterCumulative.sql | ❌ | 対象のストアドプロシージャ |
| sp_MergeInventoryMasterCumulative.sql | ❌ | 在庫マスタマージ処理 |
| sp_MergeInitialInventory.sql | ❌ | 初期在庫マージ処理 |
| sp_CreateCpInventoryFromInventoryMasterCumulative.sql | ❌ | CP在庫作成処理 |

## 6. 修正方針の提案（実装はしない）

### Option A: ストアドプロシージャをマイグレーション順序に追加
1. procedures/ フォルダ内の必要なスクリプトを _migrationOrder に追加
2. 実行順序は基本テーブル作成後、機能実装前に配置
3. 推奨追加位置: "05_create_master_tables.sql" の直後

### Option B: 手動でストアドプロシージャを実行
1. init-database 実行後に手動で procedures/ 内のスクリプトを実行
2. 暫定対処法として有効だが運用面で問題あり

### Option C: 初期化プロセスの改善
1. DatabaseInitializationService にストアドプロシージャ作成ロジックを追加
2. procedures/ フォルダを自動スキャンして実行

## 7. 追加発見事項

### 7.1 スクリプト内容の確認
`sp_UpdateOrCreateInventoryMasterCumulative.sql` の内容：
- 作成日: 2025-07-10
- 目的: 伝票データから在庫マスタを累積更新（5項目キーのみで管理）
- パラメータ: @JobDate (DATE), @DatasetId (NVARCHAR(50))
- 戻り値: InsertedCount, UpdatedCount

### 7.2 機能の違い
| プロシージャ名 | 用途 | 対象データ |
|--------------|------|----------|
| sp_UpdateOrCreateInventoryMasterCumulative | 伝票から在庫マスタを累積更新 | SalesVouchers, PurchaseVouchers, InventoryAdjustments |
| sp_MergeInventoryMasterCumulative | 在庫マスタのマージ処理 | InventoryMaster |

### 7.3 累積管理関連ファイルの分布
- **Core Services**: UnmatchListService.cs, InventoryOptimizationService.cs
- **Data Repositories**: InventoryRepository.cs, CpInventoryRepository.cs
- **Database Procedures**: 6個のストアドプロシージャスクリプト
- **Migration Scripts**: 020_Fix_MergeInventoryMaster_OutputClause.sql

## 8. 結論

**問題の核心**: ストアドプロシージャ `sp_UpdateOrCreateInventoryMasterCumulative` は存在するが、データベース初期化時に実行されていない。

**根本原因**: procedures/ フォルダ内のスクリプトが DatabaseInitializationService の _migrationOrder に含まれていないため、`init-database` コマンド実行時にストアドプロシージャが作成されない。

**推奨解決策**: _migrationOrder にストアドプロシージャ作成スクリプトを適切な順序で追加する。

## 9. 緊急度評価

- **緊急度**: 高（アンマッチリスト機能が動作しない）
- **影響範囲**: UnmatchListService.OptimizeInventoryMasterAsync メソッド
- **修正難易度**: 低（_migrationOrder への追加のみ）
- **テスト要件**: init-database 実行後のストアドプロシージャ存在確認

---

**調査実行者**: Claude Code  
**調査完了日時**: 2025-07-19 13:45:00  
**次のアクション**: _migrationOrder への procedures スクリプト追加の検討