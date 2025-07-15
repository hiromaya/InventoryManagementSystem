# 荷印検証ロジック修正レポート

実施日時: 2025-07-15 12:00

## 修正概要

販売大臣AXの仕様に従い、荷印名・荷印コードの検証ロジックとパディング処理を修正しました。

## 修正内容

### 1. 検証ロジックの変更

#### 修正前の問題
- `string.IsNullOrWhiteSpace()`を使用していたため、空白文字のみのデータがエラーになっていた
- 実データの77%（690件/900件）が空白8文字の荷印名を持っており、これらが全てエラーとして扱われていた

#### 修正内容
| ファイル | 修正前 | 修正後 |
|---------|--------|--------|
| InitialInventoryImportService.cs | `IsNullOrEmpty(ShippingMarkName)` | 変更なし（すでに適切） |
| PreviousMonthInventoryCsv.cs | `IsNullOrWhiteSpace(ShippingMarkCode)` | `ShippingMarkCode == null` |

### 2. パディング処理の修正

#### 荷印コードの処理
| ファイル | 修正前 | 修正後 |
|---------|--------|--------|
| InitialInventoryImportService.cs | `ShippingMarkCode.PadLeft(4, '0')` | `ShippingMarkCode ?? "    "` |
| PreviousMonthInventoryCsv.cs | `Trim().PadLeft(4, '0')` | `ShippingMarkCode ?? "    "` |
| SalesVoucherDaijinCsv.cs | `ShippingMarkCode?.Trim() ?? string.Empty` | `ShippingMarkCode ?? "    "` |
| PurchaseVoucherDaijinCsv.cs | `ShippingMarkCode?.Trim() ?? string.Empty` | `ShippingMarkCode ?? "    "` |
| InventoryAdjustmentDaijinCsv.cs | `ShippingMarkCode?.Trim() ?? string.Empty` | `ShippingMarkCode ?? "    "` |

#### 荷印名の処理
| ファイル | 修正前 | 修正後 |
|---------|--------|--------|
| InitialInventoryImportService.cs | `PadRight(8).Substring(0, 8)` | `ShippingMarkName ?? "        "` |
| PreviousMonthInventoryCsv.cs | `PadRight(8).Substring(0, 8)` | `HandInputItem ?? "        "` |
| SalesVoucherDaijinCsv.cs | `TrimEnd().PadRight(8).Substring(0, 8)` | `HandInputItem ?? "        "` |
| PurchaseVoucherDaijinCsv.cs | `TrimEnd().PadRight(8).Substring(0, 8)` | `HandInputItem ?? "        "` |
| InventoryAdjustmentDaijinCsv.cs | `TrimEnd().PadRight(8).Substring(0, 8)` | `HandInputItem ?? "        "` |

## 修正の根拠

### 1. Geminiとの協議結果
- 荷印コードは文字系コードとして扱い、空白をそのまま保持すべき
- 数値系コード（商品・等級・階級）とは異なる処理が必要
- 販売大臣の元データを改変せずに保存することが重要

### 2. 実データの分析
```
ZAIK20250531.csv の実データ例：
- 荷印コード: '5106', '7031', '    '（空白4文字）
- 荷印名: ' ｺｳ     ', 'ﾃﾆ2     ', '        '（空白8文字）
```

### 3. 販売大臣AXの仕様
- 荷印名・荷印コードは任意項目
- 空白も有効な値として扱う必要がある
- 5項目複合キーの一部として使用される

## 期待される効果

### 初期在庫インポート
- **修正前**: エラー752件、成功210件
- **修正後（期待値）**: エラー大幅減少、成功件数大幅増加

### 通常インポート（import-folder）
- 荷印名・荷印コード関連のエラーが解消
- 空白データも正常に処理される

## 修正ファイル一覧

1. `/src/InventorySystem.Core/Services/InitialInventoryImportService.cs`
2. `/src/InventorySystem.Import/Models/PreviousMonthInventoryCsv.cs`
3. `/src/InventorySystem.Import/Models/SalesVoucherDaijinCsv.cs`
4. `/src/InventorySystem.Import/Models/PurchaseVoucherDaijinCsv.cs`
5. `/src/InventorySystem.Import/Models/InventoryAdjustmentDaijinCsv.cs`

## 注意事項

1. **データの正規化**
   - 空白文字をTrimしないことで、元データの整合性を保持
   - nullの場合のみデフォルト値（空白4文字/8文字）を設定

2. **後方互換性**
   - 既存のデータベースに登録済みのデータとの整合性に注意
   - 必要に応じてデータ移行スクリプトの作成を検討

3. **テスト推奨事項**
   - 実際の初期在庫インポートコマンドでの動作確認
   - 各種伝票インポートでの空白データ処理の確認