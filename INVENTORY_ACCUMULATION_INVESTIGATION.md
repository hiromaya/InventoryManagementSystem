# 在庫累積管理問題の詳細調査報告書

**調査日時**: 2025-07-13 
**調査者**: Claude Code
**調査目的**: 在庫が累積されず日付ごとに独立管理される問題の原因特定

## 1. 問題の症状
- 6月1日: 前月末在庫166件（DataSetId=""）
- 6月2日: import-folder実行後78件（166件消失）

## 2. InventoryMasterOptimizationService 分析

### 2.1 OptimizeInventoryMasterAsync の処理フロー
```csharp
public async Task<int> OptimizeInventoryMasterAsync(DateTime jobDate)
{
    // ランダム文字列を含むDataSetId生成
    var random = GenerateRandomString(6);
    var dataSetId = $"IMPORT_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}_{random}";
    
    // 月初の場合は前月末在庫処理を追加
    if (jobDate.Day == 1)
    {
        await HandleMonthStartInventoryAsync(jobDate);
    }
    
    var result = await OptimizeAsync(jobDate, dataSetId);
    return result.ProcessedCount;
}
```

### 2.2 既存データの処理

**重要な発見**: 既存データの削除処理は**存在しない**

`InheritPreviousDayInventoryAsync` メソッド（451-502行目）が前日在庫を引き継ぐ処理を実装：
```csharp
private async Task<int> InheritPreviousDayInventoryAsync(
    SqlConnection connection, 
    SqlTransaction transaction, 
    DateTime jobDate)
{
    var previousDate = jobDate.AddDays(-1);
    
    const string inheritSql = @"
        -- 前日の在庫マスタを当日にコピー（CurrentStockを引き継ぎ）
        INSERT INTO InventoryMaster (
            ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
            ProductName, Unit, StandardPrice, ProductCategory1, ProductCategory2,
            JobDate, CreatedDate, UpdatedDate,
            CurrentStock, CurrentStockAmount, DailyStock, DailyStockAmount, DailyFlag,
            PreviousMonthQuantity, PreviousMonthAmount, UnitPrice
        )
        SELECT 
            prev.ProductCode, prev.GradeCode, prev.ClassCode, 
            prev.ShippingMarkCode, prev.ShippingMarkName,
            prev.ProductName, prev.Unit, prev.StandardPrice, 
            prev.ProductCategory1, prev.ProductCategory2,
            @JobDate, GETDATE(), GETDATE(),
            prev.CurrentStock, prev.CurrentStockAmount,  -- 前日在庫を引き継ぎ
            prev.CurrentStock, prev.CurrentStockAmount,  -- 日次在庫も初期値として設定
            '9',  -- 未処理フラグ
            prev.PreviousMonthQuantity, prev.PreviousMonthAmount,
            prev.UnitPrice
        FROM InventoryMaster prev
        WHERE CAST(prev.JobDate AS DATE) = CAST(@PreviousDate AS DATE)
            AND NOT EXISTS (
                -- 当日のデータが既に存在する場合はスキップ（月初処理との重複回避）
                SELECT 1 FROM InventoryMaster curr
                WHERE curr.ProductCode = prev.ProductCode
                    AND curr.GradeCode = prev.GradeCode
                    AND curr.ClassCode = prev.ClassCode
                    AND curr.ShippingMarkCode = prev.ShippingMarkCode
                    AND curr.ShippingMarkName = prev.ShippingMarkName
                    AND CAST(curr.JobDate AS DATE) = CAST(@JobDate AS DATE)
            );";
```

### 2.3 前日在庫の引き継ぎ
✅ **引き継ぎ処理は実装されている**
- `InheritPreviousDayInventoryAsync`が前日の在庫を当日にコピー
- CurrentStock（現在在庫）を引き継ぐ処理が正しく実装

## 3. 在庫累積の実装状況

### 3.1 現在の動作

**根本原因は `sp_MergeInventoryMasterCumulative` ストアドプロシージャの実装にある**

```sql
-- 既存レコード：在庫を累積更新
WHEN MATCHED THEN
    UPDATE SET
        -- 在庫数量の累積更新
        CurrentStock = ISNULL(target.CurrentStock, 0) + source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
        CurrentStockAmount = ISNULL(target.CurrentStockAmount, 0) + source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
        
        -- 当日在庫の更新
        DailyStock = source.TotalSalesQty + source.TotalPurchaseQty + source.TotalAdjustmentQty,
        DailyStockAmount = source.TotalSalesAmount + source.TotalPurchaseAmount + source.TotalAdjustmentAmount,
        
        -- メタデータの更新
        JobDate = @JobDate,  -- 最終更新日として使用
        UpdatedDate = GETDATE(),
        DataSetId = @DataSetId,
        DailyFlag = N'0'  -- データありフラグ
```

**問題点**: 
- MERGEの`ON`句でJobDateを含めていない
- そのため、異なる日付の同一商品キーが**同じレコード**として扱われる
- 結果として、前日の在庫に当日の取引が累積される

### 3.2 期待される動作

本来のシステム設計：
1. 各日付（JobDate）ごとに独立した在庫レコードを作成
2. 前日在庫を引き継いで当日レコードを作成
3. 当日の取引を当日レコードに反映

### 3.3 ギャップ分析

**設計と実装の不一致**：
- テーブル設計: プライマリキーに`JobDate`を含む（日付別管理）
- ストアドプロシージャ: JobDateを無視してMERGE（累積管理）

この不一致により：
1. 前月末在庫（6/1）が作成される
2. import-folder（6/2）実行時、前日在庫引継処理が動作
3. しかし、MERGEストアドが既存レコード（6/1）を更新してしまう
4. 結果として6/1のデータが上書きされ、6/2のデータのみが残る

## 4. 根本原因

### 4.1 主要因
**`sp_MergeInventoryMasterCumulative` のMERGE条件にJobDateが含まれていない**

```sql
ON (
    target.ProductCode = source.ProductCode
    AND target.GradeCode = source.GradeCode
    AND target.ClassCode = source.ClassCode
    AND target.ShippingMarkCode = source.ShippingMarkCode
    AND LEFT(RTRIM(COALESCE(target.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = source.ShippingMarkName
)
-- JobDateが含まれていない！
```

### 4.2 副次的要因
1. テーブル定義とストアドプロシージャの設計思想の不一致
2. 前日在庫引継処理は正しく実装されているが、その後のMERGE処理で台無しになる

## 5. 影響範囲

### 5.1 影響を受ける機能
- [x] アンマッチリスト（特定日付の在庫が見つからない）
- [x] 商品日報（日次の在庫推移が正しく表示されない）
- [x] 在庫表（累積在庫が正しく計算されない）
- [x] その他（日次終了処理など）

### 5.2 データ整合性への影響
- 過去の在庫履歴が失われる
- 日付別の在庫推移が追跡できない
- 前月末在庫との照合が不可能になる

## 6. 結論

**問題の核心**：
- システムは「日付別在庫管理」として設計されている
- しかし、ストアドプロシージャが「累積在庫管理」として実装されている
- この矛盾により、前日の在庫データが上書きされ消失する

**緊急度**: **高**
- データの完全性が損なわれている
- 在庫履歴の追跡が不可能
- アンマッチリストなど基本機能が正しく動作しない

**修正方針**：
1. `sp_MergeInventoryMasterCumulative`のON句にJobDateを追加
2. 各日付ごとに独立したレコードを管理するよう修正
3. 既存データの復旧方法を検討