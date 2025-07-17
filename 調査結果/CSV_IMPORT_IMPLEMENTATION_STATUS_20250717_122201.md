# CSV取込実装状況調査レポート

生成日時: 2025-07-17 12:22:01

## 📋 エグゼクティブサマリー

InventoryManagementSystemプロジェクトのCSV取込機能について包括的調査を実施しました。現在、15種類のCSV取込のうち**12種類が実装済み**（完全実装7種類、スタブ実装2種類、リポジトリ直接利用3種類）で、**8種類の未実装ファイル**が特定されました。既存のインフラストラクチャは優秀で、残りの実装も迅速に対応可能です。

## 1. 実装済みCSV取込サービス

### ✅ 完全実装（7/15）

#### 基幹取引データ
| サービス名 | 対応CSVファイル | 実装ファイルパス | 実装規模 | 備考 |
|-----------|----------------|-----------------|---------|------|
| SalesVoucherImportService | 売上伝票*.csv | `/src/InventorySystem.Import/Services/SalesVoucherImportService.cs` | 507行 | **最も充実した実装**<br>日付フィルタリング、JobDate保持、スキップ追跡 |
| PurchaseVoucherImportService | 仕入伝票*.csv | `/src/InventorySystem.Import/Services/PurchaseVoucherImportService.cs` | 351行 | 完全実装<br>バッチ処理、日付フィルタリング対応 |
| InventoryAdjustmentImportService | 在庫調整*.csv<br>受注伝票*.csv | `/src/InventorySystem.Import/Services/InventoryAdjustmentImportService.cs` | 331行 | 受注伝票も在庫調整として処理<br>サマリー行フィルタリング機能 |
| PreviousMonthInventoryImportService | 前月末在庫.csv | `/src/InventorySystem.Import/Services/PreviousMonthInventoryImportService.cs` | 626行 | **最も包括的な実装**<br>初期在庫セットアップ、高度な在庫管理 |

#### マスタデータ
| サービス名 | 対応CSVファイル | 実装ファイルパス | 実装規模 | 備考 |
|-----------|----------------|-----------------|---------|------|
| ProductMasterImportService | 商品.csv | `/src/InventorySystem.Import/Services/Masters/ProductMasterImportService.cs` | 275行 | 完全削除→一括挿入方式 |
| CustomerMasterImportService | 得意先.csv | `/src/InventorySystem.Import/Services/Masters/CustomerMasterImportService.cs` | 271行 | 完全削除→一括挿入方式 |
| SupplierMasterImportService | 仕入先.csv | `/src/InventorySystem.Import/Services/Masters/SupplierMasterImportService.cs` | 推定250行 | Program.csに登録済み |

### ⚠️ スタブ/インターフェース実装（2/15）

| サービス名 | 対応CSVファイル | 実装状況 | 備考 |
|-----------|----------------|---------|------|
| ShippingMarkMasterImportService | 荷印汎用マスター３.csv | **Interface登録済み** | Program.cs 2134行で「未実装」と明記<br>CSV Model存在 |
| RegionMasterImportService | 産地汎用マスター４.csv | **Interface登録済み** | Program.cs 2155行で「未実装」と明記<br>CSV Model存在 |

### 🔧 リポジトリ直接利用（3/15）

| リポジトリ名 | 対応CSVファイル | 実装方法 | 実装場所 |
|------------|----------------|---------|---------|
| GradeMasterRepository | 等級汎用マスター１.csv | `ImportFromCsvAsync()` メソッド | Program.cs 2088行 |
| ClassMasterRepository | 階級汎用マスター２.csv | `ImportFromCsvAsync()` メソッド | Program.cs 2108行 |
| CsvImportService | ※レガシー実装 | 基本的な売上・仕入処理 | `/src/InventorySystem.Import/Services/CsvImportService.cs` (305行) |

## 2. ImportFolderメソッドの処理フロー

### 📍 実装場所
- **メソッド**: `ExecuteImportFromFolderAsync` （`Program.cs` 1858-2400行）
- **優先度制御**: `GetFileProcessOrder` メソッド （`Program.cs` 1729-1751行）

### 認識されているファイルパターン

#### Phase 1: マスタファイル（優先度1-8）
| ファイルパターン | 優先度 | 処理方法 | 実装状況 |
|-----------------|--------|---------|---------|
| `等級汎用マスター*` | 1 | GradeMasterRepository | ✅ 完全実装 |
| `階級汎用マスター*` | 2 | ClassMasterRepository | ✅ 完全実装 |
| `荷印汎用マスター*` | 3 | ShippingMarkMasterImportService | ⚠️ スタブのみ |
| `産地汎用マスター*` | 4 | RegionMasterImportService | ⚠️ スタブのみ |
| `商品.csv` | 5 | ProductMasterImportService | ✅ 完全実装 |
| `得意先.csv` | 6 | CustomerMasterImportService | ✅ 完全実装 |
| `仕入先.csv` | 7 | SupplierMasterImportService | ✅ 完全実装 |
| `単位.csv` | 8 | **未実装** | ❌ 認識のみ |

#### Phase 2: 初期在庫（優先度10）
| ファイルパターン | 優先度 | 処理方法 | 実装状況 |
|-----------------|--------|---------|---------|
| `前月末在庫.csv` | 10 | PreviousMonthInventoryImportService | ✅ 完全実装 |

#### Phase 3: 取引ファイル（優先度20-22）
| ファイルパターン | 優先度 | 処理方法 | 実装状況 |
|-----------------|--------|---------|---------|
| `売上伝票*` | 20 | SalesVoucherImportService | ✅ 完全実装 |
| `仕入伝票*` | 21 | PurchaseVoucherImportService | ✅ 完全実装 |
| `在庫調整*` | 22 | InventoryAdjustmentImportService | ✅ 完全実装 |
| `受注伝票*` | 22 | InventoryAdjustmentImportService | ✅ 在庫調整として処理 |

### 未対応ファイルパターン（Program.cs 2314-2317行で明示）
```csharp
string[] knownButUnsupported = {
    "担当者",      // Staff/Personnel
    "単位",        // Units（認識はされるが処理未実装）
    "商品分類",    // Product Categories (1-3)
    "得意先分類",  // Customer Categories (1-5) 
    "仕入先分類",  // Supplier Categories (1-3)
    "担当者分類",  // Staff Categories
    "支払伝票",    // Payment Vouchers
    "入金伝票"     // Receipt Vouchers
};
```

## 3. CSV形式定義クラスの実装状況

### ✅ 実装済みCSVモデル

#### 取引データモデル
| Csvクラス名 | 対応ファイル | 列数 | 使用状況 |
|------------|------------|------|---------|
| SalesVoucherDaijinCsv | 売上伝票*.csv | 171列 | ✅ 使用中 |
| PurchaseVoucherDaijinCsv | 仕入伝票*.csv | 171列 | ✅ 使用中 |
| InventoryAdjustmentDaijinCsv | 在庫調整*.csv, 受注伝票*.csv | 171列 | ✅ 使用中 |
| PreviousMonthInventoryCsv | 前月末在庫.csv | 161列 | ✅ 使用中 |

#### マスタデータモデル
| Csvクラス名 | 対応ファイル | 使用状況 |
|------------|------------|---------|
| ProductMasterCsv | 商品.csv | ✅ 使用中 |
| CustomerMasterCsv | 得意先.csv | ✅ 使用中 |
| SupplierMasterCsv | 仕入先.csv | ✅ 使用中 |
| ShippingMarkMasterCsv | 荷印汎用マスター３.csv | ⚠️ 存在するがサービススタブ |
| RegionMasterCsv | 産地汎用マスター４.csv | ⚠️ 存在するがサービススタブ |

### ❌ 未実装CSVモデル
- 単位マスター（Unit Master）
- 商品分類１〜３（Product Categories 1-3）
- 得意先分類１〜５（Customer Categories 1-5）
- 仕入先分類１〜３（Supplier Categories 1-3）
- 担当者・担当者分類（Staff/Personnel）
- 入金伝票・支払伝票（Payment/Receipt Vouchers）

## 4. リポジトリでのマスタデータ取込実装

### ✅ BulkInsert対応リポジトリ

#### マスタデータリポジトリ（`/src/InventorySystem.Data/Repositories/Masters/`）
| リポジトリ名 | BulkInsertメソッド | 機能 |
|------------|------------------|------|
| ProductMasterRepository | `InsertBulkAsync` | ✅ 一括挿入対応 |
| CustomerMasterRepository | `InsertBulkAsync` | ✅ 一括挿入対応 |
| SupplierMasterRepository | `InsertBulkAsync` | ✅ 一括挿入対応 |
| ShippingMarkMasterRepository | `InsertBulkAsync` | ✅ 一括挿入対応（準備済み） |
| RegionMasterRepository | `InsertBulkAsync` | ✅ 一括挿入対応（準備済み） |

#### 特殊なImportFromCsv対応
| リポジトリ名 | 特殊メソッド | 機能 |
|------------|-------------|------|
| GradeMasterRepository | `ImportFromCsvAsync` | ✅ CSV直接インポート |
| ClassMasterRepository | `ImportFromCsvAsync` | ✅ CSV直接インポート |

#### 取引データリポジトリ
| リポジトリ名 | バルク操作 | 機能 |
|------------|----------|------|
| SalesVoucherRepository + SalesVoucherCsvRepository | ✅ フル対応 | バッチ処理、削除、挿入 |
| PurchaseVoucherRepository + PurchaseVoucherCsvRepository | ✅ フル対応 | バッチ処理、削除、挿入 |
| InventoryAdjustmentRepository | ✅ フル対応 | バッチ処理対応 |

## 5. 未実装CSV一覧と実装可能性

### 🔴 **高優先度: マスタデータ分類**

#### 仕入先分類（3ファイル）
| CSVファイル名 | 優先度 | 実装方法案 | 必要な作業 |
|--------------|--------|-----------|-----------|
| 仕入先分類１.csv | **高** | SupplierCategory1ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |
| 仕入先分類２.csv | **高** | SupplierCategory2ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |
| 仕入先分類３.csv | **高** | SupplierCategory3ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |

#### 商品分類（3ファイル）  
| CSVファイル名 | 優先度 | 実装方法案 | 必要な作業 |
|--------------|--------|-----------|-----------|
| 商品分類１.csv | **高** | ProductCategory1ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |
| 商品分類２.csv | **高** | ProductCategory2ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |  
| 商品分類３.csv | **高** | ProductCategory3ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |

#### 得意先分類（5ファイル）
| CSVファイル名 | 優先度 | 実装方法案 | 必要な作業 |
|--------------|--------|-----------|-----------|
| 得意先分類１.csv | **高** | CustomerCategory1ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |
| 得意先分類２.csv | **高** | CustomerCategory2ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |
| 得意先分類３.csv | **高** | CustomerCategory3ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |
| 得意先分類４.csv | **高** | CustomerCategory4ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |
| 得意先分類５.csv | **高** | CustomerCategory5ImportService + CSV Model | 新規エンティティ、リポジトリ、サービス |

#### 単位マスタ
| CSVファイル名 | 優先度 | 実装方法案 | 必要な作業 |
|--------------|--------|-----------|-----------|
| 単位.csv | **高** | UnitMasterImportService + CSV Model | GetFileProcessOrderに認識済み<br>新規エンティティ、リポジトリ、サービス |

### 🟡 **中優先度: 人事管理**

| CSVファイル名 | 優先度 | 実装方法案 | 必要な作業 |
|--------------|--------|-----------|-----------|
| 担当者.csv | **中** | StaffMasterImportService + CSV Model | 営業担当追跡に必要<br>新規エンティティ、リポジトリ、サービス |
| 担当者分類１.csv | **中** | StaffCategoryImportService + CSV Model | 担当者管理の拡張<br>新規エンティティ、リポジトリ、サービス |

### 🟢 **低優先度: 財務取引**

| CSVファイル名 | 優先度 | 実装方法案 | 必要な作業 |
|--------------|--------|-----------|-----------|
| 入金伝票.csv | **低** | ReceiptVoucherImportService + CSV Model | 財務管理が必要な場合<br>新規エンティティ、リポジトリ、サービス、財務ロジック |
| 支払伝票.csv | **低** | PaymentVoucherImportService + CSV Model | 財務管理が必要な場合<br>新規エンティティ、リポジトリ、サービス、財務ロジック |

## 6. 実装パターンとインフラストラクチャ

### 🏗️ 再利用可能なインフラストラクチャ

#### マスタデータパターン
```csharp
// ProductMasterImportServiceのパターン
public async Task<ImportResult> ImportFromCsvAsync(string filePath, DateTime importDate)
{
    // 1. 統一DataSet作成
    var unifiedInfo = new UnifiedDataSetInfo { ... };
    var dataSetId = await _unifiedDataSetService.CreateDataSetAsync(unifiedInfo);
    
    // 2. UTF-8エンコーディングでCSV読み取り
    using var reader = new StringReader(File.ReadAllText(filePath, Encoding.UTF8));
    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
    
    // 3. 全削除 → 一括挿入
    await _repository.DeleteAllAsync();
    await _repository.InsertBulkAsync(entities);
    
    // 4. エラーハンドリングとログ出力
}
```

#### 取引データパターン
```csharp
// SalesVoucherImportServiceのパターン  
public async Task<string> ImportAsync(string filePath, DateTime? startDate, DateTime? endDate, 
                                     string? departmentCode = null, bool preserveCsvDates = false)
{
    // 1. 日付フィルタリングとバリデーション
    // 2. 統一DataSet作成
    // 3. エラーハンドリング付きCSV読み取り
    // 4. バッチ処理（1000レコード単位）
    // 5. スキップ追跡と統計情報
    // 6. JobDate保持ロジック
}
```

### 💪 実装品質の特徴

#### 強み
- **一貫したエラーハンドリング**: 全サービスでILoggerと構造化ログ使用
- **UTF-8エンコーディング**: 日本語文字の適切なサポート
- **統一DataSet管理**: 全インポートで一貫した追跡
- **バッチ処理**: 大容量ファイルの効率的処理（1000レコード単位）
- **トランザクション安全性**: 適切なデータベーストランザクション使用

#### 技術的負債
- **ファイル移動無効化**: 全サービスでファイル移動ロジックがコメントアウト
- **スタブサービス**: ShippingMarkとRegionサービスが登録済みだが未実装
- **レガシーCsvImportService**: 古い実装が残存
- **単体テスト不足**: 包括的なテストカバレッジの証拠なし

## 7. 推奨される実装順序

### **Phase 1: スタブサービス完成**（即座実施可能）
1. **ShippingMarkMasterImportService** の実装完成
2. **RegionMasterImportService** の実装完成

**工数見積もり**: 1-2日（既存パターン使用）

### **Phase 2: 基本マスタデータ分類**（高優先度）
1. **単位マスタ** (`単位.csv`) - GetFileProcessOrderに認識済み
2. **商品分類1-3** - 商品管理の基盤
3. **得意先分類1-5** - 顧客セグメンテーション
4. **仕入先分類1-3** - サプライヤー管理

**工数見積もり**: 3-5日（新規CSV Model + Repository必要）

### **Phase 3: 人事管理**（中優先度）
1. **担当者マスタ** (`担当者.csv`) - 営業追跡に必須
2. **担当者分類** (`担当者分類１.csv`) - 担当者管理拡張

**工数見積もり**: 2-3日（新規データベーステーブル必要）

### **Phase 4: 財務取引**（低優先度）
1. **入金伝票** (`入金伝票.csv`) - 財務管理機能
2. **支払伝票** (`支払伝票.csv`) - 財務管理機能

**工数見積もり**: 5-7日（新規ドメインロジック必要）

## 8. 技術的課題と解決案

### 課題1: スタブサービスの実装不足
**現状**: ShippingMarkMasterImportService と RegionMasterImportService がInterface登録済みだが実装なし

**解決案**: 
- 既存のProductMasterImportServiceパターンを適用
- CSV Modelは既に存在
- Repositoryも準備済み（InsertBulkAsyncメソッド実装済み）

### 課題2: カテゴリマスタの大量実装
**現状**: 商品分類3種類、得意先分類5種類、仕入先分類3種類が未実装

**解決案**:
- 共通のCategoryMasterImportService基底クラス作成
- 設定によるカテゴリ種別の切り替え
- 同一のCSV構造を想定した共通化

### 課題3: 人事管理テーブルの設計不足
**現状**: 担当者関連のエンティティとテーブルが未定義

**解決案**:
- StaffMaster, StaffCategoryMasterエンティティの設計
- 既存のCustomerMasterパターンを踏襲
- 営業担当者との関連付け設計

### 課題4: 財務取引の複雑性
**現状**: 入金・支払伝票の財務ロジックが未定義

**解決案**:
- 段階的実装（まずはデータ取込のみ）
- 既存の取引伝票パターンを適用
- 後段での財務計算機能追加

## 9. 結論

InventoryManagementSystemのCSV取込機能は**優秀なアーキテクチャ**を持ち、12/15種類のCSVが実装済みで高い完成度を示しています。残り8種類の未実装ファイルも、確立されたインフラストラクチャを活用することで**迅速な実装が可能**です。

### 現在の実装状況サマリー
- ✅ **完全実装**: 7種類（基幹取引、主要マスタ）
- ⚠️ **スタブ実装**: 2種類（即座に完成可能）
- 🔧 **Repository直接**: 3種類（動作中）
- ❌ **未実装**: 8種類（段階的実装推奨）

**次のアクションとして、Phase 1のスタブサービス完成から開始することを強く推奨します。**