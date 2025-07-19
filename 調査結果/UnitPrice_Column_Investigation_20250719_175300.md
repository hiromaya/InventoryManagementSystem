# InventoryMaster UnitPriceカラム問題 調査報告書

## 調査日時
2025-07-19 17:53:00

## エラー概要
- エラーメッセージ: Invalid column name 'UnitPrice'
- 発生箇所: InventoryMasterOptimizationService.InheritPreviousDayInventoryAsync
- 発生コマンド: dotnet run import-folder DeptA 2025-06-02

## 1. データベーススキーマ

### InventoryMasterテーブル定義（docs/database/01_create_tables.sql）

```sql
CREATE TABLE InventoryMaster (
    -- 複合キー（5項目）
    ProductCode NVARCHAR(15) NOT NULL,
    GradeCode NVARCHAR(15) NOT NULL,
    ClassCode NVARCHAR(15) NOT NULL,
    ShippingMarkCode NVARCHAR(15) NOT NULL,
    ShippingMarkName NVARCHAR(50) NOT NULL,
    
    -- 基本情報
    ProductName NVARCHAR(100) NOT NULL,
    Unit NVARCHAR(10) NOT NULL,
    StandardPrice DECIMAL(18,4) NOT NULL DEFAULT 0,
    ProductCategory1 NVARCHAR(10) NOT NULL DEFAULT '',
    ProductCategory2 NVARCHAR(10) NOT NULL DEFAULT '',
    
    -- 日付管理
    JobDate DATE NOT NULL,
    CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    -- 在庫情報
    CurrentStock DECIMAL(18,4) NOT NULL DEFAULT 0,
    CurrentStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
    DailyStock DECIMAL(18,4) NOT NULL DEFAULT 0,
    DailyStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
    
    -- 当日発生フラグ
    DailyFlag CHAR(1) NOT NULL DEFAULT '9',
    
    -- 粗利情報
    DailyGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,
    DailyAdjustmentAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
    DailyProcessingCost DECIMAL(18,4) NOT NULL DEFAULT 0,
    FinalGrossProfit DECIMAL(18,4) NOT NULL DEFAULT 0,
    
    -- データセットID管理
    DataSetId NVARCHAR(50) NOT NULL DEFAULT ''
);
```

### 確認されたカラム名
- ProductCode
- GradeCode  
- ClassCode
- ShippingMarkCode
- ShippingMarkName
- ProductName
- Unit
- StandardPrice
- ProductCategory1
- ProductCategory2
- JobDate
- CreatedDate
- UpdatedDate
- CurrentStock
- CurrentStockAmount
- DailyStock
- DailyStockAmount
- DailyFlag
- DailyGrossProfit
- DailyAdjustmentAmount
- DailyProcessingCost
- FinalGrossProfit
- DataSetId

**重要**: InventoryMasterテーブルには**UnitPriceカラムは定義されていない**

## 2. エンティティクラス

### InventoryMaster.cs のプロパティ定義

```csharp
public class InventoryMaster
{
    public InventoryKey Key { get; set; } = new();
    
    // 基本情報
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal StandardPrice { get; set; }                   // StandardPrice（UnitPriceではない）
    public decimal AveragePrice { get; set; }
    public int PersonInChargeCode { get; set; }
    public string ProductCategory1 { get; set; } = string.Empty;
    public string ProductCategory2 { get; set; } = string.Empty;
    
    // 在庫情報
    public decimal CurrentStock { get; set; }
    public decimal CurrentStockAmount { get; set; }
    public decimal DailyStock { get; set; }
    public decimal DailyStockAmount { get; set; }
    public decimal PreviousMonthQuantity { get; set; }
    public decimal PreviousMonthAmount { get; set; }
    
    // その他のプロパティ...
}
```

**重要**: エンティティクラスにも**UnitPriceプロパティは存在しない**

## 3. 問題のSQL文

### InheritPreviousDayInventoryAsyncメソッドのSQL文（468行目）

```sql
INSERT INTO InventoryMaster (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
    ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
    JobDate, CreatedDate, UpdatedDate,
    CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount, DailyFlag,
    PreviousMonthQuantity, PreviousMonthAmount, UnitPrice  -- ← 問題のカラム
)
SELECT 
    prev.ProductCode, prev.GradeCode, prev.ClassCode, 
    prev.ShippingMarkCode, prev.ShippingMarkName,
    prev.ProductName, prev.Unit, prev.StandardPrice, 
    prev.ProductCategory1, prev.ProductCategory2,
    @JobDate, GETDATE(), GETDATE(),
    prev.CurrentStock, prev.CurrentStockAmount,
    prev.CurrentStock, prev.CurrentStockAmount,
    '9',
    prev.PreviousMonthQuantity, prev.PreviousMonthAmount,
    prev.UnitPrice  -- ← 問題のカラム
FROM InventoryMaster prev
WHERE...
```

### UnitPrice参照箇所
- **468行目**: INSERT文のカラムリストに`UnitPrice`
- **480行目**: SELECT文で`prev.UnitPrice`

## 4. 他のサービスでの実装パターン

UnitPriceを参照している他のファイル（29ファイル発見）では、主に以下のパターンで使用：

1. **CSV関連モデル**: `SalesVoucherDaijinCsv`, `PurchaseVoucherDaijinCsv`等でCSVカラムとして使用
2. **伝票エンティティ**: `SalesVoucher`, `PurchaseVoucher`で単価情報として使用
3. **他テーブル**: `PreviousMonthInventory`テーブルでは実際にUnitPriceカラムが存在

## 5. カラム名の不整合まとめ

| 場所 | 使用されている名前 | 正しい名前 |
|------|-------------------|------------|
| InventoryMasterOptimizationService.cs:468 | UnitPrice | StandardPrice |
| InventoryMasterOptimizationService.cs:480 | UnitPrice | StandardPrice |
| データベーススキーマ | StandardPrice | StandardPrice |
| エンティティクラス | StandardPrice | StandardPrice |

## 6. 修正が必要な箇所

1. **InventoryMasterOptimizationService.cs:468** - INSERT文のカラムリストから`UnitPrice`を削除または`StandardPrice`に変更
2. **InventoryMasterOptimizationService.cs:480** - SELECT文の`prev.UnitPrice`を`prev.StandardPrice`に変更

## 7. 推奨される修正方針

**統一すべきカラム名**: `StandardPrice`

### 理由:
1. データベーススキーマで定義されているカラム名が`StandardPrice`
2. エンティティクラスでも`StandardPrice`プロパティが定義済み
3. 他のテーブル（PreviousMonthInventory等）では`UnitPrice`が存在するが、InventoryMasterでは`StandardPrice`が正しい
4. 一貫性を保つため、InventoryMasterに関してはすべて`StandardPrice`で統一する

### 具体的な修正内容:

```sql
-- 修正前
INSERT INTO InventoryMaster (..., UnitPrice)
SELECT ..., prev.UnitPrice

-- 修正後
INSERT INTO InventoryMaster (..., StandardPrice)  -- または UnitPrice を削除
SELECT ..., prev.StandardPrice
```

## 8. 関連する注意事項

1. **PreviousMonthInventoryテーブル**: このテーブルには実際に`UnitPrice`カラムが存在するため、混同に注意
2. **マイグレーション履歴**: 014_AddMissingColumnsToInventoryMaster.sql では`UnitPrice`カラムの追加は行われていない
3. **他のサービス**: InventoryMaster以外のテーブルを扱うサービスでは`UnitPrice`が正しい場合がある

## 結論

InventoryMasterテーブルには`UnitPrice`カラムが存在せず、`StandardPrice`カラムを使用すべきです。InventoryMasterOptimizationService.csの468行目と480行目を修正することで、エラーが解決される見込みです。