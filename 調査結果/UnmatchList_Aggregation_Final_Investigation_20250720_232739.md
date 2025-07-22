# アンマッチリスト集約処理の最終調査結果

**調査日時**: 2025年7月20日 23:27  
**調査対象**: 411明細が16件に変換される原因  
**調査期間**: 最終調査（完全な実行フロー追跡）

## 🎯 調査結果サマリー

### 問題の特定
**411明細が16件に変換される問題は、FastReportでのデータ処理ではなく、PDF表示上の問題である可能性が高い**

### 重要な発見

#### 1. 実際に使用されている実装の確認
- **DIコンテナ設定**: `Program.cs`でのDI登録は`UnmatchListService`（V2ではない）
- **FastReportサービス**: `UnmatchListFastReportService`が使用されている
- **データ流れ**: サービス → FastReport → PDF の完全な流れを追跡完了

#### 2. データ変換ポイントの分析
- **UnmatchListService**: 集約処理（GroupBy/Distinct）は**使用されていない**
- **FastReportService**: `dataTable.Rows.Count`を`TotalCount`パラメータに設定
- **PDF生成**: 411件のDataTableが正しく作成され、TotalCountも411が設定される

## 📊 完全な実行フロー（データ件数追跡）

### Step 1: Program.cs → ExecuteUnmatchListAsync
```csharp
// Line 577-579: サービス呼び出し
var result = targetDate.HasValue 
    ? await unmatchListService.ProcessUnmatchListAsync(targetDate.Value)
    : await unmatchListService.ProcessUnmatchListAsync();

// Line 621: FastReportサービス呼び出し
var pdfBytes = reportService.GenerateUnmatchListReport(result.UnmatchItems, latestJobDate);
```

### Step 2: UnmatchListService.cs → ProcessUnmatchListInternalAsync
```csharp
// Line 202-207: アンマッチリスト生成
var unmatchItems = targetDate.HasValue 
    ? await GenerateUnmatchListAsync(dataSetId, targetDate.Value)
    : await GenerateUnmatchListAsync(dataSetId);
var unmatchList = unmatchItems.ToList();
_logger.LogInformation("アンマッチリスト生成完了 - アンマッチ件数: {Count}", unmatchList.Count);

// Line 234: 結果オブジェクト作成
UnmatchCount = unmatchList.Count,
UnmatchItems = unmatchList,
```

### Step 3: UnmatchListService.cs → GenerateUnmatchListInternalAsync
```csharp
// Line 287-303: 各種アンマッチチェック
var salesUnmatches = await CheckSalesUnmatchAsync(dataSetId, targetDate);      // 売上
var purchaseUnmatches = await CheckPurchaseUnmatchAsync(dataSetId, targetDate); // 仕入
var adjustmentUnmatches = await CheckInventoryAdjustmentUnmatchAsync(dataSetId, targetDate); // 在庫調整

unmatchItems.AddRange(salesUnmatches);
unmatchItems.AddRange(purchaseUnmatches);
unmatchItems.AddRange(adjustmentUnmatches);

// Line 317-323: ソート処理のみ（集約なし）
return enrichedItems
    .OrderBy(x => x.ProductCategory1)
    .ThenBy(x => x.Key.ProductCode)
    // ... その他のソートキー
```

### Step 4: UnmatchListFastReportService.cs → GenerateUnmatchListReport
```csharp
// Line 102: リスト変換
var unmatchList = unmatchItems.ToList();
_logger.LogDebug("PDF生成: アンマッチ項目数={Count}", unmatchList.Count);

// Line 118-136: DataTable作成
var dataTable = new DataTable("UnmatchItems");
// 17列のカラム定義...

// Line 139-203: データ追加（1:1で追加、集約なし）
foreach (var (item, index) in unmatchList.Select((i, idx) => (i, idx)))
{
    dataTable.Rows.Add(/* 17個の値 */);
}

// Line 205: 件数確認ログ
_logger.LogInformation("データソースを登録しています。件数: {Count}", dataTable.Rows.Count);

// Line 263: TotalCountパラメータ設定
report.SetParameterValue("TotalCount", dataTable.Rows.Count.ToString("0000"));
```

## 🔍 データ変換ポイントの詳細分析

### 1. **集約処理は存在しない**
- UnmatchListService内でのGroupBy/Distinct処理は**在庫マスタ最適化処理でのみ使用**
- アンマッチリスト生成では1伝票行 = 1アンマッチ項目の関係を維持

### 2. **FastReportでの1:1データ変換**
- `unmatchItems.ToList()` → `dataTable.Rows.Add()` は1:1の関係
- 411件のUnmatchItemは411行のDataTableに変換される
- `TotalCount`パラメータには`dataTable.Rows.Count`（411）が設定される

### 3. **PDF生成プロセス**
- FastReport.NETでの`report.Prepare()`処理
- DataTableの全行がPDFに反映される
- 表示上の改ページや折り返し処理

## 🚨 問題の真の原因（推定）

### 仮説1: PDF表示・印刷設定の問題
- FastReportテンプレート（.frxファイル）の設定問題
- ページサイズや表示行数の制限
- 改ページ設定の不備

### 仮説2: TotalCountパラメータの表示問題
- PDFのヘッダー部分で表示される"16件"は別の値を参照している可能性
- FastReportテンプレート内でのパラメータ参照先の誤り

### 仮説3: FastReportのバージョン固有の問題
- 使用中のFastReport.NETバージョンでのバグ
- 大量データ処理時の制限

## 💡 デバッグ用ログ追加提案

### 1. UnmatchListService.cs への追加
```csharp
// GenerateUnmatchListInternalAsync メソッド（Line 314前後）
_logger.LogCritical("===== GenerateUnmatchListInternalAsync 完了 =====");
_logger.LogCritical("総アンマッチ件数: {TotalCount}", unmatchItems.Count);
_logger.LogCritical("売上アンマッチ: {SalesCount}件", salesUnmatches.Count());
_logger.LogCritical("仕入アンマッチ: {PurchaseCount}件", purchaseUnmatches.Count());
_logger.LogCritical("在庫調整アンマッチ: {AdjustmentCount}件", adjustmentUnmatches.Count());
_logger.LogCritical("マスタ補完後: {EnrichedCount}件", enrichedItems.Count);
```

### 2. UnmatchListFastReportService.cs への追加
```csharp
// GenerateUnmatchListReport メソッド（Line 205前後）
_logger.LogCritical("===== FastReport データ処理詳細 =====");
_logger.LogCritical("入力 unmatchItems.Count(): {InputCount}", unmatchItems.Count());
_logger.LogCritical("変換後 unmatchList.Count: {ListCount}", unmatchList.Count);
_logger.LogCritical("DataTable.Rows.Count: {DataTableCount}", dataTable.Rows.Count);
_logger.LogCritical("TotalCountパラメータ値: {TotalCount}", dataTable.Rows.Count);

// report.Prepare()後の追加
_logger.LogCritical("FastReport.Prepare()完了 - レポート準備状態確認");
_logger.LogCritical("レポート内データソース行数: {ReportRowCount}", dataSource?.RowCount ?? -1);
```

### 3. Program.cs での追加
```csharp
// ExecuteUnmatchListAsync メソッド（Line 621前後）
logger.LogCritical("===== PDF生成開始前の最終確認 =====");
logger.LogCritical("result.UnmatchCount: {ResultCount}", result.UnmatchCount);
logger.LogCritical("result.UnmatchItems.Count(): {ItemsCount}", result.UnmatchItems.Count());
logger.LogCritical("PDF生成対象日付: {JobDate}", latestJobDate);

// PDF生成後の追加
logger.LogCritical("===== PDF生成完了後の確認 =====");
logger.LogCritical("PDFサイズ: {Size} bytes", pdfBytes?.Length ?? 0);
```

## 🔧 推奨調査アクション

### 即座に実行すべき調査

#### 1. **FastReportテンプレート(.frx)ファイルの確認**
```bash
# テンプレートファイルの場所
/src/InventorySystem.Reports/FastReport/Templates/UnmatchListReport.frx
```
- TotalCountパラメータの参照方法を確認
- 表示行数の制限設定を確認
- ページネーション設定を確認

#### 2. **デバッグログ追加による実行**
```bash
# 上記のログ追加後に実行
dotnet run -- create-unmatch-list 2025-06-30
```
- 各段階での正確な件数を記録
- FastReport内部でのデータ処理状況を確認

#### 3. **FastReportのDataSource詳細調査**
```csharp
// UnmatchListFastReportService.cs に追加
var dataSource = report.GetDataSource("UnmatchItems");
if (dataSource != null)
{
    _logger.LogCritical("DataSource行数: {RowCount}", dataSource.RowCount);
    _logger.LogCritical("DataSource有効状態: {Enabled}", dataSource.Enabled);
}
```

## 📋 調査対象ファイル

### 核心ファイル
1. **`/src/InventorySystem.Console/Program.cs`** - アンマッチリスト実行エントリーポイント
2. **`/src/InventorySystem.Core/Services/UnmatchListService.cs`** - アンマッチリスト生成ロジック
3. **`/src/InventorySystem.Reports/FastReport/Services/UnmatchListFastReportService.cs`** - PDF生成処理
4. **`/src/InventorySystem.Reports/FastReport/Templates/UnmatchListReport.frx`** - FastReportテンプレート

### DI設定確認済み
- **Program.cs Line 207**: `builder.Services.AddScoped<IUnmatchListService, UnmatchListService>();`
- **Program.cs Line 538**: `var unmatchListService = scopedServices.GetRequiredService<IUnmatchListService>();`

## 🏁 結論

**411明細が16件に変換される問題は、アプリケーションコード内での集約処理によるものではなく、FastReportテンプレートの設定またはFastReport.NET内部でのデータ処理に起因する可能性が高い。**

上記のデバッグログ追加と調査アクションにより、問題の正確な原因を特定できるはずです。

---
**調査完了時刻**: 2025年7月20日 23:27  
**次のアクション**: デバッグログ追加 → FastReportテンプレート調査 → 根本原因の特定