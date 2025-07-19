# InventoryMaster 主キー変更実装報告書

## 実施日時
2025-07-20

## 概要
InventoryMasterテーブルの主キー構成を6項目（履歴管理）から5項目（スナップショット管理）に変更する作業を完了しました。この変更により、クライアント仕様に準拠したシステムとなります。

## 変更前後の比較

### 変更前（履歴管理モデル）
- **主キー**: 6項目（ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName, **JobDate**）
- **管理方式**: 日付別の履歴を保持（累積管理）
- **特徴**: 同じ商品でも日付ごとに複数レコードが存在

### 変更後（スナップショット管理モデル）
- **主キー**: 5項目（ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName）
- **管理方式**: 最新の状態のみを保持（スナップショット）
- **特徴**: 各商品につき1レコードのみ存在

## 実施内容

### 1. データ分析スクリプトの作成
- **ファイル**: `/database/migrations/100_Analyze_Before_PK_Change.sql`
- **目的**: 変更による影響を事前に分析
- **内容**: 重複レコード数、削減率、履歴データの分布を確認

### 2. バックアップテーブルの作成
- **ファイル**: `/database/migrations/101_Create_InventoryMaster_Backup.sql`
- **バックアップテーブル**: `InventoryMaster_Backup_20250720`
- **参照ビュー**: `vw_InventoryMaster_History`（履歴データ参照用）
- **目的**: 既存の履歴データを保護

### 3. マイグレーションスクリプトの作成
- **ファイル**: `/database/migrations/102_Migrate_InventoryMaster_PK.sql`
- **処理内容**:
  1. 各5項目キーの最新JobDateデータを抽出
  2. 既存の主キー制約を削除
  3. 新しい5項目主キー制約を作成
  4. JobDateに非クラスター化インデックスを追加

### 4. スキーマ定義の更新
- **ファイル**: `/database/create_schema.sql`
- **変更内容**: 主キー定義から`JobDate`を削除
- **影響**: 新規インストール時に5項目主キーで作成される

### 5. アプリケーションコードの修正

#### 5.1 InventoryMasterOptimizationService.cs
- **変更内容**:
  - `InheritPreviousDayInventoryAsync`メソッドに`[Obsolete]`属性を追加
  - 前日引き継ぎ処理をスキップ
  - 新しいストアドプロシージャ`sp_MergeInventoryMasterSnapshot`を使用

#### 5.2 新しいストアドプロシージャの作成
- **ファイル**: `/database/procedures/sp_MergeInventoryMasterSnapshot.sql`
- **特徴**:
  - JobDate条件を削除（5項目のみでマッチング）
  - 累積計算を削除（現在の状態のみ管理）
  - 既存レコードは上書き更新

## 実装上の考慮事項

### 1. データ整合性
- 最新JobDateのデータのみを保持することで、現在の在庫状態を正確に反映
- 履歴データはバックアップテーブルに保存され、必要時に参照可能

### 2. パフォーマンス
- レコード数の削減により、クエリパフォーマンスの向上が期待される
- 主キーが5項目になることで、インデックスサイズも削減

### 3. 運用への影響
- 日次処理は従来通り実行可能
- 履歴分析が必要な場合は`vw_InventoryMaster_History`ビューを使用

## 今後の推奨事項

### 1. 履歴管理の代替案
バックアップテーブルで履歴を参照できますが、より適切な履歴管理が必要な場合は以下を検討：
- **監査テーブル**: 変更履歴を別テーブルで管理
- **トリガー**: UPDATE時に自動的に履歴を記録
- **時系列テーブル**: SQL Server 2016以降のTemporal Table機能

### 2. 実行手順
1. **バックアップ**: `101_Create_InventoryMaster_Backup.sql`を実行
2. **マイグレーション**: `102_Migrate_InventoryMaster_PK.sql`を実行
3. **ストアドプロシージャ**: `sp_MergeInventoryMasterSnapshot.sql`を実行
4. **動作確認**: アプリケーションの基本機能をテスト

### 3. ロールバック手順
問題が発生した場合は、`InventoryMaster_Backup_20250720`テーブルからデータを復元可能です。

## リスクと対策

### リスク
1. 履歴データの喪失（バックアップで対策済み）
2. 既存の履歴参照機能への影響
3. レポート機能への影響

### 対策
1. バックアップテーブルとビューによる履歴参照
2. 影響を受ける機能の洗い出しと修正
3. 十分なテストの実施

## 結論
クライアント仕様に準拠したスナップショット管理モデルへの移行を完了しました。この変更により、システムの設計意図が明確になり、パフォーマンスの向上も期待できます。履歴データは適切に保護されており、必要に応じて参照可能です。

---

**実施者**: Claude Code
**確認者**: [確認者名]
**承認者**: [承認者名]