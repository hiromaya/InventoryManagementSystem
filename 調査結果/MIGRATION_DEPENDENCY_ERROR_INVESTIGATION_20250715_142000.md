# マイグレーション依存関係エラー調査報告書

**調査日時**: 2025年7月15日 14:20:00  
**調査者**: Claude Code  
**対象**: init-database --forceコマンドのマイグレーション実行エラー  

## 🔍 問題の概要

`init-database --force`コマンドでマイグレーションを実行する際、多数のエラーが発生している。
主な原因は、**マイグレーションファイル間の依存関係の順序問題**と**テーブル/カラムの重複作成問題**。

## 📊 エラー分析結果

### 1. 依存関係エラー (テーブル/カラムが存在しない)

| マイグレーション | エラー内容 | 依存するテーブル/カラム |
|-----------------|-----------|------------------------|
| `005_AddDailyCloseProtectionColumns.sql` | DailyCloseManagementテーブルが存在しない | DailyCloseManagement |
| `007_AddDeactivationIndexes.sql` | PreviousMonthQuantityカラムが存在しない | InventoryMaster.PreviousMonthQuantity |
| `013_AddImportTypeToInventoryMaster.sql` | PreviousMonthQuantityカラムが存在しない | InventoryMaster.PreviousMonthQuantity |
| `016_AddMonthlyFieldsToCpInventory.sql` | CP_InventoryMasterテーブルが存在しない | CP_InventoryMaster |
| `018_FixExistingCpInventoryProductCategories.sql` | ProductMasterテーブルが存在しない | ProductMaster |
| `019_Fix_DepartmentCode_Size.sql` | DepartmentCodeカラムが存在しない | CpInventoryMaster.DepartmentCode |
| `022_AddLastTransactionDates.sql` | LastSalesDateカラムが存在しない | InventoryMaster.LastSalesDate |
| `023_UpdateDatasetManagement.sql` | ProcessType,TotalRecordCountカラムが存在しない | DatasetManagement |

### 2. 重複作成エラー (カラムが既に存在)

| マイグレーション | エラー内容 | 重複カラム |
|-----------------|-----------|------------|
| `011_AddDataSetManagement.sql` | IsActiveカラムが重複指定 | InventoryMaster.IsActive |
| `015_AddMonthlyColumnsToCpInventoryMaster.sql` | MonthlySalesQuantityカラムが重複指定 | CpInventoryMaster.MonthlySalesQuantity |

## 🔄 マイグレーション実行順序の問題

### 現在の実行順序（ファイル名順）
```
000_CreateMigrationHistory.sql       ✓ 成功
005_AddDailyCloseProtectionColumns   ❌ 失敗 (DailyCloseManagementなし)
006_AddDataSetManagement.sql         ✓ 成功
007_AddDeactivationIndexes.sql       ❌ 失敗 (PreviousMonthQuantityなし)
008_AddUnmatchOptimizationIndexes    ✓ 成功
009_CreateInitialInventoryStagingTable ✓ 成功
010_AddPersonInChargeAndAveragePrice ✓ 成功
011_AddDataSetManagement.sql         ❌ 失敗 (IsActive重複)
012_AddGrossProfitColumnToSalesVouchers ✓ 成功
013_AddImportTypeToInventoryMaster    ❌ 失敗 (PreviousMonthQuantityなし)
014_AddMissingColumnsToInventoryMaster ✓ 成功 (PreviousMonthQuantity作成)
015_AddMonthlyColumnsToCpInventoryMaster ❌ 失敗 (重複)
016_AddMonthlyFieldsToCpInventory     ❌ 失敗 (CP_InventoryMasterなし)
017_Cleanup_Duplicate_InventoryMaster ✓ 成功
018_FixExistingCpInventoryProductCategories ❌ 失敗 (ProductMasterなし)
019_Fix_DepartmentCode_Size.sql       ❌ 失敗 (DepartmentCodeなし)
020_Fix_MergeInventoryMaster_OutputClause ✓ 成功
021_VerifyInventoryMasterSchema       ✓ 成功
022_AddLastTransactionDates.sql       ❌ 失敗 (LastSalesDateなし)
023_UpdateDatasetManagement.sql       ❌ 失敗 (カラムなし)
```

## 🧩 依存関係マッピング

### 必要なテーブル作成順序
1. **基本テーブル** (CreateDatabase.sql)
   - InventoryMaster (基本構造)
   - CpInventoryMaster (基本構造)
   - SalesVouchers, PurchaseVouchers, InventoryAdjustments
   - DataSets

2. **管理テーブル** (作成が必要)
   - DailyCloseManagement → 005で参照
   - ProcessHistory → 005で参照
   - ProductMaster → 018で参照

3. **カラム追加の依存関係**
   ```
   InventoryMaster:
   - IsActive → 006で作成 → 011で重複エラー
   - PreviousMonthQuantity → 014で作成 → 007,013で先行参照エラー
   - LastSalesDate → 作成されていない → 022で参照エラー
   
   CpInventoryMaster:
   - DepartmentCode → 作成されていない → 019で参照エラー
   - MonthlySalesQuantity → 015で重複作成エラー
   
   DatasetManagement:
   - ProcessType, TotalRecordCount → 作成されていない → 023で参照エラー
   ```

## 🎯 根本原因

### 1. CreateDatabase.sqlの不完全性
- DailyCloseManagement, ProcessHistory, ProductMasterテーブルが含まれていない
- DatabaseInitializationService.csの古いテーブル定義にこれらが含まれていたが、CreateDatabase.sqlには反映されていない

### 2. マイグレーション間の循環依存
- 006と011で同じカラム（IsActive）を追加しようとしている
- 014でカラムを作成するが、007,013がそれより前に実行されて失敗

### 3. マイグレーション番号の不整合
- 古いマイグレーション（011）が新しいマイグレーション（006）と重複している
- 命名規則統一時に重複ファイルが残っている

## 💡 解決方針

### 短期的解決策（緊急）
1. **エラーのあるマイグレーションを一時的に無効化**
   - 失敗するマイグレーションファイルを別フォルダに移動
   - 最低限動作する状態を確保

### 中期的解決策（推奨）
1. **CreateDatabase.sqlの拡張**
   - DatabaseInitializationService.csの_tableDefinitionsからテーブル定義をCreateDatabase.sqlに統合
   - DailyCloseManagement, ProcessHistory等を追加

2. **マイグレーション統合・整理**
   - 重複するマイグレーション（006と011）を統合
   - 依存関係に基づく正しい番号付け

3. **段階的マイグレーション**
   ```
   001-005: テーブル作成系
   006-010: 基本カラム追加
   011-015: 機能拡張カラム
   016-020: インデックス・制約
   021-025: データ修正・最適化
   ```

### 長期的解決策（理想）
1. **マイグレーション自動生成ツール**の導入
2. **依存関係チェック機能**の実装
3. **テストデータでのマイグレーション検証**の自動化

## 📋 次のアクション

### 優先度: 高
1. ✅ CreateDatabase.sqlにDailyCloseManagement, ProcessHistoryテーブルを追加
2. ✅ 重複マイグレーション（011）を削除または統合
3. ✅ 依存関係エラーのあるマイグレーションの修正

### 優先度: 中
1. ⏳ マイグレーション番号の全面見直し
2. ⏳ テストケースの作成
3. ⏳ ドキュメント化

## 🔬 技術的詳細

### DatabaseInitializationService.csとの相違点
- 旧サービスには以下テーブル定義が含まれていた：
  ```csharp
  ["ProcessHistory"] = "CREATE TABLE ProcessHistory (...)"
  ["DatasetManagement"] = "CREATE TABLE DatasetManagement (...)" 
  ["DailyCloseManagement"] = "CREATE TABLE DailyCloseManagement (...)"
  ```
- これらをCreateDatabase.sqlに移行する必要がある

### マイグレーション冪等性の問題
- 多くのマイグレーションで`IF NOT EXISTS`チェックが不完全
- カラム追加時の存在チェックが不十分
- ロールバック機能が実装されていない

## 📊 影響範囲

### 影響を受ける機能
- ✅ 基本在庫管理機能: 動作可能（基本テーブルは作成済み）
- ❌ 日次終了処理: DailyCloseManagementテーブル不足で動作不可
- ❌ データセット管理: 一部カラム不足で機能制限
- ❌ アンマッチリスト: インデックス不足で性能劣化の可能性

### リスク評価
- **高**: 本番環境での日次終了処理エラー
- **中**: 性能劣化によるユーザー体験悪化  
- **低**: データ整合性への直接的影響（基本機能は動作）

---

**結論**: マイグレーション依存関係の根本的見直しが必要。短期的にはエラーマイグレーションの無効化、中期的にはCreateDatabase.sqlの拡張とマイグレーション統合が必要。