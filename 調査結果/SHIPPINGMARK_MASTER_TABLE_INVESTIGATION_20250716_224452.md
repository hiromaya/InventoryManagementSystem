# ShippingMarkMasterテーブル不在エラー調査結果
実行日時: 2025-07-16 22:44:52

## 1. エラー概要
- **エラー内容**: `Invalid object name 'ShippingMarkMaster'. Error Number:208,State:1,Class:16`
- **発生箇所**: ShippingMarkMasterRepository.cs:36（GetByCodeAsync内のSELECT文）
- **影響範囲**: 荷印マスタのインポート機能、荷印マスタの全操作

## 2. データベース定義調査

### 2.1 CreateDatabase.sql
**調査結果**: ShippingMarkMasterテーブルの定義が**完全に存在しない**

```sql
-- 検索結果: ShippingMarkMaster -> 0件
-- 検索結果: ShippingMark -> 12件（すべて他テーブルのカラム名）
```

**発見されたShippingMark関連項目**:
- `InventoryMaster.ShippingMarkCode`
- `InventoryMaster.ShippingMarkName`
- `CpInventoryMaster.ShippingMarkCode`
- `CpInventoryMaster.ShippingMarkName`
- `SalesVouchers.ShippingMarkCode`
- `SalesVouchers.ShippingMarkName`
- `PurchaseVouchers.ShippingMarkCode`
- `PurchaseVouchers.ShippingMarkName`
- `InventoryAdjustments.ShippingMarkCode`
- `InventoryAdjustments.ShippingMarkName`

**重要な発見**: 
- 荷印情報は各テーブルで**カラム**として定義されている
- **独立したShippingMarkMasterテーブルは存在しない**

### 2.2 マイグレーションファイル
**調査結果**: ShippingMarkMaster関連のマイグレーションファイルは**存在しない**

**確認したマイグレーション**:
- `024_CreateProductMaster.sql` - ProductMaster, CustomerMaster, SupplierMasterを作成
- 他のマイグレーション - ShippingMarkMasterの作成なし

**他のマスタテーブルとの比較**:
- ✅ **ProductMaster**: 024_CreateProductMaster.sqlで作成
- ✅ **CustomerMaster**: 024_CreateProductMaster.sqlで作成
- ✅ **SupplierMaster**: 024_CreateProductMaster.sqlで作成
- ❌ **ShippingMarkMaster**: 作成マイグレーションが存在しない

### 2.3 DatabaseInitializationService
**調査結果**: マイグレーション順序リストに関連項目なし

```csharp
// 028_AddDataSetTypeAndImportedAt.sql が最新
// ShippingMarkMaster作成マイグレーションは含まれていない
```

## 3. 実装調査

### 3.1 ShippingMarkMasterRepository
**ファイル**: `src/InventorySystem.Data/Repositories/Masters/ShippingMarkMasterRepository.cs`

**テーブル名の使用状況**:
```csharp
// 36行目: GetByCodeAsync
FROM ShippingMarkMaster WHERE ShippingMarkCode = @ShippingMarkCode

// 56行目: GetAllAsync  
FROM ShippingMarkMaster ORDER BY ShippingMarkCode

// 69行目: UpsertAsync
MERGE ShippingMarkMaster AS target

// 117行目: BulkUpsertAsync
MERGE ShippingMarkMaster AS target
```

**問題点**: 
- すべてのSQL文で`ShippingMarkMaster`テーブルを参照
- しかし、このテーブルは**データベースに存在しない**

### 3.2 他マスタリポジトリとの比較

#### ProductMasterRepository
```csharp
// 28行目: 正常に動作
SELECT * FROM ProductMaster WHERE ProductCode = @ProductCode
```

#### GradeMasterRepository  
```csharp
// 異なるアプローチ: データベーステーブルではなくCSVファイル読み込み
SELECT COUNT(*) FROM GradeMaster  // テーブルも存在
```

#### ClassMasterRepository
```csharp
// 異なるアプローチ: データベーステーブルではなくCSVファイル読み込み
SELECT COUNT(*) FROM ClassMaster  // テーブルも存在
```

**実装パターンの違い**:
1. **ProductMaster**: データベーステーブル使用（✅正常動作）
2. **GradeMaster**: CSVファイル＋データベーステーブル併用（✅正常動作）
3. **ClassMaster**: CSVファイル＋データベーステーブル併用（✅正常動作）
4. **ShippingMarkMaster**: データベーステーブル使用（❌テーブル不在）

## 4. 削除された検証ロジック
**調査結果**: 特に削除された検証ロジックの痕跡は見つからず

**ShippingMarkMasterImportService.cs**:
- 88行目付近にエラー発生箇所があるが、削除されたコードの痕跡なし
- 荷印名の空欄チェック等の削除されたロジックは確認されず

## 5. 根本原因

### 主要な原因: **設計上の不整合**

1. **テーブル定義の欠如**
   - ShippingMarkMasterテーブルがデータベースに存在しない
   - CreateDatabase.sqlに定義されていない
   - マイグレーションファイルで作成されていない

2. **設計方針の不統一**
   - 他のマスタ（Grade, Class）: CSVファイル読み込み方式
   - ShippingMarkMaster: データベーステーブル方式（しかしテーブルが存在しない）

3. **実装の不完全性**
   - ShippingMarkMasterRepository: 完全に実装済み
   - ShippingMarkMasterImportService: 完全に実装済み
   - **データベーステーブルのみ未作成**

## 6. 修正方針

### 6.1 即座の修正（推奨）
**ShippingMarkMasterテーブルの作成**

**新しいマイグレーションファイルの作成**:
```sql
-- 029_CreateShippingMarkMaster.sql
CREATE TABLE ShippingMarkMaster (
    ShippingMarkCode NVARCHAR(15) NOT NULL PRIMARY KEY,
    ShippingMarkName NVARCHAR(100) NOT NULL,
    SearchKana NVARCHAR(100) NULL,
    NumericValue1 DECIMAL(18,4) NULL,
    NumericValue2 DECIMAL(18,4) NULL,
    NumericValue3 DECIMAL(18,4) NULL,
    NumericValue4 DECIMAL(18,4) NULL,
    NumericValue5 DECIMAL(18,4) NULL,
    DateValue1 DATE NULL,
    DateValue2 DATE NULL,
    DateValue3 DATE NULL,
    DateValue4 DATE NULL,
    DateValue5 DATE NULL,
    TextValue1 NVARCHAR(100) NULL,
    TextValue2 NVARCHAR(100) NULL,
    TextValue3 NVARCHAR(100) NULL,
    TextValue4 NVARCHAR(100) NULL,
    TextValue5 NVARCHAR(100) NULL
);
```

**DatabaseInitializationService.csの更新**:
```csharp
"028_AddDataSetTypeAndImportedAt.sql",       // 既存
"029_CreateShippingMarkMaster.sql"           // 新規追加
```

### 6.2 代替案（設計変更）
**CSVファイル読み込み方式への変更**
- GradeMaster、ClassMasterと同じアプローチ
- ShippingMarkMasterRepositoryの大幅な修正が必要

## 7. 影響範囲

### 7.1 影響を受ける機能
- `import-folder`コマンド（荷印マスタインポート）
- 荷印マスタの全CRUD操作
- 荷印マスタを参照する全機能

### 7.2 影響を受けない機能
- 他のマスタテーブル（Product, Customer, Supplier）
- 荷印情報を直接カラムとして持つテーブル（InventoryMaster等）

## 8. 追加確認事項

### 8.1 実装の完全性確認
- ShippingMarkMasterエンティティクラスの構造確認
- CSVマッピングクラスの確認
- インポートサービスの詳細確認

### 8.2 他の不整合の確認
- RegionMaster（産地マスタ）の状況確認
- 他のマスタテーブルの定義確認

### 8.3 テスト確認
- 修正後の動作確認テスト
- 他マスタテーブルへの影響確認

## 9. 修正の優先度

**高**: ShippingMarkMasterテーブルの作成（マイグレーション追加）
**中**: DatabaseInitializationService.csの更新
**低**: 動作確認テストの実施

## 10. 検証方法

1. 新しいマイグレーション作成後に`init-database --force`実行
2. ShippingMarkMasterテーブルが正常に作成されることを確認
3. `import-folder DeptA 2025-06-01`コマンドを実行
4. 「Invalid object name 'ShippingMarkMaster'」エラーが解消されることを確認

---

**調査完了**: 2025-07-16 22:44:52  
**結論**: ShippingMarkMasterテーブルが完全に未作成。マイグレーションファイルを作成してテーブルを作成することで解決可能。リポジトリとサービスの実装は完了しているため、テーブル作成のみで機能が動作するようになる。