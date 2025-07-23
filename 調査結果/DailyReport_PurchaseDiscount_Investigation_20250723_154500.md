# 商品日報 仕入値引表示問題調査結果

## 調査日時
2025年07月23日 15:45:00

## 1. 問題の概要
- データベースに仕入値引データ（明細種別3）は存在する
- 商品日報の「仕入値引」列に表示されない
- 特に商品15020の-19,900円が表示されない

## 2. ストアドプロシージャの実装状況
### sp_CreateDailyReportData
**結果**: 🚨 **専用ストアドプロシージャが存在しない**
- 商品日報用の専用ストアドプロシージャ`sp_CreateDailyReportData`は実装されていない
- データ取得はDailyReportService.csで直接CP在庫マスタからクエリしている

## 3. モデルクラスの実装状況
### DailyReportItem.cs
**結果**: ✅ **正常に実装済み**
- `DailyPurchaseDiscount`プロパティが存在（58行目）
- 型：decimal、コメント：「3. 仕入値引: ZZ,ZZZ,ZZ9-」

## 4. FastReportサービスの実装状況
### DailyReportFastReportService.cs
**結果**: ✅ **正常に実装済み**
- DataTableに「PurchaseDiscount」カラムが定義されている（252行目、311行目、396行目）
- データマッピング処理は正しく実装
- フォーマット関数`FormatNumberWithMinus`も適切

## 5. FastReportテンプレートの設定
### DailyReport.frx
**結果**: ✅ **正常に実装済み**
- 「仕入値引」列のヘッダーが存在（18行目）
- 合計行の表示オブジェクト「TotalPurchaseDiscount」が存在（38行目）
- データバインディング設定は適切

## 6. CP在庫マスタの状況
### CpInventoryRepository.cs
**結果**: ⚠️ **実装に問題あり**

#### 仕入値引集計処理（757-781行目）
```csharp
// CalculatePurchaseDiscountAsync メソッド
UPDATE cp
SET cp.DailyDiscountAmount = ISNULL(pv.DiscountAmount, 0)  // ← 問題箇所
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
) pv ON ...
```

**問題点**: 仕入値引データを`DailyDiscountAmount`に保存している

#### DailyReportService.cs（316行目）
```csharp
DailyPurchaseDiscount = group.Sum(cp => cp.DailyDiscountAmount),  // ← 問題箇所
```

**問題点**: `DailyDiscountAmount`は歩引額用のフィールドであり、仕入値引とは別の概念

### SQL実行結果
**クエリ4.json** より：
- 仕入伝票の明細種別3データは正しく存在する
- しかし、CP在庫マスタの多くのレコードで「CP在庫_仕入値引」が0.0000となっている
- 一部のレコードのみに仕入値引データが設定されている状況

## 7. 問題の原因

### 根本原因：フィールドの混同
1. **`DailyDiscountAmount`**: 本来は歩引額（得意先マスタの歩引き率×売上金額）用
2. **仕入値引**: 仕入明細種別3のデータ、専用フィールドが必要

### データフローの問題
```
仕入伝票（明細種別3）
    ↓
CpInventoryRepository.CalculatePurchaseDiscountAsync()
    ↓ 
DailyDiscountAmount（歩引額用フィールド）に保存  ← 問題
    ↓
DailyReportService.cs（316行目）
    ↓
DailyPurchaseDiscount（仕入値引として表示）
```

### 競合問題
- `CalculatePurchaseDiscountAsync`（仕入値引）と`CalculateWalkingAmountAsync`（歩引額）が同一フィールド`DailyDiscountAmount`を使用
- 後から実行された処理が前の値を上書きしている可能性

## 8. 修正が必要な箇所

### 1. CpInventoryMasterテーブルの拡張
**新規カラム追加が必要**:
```sql
ALTER TABLE CpInventoryMaster 
ADD DailyPurchaseDiscountAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
```

### 2. CpInventoryRepository.cs修正
**CalculatePurchaseDiscountAsync メソッド（761行目）**:
```csharp
// 修正前
SET cp.DailyDiscountAmount = ISNULL(pv.DiscountAmount, 0)

// 修正後
SET cp.DailyPurchaseDiscountAmount = ISNULL(pv.DiscountAmount, 0)
```

### 3. DailyReportService.cs修正
**316行目**:
```csharp
// 修正前
DailyPurchaseDiscount = group.Sum(cp => cp.DailyDiscountAmount),

// 修正後
DailyPurchaseDiscount = group.Sum(cp => cp.DailyPurchaseDiscountAmount),
```

### 4. CpInventoryMasterエンティティ修正
**新プロパティ追加**:
```csharp
public decimal DailyPurchaseDiscountAmount { get; set; }
```

## 9. 実行順序の確認

現在のDailyReportService.csの実行順序（83-90行目）：
1. `CalculatePurchaseDiscountAsync` （仕入値引 → DailyDiscountAmount）
2. `CalculateIncentiveAsync` （奨励金）
3. `CalculateWalkingAmountAsync` （歩引額 → DailyDiscountAmount）

**問題**: 3番目の歩引額計算で1番目の仕入値引データが上書きされている

## 10. 検証方法

修正後の検証SQL：
```sql
-- 1. 新カラムの仕入値引データ確認
SELECT 
    ProductCode,
    DailyPurchaseDiscountAmount as 仕入値引,
    DailyDiscountAmount as 歩引額
FROM CpInventoryMaster
WHERE JobDate = '2025-06-02'
    AND (DailyPurchaseDiscountAmount != 0 OR DailyDiscountAmount != 0)
ORDER BY ProductCode;

-- 2. 商品15020の詳細確認
SELECT 
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
    DailyPurchaseDiscountAmount,
    DailyDiscountAmount
FROM CpInventoryMaster
WHERE ProductCode = '15020' AND JobDate = '2025-06-02';
```

## 11. 結論

**仕入値引が表示されない原因**は、**仕入値引データと歩引額データが同一フィールド`DailyDiscountAmount`を使用している**ことです。

**修正方針**:
1. 仕入値引専用フィールド`DailyPurchaseDiscountAmount`を追加
2. 仕入値引と歩引額の計算処理を分離
3. DailyReportServiceで正しいフィールドを参照

この修正により、商品15020の-19,900円の仕入値引が正しく商品日報に表示されるようになります。

## 12. 修正優先度

**最高優先度**: CpInventoryMasterテーブルの拡張とRepository修正
**高優先度**: DailyReportServiceの参照フィールド修正
**中優先度**: エンティティクラスのプロパティ追加