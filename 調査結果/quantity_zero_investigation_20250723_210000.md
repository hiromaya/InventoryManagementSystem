# 数量0レコード処理の現状調査結果

作成日時: 2025-07-23 21:00:00

## 概要

在庫管理システムにおける数量0レコードの処理状況を詳細に調査しました。この調査では、CSV取込処理、バリデーション、およびアンマッチリスト生成における数量0レコードの扱いについて包括的に分析しました。

## 1. クエリフォルダの調査結果

### 1.1 確認したJSONファイル
- `/クエリ/1.json`: 空のJSONファイル
- `/クエリ/2.json`: 空のJSONファイル
- `/クエリ/3.json`: 空のJSONファイル
- `/クエリ/4.json`: CategoryCode列の定義情報のみ（int型、NULL許可）

### 1.2 重要な発見
- CategoryCodeがint型でNULL許可として定義されている
- 他のファイルには有効なデータが含まれていない

## 2. CSV取込処理の実装状況

### 2.1 売上伝票取込（SalesVoucherImportService）

#### 2.1.1 数量0チェック箇所
- **ファイルパス**: `src/InventorySystem.Import/Services/SalesVoucherImportService.cs`
- **実装状況**: **数量0チェックなし**（重要な発見）
- **フィルタリング処理**: 行143-175で商品コード・得意先コードのチェックのみ実施
- **明細種別による分岐**: あり（DetailType 1,2,3,4を許可）
- **処理結果**: 数量0レコードも通常通り処理される

#### 2.1.2 重要な発見
```csharp
// 売上伝票取込サービスでは数量0のフィルタリングが実装されていない
// 以下のチェックのみ実装:
if (CodeValidator.IsExcludedCode(record.CustomerCode)) // 得意先コード
if (CodeValidator.IsExcludedCode(record.ProductCode))  // 商品コード
if (!record.IsValidSalesVoucher()) // バリデーション
```

### 2.2 仕入伝票取込（PurchaseVoucherImportService）

#### 2.2.1 数量0チェック箇所
- **ファイルパス**: `src/InventorySystem.Import/Services/PurchaseVoucherImportService.cs`
- **実装状況**: **数量0チェックなし**（重要な発見）
- **フィルタリング処理**: 行107-120で商品コード・仕入先コードのチェックのみ
- **明細種別による分岐**: あり（DetailType 1,2,3,4を許可）
- **処理結果**: 数量0レコードも通常通り処理される

### 2.3 在庫調整取込（InventoryAdjustmentImportService）

#### 2.3.1 数量0チェック箇所
- **ファイルパス**: `src/InventorySystem.Import/Services/InventoryAdjustmentImportService.cs`
- **実装状況**: **数量0チェックなし**（重要な発見）
- **特殊処理**: 行102-105で集計行（IsSummaryRow）のスキップあり
- **明細種別による分岐**: なし（在庫調整は受注伝票形式のため）
- **処理結果**: 数量0レコードも通常通り処理される

## 3. CSVモデルのバリデーション実装

### 3.1 売上伝票CSVモデル（SalesVoucherDaijinCsv）

#### 3.1.1 IsValidSalesVoucherメソッド（行242-293）
```csharp
// 数量0は除外
if (Quantity == 0)
{
    return false;
}
```
- **行番号**: 260-263
- **処理内容**: **数量0レコードを無効と判定**
- **明細種別分岐**: あり（DetailType 1,2,3,4を許可）
- **処理結果**: 数量0レコードはバリデーション失敗

#### 3.1.2 GetValidationErrorメソッド（行298-350）
```csharp
// 数量0は除外
if (Quantity == 0)
{
    return "数量が0";
}
```
- **行番号**: 313-316
- **処理内容**: "数量が0"エラーメッセージを返す

### 3.2 仕入伝票CSVモデル（PurchaseVoucherDaijinCsv）

#### 3.2.1 IsValidPurchaseVoucherメソッド（行217-274）
```csharp
// 数量0は除外
if (Quantity == 0)
{
    return false;
}
```
- **行番号**: 235-238
- **処理内容**: **数量0レコードを無効と判定**
- **明細種別分岐**: あり（DetailType 1,2,3,4を許可）
- **処理結果**: 数量0レコードはバリデーション失敗

#### 3.2.2 GetValidationErrorメソッド（行279-337）
```csharp
// 数量0は除外
if (Quantity == 0)
{
    return "数量が0";
}
```
- **行番号**: 294-297
- **処理内容**: "数量が0"エラーメッセージを返す

### 3.3 在庫調整CSVモデル（InventoryAdjustmentDaijinCsv）

#### 3.3.1 IsValidInventoryAdjustmentメソッド（行221-267）
```csharp
// 数量0は除外
if (Quantity == 0)
{
    return false;
}
```
- **行番号**: 236-239
- **処理内容**: **数量0レコードを無効と判定**
- **CategoryCode/UnitCodeの扱い**: CategoryCodeのみ使用（UnitCode概念は未実装）
- **処理結果**: 数量0レコードはバリデーション失敗

#### 3.3.2 重要な実装詳細
- **CategoryCode処理**: 行249-257でAdjustmentType列挙型との整合性チェック
- **区分コード定義**: 1:ロス、4:振替、6:調整（0-6すべて許可）
- **UnitCode**: 実装されていない（仕様との差異）

## 4. アンマッチリスト処理の実装状況

### 4.1 UnmatchListServiceの数量0除外処理

#### 4.1.1 売上伝票チェック（CheckSalesUnmatchAsync）
- **ファイルパス**: `src/InventorySystem.Core/Services/UnmatchListService.cs`
- **行番号**: 356-475
- **数量0除外処理**: 行396で実装
```csharp
.Where(s => s.Quantity != 0)  // 数量0以外
```
- **実装箇所**: フィルタリングクエリ内で事前除外
- **処理方法**: 事前フィルタリング

#### 4.1.2 仕入伝票チェック（CheckPurchaseUnmatchAsync）
- **行番号**: 477-538
- **数量0除外処理**: 行500で実装
```csharp
.Where(p => p.Quantity != 0)  // 数量0以外
```

#### 4.1.3 在庫調整チェック（CheckInventoryAdjustmentUnmatchAsync）
- **行番号**: 596-660
- **数量0除外処理**: 行619で実装
```csharp
.Where(a => a.Quantity > 0)  // 数量 > 0
```
- **特別な実装**: 在庫調整のみ `> 0` を使用（`!= 0` ではない）
- **追加の除外条件**: 
  - 行621で区分2,5（経費、加工費B）を除外
  - CategoryCode.HasValue()チェック

## 5. 現状の問題点と改善提案

### 5.1 問題点

#### 5.1.1 CSV取込サービスとバリデーションの矛盾
- **問題**: CSV取込サービスでは数量0チェックを行わないが、CSVモデルのバリデーションでは数量0を無効と判定
- **影響**: バリデーションエラーとしてログに記録されるが、数量0レコードも実際にはデータベースに保存される可能性

#### 5.1.2 仕様との差異
- **共通仕様**: 「数量が0の行は処理しない」
- **現在の実装**: 
  - CSV取込段階では処理される
  - アンマッチリスト段階で除外される
- **理想的な実装**: CSV取込段階で除外すべき

#### 5.1.3 在庫調整における単位コード（UnitCode）の未実装
- **仕様**: 「明細種別は無視して単位コードで判断」
- **現実**: CategoryCodeのみ実装、UnitCodeは存在しない
- **影響**: 仕様書の要求を満たしていない

### 5.2 改善提案

#### 5.2.1 CSV取込サービスでの数量0除外実装
```csharp
// 各CSV取込サービスに追加すべきコード例
if (record.Quantity == 0)
{
    _logger.LogInformation("行{index}: 数量が0のためスキップします。伝票番号: {VoucherNumber}", 
        index, record.VoucherNumber);
    skippedCount++;
    continue;
}
```

#### 5.2.2 在庫調整での数量チェック統一
- 現在: `Quantity > 0` 
- 提案: `Quantity != 0` で統一（負の数量も考慮）

#### 5.2.3 単位コード（UnitCode）の実装検討
- CategoryCodeとUnitCodeの関係性を明確化
- 仕様書に基づく単位コード01-06の実装

## 6. SQL実行結果の分析

### 6.1 CategoryCode列の確認
- **データ型**: int
- **NULL許可**: YES
- **用途**: 在庫調整での区分管理（1:ロス、4:振替、6:調整）

## 7. 結論

### 7.1 調査結果の総括

1. **CSV取込段階**: 数量0レコードの除外処理は**実装されていない**
2. **バリデーション段階**: すべてのCSVモデルで数量0を**無効と判定**
3. **アンマッチリスト段階**: 数量0レコードを**事前フィルタリングで除外**

### 7.2 重要な仕様違反

- **仕様**: 「数量が0の行は処理しない」
- **現実**: CSV取込段階では処理され、後段で除外される
- **推奨**: CSV取込段階での除外実装が必要

### 7.3 優先度の高い改善項目

1. **高**: CSV取込サービスでの数量0除外処理追加
2. **中**: 在庫調整の数量チェック条件統一
3. **低**: 単位コード（UnitCode）の実装検討

### 7.4 トラブルシューティング

- 数量0レコードが意図せずデータベースに保存されている場合、CSVモデルのバリデーション実装とCSV取込サービスの処理の差異が原因
- アンマッチリストに数量0レコードが表示されない場合は正常（設計通り）
- 在庫調整で負の数量を扱う場合、現在の `> 0` 条件では除外される

---

**調査担当**: Claude Code  
**調査期間**: 2025-07-23  
**対象システム**: InventoryManagementSystem v2.0  
**調査対象**: CSV取込処理、バリデーション、アンマッチリスト生成