# SQL Error 207 詳細調査結果

生成日時: 2025-07-18 14:30:00

## エグゼクティブサマリー
- **問題の根本原因**: ProductMaster、CustomerMaster、SupplierMasterテーブルでCreatedAt/UpdatedAtカラムとCreatedDate/UpdatedDateカラムの不整合
- **影響を受けているテーブル**: ProductMaster, CustomerMaster, SupplierMaster
- **推奨される修正方針**: フェーズド・マイグレーション（Phase2→Phase3→Phase5）を実行してスキーマを統一

## 1. テーブル定義の重複状況

### database/05_create_master_tables.sql（基本テーブル定義）

#### ProductMaster
- **カラム数**: 21個のカラム
- **日付カラム**: 
  - `CreatedDate DATETIME2 DEFAULT GETDATE()` (line 70)
  - `UpdatedDate DATETIME2 DEFAULT GETDATE()` (line 71)

#### CustomerMaster  
- **カラム数**: 17個のカラム
- **日付カラム**:
  - `CreatedDate DATETIME2 DEFAULT GETDATE()` (line 31)
  - `UpdatedDate DATETIME2 DEFAULT GETDATE()` (line 32)

#### SupplierMaster
- **カラム数**: 15個のカラム  
- **日付カラム**:
  - `CreatedDate DATETIME2 DEFAULT GETDATE()` (line 102)
  - `UpdatedDate DATETIME2 DEFAULT GETDATE()` (line 103)

### database/migrations/024_CreateProductMaster.sql（移行スクリプト）

#### ProductMaster
- **カラム数**: 9個のカラム（簡略版）
- **日付カラム**:
  - `CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE()` (line 21)
  - `UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()` (line 22)

#### CustomerMaster  
- **カラム数**: 9個のカラム（簡略版）
- **日付カラム**:
  - `CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE()` (line 52)
  - `UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()` (line 53)

#### SupplierMaster
- **カラム数**: 9個のカラム（簡略版）
- **日付カラム**:
  - `CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE()` (line 81)
  - `UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()` (line 82)

### フェーズド・マイグレーション（Phase2〜Phase5）

#### 051_Phase2_AddNewColumns.sql
- **目的**: 既存テーブルに新しいCreatedAt/UpdatedAtカラムを追加
- **新カラム**: CreatedAt DATETIME2 NULL, UpdatedAt DATETIME2 NULL
- **特徴**: 既存のCreatedDate/UpdatedDateカラムを残したまま新カラムを追加

#### 052_Phase3_MigrateDataAndSync.sql  
- **目的**: データ移行と同期トリガー設定
- **機能**: CreatedDate→CreatedAt、UpdatedDate→UpdatedAtのデータコピーとトリガー作成

#### 053_Phase5_Cleanup.sql
- **目的**: 古いカラムの削除とクリーンアップ
- **機能**: CreatedDate/UpdatedDateカラムの削除、CreatedAt/UpdatedAtをNOT NULLに変更

## 2. リポジトリSQL文の分析

### ProductMasterRepository.cs
```sql
-- InsertBulkAsync (line 63-78)
INSERT INTO ProductMaster (
    ProductCode, ProductName, ProductName2, ProductName3, ProductName4, ProductName5,
    SearchKana, ShortName, PrintCode,
    ProductCategory1, ProductCategory2, ProductCategory3, ProductCategory4, ProductCategory5,
    UnitCode, CaseUnitCode, Case2UnitCode, CaseQuantity, Case2Quantity,
    StandardPrice, CaseStandardPrice, IsStockManaged, TaxRate,
    CreatedAt, UpdatedAt  -- 新しいスキーマを使用
) VALUES (...)
```

**使用カラム**: CreatedAt, UpdatedAt（新スキーマ）
**問題のカラム**: CreatedDate, UpdatedDate（古いスキーマ）は使用していない

### CustomerMasterRepository.cs
```sql
-- InsertBulkAsync (line 64-75)
INSERT INTO CustomerMaster (
    CustomerCode, CustomerName, CustomerName2, SearchKana, ShortName,
    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
    CustomerCategory1, CustomerCategory2, CustomerCategory3, CustomerCategory4, CustomerCategory5,
    WalkingRate, BillingCode, IsActive, CreatedAt, UpdatedAt  -- 新しいスキーマを使用
) VALUES (...)
```

**問題箇所**: line 129でDeleteAsyncメソッドが`UpdatedDate`（古いスキーマ）を使用
```sql
UPDATE CustomerMaster 
SET IsActive = 0, UpdatedDate = GETDATE()  -- ❌ エラー箇所
WHERE CustomerCode = @CustomerCode
```

### SupplierMasterRepository.cs
```sql
-- InsertBulkAsync (line 63-74)
INSERT INTO SupplierMaster (
    SupplierCode, SupplierName, SupplierName2, SearchKana, ShortName,
    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
    SupplierCategory1, SupplierCategory2, SupplierCategory3,
    PaymentCode, IsActive, CreatedAt, UpdatedAt  -- 新しいスキーマを使用
) VALUES (...)
```

**問題箇所**: line 125でDeleteAsyncメソッドが`UpdatedDate`（古いスキーマ）を使用
```sql
UPDATE SupplierMaster 
SET IsActive = 0, UpdatedDate = GETDATE()  -- ❌ エラー箇所
WHERE SupplierCode = @SupplierCode
```

## 3. 不一致の詳細

| テーブル | カラム名 | 05_create_master_tables.sql | 024_CreateProductMaster.sql | Phase2追加 | リポジトリSQL | 状態 |
|---------|---------|---------------------------|---------------------------|-----------|-------------|------|
| ProductMaster | CreatedDate | あり | あり | - | 使用なし | 古いスキーマ |
| ProductMaster | UpdatedDate | あり | あり | - | 使用なし | 古いスキーマ |
| ProductMaster | CreatedAt | なし | なし | あり | 使用 | 新スキーマ |
| ProductMaster | UpdatedAt | なし | なし | あり | 使用 | 新スキーマ |
| CustomerMaster | CreatedDate | あり | あり | - | 使用なし | 古いスキーマ |
| CustomerMaster | UpdatedDate | あり | あり | - | ❌ 使用 | **エラー原因** |
| CustomerMaster | CreatedAt | なし | なし | あり | 使用 | 新スキーマ |
| CustomerMaster | UpdatedAt | なし | なし | あり | 使用 | 新スキーマ |
| SupplierMaster | CreatedDate | あり | あり | - | 使用なし | 古いスキーマ |
| SupplierMaster | UpdatedDate | あり | あり | - | ❌ 使用 | **エラー原因** |
| SupplierMaster | CreatedAt | なし | なし | あり | 使用 | 新スキーマ |
| SupplierMaster | UpdatedAt | なし | なし | あり | 使用 | 新スキーマ |

## 4. ステージングテーブルの確認

### InitialInventory_Staging
- **定義ファイル**: database/migrations/009_CreateInitialInventoryStagingTable.sql
- **カラム数**: 13個のカラム + 処理管理カラム
- **関連テーブル**: InitialInventory_ErrorLog
- **日付カラム**: ProcessDate DATETIME2（問題なし）

**特記事項**: ステージングテーブルは独自の構造で、マスタテーブルの日付カラム問題とは無関係

## 5. エラーの根本原因

### 直接的な原因
1. **CustomerMasterRepository.cs:129** - `UpdatedDate`カラムの使用（存在しないカラム）
2. **SupplierMasterRepository.cs:125** - `UpdatedDate`カラムの使用（存在しないカラム）

### 構造的な原因
1. **スキーマの二重管理**: 古いスキーマ（CreatedDate/UpdatedDate）と新しいスキーマ（CreatedAt/UpdatedAt）が混在
2. **migration 024_CreateProductMaster.sqlの除外**: CLAUDE.md記載のとおり、024は除外されているが、代替のマイグレーションが不完全
3. **フェーズド・マイグレーションの未実行**: Phase2〜Phase5のマイグレーションが実行されていない可能性

## 6. 推奨される修正方針

### Phase 1: 即座に実行可能な修正（緊急対応）
1. **リポジトリの修正**:
   ```csharp
   // CustomerMasterRepository.cs line 129
   SET IsActive = 0, UpdatedAt = GETDATE()  // UpdatedDate → UpdatedAt
   
   // SupplierMasterRepository.cs line 125  
   SET IsActive = 0, UpdatedAt = GETDATE()  // UpdatedDate → UpdatedAt
   ```

### Phase 2: 根本的な解決（推奨）
1. **migrate-phase2コマンドの実行**: 新しいカラムを追加
2. **migrate-phase3コマンドの実行**: データ移行と同期トリガー設定
3. **migrate-phase5コマンドの実行**: 古いカラムの削除とクリーンアップ

### Phase 3: 検証と最適化
1. **import-folderコマンドの動作確認**
2. **全マスタテーブルの整合性チェック**
3. **024_CreateProductMaster.sqlの完全除外確認**

## 7. 追加調査が必要な項目

### データベースの現在の状態確認
```sql
-- テーブル構造の確認
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
AND (COLUMN_NAME LIKE '%Created%' OR COLUMN_NAME LIKE '%Updated%')
ORDER BY TABLE_NAME, COLUMN_NAME;
```

### マイグレーション履歴の確認
```sql
-- 実行済みマイグレーションの確認
SELECT * FROM MigrationHistory 
WHERE MigrationName LIKE '%Phase%' 
OR MigrationName LIKE '%024%'
ORDER BY ExecutedAt DESC;
```

## 8. 実装時の注意事項

### 緊急修正時
- リポジトリの修正は、必ずテーブル構造に対応したカラム名を使用
- エラーログで実際に存在しないカラム名を特定してから修正

### フェーズド・マイグレーション実行時
- 必ずPhase2→Phase3→Phase5の順序で実行
- 各フェーズ完了後にアプリケーションの動作確認を実施
- Phase5実行前に完全バックアップを取得

### 運用時の考慮事項
- 新旧スキーマが混在する期間中は、どちらのスキーマを使用するかコード内で明確に統一
- DI登録での環境別のサービス切り替えは、スキーマ移行とは独立して管理

---
**調査実行者**: Claude Code  
**調査日時**: 2025-07-18 14:30:00  
**対象プロジェクト**: InventoryManagementSystem  
**調査レベル**: 詳細（コード構造・テーブル定義・マイグレーション履歴）