# 販売大臣AX 日付管理とマスタ構造

## 1. 販売大臣AX画面構成

### 1.1 仕入先マスタ画面

#### タブ構成
- **仕入先情報**: 基本情報入力
- **仕入先分類**: 分類設定
- **仕入業者（日約定等）**: 約定条件設定
- **住所日付（請求先YYYYMMDD）**: 住所・日付管理
- **システムデート**: 2025年4月3日
- **ジョブデート**: 2025年4月3日

### 1.2 仕入先一覧の表示項目

| 項目 | 説明 | 例 |
|------|------|-----|
| 仕入先コード | 5桁の識別コード | 01489, 10020等 |
| 仕入先名 | 仕入先の名称 | 下連雀、森島等 |
| 地区 | 地区コード | 023, 012等 |
| 種類 | 取引種類 | 006, 076等 |
| 数量 | 取引数量 | 10,000等 |
| 金額 | 取引金額（税抜） | 690,400等 |

### 1.3 売上伝票入力画面

#### 入力項目
- **伝票区分**: 51（掛売上）選択
- **伝票番号**: 00006
- **得意先名**: （検索可能）
- **商品情報入力エリア**: 商品コード、数量、単価等

## 2. 重要：日付管理システム

### 2.1 ジョブデート（汎用日付2）の重要性

#### なぜジョブデートを使用するのか
1. **伝票日付の問題点**
   - 翌日日付（計上が翌日で納品は本日）の場合がある
   - 昨日の日付の漏れた分を本日計上する場合がある
   - 伝票日付にはバラツキがあり、基準日として信頼できない

2. **ジョブデートの利点**
   - コンピュータの入力操作を行った実際の日付
   - 在庫管理の基準日として確実に使用可能
   - 日付範囲の絞り込みが正確にできる

### 2.2 ジョブデートの使用例

#### 年末年始の処理例
```
期間: 12/29～1/4を1日として取り扱う場合

【誤った方法】
伝票日付で範囲指定 → バラツキがあり不正確

【正しい方法】
ジョブデートで範囲指定
例: 20241229 から 20250104
```

### 2.3 販売大臣の項目位置

| 項目番号 | 項目名 | データ型 | 桁数 | 備考 |
|----------|--------|----------|------|------|
| 39 | 社店コード | ⑫ | 6 | +5 |
| 40 | 分類コード | ⑫ | 4 | +5 |
| 41 | 取引先コード | ⑫ | 6 | +5 |
| 42 | 伝票区分 | ⑫ | 2 | +5 |
| 43-47 | 汎用数値1-5（伝票） | - | 15.4 | - |
| 48 | 汎用日付1（伝票） | - | - | - |
| **49** | **汎用日付2（伝票）** | - | - | **ジョブデート** |
| 50-52 | 汎用日付3-5（伝票） | - | - | 未設定可 |
| 53-57 | 汎用項番1-5（伝票） | - | 9999 | - |
| 58 | 付箋（伝票） | ⑤ | - | ひなし/1～12各台×7 |
| 59 | 付箋過去（伝票） | - | 50 | ×8 |
| 60 | 発行区分 | ⑤ | - | 0未発行/1発行済9・25 |
| 61 | 送達区分 | ⑤ | - | 0通常/1返還 |
| 62 | 適格請求書計算 | ⑫ | - | 0しない/1する |

## 3. CSV出力時の注意事項

### 3.1 日付範囲の設定
- **必ず汎用日付2（ジョブデート）で範囲指定**
- 伝票日付は使用しない
- 複数日をまとめて処理する場合も、ジョブデートで管理

### 3.2 データ抽出の流れ
1. 販売大臣AXでジョブデート範囲を指定
2. CSVファイルを出力（D:\Share\AddonData）
3. 在庫管理システムで取り込み
4. ジョブデートを基準に処理

### 3.3 各伝票共通
- **売上伝票**: 汎用日付2がジョブデート
- **仕入伝票**: 汎用日付2がジョブデート
- **在庫調整（受注伝票）**: 汎用日付2がジョブデート

## 4. 実装時の重要ポイント

### 4.1 日付フィールドの扱い
```csharp
// 正しい実装例
public class VoucherBase
{
    public DateTime VoucherDate { get; set; }    // 伝票日付（参考値）
    public DateTime JobDate { get; set; }         // 汎用日付2（処理基準日）
    
    // 処理はJobDateを使用
    public bool IsInDateRange(DateTime startDate, DateTime endDate)
    {
        return JobDate >= startDate && JobDate <= endDate;
    }
}
```

### 4.2 CSV取込時の注意
- CSVヘッダーの「汎用日付2」列を確実に読み込む
- 日付形式の変換処理を実装（YYYYMMDD → DateTime）
- ジョブデートがない伝票はエラーとして扱う

### 4.3 年末年始等の特殊処理
```csharp
// 複数日を1日として扱う処理例
public class SpecialDateHandler
{
    public static bool IsYearEndPeriod(DateTime jobDate)
    {
        // 12/29～1/4を1つの処理日として扱う
        return (jobDate.Month == 12 && jobDate.Day >= 29) ||
               (jobDate.Month == 1 && jobDate.Day <= 4);
    }
}
```

## 5. まとめ

**最重要事項**：
- 在庫管理システムでは**汎用日付2（ジョブデート）**を必ず使用
- 伝票日付は信頼できないため、基準日として使用しない
- すべての処理（集計、アンマッチチェック、帳票作成）でジョブデートを基準とする