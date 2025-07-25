# アンマッチチェック荷印問題 調査結果

## 調査概要
- 調査日時: 2025-07-25 15:35
- 調査者: Claude Code
- 対象システム: InventoryManagementSystem
- 調査対象: アンマッチチェック機能の荷印名表示問題

## 1. 現状分析

### 1.1 荷印マスタの実装状況

**✅ 実装済み**
- `ShippingMarkMasterImportService.cs` - 荷印マスタCSV取込サービス
- `ShippingMarkMasterRepository.cs` - 荷印マスタリポジトリ
- `ShippingMarkMaster.cs` - エンティティクラス
- テーブル構造: 完全実装（15項目の汎用フィールド付き）

**❌ 問題点**
- CSV調査結果：荷印マスタ件数が1件のみ（`クエリ２/4.csv`）
- 本来必要な荷印コード（7011, 8001, 8124等）がマスタに未登録
- import-folderコマンドで荷印マスタが「未実装」として警告スキップされている

### 1.2 CP在庫マスタでの荷印処理

**✅ データ取得は正常**
- ストアドプロシージャ：`sp_CreateCpInventoryFromInventoryMasterCumulative.sql`
- 売上伝票から5項目キー（荷印名含む）で正しく抽出
- 荷印名は8桁固定長で正規化処理済み（`InventoryKey.cs:50-60`）

**⚠️ データ内容の問題**
- CSV分析結果：CP在庫マスタの荷印データが空（`クエリ２/5.csv`）
- 原因：CP在庫マスタ作成時に該当データが存在しないか、処理対象外になっている

### 1.3 アンマッチチェックでの荷印処理

**✅ ロジック実装は完全**
- `UnmatchListService.cs` - 5項目キーでの完全一致判定
- `UnmatchItem.cs` - 荷印名プロパティの適切な設定
- `InventoryKey.cs` - 8桁固定長正規化処理

**❌ データマッチングの失敗**
- CSV分析結果：アンマッチリスト詳細で `CP_ShippingMarkName` がすべてNULL（`クエリ２/8.csv`）
- 売上伝票には荷印名データが存在：`"ﾃﾆ2     "`, `"ﾃﾆ1     "`等（`クエリ２/10.csv`）
- 在庫マスタ側の荷印名フォーマットと不一致の可能性

### 1.4 PDF出力での荷印表示

**✅ PDF出力処理は完全実装**
- `UnmatchListFastReportService.cs:146,220` - ShippingMarkNameカラム設定
- DataTable作成時に荷印名を適切に設定
- ゼロコード判定処理も実装済み（`IsZeroCode`による空白表示）

**❌ 表示されない原因**
- 入力データ（UnmatchItem）の時点で荷印名が空白
- PDF処理ではなく、データ作成段階での問題

## 2. 問題の根本原因

### 2.1 荷印名が表示されない原因

**主要原因：荷印名フォーマット不一致**

1. **売上伝票の荷印名**: `"ﾃﾆ2     "` (8桁、後ろ空白埋め)
2. **在庫マスタの荷印名**: `"        "` (8桁空白) または異なるフォーマット
3. **マッチング失敗**: 5項目キー完全一致で荷印名も含むため、わずかなフォーマット差でマッチ失敗

**詳細分析（クエリ２データより）**
- 荷印コード「7011」で荷印名パターンが2種類存在（`クエリ２/7.csv`）
- 同一コードで `"ﾃﾆ2"` と空白パターンが混在
- 在庫マスタと売上伝票での荷印名不一致が多数発生

### 2.2 荷印マッチングが失敗する原因

**構造的問題**
1. **荷印マスタの未整備**: 必要な荷印コードがマスタに未登録
2. **フォーマット差異**: 在庫データと伝票データの荷印名形式不統一
3. **文字エンコーディング**: 半角カナの取り扱い差異の可能性

## 3. 関連ソースコード

### 3.1 問題のあるコード箇所

**5項目キー完全一致判定**
```csharp
// UnmatchListService.cs:112-116
// 荷印名も完全一致が必要（問題の原因）
AND sv.ShippingMarkName = im.ShippingMarkName
```

**荷印名正規化処理**
```csharp
// InventoryKey.cs:50-60
public static string NormalizeShippingMarkName(string? value)
{
    if (string.IsNullOrEmpty(value))
        return new string(' ', 8);  // 空の場合8桁空白
    
    var trimmed = value.TrimEnd();  // 右側空白削除
    return trimmed.Length >= 8 
        ? trimmed.Substring(0, 8) 
        : trimmed.PadRight(8, ' ');  // 8桁右空白埋め
}
```

### 3.2 データフロー図

```
売上伝票CSV → SalesVoucher → UnmatchItem
   ↓              ↓             ↓
"ﾃﾆ2     "    ShippingMarkName  Key.ShippingMarkName
   
在庫マスタ → CpInventoryMaster → マッチング判定
   ↓              ↓             ↓
"        "    ShippingMarkName  ❌ 不一致
```

## 4. 推奨される修正方針

### 4.1 短期的修正（即座対応可能）

**Priority 1: 荷印マスタの整備**
```bash
# 荷印汎用マスター３.csvの取込を有効化
# import-folderコマンドの警告スキップを修正
```

**Priority 2: マッチングロジックの改善**
```csharp
// 荷印名の部分一致または正規化後一致に変更
WHERE sv.ShippingMarkCode = im.ShippingMarkCode
AND (sv.ShippingMarkName = im.ShippingMarkName 
     OR TRIM(sv.ShippingMarkName) = TRIM(im.ShippingMarkName))
```

**Priority 3: デバッグ出力の強化**
```csharp
// マッチング失敗時の詳細ログ出力
_logger.LogDebug("マッチング失敗: 売上荷印名='{SalesShippingMarkName}', 在庫荷印名='{InventoryShippingMarkName}'", 
    salesShippingMarkName, inventoryShippingMarkName);
```

### 4.2 長期的改善

**Architecture 1: 荷印名の統一管理**
- 荷印マスタを基準とした荷印名の正規化
- CSV取込時の荷印名フォーマット統一処理

**Architecture 2: マッチングアルゴリズムの改善**
- 4項目キー + 荷印名パターンマッチング
- 荷印名の類似度判定機能

**Architecture 3: データ整合性チェック**
- 荷印データの定期的な整合性検証
- 不一致データの自動修復機能

## 5. 実装優先度とスケジュール

### Phase 1: 緊急対応（1-2日）
1. 荷印マスタCSV取込の有効化
2. import-folderコマンドでの荷印マスタ処理実装
3. マッチングロジックのデバッグ出力強化

### Phase 2: 根本修正（3-5日）
1. 荷印名マッチングロジックの改善
2. データ正規化処理の統一
3. 統合テストによる検証

### Phase 3: 恒久対策（1週間）
1. 荷印データ整合性チェック機能
2. 自動修復機能の実装
3. 運用手順書の整備

## 6. 追加調査が必要な項目

### 6.1 データ整合性
- [ ] 在庫マスタの荷印名フォーマット詳細確認
- [ ] 荷印マスタの完全なデータ確認
- [ ] CSV取込時の文字エンコーディング確認

### 6.2 システム動作
- [ ] CP在庫マスタ作成処理の詳細ログ確認
- [ ] マッチング処理の実行時デバッグログ取得
- [ ] PDF出力時のDataTable内容確認

### 6.3 運用面
- [ ] 荷印コード運用ルールの確認
- [ ] マスタメンテナンス手順の確認
- [ ] エラー時の復旧手順策定

## 7. 結論

アンマッチチェック機能の荷印問題は、**荷印マスタの未整備と荷印名フォーマット不一致**が根本原因です。

システム実装自体は完全ですが、データレベルでの不整合により、5項目キー完全一致判定が失敗しています。

**最優先対応**：荷印マスタの整備とマッチングロジックの改善により、問題は解決可能です。

---

**調査完了**: 2025-07-25 15:35  
**次回アクション**: Phase 1実装の開始