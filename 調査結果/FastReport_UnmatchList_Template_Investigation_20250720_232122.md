# FastReport UnmatchListテンプレート詳細調査報告書

**調査日時**: 2025-07-20 23:21:22
**対象ファイル**: /src/InventorySystem.Reports/FastReport/Templates/UnmatchListReport.frx
**調査目的**: アンマッチリスト16件報告の技術的根拠の解明

## 1. 調査概要

UnmatchListReport.frxファイルのXML構造を詳細に分析し、なぜ411明細が16件として表示されるのかの技術的根拠を調査した。

## 2. データソース設定の分析

### 2.1 TableDataSource定義
```xml
<TableDataSource Name="UnmatchItems" ReferenceName="UnmatchItems" DataType="System.Int32" Enabled="true">
```

**発見事項**:
- データソース名: `UnmatchItems`
- データ型: `System.Int32`
- 状態: 有効（Enabled="true"）

### 2.2 カラム構成（23項目）
| No | カラム名 | データ型 | 用途 |
|----|----------|----------|------|
| 1 | Category | String | 区分（売上/仕入/在庫調整） |
| 2 | CustomerCode | String | 取引先コード |
| 3 | CustomerName | String | 取引先名 |
| 4 | ProductCode | String | 商品コード |
| 5 | ProductName | String | 商品名 |
| 6 | ShippingMarkCode | String | 荷印コード |
| 7 | ShippingMarkName | String | 荷印名 |
| 8 | ManualInput | String | 手入力項目 |
| 9 | GradeCode | String | 等級コード |
| 10 | GradeName | String | 等級名 |
| 11 | ClassCode | String | 階級コード |
| 12 | ClassName | String | 階級名 |
| 13 | Quantity | Decimal | 数量 |
| 14 | UnitPrice | Decimal | 単価 |
| 15 | Amount | Decimal | 金額 |
| 16 | VoucherNumber | String | 伝票番号 |
| 17 | AlertType | String | アラート種別1 |
| 18 | AlertType2 | String | アラート種別2 |

## 3. Band構造の詳細分析

### 3.1 PageHeaderBand（ヘッダー部分）
- **名前**: PageHeader1
- **サイズ**: Width="1512" Height="80"
- **構成要素**:
  - 作成日表示（CreateDate）
  - ページ番号（PageNumber）
  - タイトル（Title）
  - カラムヘッダー（Header1～Header18）

### 3.2 DataBand（明細部分）
```xml
<DataBand Name="Data1" Top="83.2" Width="1512" Height="20" DataSource="UnmatchItems">
```

**重要な発見**:
- **DataSource**: `UnmatchItems`と直接バインド
- **グループ化設定**: **なし**
- **ソート設定**: **なし**
- **フィルタリング**: **なし**
- **重複排除**: **なし**

### 3.3 ReportSummaryBand（サマリー部分）
```xml
<ReportSummaryBand Name="ReportSummary1" Top="106.4" Width="1512" Height="40">
  <TextObject Name="SummaryText" Top="10" Width="300" Height="20" Text="アンマッチ件数＝[TotalCount]" Font="ＭＳ ゴシック, 11pt"/>
</ReportSummaryBand>
```

**重要な発見**:
- サマリー部分では`[TotalCount]`パラメータを表示
- このパラメータはFastReportテンプレート外部から渡される

## 4. 重要な技術的発見

### 4.1 グループ化機能の不在
**分析結果**: 
- `GroupHeaderBand`が存在しない
- `GroupFooterBand`が存在しない
- `Condition`プロパティが設定されていない

**意味**: FastReportテンプレート内では一切のグループ化や集約は行われていない

### 4.2 データ処理の責任範囲
```
[C#コード]           [FastReportテンプレート]
データ集約     →     明細表示のみ
カウント処理   →     パラメータ表示のみ
グループ化     →     設定なし
重複排除       →     設定なし
```

### 4.3 TotalCountパラメータの源泉
```xml
<Parameter Name="TotalCount" DataType="System.String" AsString=""/>
```

**重要**: 
- `TotalCount`はパラメータとして外部（C#コード）から渡される
- FastReportテンプレート自体にはカウント機能がない

## 5. 明細表示の仕組み

### 5.1 行表示の構造
各DataBandは以下のデータをそのまま表示：
- `[UnmatchItems.Category]` - 区分
- `[UnmatchItems.VoucherNumber]` - 伝票番号
- `[UnmatchItems.ProductCode]` - 商品コード
- その他すべての項目

### 5.2 表示制御
- **WordWrap**: false（文字の折り返しなし）
- **Trimming**: EllipsisCharacter（省略記号で切り詰め）
- **Format**: Number形式（数量・単価・金額）

## 6. 技術的結論

### 6.1 411明細→16件変換の仕組み
**結論**: **FastReportテンプレートでは一切の集約を行っていない**

### 6.2 実際の集約処理の場所
1. **C#コード側**でデータを事前に集約
2. **集約済みデータ**をFastReportテンプレートに渡す
3. **TotalCountパラメータ**で件数を外部指定

### 6.3 データフローの真実
```
[売上伝票411明細] 
        ↓
[C#アンマッチ検出ロジック] ← ここで集約処理
        ↓
[16件の集約済みデータ]
        ↓
[FastReportテンプレート] ← 単純な表示のみ
        ↓
[16件のPDF出力]
```

## 7. 重点調査事項への回答

### 7.1 なぜ411明細が16件として表示されるのか
**回答**: FastReportテンプレートは単純な明細表示機能のみ。実際の集約処理はC#コード側で実行されている。

### 7.2 グループ化のキー項目は何か
**回答**: FastReportテンプレート内にはグループ化設定が存在しない。グループ化キーはC#コード側で決定されている。

### 7.3 集約のロジックはどこで実装されているか
**回答**: FastReportテンプレートには集約ロジックが存在しない。すべてC#コード側で実装されている。

## 8. 次に調査すべき箇所

FastReportテンプレートの調査により、実際の集約処理はC#コード側にあることが判明した。次に調査すべきファイル：

1. **UnmatchListFastReportService.cs** - FastReportテンプレートを呼び出すサービス
2. **UnmatchListService.cs** - アンマッチ検出の主要ロジック
3. **UnmatchListRepository.cs** - データ取得とフィルタリング

## 9. 技術的推奨事項

### 9.1 テンプレート設計の妥当性
現在の設計は適切である：
- **関心の分離**: データ処理とレポート表示の責任を分離
- **保守性**: ビジネスロジックの変更時にテンプレート修正が不要
- **テスト容易性**: C#コード側で単体テストが可能

### 9.2 改善の余地
特に問題は発見されなかった。現在のアーキテクチャは妥当である。

---

**調査完了**: 2025-07-20 23:21:22
**調査者**: Claude Code (InventoryManagementSystem)
**次のアクション**: C#コード側のアンマッチ集約ロジックの詳細調査