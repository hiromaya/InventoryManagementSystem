# DataSetsテーブル使用状況調査結果

## 調査サマリー
- **調査日時**: 2025-07-22 18:00:00
- **対象コマンド数**: 3
- **DataSets使用コマンド数**: 1 (import-folderコマンドのみ)
- **修正必要箇所**: 1箇所
- **緊急度**: 中（DataSetManagementへの移行は完了済み）

## 重要な発見
1. **DataSetsテーブルは既に廃止予定**：マイグレーション(`036_MigrateDataSetsToDataSetManagement.sql`)によりDataSetManagementテーブルに統合済み
2. **新しいシステムはDataSetManagement中心**：Program.csでは`DataSetManagementService`を使用
3. **DataSetRepositoryは旧システムの互換性のため残存**

---

## コマンド別詳細調査

### 1. import-folderコマンド

#### 実装場所
- **ファイル**: `/src/InventorySystem.Console/Program.cs`
- **メソッド**: `ExecuteImportFromFolderAsync` (2016行～)
- **呼び出し**: `case "import-folder":` (424行)

#### 使用サービス
- **直接使用**: 各種ImportServiceクラス
- **間接使用**: `IDataSetService` (DataSetManagementServiceの実装)
- **DI登録**: Program.cs 223行で`DataSetManagementService`を登録

#### DataSets使用箇所
**✅ 修正済み**: Program.cs内のDataSets参照は以下の箇所のみ：
```csharp
// Line 1442: テーブル存在確認でのみ使用（統計情報用）
string[] tables = { "InventoryMaster", "CpInventoryMaster", "SalesVouchers", "PurchaseVouchers", "InventoryAdjustments", "DataSets" };
```

#### 修正の必要性
**🟡 低優先度**: テーブル存在確認の配列から"DataSets"を削除するか、"DataSetManagement"に変更することを推奨

### 2. create-unmatch-listコマンド（unmatch-list）

#### 実装場所
- **ファイル**: `/src/InventorySystem.Console/Program.cs`
- **メソッド**: `ExecuteUnmatchListAsync` (527行～)
- **呼び出し**: `case "unmatch-list":` (361行)

#### 使用サービス
- **メインサービス**: `IUnmatchListService` → `UnmatchListService`
- **レポートサービス**: `IUnmatchListReportService`

#### DataSets使用箇所
**✅ 修正不要**: 
- UnmatchListServiceは独自にDataSetIdを生成・管理
- DataSetRepository/DataSetsテーブルを**直接使用していない**
- DataSetIdは各伝票リポジトリから取得するか、新規生成

```csharp
// UnmatchListService.cs 84-105行: 既存DataSetId検索ロジック
existingDataSetId = await _salesVoucherRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
// DataSetsテーブルには依存しない設計
```

#### 修正の必要性
**✅ 修正不要**: DataSetsテーブル非依存の実装

### 3. create-daily-reportコマンド（daily-report）

#### 実装場所
- **ファイル**: `/src/InventorySystem.Console/Program.cs`
- **メソッド**: `ExecuteDailyReportAsync` (895行～)
- **呼び出し**: `case "daily-report":` (364行)

#### 使用サービス
- **メインサービス**: `IDailyReportService` → `DailyReportService`
- **データセット管理**: `IDataSetManager` → `DataSetManager`
- **レポートサービス**: FastReportまたはPlaceholderサービス

#### DataSets使用箇所
**✅ 修正不要**:
- DailyReportServiceは`IDataSetManager`経由でDataSetManagementテーブルを使用
- DataSetsテーブルを**直接使用していない**

```csharp
// DailyReportService.cs 25行: DataSetManager注入
public DailyReportService(IDateValidationService dateValidator, IDataSetManager dataSetManager, ...)

// DailyReportService.cs 55行: DataSetManager使用
context = await InitializeProcess(reportDate, "DAILY_REPORT", null, executedBy);
```

#### 修正の必要性
**✅ 修正不要**: DataSetManagement移行済み

---

## 修正推奨事項

### 1. 緊急修正不要
- **理由**: 主要なコマンドは全てDataSetManagementテーブルを使用
- **現状**: DataSetsテーブルは参照のみで、実害なし

### 2. 推奨修正（優先度：低）
```csharp
// Program.cs 1442行の修正案
// 修正前
string[] tables = { "InventoryMaster", "CpInventoryMaster", "SalesVouchers", "PurchaseVouchers", "InventoryAdjustments", "DataSets" };

// 修正後
string[] tables = { "InventoryMaster", "CpInventoryMaster", "SalesVouchers", "PurchaseVouchers", "InventoryAdjustments", "DataSetManagement" };
```

### 3. DataSetRepositoryの段階的廃止
- **Phase 1**: 新規開発でのDataSetRepository使用禁止
- **Phase 2**: 既存コードのDataSetManagementRepository移行
- **Phase 3**: DataSetRepositoryクラスの削除

---

## 影響範囲分析

### 1. 実システムへの影響
**✅ 影響なし**: 
- import-folderコマンドは正常動作（DataSetManagementService使用）
- アンマッチリストは独立動作（伝票テーブル直接参照）
- 商品日報はDataSetManager使用（DataSetManagement対応済み）

### 2. 開発者への影響
**⚠️ 注意事項**:
- DataSetRepository使用時は非推奨警告の追加を検討
- 新規開発時はDataSetManagementRepositoryを使用

### 3. データ整合性
**✅ 問題なし**:
- 036_MigrateDataSetsToDataSetManagement.sqlで完全移行済み
- 外部キー制約もDataSetManagementに向け直し済み

---

## 次のアクション

### 1. 即座に実行
- **なし**（緊急性なし）

### 2. 近い将来（1-2週間以内）
- [ ] Program.csのテーブル一覧配列修正
- [ ] DataSetRepository非推奨化の検討

### 3. 中長期（1-3ヶ月以内）
- [ ] DataSetsテーブル削除実行（999_DropDataSetsTable.sql）
- [ ] DataSetRepositoryクラス削除
- [ ] IDataSetRepositoryインターフェース削除

---

## 結論

**DataSetsテーブルの使用問題は実質的に解決済み**です。主要な3つのコマンドは全てDataSetManagementテーブル中心の実装に移行しており、DataSetsテーブルに依存していません。

現在の状況：
- ✅ import-folder: DataSetManagementService使用
- ✅ unmatch-list: 独立したDataSetId管理
- ✅ daily-report: DataSetManager（DataSetManagement対応）使用

軽微な修正推奨事項はありますが、システムの安定稼働に支障はありません。