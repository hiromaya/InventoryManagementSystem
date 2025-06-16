# 在庫管理システム開発ガイド

## 📋 プロジェクト概要

食品販売業向けの在庫管理システム。販売大臣AXの外付けシステムとして、日々の在庫処理時間を3時間55分から30分に短縮することが目標。

## 🛠️ 技術スタック

- **言語**: C# (.NET 8.0)
- **データベース**: SQL Server 2022 Express
- **帳票**: QuestPDF
- **データアクセス**: Dapper
- **CSV処理**: CsvHelper
- **ログ**: Serilog
- **開発環境**: Cursor + Claude Code

## ⚡ 最重要仕様

### 1. 在庫マスタのキー構造（5項目複合キー）
```csharp
public class InventoryKey
{
    public string ProductCode { get; set; }      // 商品コード (15桁)
    public string GradeCode { get; set; }        // 等級コード (15桁)
    public string ClassCode { get; set; }        // 階級コード (15桁)
    public string ShippingMarkCode { get; set; } // 荷印コード (15桁)
    public string ShippingMarkName { get; set; } // 荷印名 (50桁)
}
```

### 2. 日付管理
- **必ず汎用日付2（ジョブデート）を使用**
- 伝票日付ではなく、実際の処理日で管理

### 3. 処理の基本フロー
1. 各処理で**毎回新規にCP在庫Mを作成**
2. 当日エリアクリア → 当日発生フラグ='9'
3. データ集計 → 当日発生フラグ='0'
4. 在庫計算・粗利計算

### 4. 粗利計算（2段階）
```csharp
// 第1段階：売上伝票1行ごと
粗利益 = (売上単価 - 在庫単価) × 数量

// 第2段階：調整
最終粗利益 = 当日粗利益 - 当日在庫調整金額 - 当日加工費
```

## 📐 設計方針

### エラーハンドリング
- **0除算対策を必ず実装**
- すべての計算処理で分母チェック
- 例外時は適切なデフォルト値またはエラー処理

### パフォーマンス
- バッチ処理は1000件単位
- 適切なインデックス設計
- 大量データの一括読み込みは避ける

### 誤操作防止
- データセットID管理の実装
- 処理履歴の記録
- ロールバック機能の実装

## 💻 コーディング規約

### 命名規則
```csharp
// クラス: PascalCase
public class InventoryMaster { }

// メソッド: PascalCase
public void CalculateGrossProfit() { }

// プライベートフィールド: _camelCase
private readonly IInventoryRepository _inventoryRepository;

// ローカル変数: camelCase
var dailyStock = 0;
```

### データベースアクセス
```csharp
// Dapper使用、using文で確実にリソース解放
using var connection = new SqlConnection(connectionString);
var result = await connection.QueryAsync<InventoryMaster>(sql, parameters);
```

## 🚫 除外データ条件

### アンマッチ・商品勘定でのみ除外
- 荷印名の先頭4文字が「EXIT」「exit」
- 荷印コードが「9900」「9910」「1353」

※商品日報・在庫表では使用する

## 🔄 特殊処理ルール

### 商品分類変更
- 荷印名先頭「9aaa」→ 商品分類1='8'
- 荷印名先頭「1aaa」→ 商品分類1='6'
- 荷印名先頭「0999」→ 商品分類1='6'

## 📁 フォルダ構成

```
D:\InventoryImport\
└── User01\（部門別）
    ├── Import\     # CSV取込先
    ├── Processed\  # 処理済み
    └── Error\      # エラー
```

## 🎯 パフォーマンス目標

| 処理           | 現状     | 目標     |
|--------------- |--------- |--------- |
| アンマッチ処理 | 12分     | 3分      |
| 在庫計算       | 15分     | 5分      |
| 帳票出力       | 10分     | 5分      |
| **合計**       | **42分** | **15分** |

## 📊 データ量の目安

- 売上伝票: 500-1,200件/日（年間28万件）
- 仕入伝票: 200-400件/日（年間12万件）
- 在庫マスタ: 年間約10,000件

## 🔨 新しいルールの追加プロセス

ユーザーから今回限りではなく常に対応が必要だと思われる指示を受けた場合：

1. 「これを標準のルールにしますか？」と質問する
2. YESの回答を得た場合、このCLAUDE.mdに追加ルールとして記載する
3. 以降は標準ルールとして常に適用する

このプロセスにより、プロジェクトのルールを継続的に改善していきます。

---

最終更新日: 2025年6月16日