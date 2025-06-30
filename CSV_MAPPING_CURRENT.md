# CSVマッピング最新仕様一覧

**作成日**: 2025-06-30  
**用途**: 現在のプログラムに実装されているCSVマッピングの最新仕様  
**文字コード**: UTF-8

## 1. 売上伝票CSV（SalesVoucherDaijinCsv.cs）

**ファイル形式**: 171列フォーマット  
**伝票区分**: 51（掛売）、52（現売）

| 列番号 | Index | プロパティ名 | CSVヘッダー名 | データ型 | 重要度 | 備考 |
|--------|-------|-------------|--------------|---------|--------|------|
| 1 | 0 | VoucherNumber | 伝票番号(自動採番) | string | ★★★ | |
| 2 | 1 | VoucherDate | 伝票日付(西暦4桁YYYYMMDD) | string | ★★★ | |
| 3 | 2 | VoucherType | 伝票区分(51:掛売,52:現売) | string | ★★★ | |
| 4 | 3 | CustomerCode | 得意先コード | string | ★★★ | 00000は除外 |
| 8 | 7 | CustomerName | 得意先名１ | string | ★★ | |
| 48 | 47 | SystemDate | システムデート | string | ★★ | 汎用日付1 |
| 49 | 48 | JobDate | ジョブデート | string | ★★★ | 汎用日付2 |
| 79 | 78 | Level1LineNumber | １階層目行番号 | int? | ★ | |
| 80 | 79 | Level2LineNumber | ２階層目行番号 | int? | ★ | |
| 81 | 80 | Level3LineNumber | ３階層目行番号 | int? | ★ | |
| 82 | 81 | Level4LineNumber | ４階層目行番号 | int? | ★ | |
| 83 | 82 | Level5LineNumber | ５階層目行番号 | int? | ★★ | 通常使用 |
| 84 | 83 | DetailType | 明細種(1:売上,2:返品,4:値引) | string | ★★★ | |
| **88** | **87** | **GradeCode** | **等級コード** | **string** | **★★★** | **5項目キー** |
| **89** | **88** | **ClassCode** | **階級コード** | **string** | **★★★** | **5項目キー** |
| **90** | **89** | **ShippingMarkCode** | **荷印コード** | **string** | **★★★** | **5項目キー** |
| **94** | **93** | **ProductCode** | **商品コード** | **string** | **★★★** | **5項目キー・00000除外** |
| 99 | 98 | Quantity | 数量 | decimal | ★★★ | |
| 101 | 100 | UnitPrice | 単価 | decimal | ★★★ | |
| 102 | 101 | Amount | 金額 | decimal | ★★★ | |
| 145 | 144 | GradeName | 等級名 | string | - | 参照のみ |
| 146 | 145 | ClassName | 階級名 | string | - | 参照のみ |
| 147 | 146 | ShippingMarkName | 荷印名 | string | - | **使用しない** |
| 149 | 148 | ProductName | 商品名 | string | ★★ | |
| **153** | **152** | **HandInputItem** | **手入力項目(半角8文字)** | **string** | **★★★** | **荷印名として使用（5項目キー）** |

---

## 2. 仕入伝票CSV（PurchaseVoucherDaijinCsv.cs）

**ファイル形式**: 171列フォーマット  
**伝票区分**: 11（掛仕入）、12（現金仕入）

| 列番号 | Index | プロパティ名 | CSVヘッダー名 | データ型 | 重要度 | 備考 |
|--------|-------|-------------|--------------|---------|--------|------|
| 1 | 0 | VoucherNumber | 伝票番号 | string | ★★★ | |
| 2 | 1 | VoucherDate | 伝票日付 | string | ★★★ | |
| 3 | 2 | VoucherType | 伝票区分(11:掛仕入,12:現金仕入) | string | ★★★ | |
| 4 | 3 | SupplierCode | 仕入先コード | string | ★★★ | 00000は除外 |
| 8 | 7 | SupplierName | 仕入先名 | string | ★★ | |
| 43 | 42 | SystemDate | システムデート | string | ★★ | 汎用日付1 |
| 44 | 43 | JobDate | ジョブデート | string | ★★★ | 汎用日付2 |
| 75 | 74 | Level1LineNumber | １階層目行番号 | int? | ★ | |
| 76 | 75 | Level2LineNumber | ２階層目行番号 | int? | ★ | |
| 77 | 76 | Level3LineNumber | ３階層目行番号 | int? | ★ | |
| 78 | 77 | Level4LineNumber | ４階層目行番号 | int? | ★ | |
| 79 | 78 | Level5LineNumber | ５階層目行番号 | int? | ★★ | 通常使用 |
| 80 | 79 | DetailType | 明細種 | string | ★★★ | |
| **82** | **81** | **GradeCode** | **等級コード** | **string** | **★★★** | **5項目キー** |
| **83** | **82** | **ClassCode** | **階級コード** | **string** | **★★★** | **5項目キー** |
| **84** | **83** | **ShippingMarkCode** | **荷印コード** | **string** | **★★★** | **5項目キー** |
| **88** | **87** | **ProductCode** | **商品コード** | **string** | **★★★** | **5項目キー・00000除外** |
| 93 | 92 | Quantity | 数量 | decimal | ★★★ | |
| 95 | 94 | UnitPrice | 単価 | decimal | ★★★ | |
| 96 | 95 | Amount | 金額 | decimal | ★★★ | |
| 135 | 134 | GradeName | 等級名 | string | - | 参照のみ |
| 136 | 135 | ClassName | 階級名 | string | - | 参照のみ |
| 137 | 136 | ShippingMarkName | 荷印名 | string | - | **使用しない** |
| 141 | 140 | ProductName | 商品名 | string | ★★ | |
| **147** | **146** | **HandInputItem** | **荷印手入力** | **string** | **★★★** | **荷印名として使用（5項目キー）** |

---

## 3. 在庫調整CSV（InventoryAdjustmentDaijinCsv.cs）

**ファイル形式**: 171列フォーマット（受注伝票形式）  
**伝票区分**: 71（在庫調整）固定

| 列番号 | Index | プロパティ名 | CSVヘッダー名 | データ型 | 重要度 | 備考 |
|--------|-------|-------------|--------------|---------|--------|------|
| 1 | 0 | VoucherDate | 伝票日付 | string | ★★★ | |
| 2 | 1 | VoucherType | 伝票区分(71:在庫調整) | string | ★★★ | |
| 3 | 2 | VoucherNumber | 伝票番号 | string | ★★★ | |
| 7 | 6 | CustomerCode | 得意先コード | string | - | 在庫調整では未使用 |
| 8 | 7 | CustomerName | 得意先名１ | string | - | 在庫調整では未使用 |
| 47 | 46 | SystemDate | システムデート | string | ★★ | |
| 48 | 47 | JobDate | ジョブデート | string | ★★★ | |
| 76 | 75 | Level1LineNumber | １階層目行番号 | int? | ★ | |
| 77 | 76 | Level2LineNumber | ２階層目行番号 | int? | ★ | |
| 78 | 77 | Level3LineNumber | ３階層目行番号 | int? | ★ | |
| 79 | 78 | Level4LineNumber | ４階層目行番号 | int? | ★ | |
| 80 | 79 | Level5LineNumber | ５階層目行番号 | int? | ★★ | 通常使用 |
| 81 | 80 | DetailType | 明細種(1固定) | string | ★★★ | |
| **84** | **83** | **GradeCode** | **等級コード** | **string** | **★★★** | **5項目キー** |
| **85** | **84** | **ClassCode** | **階級コード** | **string** | **★★★** | **5項目キー** |
| **86** | **85** | **ShippingMarkCode** | **荷印コード** | **string** | **★★★** | **5項目キー** |
| **90** | **89** | **ProductCode** | **商品コード** | **string** | **★★★** | **5項目キー・00000除外** |
| 95 | 94 | Quantity | 数量 | decimal | ★★★ | |
| 96 | 95 | CategoryCode | 区分(1:ﾛｽ,4:振替,6:調整) | string | ★★★ | |
| 99 | 98 | UnitPrice | 単価 | decimal | ★★★ | |
| 100 | 99 | Amount | 金額 | decimal | ★★★ | |
| 142 | 141 | GradeName | 等級名 | string | - | 参照のみ |
| 143 | 142 | ClassName | 階級名 | string | - | 参照のみ |
| 144 | 143 | ShippingMarkName | 荷印名 | string | - | **使用しない** |
| 148 | 147 | ProductName | 商品名 | string | ★★ | |
| **157** | **156** | **HandInputItem** | **手入力項目(半角8文字)** | **string** | **★★★** | **荷印名として使用（5項目キー）** |

---

## 4. 前月末在庫CSV（PreviousMonthInventoryCsv.cs）

**ファイル形式**: 161列フォーマット  
**伝票区分**: 71（在庫調整）  
**用途**: システム導入時の初期在庫設定

| 列番号 | Index | プロパティ名 | CSVヘッダー名 | データ型 | 重要度 | 備考 |
|--------|-------|-------------|--------------|---------|--------|------|
| 1 | 0 | VoucherDate | 伝票日付 | string | ★★★ | |
| 2 | 1 | VoucherType | 伝票区分(71:在庫調整) | string | ★★★ | |
| 48 | 47 | JobDate | ジョブデート | string | ★★★ | SystemDateは存在しない |
| 82 | 81 | DetailType | 明細種(1固定) | string | ★★★ | |
| **84** | **83** | **GradeCode** | **等級コード** | **string** | **★★★** | **5項目キー** |
| **85** | **84** | **ClassCode** | **階級コード** | **string** | **★★★** | **5項目キー** |
| **86** | **85** | **ShippingMarkCode** | **荷印コード** | **string** | **★★★** | **5項目キー** |
| **90** | **89** | **ProductCode** | **商品コード** | **string** | **★★★** | **5項目キー・00000除外** |
| 95 | 94 | Quantity | 数量 | decimal | ★★★ | |
| 96 | 95 | CategoryCode | 区分(1:ﾛｽ,4:振替,6:調整) | string | ★★★ | |
| 97 | 96 | UnitPrice | 単価 | decimal | ★★★ | |
| 98 | 97 | Amount | 金額 | decimal | ★★★ | |
| 142 | 141 | ShippingMarkName | 荷印名 | string | - | **使用しない** |
| **146** | **145** | **ProductName** | **商品名** | **string** | **★★** | **修正済み（147→145）** |
| **153** | **152** | **HandInputItem** | **手入力項目(半角8文字)** | **string** | **★★★** | **荷印名として使用（5項目キー）** |

---

## 重要な実装ポイント

### 1. 5項目複合キー

すべての伝票データは以下の5項目で一意に識別されます：

| 項目 | 説明 | 桁数 | パディング |
|------|------|------|------------|
| ProductCode | 商品コード | 5桁 | 左0埋め |
| GradeCode | 等級コード | 3桁 | 左0埋め |
| ClassCode | 階級コード | 3桁 | 左0埋め |
| ShippingMarkCode | 荷印コード | 4桁 | 左0埋め |
| ShippingMarkName | 荷印名（手入力項目） | 8桁 | 右空白埋め |

### 2. 荷印名の取得処理

```csharp
// すべての伝票で共通の処理
ShippingMarkName = (HandInputItem ?? "").PadRight(8).Substring(0, 8);
```

- **重要**: CSVに含まれる「荷印名」列は**使用しない**
- 手入力項目（HandInputItem）から取得して8桁固定で使用
- 空白8文字も有効な値として処理

### 3. システムデート/ジョブデート

| 伝票種類 | SystemDate列 | JobDate列 | 備考 |
|----------|-------------|-----------|------|
| 売上伝票 | 48（Index=47） | 49（Index=48） | 同じ日付 |
| 仕入伝票 | 43（Index=42） | 44（Index=43） | 同じ日付 |
| 在庫調整 | 47（Index=46） | 48（Index=47） | 同じ日付 |
| 前月末在庫 | なし | 48（Index=47） | SystemDateなし |

### 4. 階層行番号（LineNumber）

```csharp
// 最も深い階層の値を採用（通常は5階層目）
if (Level5LineNumber > 0) return Level5LineNumber;
if (Level4LineNumber > 0) return Level4LineNumber;
if (Level3LineNumber > 0) return Level3LineNumber;
if (Level2LineNumber > 0) return Level2LineNumber;
if (Level1LineNumber > 0) return Level1LineNumber;
return 1; // デフォルト値
```

### 5. 除外データ条件

#### 共通除外条件（すべての伝票）
- 商品コード「00000」の行は読み飛ばし
- 得意先コード「00000」の行は読み飛ばし（売上伝票）
- 仕入先コード「00000」の行は読み飛ばし（仕入伝票）

#### アンマッチリスト処理での除外
- 在庫調整で単位コード「02」（ギフト経費）の行は処理しない
- 在庫調整で単位コード「05」（加工費B）の行は処理しない
- 荷印名先頭4文字が「EXIT」「exit」の行は処理しない
- 荷印コード「9900」「9910」「1353」の行は処理しない

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2025-06-30 | 初版作成 |
| 2025-06-30 | 前月末在庫の商品名Index修正（147→145） |
| 2025-06-30 | SystemDate/JobDateの追加 |
| 2025-06-30 | 仕入伝票のHandInputItem修正（147→146） |

---

## 凡例

- **★★★**: 必須項目（処理に必要不可欠）
- **★★**: 重要項目（表示や計算に使用）
- **★**: 補助項目（特定の処理で使用）
- **-**: 参照のみ（実際には使用しない）
- **太字**: 特に注意が必要な項目