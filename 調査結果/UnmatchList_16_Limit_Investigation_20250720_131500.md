# アンマッチリスト16件制限の原因調査結果

## 📝 調査概要

**問題**: アンマッチリストで411件のアンマッチが検出されているにも関わらず、PDFには16件のみが表示される

**調査日時**: 2025-07-20 13:15:00

## 🔍 調査結果

### 1. コードレベルでの数値制限調査

#### ✅ 確認済み箇所（制限なし）
- **UnmatchListService.cs**: `.Take(16)` や TOP 16 などの制限コードは存在しない
- **SalesVoucherRepository.cs**: `GetByDataSetIdAsync` メソッドに制限なし
- **UnmatchListFastReportService.cs**: データ処理ループに制限なし
- **UnmatchListReport.frx**: FastReportテンプレートに行数制限設定なし

#### ⚠️ 発見した軽微な問題
- **FastReportService.GetCategoryName()** メソッドに誤った伝票区分マッピング：
  ```csharp
  "11" => "掛売上",  // 間違い（11は掛仕入）
  "12" => "現金売上", // 間違い（12は現金仕入）
  ```
  - 正しくは: "51"→掛売上、"52"→現金売上、"11"→掛仕入、"12"→現金仕入

### 2. データフロー分析

#### ✅ 正常に動作している箇所
- **UnmatchListService.CheckSalesUnmatchAsync**: 411件を正常に検出・処理
- **UnmatchItem.FromSalesVoucher**: 正しい Category 設定（GetTransactionTypeName使用）
- **データ取得クエリ**: SQL文に制限句なし

#### ❓ 疑問箇所
- **FastReportService.GenerateUnmatchListReport**: DataTable作成〜PDF出力間のどこかで16件に制限
- **FastReport内部処理**: .NET 8環境での予期しない動作

### 3. 実際のPDF内容分析

#### 確認した事実
- **表示件数**: 16行のみ
- **内容**: すべて「掛売上」の「在庫0」エラー
- **現金売上や該当無エラーは0件**: これは不自然（411件中に混在するはず）

### 4. 推定される原因候補

#### 🔥 最有力候補
1. **FastReport処理中の例外/エラー**: ログに記録されない軽微なエラーで16件目以降の処理が停止
2. **DataBand処理の中断**: FastReport内部でのページング処理異常
3. **メモリ制限**: 大量データ処理時のメモリ不足による処理中断

#### 🔍 次点候補
4. **条件フィルタの誤適用**: 何らかの条件により16件のみが対象となっている
5. **DataSource設定の問題**: FastReportのデータソース登録で一部のみが有効化

## 🎯 次の調査手順

### Phase 1: FastReport処理の詳細調査
1. **DataTable.Rows.Count** の実際の値をログ出力
2. **FastReport.Prepare()** 前後でのデータ件数確認
3. **PDF生成完了後** のバイト数とページ数確認

### Phase 2: エラーログの徹底調査
1. **FastReport内部エラー** の捕捉強化
2. **例外ハンドリング** の詳細化
3. **処理中断の検出** ロジック追加

### Phase 3: データソース検証
1. **report.RegisterData()** 後のデータ確認
2. **DataSource.Enabled** 状態の確認
3. **Template内DataBand** の実際の処理件数取得

## 🚨 緊急対応が必要な項目

### 1. GetCategoryName修正（軽微だが誤解の原因）
```csharp
// 修正前
"11" => "掛売上",
"12" => "現金売上",

// 修正後  
"51" => "掛売上",
"52" => "現金売上",
"11" => "掛仕入", 
"12" => "現金仕入",
```

### 2. 詳細ログ追加
- DataTable作成後の件数
- FastReport.Prepare()の成功/失敗
- PDF生成時の実際の処理行数

## 📊 調査統計

| 調査項目 | 結果 | 詳細 |
|---------|------|------|
| ソースコード内16の検索 | 制限なし | 16という数値による制限は未発見 |
| SQL TOP句の調査 | 制限なし | データ取得クエリに行数制限なし |
| FastReportテンプレート | 制限なし | .frxファイルに行数制限設定なし |
| 伝票区分マッピング | 軽微な誤り発見 | GetCategoryNameに誤った対応表 |

## 🔗 関連ファイル

### 主要調査対象
- `/src/InventorySystem.Core/Services/UnmatchListService.cs`
- `/src/InventorySystem.Reports/FastReport/Services/UnmatchListFastReportService.cs`  
- `/src/InventorySystem.Reports/FastReport/Templates/UnmatchListReport.frx`
- `/src/InventorySystem.Data/Repositories/SalesVoucherRepository.cs`

### 発見した問題箇所
- `/src/InventorySystem.Reports/FastReport/Services/UnmatchListFastReportService.cs:356-369` (GetCategoryName)

## 📈 結論

**16件制限の根本原因は未特定**。コードレベルでの明示的な制限は存在しないため、**FastReport処理中の隠れたエラーまたはデータソース処理の異常**が最も可能性が高い。

次の調査フェーズでは、FastReport内部での実際のデータ処理状況を詳細に監視し、16件目以降でエラーが発生していないかを確認する必要がある。

---
**調査者**: Claude Code  
**調査時間**: 約45分  
**調査ファイル数**: 15ファイル  
**発見した問題**: 1件（軽微）