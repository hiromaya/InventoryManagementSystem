# DataSetId重複問題 根本原因調査レポート

**調査実施日**: 2025年7月24日  
**調査者**: Claude Code  
**対象システム**: InventoryManagementSystem  

## 📋 調査概要

本調査は、システム内で発生しているDataSetId重複問題の根本原因を特定し、包括的な解決策を提示することを目的として実施されました。

### 調査対象
- **DataSetIdManager**の実装詳細
- **JobExecutionLog**テーブルの設計と制約
- **データセット無効化機能**の動作状況
- **インポートサービス**でのDataSetId処理フロー
- **現在のデータ状態**の分析

## 🔍 主要な発見事項

### 1. テーブル設計の根本的矛盾

#### JobExecutionLogテーブル（制約あり）
```sql
-- 同一JobDateとJobTypeの組み合わせは一意
CONSTRAINT UK_JobExecutionLog_JobDate_JobType UNIQUE (JobDate, JobType)
CONSTRAINT UK_JobExecutionLog_DataSetId UNIQUE (DataSetId)
```

**現在のデータ状況**:
- **JobDate: 2025-06-02** で **4レコード**のみ
- JobType: "SalesVoucher", "CpInventoryMaster", "PurchaseVoucher", "InventoryAdjustment"

#### DataSetManagementテーブル（制約なし）
```sql
-- 重複防止制約が存在しない
-- 同一JobDate+ProcessTypeで複数DataSetが作成可能
```

**現在のデータ状況**:
- **JobDate: 2025-06-02** で **51レコード**
- **12倍の重複**が発生中

### 2. DataSetIdManagerの設計上の問題

#### 抽象度のミスマッチ
- **JobExecutionLog**: 抽象的な「JobType」でチェック
- **DataSetManagement**: 具体的な「ProcessType」で管理

```csharp
// JobExecutionLogでの検索（抽象的）
JobType: "SalesVoucher" → DataSetIdは1つ

// DataSetManagementでの実際の作成（具体的）  
ProcessType: "PRODUCT", "CUSTOMER", "SUPPLIER", 
            "商品CATEGORY1/2/3", "得意先CATEGORY1-5", 
            "仕入先CATEGORY1-3" → DataSetIdは複数
```

#### GetOrCreateDataSetIdAsyncメソッドの限界
- JobExecutionLogでの既存ID検索は機能している
- しかし、DataSetManagementでの実際の作成は制御されていない
- **重複チェック機構が不完全**

### 3. データセット無効化機能の調査結果

#### 実装状況
✅ **Repository実装**: `DeactivateDataSetAsync()` メソッド存在  
✅ **Service実装**: ステータス更新時の自動フラグ制御あり  
✅ **カラム設計**: IsActive, DeactivatedAt, DeactivatedBy完備  

#### 実際の動作状況
❌ **マスタ系**: 重複データが全て有効状態（IsActive=1）で残存  
⚠️ **伝票系**: 最新以外は無効化されるが削除はされない  
❌ **一括無効化**: import-folder実行時の古いDataSet無効化処理が不十分  

### 4. 重複データの詳細分析

#### 時系列での重複発生パターン
```
11:56:43-44 (初回実行): 17レコード作成
13:44:35-36 (2回目実行): 17レコード作成（重複）
14:24:08-09 (3回目実行): 17レコード作成（重複）
計51レコード = 17 × 3回実行
```

#### ProcessType別の重複状況
| ProcessType | 作成回数 | IsActive=1 | IsActive=0 | 備考 |
|-------------|----------|------------|------------|------|
| PRODUCT | 3回 | 3件 | 0件 | **全て有効**（問題） |
| CUSTOMER | 3回 | 3件 | 0件 | **全て有効**（問題） |
| SUPPLIER | 3回 | 3件 | 0件 | **全て有効**（問題） |
| 商品CATEGORY1-3 | 各3回 | 各3件 | 各0件 | **全て有効**（問題） |
| 得意先CATEGORY1-5 | 各3回 | 各3件 | 各0件 | **全て有効**（問題） |
| 仕入先CATEGORY1-3 | 各3回 | 各3件 | 各0件 | **全て有効**（問題） |
| SALES | 3回 | 1件 | 2件 | 最新以外無効化済み |
| PURCHASE | 3回 | 1件 | 2件 | 最新以外無効化済み |
| ADJUSTMENT | 3回 | 1件 | 2件 | 最新以外無効化済み |

## 🚨 重大なリスク

### 1. データ肥大化
- 現在**51レコード**（本来4レコード想定）
- 継続的な重複により**指数関数的増加**の危険性

### 2. パフォーマンス劣化
- データセット検索処理の応答遅延
- 不要なディスク容量消費
- インデックス効率の低下

### 3. データ整合性リスク
- どのDataSetが「正」なのか特定困難
- 運用時の混乱と人的エラーのリスク
- バックアップ・復旧時の複雑性増大

## 💡 推奨解決策

### 🔥 緊急対応（即座に実施）

#### 1. DataSetManagementテーブルにUNIQUE制約追加
```sql
-- JobDate + ProcessType の組み合わせを一意にする
ALTER TABLE DataSetManagement 
ADD CONSTRAINT UK_DataSetManagement_JobDate_ProcessType 
UNIQUE (JobDate, ProcessType);
```

#### 2. 重複データのクリーンアップ
```sql
-- 同一JobDate+ProcessTypeで最新以外を無効化
UPDATE DataSetManagement 
SET IsActive = 0, 
    DeactivatedAt = GETDATE(),
    DeactivatedBy = 'SYSTEM_CLEANUP'
WHERE DataSetId NOT IN (
    SELECT MAX(DataSetId) 
    FROM DataSetManagement 
    GROUP BY JobDate, ProcessType
) AND IsActive = 1;
```

### 🔧 中期対応（設計改善）

#### 1. DataSetIdManagerのロジック修正
```csharp
// ProcessType単位での重複チェック強化
public async Task<string> GetOrCreateDataSetIdAsync(DateTime jobDate, string processType)
{
    // DataSetManagementテーブルで直接チェック
    var existing = await _dataSetManagementRepository
        .GetLatestByJobDateAndTypeAsync(jobDate, processType);
    
    if (existing != null && existing.IsActive)
    {
        return existing.DataSetId;
    }
    
    // 新規作成時は古いDataSetを無効化
    await DeactivateOldDataSetsAsync(jobDate, processType);
    return await CreateNewDataSetAsync(jobDate, processType);
}
```

#### 2. import-folderコマンドの改善
```csharp
// 実行前処理：既存DataSetの無効化
await DeactivateExistingDataSetsAsync(jobDate);

// 原子性保証：全成功 or 全失敗
using var transaction = await _connection.BeginTransactionAsync();
try 
{
    await ImportAllFilesAsync(transaction);
    await transaction.CommitAsync();
}
catch 
{
    await transaction.RollbackAsync();
    throw;
}
```

### 🏗️ 長期対応（アーキテクチャ改善）

#### 1. JobExecutionLogの廃止検討
- DataSetManagementテーブルでの一元管理
- 不要な二重管理システムの解消
- ProcessType粒度での管理統一

#### 2. 並行実行制御の実装
```csharp
// 分散ロック機能でimport処理の排他制御
public async Task<string> ExecuteImportWithLockAsync(DateTime jobDate)
{
    var lockKey = $"IMPORT_{jobDate:yyyyMMdd}";
    using var distributedLock = await _lockService.AcquireAsync(lockKey);
    
    // 排他制御された状態でimport実行
    return await ImportAsync(jobDate);
}
```

#### 3. データ ライフサイクル管理
- 定期的なアーカイブ処理（IsActive=0のデータ）
- 古いDataSetの自動削除ポリシー
- 監査ログでの操作履歴追跡

## 📊 実装優先度

### 🔴 **最高優先度**（即座に実施）
1. UNIQUE制約の追加
2. 既存重複データのクリーンアップ
3. 緊急対応用の手動無効化スクリプト作成

### 🟡 **高優先度**（1週間以内）
1. DataSetIdManagerのロジック修正
2. import-folderコマンドの改善
3. 単体テストの追加

### 🟢 **中優先度**（1ヶ月以内）
1. JobExecutionLog廃止の検討・実装
2. 並行実行制御の実装
3. データ ライフサイクル管理の導入

## 🧪 検証手順

### 1. 制約追加前の準備
```sql
-- 現在の重複状況確認
SELECT JobDate, ProcessType, COUNT(*) as 重複数
FROM DataSetManagement 
GROUP BY JobDate, ProcessType 
HAVING COUNT(*) > 1;
```

### 2. 制約追加後の検証
```sql
-- 制約が正常に動作することを確認
INSERT INTO DataSetManagement (JobDate, ProcessType, ...) 
VALUES ('2025-06-02', 'PRODUCT', ...);
-- → UNIQUE制約違反エラーが発生することを確認
```

### 3. アプリケーション動作テスト
```bash
# import-folderコマンドの重複実行テスト
dotnet run -- import-folder DeptA 2025-06-02
dotnet run -- import-folder DeptA 2025-06-02  # 2回目実行
# → エラーハンドリングが正常に動作することを確認
```

## 📝 まとめ

### 問題の本質
**テーブル設計の矛盾**と**重複防止機構の不備**により、DataSetManagementテーブルで大量の重複レコードが発生。現在51レコード（正常時4レコード想定）で**12倍の重複**が確認された。

### 緊急性
**高**: データ肥大化とパフォーマンス劣化が進行中。早急な対応が必要。

### 解決可能性
**高**: 根本原因が特定済みであり、技術的な解決策も明確。UNIQUE制約追加により根本解決可能。

### 予想される効果
- データ整合性の向上
- パフォーマンスの大幅改善  
- 運用負荷の軽減
- 将来的な拡張性の確保

---

**調査完了**: 2025年7月24日  
**次のアクション**: 緊急対応（UNIQUE制約追加）の実施を推奨