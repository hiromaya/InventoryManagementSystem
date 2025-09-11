# SE3 仕様（商品勘定・在庫表・CP在庫マスタ）

この文書は、`se3/CLAUDE.md` の内容を元にした正式な取り込み版です。SE3領域（商品勘定・在庫表・CP在庫マスタ）に関する仕様のソース・オブ・トゥルースとして参照してください。原本: `../../se3/CLAUDE.md`

---

# 在庫管理システム開発プロジェクト SE3用指示書

**ロール**: SE3 - 商品勘定・在庫表担当  
**最終更新**: 2025年7月31日  
**バージョン**: 2.0  
プロジェクトパス：../InventoryManagementSystem/

## 担当範囲
SE3は以下の機能を担当します：
- **商品勘定帳票**: 商品別の在庫収支表（A3横向き）
- **在庫表帳票**: 在庫状況一覧（A4縦向き）
- **CP在庫マスタ作成**: 商品勘定実行時に作成

## 重要な仕様理解

### CP在庫マスタについて（最重要）
- **作成タイミング**: 商品勘定実行時に初めて作成
- **削除タイミング**: 
  - 商品勘定実行時（既存削除してから新規作成）
  - 日次終了時
- **DataSetId**: なし（使い捨てテーブル）
- **特徴**: 全帳票（商品勘定・商品日報・在庫表）で共有使用

### 処理フロー
```
1. 商品勘定コマンド実行
   ↓
2. 既存CP在庫マスタを削除（TRUNCATE）
   ↓
3. 在庫マスタからCP在庫マスタへ全データコピー
   ↓
4. 商品勘定帳票作成
   ↓
5. 他の帳票（商品日報・在庫表）もCP在庫マスタを使用
   ↓
6. 日次終了時にCP在庫マスタ削除
```

### 5項目複合キー
```
商品コード（5桁） + 等級コード（3桁） + 階級コード（3桁） + 荷印コード（4桁） + 荷印名（8桁固定）
```

## 商品勘定仕様

### 概要
- **目的**: 商品別の仕入・売上・在庫の動きを一覧表示
- **用紙**: A3横向き（420mm × 297mm）
- **フォント**: ＭＳ ゴシック
- **出力**: FastReport.NET使用

### CP在庫マスタ作成処理
```csharp
// 商品勘定サービスの実装例
public async Task<byte[]> GenerateProductAccountAsync(DateTime jobDate)
{
    // 1. 既存CP在庫マスタを削除
    await _cpInventoryRepository.TruncateAsync();
    
    // 2. 在庫マスタからCP在庫マスタへコピー（ストアドプロシージャ使用）
    await _cpInventoryRepository.CreateFromInventoryMasterAsync(jobDate);
    
    // 3. CP在庫マスタからデータ取得
    var cpInventoryData = await _cpInventoryRepository.GetAllAsync();
    
    // 4. 帳票作成処理...
}
```

### ストアドプロシージャ
```sql
-- sp_CreateCpInventoryFromInventoryMasterCumulative
-- 在庫マスタの当日データをCP在庫マスタへコピー
-- パラメータ: @JobDate のみ（DataSetIdは不要）
```

### レイアウト
```
作成日：YYYY年MM月DD日 HH時MM分SS秒        ※ YYYY年MM月DD日 商 品 勘 定 ※        ZZZ9 頁
担当者：[担当者コード][担当者名]

商品名 | 荷印名 | 手入力 | 等級 | 階級 | 伝票NO | 区分 | 月日 | 仕入数量 | 売上数量 | 残数量 | 単価 | 金額 | 粗利益 | 取引先名
```

### 区分表示
- 前残（前日残高）
- 掛仕（掛仕入）、現仕（現金仕入）
- 掛売（掛売上）、現売（現金売上）
- 調整（在庫調整）
- 腐り、ロス、加工、振替

### 処理仕様
1. CP在庫マスタから商品を読み込み
2. 売上・仕入伝票との突合
3. 移動平均法による在庫単価計算
4. 粗利益計算（売上単価 - 在庫単価）× 数量
5. 商品ごとに小計、最後に合計

### コマンド
```bash
# 商品勘定作成（CP在庫マスタ作成含む）
dotnet run product-account [YYYY-MM-DD]
```

## 在庫表仕様

### 概要
- **目的**: 現在の在庫状況を担当者別・商品別に表示
- **用紙**: A4縦向き
- **フォント**: ＭＳ ゴシック
- **出力**: FastReport.NET使用

### データ取得
```csharp
// 在庫表サービスの実装例
public async Task<byte[]> GenerateInventoryListAsync(DateTime jobDate)
{
    // CP在庫マスタから直接データ取得（既に商品勘定で作成済み）
    var cpInventoryData = await _cpInventoryRepository.GetAllAsync();
    
    if (!cpInventoryData.Any())
    {
        throw new InvalidOperationException(
            "CP在庫マスタが作成されていません。先に商品勘定を実行してください。");
    }
    
    // 帳票作成処理...
}
```

### レイアウト
```
作成日：YYYY年MM月DD日 HH時MM分SS秒                    ZZZ9 頁
※　YYYY年MM月DD日　在　庫　表　※

担当者コード: XX

商品名 | 荷印 | 等級 | 階級 | 在庫数量 | 在庫単価 | 在庫金額 | 最終入荷日 | ﾏｰｸ
```

### 滞留警告マーク
- `!`: 11日以上20日経過
- `!!`: 21日以上30日経過
- `!!!`: 31日以上経過

### 処理仕様
1. CP在庫マスタを担当者・商品でソート
2. 前日在庫数が0の行は印字しない
3. 当日在庫数量・金額が0の明細は印字しない
4. 担当者が変わったら改ページ
5. 商品が変わったら小計、最後に合計

### コマンド
```bash
# 在庫表作成（CP在庫マスタ使用）
dotnet run inventory-list [YYYY-MM-DD]
```

## 実装ガイドライン

### 1. 基本構成
```csharp
// サービスインターフェース
public interface IProductAccountService
{
    Task<byte[]> GenerateProductAccountAsync(DateTime jobDate);
}

public interface IInventoryListService
{
    Task<byte[]> GenerateInventoryListAsync(DateTime jobDate);
}
```

### 2. CP在庫マスタの作成と利用
```csharp
// 商品勘定での処理
// 1. TRUNCATE実行
await _cpInventoryRepository.TruncateAsync();

// 2. ストアドプロシージャでコピー
await _cpInventoryRepository.CreateFromInventoryMasterAsync(jobDate);

// 3. データ取得
var cpInventory = await _cpInventoryRepository.GetAllAsync();
```

### 3. エラーハンドリング
- CP在庫マスタが存在しない場合は明確なエラーメッセージ
- 0除算対策を必ず実装
- マスタ参照エラーの適切な処理

### 4. パフォーマンス考慮
- TRUNCATE使用で高速削除
- 大量データは分割して処理
- 不要なマスタ参照を避ける
- SQLクエリの最適化

## ファイル構成（推奨）
```
src/
├── InventorySystem.Reports/
│   ├── FastReport/
│   │   └── Templates/
│   │       ├── ProductAccount.frx    # 商品勘定テンプレート
│   │       └── InventoryList.frx     # 在庫表テンプレート
│   └── Services/
│       ├── ProductAccountService.cs
│       └── InventoryListService.cs
└── InventorySystem.Console/
    └── Commands/
        ├── ProductAccountCommand.cs
        └── InventoryListCommand.cs
```

## テスト方針
1. **単体テスト**: 計算ロジックの検証
2. **統合テスト**: CP在庫マスタの作成・削除確認
3. **帳票テスト**: レイアウトと集計値の確認

## 注意事項
- **CP在庫マスタは商品勘定で作成**（他の帳票は作成済みを使用）
- **DataSetIdは使用しない**（使い捨てテーブル）
- **Process 2-5は実行しない**（SE2担当）
- **FastReport.NETのライセンスは取得済み**
- **文字コード**: UTF-8 with BOM
- **改ページ処理を確実に実装**

## 開発手順
1. CP在庫マスタのTRUNCATE処理実装
2. ストアドプロシージャ呼び出し実装
3. サービスクラスの作成
4. コマンドクラスの作成
5. FastReportテンプレートの作成
6. 単体テストの作成
7. 統合テストの実施

## 実行順序の重要性
```bash
# 1. 商品勘定を最初に実行（CP在庫マスタ作成）
dotnet run product-account 2025-06-30

# 2. その後、他の帳票を実行可能
dotnet run daily-report 2025-06-30    # SE2担当
dotnet run inventory-list 2025-06-30  # SE3担当
```

## 🔗 tmux相互通信仕様（抜粋）

ペイン: PM/SE1/SE2/SE3。SE3は作業完了・エラー時に必ずPMへ2段階送信で報告:
```bash
tmux send-keys -t claude-company.1 "[SE3] 完了：在庫表作成正常終了"
tmux send-keys -t claude-company.1 Enter
```

禁止: 冗長表現、重複報告。Git運用: SE3はプッシュ禁止、コミット準備まで。

---

重要: CP在庫マスタは全帳票の基盤。移動平均法の0除算対策と桁処理を徹底し、矛盾を発見した場合は即時報告すること。

