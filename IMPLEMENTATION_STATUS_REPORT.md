# 在庫管理システム 実装状況調査レポート

調査日時: 2025-07-12 11:45:00
調査者: Claude Code

## 1. 日次終了処理での非アクティブ化機能

### 1.1 実装状況
- [x] 完全実装

### 1.2 詳細確認結果

#### DailyCloseService.cs:735-739行目
```csharp
// ステップ5: 在庫ゼロ商品の非アクティブ化
_logger.LogInformation("ステップ5: 在庫ゼロ商品の非アクティブ化を開始");
var deactivatedCount = await DeactivateZeroStockItemsAsync(jobDate);
_logger.LogInformation("非アクティブ化完了: {Count}件", deactivatedCount);
result.DeactivatedCount = deactivatedCount;
```

#### DeactivateZeroStockItemsAsync メソッド (812-869行目)
- **判定条件**: `LastSalesDate`と`LastPurchaseDate`の**どちらか新しい日付**を使用
- **フォールバック**: 両方がNULLの場合のみ`JobDate`を使用
- **設定可能項目**:
  - `DeactivateZeroStock:Enabled` (true/false)
  - `DeactivateZeroStock:InactiveDaysThreshold` (デフォルト180日)
  - `DeactivateZeroStock:DryRunMode` (true/false)
- **監査ログ**: ProcessHistoryServiceを使用して完全に記録

#### InventoryRepository.cs:1535-1611行目
- `GetInactiveTargetCountAsync`: 対象件数の事前確認（ドライラン）
- `DeactivateZeroStockItemsAsync`: 実際の非アクティブ化処理
- **判定SQL**: 以下の条件をすべて満たす在庫を非アクティブ化
  ```sql
  WHERE CurrentStock = 0
    AND ISNULL(PreviousMonthQuantity, 0) = 0
    AND IsActive = 1
    AND DATEDIFF(DAY, 
        ISNULL(
            CASE 
                WHEN ISNULL(LastSalesDate, '1900-01-01') > ISNULL(LastPurchaseDate, '1900-01-01') 
                THEN LastSalesDate
                ELSE LastPurchaseDate
            END,
            JobDate
        ), 
        @JobDate) >= @InactiveDays
  ```

### 1.3 データベース構造
#### 007_AddLastTransactionDates.sql
- [x] `LastSalesDate DATE NULL` カラム追加済み
- [x] `LastPurchaseDate DATE NULL` カラム追加済み
- [x] インデックス作成済み (`IX_InventoryMaster_LastSalesDate`, `IX_InventoryMaster_LastPurchaseDate`)
- [x] 既存データの初期値設定済み

### 1.4 設定ファイル
**appsettings.json** に以下の設定項目が利用可能:
```json
{
  "InventorySystem": {
    "DailyClose": {
      "DeactivateZeroStock": {
        "Enabled": true,
        "InactiveDaysThreshold": 180,
        "DryRunMode": false
      }
    }
  }
}
```

## 2. アンマッチチェックの日付フィルタリング

### 2.1 実装状況
- [x] 完全実装

### 2.2 詳細確認結果

#### UnmatchListServiceV2.cs:90-118行目
- **targetDateパラメータ**: 正しく処理されている
- **CP在庫マスタ作成**: 92行目で`targetDate`を`CreateCpInventoryFromInventoryMasterAsync`に渡している
- **データ集計**: 102-104行目で各集計メソッドに`targetDate`を渡している
- **アンマッチリスト生成**: 115-117行目で条件分岐により適切に処理

#### CpInventoryRepository.cs:16-27行目
```csharp
public async Task<int> CreateCpInventoryFromInventoryMasterAsync(string dataSetId, DateTime? jobDate)
{
    // 累積管理対応版：在庫マスタのレコードをCP在庫マスタにコピー
    // jobDateがnullの場合は全期間対象
    using var connection = new SqlConnection(_connectionString);
    var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
        "sp_CreateCpInventoryFromInventoryMasterCumulative",
        new { DataSetId = dataSetId, JobDate = jobDate },
        commandType: CommandType.StoredProcedure);
    
    return result?.CreatedCount ?? 0;
}
```

### 2.3 データベース構造
#### sp_CreateCpInventoryFromInventoryMasterCumulative.sql
- [x] `@JobDate DATE = NULL` パラメータで日付フィルタリング対応
- [x] NULLの場合は全期間対象
- [x] 指定日以前のアクティブな在庫で、対象期間の伝票に関連する5項目キーのレコードのみを処理

## 3. DatasetManagementテーブル関連

### 3.1 実装状況
- [x] 完全実装

### 3.2 テーブル構造

#### 基本テーブル (AddErrorPreventionTables.sql)
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

#### 拡張カラム (008_UpdateDatasetManagement.sql)
- [x] `ImportType NVARCHAR(20) NOT NULL DEFAULT 'IMPORT'` 追加済み
- [x] `RecordCount INT NOT NULL DEFAULT 0` 追加済み
- [x] `IsActive BIT NOT NULL DEFAULT 1` 追加済み
- [x] `IsArchived BIT NOT NULL DEFAULT 0` 追加済み
- [x] `ParentDataSetId NVARCHAR(100) NULL` 追加済み
- [x] `Notes NVARCHAR(MAX) NULL` 追加済み

#### 追加実装が必要なカラム
エンティティには存在するが、マイグレーションスクリプトにない：
- `TotalRecordCount INT`
- `DeactivatedAt DATETIME2`
- `DeactivatedBy NVARCHAR(100)`
- `ArchivedAt DATETIME2`
- `ArchivedBy NVARCHAR(100)`
- `Department NVARCHAR(10)`

### 3.3 エンティティとリポジトリ
#### DatasetManagement.cs
- [x] 全プロパティ実装済み
- [x] `GenerateDataSetId` 静的メソッド実装済み
- [x] ナビゲーションプロパティ (`ProcessHistories`) 実装済み

#### DatasetManagementRepository.cs:22-31行目
**CreateAsyncメソッドのSQL文**: 使用中のカラム
```sql
INSERT INTO DatasetManagement (
    DatasetId, JobDate, ProcessType, ImportType, RecordCount, TotalRecordCount,
    IsActive, IsArchived, ParentDataSetId, ImportedFiles, CreatedAt, CreatedBy, 
    Notes, Department
)
```

**問題**: `TotalRecordCount`と`Department`カラムが実際のテーブルに存在しない可能性

## 4. 最終取引日の更新処理

### 4.1 実装状況
- [x] 完全実装

### 4.2 詳細確認結果

#### InventoryRepository.cs:1616-1691行目
- [x] `UpdateLastSalesDateAsync` メソッド実装済み (1616-1651行目)
- [x] `UpdateLastPurchaseDateAsync` メソッド実装済み (1656-1691行目)

#### 更新条件
両メソッドとも以下の安全な更新条件を使用：
```sql
WHERE sv.JobDate > ISNULL(im.LastSalesDate, '1900-01-01')
WHERE pv.JobDate > ISNULL(im.LastPurchaseDate, '1900-01-01')
```

#### 呼び出し箇所
- **SalesVoucherImportService.cs:246行目**: 売上伝票インポート後に`UpdateLastSalesDateAsync`を呼び出し
- **PurchaseVoucherImportService.cs:233行目**: 仕入伝票インポート後に`UpdateLastPurchaseDateAsync`を呼び出し
- **ImportWithCarryoverCommand.cs:133行目, 146行目**: 引継処理後に両メソッドを呼び出し

#### エラーハンドリング
すべての呼び出し箇所で適切なtry-catch処理が実装され、更新失敗時も処理継続される設計。

## 5. import-with-carryoverコマンドの実装

### 5.1 実装状況
- [x] 完全実装

### 5.2 詳細確認結果

#### ImportWithCarryoverCommand.cs
**実装方式**: 
- **日付指定**: 自動決定方式（最終処理日の翌日）
- **データ処理**: MERGE方式を使用
- **累積管理**: 完全対応

#### 処理フロー (36-172行目)
1. 最終処理日を取得 (`GetMaxJobDateAsync`)
2. 処理対象日を決定（最終処理日 + 1日）
3. DataSetId生成 (`CARRYOVER_yyyyMMdd_HHmmss_RANDOM`)
4. 現在在庫データ取得（初回は ImportType='INIT' を検索）
5. 当日伝票データ取得
6. 在庫計算処理 (`CalculateInventory`)
7. トランザクション内でMERGE処理 (`ProcessCarryoverInTransactionAsync`)
8. 最終取引日更新
9. 完了メッセージ表示

#### 在庫計算ロジック (177-263行目)
- **売上**: 在庫減少 (`DailyStock -= sales.Quantity`)
- **仕入**: 在庫増加 (`DailyStock += purchase.Quantity`)
- **在庫調整**: 区分1,4,6のみ反映
- **新規商品**: 自動的に在庫マスタに追加

#### DatasetManagement統合 (102-118行目)
```csharp
var datasetManagement = new DatasetManagement
{
    DatasetId = dataSetId,
    JobDate = targetDate,
    ProcessType = "CARRYOVER",
    ImportType = "CARRYOVER",
    // ... その他のプロパティ
};
```

## 6. 実装必要項目サマリー

### 優先度：高
- **DatasetManagementテーブル**: `TotalRecordCount`, `Department`カラムの追加
- **マイグレーションスクリプト**: エンティティとテーブル構造の完全同期

### 優先度：中
- **監視機能**: 非アクティブ化処理の実行状況監視
- **レポート機能**: 最終取引日更新の統計情報

### 優先度：低
- **パフォーマンス最適化**: 大量データ処理時のバッチサイズ調整
- **UI改善**: 設定値の動的変更機能

## 7. リスクと注意事項

### データ整合性リスク
1. **DatasetManagementテーブル構造不整合**: エンティティとテーブル定義の相違によるランタイムエラーの可能性
2. **非アクティブ化の誤動作**: 設定値の不適切な変更により重要な在庫が非アクティブ化される可能性

### パフォーマンスリスク
1. **大量データ処理**: 在庫マスタが数万件を超える場合の処理時間増大
2. **インデックス利用**: LastSalesDate/LastPurchaseDateの検索でインデックスが適切に利用されているか要確認

### 運用リスク
1. **設定値管理**: 非アクティブ化の閾値日数は環境ごとに適切に設定する必要
2. **ログ監視**: 日次終了処理での非アクティブ化件数は日常的に監視が必要

### 実装時の注意点
1. **マイグレーション実行**: 本番環境でのカラム追加は事前にバックアップとテストが必須
2. **既存データへの影響**: 新しいカラム追加時のデフォルト値設定とNULL制約の考慮
3. **Windows環境での動作確認**: FastReport関連機能はWindows環境での最終動作確認が必要

## 8. 結論

調査の結果、在庫管理システムの主要機能は**ほぼ完全に実装**されており、特に以下の点で高い完成度を示している：

1. **非アクティブ化機能**: LastSalesDate/LastPurchaseDateを活用した適切な判定ロジック
2. **日付フィルタリング**: アンマッチチェックでの精密な時点管理
3. **累積管理**: import-with-carryoverコマンドでの完全な前日引継機能
4. **データ管理**: DatasetManagementによる包括的なデータ追跡

**唯一の重要な課題**は、DatasetManagementテーブルの構造とエンティティクラスの不整合であり、これを解決すれば本システムは本格運用可能な状態となる。