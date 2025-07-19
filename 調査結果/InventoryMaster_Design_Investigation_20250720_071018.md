# InventoryMaster設計とJobDate使用方法 調査報告書

## 調査日時
2025-07-20 07:10:18

## 1. テーブル設計の確認

### InventoryMasterテーブル定義
```sql
-- docs/database/01_create_tables.sql より抜粋
CREATE TABLE InventoryMaster (
    -- 複合キー（5項目）  ← ⚠️ コメントと実際の定義が不一致
    ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
    GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
    ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
    ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
    ShippingMarkName NVARCHAR(50) NOT NULL,     -- 荷印名
    
    -- 日付管理（汎用日付2＝ジョブデート必須）
    JobDate DATE NOT NULL,                      -- 汎用日付2（ジョブデート）
    CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    -- 在庫情報
    CurrentStock DECIMAL(18,4) NOT NULL DEFAULT 0,
    CurrentStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
    DailyStock DECIMAL(18,4) NOT NULL DEFAULT 0,
    DailyStockAmount DECIMAL(18,4) NOT NULL DEFAULT 0,
    
    -- 複合主キー
    CONSTRAINT PK_InventoryMaster PRIMARY KEY (
        ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate
    )  -- ← 実際は6項目（JobDateを含む）
);
```

### 主キー構成
**実際の主キー構成（6項目）**：
1. ProductCode（商品コード）
2. GradeCode（等級コード）
3. ClassCode（階級コード）
4. ShippingMarkCode（荷印コード）
5. ShippingMarkName（荷印名）
6. **JobDate（汎用日付2）** ← 重要：主キーに含まれる

### JobDateカラムの定義
```sql
JobDate DATE NOT NULL,  -- 汎用日付2（ジョブデート）
```
**コメント**: "汎用日付2＝ジョブデート必須"

## 2. 設計思想の分析

### 在庫マスタの管理方式
- ✅ **日付別管理（履歴保持）**
- ❌ 最新状態のみ管理

### 根拠
1. **主キーにJobDateが含まれている**
   - 5項目キー + JobDate = 6項目の複合主キー
   - 同じ商品（5項目キー）でも、JobDateが異なれば別レコードとして管理される

2. **InheritPreviousDayInventoryAsyncメソッドのコメント**
   ```csharp
   /// <summary>
   /// 前日在庫を当日に引き継ぐ処理（累積管理のため）
   /// </summary>
   ```
   - 「累積管理のため」という明確な設計意図

3. **コメント内の記述**
   ```sql
   -- 前日の在庫マスタを当日にコピー（CurrentStockを引き継ぎ）
   ```

## 3. 前日在庫引き継ぎ処理の設計意図

### InheritPreviousDayInventoryAsyncの実装概要
```csharp
private async Task<int> InheritPreviousDayInventoryAsync(
    SqlConnection connection, 
    SqlTransaction transaction, 
    DateTime jobDate)
{
    var previousDate = jobDate.AddDays(-1);
    
    // 前日の在庫マスタを当日にコピー（CurrentStockを引き継ぎ）
    const string inheritSql = @"
        INSERT INTO InventoryMaster (...)
        SELECT 
            prev.ProductCode, prev.GradeCode, prev.ClassCode, 
            prev.ShippingMarkCode, 
            LEFT(RTRIM(COALESCE(prev.ShippingMarkName, '')) + REPLICATE(' ', 8), 8) as ShippingMarkName,
            -- 他のカラム...
            @JobDate,  -- 当日の日付を設定
            -- 在庫情報...
            prev.CurrentStock, prev.CurrentStockAmount,  -- 前日在庫を引き継ぎ
        FROM InventoryMaster prev
        WHERE CAST(prev.JobDate AS DATE) = CAST(@PreviousDate AS DATE)
            AND NOT EXISTS (当日データ重複チェック);";
}
```

### 設計との整合性
- ✅ **設計に準拠**: JobDateが主キーに含まれているため、日付別の在庫履歴管理が正しい設計
- ✅ **累積管理**: 前日の在庫状態を当日に引き継ぎ、当日の取引を反映する方式

## 4. JobDateの正しい使用方法

### 伝票系での使用（フィルタリング目的）
```sql
-- SalesVouchers, PurchaseVouchers, InventoryAdjustments
SELECT DISTINCT ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName
FROM SalesVouchers
WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE);
```
**用途**: 指定日の伝票データを抽出するためのフィルタリング条件

### 在庫マスタでの使用（履歴管理のキー）
```sql
-- InventoryMaster
CONSTRAINT PK_InventoryMaster PRIMARY KEY (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate
)
```
**用途**: 日付別の在庫状態を管理するための主キーの一部

## 5. 他のテーブルとの設計思想の比較

### 伝票系テーブル（SalesVoucher, PurchaseVoucher）
| 項目 | 詳細 |
|------|------|
| **主キー構成** | `(VoucherId, LineNumber)` |
| **JobDateの位置** | 主キーに含まれない（通常のカラム） |
| **管理方式** | 伝票単位での管理 |
| **JobDateの用途** | フィルタリング用（処理対象日の絞り込み） |
| **設計思想** | トランザクション記録（変更されない静的データ） |

### 在庫マスタ（InventoryMaster）
| 項目 | 詳細 |
|------|------|
| **主キー構成** | `(ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate)` |
| **JobDateの位置** | 主キーの一部（6番目の要素） |
| **管理方式** | 日付別履歴管理 |
| **JobDateの用途** | 履歴管理のキー（日付別の状態管理） |
| **設計思想** | 状態管理（日々変化する動的データ） |

## 6. 処理フローの分析

### import-folderコマンドの処理フロー
```
Phase 1-3: CSVファイルのインポート
  ├── マスタデータ（商品、得意先、仕入先等）
  ├── 売上伝票データ
  ├── 仕入伝票データ
  └── 在庫調整データ

Phase 4: 在庫マスタ最適化 ← 核心処理
  └── optimizationService.OptimizeAsync(currentDate, dataSetId)
```

### OptimizeAsyncメソッドの詳細フロー
```
1. 売上商品の取得（JobDateでフィルタリング）
   ↓
2. 仕入商品の取得（JobDateでフィルタリング）
   ↓
3. 在庫調整商品の取得（JobDateでフィルタリング）
   ↓
4. 商品の統合（重複除去）
   ↓
5. 前日在庫の引き継ぎ処理（累積管理のため）← 重要
   ↓
6. MERGE文で一括処理（当日取引の反映）
```

### import-folderコマンドでの日付指定の意味
- **フィルタリング**: CSVから指定JobDateの伝票データを抽出
- **履歴作成**: 指定JobDateの在庫マスタレコードを作成
- **累積処理**: 前日在庫を引き継いで当日取引を反映

## 7. コメントと実装の不整合

### 発見された矛盾点

#### 矛盾1: 主キー項目数の不一致
```sql
-- コメント（誤り）
-- 複合キー（5項目）

-- 実際の定義（正しい）
CONSTRAINT PK_InventoryMaster PRIMARY KEY (
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, JobDate
)  -- 実際は6項目
```

#### 矛盾2: 設計理解の混乱
- **初期の調査前提**: 「主キーにJobDateが含まれていない」（誤解）
- **実際の設計**: JobDateは主キーの重要な構成要素

## 8. 現在の実装の正当性

### ✅ 実装は設計に準拠している

#### 理由1: 主キー構成が正しい
- JobDateが主キーに含まれているため、日付別管理が正しい設計

#### 理由2: 累積管理の実装が適切
- 前日在庫引き継ぎ → 当日取引反映 → 当日在庫確定
- この流れは在庫管理システムとして理想的

#### 理由3: JobDateの使用方法が一貫している
- 伝票系: フィルタリング用
- 在庫マスタ: 履歴管理用
- 用途が明確に分離されている

## 9. 推奨される修正方針

### 短期的修正（ドキュメント修正）
```sql
-- 修正前（誤解を招く）
-- 複合キー（5項目）

-- 修正後（正確）
-- 複合キー（6項目：5項目キー + JobDate）
```

### 長期的修正（設計明確化）
1. **設計思想のドキュメント化**
   - 在庫マスタは「日付別履歴管理」であることを明記
   - JobDateの役割（履歴管理のキー）を説明

2. **処理フローの明確化**
   - 累積管理の仕組みを図解
   - 前日引き継ぎの必要性を説明

## 10. 影響範囲

### 修正が必要なドキュメント
1. **01_create_tables.sql** - コメント修正
   ```sql
   -- 修正前: -- 複合キー（5項目）
   -- 修正後: -- 複合キー（6項目：5項目キー + JobDate）
   ```

2. **設計書・仕様書** - 在庫マスタの管理方式を明記

### 動作に影響のある機能
- **なし** - 実装は既に正しい設計に基づいている

## 11. 正しい在庫マスタ更新フロー

### 現在のフロー（正しい実装）
```
1. 前日在庫の引き継ぎ
   ├── 前日のInventoryMasterから CurrentStock を取得
   └── 当日JobDateで新規レコード作成

2. 当日取引の集計
   ├── 売上伝票（在庫減少）
   ├── 仕入伝票（在庫増加）
   └── 在庫調整（調整）

3. 在庫マスタの更新
   ├── 前日在庫 + 当日取引 = 当日在庫
   └── CurrentStock, CurrentStockAmount の更新
```

### 設計思想に基づく理想的なフロー
**現在の実装が既に理想的**

## 12. 結論

### 調査結果の要約

#### 重要な発見
1. **主キーにJobDateが含まれている**
   - 初期の調査前提が誤っていた
   - 在庫マスタは日付別履歴管理が正しい設計

2. **実装は設計に完全に準拠している**
   - InheritPreviousDayInventoryAsyncメソッドは必要な処理
   - 累積管理の実装は適切

3. **コメントと実装に軽微な不整合**
   - 「複合キー（5項目）」→「複合キー（6項目）」に修正必要

#### 推奨事項

1. **短期**: ドキュメントコメントの修正
   ```sql
   -- 複合キー（6項目：5項目キー + JobDate）
   ```

2. **中期**: 設計思想の明文化
   - 在庫マスタ = 日付別履歴管理
   - JobDate = 履歴管理のキー

3. **長期**: なし（実装は既に適切）

#### 最終判定
**現在の実装は設計思想に完全に準拠しており、修正の必要はない。ドキュメントの軽微な修正のみ推奨。**

---

**調査担当**: Claude Code  
**調査期間**: 2025-07-20  
**信頼度**: 高（実装とスキーマの詳細分析に基づく）