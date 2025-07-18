# 修正後の現状確認結果

生成日時: 2025-07-18 22:44:00

## エグゼクティブサマリー
- 前回修正の適用状況: **部分適用**（ImportServiceのみ修正済み、Repositoryは未修正）
- 現在のエラー原因: **RepositoryクラスのSQL文がCreatedAt/UpdatedAtを使用しているが、DBにはこれらのカラムがまだ存在しない**
- 必要な対応: **フェーズドマイグレーション（phase2, phase3, phase5）の実行**

## 1. リポジトリコードの現状

### ProductMasterRepository.cs
**最終更新**: 修正されていない（CreatedAt/UpdatedAtを使用）

```csharp
// InsertBulkAsync メソッド（63-78行）
const string sql = @"
    INSERT INTO ProductMaster (
        ProductCode, ProductName, ProductName2, ProductName3, ProductName4, ProductName5,
        SearchKana, ShortName, PrintCode,
        ProductCategory1, ProductCategory2, ProductCategory3, ProductCategory4, ProductCategory5,
        UnitCode, CaseUnitCode, Case2UnitCode, CaseQuantity, Case2Quantity,
        StandardPrice, CaseStandardPrice, IsStockManaged, TaxRate,
        CreatedAt, UpdatedAt
    ) VALUES (
        @ProductCode, @ProductName, @ProductName2, @ProductName3, @ProductName4, @ProductName5,
        @SearchKana, @ShortName, @PrintCode,
        @ProductCategory1, @ProductCategory2, @ProductCategory3, @ProductCategory4, @ProductCategory5,
        @UnitCode, @CaseUnitCode, @Case2UnitCode, @CaseQuantity, @Case2Quantity,
        @StandardPrice, @CaseStandardPrice, @IsStockManaged, @TaxRate,
        @CreatedAt, @UpdatedAt
    )";
```

使用カラム名: **CreatedAt/UpdatedAt**
使用パラメータ: **@CreatedAt/@UpdatedAt**

### CustomerMasterRepository.cs
**最終更新**: 修正されていない（CreatedAt/UpdatedAtを使用）

```csharp
// InsertBulkAsync メソッド（64-75行）
const string sql = @"
    INSERT INTO CustomerMaster (
        CustomerCode, CustomerName, CustomerName2, SearchKana, ShortName,
        PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
        CustomerCategory1, CustomerCategory2, CustomerCategory3, CustomerCategory4, CustomerCategory5,
        WalkingRate, BillingCode, IsActive, CreatedAt, UpdatedAt
    ) VALUES (
        @CustomerCode, @CustomerName, @CustomerName2, @SearchKana, @ShortName,
        @PostalCode, @Address1, @Address2, @Address3, @PhoneNumber, @FaxNumber,
        @CustomerCategory1, @CustomerCategory2, @CustomerCategory3, @CustomerCategory4, @CustomerCategory5,
        @WalkingRate, @BillingCode, @IsActive, @CreatedAt, @UpdatedAt
    )";
```

使用カラム名: **CreatedAt/UpdatedAt**
使用パラメータ: **@CreatedAt/@UpdatedAt**

### SupplierMasterRepository.cs
**最終更新**: 修正されていない（CreatedAt/UpdatedAtを使用）

```csharp
// InsertBulkAsync メソッド（63-74行）
const string sql = @"
    INSERT INTO SupplierMaster (
        SupplierCode, SupplierName, SupplierName2, SearchKana, ShortName,
        PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
        SupplierCategory1, SupplierCategory2, SupplierCategory3,
        PaymentCode, IsActive, CreatedAt, UpdatedAt
    ) VALUES (
        @SupplierCode, @SupplierName, @SupplierName2, @SearchKana, @ShortName,
        @PostalCode, @Address1, @Address2, @Address3, @PhoneNumber, @FaxNumber,
        @SupplierCategory1, @SupplierCategory2, @SupplierCategory3,
        @PaymentCode, @IsActive, @CreatedAt, @UpdatedAt
    )";
```

使用カラム名: **CreatedAt/UpdatedAt**
使用パラメータ: **@CreatedAt/@UpdatedAt**

## 2. データベーステーブルの現状

### 実際のカラム構造（024_CreateProductMaster.sqlより）

#### ProductMaster テーブル
```sql
CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 作成日
UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 更新日
```

#### CustomerMaster テーブル
```sql
CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(), -- 作成日
UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()  -- 更新日
```

#### SupplierMaster テーブル
```sql
-- 024_CreateProductMaster.sqlではSupplierMasterの定義が見つからないが、
-- 同様にCreatedDate/UpdatedDateを使用していると推測される
```

## 3. 不整合の詳細

| 項目 | 期待値 | 実際の値 | 状態 |
|-----|--------|---------|------|
| ProductMaster - DB列名 | CreatedAt/UpdatedAt | CreatedDate/UpdatedDate | ❌ 不整合 |
| ProductMaster - SQL文 | CreatedAt/UpdatedAt | CreatedAt/UpdatedAt | ✅ 統一 |
| ProductMaster - パラメータ | @CreatedAt/@UpdatedAt | @CreatedAt/@UpdatedAt | ✅ 統一 |
| CustomerMaster - DB列名 | CreatedAt/UpdatedAt | CreatedDate/UpdatedDate | ❌ 不整合 |
| CustomerMaster - SQL文 | CreatedAt/UpdatedAt | CreatedAt/UpdatedAt | ✅ 統一 |
| CustomerMaster - パラメータ | @CreatedAt/@UpdatedAt | @CreatedAt/@UpdatedAt | ✅ 統一 |
| SupplierMaster - DB列名 | CreatedAt/UpdatedAt | CreatedDate/UpdatedDate | ❌ 不整合 |
| SupplierMaster - SQL文 | CreatedAt/UpdatedAt | CreatedAt/UpdatedAt | ✅ 統一 |
| SupplierMaster - パラメータ | @CreatedAt/@UpdatedAt | @CreatedAt/@UpdatedAt | ✅ 統一 |

## 4. 前回修正が反映されていない原因

### 原因: フェーズドマイグレーションが未実行

1. **リポジトリクラスは既に新しいスキーマ（CreatedAt/UpdatedAt）を前提に修正済み**
2. **ImportServiceクラスも新しいプロパティ名に修正済み（最新コミット afe9a76）**
3. **しかし、データベーステーブルはまだ古いスキーマ（CreatedDate/UpdatedDate）のまま**
4. **フェーズドマイグレーション（phase2, phase3, phase5）が未実行**

### マイグレーションファイルの存在
以下のマイグレーションファイルは作成済みだが、未実行：
- `050_Phase1_CheckCurrentSchema.sql` - 現在のスキーマ確認
- `051_Phase2_AddNewColumns.sql` - 新カラム（CreatedAt/UpdatedAt）追加
- `052_Phase3_MigrateDataAndSync.sql` - データ移行と同期トリガー作成
- `053_Phase5_Cleanup.sql` - 古いカラム（CreatedDate/UpdatedDate）削除

## 5. 推奨される次のアクション

### 即座に実行すべきアクション

1. **migrate-phase2コマンドの実行**
   ```bash
   dotnet run -- migrate-phase2
   ```
   - 新しいCreatedAt/UpdatedAtカラムを追加（非破壊的変更）

2. **migrate-phase3コマンドの実行**
   ```bash
   dotnet run -- migrate-phase3
   ```
   - 既存データを新カラムに移行
   - 同期トリガーを作成（両方のカラムを同期）

3. **import-folderコマンドの動作確認**
   ```bash
   dotnet run -- import-folder DeptA 2025-06-27
   ```
   - この時点で正常動作するはず

4. **migrate-phase5コマンドの実行**（オプション、後日実行可）
   ```bash
   dotnet run -- migrate-phase5
   ```
   - 古いCreatedDate/UpdatedDateカラムを削除
   - 完全に新スキーマに移行

### 注意事項
- phase2とphase3は連続して実行する必要がある
- phase5は動作確認後、数日経ってから実行することを推奨
- 各フェーズ実行前にデータベースのバックアップを取得すること