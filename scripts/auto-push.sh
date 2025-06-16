#!/bin/bash

# 自動コミット・プッシュスクリプト
# 使用方法: ./scripts/auto-push.sh "コミットメッセージ"

set -e  # エラー時に停止

# 引数チェック
if [ $# -eq 0 ]; then
    echo "使用方法: $0 \"コミットメッセージ\""
    echo "例: $0 \"機能追加: 新しい帳票機能\""
    exit 1
fi

COMMIT_MESSAGE="$1"
CURRENT_DATE=$(date '+%Y-%m-%d %H:%M:%S')

echo "=== 自動Git操作開始 ==="
echo "日時: $CURRENT_DATE"
echo "コミットメッセージ: $COMMIT_MESSAGE"
echo

# 変更状況を確認
echo "=== 変更ファイル確認 ==="
git status --porcelain
echo

# 変更がない場合は終了
if [ -z "$(git status --porcelain)" ]; then
    echo "変更がありません。終了します。"
    exit 0
fi

# ファイルをステージング
echo "=== ステージング ==="
git add .
echo "ファイルをステージングしました。"

# コミット
echo "=== コミット ==="
git commit -m "$COMMIT_MESSAGE

🤖 Generated with [Claude Code](https://claude.ai/code)

Co-Authored-By: Claude <noreply@anthropic.com>"

echo "コミットが完了しました。"

# プッシュ
echo "=== プッシュ ==="
git push origin main

echo "=== 自動Git操作完了 ==="
echo "GitHubへのプッシュが完了しました: https://github.com/hiromaya/InventoryManagementSystem"