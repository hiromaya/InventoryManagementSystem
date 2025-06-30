# 在庫マスタ作成処理の修正（import-folderコマンド）

## 問題の概要
在庫マスタ（InventoryMaster）の作成が不完全で、売上・仕入伝票で使用される商品の組み合わせの大部分が在庫マスタに登録されていなかった。

### 具体例（6月12日）
- 必要な在庫マスタ：209件
- 実際に作成された在庫マスタ：10件のみ
- アンマッチリスト：199件（95%）

## 原因
`InventoryMasterOptimizationService.cs`のMERGE文で、`Quantity <> 0`という条件により、数量が0の伝票データが除外されていた。

```sql
-- 修正前（問題のあるコード）
WHERE CONVERT(date, JobDate) = @jobDate AND Quantity <> 0
```

数量0の伝票は以下のケースで発生：
- サンプル出荷
- 返品処理
- 在庫調整（0への修正）
- その他の特殊処理

## 修正内容

### 1. Quantity条件の削除
```sql
-- 修正後
WHERE CONVERT(date, JobDate) = @jobDate
```

これにより、数量に関係なくすべての伝票データから5項目キーの組み合わせを抽出。

### 2. ログ出力の改善
MERGE文の結果を詳細に記録：
- 新規作成件数
- 更新件数

```csharp
_logger.LogInformation(
    "在庫マスタMERGE完了 - 新規作成: {InsertCount}件, 更新: {UpdateCount}件", 
    insertCount, updateCount);
```

## 期待される効果

### 修正前
- 在庫マスタ作成：10件
- アンマッチリスト：199件（95%）

### 修正後（期待値）
- 在庫マスタ作成：209件
- アンマッチリスト：0-10件（0-5%）

## 技術的詳細

### 在庫マスタの5項目複合キー
1. ProductCode（商品コード）
2. GradeCode（等級コード）
3. ClassCode（階級コード）
4. ShippingMarkCode（荷印コード）
5. ShippingMarkName（荷印名 - 8桁固定）

### 初期値設定
- ProductName: '商品名未設定'
- Unit: 'PCS'
- 在庫数量・金額: 0
- DailyFlag: '9'（未処理）

## 確認方法

### SQLでの在庫マスタ件数確認
```sql
-- 特定日の在庫マスタ件数
SELECT COUNT(*) FROM InventoryMaster 
WHERE JobDate = '2025-06-12';

-- 売上・仕入伝票の5項目キー組み合わせ数
SELECT COUNT(DISTINCT ProductCode + GradeCode + ClassCode + ShippingMarkCode + ShippingMarkName)
FROM (
    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    FROM SalesVouchers WHERE JobDate = '2025-06-12'
    UNION
    SELECT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
    FROM PurchaseVouchers WHERE JobDate = '2025-06-12'
) AS combined;
```

## 変更ファイル
- `/src/InventorySystem.Data/Services/InventoryMasterOptimizationService.cs`