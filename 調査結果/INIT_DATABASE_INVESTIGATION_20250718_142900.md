# init-database コマンド実装調査結果

生成日時: 2025-07-18 14:29:00

## エグゼクティブサマリー

- **問題**: `init-database --force` でマスタテーブルの必要カラムが作成されない
- **根本原因**: 024_CreateProductMaster.sqlの除外により、マスタテーブルが全く作成されない
- **影響**: ProductMaster, CustomerMaster, SupplierMasterテーブルが一切作成されない
- **推奨修正方針**: 05_create_master_tables.sqlを確実に実行し、リポジトリ期待スキーマに合わせた完全版テーブル定義を作成

## 1. コマンド実装の流れ

### Program.cs
```csharp
// ExecuteInitDatabaseAsyncメソッド（行3229-3268）
private static async Task ExecuteInitDatabaseAsync(IServiceProvider services, string[] args)
{
    var initService = scopedServices.GetRequiredService<InventorySystem.Core.Interfaces.Development.IDatabaseInitializationService>();
    var force = args.Any(a => a == "--force");
    var result = await initService.InitializeDatabaseAsync(force);
    Console.WriteLine(result.GetSummary());
}
```

### DatabaseInitializationService
```csharp
// InitializeDatabaseAsyncメソッド（行255-354）
public async Task<InitializationResult> InitializeDatabaseAsync(bool force = false)
{
    // 1. CreateDatabase.sqlの実行
    // 2. マイグレーション履歴テーブルの作成
    // 3. マイグレーションスクリプトの順次実行
    // 4. データベース構造の検証
}
```

## 2. 実行されるSQLスクリプト

### 実行順序（_migrationOrder）
1. **000_CreateMigrationHistory.sql** - マイグレーション履歴テーブル作成
2. **005_AddDailyCloseProtectionColumns.sql** - 日次終了保護カラム追加
3. **006_AddDataSetManagement.sql** - データセット管理テーブル
4. **007_AddDeactivationIndexes.sql** - 非活性化インデックス
5. **008_AddUnmatchOptimizationIndexes.sql** - アンマッチ最適化インデックス
6. **009_CreateInitialInventoryStagingTable.sql** - 初期在庫ステージングテーブル
7. **010_AddPersonInChargeAndAveragePrice.sql** - 担当者と平均価格追加
8. **012_AddGrossProfitColumnToSalesVouchers.sql** - 売上伝票粗利カラム追加
9. **013_AddImportTypeToInventoryMaster.sql** - 在庫マスタインポート種別追加
10. **014_AddMissingColumnsToInventoryMaster.sql** - 在庫マスタ不足カラム追加
11. **015_AddMonthlyColumnsToCpInventoryMaster.sql** - CP在庫マスタ月次カラム追加
12. **016_AddMonthlyFieldsToCpInventory.sql** - CP在庫月次フィールド追加
13. **017_Cleanup_Duplicate_InventoryMaster.sql** - 在庫マスタ重複クリーンアップ
14. **018_FixExistingCpInventoryProductCategories.sql** - CP在庫商品分類修正
15. **019_Fix_DepartmentCode_Size.sql** - 部門コードサイズ修正
16. **020_Fix_MergeInventoryMaster_OutputClause.sql** - 在庫マスタマージ出力句修正
17. **021_VerifyInventoryMasterSchema.sql** - 在庫マスタスキーマ検証
18. **022_AddLastTransactionDates.sql** - 最終取引日追加
19. **023_UpdateDataSetManagement.sql** - データセット管理更新
20. **024_CreateProductMaster.sql** - **除外済み**（CreatedDate/UpdatedDate競合回避）
21. **024_PrepareDataSetUnification.sql** - データセット統合準備
22. **025_Fix_DataSets_Columns.sql** - データセットカラム修正
23. **025_CreateFileProcessingHistory.sql** - ファイル処理履歴作成
24. **026_CreateDateProcessingHistory.sql** - 日付処理履歴作成
25. **027_CreatePreviousMonthInventory.sql** - 前月末在庫作成
26. **028_AddDataSetTypeAndImportedAt.sql** - データセット種別と取込日時追加
27. **029_CreateShippingMarkMaster.sql** - 荷印マスタ作成
28. **030_CreateGradeMaster.sql** - 等級マスタ作成
29. **031_CreateClassMaster.sql** - 階級マスタ作成
30. **032_FixOriginMasterToRegionMaster.sql** - 産地マスタ名統一
31. **033_FixDataSetsSchema.sql** - データセットスキーマ修正
32. **034_FixDataSetManagementSchema.sql** - データセット管理スキーマ修正
33. **035_AddAllMissingTables.sql** - 不足テーブル追加
34. **050_Phase1_CheckCurrentSchema.sql** - 現在スキーマ確認
35. **051_Phase2_AddNewColumns.sql** - 新カラム追加
36. **052_Phase3_MigrateDataAndSync.sql** - データ移行と同期
37. **053_Phase5_Cleanup.sql** - 古いカラム削除

### 基本テーブル作成スクリプト
- **CreateDatabase.sql** - InventoryMaster, CpInventoryMaster, SalesVouchers, PurchaseVouchers, InventoryAdjustments, ShippingMarkMaster, DataSets
- **05_create_master_tables.sql** - マスタテーブル群（完全版）

## 3. テーブル定義の比較

### ProductMaster

#### 05_create_master_tables.sql版（完全版）
| カラム名 | データ型 | 説明 |
|---------|---------|------|
| ProductCode | NVARCHAR(15) | 商品コード（主キー） |
| ProductName | NVARCHAR(100) | 商品名 |
| ProductName2 | NVARCHAR(100) | 名称2 |
| ProductName3 | NVARCHAR(100) | 名称3 |
| ProductName4 | NVARCHAR(100) | 名称4 |
| ProductName5 | NVARCHAR(100) | 名称5 |
| SearchKana | NVARCHAR(100) | 検索カナ |
| ShortName | NVARCHAR(50) | 略称 |
| PrintCode | NVARCHAR(20) | 印刷用コード |
| ProductCategory1 | NVARCHAR(15) | 分類1コード |
| ProductCategory2 | NVARCHAR(15) | 分類2コード |
| ProductCategory3 | NVARCHAR(15) | 分類3コード |
| ProductCategory4 | NVARCHAR(15) | 分類4コード |
| ProductCategory5 | NVARCHAR(15) | 分類5コード |
| UnitCode | NVARCHAR(10) | バラ単位コード |
| CaseUnitCode | NVARCHAR(10) | ケース単位コード |
| Case2UnitCode | NVARCHAR(10) | ケース2単位コード |
| CaseQuantity | DECIMAL(13,4) | ケース入数 |
| Case2Quantity | DECIMAL(13,4) | ケース2入数 |
| StandardPrice | DECIMAL(16,4) | バラ標準価格 |
| CaseStandardPrice | DECIMAL(16,4) | ケース標準価格 |
| IsStockManaged | BIT | 在庫管理フラグ |
| TaxRate | INT | 消費税率 |
| CreatedDate | DATETIME2 | 作成日 |
| UpdatedDate | DATETIME2 | 更新日 |

#### 024_CreateProductMaster.sql版（簡易版・除外済み）
| カラム名 | データ型 | 説明 |
|---------|---------|------|
| ProductCode | NVARCHAR(15) | 商品コード（主キー） |
| ProductName | NVARCHAR(100) | 商品名 |
| ProductCategory1 | NVARCHAR(10) | 商品分類1 |
| ProductCategory2 | NVARCHAR(10) | 商品分類2 |
| Unit | NVARCHAR(10) | 単位 |
| StandardPrice | DECIMAL(12,4) | 標準単価 |
| IsActive | BIT | 有効フラグ |
| CreatedDate | DATETIME2 | 作成日 |
| UpdatedDate | DATETIME2 | 更新日 |
| Notes | NVARCHAR(500) | 備考 |

#### リポジトリが期待するカラム（ProductMasterRepository.cs）
| カラム名 | INSERT文での使用 | UPDATE文での使用 | 状態 |
|---------|----------------|----------------|------|
| ProductCode | ✓ | ✓ | OK |
| ProductName | ✓ | ✓ | OK |
| ProductName2 | ✓ | ✓ | **完全版のみ** |
| ProductName3 | ✓ | ✓ | **完全版のみ** |
| ProductName4 | ✓ | ✓ | **完全版のみ** |
| ProductName5 | ✓ | ✓ | **完全版のみ** |
| SearchKana | ✓ | ✓ | **完全版のみ** |
| ShortName | ✓ | ✓ | **完全版のみ** |
| PrintCode | ✓ | ✓ | **完全版のみ** |
| ProductCategory1 | ✓ | ✓ | OK |
| ProductCategory2 | ✓ | ✓ | OK |
| ProductCategory3 | ✓ | ✓ | **完全版のみ** |
| ProductCategory4 | ✓ | ✓ | **完全版のみ** |
| ProductCategory5 | ✓ | ✓ | **完全版のみ** |
| UnitCode | ✓ | ✓ | **完全版のみ** |
| CaseUnitCode | ✓ | ✓ | **完全版のみ** |
| Case2UnitCode | ✓ | ✓ | **完全版のみ** |
| CaseQuantity | ✓ | ✓ | **完全版のみ** |
| Case2Quantity | ✓ | ✓ | **完全版のみ** |
| StandardPrice | ✓ | ✓ | OK |
| CaseStandardPrice | ✓ | ✓ | **完全版のみ** |
| IsStockManaged | ✓ | ✓ | **完全版のみ** |
| TaxRate | ✓ | ✓ | **完全版のみ** |
| CreatedAt | ✓ | × | **新スキーマ** |
| UpdatedAt | ✓ | × | **新スキーマ** |

### CustomerMaster

#### 05_create_master_tables.sql版（完全版）
| カラム名 | データ型 | 説明 |
|---------|---------|------|
| CustomerCode | NVARCHAR(15) | 得意先コード（主キー） |
| CustomerName | NVARCHAR(100) | 得意先名 |
| CustomerName2 | NVARCHAR(100) | 得意先名2 |
| SearchKana | NVARCHAR(100) | 検索カナ |
| ShortName | NVARCHAR(50) | 略称 |
| PostalCode | NVARCHAR(10) | 郵便番号 |
| Address1 | NVARCHAR(100) | 住所1 |
| Address2 | NVARCHAR(100) | 住所2 |
| Address3 | NVARCHAR(100) | 住所3 |
| PhoneNumber | NVARCHAR(20) | 電話番号 |
| FaxNumber | NVARCHAR(20) | FAX番号 |
| CustomerCategory1 | NVARCHAR(15) | 分類1 |
| CustomerCategory2 | NVARCHAR(15) | 分類2 |
| CustomerCategory3 | NVARCHAR(15) | 分類3 |
| CustomerCategory4 | NVARCHAR(15) | 分類4 |
| CustomerCategory5 | NVARCHAR(15) | 分類5 |
| WalkingRate | DECIMAL(5,2) | 歩引き率 |
| BillingCode | NVARCHAR(15) | 請求先コード |
| IsActive | BIT | 有効フラグ |
| CreatedDate | DATETIME2 | 作成日 |
| UpdatedDate | DATETIME2 | 更新日 |

#### 024_CreateProductMaster.sql版（簡易版・除外済み）
| カラム名 | データ型 | 説明 |
|---------|---------|------|
| CustomerCode | NVARCHAR(20) | 得意先コード（主キー） |
| CustomerName | NVARCHAR(100) | 得意先名 |
| CustomerKana | NVARCHAR(100) | 得意先カナ |
| ZipCode | NVARCHAR(10) | 郵便番号 |
| Address1 | NVARCHAR(100) | 住所1 |
| Address2 | NVARCHAR(100) | 住所2 |
| Phone | NVARCHAR(20) | 電話番号 |
| Fax | NVARCHAR(20) | FAX番号 |
| IsActive | BIT | 有効フラグ |
| CreatedDate | DATETIME2 | 作成日 |
| UpdatedDate | DATETIME2 | 更新日 |

#### リポジトリが期待するカラム（CustomerMasterRepository.cs）
| カラム名 | INSERT文での使用 | UPDATE文での使用 | 状態 |
|---------|----------------|----------------|------|
| CustomerCode | ✓ | ✓ | OK |
| CustomerName | ✓ | ✓ | OK |
| CustomerName2 | ✓ | ✓ | **完全版のみ** |
| SearchKana | ✓ | ✓ | **完全版のみ** |
| ShortName | ✓ | ✓ | **完全版のみ** |
| PostalCode | ✓ | ✓ | **完全版のみ** |
| Address1 | ✓ | ✓ | OK |
| Address2 | ✓ | ✓ | OK |
| Address3 | ✓ | ✓ | **完全版のみ** |
| PhoneNumber | ✓ | ✓ | **完全版のみ** |
| FaxNumber | ✓ | ✓ | **完全版のみ** |
| CustomerCategory1 | ✓ | ✓ | **完全版のみ** |
| CustomerCategory2 | ✓ | ✓ | **完全版のみ** |
| CustomerCategory3 | ✓ | ✓ | **完全版のみ** |
| CustomerCategory4 | ✓ | ✓ | **完全版のみ** |
| CustomerCategory5 | ✓ | ✓ | **完全版のみ** |
| WalkingRate | ✓ | ✓ | **完全版のみ** |
| BillingCode | ✓ | ✓ | **完全版のみ** |
| IsActive | ✓ | ✓ | OK |
| CreatedAt | ✓ | × | **新スキーマ** |
| UpdatedAt | ✓ | × | **新スキーマ** |

### SupplierMaster

#### 05_create_master_tables.sql版（完全版）
| カラム名 | データ型 | 説明 |
|---------|---------|------|
| SupplierCode | NVARCHAR(15) | 仕入先コード（主キー） |
| SupplierName | NVARCHAR(100) | 仕入先名 |
| SupplierName2 | NVARCHAR(100) | 仕入先名2 |
| SearchKana | NVARCHAR(100) | 検索カナ |
| ShortName | NVARCHAR(50) | 略称 |
| PostalCode | NVARCHAR(10) | 郵便番号 |
| Address1 | NVARCHAR(100) | 住所1 |
| Address2 | NVARCHAR(100) | 住所2 |
| Address3 | NVARCHAR(100) | 住所3 |
| PhoneNumber | NVARCHAR(20) | 電話番号 |
| FaxNumber | NVARCHAR(20) | FAX番号 |
| SupplierCategory1 | NVARCHAR(15) | 分類1 |
| SupplierCategory2 | NVARCHAR(15) | 分類2 |
| SupplierCategory3 | NVARCHAR(15) | 分類3 |
| PaymentCode | NVARCHAR(15) | 支払先コード |
| IsActive | BIT | 有効フラグ |
| CreatedDate | DATETIME2 | 作成日 |
| UpdatedDate | DATETIME2 | 更新日 |

#### 024_CreateProductMaster.sql版（簡易版・除外済み）
| カラム名 | データ型 | 説明 |
|---------|---------|------|
| SupplierCode | NVARCHAR(20) | 仕入先コード（主キー） |
| SupplierName | NVARCHAR(100) | 仕入先名 |
| SupplierKana | NVARCHAR(100) | 仕入先カナ |
| ZipCode | NVARCHAR(10) | 郵便番号 |
| Address1 | NVARCHAR(100) | 住所1 |
| Address2 | NVARCHAR(100) | 住所2 |
| Phone | NVARCHAR(20) | 電話番号 |
| Fax | NVARCHAR(20) | FAX番号 |
| IsActive | BIT | 有効フラグ |
| CreatedDate | DATETIME2 | 作成日 |
| UpdatedDate | DATETIME2 | 更新日 |

#### リポジトリが期待するカラム（SupplierMasterRepository.cs）
| カラム名 | INSERT文での使用 | UPDATE文での使用 | 状態 |
|---------|----------------|----------------|------|
| SupplierCode | ✓ | ✓ | OK |
| SupplierName | ✓ | ✓ | OK |
| SupplierName2 | ✓ | ✓ | **完全版のみ** |
| SearchKana | ✓ | ✓ | **完全版のみ** |
| ShortName | ✓ | ✓ | **完全版のみ** |
| PostalCode | ✓ | ✓ | **完全版のみ** |
| Address1 | ✓ | ✓ | OK |
| Address2 | ✓ | ✓ | OK |
| Address3 | ✓ | ✓ | **完全版のみ** |
| PhoneNumber | ✓ | ✓ | **完全版のみ** |
| FaxNumber | ✓ | ✓ | **完全版のみ** |
| SupplierCategory1 | ✓ | ✓ | **完全版のみ** |
| SupplierCategory2 | ✓ | ✓ | **完全版のみ** |
| SupplierCategory3 | ✓ | ✓ | **完全版のみ** |
| PaymentCode | ✓ | ✓ | **完全版のみ** |
| IsActive | ✓ | ✓ | OK |
| CreatedAt | ✓ | × | **新スキーマ** |
| UpdatedAt | ✓ | × | **新スキーマ** |

## 4. 実行順序の確認

### マイグレーション実行順序
マイグレーションは`_migrationOrder`リストの順序で実行されます：

1. **基本テーブル作成**: CreateDatabase.sql
2. **マイグレーション履歴**: 000_CreateMigrationHistory.sql
3. **システムテーブル**: 005～034番台のマイグレーション
4. **マスタテーブル**: 030番台（等級・階級・荷印）
5. **追加テーブル**: 035_AddAllMissingTables.sql
6. **フェーズドマイグレーション**: 050～053番台（CreatedAt/UpdatedAt移行）

### テーブル作成の競合
- **024_CreateProductMaster.sql は除外済み**
- **05_create_master_tables.sql は_migrationOrderに含まれていない**
- **CreateDatabase.sql は基本テーブルのみ作成**

## 5. 問題の特定

### 根本原因
1. **024_CreateProductMaster.sql の除外**: migrate-phase3/5との競合回避のため除外されている
2. **05_create_master_tables.sql の未実行**: _migrationOrderリストに含まれていない
3. **CreateDatabase.sql の対象外**: ProductMaster, CustomerMaster, SupplierMasterは含まれていない

### 影響
- **ProductMaster**: テーブル自体が作成されない
- **CustomerMaster**: テーブル自体が作成されない
- **SupplierMaster**: テーブル自体が作成されない
- **リポジトリエラー**: INSERT/UPDATE文で存在しないテーブルを参照

## 6. 修正案（調査のみ、実装は次のステップ）

### 案1: 05_create_master_tables.sqlの_migrationOrderへの追加
```csharp
// DatabaseInitializationService.cs の _migrationOrder
"029_CreateShippingMarkMaster.sql",
"030_CreateGradeMaster.sql",
"031_CreateClassMaster.sql",
"032_FixOriginMasterToRegionMaster.sql",
"05_create_master_tables.sql",           // 追加
"033_FixDataSetsSchema.sql",
```

### 案2: 新しいマイグレーションファイルの作成
CreatedAt/UpdatedAtスキーマに対応した新しいマイグレーションファイルを作成：
- `036_CreateMasterTablesWithNewSchema.sql`

### 案3: 既存マイグレーションの修正
030_CreateGradeMaster.sql, 031_CreateClassMaster.sql と同様の形式で、完全版マスタテーブルを作成するマイグレーションを追加。

### 案4: CreateDatabase.sqlの拡張
CreateDatabase.sqlに完全版のマスタテーブル定義を追加（リスクあり）。

## 7. 追加の発見事項

### スキーマ移行の状況
- **フェーズドマイグレーション**: 050～053番台でCreatedDate/UpdatedDate → CreatedAt/UpdatedAtの移行が実装済み
- **除外設定**: 024_CreateProductMaster.sqlは意図的に除外されている
- **新スキーマ対応**: リポジトリは既にCreatedAt/UpdatedAtを使用

### 実装済み機能
- **等級・階級マスタ**: 030, 031番台で作成済み
- **荷印マスタ**: 029番台で作成済み
- **追加テーブル**: 035番台で各種分類マスタやステージングテーブルを作成済み

### 未実装の重要テーブル
- **ProductMaster**: 完全版が作成されない
- **CustomerMaster**: 完全版が作成されない
- **SupplierMaster**: 完全版が作成されない

## 8. 推奨実装方針

### 最優先事項
1. **05_create_master_tables.sqlの確実な実行**: _migrationOrderに追加
2. **スキーマ整合性の確保**: CreatedAt/UpdatedAtへの対応
3. **リポジトリ期待カラムの全対応**: 完全版テーブル定義の使用

### 実装時の注意点
- **フェーズドマイグレーション**: 既存の050～053番台との整合性を保つ
- **除外設定の維持**: 024_CreateProductMaster.sqlの除外は継続
- **外部キー制約**: 05_create_master_tables.sqlの外部キー制約も考慮

### 検証方法
1. **テーブル存在確認**: 3つのマスタテーブルが作成されること
2. **カラム存在確認**: リポジトリが期待するすべてのカラムが存在すること
3. **型整合性確認**: データ型とサイズがリポジトリの期待値と一致すること
4. **インデックス確認**: 必要なインデックスが作成されること

---

**調査完了**: この調査により、init-databaseコマンドでマスタテーブルが作成されない根本原因が特定されました。次のステップとして、05_create_master_tables.sqlの確実な実行を実装することを推奨します。