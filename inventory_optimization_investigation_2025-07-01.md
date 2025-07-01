# 在庫マスタ最適化処理 - 0件取得問題調査報告書

**調査日**: 2025年7月1日  
**調査者**: Claude Code  
**問題**: 在庫マスタ最適化処理で売上4167件、仕入779件、在庫調整144件が期待されるが、実際には0件が取得される

## 🔍 問題の概要

在庫マスタ最適化処理（`InventoryMasterOptimizationService`）において、以下の状況が発生している：

- **期待される結果**: 売上4167件、仕入779件、在庫調整144件の取得
- **実際の結果**: すべてのテーブルで0件取得
- **対象日付**: 2025年6月30日
- **使用クエリ**: `CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)`

## 🔎 根本原因の特定

### 1. 日付フォーマットの不整合

#### CSVデータの実際の形式（売上伝票.csv分析）
```csv
"2025/06/02","2025/06/02"
```
- **列位置**: 49列目（Index 48）= SystemDate、50列目（Index 49）= JobDate  
- **フォーマット**: `YYYY/MM/DD` (スラッシュ区切り)

#### インポート処理の日付解析
```csharp
// SalesVoucherDaijinCsv.ParseDate() メソッド
private static DateTime ParseDate(string dateStr)
{
    // 1. YYYYMMDD形式を先に試行（8桁数値）
    if (dateStr.Length == 8 && int.TryParse(dateStr, out _))
    {
        if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
    }
    
    // 2. その他の形式（YYYY/MM/DDなど）をシステムロケールで解析
    if (DateTime.TryParse(dateStr, out var parsedDate))
    {
        return parsedDate.Date;
    }
    
    return DateTime.Today;
}
```

#### 問題の発生メカニズム
1. **CSV入力**: "2025/06/02" （スラッシュ区切り形式）
2. **解析処理**: `DateTime.TryParse()` によりシステムロケールで解析
3. **データベース保存**: システムロケール依存の形式で保存
4. **ログ出力**: "30.06.2025 00:00:00" （ドイツ語ロケール形式）
5. **最適化クエリ**: `@jobDate` = `new DateTime(2025, 6, 30)` で検索
6. **結果**: 日付形式の不整合により0件取得

### 2. ロケール依存の問題

#### 現在の環境設定
- **システムロケール**: ドイツ語圏または類似のロケール（DD.MM.YYYY形式）
- **コード設定**: `CultureInfo.InvariantCulture` は一部でのみ使用
- **データベース**: SQL Serverの日付比較でロケール差異が影響

#### ログ証拠
```
インポートログ: "JobDate = 30.06.2025 00:00:00"
最適化検索: "2025-06-30"
取得件数: 0件
```

## 🔧 技術的分析

### SQL CАSTクエリの動作検証

最適化サービスで使用されるクエリ：
```sql
SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
FROM SalesVouchers
WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)
```

#### 予想される問題パターン

| データベース内JobDate | パラメータ@jobDate | CAST結果 | マッチ |
|---------------------|------------------|---------|-------|
| 30.06.2025 00:00:00 | 2025-06-30 | 異なる内部表現 | ❌ |
| 2025-06-30 00:00:00 | 2025-06-30 | 同一内部表現 | ✅ |

### CSVマッピングの検証

#### 現在の列インデックス（SalesVoucherDaijinCsv.cs）
```csharp
[Name("ジョブデート")]
[Index(48)]  // 49列目（汎用日付2）
public string JobDate { get; set; } = string.Empty;
```

#### 実際のCSVデータ確認
- **49列目（Index 48）**: "2025/06/02" ✅ 正しくマッピングされている
- **50列目（Index 49）**: "2025/06/02" ← これもJobDate候補

## 💡 解決方法

### 1. 即効性のある修正（推奨）

#### A. 日付解析の標準化
```csharp
private static DateTime ParseDate(string dateStr)
{
    if (string.IsNullOrEmpty(dateStr))
        return DateTime.Today;
    
    // 1. YYYY/MM/DD形式を優先処理
    if (DateTime.TryParseExact(dateStr, new[] { "yyyy/MM/dd", "yyyyMMdd" }, 
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
    {
        return date;
    }
    
    // 2. フォールバック：InvariantCultureで解析
    if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
    {
        return parsedDate.Date;
    }
    
    return DateTime.Today;
}
```

#### B. 最適化クエリの強化
```sql
-- より柔軟な日付比較
WHERE FORMAT(JobDate, 'yyyy-MM-dd') = FORMAT(@jobDate, 'yyyy-MM-dd')
-- または
WHERE YEAR(JobDate) = YEAR(@jobDate) 
  AND MONTH(JobDate) = MONTH(@jobDate) 
  AND DAY(JobDate) = DAY(@jobDate)
```

### 2. 根本的な修正

#### A. CultureInfo.InvariantCultureの全面適用
```csharp
// すべての日付処理でInvariantCultureを使用
public SalesVoucher ToEntity(string dataSetId)
{
    var salesVoucher = new SalesVoucher
    {
        // ...
        VoucherDate = ParseDateInvariant(VoucherDate),
        JobDate = ParseDateInvariant(JobDate),
        // ...
    };
}

private static DateTime ParseDateInvariant(string dateStr)
{
    return DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, 
        DateTimeStyles.None, out var date) ? date : DateTime.Today;
}
```

#### B. データベース接続でのカルチャ設定
```csharp
// 接続文字列にカルチャ設定を追加
public InventoryMasterOptimizationService(IConfiguration configuration)
{
    System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
}
```

## 🧪 検証手順

### 1. 問題再現テスト
```csharp
// 日付解析テスト
var testDate = "2025/06/30";
var parsed1 = DateTime.TryParse(testDate, out var result1); // システムロケール
var parsed2 = DateTime.TryParse(testDate, CultureInfo.InvariantCulture, 
    DateTimeStyles.None, out var result2); // Invariant

Console.WriteLine($"System: {result1:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Invariant: {result2:yyyy-MM-dd HH:mm:ss}");
```

### 2. データベース検証クエリ
```sql
-- 実際のJobDate値を確認
SELECT TOP 10 
    JobDate,
    FORMAT(JobDate, 'yyyy-MM-dd') as Formatted,
    FORMAT(JobDate, 'dd.MM.yyyy') as German,
    CAST(JobDate AS DATE) as CastResult
FROM SalesVouchers 
ORDER BY CreatedAt DESC;

-- 期待される2025-06-30データの検索
SELECT COUNT(*) FROM SalesVouchers 
WHERE FORMAT(JobDate, 'yyyy-MM-dd') = '2025-06-30';
```

### 3. 最適化クエリテスト
```sql
DECLARE @testDate datetime = '2025-06-30';

SELECT 
    'SalesVouchers' as TableName,
    COUNT(*) as RecordCount
FROM SalesVouchers
WHERE CAST(JobDate AS DATE) = CAST(@testDate AS DATE)

UNION ALL

SELECT 
    'PurchaseVouchers' as TableName,
    COUNT(*) as RecordCount  
FROM PurchaseVouchers
WHERE CAST(JobDate AS DATE) = CAST(@testDate AS DATE)

UNION ALL

SELECT 
    'InventoryAdjustments' as TableName,
    COUNT(*) as RecordCount
FROM InventoryAdjustments  
WHERE CAST(JobDate AS DATE) = CAST(@testDate AS DATE);
```

## 📋 修正優先度

| 優先度 | 修正内容 | 影響範囲 | 実装工数 |
|-------|---------|----------|---------|
| **高** | ParseDate メソッドの InvariantCulture 適用 | CSV取込全般 | 1日 |
| **高** | 最適化クエリの FORMAT 関数使用 | 最適化処理のみ | 半日 |
| **中** | 全システムの CultureInfo 統一 | システム全体 | 2-3日 |
| **低** | データベース既存データの正規化 | 既存データ | 1-2日 |

## 🎯 推奨される実装手順

1. **即座の修正** (当日実装可能)
   - `SalesVoucherDaijinCsv.ParseDate()` の修正
   - `PurchaseVoucherDaijinCsv.ParseDate()` の修正  
   - `InventoryAdjustmentDaijinCsv.ParseDate()` の修正

2. **検証テスト** (翌日)
   - 修正後のCSV取込テスト
   - 最適化処理の動作確認
   - 期待される4167, 779, 144件の取得確認

3. **根本修正** (後日実装)
   - システム全体の CultureInfo.InvariantCulture 適用
   - 設定ファイルでのロケール固定化

## 📈 期待される改善効果

- ✅ **在庫マスタ最適化**: 0件 → 期待件数の正常取得
- ✅ **日付処理の安定化**: ロケール依存問題の解消  
- ✅ **保守性向上**: 日付処理の統一化
- ✅ **国際化対応**: どの地域でも安定動作

## 🔄 今後の予防策

1. **開発標準の策定**
   - すべての日付処理で `CultureInfo.InvariantCulture` 使用を義務化
   - CSVパース処理のテンプレート化

2. **単体テストの強化**
   - 各種ロケール環境での日付処理テスト
   - CSVデータ形式の境界値テスト

3. **設定の外部化**
   - 日付フォーマットの設定ファイル管理
   - ロケール設定の明示化

---

**結論**: 主要因は日付解析でのロケール依存処理。`CultureInfo.InvariantCulture`の適用により解決可能。即効性のある修正により当日中の問題解決が期待できる。