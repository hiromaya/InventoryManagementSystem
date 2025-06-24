#!/bin/bash

# 開発環境のセットアップスクリプト
echo -e "\033[32m開発環境をセットアップしています...\033[0m"

# プロジェクトルートの取得
PROJECT_ROOT=$(pwd)

# .envファイルの作成（既存の場合はスキップ）
ENV_FILE="$PROJECT_ROOT/.env"
if [ ! -f "$ENV_FILE" ]; then
    cat > "$ENV_FILE" << EOF
ASPNETCORE_ENVIRONMENT=Development
EOF
    echo -e "\033[32m✓ .envファイルを作成しました\033[0m"
else
    echo -e "\033[33m- .envファイルは既に存在します\033[0m"
fi

# 開発用データフォルダの作成
DATA_PATH="$PROJECT_ROOT/data/InventoryImport"
DEPARTMENTS=("DeptA" "DeptB" "DeptC")

for dept in "${DEPARTMENTS[@]}"; do
    mkdir -p "$DATA_PATH/$dept/Import"
    mkdir -p "$DATA_PATH/$dept/Processed"
    mkdir -p "$DATA_PATH/$dept/Error"
    
    echo -e "\033[32m✓ 部門 $dept のフォルダを作成しました\033[0m"
done

# gitignoreに開発用データフォルダを追加
GITIGNORE="$PROJECT_ROOT/.gitignore"
IGNORE_ENTRY="data/"
if [ -f "$GITIGNORE" ]; then
    if ! grep -q "^$IGNORE_ENTRY$" "$GITIGNORE"; then
        echo "" >> "$GITIGNORE"
        echo "$IGNORE_ENTRY" >> "$GITIGNORE"
        echo -e "\033[32m✓ .gitignoreに'data/'を追加しました\033[0m"
    fi
fi

echo ""
echo -e "\033[32m=== 開発環境のセットアップが完了しました ===\033[0m"
echo ""
echo -e "\033[36mフォルダ構造:\033[0m"
echo "  data/InventoryImport/"
echo "  ├── DeptA/"
echo "  │   ├── Import/       # CSVファイルをここに配置"
echo "  │   ├── Processed/    # 処理済みファイル"
echo "  │   └── Error/        # エラーファイル"
echo "  ├── DeptB/ (同様の構造)"
echo "  └── DeptC/ (同様の構造)"
echo ""
echo -e "\033[36m使用方法:\033[0m"
echo "1. CSVファイルを対応する部門のImportフォルダに配置"
echo "2. import-salesコマンドで取込処理を実行"
echo "3. 処理済みファイルはProcessed、エラーはErrorフォルダへ自動移動"