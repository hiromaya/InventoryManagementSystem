# DataSets/DataSetManagement二重管理調査報告書

生成日時: 2025-07-18 22:30:00

## エグゼクティブサマリー

- **現状**: DataSetsテーブルとDataSetManagementテーブルが併存し、段階的移行を実施中
- **設計思想**: DataSetsは従来型のシンプルな管理、DataSetManagementはエンタープライズ向け高機能管理
- **使用状況**: UnifiedDataSetServiceによる二重書き込みで互換性を保ちながら新機能への移行を進行
- **技術的課題**: 重複機能の存在、データ整合性管理の複雑化、外部キー制約の不整合
- **推奨方針**: DataSetManagementへの段階的統合、明確な役割分担の確立

## 1. テーブル構造比較

### DataSetsテーブル
| カラム名 | データ型 | 制約 | 説明 |
|---------|----------|------|------|
| Id | NVARCHAR(100) | PK | GUID形式のデータセットID |
| Name | NVARCHAR(255) | NOT NULL | データセット名 |
| Description | NVARCHAR(MAX) | NULL | 説明文 |
| ProcessType | NVARCHAR(50) | NULL | 処理種別 |
| DataSetType | NVARCHAR(50) | NULL | データセットタイプ |
| Status | NVARCHAR(20) | NULL | ステータス（文字列） |
| JobDate | DATE | NOT NULL | 業務日付 |
| ImportedAt | DATETIME2 | NOT NULL | 取込日時 |
| RecordCount | INT | NOT NULL | レコード数 |
| FilePath | NVARCHAR(500) | NULL | ファイルパス |
| ErrorMessage | NVARCHAR(MAX) | NULL | エラーメッセージ |
| CreatedAt | DATETIME2 | NOT NULL | 作成日時 |
| UpdatedAt | DATETIME2 | NOT NULL | 更新日時 |

### DataSetManagementテーブル
| カラム名 | データ型 | 制約 | 説明 |
|---------|----------|------|------|
| DataSetId | NVARCHAR(100) | PK | データセットID |
| JobDate | DATE | NOT NULL | 業務日付 |
| ProcessType | NVARCHAR(50) | NOT NULL | 処理種別 |
| ImportType | NVARCHAR(20) | NOT NULL | インポートタイプ |
| RecordCount | INT | NOT NULL | レコード数 |
| TotalRecordCount | INT | NOT NULL | 総レコード数 |
| IsActive | BIT | NOT NULL | アクティブフラグ |
| IsArchived | BIT | NOT NULL | アーカイブフラグ |
| ParentDataSetId | NVARCHAR(100) | FK | 親データセットID |
| ImportedFiles | NVARCHAR(MAX) | NULL | インポートファイル一覧 |
| CreatedAt | DATETIME2 | NOT NULL | 作成日時 |
| CreatedBy | NVARCHAR(100) | NOT NULL | 作成者 |
| Department | NVARCHAR(50) | NOT NULL | 部門 |
| Notes | NVARCHAR(MAX) | NULL | 備考 |
| DeactivatedAt | DATETIME2 | NULL | 無効化日時 |
| DeactivatedBy | NVARCHAR(50) | NULL | 無効化実行者 |
| ArchivedAt | DATETIME2 | NULL | アーカイブ日時 |
| ArchivedBy | NVARCHAR(50) | NULL | アーカイブ実行者 |

### 主な違い
- **主キー**: DataSetsはId、DataSetManagementはDataSetId
- **ステータス管理**: DataSetsは文字列、DataSetManagementはブール値フラグ
- **階層管理**: DataSetManagementのみParentDataSetIdで階層構造をサポート
- **監査機能**: DataSetManagementが作成者、無効化者、アーカイブ者を追跡
- **部門管理**: DataSetManagementのみDepartmentカラムで部門別管理

## 2. 使用状況マトリックス

| コンポーネント | DataSets使用 | DataSetManagement使用 | 備考 |
|---------------|--------------|---------------------|------|
| SalesVoucherImportService | ○ | × | 従来型インポート |
| PurchaseVoucherImportService | ○ | × | 従来型インポート |
| InventoryAdjustmentImportService | ○ | × | 従来型インポート |
| DailyReportService | × | ○ | 新型業務処理 |
| DailyCloseService | × | ○ | 新型業務処理 |
| UnmatchListService | × | ○ | 新型業務処理 |
| UnifiedDataSetService | ○ | ○ | 統合管理サービス |
| DataSetManager | × | ○ | 専用管理サービス |

## 3. コマンド別使用状況

### import-folder
- **使用テーブル**: DataSets（UnifiedDataSetService経由で両方）
- **使用方法**: 複数ファイルの一括インポート処理でデータセット作成・管理
- **関連コード箇所**: Program.cs:ExecuteImportFolderAsync()

### import-sales/purchase/adjustment
- **使用テーブル**: DataSets（個別インポートサービス経由）
- **使用方法**: 個別ファイルインポート処理でデータセット作成・ステータス管理
- **関連コード箇所**: 各種ImportService

### daily-report
- **使用テーブル**: DataSetManagement（DataSetManager経由）
- **使用方法**: 商品日報作成時のデータセット管理と誤操作防止
- **関連コード箇所**: DailyReportService

### daily-close
- **使用テーブル**: DataSetManagement（DataSetManager経由）
- **使用方法**: 日次終了処理でのデータセット紐付け管理
- **関連コード箇所**: DailyCloseService

### unmatch-list
- **使用テーブル**: DataSetManagement（DataSetManager経由）
- **使用方法**: アンマッチリスト処理でのデータセット管理
- **関連コード箇所**: UnmatchListService

## 4. 外部キー依存関係

### DataSetsへの参照
| テーブル名 | 外部キーカラム | 削除時の影響 |
|-----------|---------------|-------------|
| SalesVouchers | DataSetId | 売上伝票データが孤立 |
| PurchaseVouchers | DataSetId | 仕入伝票データが孤立 |
| InventoryAdjustments | DataSetId | 在庫調整データが孤立 |

### DataSetManagementへの参照
| テーブル名 | 外部キーカラム | 削除時の影響 |
|-----------|---------------|-------------|
| DataSetManagement | ParentDataSetId | 階層構造の不整合 |
| ProcessHistory | DataSetId | プロセス履歴が孤立 |

## 5. ビジネスロジック分析

### DataSetsの役割
- CSV取込処理の基本的な単位管理
- シンプルなステータス管理（Imported, Processing, Completed, Error）
- ファイルパスとエラーメッセージの管理
- 従来システムとの互換性維持

### DataSetManagementの役割
- 高度なデータセット世代管理
- 階層構造による親子関係管理
- アクティブ/アーカイブ/無効化のライフサイクル管理
- 部門別管理と監査証跡
- 誤操作防止機能（日次終了処理等）

### 機能の重複と差異
- **重複機能**: JobDate、RecordCount、ProcessType、CreatedAt
- **DataSets固有機能**: Status、ErrorMessage、FilePath、UpdatedAt
- **DataSetManagement固有機能**: ImportType、階層管理、監査機能、部門管理

## 6. 移行影響分析

### 高リスク項目
1. **外部キー制約の変更**: 3つの伝票テーブルがDataSetsを参照
2. **ID体系の不整合**: DataSet.Id vs DataSetManagement.DataSetId
3. **ステータス管理の変更**: 文字列からブール値フラグへの変更
4. **既存データの移行**: 異なるスキーマ間でのデータ変換

### 中リスク項目
1. **UnifiedDataSetServiceの複雑化**: 二重書き込みロジックの維持
2. **インポートサービスの変更**: 従来型から新型への移行
3. **レポート処理の変更**: データ参照先の変更
4. **エラーハンドリングの統一**: 異なるエラー管理方式の統合

### 低リスク項目
1. **新機能の追加**: DataSetManagementの新機能は既存処理に影響なし
2. **ログ出力の変更**: 内部処理の変更でユーザー影響なし
3. **パフォーマンスの最適化**: 段階的な改善が可能

## 7. 推奨移行戦略

### Option 1: 段階的移行（推奨）
- **Phase 1**: 現状維持（二重書き込み継続）
- **Phase 2**: 新機能開発はDataSetManagementのみ使用
- **Phase 3**: 既存機能のDataSetManagement移行
- **Phase 4**: DataSetsテーブルの廃止

**メリット**: 段階的な移行でリスクを最小化
**デメリット**: 移行期間が長期化
**必要な作業**: 
1. 外部キー制約の段階的変更
2. データ移行スクリプトの作成
3. 統合サービスの段階的簡素化

### Option 2: 並行稼働（現状）
- **実装方法**: UnifiedDataSetServiceによる二重書き込み
- **移行期間**: 無期限（現在の状態を継続）
- **リスク**: 複雑性の増大、データ不整合の可能性

### Option 3: 機能分離（代替案）
- **DataSets**: 基本的なインポート管理に特化
- **DataSetManagement**: 高度な業務処理に特化
- **実装方法**: 明確な役割分担と連携機能の実装

## 8. 技術的推奨事項

### 短期的対応（1週間以内）
1. **データ整合性チェック機能の強化**: 両テーブル間の整合性確認
2. **UnifiedDataSetServiceのエラーハンドリング改善**: 片方の書き込み失敗時の処理
3. **ドキュメント整備**: 使い分けガイドラインの作成

### 中期的対応（1ヶ月以内）
1. **外部キー制約の段階的変更**: DataSetManagementへの移行準備
2. **データ移行スクリプトの作成**: 既存DataSetsからDataSetManagementへの移行
3. **統合テストの実装**: 両テーブル使用時の整合性テスト

### 長期的対応（3ヶ月以内）
1. **新機能開発ガイドラインの確立**: DataSetManagement優先の方針
2. **レガシーサポートの段階的廃止**: DataSetsテーブルの使用削減
3. **パフォーマンス最適化**: 単一テーブルでの運用最適化

## 9. 未解決の課題

### 技術的課題
- **ID体系の統一**: DataSet.Id vs DataSetManagement.DataSetId
- **ステータス管理の統一**: 文字列 vs ブール値フラグ
- **エラーハンドリング**: 異なるエラー管理方式の統合
- **パフォーマンス**: 二重書き込みのオーバーヘッド

### 運用上の課題
- **データ移行**: 大量の既存データの移行方法
- **ダウンタイム**: 外部キー制約変更時のサービス停止
- **ロールバック**: 移行失敗時の復旧手順
- **監査**: 移行プロセスの監査証跡

## 10. 付録

### 関連ファイル一覧
- **エンティティ**: DataSet.cs, DataSetManagement.cs
- **リポジトリ**: DataSetRepository.cs, DataSetManagementRepository.cs
- **サービス**: UnifiedDataSetService.cs, DataSetManager.cs
- **データベース**: CreateDatabase.sql, 006_AddDataSetManagement.sql

### 参考コード箇所
- **Program.cs**: L1850-2000 (DI登録)
- **UnifiedDataSetService.cs**: L50-150 (二重書き込みロジック)
- **DataSetManager.cs**: L30-80 (DataSetManagement専用機能)
- **各ImportService**: CreateDataSetAsync()メソッド

---

**調査担当**: Claude Code
**調査期間**: 2025-07-18
**調査方法**: 静的コード解析、データベーススキーマ分析、依存関係調査
**推奨実装**: 段階的移行アプローチ（Option 1）