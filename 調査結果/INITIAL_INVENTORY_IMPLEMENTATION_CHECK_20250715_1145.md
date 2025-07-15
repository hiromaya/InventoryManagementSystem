# 初期在庫インポート実装状況調査結果

実施日時: 2025-07-15 11:45

## 現在の実装状況調査結果

### 1. ValidateRecordメソッド（InitialInventoryImportService.cs: 246-308行目）

#### 荷印名検証
- **実装内容**: `string.IsNullOrEmpty(record.ShippingMarkName)` （267行目）
- **エラーメッセージ**: `"行{rowNumber}: 荷印名が空です"`
- **問題点**: 空白8文字を無効として扱う可能性がある（IsNullOrEmptyは空文字列のみチェック）

#### 荷印コード検証
- **実装内容**: `record.ShippingMarkCode == null` （263行目）
- **エラーメッセージ**: `"行{rowNumber}: 荷印コードがnullです"`
- **特徴**: nullチェックのみ、空白文字は有効として扱う

#### 等級コード検証
- **実装内容**: `record.GradeCode == null` （257行目）
- **エラーメッセージ**: `"行{rowNumber}: 等級コードがnullです"`
- **特徴**: nullチェックのみ、空白文字は有効として扱う

#### 階級コード検証
- **実装内容**: `record.ClassCode == null` （260行目）
- **エラーメッセージ**: `"行{rowNumber}: 階級コードがnullです"`
- **特徴**: nullチェックのみ、空白文字は有効として扱う

#### 商品コード検証
- **実装内容**: `string.IsNullOrEmpty(record.ProductCode)` （253行目）
- **エラーメッセージ**: `"行{rowNumber}: 商品コードが空です"`
- **除外チェック**: `record.ProductCode == "00000"` （302行目）

### 2. ConvertToInventoryMasterメソッド（312-370行目）

#### ImportTypeの設定
- **現在の値**: `"INIT"` （363行目）
- **正しい値との確認**: 期待通り

#### 荷印名の処理
- **実装内容**: `(record.ShippingMarkName ?? "").PadRight(8).Substring(0, 8)` （340行目）
- **処理内容**: 8桁右埋め処理を実装済み

#### 各コードの0埋め処理
- **商品コード**: `record.ProductCode.PadLeft(5, '0')` （336行目）
- **等級コード**: `record.GradeCode.PadLeft(3, '0')` （337行目）
- **階級コード**: `record.ClassCode.PadLeft(3, '0')` （338行目）
- **荷印コード**: `record.ShippingMarkCode.PadLeft(4, '0')` （339行目）

### 3. CSV読み込み設定（ReadCsvFileAsyncメソッド: 188-202行目）

#### TrimOptions
- **設定**: `TrimOptions.None` （201行目）
- **コメント**: "空白8文字を保持するため"と明記
- **評価**: 正しく設定されている

#### Encoding
- **設定**: `Encoding.UTF8` （187行目）
- **注意**: 販売大臣のCSVは通常Shift-JISだが、UTF-8として読み込んでいる

#### その他のオプション
- **HasHeaderRecord**: true
- **IgnoreBlankLines**: true
- **ClassMap登録**: `csv.Context.RegisterClassMap<InitialInventoryRecordMap>()` （205行目）

### 4. 問題点の特定

#### 主要な問題
1. **エンコーディングの不一致**
   - 販売大臣のCSVは通常Shift-JISだが、UTF-8で読み込んでいる
   - これが文字化けやエラーの原因になっている可能性が高い

2. **荷印名の検証ロジック**
   - `string.IsNullOrEmpty`を使用しているが、空白8文字は有効な値
   - ただし、TrimOptions.Noneが設定されているので、空白は保持されるはず

#### 期待される実装と現在の実装の差異

| 項目 | 期待される実装 | 現在の実装 | 影響 |
|-----|------------|----------|-----|
| エンコーディング | Shift-JIS | UTF-8 | 文字化け、読み込みエラー |
| 荷印名検証 | nullチェックのみ | IsNullOrEmpty | 理論上は問題ないはず |
| TrimOptions | None | None | ✓ 正しい |
| ImportType | "INIT" | "INIT" | ✓ 正しい |

### 5. エラーの原因分析

752件のエラーの主な原因は以下の可能性が高い：

1. **エンコーディング問題（最も可能性が高い）**
   - Shift-JISのCSVをUTF-8で読み込むと、日本語文字が正しく読み込めない
   - ヘッダー行の認識に失敗し、データが正しくマッピングされない

2. **CSVフォーマットの問題**
   - 実際のCSVファイルの列数や順序が期待と異なる可能性
   - InitialInventoryRecordMapのインデックス指定が実際のCSVと一致していない可能性

### 6. 推奨される修正

1. **エンコーディングの修正**（最優先）
   ```csharp
   using var reader = new StreamReader(filePath, Encoding.GetEncoding("Shift_JIS"));
   ```

2. **デバッグログの追加**
   - CSV読み込み時の列数確認
   - ヘッダー行の内容確認
   - 各レコードの生データ確認

3. **エラーメッセージの詳細化**
   - どの項目でエラーが発生したか
   - 実際の値と期待される値の表示