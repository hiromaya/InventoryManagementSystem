# アンマッチチェック処理 日付フィルタリング最適化

## 📋 実装概要

**変更内容**: 全期間対象から指定日以前のアクティブ在庫のみ対象に変更  
**目的**: パフォーマンス向上と論理的整合性の確保

### 🎯 変更前後の比較

| 項目 | 変更前 | 変更後 |
|------|--------|--------|
| 処理対象 | 全期間の在庫 | 指定日以前のアクティブ在庫 |
| パフォーマンス | 全データ処理で低速 | 対象データ限定で高速 |
| 論理性 | 時系列の概念なし | 指定日時点の状況を正確に反映 |

## 🔧 実装詳細

### 1. ストアドプロシージャの最適化

**ファイル**: `database/procedures/sp_CreateCpInventoryFromInventoryMasterCumulative.sql`

```sql
-- 追加されたフィルタリング条件
WHERE im.IsActive = 1  -- アクティブな在庫のみ対象
AND (@JobDate IS NULL OR im.JobDate <= @JobDate)  -- 指定日以前の在庫のみ
AND EXISTS (
    -- 伝票条件も指定日以前に変更
    SELECT 1 FROM SalesVouchers sv 
    WHERE (@JobDate IS NULL OR sv.JobDate <= @JobDate)
    -- 5項目キー条件...
)
```

### 2. UnmatchListService の拡張

**ファイル**: `src/InventorySystem.Core/Services/UnmatchListService.cs`

#### 新しいメソッド

```csharp
// 指定日以前対象の処理
Task<UnmatchListResult> ProcessUnmatchListAsync(DateTime targetDate);
Task<IEnumerable<UnmatchItem>> GenerateUnmatchListAsync(string dataSetId, DateTime targetDate);

// 内部実装
private async Task<UnmatchListResult> ProcessUnmatchListInternalAsync(DateTime? targetDate)
```

#### 伝票フィルタリング

```csharp
// 売上伝票
.Where(s => !targetDate.HasValue || s.JobDate <= targetDate.Value)

// 仕入伝票  
.Where(p => !targetDate.HasValue || p.JobDate <= targetDate.Value)

// 在庫調整
.Where(a => !targetDate.HasValue || a.JobDate <= targetDate.Value)
```

### 3. コマンドライン対応

**ファイル**: `src/InventorySystem.Console/Program.cs`

```bash
# 使用例
dotnet run unmatch-list 2025-06-15  # 指定日以前対象
dotnet run unmatch-list              # 全期間対象（従来通り）
```

## 📊 パフォーマンス最適化

### SQLインデックス追加

**ファイル**: `database/migrations/008_AddUnmatchOptimizationIndexes.sql`

#### 主要インデックス

1. **InventoryMaster**: `IsActive + JobDate`
```sql
CREATE INDEX IX_InventoryMaster_IsActive_JobDate 
ON InventoryMaster(IsActive, JobDate) 
INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);
```

2. **SalesVouchers**: `JobDate + VoucherType`
```sql
CREATE INDEX IX_SalesVouchers_JobDate_VoucherType 
ON SalesVouchers(JobDate, VoucherType, DetailType) 
INCLUDE (ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName);
```

3. **PurchaseVouchers**: `JobDate + VoucherType`
4. **InventoryAdjustments**: `JobDate + CategoryCode`
5. **CpInventoryMaster**: `5項目キー複合インデックス`

## 🧪 テスト方法

### 1. 段階的テスト

```bash
# Step 1: 月初時点のチェック
dotnet run unmatch-list 2025-06-01

# Step 2: 月中時点のチェック
dotnet run unmatch-list 2025-06-15

# Step 3: 月末時点のチェック
dotnet run unmatch-list 2025-06-30

# Step 4: 全期間チェック（従来通り）
dotnet run unmatch-list
```

### 2. パフォーマンス検証

```sql
-- インデックス効果の確認
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

-- アクティブ在庫フィルタリング
SELECT COUNT(*) FROM InventoryMaster 
WHERE IsActive = 1 AND JobDate <= '2025-06-30';

-- 売上伝票フィルタリング
SELECT COUNT(*) FROM SalesVouchers 
WHERE JobDate <= '2025-06-30' 
AND VoucherType IN ('51', '52') 
AND DetailType IN ('1', '2');
```

### 3. データ整合性確認

```bash
# 処理前後のレコード数比較
echo "=== 期間別データ件数 ==="
sqlcmd -Q "SELECT '2025-06-01以前' as 期間, COUNT(*) as 件数 FROM InventoryMaster WHERE IsActive=1 AND JobDate<='2025-06-01'"
sqlcmd -Q "SELECT '2025-06-15以前' as 期間, COUNT(*) as 件数 FROM InventoryMaster WHERE IsActive=1 AND JobDate<='2025-06-15'"
sqlcmd -Q "SELECT '2025-06-30以前' as 期間, COUNT(*) as 件数 FROM InventoryMaster WHERE IsActive=1 AND JobDate<='2025-06-30'"
```

## 💡 期待効果

### 1. パフォーマンス向上

- **データ量削減**: 未来日付データの除外
- **インデックス活用**: 日付・アクティブフラグでの高速フィルタリング
- **メモリ使用量削減**: 処理対象レコードの限定

### 2. 論理的整合性

- **時系列検証**: 指定日時点での在庫状況を正確に反映
- **段階的チェック**: 日付を進めながら問題箇所を特定可能
- **デバッグ容易性**: 期間限定でのトラブルシューティング

### 3. 運用効率

- **選択的チェック**: 必要な期間のみを対象とした検証
- **リソース最適化**: システムリソースの効率的利用
- **スケーラビリティ**: 大量データ環境への対応

## ⚠️ 注意事項

### 1. 互換性

- **既存機能**: 引数なしの場合は従来通り全期間対象
- **レポート出力**: PDF生成機能はそのまま利用可能

### 2. データ要件

- **アクティブフラグ**: `IsActive = 1` の在庫のみが対象
- **日付整合性**: JobDate の正確性が重要

### 3. 運用ガイドライン

- **段階的適用**: 小さな期間から始めて徐々に拡大
- **定期的監視**: パフォーマンス改善効果の測定
- **インデックス保守**: 統計情報の定期的な更新

## 🚀 次のステップ

### Windows環境での実行例

```bash
# 1. インデックス作成
sqlcmd -S localhost\SQLEXPRESS -d InventoryManagementDB -i database/migrations/008_AddUnmatchOptimizationIndexes.sql

# 2. パフォーマンステスト
dotnet run unmatch-list 2025-06-15

# 3. 結果比較
dotnet run unmatch-list  # 全期間との比較
```

---

**実装日**: 2025-07-12  
**実装者**: Claude Code with Gemini CLI consultation  
**バージョン**: v2.0 - Date Filtering Optimization