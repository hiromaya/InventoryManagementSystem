# CSVマッピング最終仕様一覧（修正後）

**最終更新**: 2025-06-30  
**修正理由**: 実際のCSVファイルとの照合により全面的に修正  
**文字コード**: UTF-8

## 修正概要

実際のCSVファイルを目視確認した結果、以下の主要な修正を実施しました：

- 伝票番号、伝票日付、伝票区分の列順序を修正
- 得意先コード・仕入先コードの列位置を修正
- 5項目複合キー（商品・等級・階級・荷印コード、手入力項目）の列位置を修正
- 数量・単価・金額の列位置を修正

## 1. 売上伝票CSV（SalesVoucherDaijinCsv.cs）

**ファイル形式**: 171列フォーマット  
**伝票区分**: 51（掛売）、52（現売）

| 列番号 | Index | プロパティ名 | CSVヘッダー名 | データ型 | 修正内容 |
|--------|-------|-------------|--------------|---------|---------|
| 1 | 0 | VoucherDate | 伝票日付(西暦4桁YYYYMMDD) | string | 2→1列目 |
| 2 | 1 | VoucherType | 伝票区分(51:掛売,52:現売) | string | 3→2列目 |
| 3 | 2 | VoucherNumber | 伝票番号(自動採番) | string | 1→3列目 |
| 8 | 7 | CustomerCode | 得意先コード | string | 4→8列目 |
| 49 | 48 | JobDate | ジョブデート | string | 変更なし |
| **85** | **84** | **GradeCode** | **等級コード** | **string** | **88→85列目** |
| **86** | **85** | **ClassCode** | **階級コード** | **string** | **89→86列目** |
| **87** | **86** | **ShippingMarkCode** | **荷印コード** | **string** | **90→87列目** |
| **91** | **90** | **ProductCode** | **商品コード** | **string** | **94→91列目** |
| **96** | **95** | **Quantity** | **数量** | **decimal** | **99→96列目** |
| **98** | **97** | **UnitPrice** | **単価** | **decimal** | **101→98列目** |
| **99** | **98** | **Amount** | **金額** | **decimal** | **102→99列目** |
| **155** | **154** | **HandInputItem** | **手入力項目(半角8文字)** | **string** | **153→155列目** |

---

## 2. 仕入伝票CSV（PurchaseVoucherDaijinCsv.cs）

**ファイル形式**: 171列フォーマット  
**伝票区分**: 11（掛仕入）、12（現金仕入）

| 列番号 | Index | プロパティ名 | CSVヘッダー名 | データ型 | 修正内容 |
|--------|-------|-------------|--------------|---------|---------|
| 1 | 0 | VoucherDate | 伝票日付 | string | 2→1列目 |
| 2 | 1 | VoucherType | 伝票区分(11:掛仕入,12:現金仕入) | string | 3→2列目 |
| 3 | 2 | VoucherNumber | 伝票番号 | string | 1→3列目 |
| 7 | 6 | SupplierCode | 仕入先コード | string | 4→7列目 |
| 44 | 43 | JobDate | ジョブデート | string | 変更なし |
| **81** | **80** | **GradeCode** | **等級コード** | **string** | **82→81列目** |
| **82** | **81** | **ClassCode** | **階級コード** | **string** | **83→82列目** |
| **83** | **82** | **ShippingMarkCode** | **荷印コード** | **string** | **84→83列目** |
| **87** | **86** | **ProductCode** | **商品コード** | **string** | **88→87列目** |
| **92** | **91** | **Quantity** | **数量** | **decimal** | **93→92列目** |
| **94** | **93** | **UnitPrice** | **単価** | **decimal** | **95→94列目** |
| **95** | **94** | **Amount** | **金額** | **decimal** | **96→95列目** |
| 147 | 146 | HandInputItem | 荷印手入力 | string | 変更なし |

---

## 3. 在庫調整CSV（InventoryAdjustmentDaijinCsv.cs）

**ファイル形式**: 171列フォーマット（受注伝票形式）  
**伝票区分**: 71（在庫調整）固定

| 列番号 | Index | プロパティ名 | CSVヘッダー名 | データ型 | 修正内容 |
|--------|-------|-------------|--------------|---------|---------|
| 1 | 0 | VoucherDate | 伝票日付 | string | 変更なし |
| 2 | 1 | VoucherType | 伝票区分(71:在庫調整) | string | 変更なし |
| 3 | 2 | VoucherNumber | 伝票番号 | string | 変更なし |
| 48 | 47 | JobDate | ジョブデート | string | 変更なし |
| **96** | **95** | **CategoryCode** | **区分(1:ﾛｽ,4:振替,6:調整)** | **string** | **88→96列目** |
| **97** | **96** | **UnitPrice** | **単価** | **decimal** | **99→97列目** |
| **98** | **97** | **Amount** | **金額** | **decimal** | **99→98列目** |
| **153** | **152** | **HandInputItem** | **手入力項目(半角8文字)** | **string** | **157→153列目** |

---

## 4. 前月末在庫CSV（PreviousMonthInventoryCsv.cs）

**ファイル形式**: 161列フォーマット  
**伝票区分**: 71（在庫調整）

| 列番号 | Index | プロパティ名 | CSVヘッダー名 | データ型 | 修正内容 |
|--------|-------|-------------|--------------|---------|---------|
| 1 | 0 | VoucherDate | 伝票日付 | string | 変更なし |
| 2 | 1 | VoucherType | 伝票区分(71:在庫調整) | string | 変更なし |
| 48 | 47 | JobDate | ジョブデート | string | 変更なし |
| 82 | 81 | DetailType | 明細種(1固定) | string | 変更なし |
| 84 | 83 | GradeCode | 等級コード | string | 変更なし |
| 85 | 84 | ClassCode | 階級コード | string | 変更なし |
| 86 | 85 | ShippingMarkCode | 荷印コード | string | 変更なし |
| **96** | **95** | **CategoryCode** | **区分(1:ﾛｽ,4:振替,6:調整)** | **string** | **88→96列目** |
| 90 | 89 | ProductCode | 商品コード | string | 変更なし |
| 95 | 94 | Quantity | 数量 | decimal | 変更なし |
| 97 | 96 | UnitPrice | 単価 | decimal | 変更なし |
| 98 | 97 | Amount | 金額 | decimal | 変更なし |
| 146 | 145 | ProductName | 商品名 | string | 148→146列目（既に修正済み） |
| 153 | 152 | HandInputItem | 手入力項目(半角8文字) | string | 変更なし |

---

## 修正の影響範囲

### 1. インポート処理への影響
- ✅ 既存のCSVファイルが正しく読み込まれるようになる
- ✅ 5項目複合キーが正しい列から取得される
- ✅ 数量・単価・金額が正しい列から取得される

### 2. データ整合性への影響
- ⚠️ 既にインポート済みのデータとの整合性確認が必要
- ⚠️ アンマッチリストの再処理が必要な可能性

### 3. 互換性への影響
- ✅ サービスインターフェースは変更なし
- ✅ データベース構造は変更なし
- ✅ エンティティクラスは変更なし

---

## 重要な実装ポイント（変更なし）

### 1. 5項目複合キー
すべての伝票データは以下の5項目で一意に識別されます：

| 項目 | 説明 | 桁数 | パディング |
|------|------|------|------------|
| ProductCode | 商品コード | 5桁 | 左0埋め |
| GradeCode | 等級コード | 3桁 | 左0埋め |
| ClassCode | 階級コード | 3桁 | 左0埋め |
| ShippingMarkCode | 荷印コード | 4桁 | 左0埋め |
| ShippingMarkName | 荷印名（手入力項目） | 8桁 | 右空白埋め |

### 2. 荷印名の取得処理（変更なし）
```csharp
// すべての伝票で共通の処理
ShippingMarkName = (HandInputItem ?? "").PadRight(8).Substring(0, 8);
```

### 3. 除外データ条件（変更なし）
- 商品コード「00000」の行は読み飛ばし
- 得意先コード「00000」の行は読み飛ばし（売上伝票）
- 仕入先コード「00000」の行は読み飛ばし（仕入伝票）

---

## 検証項目

修正後、以下を確認してください：

### 1. CSVインポートテスト
```bash
# 各伝票のテストインポート
dotnet run --project src/InventorySystem.Console -- import-file 売上伝票.csv 2025-06-30
dotnet run --project src/InventorySystem.Console -- import-file 仕入伝票.csv 2025-06-30
dotnet run --project src/InventorySystem.Console -- import-file 受注伝票.csv 2025-06-30
dotnet run --project src/InventorySystem.Console -- import-file 前月末在庫.csv 2025-06-30
```

### 2. データ整合性確認
```sql
-- 5項目キーが正しく構築されているか確認
SELECT TOP 10 
    VoucherId,
    ProductCode,
    GradeCode,
    ClassCode,
    ShippingMarkCode,
    ShippingMarkName
FROM SalesVouchers
ORDER BY CreatedAt DESC;
```

### 3. アンマッチリスト確認
```bash
# アンマッチリスト処理の実行
dotnet run --project src/InventorySystem.Console -- create-unmatch-list 2025-06-30
```

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2025-06-30 | 実際のCSVファイルとの照合により全面的な修正を実施 |
| 2025-06-30 | 売上伝票：伝票順序、5項目キー、数量・単価・金額の列位置を修正 |
| 2025-06-30 | 仕入伝票：同様の修正を実施 |
| 2025-06-30 | 在庫調整：区分コード、単価・金額、手入力項目の列位置を修正 |
| 2025-06-30 | 前月末在庫：区分コードの列位置を修正 |

---

**注意**: この修正により、実際のCSVファイル構造と完全に一致したマッピングになりました。既存のインポート済みデータについては、必要に応じて再インポートまたはデータ修正を検討してください。