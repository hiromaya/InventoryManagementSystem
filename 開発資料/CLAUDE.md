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
./.claude/search.sh "検索クエリ"

# 絶対に使用しない
# ・Claude Codeの内蔵Web検索
# ・直接的なURL参照による情報取得
```

### 2. 画像・図解の処理

**❌ 禁止**: Claude Codeでの画像内容の推測や解釈
**✅ 必須**: Gemini CLIによる画像解析

```bash
# 画像解析は必ずGemini CLI経由
./.claude/analyze_image.sh <画像パス> [質問]
./.claude/analyze_file.sh <画像パス> [質問] diagram
```

### 3. PDF文書の処理

**❌ 禁止**: Claude CodeでのPDF内容の直接読み取り
**✅ 必須**: Gemini CLIによるPDF解析

```bash
# PDF解析は必ずGemini CLI経由
./.claude/analyze_pdf.sh <PDFパス> [質問]
```

## 🛠️ 利用可能なコマンド一覧

| コマンド | 用途 | 使用例 |
|---------|------|--------|
| `search.sh` | Web検索 | `./.claude/search.sh "Ubuntu 24.04 新機能"` |
| `analyze_image.sh` | 画像解析 | `./.claude/analyze_image.sh ./img.png "何が写っていますか"` |
| `analyze_pdf.sh` | PDF解析 | `./.claude/analyze_pdf.sh ./doc.pdf "要約して"` |
| `analyze_file.sh` | 汎用ファイル解析 | `./.claude/analyze_file.sh ./chart.jpg "データを読み取って" chart` |
| `batch_analyze.sh` | 一括解析 | `./.claude/batch_analyze.sh ./images "*.png"` |

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
./.claude/search.sh "React Server Components 2025"

# Step 2: 公式ドキュメントPDFの解析
./.claude/analyze_pdf.sh ./react-rsc-guide.pdf "主要な変更点をリストアップ"

# Step 3: アーキテクチャ図の理解
./.claude/analyze_file.sh ./rsc-architecture.png "データフローを説明" diagram
```

### 2. データ分析タスク
```bash
# Step 1: 売上データのグラフ解析
./.claude/analyze_file.sh ./sales-chart.jpg "トレンドと異常値を特定" chart

# Step 2: レポートPDFから詳細情報抽出
./.claude/analyze_pdf.sh ./quarterly-report.pdf "地域別の売上を抽出"

# Step 3: 競合情報の検索
./.claude/search.sh "業界平均成長率 2025"
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
./.claude/analyze_image.sh ./screenshot.png

# ✅ 良い例：具体的な質問
./.claude/analyze_image.sh ./screenshot.png "エラーメッセージとその原因を特定してください"
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

---

**注意**: このルールは厳守してください。Claude Codeの制限を補完し、より高品質な成果物を生成するための重要なガイドラインです。


## 🔨 最重要ルール - 新しいルールの追加プロセス

ユーザーから今回限りではなく常に対応が必要だと思われる指示を受けた場合：

1. 「これを標準のルールにしますか？」と質問する
2. YESの回答を得た場合、CLAUDE.mdに追加ルールとして記載する
3. 以降は標準ルールとして常に適用する

このプロセスにより、プロジェクトのルールを継続的に改善していきます。