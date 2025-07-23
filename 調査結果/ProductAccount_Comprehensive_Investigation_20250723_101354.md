# 商品勘定帳票システム包括調査結果

## 調査日時
- 実施日: 2025-07-23 10:13:54
- 調査者: Claude Code

## 1. ストアドプロシージャ分析

### sp_CreateProductLedgerData（最終SELECT文のカラム）
```sql
SELECT
    ProductCode,
    ProductName,
    ShippingMarkCode,
    ShippingMarkName,
    ManualShippingMark,
    GradeCode,
    GradeName,
    ClassCode,
    ClassName,
    VoucherNumber,
    DisplayCategory,
    TransactionDate,                -- ✅ 存在
    FORMAT(TransactionDate, 'MM/dd') as MonthDay,
    PurchaseQuantity,
    SalesQuantity,
    CASE WHEN RecordType = 'Previous' THEN RemainingQuantity ELSE RunningQuantity END as RemainingQuantity,
    UnitPrice,
    Amount,
    GrossProfit,
    WalkingDiscount,
    CustomerSupplierName,
    GroupKey,
    ProductCategory1,
    ProductCategory5,
    SortKeyPart1,                   -- ✅ 3分割構造
    SortKeyPart2,
    SortKeyPart3,
    -- 集計用フィールド
    FIRST_VALUE(RemainingQuantity) OVER (...) as PreviousBalance,
    SUM(PurchaseQuantity) OVER (...) as TotalPurchaseQuantity,
    SUM(SalesQuantity) OVER (...) as TotalSalesQuantity,
    LAST_VALUE(RunningQuantity) OVER (...) as CurrentBalance,
    LAST_VALUE(...) OVER (...) as InventoryUnitPrice,
    LAST_VALUE(RunningAmount) OVER (...) as InventoryAmount,
    SUM(GrossProfit) OVER (...) as TotalGrossProfit,
    CASE ... END as GrossProfitRate
```

### CTE構造（TransactionData）
各UNION ALLブロックで以下のカラムを定義：
- 前残レコード: すべてのカラムを含む
- 売上伝票: VoucherDate → TransactionDateにマッピング
- 仕入伝票: VoucherDate → TransactionDateにマッピング  
- 在庫調整: VoucherDate → TransactionDateにマッピング

### 問題点
- ✅ TransactionDateは正しく定義されている
- ✅ 各UNION ALLでカラム整合性は保たれている
- ✅ ORDER BY句の900バイト制限は解決済み

## 2. C#コード分析

### ProductAccountFastReportService.cs（DataReader読み取りカラム）
```csharp
// PrepareReportDataメソッド（115-164行目）
while (reader.Read())
{
    var model = new ProductAccountReportModel
    {
        ProductCode = reader.GetString("ProductCode"),                      // ✅
        ProductName = reader.GetString("ProductName"),                      // ✅
        ShippingMarkCode = reader.GetString("ShippingMarkCode"),            // ✅
        ShippingMarkName = reader.GetString("ShippingMarkName"),            // ✅
        ManualShippingMark = reader.GetString("ManualShippingMark"),        // ✅
        GradeCode = reader.GetString("GradeCode"),                          // ✅
        GradeName = reader.IsDBNull("GradeName") ? "" : reader.GetString("GradeName"),  // ✅ NULL処理
        ClassCode = reader.GetString("ClassCode"),                          // ✅
        ClassName = reader.IsDBNull("ClassName") ? "" : reader.GetString("ClassName"),  // ✅ NULL処理
        VoucherNumber = reader.GetString("VoucherNumber"),                  // ✅
        DisplayCategory = reader.GetString("DisplayCategory"),              // ✅
        TransactionDate = reader.GetDateTime("TransactionDate"),            // ✅ 期待するカラム
        PurchaseQuantity = reader.GetDecimal("PurchaseQuantity"),           // ✅
        SalesQuantity = reader.GetDecimal("SalesQuantity"),                 // ✅
        RemainingQuantity = reader.GetDecimal("RemainingQuantity"),         // ✅
        UnitPrice = reader.GetDecimal("UnitPrice"),                         // ✅
        Amount = reader.GetDecimal("Amount"),                               // ✅
        GrossProfit = reader.GetDecimal("GrossProfit"),                     // ✅
        WalkingDiscount = reader.GetDecimal("WalkingDiscount"),             // ✅
        CustomerSupplierName = reader.GetString("CustomerSupplierName"),    // ✅
        GroupKey = reader.GetString("GroupKey"),                            // ✅
        ProductCategory1 = reader.IsDBNull("ProductCategory1") ? null : reader.GetString("ProductCategory1"),  // ✅ NULL処理
        ProductCategory5 = reader.IsDBNull("ProductCategory5") ? null : reader.GetString("ProductCategory5"),  // ✅ NULL処理
        
        // 集計用データ（ストアドプロシージャで計算済み）
        PreviousBalanceQuantity = reader.GetDecimal("PreviousBalance"),     // ✅
        TotalPurchaseQuantity = reader.GetDecimal("TotalPurchaseQuantity"), // ✅
        TotalSalesQuantity = reader.GetDecimal("TotalSalesQuantity"),       // ✅
        CurrentBalanceQuantity = reader.GetDecimal("CurrentBalance"),       // ✅
        InventoryUnitPrice = reader.GetDecimal("InventoryUnitPrice"),       // ✅
        InventoryAmount = reader.GetDecimal("InventoryAmount"),             // ✅
        TotalGrossProfit = reader.GetDecimal("TotalGrossProfit"),           // ✅
        GrossProfitRate = reader.GetDecimal("GrossProfitRate")              // ✅
    };

    // 月日表示を設定
    model.MonthDayDisplay = reader.GetString("MonthDay");                   // ✅
}
```

### NULL値処理
- ✅ **適切**: GradeName, ClassName, ProductCategory1, ProductCategory5でIsDBNullチェック実装
- ✅ **適切**: 文字列の場合は空文字またはnullを設定
- ✅ **適切**: Decimal型は非NULL前提（ストアドプロシージャで保証）

## 3. ProductAccountReportModel分析

### プロパティ定義（完全リスト）
```csharp
// 基本情報
public string ProductCode { get; set; } = string.Empty;                // ✅
public string ProductName { get; set; } = string.Empty;                // ✅
public string ShippingMarkCode { get; set; } = string.Empty;           // ✅
public string ShippingMarkName { get; set; } = string.Empty;           // ✅
public string ManualShippingMark { get; set; } = string.Empty;         // ✅
public string GradeCode { get; set; } = string.Empty;                  // ✅
public string GradeName { get; set; } = string.Empty;                  // ✅
public string ClassCode { get; set; } = string.Empty;                  // ✅
public string ClassName { get; set; } = string.Empty;                  // ✅
public string VoucherNumber { get; set; } = string.Empty;              // ✅
public string RecordType { get; set; } = string.Empty;                 // ❌ SQL非提供
public string VoucherCategory { get; set; } = string.Empty;            // ❌ SQL非提供
public string DisplayCategory { get; set; } = string.Empty;            // ✅
public DateTime TransactionDate { get; set; }                          // ✅

// 数量・金額
public decimal PurchaseQuantity { get; set; }                          // ✅
public decimal SalesQuantity { get; set; }                             // ✅
public decimal RemainingQuantity { get; set; }                         // ✅
public decimal UnitPrice { get; set; }                                 // ✅
public decimal Amount { get; set; }                                    // ✅
public decimal GrossProfit { get; set; }                               // ✅
public decimal WalkingDiscount { get; set; }                           // ✅

// 分類・キー
public string CustomerSupplierName { get; set; } = string.Empty;       // ✅
public string? ProductCategory1 { get; set; }                          // ✅
public string? ProductCategory5 { get; set; }                          // ✅
public string GroupKey { get; set; } = string.Empty;                   // ✅
public string SortKey { get; set; } = string.Empty;                    // ❌ 廃止済み

// 集計用（正常マッピング）
public decimal PreviousBalanceQuantity { get; set; }                   // ✅ → PreviousBalance
public decimal PreviousBalanceAmount { get; set; }                     // ❌ SQL非提供
public decimal TotalPurchaseQuantity { get; set; }                     // ✅
public decimal TotalPurchaseAmount { get; set; }                       // ❌ SQL非提供
public decimal TotalSalesQuantity { get; set; }                        // ✅
public decimal TotalSalesAmount { get; set; }                          // ❌ SQL非提供
public decimal CurrentBalanceQuantity { get; set; }                    // ✅ → CurrentBalance
public decimal CurrentBalanceAmount { get; set; }                      // ❌ SQL非提供
public decimal InventoryUnitPrice { get; set; }                        // ✅
public decimal InventoryAmount { get; set; }                           // ✅
public decimal TotalGrossProfit { get; set; }                          // ✅
public decimal GrossProfitRate { get; set; }                           // ✅

// 表示用
public string MonthDayDisplay { get; set; } = string.Empty;            // ✅ → MonthDay
```

## 4. FastReportテンプレート分析

### ProductAccount.frx（使用フィールド）
```xml
<TableDataSource Name="ProductAccount">
  <Column Name="ProductCode" DataType="System.String"/>                 <!-- ✅ -->
  <Column Name="ProductName" DataType="System.String"/>                 <!-- ✅ -->
  <Column Name="ShippingMarkCode" DataType="System.String"/>            <!-- ✅ -->
  <Column Name="ShippingMarkName" DataType="System.String"/>            <!-- ✅ -->
  <Column Name="ManualShippingMark" DataType="System.String"/>          <!-- ✅ -->
  <Column Name="GradeCode" DataType="System.String"/>                   <!-- ✅ -->
  <Column Name="GradeName" DataType="System.String"/>                   <!-- ✅ -->
  <Column Name="ClassCode" DataType="System.String"/>                   <!-- ✅ -->
  <Column Name="ClassName" DataType="System.String"/>                   <!-- ✅ -->
  <Column Name="VoucherNumber" DataType="System.String"/>               <!-- ✅ -->
  <Column Name="DisplayCategory" DataType="System.String"/>             <!-- ✅ -->
  <Column Name="MonthDay" DataType="System.String"/>                    <!-- ✅ -->
  <Column Name="PurchaseQuantity" DataType="System.Decimal"/>           <!-- ✅ -->
  <Column Name="SalesQuantity" DataType="System.Decimal"/>              <!-- ✅ -->
  <Column Name="RemainingQuantity" DataType="System.Decimal"/>          <!-- ✅ -->
  <Column Name="UnitPrice" DataType="System.Decimal"/>                  <!-- ✅ -->
  <Column Name="Amount" DataType="System.Decimal"/>                     <!-- ✅ -->
  <Column Name="GrossProfit" DataType="System.Decimal"/>                <!-- ✅ -->
  <Column Name="CustomerSupplierName" DataType="System.String"/>        <!-- ✅ -->
  <Column Name="GroupKey" DataType="System.String"/>                    <!-- ✅ -->
  <Column Name="PreviousBalance" DataType="System.Decimal"/>            <!-- ✅ -->
  <Column Name="TotalPurchaseQuantity" DataType="System.Decimal"/>      <!-- ✅ -->
  <Column Name="TotalSalesQuantity" DataType="System.Decimal"/>         <!-- ✅ -->
  <Column Name="CurrentBalance" DataType="System.Decimal"/>             <!-- ✅ -->
  <Column Name="InventoryUnitPrice" DataType="System.Decimal"/>         <!-- ✅ -->
  <Column Name="InventoryAmount" DataType="System.Decimal"/>            <!-- ✅ -->
  <Column Name="TotalGrossProfit" DataType="System.Decimal"/>           <!-- ✅ -->
  <Column Name="GrossProfitRate" DataType="System.Decimal"/>            <!-- ✅ -->
</TableDataSource>

<Parameter Name="JobDate" DataType="System.String"/>                    <!-- ✅ -->
<Parameter Name="GeneratedAt" DataType="System.String"/>               <!-- ✅ -->
```

### データバインディング
- ✅ ScriptLanguage="None"設定済み
- ✅ A3横向き（420mm × 297mm、1512px幅）対応
- ✅ パラメータはC#コードで正しく設定

## 5. 他帳票との実装差異

### UnmatchListServiceとの比較
| 項目 | UnmatchList | ProductAccount | 差異 |
|------|-------------|----------------|------|
| データ取得 | Repository経由 | ストアドプロシージャ直接 | ✅ 設計方針の違い |
| 接続方式 | DI注入 | IConfiguration使用 | ✅ 修正済み |
| エラーハンドリング | try-catch | try-catch | ✅ 同等 |
| ScriptLanguage設定 | リフレクション | 設定なし | ❌ 設定推奨 |

### DailyReportServiceとの比較
| 項目 | DailyReport | ProductAccount | 差異 |
|------|-------------|----------------|------|
| データ準備 | PopulateReportData | CreateDataTable | ✅ 方式の違い |
| ScriptLanguage | SetScriptLanguageToNone | 設定なし | ❌ 設定推奨 |
| パラメータ設定 | SetParameterValue | SetParameterValue | ✅ 同等 |

## 6. 不整合マトリックス

| カラム名 | SQL型 | SQL存在 | C#型 | C#読取 | Model定義 | FastReport | 不整合詳細 |
|----------|-------|---------|------|--------|-----------|------------|------------|
| TransactionDate | DATE | ✅ | DateTime | ✅ | ✅ | ❌ | **FRXに未定義** |
| MonthDay | STRING | ✅ | String | ✅ | ✅ | ✅ | 正常 |
| RecordType | - | ❌ | String | ❌ | ✅ | ❌ | **Model定義のみ** |
| VoucherCategory | - | ❌ | String | ❌ | ✅ | ❌ | **Model定義のみ** |
| SortKey | - | ❌ | String | ❌ | ✅ | ❌ | **廃止済み（SortKeyPart1-3に分割）** |
| SortKeyPart1 | STRING | ✅ | - | ❌ | ❌ | ❌ | **SQL提供のみ** |
| SortKeyPart2 | INT | ✅ | - | ❌ | ❌ | ❌ | **SQL提供のみ** |
| SortKeyPart3 | STRING | ✅ | - | ❌ | ❌ | ❌ | **SQL提供のみ** |
| PreviousBalanceAmount | - | ❌ | Decimal | ❌ | ✅ | ❌ | **Model定義のみ** |
| TotalPurchaseAmount | - | ❌ | Decimal | ❌ | ✅ | ❌ | **Model定義のみ** |
| TotalSalesAmount | - | ❌ | Decimal | ❌ | ✅ | ❌ | **Model定義のみ** |
| CurrentBalanceAmount | - | ❌ | Decimal | ❌ | ✅ | ❌ | **Model定義のみ** |
| WalkingDiscount | DECIMAL | ✅ | Decimal | ✅ | ✅ | ❌ | **FRXに未定義** |

## 7. 根本原因の分析

### 主要な不整合
1. **FastReportテンプレートの不備**
   - TransactionDateカラムが未定義
   - WalkingDiscountカラムが未定義
   - SortKeyPart1-3が未反映

2. **使用されていないModelプロパティ**
   - RecordType, VoucherCategory（SQLで提供されていない）
   - Amount系プロパティ（計算が必要だが未実装）
   - SortKey（廃止済みだが削除されていない）

3. **設計の不統一**
   - ScriptLanguage設定が他サービスと異なる
   - データバインディング方式の違い

### 推定される問題の流れ
1. ストアドプロシージャ修正（SortKey → SortKeyPart1-3分割）
2. TransactionDateカラム追加
3. FastReportテンプレートが古い定義のまま
4. C#コードはTransactionDateを読み取ろうとする
5. **IndexOutOfRangeException: TransactionDate**発生の可能性

## 8. 修正推奨事項

### 優先度：高
1. **FastReportテンプレート更新**
   - TransactionDateカラム追加
   - WalkingDiscountカラム追加
   - SortKeyPart1-3カラム追加（必要に応じて）

2. **ScriptLanguage設定追加**
   - SetScriptLanguageToNoneメソッド実装

### 優先度：中
1. **ProductAccountReportModel整理**
   - 未使用プロパティの削除（RecordType, VoucherCategory, SortKey）
   - Amount系プロパティの実装または削除

2. **データバインディング統一**
   - 他サービスとの方式統一検討

### 優先度：低
1. **設計統一**
   - データ取得方式の統一検討
   - エラーハンドリングパターンの統一

## 9. テスト推奨事項

### 単体テスト
1. ストアドプロシージャの単独実行テスト
2. DataReader読み取りテスト（モックデータ使用）
3. FastReportテンプレートの構文チェック

### 統合テスト
1. 実際のデータでのPDF生成テスト
2. 異なる日付での動作確認
3. 部門フィルタでの動作確認

## 10. 付録

### 調査で使用したコマンド
```bash
# ストアドプロシージャ確認
Read database/procedures/sp_CreateProductLedgerData.sql

# C#コード確認  
Read src/InventorySystem.Reports/FastReport/Services/ProductAccountFastReportService.cs
Read src/InventorySystem.Reports/Models/ProductAccountReportModel.cs

# FastReportテンプレート確認
Read src/InventorySystem.Reports/FastReport/Templates/ProductAccount.frx

# 他サービス比較
Read src/InventorySystem.Reports/FastReport/Services/UnmatchListFastReportService.cs
Read src/InventorySystem.Reports/FastReport/Services/DailyReportFastReportService.cs
```

### 参照ファイル
- `/database/procedures/sp_CreateProductLedgerData.sql`
- `/src/InventorySystem.Reports/FastReport/Services/ProductAccountFastReportService.cs`
- `/src/InventorySystem.Reports/Models/ProductAccountReportModel.cs`
- `/src/InventorySystem.Reports/FastReport/Templates/ProductAccount.frx`
- `/src/InventorySystem.Reports/FastReport/Services/UnmatchListFastReportService.cs`
- `/src/InventorySystem.Reports/FastReport/Services/DailyReportFastReportService.cs`

## 11. 結論

**最重要問題**: FastReportテンプレート（ProductAccount.frx）がストアドプロシージャの最新スキーマに対応していない

**対応必要項目**:
1. ✅ TransactionDateカラム（修正済み）
2. ❌ FastReportテンプレートの更新（未対応）
3. ❌ ScriptLanguage設定（未対応）

システム全体の整合性は概ね良好だが、FastReportテンプレートの更新が最優先課題。