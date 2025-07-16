# データセット管理のダブルスタンダード調査レポート

調査日時: 2025-01-16

## 要約

在庫管理システムにおいて、データセット管理に2つの異なるテーブルとエンティティが存在し、用途によって使い分けられている状態が確認されました。

- **DataSetsテーブル + DataSetエンティティ**: 主に伝票インポート処理で使用
- **DataSetManagementテーブル + DataSetManagementエンティティ**: 主に在庫マスタ管理と日次処理で使用

## 1. テーブル定義の比較

### DataSetsテーブル
- **定義場所**: 
  - `/database/04_create_import_tables.sql`
  - `/database/CreateDatabase.sql`
- **主要カラム**:
  ```sql
  Id NVARCHAR(100) -- データセットID
  Name NVARCHAR(100) -- データセット名
  Description NVARCHAR(500) -- 説明
  ProcessType NVARCHAR(50) -- 処理種類
  Status NVARCHAR(20) -- ステータス
  RecordCount INT -- レコード数
  ErrorMessage NVARCHAR(MAX) -- エラーメッセージ
  FilePath NVARCHAR(500) -- ファイルパス
  JobDate DATETIME2 -- ジョブ日付
  CreatedAt DATETIME2 -- 作成日時
  UpdatedAt DATETIME2 -- 更新日時
  DepartmentCode NVARCHAR(50) -- 部門コード
  ```

### DataSetManagementテーブル
- **定義場所**: 
  - `/database/migrations/006_AddDataSetManagement.sql`
  - `/database/migrations/023_UpdateDataSetManagement.sql`
- **主要カラム**:
  ```sql
  DataSetId NVARCHAR(100) PRIMARY KEY
  JobDate DATE -- ジョブ日付
  ImportType NVARCHAR(20) -- インポートタイプ (INIT/IMPORT/CARRYOVER/MANUAL/UNKNOWN)
  ProcessType NVARCHAR(50) -- 処理タイプ
  RecordCount INT -- レコード数
  TotalRecordCount INT -- 総レコード数
  IsActive BIT -- アクティブフラグ
  IsArchived BIT -- アーカイブフラグ
  ParentDataSetId NVARCHAR(100) -- 親データセットID
  ImportedFiles NVARCHAR(MAX) -- インポートファイル一覧
  Department NVARCHAR(50) -- 部門
  CreatedAt DATETIME2 -- 作成日時
  CreatedBy NVARCHAR(50) -- 作成者
  UpdatedAt DATETIME2 -- 更新日時
  DeactivatedAt DATETIME2 -- 無効化日時
  DeactivatedBy NVARCHAR(50) -- 無効化実行者
  ArchivedAt DATETIME2 -- アーカイブ日時
  ArchivedBy NVARCHAR(50) -- アーカイブ実行者
  Notes NVARCHAR(500) -- 備考
  ```

## 2. エンティティクラスの比較

### DataSetエンティティ
- **ファイル**: `/src/InventorySystem.Core/Entities/DataSet.cs`
- **特徴**:
  - シンプルな構造
  - 互換性のための非推奨プロパティ（ImportedAt、DataSetType等）を含む
  - 主に伝票インポート処理で使用

### DataSetManagementエンティティ
- **ファイル**: `/src/InventorySystem.Core/Entities/DataSetManagement.cs`
- **特徴**:
  - より詳細な管理情報を持つ
  - 世代管理機能（ParentDataSetId）
  - アーカイブ・無効化機能
  - ProcessHistoriesのナビゲーションプロパティ
  - 一意なDataSetId生成メソッドを含む

## 3. 使用状況の分析

### DataSetsテーブル/DataSetエンティティの使用箇所
1. **伝票インポートサービス**:
   - `SalesVoucherImportService.cs` - 売上伝票インポート
   - `PurchaseVoucherImportService.cs` - 仕入伝票インポート
   - `InventoryAdjustmentImportService.cs` - 在庫調整インポート
   - `PreviousMonthInventoryImportService.cs` - 前月末在庫インポート

2. **マスタインポートサービス**:
   - `ProductMasterImportService.cs` - 商品マスタ
   - `CustomerMasterImportService.cs` - 得意先マスタ
   - `SupplierMasterImportService.cs` - 仕入先マスタ

3. **リポジトリ**:
   - `IDataSetRepository` インターフェース
   - `DataSetRepository` 実装

### DataSetManagementテーブル/DataSetManagementエンティティの使用箇所
1. **在庫管理サービス**:
   - `DataSetManager.cs` - データセット管理の中核
   - `InitialInventoryImportService.cs` - 初期在庫インポート
   - `DailyReportService.cs` - 商品日報処理（InitializeProcess経由）
   - `BatchProcessBase.cs` - バッチ処理基底クラス

2. **コマンド**:
   - `ImportInitialInventoryCommand.cs`
   - `ImportWithCarryoverCommand.cs`

3. **リポジトリ**:
   - `IDataSetManagementRepository` インターフェース
   - `DataSetManagementRepository` 実装

## 4. コマンド別使用状況

| コマンド | 使用テーブル | 備考 |
|---------|------------|------|
| import-folder | DataSets | 伝票・マスタCSVインポート |
| import-sales | DataSets | 売上伝票インポート |
| import-purchases | DataSets | 仕入伝票インポート |
| import-adjustments | DataSets | 在庫調整インポート |
| import-previous-inventory | DataSets | 前月末在庫インポート |
| import-initial-inventory | DataSetManagement | 初期在庫インポート |
| daily-report | DataSetManagement | InitializeProcess経由で使用 |
| import-with-carryover | DataSetManagement | 繰越込みインポート |

## 5. 問題点と推奨事項

### 現在の問題点
1. **概念の重複**: 2つのテーブルが似た目的で使用されている
2. **データの分散**: 同じデータセットの情報が2箇所に分かれる可能性
3. **保守性の低下**: どちらを使うべきか判断が必要
4. **一貫性の欠如**: ProcessTypeの値が統一されていない可能性

### 推奨事項

#### 短期的対応（推奨）
1. **現状維持と明確化**:
   - 各テーブルの役割を明確に文書化
   - DataSets: 伝票・マスタインポートの一時的な状態管理
   - DataSetManagement: 在庫マスタの世代管理と日次処理の履歴管理

2. **命名規則の統一**:
   - ProcessTypeの値を統一（例: SALES_IMPORT, PURCHASE_IMPORT等）
   - DataSetIdの生成ルールを統一

#### 長期的対応（将来の検討事項）
1. **テーブル統合**:
   - DataSetManagementテーブルに統一
   - DataSetsテーブルの機能をDataSetManagementに移行
   - 移行スクリプトの作成と段階的な実行

2. **インターフェースの統一**:
   - 単一のIDataSetServiceインターフェースを作成
   - 用途に応じた実装クラスの提供

## 6. 移行計画（将来実施時）

### Phase 1: 準備
1. DataSetManagementテーブルに不足カラムを追加
2. 両テーブルを同時に更新する移行期間を設定

### Phase 2: 移行
1. 新規処理はDataSetManagementのみ使用
2. 既存処理を段階的に移行
3. データ移行スクリプトの実行

### Phase 3: 完了
1. DataSetsテーブルの削除
2. 関連コードのクリーンアップ

## 結論

現在のダブルスタンダード状態は、システムの進化の過程で生じたものと考えられます。短期的には現状を維持しつつ、役割を明確化することで運用は可能です。ただし、長期的にはDataSetManagementテーブルへの統一を検討することで、システムの保守性と一貫性を向上させることができます。