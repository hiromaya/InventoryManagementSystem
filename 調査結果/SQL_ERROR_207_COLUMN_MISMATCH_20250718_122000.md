# SQLエラー207 カラム名不一致調査結果

## 1. ProductMasterRepository

### 使用しているカラム名（リポジトリ側）
```sql
INSERT INTO ProductMaster (
    ProductCode, ProductName, ProductName2, ProductName3, ProductName4, ProductName5,
    SearchKana, ShortName, PrintCode,
    ProductCategory1, ProductCategory2, ProductCategory3, ProductCategory4, ProductCategory5,
    UnitCode, CaseUnitCode, Case2UnitCode, CaseQuantity, Case2Quantity,
    StandardPrice, CaseStandardPrice, IsStockManaged, TaxRate,
    CreatedAt, UpdatedAt  -- ❌ エラーの原因
)
```

### 実際のテーブルカラム名（database/05_create_master_tables.sql）
```sql
CREATE TABLE ProductMaster (
    ProductCode, ProductName, ProductName2, ProductName3, ProductName4, ProductName5,
    SearchKana, ShortName, PrintCode,
    ProductCategory1, ProductCategory2, ProductCategory3, ProductCategory4, ProductCategory5,
    UnitCode, CaseUnitCode, Case2UnitCode, CaseQuantity, Case2Quantity,
    StandardPrice, CaseStandardPrice, IsStockManaged, TaxRate,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE()  -- ✅ 正しいカラム名
);
```

### 不一致のカラム（ProductMaster）
**✅ すべて一致** - ProductMasterに問題なし

## 2. CustomerMasterRepository

### 使用しているカラム名（リポジトリ側）
```sql
INSERT INTO CustomerMaster (
    CustomerCode, CustomerName, CustomerName2, SearchKana, ShortName,
    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
    CustomerCategory1, CustomerCategory2, CustomerCategory3, CustomerCategory4, CustomerCategory5,
    WalkingRate, BillingCode, IsActive, CreatedAt, UpdatedAt  -- ❌ エラーの原因
)
```

### 実際のテーブルカラム名（database/05_create_master_tables.sql）
```sql
CREATE TABLE CustomerMaster (
    CustomerCode, CustomerName, CustomerName2, SearchKana, ShortName,
    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
    CustomerCategory1, CustomerCategory2, CustomerCategory3, CustomerCategory4, CustomerCategory5,
    WalkingRate, BillingCode, IsActive,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE()  -- ✅ 正しいカラム名
);
```

### 不一致のカラム（CustomerMaster）
**✅ すべて一致** - CustomerMasterに問題なし

## 3. SupplierMasterRepository

### 使用しているカラム名（リポジトリ側）
```sql
INSERT INTO SupplierMaster (
    SupplierCode, SupplierName, SupplierName2, SearchKana, ShortName,
    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
    SupplierCategory1, SupplierCategory2, SupplierCategory3,
    PaymentCode, IsActive, CreatedAt, UpdatedAt  -- ❌ エラーの原因
)
```

### 実際のテーブルカラム名（database/05_create_master_tables.sql）
```sql
CREATE TABLE SupplierMaster (
    SupplierCode, SupplierName, SupplierName2, SearchKana, ShortName,
    PostalCode, Address1, Address2, Address3, PhoneNumber, FaxNumber,
    SupplierCategory1, SupplierCategory2, SupplierCategory3,
    PaymentCode, IsActive,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE()  -- ✅ 正しいカラム名
);
```

### 不一致のカラム（SupplierMaster）
**✅ すべて一致** - SupplierMasterに問題なし

## 4. 根本原因の発見

### 📊 重複するテーブル定義の問題

データベースには**2つの異なるマスタテーブル定義**が存在していることが判明：

#### ❌ 古い定義（database/migrations/024_CreateProductMaster.sql）
```sql
CREATE TABLE ProductMaster (
    ProductCode, ProductName, ProductCategory1, ProductCategory2, Unit, StandardPrice, 
    IsActive, 
    CreatedDate,  -- ❌ Date
    UpdatedDate,  -- ❌ Date
    Notes
);

CREATE TABLE CustomerMaster (
    CustomerCode, CustomerName, CustomerKana, ZipCode, Address1, Address2, Phone, Fax, 
    IsActive, 
    CreatedDate,  -- ❌ Date
    UpdatedDate   -- ❌ Date
);

CREATE TABLE SupplierMaster (
    SupplierCode, SupplierName, SupplierKana, ZipCode, Address1, Address2, Phone, Fax, 
    IsActive, 
    CreatedDate,  -- ❌ Date
    UpdatedDate   -- ❌ Date
);
```

#### ✅ 正しい定義（database/05_create_master_tables.sql）
```sql
CREATE TABLE ProductMaster (
    [完全な列定義]
    CreatedAt DATETIME2,  -- ✅ At
    UpdatedAt DATETIME2   -- ✅ At
);

CREATE TABLE CustomerMaster (
    [完全な列定義]
    CreatedAt DATETIME2,  -- ✅ At
    UpdatedAt DATETIME2   -- ✅ At
);

CREATE TABLE SupplierMaster (
    [完全な列定義]
    CreatedAt DATETIME2,  -- ✅ At
    UpdatedAt DATETIME2   -- ✅ At
);
```

## 5. 修正が必要な箇所

**問題**: データベースに古い定義のテーブルが作成されている可能性

### 🔧 解決策

1. **データベース状態の確認**
   ```sql
   SELECT COLUMN_NAME, DATA_TYPE 
   FROM INFORMATION_SCHEMA.COLUMNS 
   WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
   ORDER BY TABLE_NAME, ORDINAL_POSITION;
   ```

2. **テーブル再作成が必要な場合**
   - 古い定義（migration 024）で作成されたテーブルをDROP
   - 正しい定義（05_create_master_tables.sql）で再作成

3. **マイグレーションファイルの無効化**
   - `database/migrations/024_CreateProductMaster.sql`を無効化
   - 正しい定義の`database/05_create_master_tables.sql`を使用

## 6. 緊急対応の推奨手順

### Step 1: データベース確認
```sql
-- 実際のカラム名を確認
SELECT TABLE_NAME, COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
AND COLUMN_NAME LIKE '%Created%' OR COLUMN_NAME LIKE '%Updated%'
ORDER BY TABLE_NAME, COLUMN_NAME;
```

### Step 2: テーブル再作成（データ損失注意）
```sql
-- バックアップ取得後
DROP TABLE IF EXISTS ProductMaster;
DROP TABLE IF EXISTS CustomerMaster; 
DROP TABLE IF EXISTS SupplierMaster;

-- 正しい定義で再作成
-- database/05_create_master_tables.sql を実行
```

## 7. 予防策

1. **マイグレーション管理の改善**
   - 重複するテーブル定義ファイルの削除
   - マイグレーション実行順序の明確化

2. **テストの追加**
   - INSERT文実行前のカラム存在確認
   - リポジトリ単体テストでの検証

## 8. 影響範囲

- ✅ **読み取り処理**: SELECT文は`SELECT *`のため影響なし
- ❌ **書き込み処理**: INSERT文でカラム名を明示的に指定しているため全て失敗
- ❌ **インポート処理**: マスタインポート機能が完全に停止

## 結論

SQLエラー207の原因は、**テーブル定義の重複により、古い簡易版のテーブルが作成され、リポジトリが期待する完全なカラム構成と一致していない**ことです。

**即座にデータベースの状態確認とテーブル再作成が必要**です。