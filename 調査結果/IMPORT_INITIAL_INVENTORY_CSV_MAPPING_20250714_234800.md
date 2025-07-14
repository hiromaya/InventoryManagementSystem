# import-initial-inventoryコマンド CSVマッピング仕様書

**作成日時**: 2025年7月14日 23:48:00  
**対象ファイル**: ZAIK*.csv（移行用在庫マスタ）  
**実装ファイル**: `src/InventorySystem.Core/Models/InitialInventoryRecord.cs`  

## 概要

import-initial-inventoryコマンドは、販売大臣AXから出力される移行用在庫マスタ（ZAIK*.csv）を読み取り、在庫管理システムの初期在庫データとして取り込むためのコマンドです。

## CSVファイル仕様

### ファイル形式
- **エンコーディング**: UTF-8 with BOM
- **ヘッダー行**: あり（1行目）
- **列数**: 18列（有効データ列）
- **フォーマット**: カンマ区切り（CSV）
- **命名規則**: `ZAIK{YYYYMMDD}.csv`（例：ZAIK20250531.csv）

### ファイルパス
- **インポートパス**: `D:\InventoryImport\{部門名}\Import\`
- **処理済みパス**: `D:\InventoryImport\{部門名}\Processed\`
- **エラーパス**: `D:\InventoryImport\{部門名}\Error\`

## CSVマッピング詳細

### マッピング方式
- **実装方式**: CsvHelper ClassMapのみ使用
- **属性**: 削除済み（トリミング問題対策）
- **ClassMap**: `InitialInventoryRecordMap`クラス

### 列マッピング一覧

| 列番号 | Index | CSVヘッダー名 | プロパティ名 | データ型 | 必須 | 説明 |
|--------|-------|---------------|--------------|----------|------|------|
| 1 | 0 | 商品ＣＤ | ProductCode | string | ✓ | 商品コード（5桁、左0埋め） |
| 2 | 1 | 等級ＣＤ | GradeCode | string | ✓ | 等級コード（3桁、左0埋め） |
| 3 | 2 | 階級ＣＤ | ClassCode | string | ✓ | 階級コード（3桁、左0埋め） |
| 4 | 3 | 荷印ＣＤ | ShippingMarkCode | string | ✓ | 荷印コード（4桁、左0埋め） |
| 5 | 4 | 荷印名 | ShippingMarkName | string | ✓ | 荷印名（8桁固定） |
| 6 | 5 | 商品分類１担当者ＣＤ | PersonInChargeCode | int | - | 担当者コード |
| 7-9 | 6-8 | - | - | - | - | **スキップ列** |
| 10 | 9 | 前日在庫数量 | PreviousStockQuantity | decimal | - | 前日在庫数量 |
| 11 | 10 | - | - | - | - | **スキップ列** |
| 12 | 11 | 前日在庫金額 | PreviousStockAmount | decimal | - | 前日在庫金額 |
| 13-14 | 12-13 | - | - | - | - | **スキップ列** |
| 15 | 14 | 当日在庫数量 | CurrentStockQuantity | decimal | ✓ | 在庫数量（メイン項目） |
| 16 | 15 | 当日在庫単価 | StandardPrice | decimal | ✓ | 標準単価 |
| 17 | 16 | 当日在庫金額 | CurrentStockAmount | decimal | ✓ | 在庫金額 |
| 18 | 17 | 粗利計算用平均単価 | AveragePrice | decimal | - | 平均単価（粗利計算用） |

### スキップ列について
以下の列はCSVに存在しますが、マッピング対象外です：
- **列6-8** (Index 6-8): 未使用領域
- **列10** (Index 10): 予備項目
- **列12-13** (Index 12-13): 予備項目

## ClassMap実装

```csharp
public sealed class InitialInventoryRecordMap : ClassMap<InitialInventoryRecord>
{
    public InitialInventoryRecordMap()
    {
        // 属性を使わず、ClassMapのみでマッピングを定義（トリミング問題回避）
        Map(m => m.ProductCode).Index(0).Name("商品ＣＤ");
        Map(m => m.GradeCode).Index(1).Name("等級ＣＤ");
        Map(m => m.ClassCode).Index(2).Name("階級ＣＤ");
        Map(m => m.ShippingMarkCode).Index(3).Name("荷印ＣＤ");
        Map(m => m.ShippingMarkName).Index(4).Name("荷印名");
        Map(m => m.PersonInChargeCode).Index(5).Name("商品分類１担当者ＣＤ");
        Map(m => m.PreviousStockQuantity).Index(9).Name("前日在庫数量");
        Map(m => m.PreviousStockAmount).Index(11).Name("前日在庫金額");
        Map(m => m.CurrentStockQuantity).Index(14).Name("当日在庫数量");
        Map(m => m.StandardPrice).Index(15).Name("当日在庫単価");
        Map(m => m.CurrentStockAmount).Index(16).Name("当日在庫金額");
        Map(m => m.AveragePrice).Index(17).Name("粗利計算用平均単価");
    }
}
```

## バリデーション仕様

### 必須項目チェック
- ProductCode: 商品コード（空白不可、"00000"は除外）
- GradeCode: 等級コード（空白不可）
- ClassCode: 階級コード（空白不可）
- ShippingMarkCode: 荷印コード（空白不可）
- ShippingMarkName: 荷印名（空白不可）

### 数値検証
- CurrentStockQuantity: 負値不可
- CurrentStockAmount: 負値不可
- StandardPrice: 負値不可

### データ整合性チェック
- **金額整合性**: `CurrentStockAmount = CurrentStockQuantity × StandardPrice`
- **誤差許容範囲**: ±1円
- **除外チェック**: 商品コード"00000"は処理対象外

## 5項目複合キー構築

初期在庫データは以下の5項目で複合キーを構築します：

```csharp
var key = new InventoryKey
{
    ProductCode = record.ProductCode.PadLeft(5, '0'),           // 5桁左0埋め
    GradeCode = record.GradeCode.PadLeft(3, '0'),               // 3桁左0埋め
    ClassCode = record.ClassCode.PadLeft(3, '0'),               // 3桁左0埋め
    ShippingMarkCode = record.ShippingMarkCode.PadLeft(4, '0'), // 4桁左0埋め
    ShippingMarkName = (record.ShippingMarkName ?? "").PadRight(8).Substring(0, 8) // 8桁固定
};
```

## 使用方法

### コマンド実行例

```bash
# 基本実行（部門指定）
dotnet run -- import-initial-inventory DeptA

# 指定日付の初期在庫取込
dotnet run -- import-initial-inventory DeptA 2025-05-31
```

### 処理フロー

1. **ファイル検索**: `D:\InventoryImport\DeptA\Import\`からZAIK*.csvを検索
2. **日付推定**: ファイル名から対象日付を抽出（ZAIK20250531.csv → 2025-05-31）
3. **CSV読込**: ClassMapを使用してデータを読み取り
4. **バリデーション**: 必須項目・数値・整合性をチェック
5. **変換処理**: InitialInventoryRecord → InventoryMaster
6. **DB登録**: SqlBulkCopyで高速一括登録
7. **ファイル移動**: 成功時はProcessedフォルダ、エラー時はErrorフォルダへ移動

## エラーハンドリング

### 一般的なエラー
- **ファイル未存在**: ZAIK*.csvファイルが見つからない
- **形式エラー**: CSV形式が不正、列数不足
- **データエラー**: 必須項目空白、データ型不一致
- **整合性エラー**: 金額計算の不整合

### エラー出力
- **ログファイル**: 詳細なエラー内容とスタックトレース
- **エラーCSV**: エラー行を含むCSVファイル
- **サマリー**: 成功件数・エラー件数・処理時間

## 技術的詳細

### CsvHelper設定

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    HeaderValidated = null,
    MissingFieldFound = null,
    BadDataFound = context => { /* ログ出力 */ },
    IgnoreBlankLines = true,
    TrimOptions = TrimOptions.Trim
};
```

### トリミング対策
- **問題**: .NET 8のアセンブリトリミング機能により属性ベースマッピングでArgumentNullException
- **対策**: 属性削除、ClassMapのみ使用でトリミング耐性を確保

### パフォーマンス
- **SqlBulkCopy**: 大量データの高速登録
- **バッチ処理**: 1000件単位でのメモリ効率化
- **トランザクション**: 全件成功またはロールバック

## 関連ファイル

### 実装ファイル
- `src/InventorySystem.Core/Models/InitialInventoryRecord.cs` - データモデルとClassMap
- `src/InventorySystem.Core/Services/InitialInventoryImportService.cs` - インポートサービス
- `src/InventorySystem.Console/Commands/ImportInitialInventoryCommand.cs` - コマンド実装

### 設定ファイル
- `appsettings.json` - 接続文字列とログ設定
- `CLAUDE.md` - プロジェクト固有ルール

## 更新履歴

- **2025-07-14**: 初回作成
- **2025-07-14**: トリミング問題対策（属性削除、ClassMapのみ使用）
- **2025-07-14**: バリデーション強化（金額整合性チェック追加）

---

**注意**: このマッピング仕様は移行用在庫マスタ（ZAIK*.csv）専用です。他の伝票CSVとは形式が異なります。