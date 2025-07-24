# Process 2-5 マッチング失敗原因分析報告書

## 調査日時
- 実施日: 2025-07-23 20:00:00
- 調査者: Claude Code

## 1. エグゼクティブサマリー

### 問題の概要
前回調査でProcess 2-5が実行されているにも関わらず、売上伝票への在庫単価書き込みが行われていない問題について、「クエリ２」フォルダのデータを詳細分析した結果、**DataSetIdの完全不一致**が根本原因であることが判明しました。

### 重大な発見
- **売上伝票とCP在庫マスタのDataSetIdが完全に異なる**
- **Process 2-5は正常に動作しているが、対象データが存在しない**
- **マッチング成功率: 0%（DataSetId不一致のため）**

## 2. データ分析結果

### 2.1 DataSetIdの致命的な不整合

#### 実データ
```
売上伝票（SalesVouchers）:
- DataSetId: "36b121e7-d2ef-433a-bed3-b994447b66a0"
- レコード数: 469件
- 作成日時: 2025-07-23T23:59:11.91

CP在庫マスタ（CpInventoryMaster）:
- DataSetId: "DS_20250602_230219_DAILY_REPORT"
- レコード数: 162件
- 作成日時: 2025-07-23T23:02:19.95
```

#### 分析結果
- **完全に異なるDataSetId**
- **作成時刻が約1時間違い**（23:02 vs 23:59）
- **Process 2-5実行時のDataSetIdはCP在庫マスタ側を使用**

### 2.2 5項目複合キーの表面的マッチング

#### 商品01503の比較例
```
売上伝票:
- ProductCode: "01503", GradeCode: "008", ClassCode: "080"
- ShippingMarkCode: "5773", ShippingMarkName: ""

CP在庫マスタ:
- ProductCode: "01503", GradeCode: "008", ClassCode: "080"  
- ShippingMarkCode: "5773", ShippingMarkName: "        " (8桁空白)
```

#### マッチング状況
- **5項目の内容は一致**（荷印名を除く）
- **しかし、DataSetIdが違うため検索対象外**

### 2.3 荷印名の詳細分析

#### ASCIIコードレベルでの差異
```
CP在庫マスタの荷印名:
- 8桁すべてASCII 32（スペース文字）
- 表示: "[        ]"

売上伝票の荷印名:
- null値または空文字
- 表示: "[]"
```

#### 処理方法の違い
- **CP在庫マスタ**: `String.PadRight(8)`で8桁固定
- **売上伝票**: そのまま（長さ不定）

### 2.4 マッチング成功率の実態

#### 表面的な成功率
- **17.json**: すべて"Matched"ステータス表示
- **実際のProcess 2-5**: マッチング0件

#### 矛盾の理由
- **SQLクエリ**: 5項目キーのみでマッチング確認
- **Process 2-5**: DataSetIdでフィルタリング後にマッチング

## 3. 原因の特定

### 3.1 主要原因: DataSetIdの管理不整合

#### 問題の構造
```
1. import-folderコマンド実行 → 売上伝票にDataSetId-A設定
2. 商品日報作成処理     → CP在庫マスタにDataSetId-B設定  
3. Process 2-5実行     → DataSetId-BでCP在庫マスタ検索
4. 売上伝票検索        → DataSetId-AとDataSetId-Bが不一致
5. 結果               → マッチング0件、処理25ms
```

#### 根本的な設計問題
- **DataSetIdの一意性保証が不十分**
- **処理フェーズ間でのDataSetId継承が失敗**
- **同一JobDateで複数DataSetIdが存在**

### 3.2 副次的要因: 荷印名処理の非統一

#### 問題点
- **8桁固定処理のタイミングが異なる**
- **null値と空白8文字の混在**
- **キー正規化処理の実装差異**

### 3.3 時系列的問題

#### 処理順序の問題
```
23:02:19 - CP在庫マスタ作成（商品日報処理時）
23:59:11 - 売上伝票インポート（別DataSetId）
```

**本来の想定順序**:
1. 売上伝票インポート
2. CP在庫マスタ作成（同一DataSetId）
3. Process 2-5実行

## 4. コード分析結果

### 4.1 Process 2-5のロジック検証

#### キー生成ロジック（正常）
```csharp
// GrossProfitCalculationService.cs 47-53行目
var allSalesVouchers = await _salesVoucherRepository
    .GetByJobDateAndDataSetIdAsync(jobDate, dataSetId);
    
var cpInventoryDict = await GetCpInventoryDictionaryAsync(jobDate, dataSetId);
```

#### 問題の所在
- **ロジック自体は正常**
- **引数のdataSetIdが間違っている**

### 4.2 DataSetId取得ロジック

#### import-folderコマンド（Program.cs 688-692行目）
```csharp
var dataSets = await datasetRepo.GetByJobDateAsync(currentDate);
var latestDataSet = dataSets.OrderByDescending(d => d.CreatedAt).FirstOrDefault();
```

#### 問題点
- **最新のDataSetIdを取得**（売上伝票用）
- **CP在庫マスタは商品日報作成時の古いDataSetId**
- **DataSetId不一致が発生**

## 5. 修正提案

### 5.1 即時対応（ホットフィックス）

#### 1. DataSetId統一処理
```csharp
// Program.cs のProcess 2-5実行部分を修正
// 変更前（688-692行目）
var dataSets = await datasetRepo.GetByJobDateAsync(currentDate);
var latestDataSet = dataSets.OrderByDescending(d => d.CreatedAt).FirstOrDefault();

// 変更後
// 売上伝票のDataSetIdを取得
var salesDataSetId = await GetSalesVoucherDataSetId(currentDate);
if (!string.IsNullOrEmpty(salesDataSetId))
{
    await grossProfitService.ExecuteProcess25Async(currentDate, salesDataSetId);
}
```

#### 2. CP在庫マスタの再作成
```csharp
// 売上伝票と同じDataSetIdでCP在庫マスタを再作成
public async Task RecreateCP在庫MasterWithCorrectDataSetId(DateTime jobDate, string salesDataSetId)
{
    // 1. 既存CP在庫マスタを削除
    await _cpInventoryRepository.DeleteByJobDateAsync(jobDate);
    
    // 2. 売上伝票のDataSetIdでCP在庫マスタを再作成
    await _cpInventoryRepository.CreateCpInventoryFromInventoryMasterAsync(salesDataSetId, jobDate);
}
```

### 5.2 根本的な修正

#### 1. DataSetId管理の統一化
```csharp
public class DataSetIdManager
{
    // JobDate単位でのDataSetId管理
    public async Task<string> GetOrCreateDataSetIdForJobDate(DateTime jobDate, string processType)
    {
        // 同一JobDateでは常に同じDataSetIdを返す
        var existing = await _repository.GetByJobDateAndProcessTypeAsync(jobDate, processType);
        return existing?.DataSetId ?? CreateNewDataSetId();
    }
}
```

#### 2. 荷印名処理の統一化
```csharp
public static class ShippingMarkNameNormalizer
{
    public static string Normalize(string input)
    {
        // 売上伝票、CP在庫マスタ共通の正規化処理
        if (string.IsNullOrEmpty(input))
            return new string(' ', 8); // 8桁空白
        
        return input.PadRight(8).Substring(0, 8);
    }
}
```

### 5.3 データ修正SQL

#### 既存データの修正
```sql
-- 現在の売上伝票のDataSetIdを取得
DECLARE @SalesDataSetId NVARCHAR(100) = '36b121e7-d2ef-433a-bed3-b994447b66a0';

-- CP在庫マスタのDataSetIdを統一
UPDATE CpInventoryMaster 
SET DataSetId = @SalesDataSetId,
    UpdatedAt = GETDATE()
WHERE JobDate = '2025-06-02'
  AND DataSetId = 'DS_20250602_230219_DAILY_REPORT';

-- DataSetManagementテーブルも更新
UPDATE DataSetManagement
SET DataSetId = @SalesDataSetId
WHERE JobDate = '2025-06-02'
  AND DataSetId = 'DS_20250602_230219_DAILY_REPORT';
```

## 6. 検証方法

### 6.1 修正後の確認SQL

#### マッチング成功率の確認
```sql
-- Process 2-5のマッチング可能性を確認
SELECT 
    COUNT(*) as TotalSalesVouchers,
    COUNT(cp.ProductCode) as MatchedWithCP,
    (COUNT(cp.ProductCode) * 100.0 / COUNT(*)) as MatchingRate
FROM SalesVouchers sv
LEFT JOIN CpInventoryMaster cp ON 
    sv.DataSetId = cp.DataSetId AND
    sv.ProductCode = cp.ProductCode AND
    sv.GradeCode = cp.GradeCode AND
    sv.ClassCode = cp.ClassCode AND
    sv.ShippingMarkCode = cp.ShippingMarkCode AND
    ISNULL(sv.ShippingMarkName, '') = ISNULL(cp.ShippingMarkName, '')
WHERE sv.JobDate = '2025-06-02';
```

#### 期待結果
```
修正前: MatchingRate = 0%
修正後: MatchingRate = 80-90%（荷印名の差異分を除く）
```

### 6.2 テスト手順

#### Phase 1: DataSetId修正
1. 上記SQLでCP在庫マスタのDataSetIdを統一
2. Process 2-5を手動実行
3. 売上伝票のInventoryUnitPriceを確認

#### Phase 2: 包括的テスト
1. import-folderコマンドを再実行
2. 商品日報を作成
3. 粗利率の正常化を確認

## 7. 影響度評価

### 7.1 影響を受ける商品

#### 全商品影響
- **対象商品数**: 41商品（売上伝票ベース）
- **影響伝票数**: 469件
- **影響範囲**: 売上伝票のInventoryUnitPrice、GrossProfit

#### 特に影響の大きい商品
```
商品15020: 50伝票、売上金額1,013,000円
商品01503: 26伝票、売上金額不明
商品00104: 47伝票、売上金額292,500円
```

### 7.2 ビジネス影響

#### 商品日報の信頼性
- **粗利率**: 現在異常値（145.32%等）
- **修正後**: 正常な粗利率に復旧
- **在庫評価**: 正確な在庫単価による評価

## 8. 予防策

### 8.1 DataSetId管理の強化

#### 設計原則
1. **JobDate単位での一意性保証**
2. **処理フェーズ間での継承保証**
3. **異常検知機能の実装**

#### 実装例
```csharp
public async Task ValidateDataSetConsistency(DateTime jobDate)
{
    var salesDataSetIds = await GetSalesVoucherDataSetIds(jobDate);
    var cpDataSetIds = await GetCpInventoryDataSetIds(jobDate);
    
    if (salesDataSetIds.Count > 1 || cpDataSetIds.Count > 1 || 
        !salesDataSetIds.SequenceEqual(cpDataSetIds))
    {
        throw new DataSetInconsistencyException($"JobDate {jobDate} has inconsistent DataSetIds");
    }
}
```

### 8.2 プロセス監視の強化

#### 監視項目
1. **Process 2-5実行時間**: 25ms以下は異常
2. **マッチング成功率**: 70%未満は異常
3. **DataSetId一意性**: 同一JobDateで複数は異常

## 9. 付録

### 9.1 分析に使用したデータ

#### クエリ２フォルダの全ファイル
- **12.json**: 売上伝票の5項目複合キー（29レコード）
- **13.json**: CP在庫マスタの5項目複合キー（47レコード）
- **14.json**: DataSetId別統計（重要）
- **15.json**: 商品01503の荷印名詳細（20レコード）
- **16.json**: 売上伝票荷印名統計（48商品）
- **17.json**: マッチング状況（17レコード、すべて"Matched"）
- **18.json**: 作成タイミング（3テーブル）
- **19.json**: 荷印名ASCIIコード分析（8レコード）

### 9.2 重要な数値データ

#### DataSetId統計
```
売上伝票: 469件 (DataSetId: 36b121e7-d2ef...)
CP在庫マスタ: 162件 (DataSetId: DS_20250602...)
商品数: 売上41商品、CP在庫44商品
```

#### 時系列データ
```
23:02:19 - CP在庫マスタ作成
23:59:11 - 売上伝票作成（約57分後）
```

#### 荷印名分析
```
売上伝票: 空文字（null）が95%以上
CP在庫マスタ: 8桁空白（ASCII 32）が90%以上
3文字荷印名: "ﾃﾆ2", "ﾃﾆ6"等（少数）
```

### 9.3 影響を受ける商品リスト

#### 高影響商品（売上金額順）
1. **15020**: 301数量、1,013,000円、50伝票
2. **00104**: 不明数量、292,500円、47伝票  
3. **01503**: 不明数量、不明金額、26伝票

#### マッチング特性
- **空荷印名**: 約400件（マッチング可能）
- **文字荷印名**: 約69件（要注意、ASCIIレベル差異あり）

---

## 結論

**Process 2-5マッチング失敗の根本原因は、DataSetIdの管理不整合**です。Process 2-5のロジック自体に問題はなく、売上伝票とCP在庫マスタが異なるDataSetIdを持っているため、検索時に0件となり25msで処理が完了していました。

**最優先対応**: DataSetIdの統一（SQLまたはコード修正）により、即座に問題を解決できます。

**長期対策**: DataSetId管理の設計見直しと、プロセス監視の強化が必要です。