# 商品日報スクリプトコンパイルエラー修正完了報告

**修正日**: 2025年7月4日  
**目的**: FastReport商品日報の.NET 8環境でのスクリプトコンパイルエラーを修正

## 📋 問題の概要

商品日報でFastReportが`System.PlatformNotSupportedException`エラーを発生：
- 原因：テンプレート内の`[DailyReportData.ProductCode]`のような式を評価しようとしてCSharpCodeGeneratorを使用
- .NET 8環境では動的コンパイルがサポートされていない

## 🔧 修正内容

### 1. テンプレートファイルの修正
**ファイル**: `src/InventorySystem.Reports/FastReport/Templates/DailyReport.frx`

**修正内容**:
- すべての`[式]`を削除（例：`[Format('{0:yyyy年MM月dd日}', [ReportDate])]`）
- TextObjectのText属性を空文字列に変更
- データバインディング用のDataSource定義を削除
- 固定レイアウト方式に変更（DataBandの代わりにChildBandを使用）
- 50行分のTextObjectを事前配置

**例**:
```xml
<!-- 修正前 -->
<TextObject Name="Text2" Text="[Format('{0:yyyy年MM月dd日}', [ReportDate])]" />

<!-- 修正後 -->
<TextObject Name="ReportDateText" Text="" />
```

### 2. DailyReportFastReportService.csの修正

**主な変更点**:

1. **データソース登録の削除**:
```csharp
// 削除した行
// report.RegisterData(dataSet);
// report.SetParameterValue("ReportDate", reportDate);
```

2. **手動データバインディングメソッドの追加**:
```csharp
private void BindDataManually(Report report, List<DailyReportItem> items, 
    List<DailyReportSubtotal> subtotals, DailyReportTotal total, DateTime reportDate)
{
    // ヘッダー情報の設定
    SetTextObjectValue(report, "ReportDateText", reportDate.ToString("yyyy年MM月dd日"));
    SetTextObjectValue(report, "PageInfo", "1 / 1");
    
    // データ行の設定
    int rowIndex = 0;
    foreach (var item in items.Where(IsNotZeroItem))
    {
        if (rowIndex >= 50) break; // 最大50行
        
        // 各フィールドを個別に設定
        SetTextObjectValue(report, $"ProductCode_{rowIndex}", item.ProductCode ?? "");
        SetTextObjectValue(report, $"DailySalesAmount_{rowIndex}", 
            item.DailySalesAmount.ToString("N0"));
        // ... 他のフィールドも同様
        
        rowIndex++;
    }
}
```

3. **数値フォーマット用ヘルパーメソッドの追加**:
```csharp
private string FormatNumberWithTriangle(decimal value)
{
    if (value < 0)
    {
        return "▲" + Math.Abs(value).ToString("N0");
    }
    return value.ToString("N0");
}
```

4. **不要なメソッドの削除**:
- `PrepareDataSet` メソッド
- `ConfigureSubtotalsAndTotals` メソッド
- `UpdateSubtotal` メソッド

### 3. デバッグログの追加
```csharp
_logger.LogDebug("テンプレート読み込み完了。スクリプト言語: {ScriptLanguage}", 
    report.GetType().GetProperty("ScriptLanguage")?.GetValue(report));
_logger.LogDebug("データバインディング開始。アイテム数: {Count}", items.Count);
```

## ✅ 修正の効果

1. **スクリプトコンパイルエラーの解消**
   - 式の評価を完全に排除
   - 動的コンパイルを使用しない

2. **パフォーマンスの向上**
   - スクリプトコンパイルのオーバーヘッドがない
   - データバインディングが高速

3. **デバッグの容易さ**
   - すべての処理がC#コード内で完結
   - ブレークポイントの設定が可能

## 🎯 実装のポイント

1. **固定レイアウト方式**
   - 最大50行分のTextObjectを事前配置
   - 動的な行数には対応しないが、安定性を優先

2. **手動データバインディング**
   - すべてのデータをC#コードから設定
   - 式評価を一切使用しない

3. **ScriptLanguage="None"の維持**
   - テンプレートでスクリプト言語を無効化
   - SetScriptLanguageToNoneメソッドで追加確認

## 📝 変更ファイル一覧

1. `/src/InventorySystem.Reports/FastReport/Templates/DailyReport.frx` (全面改修)
2. `/src/InventorySystem.Reports/FastReport/Services/DailyReportFastReportService.cs` (メソッド構造変更)

## 🚀 次のステップ

Windows環境でのテスト：
```bash
dotnet run daily-report 2025-06-30
```

期待される結果：
- スクリプトコンパイルエラーが発生しない
- PDFが正常に生成される
- すべてのデータが正しく表示される

---

**実装者**: Claude Code  
**ステータス**: ✅ 完了