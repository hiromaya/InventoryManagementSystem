# 在庫管理システム 累積管理実装状況調査報告書

**調査日時**: 2025-01-10 15:00:00
**調査者**: Claude Code
**調査目的**: 前月末在庫引き継ぎ問題の実装状況確認

## 1. エグゼクティブサマリー
- **累積管理の実装状況**: 実装済み
- **問題の解決状況**: 部分的に解決済み（重要な問題点あり）
- **緊急度**: 高

## 2. 詳細調査結果

### 2.1 import-folderコマンド
- ファイル: Program.cs
- メソッド: ExecuteImportFromFolderAsync
- 現在の実装:
  ```csharp
  // Phase 4: 在庫マスタ最適化
  if (optimizationService != null && startDate.HasValue && endDate.HasValue)
  {
      var currentDate = startDate.Value;
      while (currentDate <= endDate.Value)
      {
          var dataSetId = $"AUTO_OPTIMIZE_{currentDate:yyyyMMdd}_{DateTime.Now:HHmmss}";
          var result = await optimizationService.OptimizeAsync(currentDate, dataSetId);
          // ...
      }
  }
  ```
- 問題点: import-folderコマンド実行時に毎回在庫マスタ最適化（OptimizeAsync）が呼ばれている

### 2.2 在庫マスタ最適化サービス
- ファイル: InventoryMasterOptimizationService.cs
- メソッド: OptimizeAsync
- 前日在庫の取得: **あり**
- 実装内容:
  ```csharp
  // 4.5. 前日在庫の引き継ぎ処理（累積管理のため）
  var inheritResult = await InheritPreviousDayInventoryAsync(connection, transaction, jobDate);
  _logger.LogInformation("前日在庫引き継ぎ完了: {Count}件", inheritResult);
  ```

  ```csharp
  private async Task<int> InheritPreviousDayInventoryAsync(...)
  {
      var previousDate = jobDate.AddDays(-1);
      const string inheritSql = @"
          -- 前日の在庫マスタを当日にコピー（CurrentStockを引き継ぎ）
          INSERT INTO InventoryMaster (...)
          SELECT 
              prev.ProductCode, ...,
              prev.CurrentStock, prev.CurrentStockAmount,  -- 前日在庫を引き継ぎ
              ...
          FROM InventoryMaster prev
          WHERE CAST(prev.JobDate AS DATE) = CAST(@PreviousDate AS DATE)
              AND NOT EXISTS (
                  -- 当日のデータが既に存在する場合はスキップ
                  SELECT 1 FROM InventoryMaster curr
                  WHERE curr.ProductCode = prev.ProductCode
                      AND ... -- 5項目キーで比較
                      AND CAST(curr.JobDate AS DATE) = CAST(@JobDate AS DATE)
              );";
  }
  ```

### 2.3 在庫リポジトリ
- ファイル: InventoryRepository.cs
- DELETE処理の有無: **なし**（累積管理に対応済み）
- 該当箇所:
  ```csharp
  // UpdateOrCreateFromVouchersAsync メソッド
  // sp_UpdateOrCreateInventoryMasterCumulativeストアドプロシージャを呼び出し
  // DELETEではなくMERGE処理を使用
  ```

### 2.4 ストアドプロシージャ
- ファイル: sp_MergeInventoryMasterCumulative.sql
- 処理方式: **MERGE**（累積管理対応）
- 実装内容:
  ```sql
  -- 既存レコード：在庫を累積更新
  WHEN MATCHED THEN
      UPDATE SET
          -- 在庫数量の累積更新
          CurrentStock = ISNULL(target.CurrentStock, 0) + source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
          CurrentStockAmount = ISNULL(target.CurrentStockAmount, 0) + source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
          ...
  ```

## 3. 問題の根本原因

1. **前日在庫の引き継ぎ処理の重複実行**
   - `InheritPreviousDayInventoryAsync`で前日在庫を当日にINSERT
   - その後、`sp_MergeInventoryMasterCumulative`で伝票データから累積更新
   - **問題**: 前日在庫が当日にコピーされるが、当日に伝票がない商品は前日のCurrentStockがそのまま残る

2. **5項目キーの部分的な考慮**
   - 前日在庫の引き継ぎ時：すべての前日在庫商品が対象
   - MERGE処理時：当日伝票に存在する商品のみが対象
   - **結果**: 当日伝票に存在しない商品は前日の在庫が保持される（これは正しい動作）

## 4. 影響範囲
- 影響を受ける処理: import-folderコマンド実行時の在庫マスタ最適化
- データへの影響: 前月末在庫は正しく引き継がれるが、日付管理に課題あり

## 5. 推奨される修正方針
※コード修正は行わないため、修正すべき箇所の指摘のみ

1. **在庫マスタ最適化の実行タイミング見直し**
   - import-folderコマンドからの自動実行を削除検討
   - 別コマンドとして独立させる

2. **JobDate管理の明確化**
   - 在庫マスタのJobDateは「最終更新日」として使用されている
   - 累積管理においてJobDateの意味を再定義する必要あり

3. **前日在庫引き継ぎロジックの最適化**
   - NOT EXISTS条件により重複は防がれているが、処理順序の明確化が必要

## 6. 調査で発見したその他の問題

1. **DataSetId管理**
   - OptimizeAsyncで`AUTO_OPTIMIZE_`プレフィックスのDataSetIdを生成
   - ImportTypeフィールドが使用されていない

2. **月初処理の特別扱い**
   - `HandleMonthStartInventoryAsync`で前月末在庫を処理
   - 通常の前日在庫引き継ぎとの整合性確認が必要

## 7. 結論

累積管理は実装されており、前月末在庫が引き継がれる仕組みになっています。具体的には：

1. ✅ **前日在庫の引き継ぎ機能が実装されている**
2. ✅ **DELETEによる既存データ削除は行われていない**
3. ✅ **MERGEによる累積更新が実装されている**
4. ⚠️ **ただし、import-folderコマンドで毎回最適化処理が走るため、処理の重複や不整合のリスクがある**

前月末在庫が消える問題は基本的に解決されていますが、運用方法によっては問題が発生する可能性があります。特に、import-folderコマンドを同じ日付で複数回実行した場合の動作について、さらなる検証が必要です。