# DataSetManagement統合詳細影響調査

生成日時: 2025-07-18 22:45:00

## エグゼクティブサマリー

DataSetsテーブルをDataSetManagementテーブルに完全統合するための詳細な技術的影響調査結果です。Gemini CLIとの相談により、**段階的移行アプローチ**を強く推奨します。

## 1. 外部キー制約詳細

### 影響を受けるテーブル

#### SalesVouchers
```sql
CONSTRAINT FK_SalesVouchers_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)
```
- **制約名**: FK_SalesVouchers_DataSets
- **データ型**: NVARCHAR(50) → NVARCHAR(100) (拡張必要)
- **NULL許可**: NOT NULL
- **ON DELETE/UPDATE**: 未指定（デフォルトNO ACTION）
- **インデックス**: IX_SalesVouchers_DataSetId
- **推定影響レコード数**: 数万〜数十万件

#### PurchaseVouchers
```sql
CONSTRAINT FK_PurchaseVouchers_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)
```
- **制約名**: FK_PurchaseVouchers_DataSets
- **データ型**: NVARCHAR(50) → NVARCHAR(100) (拡張必要)
- **NULL許可**: NOT NULL
- **ON DELETE/UPDATE**: 未指定（デフォルトNO ACTION）
- **インデックス**: IX_PurchaseVouchers_DataSetId
- **推定影響レコード数**: 数万〜数十万件

#### InventoryAdjustments
```sql
CONSTRAINT FK_InventoryAdjustments_DataSets FOREIGN KEY (DataSetId) REFERENCES DataSets(Id)
```
- **制約名**: FK_InventoryAdjustments_DataSets
- **データ型**: NVARCHAR(50) → NVARCHAR(100) (拡張必要)
- **NULL許可**: NOT NULL
- **ON DELETE/UPDATE**: 未指定（デフォルトNO ACTION）
- **インデックス**: IX_InventoryAdjustments_DataSetId
- **推定影響レコード数**: 数万〜数十万件

### 重要な発見：スキーマ不整合問題

**問題**: 2つの異なるスキーマ定義が存在
- `04_create_import_tables.sql`: DataSetId NVARCHAR(50) NOT NULL
- `CreateDatabase.sql`: DataSetId NVARCHAR(100) NULL

**影響**: 外部キー制約の更新時にデータ型の拡張が必要

## 2. データ型とサイズの互換性調査

### DataSets vs DataSetManagement構造比較

| 項目 | DataSets | DataSetManagement | 互換性 |
|------|----------|-------------------|--------|
| 主キー | Id (string) | DataSetId (string) | ✓ 互換 |
| データ型 | NVARCHAR(100) | NVARCHAR(100) | ✓ 一致 |
| 制約 | NOT NULL | NOT NULL | ✓ 一致 |

### 機能フィールド比較

| 機能 | DataSets | DataSetManagement | 移行戦略 |
|------|----------|-------------------|----------|
| 識別子 | Id | DataSetId | 直接マッピング |
| 名前 | Name | Notes | 変換：`'Name: ' + Name` |
| 説明 | Description | Notes | 変換：`Description + '\n' + Name` |
| 状態管理 | Status (string) | IsActive/IsArchived (bool) | 変換：`Status → Boolean flags` |
| 日付 | JobDate | JobDate | 直接マッピング |
| 作成日時 | CreatedAt | CreatedAt | 直接マッピング |
| 更新日時 | UpdatedAt | × | 削除（更新履歴はProcessHistoryで管理） |

## 3. データ移行マッピング詳細

### 必須フィールドマッピング

| DataSets | DataSetManagement | 変換SQL/ロジック |
|----------|------------------|-----------------|
| Id | DataSetId | `CAST(Id AS NVARCHAR(100))` |
| Name | Notes | `CASE WHEN Name IS NOT NULL THEN 'Name: ' + Name ELSE NULL END` |
| Description | Notes | `CASE WHEN Description IS NOT NULL AND Name IS NOT NULL THEN Name + '\n' + Description WHEN Description IS NOT NULL THEN Description WHEN Name IS NOT NULL THEN 'Name: ' + Name ELSE NULL END` |
| Status | IsActive | `CASE WHEN Status IN ('Completed', 'Imported') THEN 1 ELSE 0 END` |
| Status | IsArchived | `CASE WHEN Status = 'Error' THEN 1 ELSE 0 END` |
| JobDate | JobDate | `JobDate` |
| CreatedAt | CreatedAt | `CreatedAt` |
| DataSetType | ProcessType | `COALESCE(DataSetType, 'UNKNOWN')` |

### デフォルト値が必要なフィールド

| DataSetManagement列 | デフォルト値 | 理由 |
|-------------------|------------|------|
| ImportType | 'LEGACY' | 旧システムからの移行データ |
| CreatedBy | 'migration' | 移行処理実行者 |
| Department | 'DeptA' | デフォルト部門 |
| TotalRecordCount | RecordCount | 同じ値を設定 |
| RecordCount | 0 | DataSetsにRecordCountがない場合 |

## 4. コード変更箇所詳細

### Entity層の変更

| ファイル | 変更内容 | 影響度 |
|---------|---------|--------|
| DataSet.cs | 削除予定 | 高 |
| DataSetManagement.cs | 機能拡張（DataSetsの機能を統合） | 中 |

### Repository層の変更

| ファイル | 変更内容 | 影響度 |
|---------|---------|--------|
| DataSetRepository.cs | 削除予定 | 高 |
| DataSetManagementRepository.cs | 機能拡張 | 中 |

### Service層の変更

| ファイル | 変更内容 | 影響度 |
|---------|---------|--------|
| UnifiedDataSetService.cs | 二重書き込みロジック削除 | 高 |
| SalesVoucherImportService.cs | DataSetRepository → DataSetManagementRepository | 中 |
| PurchaseVoucherImportService.cs | DataSetRepository → DataSetManagementRepository | 中 |
| InventoryAdjustmentImportService.cs | DataSetRepository → DataSetManagementRepository | 中 |

### DI Registration変更

| ファイル | 行番号 | 変更内容 |
|---------|--------|---------|
| Program.cs | 推定L1850-2000 | IDataSetRepository登録削除 |

## 5. 移行手順案（Gemini推奨の段階的アプローチ）

### Phase 1: 準備（推定1日）
1. **完全バックアップ取得**
   ```sql
   BACKUP DATABASE InventoryManagementDB TO DISK = 'C:\Backup\InventoryManagementDB_PreMigration.bak'
   ```

2. **DataSetManagementに不足カラム追加**
   ```sql
   -- 必要に応じて追加のカラムを追加
   ALTER TABLE DataSetManagement ADD 
       DataSetType NVARCHAR(20) NULL,
       ImportedAt DATETIME2 NULL,
       FilePath NVARCHAR(500) NULL,
       ErrorMessage NVARCHAR(MAX) NULL;
   ```

3. **データ型の拡張**
   ```sql
   -- 外部キー制約を持つテーブルのDataSetIdを拡張
   ALTER TABLE SalesVouchers ALTER COLUMN DataSetId NVARCHAR(100) NOT NULL;
   ALTER TABLE PurchaseVouchers ALTER COLUMN DataSetId NVARCHAR(100) NOT NULL;
   ALTER TABLE InventoryAdjustments ALTER COLUMN DataSetId NVARCHAR(100) NOT NULL;
   ```

### Phase 2: データ移行（推定2時間）
```sql
-- 1. DataSetsからDataSetManagementへのデータ移行
INSERT INTO DataSetManagement (
    DataSetId, JobDate, ProcessType, ImportType, RecordCount, TotalRecordCount,
    IsActive, IsArchived, CreatedAt, CreatedBy, Department, Notes
)
SELECT 
    Id,
    JobDate,
    COALESCE(DataSetType, 'UNKNOWN') AS ProcessType,
    'LEGACY' AS ImportType,
    COALESCE(RecordCount, 0) AS RecordCount,
    COALESCE(RecordCount, 0) AS TotalRecordCount,
    CASE WHEN Status IN ('Completed', 'Imported') THEN 1 ELSE 0 END AS IsActive,
    CASE WHEN Status = 'Error' THEN 1 ELSE 0 END AS IsArchived,
    CreatedAt,
    'migration' AS CreatedBy,
    'DeptA' AS Department,
    CASE 
        WHEN Name IS NOT NULL AND Description IS NOT NULL THEN Name + '\n' + Description
        WHEN Description IS NOT NULL THEN Description
        WHEN Name IS NOT NULL THEN 'Name: ' + Name
        ELSE NULL
    END AS Notes
FROM DataSets
WHERE NOT EXISTS (
    SELECT 1 FROM DataSetManagement dsm WHERE dsm.DataSetId = DataSets.Id
);

-- 2. 外部キー制約の更新
ALTER TABLE SalesVouchers DROP CONSTRAINT FK_SalesVouchers_DataSets;
ALTER TABLE PurchaseVouchers DROP CONSTRAINT FK_PurchaseVouchers_DataSets;
ALTER TABLE InventoryAdjustments DROP CONSTRAINT FK_InventoryAdjustments_DataSets;

-- 3. 新しい外部キー制約の作成（WITH NOCHECK for performance）
ALTER TABLE SalesVouchers WITH NOCHECK ADD CONSTRAINT FK_SalesVouchers_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);

ALTER TABLE PurchaseVouchers WITH NOCHECK ADD CONSTRAINT FK_PurchaseVouchers_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);

ALTER TABLE InventoryAdjustments WITH NOCHECK ADD CONSTRAINT FK_InventoryAdjustments_DataSetManagement 
    FOREIGN KEY (DataSetId) REFERENCES DataSetManagement(DataSetId);

-- 4. 制約の有効化
ALTER TABLE SalesVouchers CHECK CONSTRAINT FK_SalesVouchers_DataSetManagement;
ALTER TABLE PurchaseVouchers CHECK CONSTRAINT FK_PurchaseVouchers_DataSetManagement;
ALTER TABLE InventoryAdjustments CHECK CONSTRAINT FK_InventoryAdjustments_DataSetManagement;
```

### Phase 3: アプリケーション変更（推定3日）
1. **フィーチャーフラグの実装**
   ```csharp
   // appsettings.json
   "Features": {
       "UseDataSetManagementOnly": false
   }
   ```

2. **DI登録の条件分岐**
   ```csharp
   // Program.cs
   if (configuration.GetValue<bool>("Features:UseDataSetManagementOnly"))
   {
       builder.Services.AddScoped<IDataSetService, DataSetManagementService>();
   }
   else
   {
       builder.Services.AddScoped<IDataSetService, UnifiedDataSetService>();
   }
   ```

3. **段階的な切り替え**
   - 書き込み処理を先にDataSetManagementに変更
   - 読み取り処理を後からDataSetManagementに変更
   - フィーチャーフラグで動的切り替え

### Phase 4: 検証と最終化（推定1日）
1. **機能テスト**
   ```bash
   # 各種インポート処理のテスト
   dotnet run import-sales test-data.csv
   dotnet run import-purchase test-data.csv
   dotnet run import-adjustment test-data.csv
   ```

2. **データ整合性確認**
   ```sql
   -- 伝票データの外部キー整合性確認
   SELECT COUNT(*) FROM SalesVouchers sv
   LEFT JOIN DataSetManagement dsm ON sv.DataSetId = dsm.DataSetId
   WHERE dsm.DataSetId IS NULL; -- 0件であることを確認
   ```

3. **旧テーブルの廃止**
   ```sql
   -- 十分な検証後にDataSetsテーブルを削除
   DROP TABLE DataSets;
   ```

## 6. リスクと対策

### 高リスク項目

| リスク | 影響度 | 対策 |
|--------|--------|------|
| 外部キー制約エラー | 高 | 事前の依存関係確認、WITH NOCHECKオプション使用 |
| データ損失 | 高 | 完全バックアップ、段階的移行、検証スクリプト |
| 長時間のダウンタイム | 高 | フィーチャーフラグ、段階的移行 |
| アプリケーションの隠れたバグ | 高 | 広範囲な機能テスト、段階的切り替え |

### 中リスク項目

| リスク | 影響度 | 対策 |
|--------|--------|------|
| パフォーマンス劣化 | 中 | インデックス最適化、クエリ性能測定 |
| データ型不整合 | 中 | 事前のスキーマ統一、変換テスト |
| 複雑性の一時的増加 | 中 | 明確なドキュメント、段階的簡素化 |

## 7. ロールバック計画

### データベース
```sql
-- 緊急時のロールバック手順
-- 1. 外部キー制約をDataSetsに戻す
ALTER TABLE SalesVouchers DROP CONSTRAINT FK_SalesVouchers_DataSetManagement;
ALTER TABLE PurchaseVouchers DROP CONSTRAINT FK_PurchaseVouchers_DataSetManagement;
ALTER TABLE InventoryAdjustments DROP CONSTRAINT FK_InventoryAdjustments_DataSetManagement;

-- 2. DataSetsテーブルの復元（バックアップから）
RESTORE DATABASE InventoryManagementDB FROM DISK = 'C:\Backup\InventoryManagementDB_PreMigration.bak'
WITH REPLACE;
```

### アプリケーション
```bash
# Gitリバート
git revert [commit-hash]

# フィーチャーフラグでの即座の切り戻し
# appsettings.json
"Features": {
    "UseDataSetManagementOnly": false
}
```

## 8. 推定工数

| フェーズ | タスク | 工数 | 備考 |
|---------|--------|------|------|
| Phase 1 | 準備・バックアップ | 4h | |
| Phase 1 | スキーマ変更 | 4h | |
| Phase 2 | データ移行スクリプト作成 | 8h | |
| Phase 2 | データ移行実行 | 2h | |
| Phase 3 | エンティティ・リポジトリ変更 | 16h | |
| Phase 3 | サービス層変更 | 8h | |
| Phase 3 | DI登録・設定変更 | 4h | |
| Phase 4 | テスト修正 | 16h | |
| Phase 4 | 検証・調整 | 8h | |
| **合計** | | **70h** | **8.75人日** |

## 9. 推奨実装順序（Gemini推奨）

### 1. UnifiedDataSetServiceの改修
- 二重書き込みをDataSetManagementのみに変更
- 読み取り処理は当面両方をサポート

### 2. 新規インポートサービスの作成
- DataSetManagementのみを使用する新しいサービス
- 既存サービスと並行稼働

### 3. 段階的サービス移行
- フィーチャーフラグで動的切り替え
- 1つずつサービスを移行

### 4. 最終統合
- 全サービスの移行完了後、DataSetsテーブル削除
- UnifiedDataSetServiceの簡素化

## 10. 成功基準・チェックポイント

### 必須確認項目
- [ ] すべての外部キー制約が正しく参照されている
- [ ] データ移行で1件も欠損していない
- [ ] すべてのインポートサービスが正常動作する
- [ ] パフォーマンステストで劣化が5%以内
- [ ] ロールバック手順の動作確認完了

### 品質指標
- **データ整合性**: 100%（欠損0件）
- **機能カバレッジ**: 100%（全機能正常動作）
- **パフォーマンス**: 劣化5%以内
- **ダウンタイム**: 30分以内

---

**調査担当**: Claude Code + Gemini CLI
**調査期間**: 2025-07-18
**調査方法**: 静的コード解析、データベーススキーマ分析、専門家相談
**推奨実装**: 段階的移行アプローチ（Gemini推奨）
**優先度**: 高（クライアント納品品質向上のため）