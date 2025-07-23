# 商品勘定帳票システム制約下再調査結果

## 調査日時
- 実施日: 2025-07-23 12:00:00
- 調査者: Claude Code
- **制約**: FastReportテンプレートの更新は不可

## 1. 調査背景

### ユーザーからの重要な制約事項
- **FastReportテンプレートの更新はできません**
- アンマッチリストも商品日報も正常に動作している
- 商品勘定だけが上手くいかない
- **商品勘定を現状のシステムに整合させなければいけません**

## 2. 正常動作帳票の実装パターン分析

### UnmatchListFastReportService（正常動作）
#### データ処理パターン
```csharp
// 1. 手動DataTableコラム定義
var dataTable = new DataTable("UnmatchItems");
dataTable.Columns.Add("Category", typeof(string));
dataTable.Columns.Add("CustomerCode", typeof(string));
// ...全18列を明示的に定義

// 2. 行単位でのデータ追加
foreach (var item in unmatchList)
{
    dataTable.Rows.Add(
        categoryName,
        customerCode,
        customerName,
        // ...すべての値を明示的に設定
    );
}

// 3. FastReport登録
report.RegisterData(dataTable, "UnmatchItems");
```

#### FastReport設定
```csharp
// ScriptLanguageをリフレクションで設定
var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
if (scriptLanguageProperty != null)
{
    var noneValue = Enum.GetValues(scriptLanguageType)
        .Cast<object>()
        .FirstOrDefault(v => v.ToString() == "None");
    if (noneValue != null)
    {
        scriptLanguageProperty.SetValue(report, noneValue);
    }
}
```

### DailyReportFastReportService（正常動作）
#### データ処理パターン
```csharp
// 1. SetScriptLanguageToNone専用メソッド
private void SetScriptLanguageToNone(Report report)
{
    // リフレクションでScriptLanguage.None設定
}

// 2. PopulateReportDataによる直接オブジェクト操作
private void PopulateReportData(Report report, List<DailyReportItem> items)
{
    // DataBandに直接TextObjectを動的作成
    var dataBand = report.FindObject("Data1") as FR.DataBand;
    AddDetailRow(dataBand, currentY, item);
}

// 3. 動的TextObject生成
private void AddDetailRow(FR.DataBand dataBand, float y, DailyReportItem item)
{
    var nameText = new FR.TextObject
    {
        Name = $"ProductName_{y}",
        Left = 0,
        Top = y,
        Width = 114.43f,
        Height = 18.9f,
        Text = item.ProductName ?? "",
        Font = new Font("MS Gothic", 8),
        VertAlign = FR.VertAlign.Center
    };
    dataBand.Objects.Add(nameText);
}
```

## 3. ProductAccountFastReportService（問題発生）の実装パターン

### 現在の実装
```csharp
// 1. ストアドプロシージャ直接実行
using var reader = command.ExecuteReader();
var reportModels = new List<ProductAccountReportModel>();
while (reader.Read())
{
    var model = new ProductAccountReportModel
    {
        // DataReaderから直接値を読み取り
        TransactionDate = reader.GetDateTime("TransactionDate"), // ❌ 問題箇所
        // ...
    };
}

// 2. CreateDataTableによる変換
var dataTable = CreateDataTable(reportData);
report.RegisterData(dataTable, "ProductAccount");

// 3. ScriptLanguage設定なし ❌
// SetScriptLanguageToNoneメソッドが呼ばれていない
```

### 発見された問題点

#### 1. ScriptLanguage設定の欠如
```csharp
// ❌ ProductAccountでは設定されていない
// report.Load(_templatePath);
// report.Prepare(); // 直接準備

// ✅ 他サービスでは実装済み
SetScriptLanguageToNone(report);
report.Prepare();
```

#### 2. FastReportテンプレートとの不整合
```xml
<!-- ProductAccount.frx（現在の定義） -->
<TableDataSource Name="ProductAccount">
  <Column Name="MonthDay" DataType="System.String"/>        <!-- ✅ 存在 -->
  <!-- TransactionDateカラムが未定義 ❌ -->
  <!-- WalkingDiscountカラムが未定義 ❌ -->
</TableDataSource>
```

#### 3. C#コードでの期待カラム
```csharp
// C#コードで読み取ろうとしているカラム
TransactionDate = reader.GetDateTime("TransactionDate"),    // ❌ FRXに未定義
MonthDayDisplay = reader.GetString("MonthDay"),            // ✅ FRXに定義済み
WalkingDiscount = reader.GetDecimal("WalkingDiscount"),    // ❌ FRXに未定義
```

## 4. 制約下での解決策

### 戦略1: FastReportテンプレート制約を回避
**TransactionDateは使用せず、MonthDayのみ使用**

#### 修正方法
```csharp
// 変更前
TransactionDate = reader.GetDateTime("TransactionDate"),     // ❌ 削除
MonthDayDisplay = reader.GetString("MonthDay"),             // ✅ 維持

// 変更後
// TransactionDate読み取りを削除
MonthDayDisplay = reader.GetString("MonthDay"),             // ✅ これのみ使用
```

### 戦略2: SetScriptLanguageToNoneメソッド追加
```csharp
// ProductAccountFastReportServiceに追加
private void SetScriptLanguageToNone(Report report)
{
    try
    {
        var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
        if (scriptLanguageProperty != null)
        {
            var scriptLanguageType = scriptLanguageProperty.PropertyType;
            if (scriptLanguageType.IsEnum)
            {
                var noneValue = Enum.GetValues(scriptLanguageType)
                    .Cast<object>()
                    .FirstOrDefault(v => v.ToString() == "None");
                
                if (noneValue != null)
                {
                    scriptLanguageProperty.SetValue(report, noneValue);
                }
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
    }
}
```

### 戦略3: WalkingDiscount対応
```csharp
// 変更前
WalkingDiscount = reader.GetDecimal("WalkingDiscount"),     // ❌ FRXに未定義

// 変更後
// 1. DataTable作成時にWalkingDiscountカラムを除外
// 2. またはCreateDataTableメソッドでカラム名をマッピング
```

## 5. 他帳票との統一パターン適用

### 推奨する統一実装
```csharp
public byte[] GenerateProductAccountReport(DateTime jobDate, string? departmentCode = null)
{
    try
    {
        // 1. データ取得
        var reportData = PrepareReportData(jobDate, departmentCode);
        
        using var report = new FR.Report();
        report.Load(_templatePath);
        
        // 2. ScriptLanguage設定（他帳票と統一）
        SetScriptLanguageToNone(report);
        
        // 3. データテーブル作成（FastReportテンプレートに合わせて調整）
        var dataTable = CreateCompatibleDataTable(reportData);
        report.RegisterData(dataTable, "ProductAccount");
        
        // 4. パラメータ設定
        report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
        report.SetParameterValue("GeneratedAt", DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分ss秒"));
        
        // 5. レポート準備・生成
        report.Prepare();
        
        // 6. PDF出力
        return ExportToPdf(report);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "商品勘定帳票の生成に失敗しました");
        throw;
    }
}
```

## 6. 具体的修正箇所

### 修正1: PrepareReportDataメソッド
```csharp
// 変更前
TransactionDate = reader.GetDateTime("TransactionDate"),    // ❌ 削除
WalkingDiscount = reader.GetDecimal("WalkingDiscount"),     // ❌ 削除

// 変更後
// これらの行を削除またはコメントアウト
// MonthDayDisplayのみを使用
```

### 修正2: CreateDataTableメソッド
```csharp
// WalkingDiscount関連のカラムを除外
// table.Columns.Add("WalkingDiscount", typeof(decimal));  // ❌ 削除

// データ行追加時も対応
// row["WalkingDiscount"] = item.WalkingDiscount;          // ❌ 削除
```

### 修正3: GeneratePdfReportメソッド
```csharp
// SetScriptLanguageToNone追加
report.Load(_templatePath);
SetScriptLanguageToNone(report);  // ✅ 追加
var dataTable = CreateDataTable(reportData);
```

## 7. 期待される効果

### 即座に解決される問題
1. **TransactionDate未定義エラー** → MonthDayのみ使用で解決
2. **WalkingDiscount未定義エラー** → カラム除外で解決  
3. **ScriptLanguageエラー** → SetScriptLanguageToNoneで解決

### システム整合性の向上
1. **他帳票との統一性** → 同じScriptLanguage設定パターン
2. **保守性向上** → 統一されたエラーハンドリング
3. **運用安定性** → FastReportテンプレート制約の回避

## 8. 実装優先度

### 優先度：最高（即座に実行）
1. **SetScriptLanguageToNoneメソッド追加・呼び出し**
2. **TransactionDate読み取り削除**
3. **WalkingDiscountカラム除外**

### 優先度：高（次のフェーズ）
1. **ProductAccountReportModel不要プロパティ削除**
2. **エラーハンドリング統一**

### 優先度：中（将来的改善）
1. **データ取得方式の統一検討**
2. **設計パターンの全体最適化**

## 9. 制約下での最適解

### 核心戦略
**「FastReportテンプレートに合わせてC#コードを調整」**

1. FastReportテンプレートで定義されていないカラムは使用しない
2. 他の正常動作帳票と同じパターンを適用
3. ScriptLanguage.None設定でスクリプトエラーを回避

### テンプレート制約の回避方法
```csharp
// ❌ テンプレートに依存する実装
TransactionDate = reader.GetDateTime("TransactionDate");

// ✅ テンプレート制約を回避する実装  
// TransactionDateは使用せず、MonthDayで日付表示
MonthDayDisplay = reader.GetString("MonthDay");
```

## 10. 結論

### 問題の本質
- **FastReportテンプレートが更新されていない**
- **他帳票で成功しているパターンが適用されていない**
- **テンプレート制約下でのコード調整が不十分**

### 解決方向性
1. **FastReportテンプレートは現状維持**
2. **C#コードをテンプレートに合わせて調整**
3. **他帳票の成功パターンを適用**

### 最小限の修正で最大効果
- SetScriptLanguageToNone追加
- 未定義カラム（TransactionDate, WalkingDiscount）の削除
- MonthDay使用による日付表示

この修正により、**FastReportテンプレートを変更せずに**商品勘定帳票を正常動作させることができます。

## 11. 修正ファイル一覧
- `/src/InventorySystem.Reports/FastReport/Services/ProductAccountFastReportService.cs`（主要修正）
- `/src/InventorySystem.Reports/Models/ProductAccountReportModel.cs`（プロパティ整理）

**重要**: この修正はFastReportテンプレートを変更せずに、現状のシステム制約下で商品勘定帳票を動作させる最適解です。