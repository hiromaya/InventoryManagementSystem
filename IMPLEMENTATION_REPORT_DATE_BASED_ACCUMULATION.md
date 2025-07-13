# 日付別累積在庫管理 実装レポート

**実装日**: 2025-07-13  
**実装者**: Claude Code  
**対象システム**: 在庫管理システム（InventoryManagementSystem）

## 1. 実装内容

### 1.1 実装した変更

#### ストアドプロシージャ `sp_MergeInventoryMasterCumulative` の修正

1. **MERGE条件にJobDateを追加**
   ```sql
   -- 修正前
   ON (
       target.ProductCode = source.ProductCode
       AND target.GradeCode = source.GradeCode
       AND target.ClassCode = source.ClassCode
       AND target.ShippingMarkCode = source.ShippingMarkCode
       AND LEFT(RTRIM(COALESCE(target.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = source.ShippingMarkName
   )
   
   -- 修正後
   ON (
       target.ProductCode = source.ProductCode
       AND target.GradeCode = source.GradeCode
       AND target.ClassCode = source.ClassCode
       AND target.ShippingMarkCode = source.ShippingMarkCode
       AND LEFT(RTRIM(COALESCE(target.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) = source.ShippingMarkName
       AND target.JobDate = @JobDate  -- JobDateを追加して日付別管理を実現
   )
   ```

2. **累積計算ロジックの修正**
   ```sql
   -- 修正前
   CurrentStock = ISNULL(target.CurrentStock, 0) + source.TotalSalesQty + ...
   
   -- 修正後
   CurrentStock = target.CurrentStock + source.TotalSalesQty + ...
   ```
   - 前日引継処理で既にCurrentStockに前日在庫が設定されているため、ISNULL処理を削除
   - JobDateフィールドの更新を削除（日付別管理のため変更不要）

### 1.2 バックアップファイル
- 場所: `/database/procedures/backup/sp_MergeInventoryMasterCumulative_backup_20250713.sql`
- 内容: 修正前の完全なストアドプロシージャコード

## 2. 期待される動作

### 2.1 修正前の問題
```
【6月1日】前月末在庫: 166件
【6月2日】import-folder実行後: 78件（166件が消失）
```

### 2.2 修正後の動作
```
【6月1日】前月末在庫: 166件（JobDate=2025-06-01）
【6月2日】import-folder実行
  1. 前日在庫引継: 166件を6月2日レコードとして作成
  2. 当日取引反映: 売上・仕入・調整を反映
  3. 結果: 
     - 6月1日: 166件（保持される）
     - 6月2日: 166件 + 新規商品
```

## 3. テスト方法

### 3.1 基本動作テスト
```bash
# データベースリセット
dotnet run -- init-database --force

# 6月1日: 前月末在庫登録
dotnet run -- init-inventory DeptA

# 6月2日: import-folder実行
dotnet run -- import-folder DeptA 2025-06-02

# 結果確認
# SQL Serverで test-queries/test_date_based_accumulation.sql を実行
```

### 3.2 確認ポイント
1. **6月1日のデータが消えていないこと**
   ```sql
   SELECT COUNT(*) FROM InventoryMaster WHERE JobDate = '2025-06-01'
   -- 期待値: 166件
   ```

2. **6月2日のデータが作成されていること**
   ```sql
   SELECT COUNT(*) FROM InventoryMaster WHERE JobDate = '2025-06-02'
   -- 期待値: 166件以上
   ```

3. **在庫の累積計算が正しいこと**
   ```sql
   SELECT ProductCode, JobDate, CurrentStock, DailyStock
   FROM InventoryMaster
   WHERE ProductCode = '10001'
   ORDER BY JobDate
   ```

## 4. 影響範囲

### 4.1 改善される機能
- ✅ アンマッチリスト: 特定日付の在庫が正しく取得できる
- ✅ 商品日報: 日次の在庫推移が正しく表示される
- ✅ 在庫表: 累積在庫が正しく計算される
- ✅ 日次終了処理: 過去データを保持したまま処理可能

### 4.2 追加の最適化（推奨）
1. **インデックスの追加**
   ```sql
   CREATE INDEX IX_InventoryMaster_CompositeKey_JobDate
   ON InventoryMaster (
       ProductCode, GradeCode, ClassCode, 
       ShippingMarkCode, ShippingMarkName, JobDate
   ) INCLUDE (CurrentStock, CurrentStockAmount);
   ```

2. **統計情報の更新**
   ```sql
   UPDATE STATISTICS InventoryMaster;
   ```

## 5. 注意事項

### 5.1 既存データへの影響
- 修正前に作成されたデータは、JobDateが上書きされている可能性がある
- 必要に応じて、過去データの復旧処理を検討

### 5.2 パフォーマンス
- JobDateをMERGE条件に追加したことで、適切なインデックスが必要
- 大量データ処理時は、バッチサイズの調整を検討

### 5.3 運用上の注意
- 同一日付で複数回import-folderを実行しても、データは累積される
- DataSetIdによる世代管理は引き続き有効

## 6. 今後の課題

1. **過去データの復旧方法の検討**
   - 伝票データから在庫履歴を再構築する処理

2. **パフォーマンス最適化**
   - 大量データ時のMERGE処理の最適化
   - パーティショニングの検討

3. **監視機能の追加**
   - 日付別在庫件数の推移監視
   - 異常な在庫変動の検知

## 7. 完了ステータス

- ✅ JobDateを含むMERGE条件の実装
- ✅ 累積計算ロジックの修正
- ✅ バックアップファイルの作成
- ✅ テストクエリの作成
- ⏳ 実環境でのテスト実行（SQL Server環境が必要）
- ⏳ インデックスの最適化