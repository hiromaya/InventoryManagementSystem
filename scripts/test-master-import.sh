#!/bin/bash

# マスタデータ取込テストスクリプト
PROJECT_ROOT=$(pwd)

echo -e "\033[32m=== マスタデータ取込テスト ===\033[0m"
echo "プロジェクトルート: $PROJECT_ROOT"
echo ""

# CSVファイルの配列定義
declare -A CSV_FILES
CSV_FILES["得意先マスタ"]="得意先.csv 大臣出力ファイル/得意先.csv"
CSV_FILES["商品マスタ"]="商品.csv 大臣出力ファイル/商品.csv"
CSV_FILES["仕入先マスタ"]="仕入先.csv 大臣出力ファイル/仕入先.csv"
CSV_FILES["等級マスタ"]="等級汎用マスター1.csv 大臣出力ファイル/等級汎用マスター１.csv"
CSV_FILES["階級マスタ"]="階級汎用マスター2.csv 大臣出力ファイル/階級汎用マスター２.csv"
CSV_FILES["荷印マスタ"]="荷印汎用マスター3.csv 大臣出力ファイル/荷印汎用マスター３.csv"

echo -e "\033[36m■ CSVファイル確認\033[0m"
for master in "${!CSV_FILES[@]}"; do
    echo -e "\n\033[33m[$master]\033[0m"
    found=false
    
    for path in ${CSV_FILES[$master]}; do
        full_path="$PROJECT_ROOT/$path"
        if [ -f "$full_path" ]; then
            echo -e "  \033[32m✓ $path が見つかりました\033[0m"
            
            # ヘッダー行を表示
            header=$(head -n 1 "$full_path" 2>/dev/null)
            if [ $? -eq 0 ]; then
                echo -e "    \033[90mヘッダー: $header\033[0m"
            else
                echo -e "    \033[31mヘッダー読み取り失敗\033[0m"
            fi
            
            # ファイルサイズと行数
            size=$(stat -c%s "$full_path" 2>/dev/null || stat -f%z "$full_path" 2>/dev/null)
            lines=$(wc -l < "$full_path")
            echo -e "    \033[90mサイズ: $size bytes, 行数: $lines\033[0m"
            
            found=true
            break
        fi
    done
    
    if [ "$found" = false ]; then
        echo -e "  \033[31m✗ CSVファイルが見つかりません\033[0m"
        echo -e "    \033[90m探した場所:\033[0m"
        for path in ${CSV_FILES[$master]}; do
            echo -e "      \033[90m- $path\033[0m"
        done
    fi
done

echo -e "\n\033[36m■ データベース接続確認\033[0m"
echo -e "\033[90m実行コマンド: dotnet run test-connection\033[0m"

echo -e "\n\033[36m■ マスタ取込コマンド例\033[0m"
echo -e "\033[33mdotnet run import-customers '大臣出力ファイル/得意先.csv'\033[0m"
echo -e "\033[33mdotnet run import-products '大臣出力ファイル/商品.csv'\033[0m"
echo -e "\033[33mdotnet run import-suppliers '大臣出力ファイル/仕入先.csv'\033[0m"

echo -e "\n\033[36m■ 注意事項\033[0m"
echo -e "\033[90m- CSVファイルのエンコーディングは自動判定されます（UTF-8 BOM付き優先、次にShift-JIS）\033[0m"
echo -e "\033[90m- ヘッダー名が一致しない場合、詳細なエラーログが出力されます\033[0m"
echo -e "\033[90m- 既存データは全て削除されてから新規データが投入されます\033[0m"

echo -e "\n\033[32m=== テストスクリプト完了 ===\033[0m"