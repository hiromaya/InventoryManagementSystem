# import-initial-inventory CHECK制約エラー修正報告書

**修正日時**: 2025年7月14日  
**修正者**: Claude Code  
**対象**: import-initial-inventoryコマンドのCHECK制約違反とCSV検証エラー

## 🚨 発生した問題

### 1. CHECK制約違反（CK_ImportType）
- **エラー**: `ImportType`に`"INITIAL"`を設定していたが、CHECK制約では許可されていない
- **許可される値**: `'INIT', 'IMPORT', 'CARRYOVER', 'MANUAL', 'UNKNOWN'`

### 2. CSV検証エラー（752件/900件がエラー）
- **原因**: 荷印名の検証が厳しすぎる（空白8文字を無効と判定）
- **実際のデータ**: 荷印名は`"        "`（空白8文字）や`" ｺｳ     "`（文字+空白）の形式

## 🔧 実装した修正

### 1. ImportType値の修正

**ファイル**: `src/InventorySystem.Core/Services/InitialInventoryImportService.cs`

```csharp
// 修正前
ImportType = "INITIAL",

// 修正後  
ImportType = "INIT",
```

**対象箇所**:
- ConvertToInventoryMasterAsyncメソッド（InventoryMaster生成時）
- DatasetManagement生成時

### 2. 荷印名検証の緩和

**修正前**:
```csharp
if (string.IsNullOrWhiteSpace(record.ShippingMarkName))
    errors.Add($"行{rowNumber}: 荷印名が空です");
```

**修正後**:
```csharp
// 荷印名の検証：nullまたは空文字列の場合のみエラーとする（空白8文字は有効）
if (string.IsNullOrEmpty(record.ShippingMarkName))
    errors.Add($"行{rowNumber}: 荷印名が空です");
```

### 3. データ整合性チェックの改善

**修正前**: 数量>0かつ単価>0の場合のみチェック

**修正後**: 数量0の場合の特別処理を追加
```csharp
// データ整合性チェック（金額 = 数量 × 単価）
// 数量0の場合は金額も0であることを確認し、単価は問わない
if (record.CurrentStockQuantity == 0)
{
    if (record.CurrentStockAmount != 0)
    {
        errors.Add($"行{rowNumber}: 在庫数量が0の場合、在庫金額も0である必要があります（実際値: {record.CurrentStockAmount}）");
    }
}
else if (record.CurrentStockQuantity > 0 && record.StandardPrice > 0)
{
    var calculatedAmount = record.CurrentStockQuantity * record.StandardPrice;
    var difference = Math.Abs(calculatedAmount - record.CurrentStockAmount);
    
    // 誤差許容範囲: ±10円（小数点計算誤差を考慮）
    if (difference > 10)
    {
        errors.Add($"行{rowNumber}: 在庫金額の整合性エラー - 計算値: {calculatedAmount:F2}, 実際値: {record.CurrentStockAmount:F2}, 差額: {difference:F2}");
    }
}
```

## 📊 期待される改善効果

### CSV検証エラーの大幅減少
- **修正前**: 900件中210件有効（752件エラー）
- **修正後予想**: 900件中800件以上有効（100件以下エラー）

### 対処したデータパターン
1. **荷印名**: `"        "`（空白8文字） → ✅ 有効
2. **荷印名**: `" ｺｳ     "`（文字+空白5文字） → ✅ 有効  
3. **在庫数量0**: 金額も0の場合 → ✅ 有効
4. **ImportType**: `"INIT"`で CHECK制約に適合 → ✅ 有効

## 🔍 実際のCSVデータ分析

**ファイル**: `大臣出力ファイル/ZAIK20250531.csv`

**確認されたデータパターン**:
```csv
104,0,0,5106,        ,1,1,1,  ,0,0,0,0,0,0,0,0,0
104,25,28,7011, ｺｳ     ,1,1,1,  ,37.00,6000.0000,222000.0000,0,0,0,6000.0000,0,0
```

- 荷印名は8桁固定フォーマット
- 在庫数量0のレコードが多数存在
- これらは全て有効な業務データ

## ⚠️ 注意事項

### 1. CHECK制約の仕様確認
```sql
-- データベース内のCHECK制約定義
CONSTRAINT CK_ImportType CHECK (ImportType IN ('INIT', 'IMPORT', 'CARRYOVER', 'MANUAL', 'UNKNOWN'));
```

### 2. 荷印名の正確な処理
- 8桁固定（`PadRight(8).Substring(0, 8)`）
- 空白文字も有効なデータとして処理
- 販売大臣の仕様に準拠

### 3. 今後の注意点
- 新しいImportType値を使用する場合は、CHECK制約の確認が必要
- CSVフォーマットの変更時は、検証ロジックの見直しが必要

## 🚀 次のステップ

1. **Windows環境でのテスト実行**
   ```bash
   dotnet run -- import-initial-inventory
   ```

2. **結果の確認**
   - CHECK制約違反が解消されること
   - エラー件数が大幅に減少すること
   - データベースに正常登録されること

3. **エラーファイルの分析**
   - 残ったエラーの内容確認
   - 必要に応じた追加修正

## 📝 修正履歴

| 日時 | 修正内容 | 影響範囲 |
|------|----------|----------|
| 2025-07-14 | ImportType値修正 | CHECK制約違反解消 |
| 2025-07-14 | 荷印名検証緩和 | CSV検証エラー大幅減少 |
| 2025-07-14 | データ整合性チェック改善 | 数量0データの適切な処理 |

---

**結論**: CsvHelperのArgumentNullExceptionに続き、CHECK制約違反とCSV検証エラーも修正しました。これにより、import-initial-inventoryコマンドが正常に動作するはずです。