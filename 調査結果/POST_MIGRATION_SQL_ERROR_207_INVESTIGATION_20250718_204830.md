# フェーズドマイグレーション実行後のSQL Error 207調査結果

生成日時: 2025-07-18 20:48:30

## エグゼクティブサマリー

- **フェーズドマイグレーションの実行状況**: 実行不可能状態
- **データベーステーブルの現在状態**: 確認不可能（Linux環境制限）
- **SQL Error 207の根本原因**: Linux環境での実行環境制限による調査不完全
- **推奨される解決策**: Windows環境での実行とマイグレーションの直接実行

## 1. フェーズドマイグレーションの実行結果

### migrate-phase2の実行状況
**実行ログ**: Linux環境でのビルドエラーにより実行不可能
```
error NETSDK1005: Assets file doesn't have a target for 'net8.0'
Project targets net8.0-windows7.0 framework
```

**主要な問題点**:
- プロジェクトのターゲットフレームワークが `net8.0-windows7.0` であるため、Linux環境では実行不可能
- FastReport関連のWindows専用参照により、Linux環境でのビルドが困難

### migrate-phase3の実行状況
**実行ログ**: migrate-phase2と同じ理由で実行不可能

## 2. 現在のデータベーステーブル構造

### 実際のカラム構造
Linux環境では `sqlcmd` コマンドが利用できないため、直接的なデータベース構造の確認は不可能でした。

### 期待される構造との比較
| テーブル名 | 期待されるカラム | 実際のカラム | 状態 |
|-----------|-----------------|-------------|------|
| ProductMaster | CreatedAt, UpdatedAt | 確認不可能 | 未確認 |
| CustomerMaster | CreatedAt, UpdatedAt | 確認不可能 | 未確認 |
| SupplierMaster | CreatedAt, UpdatedAt | 確認不可能 | 未確認 |

## 3. フェーズドマイグレーションファイルの構造分析

### 051_Phase2_AddNewColumns.sqlの内容
**正常性**: SQLファイルの構造は正常
**主要な処理**:
- ProductMaster、CustomerMaster、SupplierMasterに `CreatedAt`, `UpdatedAt` カラムを追加
- 条件付きカラム追加（既存の場合はスキップ）
- トランザクション処理による安全性確保

### 052_Phase3_MigrateDataAndSync.sqlの内容
**正常性**: SQLファイルの構造は正常
**主要な処理**:
- 既存データの `CreatedDate`/`UpdatedDate` から `CreatedAt`/`UpdatedAt` への移行
- 同期トリガーの作成（双方向同期）
- 動的SQLによる条件付き処理

## 4. 発見された環境制限

### Linux環境での制限事項
1. **プロジェクトのターゲットフレームワーク**: `net8.0-windows7.0`
2. **FastReport依存関係**: Windows専用ライブラリ
3. **SQL Server接続**: `sqlcmd` コマンドが利用不可
4. **マイグレーションシステム**: コンパイル時依存関係によりLinux環境では実行不可

### 実行環境の不整合
- **開発環境**: Linux (WSL2)
- **プロジェクト設計**: Windows専用
- **データベース**: SQL Server（Windows環境想定）

## 5. 根本原因の特定

### 最も可能性の高い原因
**フェーズドマイグレーションが実行されていない**

**証拠**:
1. Linux環境では `migrate-phase2` および `migrate-phase3` コマンドが実行不可能
2. プロジェクトのビルドがLinux環境で失敗
3. データベースに対する直接的な確認手段がない

### 推論される現在の状況
1. データベースには従来の `CreatedDate`/`UpdatedDate` カラムのみが存在
2. アプリケーションコードは新しい `CreatedAt`/`UpdatedAt` カラムを参照している
3. この不整合により SQL Error 207 が発生している

## 6. 推奨される解決策

### 即座に実行可能な対策

#### 1. Windows環境での実行
```bash
# Windows環境で実行
cd C:\path\to\InventoryManagementSystem\src\InventorySystem.Console
dotnet run -- migrate-phase2
dotnet run -- migrate-phase3
```

#### 2. SQLファイルの直接実行
```sql
-- SQL Server Management Studio または sqlcmd で実行
-- 051_Phase2_AddNewColumns.sql
-- 052_Phase3_MigrateDataAndSync.sql
```

#### 3. 手動でのカラム追加（緊急時）
```sql
-- 緊急時の手動カラム追加
ALTER TABLE ProductMaster ADD CreatedAt DATETIME2 NULL;
ALTER TABLE ProductMaster ADD UpdatedAt DATETIME2 NULL;
ALTER TABLE CustomerMaster ADD CreatedAt DATETIME2 NULL;
ALTER TABLE CustomerMaster ADD UpdatedAt DATETIME2 NULL;
ALTER TABLE SupplierMaster ADD CreatedAt DATETIME2 NULL;
ALTER TABLE SupplierMaster ADD UpdatedAt DATETIME2 NULL;

-- 既存データの移行
UPDATE ProductMaster SET CreatedAt = CreatedDate, UpdatedAt = UpdatedDate WHERE CreatedAt IS NULL;
UPDATE CustomerMaster SET CreatedAt = CreatedDate, UpdatedAt = UpdatedDate WHERE CreatedAt IS NULL;
UPDATE SupplierMaster SET CreatedAt = CreatedDate, UpdatedAt = UpdatedDate WHERE CreatedAt IS NULL;
```

### 根本的な解決策

#### 1. 環境固有の設定改善
- Linux環境用の条件付きビルド設定の改善
- 開発環境とプロダクション環境の明確な分離

#### 2. マイグレーションシステムの改善
- 環境に依存しないマイグレーション実行方式の導入
- SQLファイルの直接実行オプションの追加

## 7. 検証方法

### 解決後の確認手順

#### 1. データベース構造の確認
```sql
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
    AND (COLUMN_NAME LIKE '%Created%' OR COLUMN_NAME LIKE '%Updated%')
ORDER BY TABLE_NAME, COLUMN_NAME;
```

#### 2. マイグレーション履歴の確認
```sql
SELECT 
    MigrationId,
    AppliedDate,
    AppliedBy,
    ExecutionTimeMs
FROM __SchemaVersions 
WHERE MigrationId IN (
    '051_Phase2_AddNewColumns.sql',
    '052_Phase3_MigrateDataAndSync.sql'
)
ORDER BY AppliedDate DESC;
```

#### 3. アプリケーションの動作確認
```bash
# Windows環境で実行
dotnet run -- import-folder DeptA 2025-07-18
```

### 再発防止策

#### 1. 環境依存の明確化
- Windows環境での実行を前提とした開発・運用ガイドラインの作成
- Linux環境での調査用ツールの整備

#### 2. マイグレーション実行の監視
- マイグレーション実行状況の定期的な確認
- 失敗時の自動通知システムの導入

#### 3. 開発プロセスの改善
- 環境固有の問題を早期発見するためのCI/CDパイプライン改善
- 複数環境での動作確認の義務化

## 8. 今後の対応計画

### 短期的対応（即座に実行）
1. **Windows環境での緊急確認**
   - 現在のデータベース構造の確認
   - フェーズドマイグレーションの実行状況確認
   - 必要に応じて手動でのマイグレーション実行

2. **SQL Error 207の即座解決**
   - 不足しているカラムの特定と追加
   - アプリケーションの動作確認

### 長期的対応（今後の改善）
1. **環境依存問題の解決**
   - クロスプラットフォーム対応の検討
   - 開発環境の統一化

2. **調査・運用ツールの改善**
   - Linux環境での最低限の調査機能の実装
   - 環境に依存しない管理ツールの開発

## 9. 重要な注意事項

### 現在の状況の危険性
1. **データ不整合の可能性**: 新旧カラムの不整合により、データ損失の危険性
2. **アプリケーション停止**: SQL Error 207によりシステムが機能停止状態
3. **移行作業の複雑化**: 時間経過により、データ整合性の確保が困難になる可能性

### 緊急性の高い対応
- **24時間以内**: Windows環境での現状確認と緊急対応
- **48時間以内**: 根本的な解決策の実装
- **1週間以内**: 再発防止策の実装

---

**調査結果の信頼性について**

この調査は Linux 環境での制限により、完全な情報を取得することができませんでした。実際の問題解決には、Windows 環境での詳細な確認と実行が必要です。

**最重要推奨事項**: 即座に Windows 環境でのマイグレーション実行状況の確認と、必要に応じた手動マイグレーションの実行