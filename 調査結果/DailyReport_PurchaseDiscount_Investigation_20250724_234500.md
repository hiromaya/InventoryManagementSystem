# 商品日報 仕入値引・歩引き額 実装調査結果

## 調査日時
2025年07月24日 23:45:00

## 調査対象
商品日報において「仕入値引」と「歩引き額」が正しく表示されない問題の原因調査

## 1. CpInventoryRepository.cs の実装状況

### 1.1 仕入値引計算メソッド（CalculatePurchaseDiscountAsync）
**場所**: 行761-785
```csharp
public async Task<int> CalculatePurchaseDiscountAsync(string dataSetId, DateTime jobDate)
{
    const string sql = @"
        UPDATE cp
        SET cp.DailyPurchaseDiscountAmount = ISNULL(pv.DiscountAmount, 0)
        FROM CpInventoryMaster cp
        LEFT JOIN (
            SELECT 
                ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
                SUM(Amount) as DiscountAmount
            FROM PurchaseVouchers
            WHERE JobDate = @jobDate
                AND VoucherType IN ('11', '12')
                AND DetailType = '3'  -- 単品値引
            GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
        ) pv ON cp.ProductCode = pv.ProductCode 
            -- 5項目複合キーでJOIN
        WHERE cp.DataSetId = @dataSetId";
}
```

**更新対象フィールド**: `DailyPurchaseDiscountAmount`
**抽出条件**: DetailType = '3' （単品値引）

### 1.2 歩引き額計算メソッド（CalculateWalkingAmountAsync）
**場所**: 行820-845
```csharp
public async Task<int> CalculateWalkingAmountAsync(string dataSetId, DateTime jobDate)
{
    const string sql = @"
        UPDATE cp
        SET cp.DailyWalkingAmount = ISNULL(sv.WalkingAmount, 0)
        FROM CpInventoryMaster cp
        LEFT JOIN (
            SELECT 
                sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ShippingMarkName,
                SUM(sv.Amount * ISNULL(cm.WalkingRate, 0) / 100) as WalkingAmount
            FROM SalesVouchers sv
            LEFT JOIN CustomerMaster cm ON sv.CustomerCode = cm.CustomerCode
            WHERE sv.JobDate = @jobDate
                AND sv.VoucherType IN ('51', '52')
                AND sv.DetailType IN ('1', '2', '3')
            GROUP BY sv.ProductCode, sv.GradeCode, sv.ClassCode, sv.ShippingMarkCode, sv.ShippingMarkName
        ) sv ON cp.ProductCode = sv.ProductCode 
        WHERE cp.DataSetId = @dataSetId";
}
```

**更新対象フィールド**: `DailyWalkingAmount`
**計算方法**: 売上金額 × 得意先マスタの歩引き率 / 100

### 1.3 重複処理の問題発見
**場所**: 行1019-1042
```csharp
// Step 6: 仕入値引き計算
const string calculateDiscountAmountSql = @"
    UPDATE cp
    SET cp.DailyDiscountAmount = ISNULL(disc.DiscountAmount, 0),  -- ★問題箇所★
        cp.UpdatedDate = GETDATE()
    FROM CpInventoryMaster cp
    LEFT JOIN (
        SELECT 
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            SUM(ABS(Amount)) as DiscountAmount
        FROM PurchaseVouchers
        WHERE JobDate = @JobDate
            AND VoucherType IN ('11', '12')
            AND DetailType = '3'
        GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    ) disc ON 
        -- 5項目複合キーでJOIN
    WHERE cp.DataSetId = @DataSetId";
```

**問題点**: 同じ仕入値引データを`DailyDiscountAmount`フィールドにも書き込んでいる（二重処理）

## 2. DailyReportService.cs の処理順序

### 2.1 InitializeProcessメソッドでの処理順序
**場所**: 行82-91
```csharp
// 経費項目の計算を追加
var discountResult = await _cpInventoryRepository.CalculatePurchaseDiscountAsync(context.DataSetId, reportDate);
_logger.LogInformation("仕入値引計算完了 - 更新件数: {Count}", discountResult);

var incentiveResult = await _cpInventoryRepository.CalculateIncentiveAsync(context.DataSetId, reportDate);
_logger.LogInformation("奨励金計算完了 - 更新件数: {Count}", incentiveResult);

var walkingResult = await _cpInventoryRepository.CalculateWalkingAmountAsync(context.DataSetId, reportDate);
_logger.LogInformation("歩引き金計算完了 - 更新件数: {Count}", walkingResult);
```

**実行順序**: 
1. 仕入値引計算 → `DailyPurchaseDiscountAmount`に設定
2. 奨励金計算
3. 歩引き計算 → `DailyWalkingAmount`に設定

### 2.2 粗利計算メソッド（CalculateGrossProfitAsync）での重複処理
**場所**: 行1019-1042で仕入値引を`DailyDiscountAmount`に再設定

## 3. CpInventoryMaster.cs エンティティ定義

### 3.1 フィールド定義の確認
**場所**: 行62-65
```csharp
public decimal DailyWalkingAmount { get; set; }              // 当日歩引き額
public decimal DailyIncentiveAmount { get; set; }            // 当日奨励金
public decimal DailyDiscountAmount { get; set; }             // 当日歩引き額 ★重複定義★
public decimal DailyPurchaseDiscountAmount { get; set; }     // 当日仕入値引き額
```

**問題点**: 
- `DailyWalkingAmount`と`DailyDiscountAmount`が両方とも「歩引き額」として定義されている
- コメントでは同じ用途だが、実際の使用方法が異なる可能性

## 4. DailyReportFastReportService.cs での表示

### 4.1 DailyReportItemへのマッピング
**場所**: 行316, 322
```csharp
DailyPurchaseDiscount = group.Sum(cp => cp.DailyPurchaseDiscountAmount),  // 正しいフィールド使用
DailyDiscountAmount = group.Sum(cp => cp.DailyDiscountAmount),           // 間違ったフィールド使用
```

**問題点**: 
- 仕入値引: 正しく`DailyPurchaseDiscountAmount`を使用
- 歩引き額: `DailyDiscountAmount`を使用（本来は`DailyWalkingAmount`を使用すべき）

### 4.2 FastReportでの表示項目
**場所**: 行252, 396
```csharp
// 明細行での歩引き表示
AddTextObject(dataBand, y, "PurchaseDiscount", 289.37f, FormatNumberWithMinus(item.DailyPurchaseDiscount), 87.47f);

// 合計行での歩引き表示  
SetTextObjectValue(report, "TotalPurchaseDiscount", FormatNumberWithMinus(total.GrandTotalDailyPurchaseDiscount));
```

## 5. CSVデータからの実態確認

### 5.1 CpInventoryMasterテーブル構造（クエリ/1.csv）
```
DailyWalkingAmount,decimal,NULL,12,4
DailyDiscountAmount,decimal,NULL,12,4
DailyPurchaseDiscountAmount,decimal,NULL,12,4
```

**確認事項**: 3つのフィールドすべてが存在している

### 5.2 仕入値引データ（クエリ/2.csv）
```
VoucherNumber,ProductCode,DetailType,Quantity,Amount,ShippingMarkCode,ShippingMarkName
0000000175,14900,3,0.0000,-1000.0000,9009,
0000000175,01501,3,0.0000,-2000.0000,9009,
```

**確認事項**: DetailType=3のデータが存在し、金額はマイナス値

### 5.3 期待されるフィールド（クエリ/4.csv）
```
CP在庫_仕入値引,CP在庫_奨励金
```

## 問題の根本原因

### 1. フィールド使用の混乱
- **仕入値引**: `DailyPurchaseDiscountAmount`（正しい）と`DailyDiscountAmount`（誤用）の二重管理
- **歩引き額**: `DailyWalkingAmount`（正しい）の計算結果が`DailyDiscountAmount`に表示されている

### 2. 処理順序の問題
1. `CalculatePurchaseDiscountAsync`で`DailyPurchaseDiscountAmount`に仕入値引を設定
2. `CalculateWalkingAmountAsync`で`DailyWalkingAmount`に歩引き額を設定
3. `CalculateGrossProfitAsync`で**仕入値引を`DailyDiscountAmount`に重複設定**

### 3. 表示ロジックの問題
- DailyReportServiceで`DailyDiscountAmount`を歩引き額として使用
- しかし実際には`DailyDiscountAmount`に仕入値引データが入っている

## 修正が必要な箇所

### 1. CpInventoryRepository.cs
- **行1022**: `DailyDiscountAmount`への仕入値引設定を削除
- 仕入値引は`DailyPurchaseDiscountAmount`のみに設定

### 2. DailyReportService.cs  
- **行322**: `DailyDiscountAmount`を`DailyWalkingAmount`に変更

### 3. エンティティクラスの整理
- `DailyDiscountAmount`フィールドの用途を明確化するか削除を検討

## 推奨修正手順

1. **Step 1**: CpInventoryRepository.csの`CalculateGrossProfitAsync`メソッドから仕入値引の`DailyDiscountAmount`設定を削除
2. **Step 2**: DailyReportService.csで歩引き額表示に`DailyWalkingAmount`を使用
3. **Step 3**: FastReportテンプレートでの表示項目確認
4. **Step 4**: 動作テストでデータの正確性を確認
