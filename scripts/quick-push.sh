#!/bin/bash

# クイックプッシュスクリプト - Claude Codeでの開発用
# 変更を自動的にコミット・プッシュ

set -e

CURRENT_DATE=$(date '+%Y-%m-%d %H:%M:%S')

echo "=== Claude Code - クイックプッシュ ==="
echo "日時: $CURRENT_DATE"

# 変更がない場合は終了
if [ -z "$(git status --porcelain)" ]; then
    echo "変更がありません。"
    exit 0
fi

echo "変更されたファイル:"
git status --porcelain

# 自動的にコミットメッセージを生成
COMMIT_MSG="Auto update: $(date '+%Y-%m-%d %H:%M')"

# ファイルをステージング
git add .

# コミット
git commit -m "$COMMIT_MSG

🤖 Generated with [Claude Code](https://claude.ai/code)

Co-Authored-By: Claude <noreply@anthropic.com>"

# プッシュ
git push origin main

echo "✅ GitHubへのプッシュ完了"
echo "🔗 https://github.com/hiromaya/InventoryManagementSystem"