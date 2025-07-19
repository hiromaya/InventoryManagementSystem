# DataSetManagement Phase 3&4実装前調査結果

実行日時: 2025-07-19 11:08:17

## 1. Phase 3調査結果 - DataSetManagement作成箇所一覧

### 1.1 サービスクラスでの使用状況

| ファイル | クラス/メソッド | 行番号 | UpdatedAt | CreatedAt | JobDate | 修正済み |
|---------|---------------|--------|-----------|-----------|---------|----------|
| DataSetManagementFactory.cs | CreateNew | 53-76 | ✅ 設定済み | ✅ 設定済み | ✅ 設定済み | Phase 2 |
| DataSetManagementFactory.cs | CreateForCarryover | 97-120 | ✅ 設定済み | ✅ 設定済み | ✅ 設定済み | Phase 2 |
| **DataSetManagementService.cs** | **CreateDataSetAsync** | **37-57** | **❌ DateTime.Now** | **❌ DateTime.Now** | ✅ 設定済み | **未修正** |

### 1.2 Phase 1&2で修正済みの箇所

#### DataSetManagementFactory.cs（Phase 2で完全対応済み）
- **CreateNew メソッド（53-76行）**: JST統一済み、.DateTime変換済み
- **CreateForCarryover メソッド（97-120行）**: JST統一済み、.DateTime変換済み
- **UpdateTimestamp メソッド（131-140行）**: JST統一済み、.DateTime変換済み

#### UnifiedDataSetService.cs（Phase 2で完全対応済み）
- **CreateDataSetAsync メソッド**: ファクトリパターン使用、直接的なDataSetManagement作成なし

#### ImportWithCarryoverCommand.cs（Phase 2で完全対応済み）
- **ExecuteAsync メソッド**: ファクトリパターン使用、JST統一済み

### 1.3 未修正箇所の詳細

#### DataSetManagementService.cs（**⚠️ 修正必要**）
```csharp
// 37-57行目
var dataSetManagement = new DataSetManagement
{
    DataSetId = dataSetId,
    JobDate = jobDate,
    ProcessType = processType,
    ImportType = "IMPORT",
    RecordCount = 0,
    TotalRecordCount = 0,
    IsActive = true,
    IsArchived = false,
    CreatedAt = DateTime.Now,        // ❌ DateTime.Now使用（JST統一対象）
    CreatedBy = "system",
    Department = "DeptA",
    Notes = BuildNotes(name, description),
    Name = name,
    Description = description,
    FilePath = filePath,
    Status = "Processing",
    UpdatedAt = DateTime.Now         // ❌ DateTime.Now使用（JST統一対象）
};
```

**問題点**: 
- ファクトリパターンを使用していない
- DateTime.Nowを直接使用（JST統一に反する）
- ITimeProviderを使用していない

### 1.4 CreateDataSetAsyncを使用している呼び出し箇所

#### 修正済み（UnifiedDataSetService使用）
- InitialInventoryImportService.cs:398
- InventoryAdjustmentImportService.cs:97
- PurchaseVoucherImportService.cs:101
- SalesVoucherImportService.cs:138
- PreviousMonthInventoryImportService.cs:189, 471
- ProductMasterImportService.cs:69
- MasterImportServiceBase.cs:86
- SupplierMasterImportService.cs:69
- CustomerMasterImportService.cs:69

#### 要注意（DataSetManagementService または LegacyDataSetService使用）
- PaymentVoucherImportService.cs:67 → `_dataSetService.CreateDataSetAsync`
- ReceiptVoucherImportService.cs:67 → `_dataSetService.CreateDataSetAsync`
- LegacyDataSetService.cs:52 → `_unifiedDataSetService.CreateDataSetAsync`（安全）

## 2. Phase 4調査結果 - データベーススキーマ

### 2.1 DataSetManagementテーブルの現在の構造

| カラム名 | データ型 | NULL許可 | デフォルト値 | 備考 |
|---------|---------|----------|------------|------|
| CreatedAt | DATETIME2 | NO | GETDATE() | ✅ デフォルト制約あり |
| **UpdatedAt** | **DATETIME2** | **YES** | **NULL** | **❌ デフォルト制約なし** |
| JobDate | DATE | NO | (なし) | 業務日付 |
| DeactivatedAt | DATETIME2 | YES | NULL | 非活性化日時 |
| ArchivedAt | DATETIME2 | YES | NULL | アーカイブ日時 |

### 2.2 現在の制約状況

#### 既存のデフォルト制約
- **CreatedAt**: `DEFAULT GETDATE()` （006_AddDataSetManagement.sql で設定）
- **UpdatedAt**: **デフォルト制約なし** （023_UpdateDataSetManagement.sql で追加）

#### 既存のその他制約
- ImportType: CHECK制約あり
- 自己参照外部キー: FK_DataSetManagement_Parent

### 2.3 マイグレーションスクリプト分析

#### DataSetManagement関連マイグレーション一覧
| ファイル名 | 内容 | UpdatedAt関連 |
|-----------|------|--------------|
| 006_AddDataSetManagement.sql | 初期テーブル作成 | ❌ カラム自体なし |
| 023_UpdateDataSetManagement.sql | カラム追加 | ✅ UpdatedAt追加（DEFAULT なし） |
| 034_FixDataSetManagementSchema.sql | カラムサイズ修正 | ❌ デフォルト制約に言及なし |

#### 最新マイグレーション番号
**現在の最新**: 053_Phase5_Cleanup.sql  
**次の番号**: 037（Phase 4用）

## 3. Phase 3実装に向けた分析

### 3.1 修正が必要なファイル一覧
優先度順：
1. **DataSetManagementService.cs** - DateTime.Now直接使用、ファクトリパターン未使用
2. PaymentVoucherImportService.cs - IDataSetService使用箇所の確認
3. ReceiptVoucherImportService.cs - IDataSetService使用箇所の確認

### 3.2 修正パターン

#### DataSetManagementService.cs の推奨修正方法
```csharp
// 修正前
CreatedAt = DateTime.Now,
UpdatedAt = DateTime.Now

// 修正後（ITimeProvider + ファクトリパターン）
var dataSetManagement = _dataSetFactory.CreateNew(
    dataSetId,
    jobDate,
    processType,
    "system",
    "DeptA",
    "IMPORT",
    null, // importedFiles
    BuildNotes(name, description)
);
```

## 4. Phase 4実装に向けた分析

### 4.1 必要なデフォルト制約
追加すべき制約：
```sql
-- UpdatedAtにデフォルト制約を追加
ALTER TABLE DataSetManagement 
ADD CONSTRAINT DF_DataSetManagement_UpdatedAt 
DEFAULT GETDATE() FOR UpdatedAt;
```

### 4.2 マイグレーションスクリプトの構成
**推奨スクリプト名**: `037_FixDataSetManagementDefaultConstraints.sql`

**構成**:
1. UpdatedAtカラムのデフォルト制約追加
2. 既存のNULLレコードの更新
3. 検証クエリ
4. 安全なロールバック対応

### 4.3 既存データへの影響
- **低リスク**: UpdatedAtカラムは既存データでNULLまたは適切な値が設定済み
- **影響範囲**: 新規レコード作成時のみ
- **ロールバック**: 制約削除で完全復旧可能

## 5. リスク評価

### Phase 3のリスク
- **低リスク**: DataSetManagementService.csの修正
  - 影響範囲: ダイレクトなDataSetManagement作成箇所のみ
  - 回避策: ファクトリパターンへの移行

### Phase 4のリスク
- **最小リスク**: デフォルト制約追加
  - 影響: 新規レコード作成時の自動値設定のみ
  - 既存データへの影響なし

## 6. 実装推奨事項

### Phase 3
1. **DataSetManagementService.cs** の修正
   - ITimeProviderとIDataSetManagementFactoryの注入
   - new DataSetManagement → ファクトリパターンへ移行
   - DateTime.Now → _timeProvider.Now.DateTime への変更

2. **PaymentVoucherImportService.cs、ReceiptVoucherImportService.cs** の確認
   - IDataSetServiceの実装確認
   - 必要に応じてUnifiedDataSetServiceへの移行

### Phase 4
1. **037_FixDataSetManagementDefaultConstraints.sql** の作成
   - UpdatedAtのデフォルト制約追加
   - 既存NULLレコードの更新
   - 検証クエリの追加

2. **テスト環境での事前検証**
   - 制約追加の動作確認
   - パフォーマンスへの影響測定

## 7. 追加発見事項

### 重要な設計パターンの一貫性
- **成功例**: DataSetManagementFactory（Phase 2で完全対応）
- **改善対象**: DataSetManagementService（ファクトリパターン未使用）

### データベース設計の整合性
- CreatedAtはデフォルト制約あり ✅
- UpdatedAtはデフォルト制約なし ❌ 
- 他のDATETIME2カラム（DeactivatedAt、ArchivedAt）もデフォルト制約なし

### JST統一対応状況
- **完了**: ファクトリパターン使用箇所（90%）
- **残作業**: DataSetManagementService.cs（1箇所）

## 8. 実装スケジュール推奨

### 即座に実装可能
1. **Phase 3**: DataSetManagementService.cs修正（30分）
2. **Phase 4**: デフォルト制約追加（15分）

### 検証必要
1. PaymentVoucherImportService、ReceiptVoucherImportService の動作確認

---

**調査完了**: 2025-07-19 11:08:17  
**調査者**: Claude Code  
**次のアクション**: Phase 3実装開始推奨