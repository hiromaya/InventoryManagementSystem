# DataSetManagementテーブルのスキーマエラー調査結果

## 📋 調査概要

**調査日時**: 2025年7月15日 11:15  
**調査者**: Claude Code  
**対象**: 023_UpdateDatasetManagement.sqlマイグレーションスクリプト  
**問題**: ProcessTypeとTotalRecordCountカラムが存在しないエラー

## 🚨 発生したエラー

### エラー内容
```sql
-- 71行目: ProcessTypeカラムが存在しない
UPDATE DataSetManagement
SET ImportType = CASE 
    WHEN ProcessType = 'INITIAL_INVENTORY' THEN 'INIT'
    -- エラー: Invalid column name 'ProcessType'

-- 82行目: TotalRecordCountカラムが存在しない  
UPDATE DataSetManagement
SET RecordCount = ISNULL(TotalRecordCount, 0)
-- エラー: Invalid column name 'TotalRecordCount'
```

### エラーの原因
1. **処理順序の問題**: 存在しないカラムを参照するUPDATE文が、カラム追加処理より先に実行された
2. **カラム依存関係**: 他のカラムが存在することを前提としたUPDATE文

## 📊 現在のテーブル構造

### 既存カラム（確認済み）
- DataSetId (PK)
- JobDate
- ImportType
- RecordCount
- IsActive
- IsArchived
- ParentDataSetId
- CreatedAt
- CreatedBy
- DeactivatedAt
- DeactivatedBy
- ArchivedAt
- ArchivedBy
- Notes

### 不足していたカラム
- **ProcessType** - プロセスタイプ（NVARCHAR(50)）
- **TotalRecordCount** - 総レコード数（INT）
- **Department** - 部門コード（NVARCHAR(50)）
- **ImportedFiles** - インポートファイル情報（NVARCHAR(MAX)）
- **UpdatedAt** - 更新日時（DATETIME2）

## 🔧 実施した修正

### 1. 不足カラムの追加処理を先頭に挿入

```sql
-- 0-1. ProcessTypeカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'ProcessType')
BEGIN
    ALTER TABLE DataSetManagement
    ADD ProcessType NVARCHAR(50) NULL;
    
    -- デフォルト値の設定
    UPDATE DataSetManagement
    SET ProcessType = CASE 
        WHEN ImportType = 'INIT' THEN 'INITIAL_INVENTORY'
        WHEN ImportType = 'CARRYOVER' THEN 'CARRYOVER'
        ELSE 'IMPORT'
    END
    WHERE ProcessType IS NULL;
END
```

### 2. TotalRecordCountカラムの追加

```sql
-- 0-2. TotalRecordCountカラムの追加（存在しない場合）
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'TotalRecordCount')
BEGIN
    ALTER TABLE DataSetManagement
    ADD TotalRecordCount INT NOT NULL DEFAULT 0;
    
    -- 既存のRecordCountから値をコピー
    IF EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[DataSetManagement]') 
               AND name = 'RecordCount')
    BEGIN
        UPDATE DataSetManagement
        SET TotalRecordCount = RecordCount
        WHERE TotalRecordCount = 0 AND RecordCount > 0;
    END
END
```

### 3. 追加カラムの処理

```sql
-- 0-3. Departmentカラムの追加
-- 0-4. ImportedFilesカラムの追加
-- 0-5. UpdatedAtカラムの追加
```

### 4. 既存のUPDATE文の修正

```sql
-- 8. 既存データのImportType設定（ProcessTypeに基づいて）
-- 注意: この処理は不要。ProcessTypeは既に0-1で設定済み
-- UPDATE文をコメントアウト

-- 9. RecordCountの設定（TotalRecordCountから）
-- 注意: この処理は不要。TotalRecordCountは既に0-2で設定済み
-- UPDATE文をコメントアウト
```

## 🎯 修正後の処理フロー

1. **Phase 0**: 不足カラムの追加とデフォルト値設定
   - ProcessType追加 → ImportTypeから推測して設定
   - TotalRecordCount追加 → RecordCountから値をコピー
   - Department追加 → 'DeptA'をデフォルト設定
   - ImportedFiles追加 → NULL許可
   - UpdatedAt追加 → CreatedAtから値をコピー

2. **Phase 1-7**: 既存カラムの追加処理（既存のまま）
   - ImportType, RecordCount, IsActive, IsArchived, ParentDataSetId, Notes, CreatedBy

3. **Phase 8-9**: 既存データの更新処理（無効化）
   - 既にPhase 0で処理済みのためコメントアウト

4. **Phase 10**: 実行結果の確認

## 🔍 デバッグ用ツール

### debug_datasetmanagement.sqlを作成
- テーブル構造の確認
- 必要カラムの存在確認
- データサンプルの表示
- 統計情報の表示

### 実行方法
```bash
# デバッグスクリプトの実行
sqlcmd -S localhost -d InventoryManagementSystem -i database/debug_datasetmanagement.sql

# マイグレーションスクリプトの実行
sqlcmd -S localhost -d InventoryManagementSystem -i database/migrations/023_UpdateDatasetManagement.sql
```

## ✅ 期待される結果

### 成功時のメッセージ
```
===== 023_UpdateDataSetManagement.sql 実行完了 =====
DataSetManagementテーブルのスキーマを更新しました。
追加されたカラム: ProcessType, TotalRecordCount, Department, ImportedFiles, UpdatedAt
```

### 確認すべき点
- [ ] 全ての必要カラムが存在する
- [ ] 既存データに適切なデフォルト値が設定されている
- [ ] ProcessTypeが正しく設定されている（ImportTypeから推測）
- [ ] TotalRecordCountが正しくコピーされている（RecordCountから）
- [ ] エラーが発生していない

## 📝 学習事項

### 1. マイグレーション設計の重要性
- カラム間の依存関係を正しく把握する
- 処理順序を適切に設計する
- 存在確認を必ず行う

### 2. SQLスクリプトのベストプラクティス
- IF NOT EXISTS文の活用
- 条件付きUPDATE文の使用
- エラーハンドリングの実装
- デバッグ用ツールの準備

### 3. データ移行の注意点
- 既存データの整合性を保つ
- デフォルト値の適切な設定
- NULL許容性の検討

## 🔄 今後の改善点

1. **エラーハンドリングの強化**
   - TRY-CATCH文の実装
   - ロールバック機能の追加

2. **バリデーションの追加**
   - データ整合性チェック
   - 制約の確認

3. **ドキュメントの整備**
   - スキーマ変更履歴の記録
   - 影響範囲の明確化

---

**修正完了**: 2025年7月15日 11:15  
**検証**: 未実施（次回実行時に確認）  
**影響範囲**: DataSetManagementテーブル、関連するサービスクラス