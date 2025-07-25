# Claude Code プロジェクトルール

## 🎯 プロジェクト概要

このプロジェクトでは、Claude CodeとGemini CLIを連携させ、それぞれの強みを活かした開発を行います。

- **Claude Code**: コード生成、リファクタリング、ロジック設計
- **Gemini CLI**: Web検索、画像/PDF解析、視覚情報の理解

## 📋 必須ルール

### 1. Web検索の実行方法

**❌ 禁止**: Claude Codeの直接的なWeb検索機能の使用
**✅ 必須**: Gemini CLI経由での検索

```bash
# 正しい使用方法
/home/hiroki/.claude/gemini-search.sh "検索クエリ"

# 絶対に使用しない
# ・Claude Codeの内蔵Web検索
# ・直接的なURL参照による情報取得
```

### 2. 画像・図解の処理

**❌ 禁止**: Claude Codeでの画像内容の推測や解釈
**✅ 必須**: Gemini CLIによる画像解析

```bash
# 画像解析は必ずGemini CLI経由
/home/hiroki/.claude/analyze_image.sh <画像パス> [質問]
/home/hiroki/.claude/analyze_file.sh <画像パス> [質問] diagram
```

### 3. PDF文書の処理

**❌ 禁止**: Claude CodeでのPDF内容の直接読み取り
**✅ 必須**: Gemini CLIによるPDF解析

```bash
# PDF解析は必ずGemini CLI経由
/home/hiroki/.claude/analyze_pdf.sh <PDFパス> [質問]
```

## 🛠️ 利用可能なコマンド一覧

| コマンド | 用途 | 使用例 |
|---------|------|--------|
| `gemini-search.sh` | Web検索 | `/home/hiroki/.claude/gemini-search.sh "Ubuntu 24.04 新機能"` |
| `analyze_image.sh` | 画像解析 | `/home/hiroki/.claude/analyze_image.sh ./img.png "何が写っていますか"` |
| `analyze_pdf.sh` | PDF解析 | `/home/hiroki/.claude/analyze_pdf.sh ./doc.pdf "要約して"` |
| `analyze_file.sh` | 汎用ファイル解析 | `/home/hiroki/.claude/analyze_file.sh ./chart.jpg "データを読み取って" chart` |
| `batch_analyze.sh` | 一括解析 | `/home/hiroki/.claude/batch_analyze.sh ./images "*.png"` |

## 📐 解析タイプ指定

`analyze_file.sh`使用時の解析タイプ：

- `auto` - 自動判定（デフォルト）
- `image` - 一般的な画像
- `diagram` - 図解・フローチャート・アーキテクチャ図
- `chart` - グラフ・チャート・データ可視化
- `pdf` - PDF文書

## 🔄 ワークフロー例

### 1. 技術調査タスク
```bash
# Step 1: 最新情報の検索
/home/hiroki/.claude/gemini-search.sh "React Server Components 2025"

# Step 2: 公式ドキュメントPDFの解析
/home/hiroki/.claude/analyze_pdf.sh ./react-rsc-guide.pdf "主要な変更点をリストアップ"

# Step 3: アーキテクチャ図の理解
/home/hiroki/.claude/analyze_file.sh ./rsc-architecture.png "データフローを説明" diagram
```

### 2. データ分析タスク
```bash
# Step 1: 売上データのグラフ解析
/home/hiroki/.claude/analyze_file.sh ./sales-chart.jpg "トレンドと異常値を特定" chart

# Step 2: レポートPDFから詳細情報抽出
/home/hiroki/.claude/analyze_pdf.sh ./quarterly-report.pdf "地域別の売上を抽出"

# Step 3: 競合情報の検索
/home/hiroki/.claude/gemini-search.sh "業界平均成長率 2025"
```

## ⚠️ 重要な制限事項

1. **最新情報の取得**
   - 2025年1月以降の情報は必ずGemini CLI検索を使用
   - 時事的な話題、価格情報、最新技術動向は検索必須

2. **視覚情報の解釈**
   - スクリーンショット、UIデザイン、ワイヤーフレーム → Gemini CLI
   - 技術図解、システム構成図、ER図 → Gemini CLI
   - 数値データを含むグラフ、チャート → Gemini CLI

3. **文書処理**
   - PDF、スキャン画像、複雑なレイアウト文書 → Gemini CLI
   - プレーンテキスト、Markdown、ソースコード → Claude Code

## 🚀 ベストプラクティス

### 効率的な質問の仕方

```bash
# ❌ 悪い例：曖昧な質問
/home/hiroki/.claude/analyze_image.sh ./screenshot.png

# ✅ 良い例：具体的な質問
/home/hiroki/.claude/analyze_image.sh ./screenshot.png "エラーメッセージとその原因を特定してください"
```

### 結果の活用方法

1. Gemini CLIの出力を`.claude/analysis_results/`に保存
2. 解析結果を基にClaude Codeでコード生成
3. 必要に応じて追加の質問で詳細を深掘り

## 🔧 トラブルシューティング

### よくある問題と解決策

| 問題 | 原因 | 解決策 |
|------|------|--------|
| スクリプトが実行できない | 実行権限なし | `chmod +x .claude/*.sh` |
| ファイルが見つからない | 相対パスの問題 | 絶対パスを使用するか、プロジェクトルートから実行 |
| Gemini CLIエラー | APIキー未設定 | `gemini auth`で認証を実行 |

## 📝 プロジェクト固有の追加ルール

### コード生成時の注意事項

1. 画像やPDFから抽出した情報を基にコードを生成する場合：
   - 必ず解析結果を確認してから実装
   - 不明な点は再度Gemini CLIで詳細解析

2. Web検索結果の活用：
   - 最新のAPIやライブラリ情報は検索結果を優先
   - 検索結果とClaude Codeの知識に矛盾がある場合は、新しい情報を採用

### チーム開発での運用

1. 解析結果は必ず共有可能な形式で保存
2. 重要な図解の解析結果はドキュメント化
3. 定期的にGemini CLIのログを確認し、使用パターンを最適化

## 🔄 更新履歴

- 2025-01-XX: 初版作成
- 2025-01-XX: バッチ処理機能追加
- 2025-01-XX: 解析タイプの詳細化
- **2025-06-30: 荷印名の仕様変更を反映**
- **2025-06-30: 除外データ条件を明確化**
- **2025-06-30: 荷印名（手入力）の処理詳細を追加**
- **2025-07-01: FastReportの使用方針を追加**
- **2025-07-01: CSVマッピング仕様を詳細化（正確な列番号とIndex、ジョブデートを追加）**
- **2025-07-01: import-folderコマンドの処理ルールを追加**
- **2025-07-05: 環境別ビルドエラー対応ルールを追加（Windows環境優先）**
- **2025-07-05: FastReport必須仕様を追加（最重要）**
- **2025-07-05: Linux環境でのビルドエラーは無視して開発を進めるルールを強化**
- **2025-07-05: CSVマッピング仕様に商品名列を追加（売上148列目、仕入140列目、在庫調整146列目）**
- **2025-07-07: JobDate仕様を追加（伝票データに手動でJobDateを指定する機能）**
- **2025-07-18: init-database--force実行時の024_CreateProductMaster.sql除外設定（移行作業保護のため）**
- **2025-07-19: DailyCloseManagement完全実装（Gemini CLI連携によるベストプラクティス適用）**
- **2025-07-22: DataSets/DataSetManagement二重管理問題の完全解決**
- **2025-07-22: 商品勘定帳票機能とストアドプロシージャsp_CreateProductLedgerData実装**
- **2025-07-22: FastReport商品勘定テンプレート詳細設計適用（A3横1512px幅対応）**
- **2025-07-26: アンマッチリスト仕様変更実装（在庫0エラー完全削除、出荷系データのみチェック対象）**

---

**注意**: このルールは厳守してください。Claude Codeの制限を補完し、より高品質な成果物を生成するための重要なガイドラインです。


## 🔨 最重要ルール - 新しいルールの追加プロセス

ユーザーから今回限りではなく常に対応が必要だと思われる指示を受けた場合：

1. 「これを標準のルールにしますか？」と質問する
2. YESの回答を得た場合、CLAUDE.mdに追加ルールとして記載する
3. 以降は標準ルールとして常に適用する

このプロセスにより、プロジェクトのルールを継続的に改善していきます。

## ⚠️ 環境別ビルドエラー対応ルール

### Windows環境優先の原則

1. **絶対に守るべきルール**
   - Windows環境での動作を最優先とする
   - Linux環境でのビルドエラーを解消するために、Windows環境の動作に影響を与える変更は行わない
   - 特に、FastReportサービスなどのWindows専用機能を勝手にPlaceholderに置き換えない

2. **Linux環境でのビルドエラー対応**
   - **重要**: Linux環境（Claude Code）でのWindows関連のビルドエラーは無視して開発を進める
   - FastReport関連のビルドエラーが発生しても、機能実装を優先する
   - ビルドエラーの修正に時間を費やさず、実際の機能開発に集中する
   - Windows環境でのビルドエラーが発生した場合のみ対応する

3. **変更時の注意事項**
   - `#if WINDOWS`ディレクティブ内のコードは絶対に変更しない
   - DI登録でFastReportサービスをPlaceholderサービスに勝手に置き換えない
   - Program.csの環境別分岐は維持する

4. **開発時の推奨事項**
   - Linux環境でビルドエラーが出ても、コード実装・修正を継続する
   - エラーメッセージに「FastReport」「Windows」が含まれる場合は無視
   - 機能の実装完了を最優先とする

**重要**: このプロジェクトは主にWindows環境で実行されることを前提としており、Linux環境（Claude Code）はあくまで開発補助ツールとしての位置づけです。Linux環境でのビルドエラーは開発の妨げにならないよう、適切に無視してください。

## 📊 在庫管理システム開発ガイドライン

### プロジェクト固有の開発ルール

#### 1. CSVインポート処理
- **販売大臣CSVの処理**：必ず既存のCSVマッピングクラスを参照し、インデックス指定を維持する
- **バリデーション修正**：バリデーションルールを変更する際は、実際のCSVデータを確認してから修正する
- **エラー処理**：インポートエラーは詳細なログを残し、どのレコードでエラーが発生したか特定できるようにする

#### 2. データベース操作
- **テーブル構造変更**：既存データがある場合は必ずマイグレーションスクリプトを作成する
- **カラム名変更**：`sp_rename`を使用し、ロールバック手順も記載する
- **パフォーマンス**：大量データ処理時は必ずバッチ処理を使用（1000件単位）

#### 3. 5項目複合キー管理
- **必須項目**：商品コード、等級コード、階級コード、荷印コード、荷印名（8桁固定）
- **荷印名**：半角8文字（**伝票の153列目「手入力項目」から取得**）
- **コード「000」の扱い**：等級・階級コードの「000」は有効な値として扱う
- **除外処理**：商品コード「00000」は必ず除外する

#### 4. 特殊処理ルール
- **荷印除外条件**：荷印名が「EXIT」「exit」で始まる、または特定の荷印コードは除外フラグを立てる
- **商品分類変更**：荷印名による自動変更ルールを必ず適用する
- **消費税処理**：現金売上（伝票種別52）には自動的に消費税を追加する

#### 5. パフォーマンス目標
- **アンマッチ処理**：3分以内（現在達成済み）
- **在庫計算処理**：5分以内（実装時に考慮）
- **帳票出力**：5分以内（実装時に考慮）

#### 6. 開発時の注意事項
- **マルチ部門対応**：DeptA/B/Cの部門ごとの処理を常に考慮する
- **日付処理**：販売大臣の日付形式（YYYYMMDD）を正しく処理する
- **文字コード**：CSVファイルはShift-JISエンコーディングを想定
- **ゾーンIdentifier**：ダウンロードファイルの:Zone.Identifierファイルは無視する

### 実装済み機能一覧
1. ✅ CSV取込機能（売上・仕入・在庫調整・各種マスタ）
2. ✅ アンマッチリスト処理
3. ✅ データセット管理（DataSetManagement完全移行完了）
4. ✅ 日次終了管理（DailyCloseManagement）
5. ✅ 商品勘定計算処理（ストアドプロシージャsp_CreateProductLedgerData実装）
6. ✅ 商品日報出力（実装済み）
7. ⏳ 在庫表出力（未実装）

### テスト・デバッグ時の確認事項
- `import-folder`コマンド実行時は必ずログを確認
- エラー発生時は`大臣出力ファイル`フォルダの実際のCSVデータを確認
- SQLクエリは`クエリ`フォルダのJSONファイルで実際のテーブル構造を確認

### CSV仕様

#### 荷印名（手入力）の取得方法
- 売上伝票CSV：**155列目**「手入力項目(半角8文字)」（Index=154）
- 仕入伝票CSV：**147列目**「荷印手入力」（Index=146）
- 在庫調整CSV：**153列目**「手入力項目(半角8文字)」（Index=152）
- 前月末在庫CSV：**153列目**「手入力項目(半角8文字)」（Index=152）

**重要**：荷印名は荷印マスタから取得するのではなく、伝票データに直接入力された値を使用する。空白8文字も有効な値として扱う。

#### 荷印名（手入力）の処理詳細
- **桁数**: 8桁固定（必ず8桁に調整）
- **列位置**: 
  - 売上伝票: 155列目（Index=154）
  - 仕入伝票: 147列目（Index=146）
  - 在庫調整: 153列目（Index=152）
  - 前月末在庫: 153列目（Index=152）
- **処理方法**:
  ```csharp
  // C#での実装例
  ShippingMarkName = (HandInputItem ?? "").PadRight(8).Substring(0, 8);
  ```
- **データ例**:
  - 「ﾃﾆ1     」（"ﾃﾆ1"の後に空白5文字）
  - 「ﾃﾆ2     」（"ﾃﾆ2"の後に空白5文字）  
  - 「        」（空白8文字 - 有効な値）

**注意事項**:
- 空白8文字も有効な荷印名として処理する
- 荷印マスタは参照しない（伝票の手入力値をそのまま使用）
- 5項目複合キーの一部として使用されるため、正確な8桁処理が必須
- CSV内の荷印名フィールド（売上141列目等）は使用しない（これは荷印マスタの参照値）

### データ管理仕様

#### 除外データ条件

##### 伝票データの除外条件（読み飛ばし）
以下のコードがセットされている伝票データの行は処理対象外：
- **商品コード**が「00000」（オール0）
- **得意先コード**が「00000」（オール0）
- **仕入先コード**が「00000」（オール0）
- **単位コード**が「0」（在庫調整のみ）

#### 除外データ条件（処理別）

##### 共通除外条件（すべての伝票）
- 商品コード「00000」の行は読み飛ばし
- 得意先コード「00000」の行は読み飛ばし（売上伝票）
- 仕入先コード「00000」の行は読み飛ばし（仕入伝票）

##### アンマッチリスト処理での除外
- 在庫調整で単位コード「02」（ギフト経費）の行は処理しない
- 在庫調整で単位コード「05」（加工費B）の行は処理しない
- 荷印名先頭4文字が「EXIT」「exit」の行は処理しない
- 荷印コード「9900」「9910」「1353」の行は処理しない

##### 在庫集計処理での扱い
- 単位コード「02」「05」は「加工」として集計
- 荷印除外条件は商品日報・在庫表では適用しない

##### コード0の扱い
| マスタ種別 | コード0の扱い | 備考 |
|-----------|-------------|------|
| 商品・得意先・仕入先 | 処理しない（エラー） | 伝票行を読み飛ばし |
| 荷印・等級・階級・産地 | 処理する（正常） | マスタ参照は不要、名称は空白表示 |

**注意**：販売大臣の仕様上、荷印・等級・階級・産地マスタにはコード0は登録できないが、伝票データには0が入力可能。この場合、コード0として処理し、名称表示時は空白とする。

### マスタコード0の特別扱い
- 荷印コード「0000」：マスタ参照不要、名称は空白表示
- 等級コード「000」：マスタ参照不要、名称は空白表示
- 階級コード「000」：マスタ参照不要、名称は空白表示

これらのコード0は正常なデータとして処理するが、画面表示・帳票印字時の名称は空白とする。

### トラブルシューティング

#### 売上伝票インポートエラー（荷印名空）
- **症状**：「不正な売上伝票データ」エラーが大量発生
- **原因**：荷印名が空白8文字の場合をエラーと判定
- **解決方法**：
  ```csharp
  // IsValidSalesVoucherメソッドの修正
  // 変更前：string.IsNullOrWhiteSpace(ShippingMarkName)
  // 変更後：string.IsNullOrEmpty(ShippingMarkName)
  ```

## 📅 JobDate（汎用日付2）の取り扱い仕様

### 基本原則

#### 1. JobDateとは
- **定義**: 販売大臣AXで実際にコンピュータ入力操作を行った日付
- **CSVファイル内の位置**: 「汎用日付2（伝票）」列
- **重要性**: 在庫管理システムのすべての処理の基準日

#### 2. なぜJobDateを使用するのか
- **伝票日付の問題点**:
  - 翌日日付（計上が翌日で納品は本日）の場合がある
  - 昨日の日付の漏れた分を本日計上する場合がある
  - 伝票日付にはバラツキがあり、基準日として信頼できない
- **JobDateの利点**:
  - 実際の入力操作日という確実な基準
  - 日付範囲の絞り込みが正確にできる

### 重要な実装ルール

#### 1. JobDateは不変のデータ
```csharp
// ❌ 絶対にやってはいけない
salesVoucher.JobDate = startDate.Value;  // JobDateの改変は禁止

// ✅ 正しい実装
// JobDateはCSVから読み取った値をそのまま使用
```

#### 2. import-folderコマンドの日付指定
- **単一日付モード** (`import-folder DeptA 2025-06-30`)
  - JobDateが2025-06-30のデータのみを取り込む
  - JobDateの改変は行わない

- **期間指定モード** (`import-folder DeptA 2025-05-31 2025-06-30`)
  - JobDateが指定期間内のデータのみを取り込む
  - JobDateの改変は行わない

- **全期間モード** (`import-folder DeptA`)
  - すべてのデータを取り込む（フィルタリングなし）

#### 3. 在庫マスタとJobDate
- 在庫マスタは**JobDateごと**に在庫を管理
- 在庫マスタ最適化は**実際のJobDate**で実行される
- 複数日のデータを取り込んだ場合、各日付で在庫マスタが作成される

### 年末年始等の特殊処理

#### 複数日を1日として扱う場合の例
```
期間: 12/29～1/4を1日として扱いたい場合

【販売大臣AX側の処理】
- 12/29～1/4のすべての伝票のジョブデートを統一日付（例：1/4）で入力
- CSVエクスポート時に「汎用日付2」で絞り込み

【在庫管理システム側の処理】
dotnet run -- import-folder DeptA 2025-01-04
```

### アンチパターン

#### ❌ やってはいけないこと
1. **JobDateの上書き**: CSVから読み取ったJobDateを別の日付で上書き
2. **伝票日付での処理**: 伝票日付を基準にした在庫計算
3. **preserveCsvDates=false**: このオプションは廃止予定（仕様違反）

#### ✅ 正しい実装
1. **JobDateの保持**: CSVの「汎用日付2」の値を常に保持
2. **JobDateでの処理**: すべての在庫計算・集計はJobDate基準
3. **日付フィルタリング**: 取込時の日付指定は単なるフィルタ条件

### 開発時の注意事項

1. **CSV取込時**
   - 「汎用日付2」列を確実に読み取る
   - 日付形式の変換処理を実装（YYYYMMDD → DateTime）
   - JobDateがない伝票はエラーとして扱う

2. **データ処理時**
   - すべての集計・計算はJobDateを基準に行う
   - 伝票日付（VoucherDate）は参考情報としてのみ使用

3. **帳票出力時**
   - 処理対象日はJobDateを表示
   - 伝票日付も参考として表示可能

### 関連ドキュメント
- 「CW売上伝票入力キャプチャ.pdf」- 販売大臣AXの画面と日付管理の説明
- 「開発資料/CW仕入売上伝票入力キャプチャ.md」- 詳細な日付管理仕様

## 📑 CSVマッピング仕様（販売大臣CSV）

### 重要な列位置情報

#### 1. 売上伝票CSV（171列）
| 項目 | 列番号 | Index | 説明 |
|------|--------|-------|------|
| 伝票日付 | 1 | 0 | YYYYMMDD形式 |
| 伝票区分 | 2 | 1 | 51:掛売、52:現売 |
| 伝票番号 | 3 | 2 | 自動採番 |
| 得意先コード | 8 | 7 | 5桁（00000は除外） |
| ジョブデート | 49 | 48 | システム処理日 |
| 商品コード | **91** | **90** | 5桁（00000は除外） |
| 等級コード | **85** | **84** | 3桁 |
| 階級コード | **86** | **85** | 3桁 |
| 荷印コード | **87** | **86** | 4桁 |
| 数量 | **96** | **95** | 小数対応 |
| 単価 | **98** | **97** | 小数対応 |
| 金額 | **99** | **98** | 小数対応 |
| 商品名 | **148** | **147** | 商品名称 |
| 手入力項目 | **155** | **154** | 荷印名（8桁固定） |

#### 2. 仕入伝票CSV（171列）
| 項目 | 列番号 | Index | 説明 |
|------|--------|-------|------|
| 伝票日付 | 1 | 0 | YYYYMMDD形式 |
| 伝票区分 | 2 | 1 | 11:掛仕入、12:現金仕入 |
| 伝票番号 | 3 | 2 | 自動採番 |
| 仕入先コード | 7 | 6 | 5桁（00000は除外） |
| ジョブデート | 44 | 43 | システム処理日 |
| 商品コード | **87** | **86** | 5桁（00000は除外） |
| 等級コード | **81** | **80** | 3桁 |
| 階級コード | **82** | **81** | 3桁 |
| 荷印コード | **83** | **82** | 4桁 |
| 数量 | **92** | **91** | 小数対応 |
| 単価 | **94** | **93** | 小数対応 |
| 金額 | **95** | **94** | 小数対応 |
| 商品名 | **140** | **139** | 商品名称 |
| 荷印手入力 | **147** | **146** | 荷印名（8桁固定） |

#### 3. 在庫調整CSV（171列、受注伝票形式）
| 項目 | 列番号 | Index | 説明 |
|------|--------|-------|------|
| 伝票日付 | 1 | 0 | YYYYMMDD形式 |
| 伝票区分 | 2 | 1 | 71:在庫調整 |
| 伝票番号 | 3 | 2 | 自動採番 |
| ジョブデート | 48 | 47 | システム処理日 |
| 商品コード | **91** | **90** | 5桁（00000は除外） |
| 等級コード | **85** | **84** | 3桁 |
| 階級コード | **86** | **85** | 3桁 |
| 荷印コード | **87** | **86** | 4桁 |
| 区分 | **96** | **95** | 1:ﾛｽ、4:振替、6:調整 |
| 単価 | **98** | **97** | 小数対応 |
| 金額 | **99** | **98** | 小数対応 |
| 商品名 | **146** | **145** | 商品名称 |
| 手入力項目 | **153** | **152** | 荷印名（8桁固定） |

#### 4. 前月末在庫CSV（161列）
| 項目 | 列番号 | Index | 説明 |
|------|--------|-------|------|
| 伝票日付 | 1 | 0 | YYYYMMDD形式 |
| 伝票区分 | 2 | 1 | 71:在庫調整 |
| 伝票番号 | 3 | 2 | 自動採番 |
| ジョブデート | 48 | 47 | システム処理日 |
| 商品コード | **90** | **89** | 5桁（00000は除外） |
| 等級コード | **84** | **83** | 3桁 |
| 階級コード | **85** | **84** | 3桁 |
| 荷印コード | **86** | **85** | 4桁 |
| 数量 | **95** | **94** | 小数対応 |
| 区分 | **96** | **95** | 1:ﾛｽ、4:振替、6:調整 |
| 単価 | **97** | **96** | 小数対応 |
| 金額 | **98** | **97** | 小数対応 |
| 手入力項目 | **153** | **152** | 荷印名（8桁固定） |

### CSVインポート時の注意事項

1. **文字コード**: UTF-8 with BOM
2. **ヘッダー行**: あり（1行目）
3. **列数チェック**: 必ず規定の列数を確認
4. **数値フォーマット**: ロケール非依存（InvariantCulture）で処理
5. **日付フォーマット**: YYYYMMDD形式（8桁）

### 5項目複合キーの構築
```csharp
// すべての伝票共通
var key = new InventoryKey
{
    ProductCode = productCode.PadLeft(5, '0'),      // 5桁左0埋め
    GradeCode = gradeCode.PadLeft(3, '0'),          // 3桁左0埋め
    ClassCode = classCode.PadLeft(3, '0'),          // 3桁左0埋め
    ShippingMarkCode = shippingMarkCode.PadLeft(4, '0'), // 4桁左0埋め
    ShippingMarkName = (handInputItem ?? "").PadRight(8).Substring(0, 8) // 8桁固定
};
```

## 📄 FastReportの使用方針

### 基本方針
- **FastReportのスクリプト機能は使用しない**
- **すべての計算・制御ロジックはC#プログラム側で実装する**
- **FastReportは純粋に表のレイアウトとテンプレートとしてのみ使用する**

### 実装ガイドライン

#### 1. テンプレートファイル（.frx）の設定
- `ScriptLanguage="None"`を必ず指定
- `<ScriptText>`セクションは削除または空にする
- イベントハンドラー（例：`BeforePrintEvent`）は使用しない

#### 2. データの準備
- すべてのデータ加工・計算はC#側で完結させる
- DataTableまたはコレクションとして完成されたデータを渡す
- 条件分岐やフィルタリングもC#側で実装

#### 3. 動的な表示制御
- 0件時のヘッダー非表示などの制御はC#コードで実装
- `report.FindObject()`を使用してオブジェクトを取得
- `Visible`プロパティなどを直接操作

#### 実装例
```csharp
// アンマッチリスト0件時のヘッダー制御
if (dataCount == 0)
{
    var pageHeader = report.FindObject("PageHeader1") as FR.PageHeaderBand;
    if (pageHeader != null)
    {
        // ヘッダーオブジェクトを非表示
        for (int i = 1; i <= 18; i++)
        {
            var header = report.FindObject($"Header{i}") as FR.TextObject;
            if (header != null)
            {
                header.Visible = false;
            }
        }
    }
}
```

### メリット
- .NET 8環境でのスクリプトエラーを回避
- デバッグが容易（C#側でブレークポイント設定可能）
- 単体テストの実装が可能
- コードの保守性向上

## 日次終了処理と誤操作防止機能

### 概要
日次終了処理は在庫マスタの原本を直接更新する重要な処理です。オペレータのミスによる誤ったデータ更新を防ぐため、複数の安全機構を実装しています。

### 背景となる課題
クライアントから報告された問題シナリオ：
- 14:10に正しいデータで商品日報を作成
- その後、オペレータが誤って翌日分のCSVをエクスポート
- 15:10の日次終了処理で誤ったデータが在庫マスタに反映される危険性

### 実装された誤操作防止機能

#### 1. データセット管理による紐付け
- CSV取込時に一意の`DataSetId`を付与
- 商品日報作成時の`DataSetId`を記録
- 日次終了処理は商品日報と同じ`DataSetId`でのみ実行可能

#### 2. 時間的制約
- **処理可能時間**: 15:00以降のみ
- **商品日報からの経過時間**: 最低30分
- **CSV取込からの経過時間**: 最低5分

#### 3. データ整合性チェック
- SHA256ハッシュによるデータ不変性の確認
- 商品日報作成時と日次終了時のデータを比較
- 変更が検出された場合は処理を中断

#### 4. 処理前確認機能
```bash
# 日次終了処理の事前確認コマンド
dotnet run -- check-daily-close 2025-06-30
```
表示される情報：
- 対象日付と商品日報情報
- データ件数・金額サマリー
- 検証結果（エラー/警告/情報）

### 処理フロー
```
1. CSV取込（DataSetId: ABC123）
   ↓
2. 商品日報作成（DataSetId: ABC123を記録）
   ↓ （最低30分経過）
3. 日次終了処理前確認
   ↓
4. 日次終了処理実行（DataSetId: ABC123のみ処理可能）
```

### 関連ファイル
- **サービス**: `src/InventorySystem.Core/Services/DailyCloseService.cs`
- **エンティティ**: `src/InventorySystem.Core/Entities/DailyCloseManagement.cs`
- **テーブル**: `DailyCloseManagement`, `ProcessHistory`, `AuditLogs`

### 重要な実装詳細

#### データハッシュの計算方法
```csharp
// 売上、仕入、在庫調整のデータを含めてSHA256でハッシュ化
// フォーマット: "種別:伝票ID,商品コード,数量,金額"
```

#### エラーメッセージ
- `"日次終了処理は15:00以降にのみ実行可能です。"`
- `"商品日報作成から30分以上経過する必要があります。"`
- `"データが商品日報作成時から変更されています。"`

### データベース構造

#### DailyCloseManagementテーブル
| カラム名 | 型 | 説明 |
|---------|-----|-----|
| JobDate | DATE | 処理対象日 |
| DatasetId | NVARCHAR(100) | データセットID |
| DailyReportDatasetId | NVARCHAR(100) | 商品日報のDatasetId |
| DataHash | NVARCHAR(100) | データハッシュ値 |
| ValidationStatus | NVARCHAR(20) | 検証結果 |
| ProcessedAt | DATETIME2 | 処理日時 |
| ProcessedBy | NVARCHAR(100) | 処理実行者 |

### トラブルシューティング

#### 「データが変更されています」エラー
1. 最新のCSVを再取込
2. 商品日報を再作成
3. 30分待機後、日次終了処理を実行

#### 時間制約エラー
- 指定された時刻まで待機
- または管理者権限で強制実行（非推奨）

### 運用上の注意点
1. **日次終了処理は1日1回のみ**実行（同一DataSetIdでの重複実行は不可）
2. **バックアップは自動作成**されるが、30日で自動削除
3. **監査ログ**ですべての操作履歴を追跡可能
4. **ロールバック**が必要な場合は、バックアップパスから復元

### 開発時の注意
- 日次終了処理の修正時は必ず`ValidateDataIntegrity`メソッドも確認
- 新しい検証ルールを追加する場合は`DataValidationResult`クラスを使用
- エラーメッセージは`ErrorMessages`定数クラスに定義

## 📋 DailyCloseManagement完全実装仕様（2025-07-19）

### 概要
DailyCloseManagementは日次終了処理の状態管理とデータ整合性を保証する重要な機能です。Gemini CLI連携により、業界のベストプラクティスを取り入れた設計になっています。

### 実装された主要機能

#### 1. ValidationStatus状態遷移管理
```
PENDING → PROCESSING → VALIDATING → PASSED/FAILED
```
- **PENDING**: 初期状態（データセット作成時）
- **PROCESSING**: 処理開始時
- **VALIDATING**: 検証実行中
- **PASSED**: 検証成功・処理完了
- **FAILED**: 検証失敗・エラー発生

#### 2. 冪等性保証
- 同一JobDateでの重複実行を防止
- SqlException 2627（ユニーク制約違反）をDuplicateDailyCloseExceptionに変換
- エラー状態での適切なロールバック処理

#### 3. エンティティ拡張機能
```csharp
// タイムスタンプ付きログ機能
dailyClose.AppendRemark("データ検証完了");

// パフォーマンス追跡機能
dailyClose.AppendPerformanceInfo(startTime, "追加情報");
```

#### 4. Repository実装
- **CreateAsync**: 新規作成（デフォルト値設定）
- **GetByJobDateAsync**: JobDate検索
- **GetLatestAsync**: 最新レコード取得
- **UpdateStatusAsync**: 状態更新（SQL Server側でタイムスタンプ管理）

#### 5. カスタム例外処理
```csharp
public class DuplicateDailyCloseException : Exception
{
    public DateTime JobDate { get; }
    public string? ExistingStatus { get; }
}
```

### テーブル構造（038_RecreateDailyCloseManagementIdealStructure.sql）

| カラム名 | 型 | 制約 | 説明 |
|---------|-----|------|------|
| Id | INT IDENTITY | PK | 主キー |
| JobDate | DATE | NOT NULL, UNIQUE | 処理対象日（一意） |
| DataSetId | NVARCHAR(100) | NOT NULL | データセットID |
| DailyReportDataSetId | NVARCHAR(100) | NULL | 商品日報DataSetId |
| BackupPath | NVARCHAR(500) | NULL | バックアップパス |
| ProcessedAt | DATETIME2(7) | DEFAULT GETDATE() | 処理日時 |
| ProcessedBy | NVARCHAR(100) | DEFAULT SYSTEM_USER | 処理実行者 |
| DataHash | NVARCHAR(100) | NULL | データハッシュ値 |
| ValidationStatus | NVARCHAR(20) | DEFAULT 'PENDING' | 検証ステータス |
| Remarks | NVARCHAR(MAX) | NULL | 備考（ログ情報） |

### 実装ファイル一覧

#### 核心実装
- `/database/migrations/038_RecreateDailyCloseManagementIdealStructure.sql`
- `/src/InventorySystem.Core/Entities/DailyCloseManagement.cs`
- `/src/InventorySystem.Data/Repositories/DailyCloseManagementRepository.cs`
- `/src/InventorySystem.Core/Interfaces/IDailyCloseManagementRepository.cs`

#### エラーハンドリング
- `/src/InventorySystem.Core/Exceptions/DuplicateDailyCloseException.cs`

#### サービス層
- `/src/InventorySystem.Core/Services/DailyCloseService.cs`（状態遷移ロジック）

### Gemini CLI連携で適用されたベストプラクティス

#### 1. テーブル設計原則
- 単一責任の原則（SRP）
- データ整合性の保証（UNIQUE制約）
- 監査証跡の実装（ProcessedAt, ProcessedBy）

#### 2. エラーハンドリング戦略
- SQL例外の業務例外への変換
- 詳細なログ記録
- 適切なエラーメッセージ

#### 3. パフォーマンス最適化
- インデックス戦略（JobDate UNIQUE）
- デフォルト値のDB側設定
- 効率的なクエリ設計

#### 4. セキュリティ考慮
- SQLインジェクション対策（Dapper使用）
- 入力値検証
- 権限管理（ProcessedBy記録）

### 運用上の重要なルール

#### 1. 状態遷移の厳格管理
```csharp
// ❌ 禁止：直接的な状態変更
dailyClose.ValidationStatus = "PASSED";

// ✅ 推奨：Repository経由の状態更新
await _repository.UpdateStatusAsync(id, "PASSED", "検証完了");
```

#### 2. エラー時の適切な処理
- FAILED状態での詳細情報記録
- ロールバック時の状態復旧
- 運用チームへの通知

#### 3. データ整合性の保証
- JobDate単位での一意性保証
- 関連データとの整合性チェック
- 定期的な整合性検証

### 今後の拡張予定

#### 1. 通知機能
- 処理完了/エラー時のメール通知
- Slack連携
- ダッシュボード表示

#### 2. レポート機能
- 処理履歴レポート
- パフォーマンス分析
- エラー傾向分析

#### 3. 自動化機能
- スケジュール実行
- 自動リトライ
- 自動バックアップ

### 注意事項とトラブルシューティング

#### よくある問題
1. **重複実行エラー**: 同一JobDateで複数回実行
   - 解決：既存レコードの状態確認後、適切な処理判断
   
2. **状態不整合**: 処理中断による不正な状態
   - 解決：手動での状態リセット、ログ確認

3. **パフォーマンス問題**: 大量データ処理時の応答遅延
   - 解決：バッチサイズ調整、インデックス最適化

#### デバッグ支援
- Remarks列での詳細ログ確認
- ProcessedAt, ProcessedByでの操作履歴追跡
- ValidationStatusでの処理状況把握

## 🚨 init-database --force 実行時の重要な注意事項

### 024_CreateProductMaster.sqlの除外設定

**状況**: 2025-07-18にSQLエラー207の解決のため、フェーズド・マイグレーション（migrate-phase2/3/5）を実装しました。これにより、テーブルスキーマが`CreatedDate/UpdatedDate`から`CreatedAt/UpdatedAt`に移行されます。

**問題**: `init-database --force`コマンドは`DatabaseInitializationService.cs`の`_migrationOrder`リストに基づいてマイグレーションを実行しますが、024_CreateProductMaster.sqlは古いスキーマ（`CreatedDate/UpdatedDate`）を前提としているため、移行済み環境では競合が発生します。

**解決策**: 024_CreateProductMaster.sqlを`_migrationOrder`からコメントアウトして除外

```csharp
// "024_CreateProductMaster.sql",              // 除外: migrate-phase3/5との競合回避のため
                                                // このスクリプトはCreatedDate/UpdatedDateスキーマを前提とするが
                                                // 移行後はCreatedAt/UpdatedAtスキーマになるため除外
```

### 影響と対策

#### ✅ 保護される内容
- `migrate-phase3`で追加された`CreatedAt/UpdatedAt`カラム
- `migrate-phase5`で削除された古い`CreatedDate/UpdatedDate`カラム
- エンティティクラスとデータベーススキーマの整合性

#### ⚠️ 注意事項
- 024_CreateProductMaster.sqlで投入される初期商品データは、`init-database --force`では作成されません
- 必要に応じて、移行後に別途商品マスタのインポートを実行してください

#### 🔄 推奨運用手順
1. `init-database --force` でクリーンなデータベースを作成
2. `migrate-phase2` で新カラムを追加
3. `migrate-phase3` で既存データを移行し同期トリガーを設定
4. `migrate-phase5` で古いカラムをクリーンアップ
5. 必要に応じて商品マスタの個別インポートを実行

この設定により、`init-database --force`と手動移行作業の両方が安全に実行できます。

## 📊 DataSets/DataSetManagement二重管理問題の完全解決（2025-07-22）

### 背景
システム起動時に「🔄 DataSets/DataSetManagement二重管理モード」で動作し、これがストアドプロシージャ未作成問題の根本原因となっていました。

### 解決内容

#### 1. appsettings.json設定整理
```json
// 変更前
"Features": {
    "UseDataSetManagementOnly": false,
    "EnableDataSetsMigrationLog": true
}

// 変更後（設定自体を簡素化）
"Features": {
    "Reserved": false
}
```

#### 2. Program.cs二重管理モード削除
```csharp
// 変更前：条件分岐による二重管理
if (features.UseDataSetManagementOnly) {
    builder.Services.AddScoped<IDataSetService, DataSetManagementService>();
} else {
    builder.Services.AddScoped<IUnifiedDataSetService, UnifiedDataSetService>();
    builder.Services.AddScoped<IDataSetService, LegacyDataSetService>();
}

// 変更後：DataSetManagement専用
builder.Services.AddScoped<IDataSetService, DataSetManagementService>();
Console.WriteLine("🔄 DataSetManagement専用モードで起動");
```

#### 3. 不要サービス完全削除
- `LegacyDataSetService.cs` 削除
- `UnifiedDataSetService.cs` 削除
- `IUnifiedDataSetService.cs` 削除
- 全ファイル（20+）でIUnifiedDataSetService → IDataSetServiceに統一

#### 4. DataSetsテーブル削除対応
- `999_DropDataSetsTable.sql` スクリプト作成
- DatabaseInitializationServiceに自動実行登録
- 主要コマンド（import-folder、unmatch-list、daily-report）のDataSets依存確認完了

### 効果
- ✅ システム起動：「DataSetManagement専用モード」で起動
- ✅ ストアドプロシージャsp_CreateProductLedgerDataの正常作成・実行
- ✅ 商品勘定帳票の正常動作
- ✅ データ整合性の向上

## 📈 商品勘定帳票機能実装（2025-07-22）

### 実装内容
- **コマンド**: `product-account <日付>`
- **ストアドプロシージャ**: `sp_CreateProductLedgerData`
- **FastReportテンプレート**: `ProductAccount.frx`
- **レイアウト**: A3横向き（420mm × 297mm）、1512px幅

### 主要機能
#### 1. CPInventoryMaster一時テーブル作成
- 前日残高と当日残高データの管理
- 5項目複合キー（商品・等級・階級・荷印コード・荷印名）での集計

#### 2. 取引明細データ統合
- 前残高レコード
- 売上伝票（掛売・現売）
- 仕入伝票（掛仕入・現金仕入）
- 在庫調整（ロス・振替・調整）

#### 3. 移動平均法計算
- 0除算対策付きの在庫単価計算
- ウィンドウ関数による累積残高計算
- 粗利益・粗利率の自動計算

#### 4. FastReportテンプレート詳細設計
```
列幅設定（総幅1512px）:
商品名:150px, 荷印名:144px, 手入力:86px, 等級:75px, 階級:75px,
伝票NO:91px, 区分:59px, 月日:64px, 仕入数量:96px, 売上数量:96px,
残数量:96px, 単価:96px, 金額:118px, 粗利益:102px, 取引先名:164px
```

### 使用方法
```bash
# 商品勘定帳票生成
dotnet run -- product-account 2025-06-30

# 部門指定での生成
dotnet run -- product-account 2025-06-30 DeptA
```

## 📌 import-folderコマンドの処理ルール

### 1. 基本仕様
- **目的**: 指定フォルダ内のすべてのCSVファイルを適切な順序で一括インポート
- **構文**: `dotnet run -- import-folder <部門名> <ジョブ日付>`
- **対象フォルダ**: `D:\InventoryImport\<部門名>\Import\`

### 2. ファイル処理順序（重要）
```
1. マスタ系ファイル（必ず最初）
2. 前月末在庫（マスタの後、伝票の前）
3. 伝票系ファイル（最後）
```

### 3. ファイル認識パターン

#### マスタ系（優先度1）
- `*等級汎用マスター*.csv` → Contains("等級汎用マスター")
- `*階級汎用マスター*.csv` → Contains("階級汎用マスター")
- `*荷印汎用マスター*.csv` → Contains("荷印汎用マスター")
- `*産地汎用マスター*.csv` → Contains("産地汎用マスター")
- `商品.csv` → FileName == "商品.csv"
- `得意先.csv` → FileName == "得意先.csv"
- `仕入先.csv` → FileName == "仕入先.csv"

**注意**: ファイル名に全角数字（１、２、３、４）が含まれるため、Contains判定を使用

#### 初期在庫（優先度2）
- `前月末在庫.csv` → FileName == "前月末在庫.csv"

#### 伝票系（優先度3）
- `売上伝票*.csv` → StartsWith("売上伝票")
- `仕入伝票*.csv` → StartsWith("仕入伝票")
- `在庫調整*.csv` → StartsWith("在庫調整")
- `受注伝票*.csv` → StartsWith("受注伝票") ※在庫調整として処理

### 4. 処理実装の注意点

#### 4.1 マスタ処理の分岐
```csharp
// 等級・階級マスタ：リポジトリ直接利用（現状）
await _gradeMasterRepository.BulkDeleteAsync();
await _gradeMasterRepository.BulkInsertAsync(gradeMasters);

// 商品・得意先・仕入先：サービス経由
await _productMasterImportService.ImportAsync(filePath);

// 荷印・産地：未実装時は警告してスキップ
_logger.LogWarning("荷印マスタインポートサービスが未実装のためスキップ");
```

#### 4.2 エラーハンドリング
- エラーファイルは `D:\InventoryImport\<部門名>\Error\` へ移動
- エラーが発生しても他のファイル処理は継続
- エラーログファイル（.error.txt）も生成

#### 4.3 アンマッチリスト処理
- **重要**: import-folderコマンドからアンマッチリスト自動実行は削除済み
- 必要な場合は別途 `create-unmatch-list` コマンドを実行

### 5. 実装時の必須確認事項

#### 5.1 前提条件
- [ ] PreviousMonthInventoryテーブルが存在すること
- [ ] 各種マスタテーブルが作成済みであること
- [ ] UTF-8 with BOMのCSVファイルに対応していること

#### 5.2 処理順序の保証
- [ ] GetFileProcessOrderメソッドで処理順序を制御
- [ ] マスタ→初期在庫→伝票の順序を厳守

#### 5.3 データ整合性
- [ ] マスタデータは全削除→新規登録（トランザクション使用）
- [ ] 伝票データは重複チェック後に追加
- [ ] JobDateをすべての伝票データに設定

### 6. 既知の問題と対処

#### 6.1 マスタ未登録データ
- 等級コード「000」：マスタ未登録だが72件使用中
- 荷印コード「8001」：新規荷印で88件使用中
- 対処：インポート後にアンマッチリストで確認

#### 6.2 未実装サービス
- IShippingMarkMasterImportService（荷印）
- IRegionMasterImportService（産地）
- 対処：警告表示してスキップ、将来実装予定

### 7. テスト用コマンド例

```bash
# 基本的な実行
dotnet run -- import-folder DeptA 2025-06-27

# 実行後の確認
dotnet run -- create-unmatch-list 2025-06-27

# データ確認SQL
SELECT COUNT(*) as マスタ件数, 'GradeMaster' as テーブル名 FROM GradeMaster
UNION ALL SELECT COUNT(*), 'ClassMaster' FROM ClassMaster
UNION ALL SELECT COUNT(*), 'ProductMaster' FROM ProductMaster;
```

### 8. ログ出力形式

```
=== CSVファイル一括インポート開始 ===
[1/15] 処理中: 等級汎用マスター１.csv
✅ 等級マスタをインポートしました（218件）
⚠️ 荷印マスタインポートサービスが未実装のためスキップ
❌ エラー: ファイル処理に失敗しました
=== インポート完了 ===
成功: 12件、スキップ: 2件、エラー: 1件
```

### 9. 禁止事項
- ❌ 処理順序を無視したインポート
- ❌ アンマッチリストの自動実行（削除済み）
- ❌ エラー時の処理中断（継続すること）
- ❌ マスタデータの追加のみ更新（全削除→新規登録を使用）

---

**最終更新**: 2025-07-01  
**対象バージョン**: InventoryManagementSystem v2.0

## FastReport必須仕様（最重要）

### FastReportは絶対に削除・置換禁止

このプロジェクトでは、FastReport.NETが**必須のコンポーネント**です。以下のルールを厳守してください：

#### 1. FastReportサービスの保護
- `UnmatchListFastReportService.cs`と`DailyReportFastReportService.cs`は**絶対に削除しない**
- PlaceholderサービスはLinux開発環境用の一時的な代替であり、本番環境では使用しない
- Program.csのDI登録で、Windows環境では必ずFastReportサービスを使用する

#### 2. ビルドエラー時の対応
ビルドエラーが発生しても、以下の対応は**絶対に行わない**：
- ❌ FastReportサービスの削除
- ❌ PlaceholderサービスへのDI登録の変更
- ❌ FastReport関連のusingディレクティブの削除
- ❌ #if WINDOWS ブロックの削除

**正しい対応方法**：
- ✅ 条件付きコンパイル（#if WINDOWS）で囲む
- ✅ Linux環境では Placeholder サービスを使用
- ✅ Windows環境では必ず FastReport サービスを使用

#### 3. FastReport DLL管理
```xml
<!-- InventorySystem.Reports.csproj で必須 -->
<ItemGroup Condition="'$(OS)' == 'Windows_NT'">
    <Reference Include="FastReport">
        <HintPath>C:\Program Files (x86)\FastReports\FastReport .NET Trial\FastReport.dll</HintPath>
        <Private>true</Private>
    </Reference>
</ItemGroup>
```

#### 4. エラー処理の原則
FastReport関連でエラーが発生した場合：
1. **削除や置換ではなく、条件付きコンパイルで対応**
2. **エラーメッセージを明確に記録**
3. **FastReportが必須である旨をコメントで明記**

#### 5. コード例（必須パターン）
```csharp
// Program.cs の DI登録（このパターンを維持）
#if WINDOWS
    builder.Services.AddScoped<IUnmatchListReportService, UnmatchListFastReportService>();
    builder.Services.AddScoped<IDailyReportService, DailyReportFastReportService>();
#else
    builder.Services.AddScoped<IUnmatchListReportService, PlaceholderUnmatchListReportService>();
    builder.Services.AddScoped<IDailyReportService, PlaceholderDailyReportService>();
#endif
```

#### 6. 禁止事項の明示
以下のような変更は**いかなる理由があっても禁止**：
```csharp
// ❌ 絶対にこのような変更をしない
builder.Services.AddScoped<IUnmatchListReportService, PlaceholderUnmatchListReportService>();
// ❌ #if WINDOWS を削除しない
// ❌ FastReportサービスクラスを削除しない
```

### FastReport関連ファイルの保護リスト
以下のファイルは**削除・大幅変更禁止**：
- `/src/InventorySystem.Reports/FastReport/Services/UnmatchListFastReportService.cs`
- `/src/InventorySystem.Reports/FastReport/Services/DailyReportFastReportService.cs`
- `/src/InventorySystem.Reports/FastReport/Services/FastReportService.cs`
- `/src/InventorySystem.Reports/FastReport/Templates/*.frx`

**理由**: FastReportはこのシステムの中核機能であり、PDF帳票生成の唯一の本番実装です。

## 🔧 重要な開発ルール更新（2025-07-26）

### アンマッチリスト仕様変更（最重要）
- **在庫0エラー**: 完全削除（マイナス在庫許容）
- **出荷系データのみチェック**: 売上（数量>0）、仕入返品（数量<0）、在庫調整（数量>0）
- **エラー種別**: 「在庫マスタ無」のみに統一
- **入荷データ除外**: 新商品の入荷はチェック対象外

### DataSetManagement専用システム
- **DataSetsテーブル**: 完全廃止済み（使用禁止）
- **DataSetManagementテーブル**: 全データセット管理の単一テーブル
- **サービス**: DataSetManagementServiceのみ使用
- **二重管理モード**: 完全削除済み（過去の仕様）

### 商品勘定帳票開発時の注意
- **ストアドプロシージャ**: sp_CreateProductLedgerDataが必須
- **データ準備**: CPInventoryMasterの一時テーブル作成が前提
- **計算ロジック**: 移動平均法による在庫単価計算をストアドプロシージャ内で実行
- **FastReport**: ScriptLanguage="None"厳守、すべての計算はC#側で完結

### エラー対応時の調査手順
1. **起動モード確認**: "DataSetManagement専用モード"で起動しているか
2. **ストアドプロシージャ確認**: sp_CreateProductLedgerDataが存在するか
3. **データベース接続**: 同一データベース・同一接続文字列を使用しているか
4. **Gemini CLI活用**: 複雑な問題はGemini CLIと相談しながら解決

## 🤝 Gemini CLI 連携ガイド

### 目的
ユーザーが **「Geminiと相談しながら進めて」** （または同義語）と指示した場合、Claude は以降のタスクを **Gemini CLI** と協調しながら進める。
Gemini から得た回答はそのまま提示し、Claude 自身の解説・統合も付け加えることで、両エージェントの知見を融合する。

### トリガー
- 正規表現: `/Gemini.*相談しながら/`
- 例:
  - 「Geminiと相談しながら進めて」
  - 「この件、Geminiと話しつつやりましょう」
  - 「Geminiの意見も聞きながら実装して」

### 基本フロー

#### 1. PROMPT 生成
Claude はユーザーの要件を 1 つのテキストにまとめ、環境変数 `$PROMPT` に格納する。

#### 2. Gemini CLI 呼び出し
```bash
# Gemini CLIを直接実行する方法
export PROMPT="ここに質問や要件を記載"
gemini <<EOF
$PROMPT
EOF
```

#### 3. 結果の統合
1. Gemini の回答を**そのまま**ユーザーに提示
2. Claude が補足説明や実装の詳細を追加
3. 両方の見解を統合した最終的な解決策を提示

### 実装例

#### 例1: アーキテクチャ設計の相談
```bash
# ユーザー: 「新しいキャッシュ機構の設計をGeminiと相談しながら進めて」

# Claude の処理:
export PROMPT="Redis を使った分散キャッシュシステムの設計について、以下の要件で最適なアーキテクチャを提案してください：
- 高可用性（99.99%）
- 秒間10万リクエスト対応
- データ整合性の保証
- 自動フェイルオーバー"

gemini <<EOF
$PROMPT
EOF
```

#### 例2: 技術選定の相談
```bash
# ユーザー: 「GraphQL vs REST APIの選択をGeminiと話しつつ決めましょう」

# Claude の処理:
export PROMPT="マイクロサービス環境でのAPI設計において、GraphQLとREST APIの選択基準と、それぞれのメリット・デメリットを比較してください。特に以下の観点から：
- パフォーマンス
- 開発効率
- クライアント側の柔軟性
- キャッシング戦略"

gemini <<EOF
$PROMPT
EOF
```

### 統合時の注意事項

1. **Gemini の回答は改変しない**
   - 原文のまま提示する
   - 引用として明確に区別する

2. **相互補完的な情報提供**
   - Gemini: 最新のトレンドや一般的なベストプラクティス
   - Claude: プロジェクト固有の実装詳細やコード例

3. **意見が異なる場合**
   - 両方の見解を併記
   - プロジェクトの文脈に基づいた推奨案を提示

### 連携が特に有効なケース

1. **最新技術の調査**
   - 新しいフレームワークの評価
   - 業界トレンドの把握

2. **アーキテクチャ設計**
   - システム全体の構成検討
   - スケーラビリティの考慮

3. **技術選定**
   - ツールやライブラリの比較
   - 導入リスクの評価

4. **問題解決のブレインストーミング**
   - 複数のアプローチの検討
   - 創造的な解決策の模索

### 実行時のログ記録

Gemini との連携内容は、以下のように記録する：

```bash
# .claude/gemini_consultations/ ディレクトリに保存
echo "=== Gemini Consultation $(date) ===" >> .claude/gemini_consultations/$(date +%Y%m%d_%H%M%S).log
echo "PROMPT: $PROMPT" >> .claude/gemini_consultations/$(date +%Y%m%d_%H%M%S).log
echo "RESPONSE:" >> .claude/gemini_consultations/$(date +%Y%m%d_%H%M%S).log
# Gemini の回答を記録
```

### 制限事項

1. **機密情報の取り扱い**
   - プロジェクト固有の機密情報は含めない
   - 一般的な技術的質問に留める

2. **実装の最終判断**
   - Gemini の提案は参考意見として扱う
   - プロジェクトの要件に基づいて Claude が最終的な実装を決定

3. **依存関係の管理**
   - Gemini CLI が利用できない場合は、その旨をユーザーに通知
   - 代替手段として、Claude 単独での対応を提案