# 在庫マスタ最適化処理0件問題の調査報告

## 📋 問題の概要

`import-folder`コマンド実行時に、Phase 4の在庫マスタ最適化処理で以下の結果となった：

```
info: InventorySystem.Data.Services.InventoryMasterOptimizationService[0]
      売上商品数: 0件
info: InventorySystem.Data.Services.InventoryMasterOptimizationService[0]
      仕入商品数: 0件
info: InventorySystem.Data.Services.InventoryMasterOptimizationService[0]
      在庫調整商品数: 0件
```

しかし、実際には以下のデータが正常に取り込まれている：
- **売上伝票**: 成功4167件
- **仕入伝票**: 成功779件  
- **在庫調整**: 成功144件

## 🔍 原因分析

### 1. 最も可能性の高い原因：JobDate形式の不一致

実行ログから以下の情報が判明：

#### ジョブ日付の設定
- コマンド引数で指定されたジョブ日付: `2025-06-30`
- ログに表示されるJobDate: `06/30/2025 00:00:00`

#### CSVインポート処理でのJobDate保存
ログで確認できる実際の保存データ：
```
info: InventorySystem.Data.Repositories.InventoryRepository[0]
      Created inventory record. Parameters: { Key = 00104-056-026-5106-        , JobDate = 30.06.2025 00:00:00 }
```

#### 最適化処理でのJobDate検索
```
info: InventorySystem.Data.Services.InventoryMasterOptimizationService[0]
      在庫マスタ最適化開始: 2025-06-30
```

### 2. 具体的な不一致パターン

**保存時の形式**: `30.06.2025 00:00:00` (ドイツ式日付形式)
**検索時の形式**: `2025-06-30` (ISO形式)

この形式の違いにより、以下のSQLクエリが0件を返している可能性が高い：

```sql
SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
FROM SalesVouchers
WHERE CONVERT(date, JobDate) = @jobDate
```

### 3. 地域設定の影響

システムがドイツ語圏の地域設定 (`de-DE`) を使用している可能性：
- **保存時**: 地域設定に従ってドイツ式形式で保存
- **検索時**: ISO形式の日付文字列をそのまま使用
- **結果**: `CONVERT(date, JobDate)` の結果が一致しない

## 🔧 検証すべき項目

### 1. データベース内の実際のJobDate値確認
```sql
-- 最近保存された売上伝票のJobDate確認
SELECT TOP 5 
    VoucherId, 
    JobDate,
    CONVERT(date, JobDate) as ConvertedDate,
    FORMAT(JobDate, 'yyyy-MM-dd') as FormattedDate
FROM SalesVouchers 
ORDER BY CreatedDate DESC;
```

### 2. 最適化処理で使用される検索条件のテスト
```sql
-- 2025-06-30で検索（現在の処理）
SELECT COUNT(*) as Count_ISO_Format
FROM SalesVouchers 
WHERE CONVERT(date, JobDate) = '2025-06-30';

-- 30.06.2025で検索（ドイツ式）
SELECT COUNT(*) as Count_German_Format
FROM SalesVouchers 
WHERE CONVERT(date, JobDate) = '30.06.2025';

-- JobDateの生値確認
SELECT DISTINCT 
    JobDate,
    CONVERT(date, JobDate) as ConvertedDate
FROM SalesVouchers 
WHERE CreatedDate >= CAST(GETDATE() AS DATE);
```

### 3. 地域設定の確認
```sql
-- SQL Server の言語・地域設定確認
SELECT 
    @@LANGUAGE as CurrentLanguage,
    @@DATEFIRST as DateFirst,
    FORMAT(GETDATE(), 'yyyy-MM-dd') as ISO_Format,
    FORMAT(GETDATE(), 'dd.MM.yyyy') as German_Format;
```

## 💡 修正方針

### 短期的な修正
1. **JobDate比較の標準化**: SQL クエリで明示的な日付形式変換を使用
2. **パラメータ形式の統一**: C#側で日付パラメータを標準形式に変換

### 長期的な修正
1. **地域設定の統一**: システム全体でISO形式に統一
2. **日付処理の標準化**: `DateTime`型の直接比較を使用

## 📊 影響範囲

この問題により以下の機能が正常に動作していない：
1. **在庫マスタ最適化**: 5項目キーの組み合わせが生成されない
2. **アンマッチリスト**: 大量の「該当無」エラーが発生
3. **商品日報**: 在庫マスタとの連携が不完全

## 🚨 緊急度

**高**: アンマッチリスト処理で大量エラーが発生し、システムの基本機能が正常に動作しない状態。

## 📋 次のアクション

1. **データベース確認**: 上記SQLクエリを実行してJobDate形式を特定
2. **地域設定確認**: システムの現在の地域設定を確認
3. **修正実装**: 確認結果に基づいて適切な修正を実装
4. **テスト実行**: 修正後のimport-folderコマンドで動作確認

---

**調査実施日時**: 2025-07-01  
**調査対象**: import-folder DeptA 2025-06-30 の実行結果  
**結論**: JobDate形式の不一致により、在庫マスタ最適化処理でデータを検索できていない