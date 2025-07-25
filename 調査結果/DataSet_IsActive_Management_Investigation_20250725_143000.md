# IsActiveによるデータセット管理の実装状況調査報告

## 調査日時
2025年7月25日 14:30:00

## 調査背景
現在のシステムでは、DataSetManagementレベルでIsActiveフラグを管理していますが、伝票レベル（SalesVouchers/PurchaseVouchers）にはIsActiveフラグが存在しません。これにより、データセットの復元や切り替え時に問題が発生する可能性について調査を実施しました。

## 1. 現在の実装状況サマリー

### ✅ 実装済み機能
- **DataSetManagementレベルのIsActive管理**: 完全実装済み
- **InventoryMasterテーブルのIsActive管理**: 実装済み（クエリ２/17.csv確認）
- **DataSetManagement無効化処理**: `DeactivateOldDataSetsAsync`メソッド実装済み

### ❌ 未実装機能
- **伝票レベルのIsActive管理**: SalesVouchers/PurchaseVouchers/InventoryAdjustmentsにIsActiveカラムなし
- **import-folder実行時の既存DataSet無効化**: 実装されていない
- **データセット復元機能**: 未実装

## 2. 問題点の詳細

### 2.1 import-folder実行時の問題
- **問題**: `ExecuteImportFromFolderAsync`メソッドで既存DataSetの無効化処理が実装されていない
- **影響**: 同一JobDate+ProcessTypeで複数のDataSetが Active状態で残存する可能性
- **根拠**: Program.cs Line 2032-2150で既存DataSet無効化処理が見当たらない

### 2.2 伝票レベルのIsActive管理欠如
- **問題**: 伝票テーブルにIsActiveカラムが存在しない
- **影響**: DataSet無効化時に伝票データが有効のまま残る
- **詳細**: 以下の通り確認済み
  
### 2.3 データセット復元時の問題
- **問題**: 復元機能自体が実装されていない
- **影響**: 過去のDataSetに戻すことができない
- **確認**: Program.csにrestoreコマンドが存在しない

## 3. 各テーブルの実装状況

| テーブル名 | IsActiveカラム | 管理方法 | 問題点 |
|-----------|---------------|---------|--------|
| DataSetManagement | ✅あり | 完全実装済み（DeactivateOldDataSetsAsync） | なし |
| InventoryMaster | ✅あり | 実装済み | なし |
| SalesVouchers | ❌なし | 未実装 | 伝票データの無効化不可 |
| PurchaseVouchers | ❌なし | 未実装 | 伝票データの無効化不可 |
| InventoryAdjustments | ❌なし | 未実装 | 伝票データの無効化不可 |

### テーブル定義詳細（database/04_create_import_tables.sql調査結果）

#### SalesVouchers (Line 45-76)
```sql
CREATE TABLE SalesVouchers (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DataSetId NVARCHAR(50) NOT NULL,
    -- IsActiveカラムなし
    IsExcluded BIT DEFAULT 0,  -- 除外フラグのみ存在
    ExcludeReason NVARCHAR(100),
    -- ...
);
```

#### PurchaseVouchers (Line 99-130)
```sql
CREATE TABLE PurchaseVouchers (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DataSetId NVARCHAR(50) NOT NULL,
    -- IsActiveカラムなし
    IsExcluded BIT DEFAULT 0,  -- 除外フラグのみ存在
    ExcludeReason NVARCHAR(100),
    -- ...
);
```

#### InventoryAdjustments (Line 153-182)
```sql
CREATE TABLE InventoryAdjustments (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DataSetId NVARCHAR(50) NOT NULL,
    -- IsActiveカラムなし
    IsExcluded BIT DEFAULT 0,  -- 除外フラグのみ存在
    ExcludeReason NVARCHAR(100),
    -- ...
);
```

## 4. データフロー分析

### 現在の実装（問題あり）
```
import-folderコマンド実行
  ↓
スキーマ更新
  ↓
各CSVファイル処理（順次）
  ├── マスタ系ファイル
  ├── 前月末在庫
  └── 伝票系ファイル
  ↓
❌ 既存DataSet無効化なし
  ↓
新規DataSet作成
  ↓
伝票データインポート
  ↓
❌ 伝票レベルIsActive管理なし
```

### 理想的な実装
```
import-folderコマンド実行
  ↓
スキーマ更新
  ↓
✅ 既存DataSet無効化
  ↓
新規DataSet作成
  ↓
各CSVファイル処理
  ↓
✅ 伝票レベルIsActive管理
```

## 5. DataSetManagement実装詳細（実装済み部分）

### 5.1 IsActive管理機能
- **DeactivateOldDataSetsAsync** (DataSetManagementService.cs Line 250-281)
  - 同一JobDate+ProcessTypeの古いDataSetを自動無効化
  - `IsActive = false`, `DeactivatedAt`設定
  - 自動ログ記録
  
- **UpdateStatusAsync** (DataSetManagementService.cs Line 100-136)
  - ステータス変更時のIsActive自動管理
  - "Completed" → `IsActive = true`
  - "Error"/"Failed" → `IsActive = false`, `IsArchived = true`

### 5.2 Repository実装
- **DeactivateDataSetAsync** (DataSetManagementRepository.cs Line 189-211)
  - 指定DataSetの無効化
  - SQL: `UPDATE ... SET IsActive = 0, DeactivatedAt = GETDATE()`

## 6. 各インポートサービスのDataSet無効化処理状況

### 6.1 実装されているサービス
- **PreviousMonthInventoryImportService**: 
  - `DeactivateDataSetAsync`を呼び出し（Line 292確認）
  - 前月末在庫の重複インポート防止

### 6.2 未実装のサービス
- **SalesVoucherImportService**: 無効化処理なし
- **PurchaseVoucherImportService**: 無効化処理なし
- **InventoryAdjustmentImportService**: 無効化処理なし

## 7. データセット復元機能の調査結果

### 7.1 復元機能の実装状況
- **コマンド**: restoreコマンド未実装
- **サービス**: 復元関連メソッド未実装
- **UI**: 復元機能の画面なし

### 7.2 復元が必要になるシナリオ
1. 誤った日付でimport-folderを実行した場合
2. 不正なCSVファイルをインポートした場合
3. システム障害により処理が中断した場合

## 8. 改善提案（調査結果に基づく）

### 8.1 緊急度: 高（すぐに対応が必要）

#### 1. import-folderコマンドへの無効化処理追加
**対象ファイル**: `src/InventorySystem.Console/Program.cs`
**実装箇所**: ExecuteImportFromFolderAsync (Line 2032)
**修正内容**:
```csharp
// 各CSVファイル処理前に追加
await dataSetService.DeactivateOldDataSetsAsync(targetDate, "IMPORT", currentDataSetId);
```

#### 2. 各インポートサービスへの無効化処理追加
**対象ファイル**:
- `src/InventorySystem.Import/Services/SalesVoucherImportService.cs`
- `src/InventorySystem.Import/Services/PurchaseVoucherImportService.cs`  
- `src/InventorySystem.Import/Services/InventoryAdjustmentImportService.cs`

**修正内容**: ImportAsync実行前に既存DataSet無効化処理を追加

### 8.2 緊急度: 中（計画的に対応）

#### 1. 伝票テーブルへのIsActive追加
**データベーススキーマ変更**:
```sql
-- 各伝票テーブルに追加
ALTER TABLE SalesVouchers ADD IsActive BIT NOT NULL DEFAULT 1;
ALTER TABLE PurchaseVouchers ADD IsActive BIT NOT NULL DEFAULT 1;
ALTER TABLE InventoryAdjustments ADD IsActive BIT NOT NULL DEFAULT 1;

-- インデックス追加
CREATE INDEX IX_SalesVouchers_IsActive ON SalesVouchers(IsActive);
CREATE INDEX IX_PurchaseVouchers_IsActive ON PurchaseVouchers(IsActive);
CREATE INDEX IX_InventoryAdjustments_IsActive ON InventoryAdjustments(IsActive);
```

#### 2. 伝票レベル無効化処理の実装
**対象ファイル**: 各インポートサービス
**実装内容**: DataSet無効化時に関連伝票データも無効化

### 8.3 緊急度: 低（将来的に実装）

#### 1. データセット復元機能
**新規実装が必要**:
- `restore-dataset <DataSetId>` コマンド
- DataSetManagementService.RestoreDataSetAsync
- 伝票データの復元処理

#### 2. データセット管理UI
**管理画面の実装**:
- 過去のDataSet一覧表示
- IsActive状態の切り替え
- 復元操作のUI

## 9. 実装影響範囲

### 9.1 修正が必要なファイル一覧

#### 緊急度: 高
1. `src/InventorySystem.Console/Program.cs` - import-folder無効化処理追加
2. `src/InventorySystem.Import/Services/SalesVoucherImportService.cs` - 無効化処理追加
3. `src/InventorySystem.Import/Services/PurchaseVoucherImportService.cs` - 無効化処理追加
4. `src/InventorySystem.Import/Services/InventoryAdjustmentImportService.cs` - 無効化処理追加

#### 緊急度: 中
5. `database/migrations/新規マイグレーションファイル` - 伝票テーブルIsActive追加
6. 各インポートサービス - 伝票レベル無効化処理追加
7. 各Repositoryクラス - IsActive条件での検索メソッド追加

### 9.2 データベーススキーマ変更の必要性
- **新規カラム追加**: 3テーブル（SalesVouchers/PurchaseVouchers/InventoryAdjustments）
- **インデックス追加**: IsActiveカラム用インデックス
- **マイグレーション作成**: 段階的な移行処理

### 9.3 既存データへの影響
- **互換性**: 既存のIsExcludedフラグと併用可能
- **データ移行**: 既存レコードはIsActive=1に設定
- **パフォーマンス**: IsActiveインデックス追加によりクエリ性能向上

## 10. リスク分析

### 10.1 修正しない場合のリスク
- **データ整合性の問題**: 複数のActiveなDataSetが混在
- **運用上の混乱**: どのDataSetが有効か判断困難
- **復旧困難**: 問題発生時の対処が複雑

### 10.2 修正時のリスク
- **ダウンタイム**: データベーススキーマ変更時
- **データ移行リスク**: 大量データのIsActive設定
- **テスト工数**: 各インポートサービスの回帰テスト

## 11. 推奨する実装順序

### フェーズ1: 緊急対応（1-2週間）
1. import-folderコマンドへの無効化処理追加
2. 各インポートサービスへの無効化処理追加
3. 単体テスト・統合テスト実施

### フェーズ2: 伝票レベル対応（2-3週間）
1. データベーススキーマ変更
2. 伝票レベル無効化処理実装
3. 既存データの移行

### フェーズ3: 復元機能（4-6週間）
1. restore-datasetコマンド実装
2. 復元処理の実装
3. 管理UI実装

## 結論

現在のシステムは **DataSetManagementレベルでのIsActive管理は完全に実装済み** ですが、**import-folderコマンドでの活用と伝票レベルでの管理が未実装** という状況です。

特に **import-folderコマンド実行時の既存DataSet無効化処理の欠如** は、データ整合性に関わる重要な問題であり、優先的な対応が必要です。

一方、伝票レベルのIsActive管理は現時点で大きな問題は発生していませんが、将来的なデータセット復元機能やより厳密なデータ管理のためには実装が望ましい状況です。