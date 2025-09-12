# Claude Code参照用データ集

## 1. テーブル構造定義

### InventoryMaster（在庫マスタ）
```sql
-- 主キー：5項目（JobDateは含まれない）
PRIMARY KEY (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName)

-- カラム構造
ProductCode NVARCHAR(15) NOT NULL
GradeCode NVARCHAR(15) NOT NULL  
ClassCode NVARCHAR(15) NOT NULL
ShippingMarkCode NVARCHAR(15) NOT NULL
ShippingMarkName NVARCHAR(50) NOT NULL
ProductName NVARCHAR(100) NOT NULL
Unit NVARCHAR(10) NOT NULL
StandardPrice DECIMAL(12,4) NOT NULL DEFAULT 0
ProductCategory1 NVARCHAR(10) NOT NULL DEFAULT ''
ProductCategory2 NVARCHAR(10) NOT NULL DEFAULT ''
JobDate DATE NOT NULL
CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE()
UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE()
CurrentStock DECIMAL(9,4) NOT NULL DEFAULT 0
CurrentStockAmount DECIMAL(12,4) NOT NULL DEFAULT 0
DailyStock DECIMAL(9,4) NOT NULL DEFAULT 0
DailyStockAmount DECIMAL(12,4) NOT NULL DEFAULT 0
DailyFlag NCHAR(1) NOT NULL DEFAULT '9'
DataSetId NVARCHAR(50) NOT NULL DEFAULT ''
DailyGrossProfit DECIMAL(12,4) NOT NULL DEFAULT 0
DailyAdjustmentAmount DECIMAL(12,4) NOT NULL DEFAULT 0
DailyProcessingCost DECIMAL(12,4) NOT NULL DEFAULT 0
FinalGrossProfit DECIMAL(12,4) NOT NULL DEFAULT 0
```

## 2. 現在の問題状況（実データ）

### 2025年6月12日の状況
```
売上伝票の商品種類：84種類
在庫マスタ登録数：10件（更新前）→ 90件（更新後、重複含む）
CP在庫マスタ：0件（在庫マスタ不足のため）
```

### アンマッチリストのエラー内容
```
すべて「該当無」エラー（在庫マスタに商品が存在しない）
在庫0エラー：0件（本来検出されるべきエラーが出ない）
```

## 3. 必要なSQLクエリ例

### 売上商品の取得
```sql
SELECT DISTINCT 
    ProductCode, 
    GradeCode, 
    ClassCode, 
    ShippingMarkCode, 
    ShippingMarkName
FROM SalesVouchers
WHERE CONVERT(date, JobDate) = @jobDate
    AND Quantity <> 0  -- 数量0は除外
```

### 在庫マスタの存在確認
```sql
SELECT COUNT(*) 
FROM InventoryMaster
WHERE ProductCode = @productCode
    AND GradeCode = @gradeCode
    AND ClassCode = @classCode
    AND ShippingMarkCode = @shippingMarkCode
    AND ShippingMarkName = @shippingMarkName
```

### MERGE文での一括処理（推奨）
```sql
MERGE InventoryMaster AS target
USING (
    SELECT DISTINCT
        s.ProductCode,
        s.GradeCode,
        s.ClassCode,
        s.ShippingMarkCode,
        s.ShippingMarkName,
        @jobDate as JobDate
    FROM SalesVouchers s
    WHERE CONVERT(date, s.JobDate) = @jobDate
) AS source
ON target.ProductCode = source.ProductCode
    AND target.GradeCode = source.GradeCode
    AND target.ClassCode = source.ClassCode
    AND target.ShippingMarkCode = source.ShippingMarkCode
    AND target.ShippingMarkName = source.ShippingMarkName
WHEN MATCHED AND target.JobDate <> source.JobDate THEN
    UPDATE SET 
        JobDate = source.JobDate,
        UpdatedDate = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
            JobDate, CreatedDate, UpdatedDate, CurrentStock, CurrentStockAmount,
            DailyStock, DailyStockAmount, DailyFlag, DataSetId,
            DailyGrossProfit, DailyAdjustmentAmount, DailyProcessingCost, FinalGrossProfit)
    VALUES (source.ProductCode, source.GradeCode, source.ClassCode, 
            source.ShippingMarkCode, source.ShippingMarkName,
            '商品名未設定', 'PCS', 0, '', '',
            source.JobDate, GETDATE(), GETDATE(), 0, 0,
            0, 0, '9', @dataSetId,
            0, 0, 0, 0);
```

## 4. 既存コードの参照ポイント

### ImportFolderCommandHandler
- ExecuteAsync メソッドの処理フロー
- トランザクション管理の方法
- ログ出力の形式

### リポジトリインターフェース
```csharp
public interface IInventoryRepository
{
    Task<IEnumerable<Inventory>> GetByJobDateAsync(DateTime jobDate);
    Task<int> UpdateJobDateAsync(string productCode, string gradeCode, 
        string classCode, string shippingMarkCode, string shippingMarkName, 
        DateTime newJobDate);
    Task<int> InsertAsync(Inventory inventory);
}
```

## 5. エラーハンドリングの考慮事項

### 主キー重複エラー
```
Violation of PRIMARY KEY constraint 'PK_InventoryMaster'. 
Cannot insert duplicate key in object 'dbo.InventoryMaster'. 
The duplicate key value is (00104, 510, 032, 5115,         ).
```

### 対処方法
- INSERT前に存在確認
- またはMERGE文を使用
- エラーをキャッチして更新処理に切り替え

## 6. パフォーマンス要件

- 売上商品数：日次50-150種類
- 処理時間目標：1分以内
- バッチサイズ：1000件単位

## 7. 検証用データ

### 正常動作の確認
```sql
-- 処理前
SELECT COUNT(*) FROM InventoryMaster WHERE JobDate = '2025-06-12';  -- 10件

-- 処理後（期待値）
SELECT COUNT(*) FROM InventoryMaster WHERE JobDate = '2025-06-12';  -- 84件
```

## 8. 関連ファイルパス

```
src/InventorySystem.Console/Commands/ImportFolderCommandHandler.cs
src/InventorySystem.Data/Repositories/InventoryRepository.cs
src/InventorySystem.Core/Interfaces/IInventoryRepository.cs
src/InventorySystem.Core/Services/CPInventoryService.cs
```