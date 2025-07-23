# 在庫単価ゼロ問題の包括的調査結果

## 🎯 調査概要

**調査日**: 2025-07-23  
**調査対象**: 売上伝票の在庫単価がゼロのまま残り、商品日報で異常な粗利率（145.32%、-2.39%）が発生する問題  
**参照データ**: クエリフォルダ 20.json（売上伝票469件）、21.json（CP在庫マスタ）

## 🔍 問題の特定

### 1. 根本原因
売上伝票の`InventoryUnitPrice`が**CSV取込時から一度も更新されない**ことが根本原因。

### 2. データフロー全体での問題点

```
【現状のデータフロー】
CSVインポート → 売上伝票(InventoryUnitPrice=0) → 商品日報生成
     ↓
CP在庫マスタ作成（DailyUnitPrice計算済み）
     ↓
売上伝票のInventoryUnitPriceは更新されない ← 問題箇所
```

## 📊 調査結果詳細

### 1. CSV取込処理の問題

**ファイル**: `src/InventorySystem.Import/Models/SalesVoucherCsv.cs`

```csharp
// 問題箇所：ToEntity()メソッド
public SalesVoucher ToEntity()
{
    return new SalesVoucher
    {
        // ... 他の項目は設定される
        InventoryUnitPrice = 0, // ← ここが常にゼロ
    };
}
```

**問題**: 販売大臣CSVに在庫単価の列がないため、`InventoryUnitPrice`は常に初期値0のまま。

### 2. CP在庫マスタでの計算は正常

**ファイル**: `src/InventorySystem.Core/Entities/CpInventoryMaster.cs`

**確認済み**:
- `DailyUnitPrice`プロパティは存在（27行目）
- 移動平均法による計算はストアドプロシージャで正常実行
- JSON 21.jsonで確認：DailyUnitPrice値が正しく計算されている
  - 例：-2078.5714、2700.0000、3333.3333など

### 3. 売上伝票更新処理の欠如

**ファイル**: `src/InventorySystem.Data/Repositories/CpInventoryRepository.cs`

```csharp
// CalculateGrossProfitAsyncメソッド（既存）
public async Task CalculateGrossProfitAsync(DateTime jobDate)
{
    // 粗利益は計算するが、InventoryUnitPriceの更新は行わない
    // Process 2-5の実装が欠如している
}
```

**問題**: CP在庫マスタで計算した`DailyUnitPrice`を売上伝票の`InventoryUnitPrice`に書き戻す処理が実装されていない。

### 4. 商品日報生成時の影響

**ファイル**: `src/InventorySystem.Core/Entities/DailyReportItem.cs`

**粗利率計算**: 146-153行目
```csharp
public static decimal CalculateGrossProfitRate(decimal grossProfit, decimal salesAmount)
{
    if (salesAmount == 0) return 0;
    var rate = (grossProfit / salesAmount) * 100;
    return Math.Round(rate, 2, MidpointRounding.AwayFromZero);
}
```

**結果**: 
- 売上伝票のInventoryUnitPrice=0により、粗利計算が不正確
- 異常な粗利率（145.32%、-2.39%）が発生

## 📈 データ検証結果

### JSON 20.json分析（売上伝票469件）
- **全件のInventoryUnitPriceが0**: 469件すべてが0.0000
- **売上単価は正常**: UnitPriceは適切な値が設定されている
- **金額計算は正常**: Amount = Quantity × UnitPriceの計算は正しい

### JSON 21.json分析（CP在庫マスタ）
- **在庫単価計算は正常**: DailyUnitPriceに適切な値
- **移動平均法計算**: 正常に実行されている
- **データ不整合**: 売上伝票との在庫単価の乖離が発生

## 🎯 解決すべき課題

### 1. 緊急度：高
**Process 2-5の実装**：CP在庫マスタのDailyUnitPriceを売上伝票のInventoryUnitPriceに書き戻す処理

### 2. 緊急度：高
**粗利計算の修正**：在庫単価ゼロ問題が解決されるまでの暫定対応

### 3. 緊急度：中
**データ整合性チェック**：売上伝票と在庫マスタ間の一貫性確保

## 💡 推奨される修正アプローチ

### アプローチ1：売上伝票更新処理の実装
```csharp
// 新規メソッド：UpdateSalesVoucherInventoryUnitPrice
public async Task UpdateSalesVoucherInventoryUnitPriceAsync(DateTime jobDate)
{
    // 1. CP在庫マスタから在庫単価を取得
    // 2. 売上伝票のInventoryUnitPriceを更新
    // 3. 粗利益を再計算
}
```

### アプローチ2：商品日報生成時の補正処理
```csharp
// DailyReportServiceでの補正
// CP在庫マスタのDailyUnitPriceを直接参照して粗利計算
```

### アプローチ3：在庫マスタ最適化タイミングでの一括更新
```csharp
// OptimizeInventoryMasterAsyncでの同時処理
// 在庫計算と売上伝票の在庫単価更新を一体化
```

## ⚠️ 関連する影響範囲

### 1. 商品日報（DailyReport）
- 粗利計算の不正確性
- 粗利率の異常値表示

### 2. 商品勘定帳票（ProductAccount）
- 在庫単価を使用する可能性（未確認）

### 3. アンマッチリスト処理
- 現在は影響なし（在庫単価を使用しない）

## 🔧 技術的な実装ガイダンス

### 1. 売上伝票テーブル更新SQL
```sql
UPDATE SalesVoucher 
SET InventoryUnitPrice = (
    SELECT DailyUnitPrice 
    FROM CpInventoryMaster 
    WHERE CpInventoryMaster.ProductCode = SalesVoucher.ProductCode
      AND CpInventoryMaster.JobDate = SalesVoucher.JobDate
    -- 5項目複合キーによる結合が必要
)
WHERE JobDate = @targetDate
```

### 2. 性能考慮事項
- 大量データ処理：1000件単位でのバッチ処理
- インデックス効率：5項目複合キーでの高速検索
- トランザクション管理：一貫性を保つロールバック対応

## 📅 実装優先順位

1. **最優先**：Process 2-5実装（売上伝票のInventoryUnitPrice更新）
2. **優先**：商品日報の粗利計算修正
3. **通常**：データ整合性チェック機能の追加

## 🎯 期待される効果

1. **商品日報の粗利率正常化**：145.32%、-2.39% → 適正範囲
2. **在庫管理精度向上**：正確な在庫単価による適切な在庫評価
3. **帳票出力品質向上**：信頼性の高い財務データ提供

## 📝 次のアクション

1. Process 2-5実装の設計・開発
2. 既存データの在庫単価補正処理
3. 商品日報の粗利計算ロジック見直し
4. 統合テストによる動作確認

---

**調査完了**: 2025-07-23  
**調査者**: Claude Code  
**重要度**: 🔴 緊急対応が必要