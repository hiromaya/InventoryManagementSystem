# 在庫マスタ最適化デバッグ実装完了報告

**実装日**: 2025年7月1日  
**目的**: 在庫マスタ最適化処理が0件となる問題の調査とデバッグ

## 📋 実装したデバッグログ一覧

### 1. SalesVoucherImportService.cs
- ✅ CSVヘッダー確認ログ追加
- ✅ 各レコード読み込み時のJobDate確認
- ✅ エンティティ変換後のJobDate比較
- ✅ **重要**: JobDateをパラメータで上書きする処理を追加

### 2. PurchaseVoucherImportService.cs
- ✅ CSVヘッダー確認ログ追加
- ✅ 各レコード読み込み時のJobDate確認
- ✅ エンティティ変換後のJobDate比較
- ✅ **重要**: JobDateをパラメータで上書きする処理を追加

### 3. InventoryAdjustmentImportService.cs
- ✅ CSVヘッダー確認ログ追加
- ✅ 各レコード読み込み時のJobDate確認
- ✅ エンティティ変換後のJobDate比較
- ✅ **重要**: JobDateをパラメータで上書きする処理を追加

### 4. SalesVoucherDaijinCsv.cs (ParseDateメソッド)
- ✅ 入力値の詳細ログ
- ✅ 各解析段階の成功/失敗ログ
- ✅ **修正**: InvariantCultureを優先使用
- ✅ システムロケールでのフォールバック処理

### 5. InventoryMasterOptimizationService.cs
- ✅ 検索条件の詳細ログ
- ✅ SQLパラメータの型確認
- ✅ 各テーブルでの取得件数確認
- ✅ **重要**: 0件時の全JobDate確認クエリ
- ✅ 直近24時間の伝票件数確認

### 6. SalesVoucherCsvRepository.cs (BulkInsertAsyncメソッド)
- ✅ 保存前データの詳細確認
- ✅ 最初の5件のJobDate確認
- ✅ **重要**: JobDateの分布状況ログ

### 7. Program.cs (ExecuteImportFromFolderAsync)
- ✅ 各伝票インポート開始/完了時のログ
- ✅ JobDateパラメータの確認
- ✅ データセットID追跡

### 8. appsettings.json
- ✅ デバッグログレベルの設定追加
- ✅ 対象サービス/リポジトリのDebugレベル有効化

## 🔍 実装した重要な修正

### 問題1: JobDateの上書き処理追加
**問題**: CSVの日付がパラメータのjobDateと異なる場合の処理
**修正**: 全ての伝票インポートサービスでJobDateを強制上書き
```csharp
// JobDateをパラメータで上書き（重要な修正）
salesVoucher.JobDate = jobDate;
```

### 問題2: ParseDateメソッドの改善
**問題**: ロケール依存の日付解析
**修正**: InvariantCultureを優先、システムロケールをフォールバック
```csharp
// InvariantCultureで解析（優先）
if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))

// フォールバック: システムロケールで解析
if (DateTime.TryParse(dateStr, out var systemParsedDate))
```

### 問題3: 最適化処理での詳細ログ
**問題**: なぜ0件なのかがわからない
**修正**: 全JobDate確認、直近データ確認ログ追加
```csharp
// 0件時の詳細確認
var allDates = await connection.QueryAsync<DateTime>(
    "SELECT DISTINCT CAST(JobDate AS DATE) as JobDate FROM SalesVouchers ORDER BY JobDate DESC");
```

## 🧪 デバッグ実行手順

### 1. デバッグログ有効化
```bash
# appsettings.jsonでDebugレベル有効化済み
```

### 2. デバッグ実行
```bash
cd /home/hiroki/projects/InventoryManagementSystem/src/InventorySystem.Console
dotnet run -- import-folder DeptA 2025-06-30 > debug_log.txt 2>&1
```

### 3. ログ確認項目
1. **CSVのJobDate列の実際の値**
2. **パラメータjobDateとの比較結果**
3. **JobDate上書き処理の実行確認**
4. **最適化処理での検索条件**
5. **データベース内の実際のJobDate分布**

## 📊 期待される結果

### Before（修正前）
```
売上商品数: 0件
仕入商品数: 0件
在庫調整商品数: 0件
```

### After（修正後・期待値）
```
売上商品数: 数百～数千件
仕入商品数: 数百件
在庫調整商品数: 数十～数百件
```

## 🔧 追加の調査SQL

### データベース内JobDate確認
```sql
-- JobDateの分布確認
SELECT 
    CAST(JobDate AS DATE) as JobDate,
    COUNT(*) as RecordCount
FROM SalesVouchers
WHERE JobDate >= '2025-06-25' AND JobDate <= '2025-07-05'
GROUP BY CAST(JobDate AS DATE)
ORDER BY JobDate;

-- 特定日付のデータ詳細
SELECT TOP 10
    VoucherNumber,
    VoucherDate,
    JobDate,
    DataSetId,
    CreatedDate
FROM SalesVouchers
WHERE CAST(JobDate AS DATE) = '2025-06-30'
ORDER BY CreatedDate DESC;
```

## 📈 期待される問題解決

1. **JobDate不一致問題**: パラメータで強制上書きにより解決
2. **ロケール依存問題**: InvariantCulture優先により解決
3. **可視性問題**: 詳細ログにより問題箇所特定可能

## 🚀 次のステップ

1. **デバッグ実行**: 上記手順でデバッグログを確認
2. **問題特定**: ログから根本原因を特定
3. **最終修正**: 必要に応じて追加修正
4. **本番適用**: 修正確認後、本番環境に適用

---

**実装者**: Claude Code  
**実装完了日時**: 2025年7月1日
**対象ファイル数**: 8ファイル  
**追加ログ数**: 約30箇所