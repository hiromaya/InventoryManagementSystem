# アンマッチチェック関連実装状況調査結果

調査日時: 2025-07-25 12:30

## 📊 調査概要

本調査は、アンマッチチェック0件を帳票実行の必須条件とする機能を実装する前に、現在の実装状況を詳細に調査したものです。元データとして`クエリ２/14.json`のテーブル一覧を参照し、データベースの現状を把握しました。

## 1. アンマッチチェック処理の現状

### 1.1 UnmatchListService
- **ファイルパス**: `src/InventorySystem.Core/Services/UnmatchListService.cs`
- **実装状況**:
  - ✅ **アンマッチ結果の保存処理**: `UnmatchListResult`クラスで結果を返却
  - ✅ **戻り値にアンマッチ件数含む**: `UnmatchCount`プロパティで件数を返却
  - ⚠️ **CP在庫マスタの削除処理**: 現在は保留（コメントアウト済み）

### 1.2 処理フロー（現在の実装）
1. **在庫マスタ最適化処理**（`OptimizeInventoryMasterAsync`）
2. **CP在庫マスタ作成**（`CreateCpInventoryFromInventoryMasterAsync`）
3. **当日エリアクリア**（`ClearDailyAreaAsync`） 
4. **データ集計と検証**（`AggregateDailyDataWithValidationAsync`）
5. **アンマッチリスト生成**（`GenerateUnmatchListAsync`）
6. **結果返却**（`UnmatchListResult`オブジェクト）

### 1.3 重要な実装詳細
- **DataSetId管理**: 既存伝票データから取得または新規生成
- **アンマッチ種別**: "該当無"、"在庫0"の2種類
- **対象伝票**: 売上（51,52）、仕入（11,12）、在庫調整（71,72）
- **除外条件**: 数量0、経費・加工費（区分2,5）、EXIT系荷印

## 2. 各帳票処理の実装状況

### 2.1 商品日報
- **サービスクラス**: ✅ `src/InventorySystem.Core/Services/DailyReportService.cs`
- **FastReportサービス**: ✅ `src/InventorySystem.Reports/FastReport/Services/DailyReportFastReportService.cs`
- **コンソールコマンド**: ✅ `daily-report` コマンド実装済み
- **実行前チェック**: ❌ アンマッチチェック0件の前提条件チェックなし

### 2.2 商品勘定
- **サービスクラス**: ✅ `src/InventorySystem.Reports/FastReport/Services/ProductAccountFastReportService.cs`
- **コンソールコマンド**: ✅ `product-account` コマンド実装済み
- **実行前チェック**: ❌ アンマッチチェック0件の前提条件チェックなし
- **特徴**: ストアドプロシージャ`sp_CreateProductLedgerData`を使用

### 2.3 在庫表
- **サービスクラス**: ✅ `src/InventorySystem.Core/Services/InventoryListService.cs`
- **コンソールコマンド**: ✅ `inventory-list` コマンド実装済み
- **実行前チェック**: ❌ アンマッチチェック0件の前提条件チェックなし

### 2.4 日次終了処理
- **サービスクラス**: ✅ `src/InventorySystem.Core/Services/DailyCloseService.cs`
- **コンソールコマンド**: ✅ `check-daily-close`, `dev-daily-close` コマンド実装済み
- **実行前チェック**: ✅ 複数の安全機構実装（時間制約、データ整合性チェック等）
- **特徴**: `DailyCloseManagement`テーブルによる状態管理

## 3. CSVインポート処理

### 3.1 import-folderコマンド
- **実装場所**: `src/InventorySystem.Console/Program.cs:ExecuteImportFromFolderAsync`
- **DataSetId管理**: 各インポートサービスで個別生成・管理
- **処理完了後の動作**: 
  - ✅ ファイル移動（Processed フォルダ）
  - ✅ エラーファイル移動（Error フォルダ）
  - ❌ 自動アンマッチチェック実行なし（削除済み）

### 3.2 サポートファイル種別
- **マスタ系**: 商品、得意先、仕入先、等級、階級、荷印、産地
- **伝票系**: 売上、仕入、在庫調整、前月末在庫
- **その他**: 入金、支払、担当者等

## 4. データベース構造

### 4.1 既存のアンマッチ関連テーブル
- ✅ **CpInventoryMaster**: 処理用一時テーブル（アンマッチチェックの基準）
- ✅ **DataSetManagement**: データセット管理（単一テーブル方式）
- ✅ **DailyCloseManagement**: 日次終了処理管理
- ✅ **ProcessHistory**: 処理履歴
- ✅ **AuditLogs**: 監査ログ

### 4.2 現在存在しないテーブル
- ❌ **UnmatchCheckResult**: アンマッチチェック結果専用テーブル
- ❌ **ReportExecutionHistory**: 帳票実行履歴テーブル

### 4.3 主要テーブル詳細（クエリ２/14.json より）
```json
主要テーブル一覧（42テーブル中、アンマッチ関連）:
- AuditLogs: 監査ログ
- CpInventoryMaster: CP在庫マスタ（処理用）
- DailyCloseManagement: 日次終了管理
- DataSetManagement: データセット管理（統一済み）
- DataSets: 廃止予定（999_DropDataSetsTable.sql）
- InventoryMaster: 在庫マスタ本体
- ProcessHistory: 処理履歴
```

## 5. コンソールコマンド一覧

| コマンド名 | 用途 | 実行前チェック | アンマッチ関連 |
|-----------|------|----------------|----------------|
| `unmatch-list` | アンマッチチェック・PDF生成 | なし | 本体機能 |
| `daily-report` | 商品日報PDF生成 | なし | **要改修** |
| `product-account` | 商品勘定PDF生成 | なし | **要改修** |
| `inventory-list` | 在庫表PDF生成 | なし | **要改修** |
| `import-folder` | CSVファイル一括取込 | なし | 関連処理 |
| `check-daily-close` | 日次終了前確認 | ✅ 実装済み | 参考実装 |
| `dev-daily-close` | 開発・日次終了処理 | ✅ 実装済み | 参考実装 |

### 5.1 実装済みコマンド詳細
- **総数**: 28コマンド実装済み
- **開発用**: 12コマンド（`init-database`, `migrate-*`, `dev-*`等）
- **運用用**: 16コマンド（メイン機能）

## 6. 実装に向けた課題

### 6.1 追加が必要な機能
1. **UnmatchCheckResultテーブル**: アンマッチチェック結果の永続化
2. **各帳票処理の前提条件チェック**: アンマッチ件数0の検証機能
3. **ReportExecutionHistoryテーブル**: 帳票実行履歴の管理
4. **アンマッチチェック自動実行**: import-folder後の自動実行

### 6.2 修正が必要な箇所
1. **DailyReportService**: アンマッチチェック前提条件の追加
2. **ProductAccountFastReportService**: 同上
3. **InventoryListService**: 同上
4. **import-folderコマンド**: 完了後のアンマッチチェック自動実行

### 6.3 設計上の考慮事項
1. **パフォーマンス**: アンマッチチェックは重い処理（3分程度）
2. **並行処理**: 複数の帳票処理からの同時実行
3. **エラーハンドリング**: アンマッチチェック失敗時の処理
4. **ユーザビリティ**: 0件でない場合の明確なエラーメッセージ

## 7. 推奨実装順序

### Phase 1: データベース拡張
1. **UnmatchCheckResultテーブル作成** - アンマッチ結果永続化
2. **ReportExecutionHistoryテーブル作成** - 帳票実行履歴管理

### Phase 2: アンマッチチェック機能強化
1. **UnmatchListService拡張** - 結果保存機能追加
2. **アンマッチチェック結果取得API** - 最新結果の取得機能

### Phase 3: 帳票処理の前提条件チェック実装
1. **共通チェック機能** - `IPreReportValidationService`作成
2. **各帳票サービス修正** - 前提条件チェックの組み込み

### Phase 4: ユーザビリティ改善
1. **エラーメッセージ改善** - わかりやすいエラー表示
2. **自動アンマッチチェック** - import-folder後の自動実行

## 8. 技術的な注意事項

### 8.1 現在の実装の特徴
- **DataSetManagement専用**: DataSetsテーブルは廃止済み
- **FastReport必須**: Windows環境での本格運用前提
- **5項目複合キー**: 商品・等級・階級・荷印コード・荷印名での管理

### 8.2 互換性確保
- **既存処理への影響**: 最小限の変更で実装
- **段階的適用**: 帳票種別ごとの段階的な適用可能
- **設定による制御**: フィーチャーフラグでの機能制御

## 9. 結論

現在のシステムには、アンマッチチェック処理の基盤は整っているが、**帳票実行の前提条件としてのアンマッチ件数0チェックは未実装**です。実装には以下の要素が必要です：

1. ✅ **アンマッチチェック処理**: 実装済み・動作確認済み
2. ❌ **結果の永続化**: UnmatchCheckResultテーブルが必要
3. ❌ **帳票処理の前提条件チェック**: 各サービスへの組み込みが必要
4. ❌ **エラーハンドリング**: アンマッチ検出時の適切な処理が必要

推定実装工数は **5-8日程度**（設計・実装・テストを含む）です。