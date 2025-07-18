# マスタテーブル完全移行状況調査結果

生成日時: 2025-07-18 14:20:00

## エグゼクティブサマリー
- **現在の状態**: システム全体で`CreatedAt/UpdatedAt`と`CreatedDate/UpdatedDate`が混在
- **推奨される方向性**: `CreatedDate/UpdatedDate`への統一を推奨
- **影響範囲**: エンティティクラス10個、リポジトリクラス10個以上、マイグレーションファイル30個以上

## 1. データベース層の現状

### 1.1 マイグレーションファイルでの使用パターン

| ファイル | 使用パターン | 状況 |
|---------|-------------|------|
| 024_CreateProductMaster.sql | `CreatedDate/UpdatedDate` | マスタテーブル初期作成 |
| 035_AddAllMissingTables.sql | `CreatedAt/UpdatedAt` | 新規テーブル群作成 |
| 051_Phase2_AddNewColumns.sql | `CreatedAt/UpdatedAt` | 移行用新カラム追加 |
| 052_Phase3_MigrateDataAndSync.sql | 両方対応 | データ移行処理 |
| 053_Phase5_Cleanup.sql | 古いカラム削除 | クリーンアップ処理 |

### 1.2 システム全体の傾向
- **CreatedAt/UpdatedAt使用**: 20個以上のテーブル（新規作成分）
- **CreatedDate/UpdatedDate使用**: 既存の主要マスタテーブル（3個）
- **主流パターン**: 新規作成テーブルは`CreatedAt/UpdatedAt`、既存テーブルは`CreatedDate/UpdatedDate`

## 2. アプリケーション層の現状

### 2.1 エンティティクラスの状況

| クラス名 | 作成日プロパティ | 更新日プロパティ | 状況 |
|---------|----------------|----------------|------|
| **ProductMaster** | `CreatedDate` | `UpdatedDate` | ✅修正済み |
| **CustomerMaster** | `CreatedDate` | `UpdatedDate` | ✅修正済み |
| **SupplierMaster** | `CreatedDate` | `UpdatedDate` | ✅修正済み |
| UnitMaster | `CreatedAt` | `UpdatedAt` | ❌未修正 |
| GradeMaster | `CreatedAt` | `UpdatedAt` | ❌未修正 |
| ClassMaster | `CreatedAt` | `UpdatedAt` | ❌未修正 |
| ShippingMarkMaster | なし | なし | ❌日付プロパティなし |
| RegionMaster | `CreatedAt` | `UpdatedAt` | ❌未修正 |
| StaffMaster | `CreatedAt` | `UpdatedAt` | ❌未修正 |

### 2.2 リポジトリSQL文の状況

| リポジトリ | INSERT文での使用 | UPDATE文での使用 | 状況 |
|-----------|-----------------|-----------------|------|
| **ProductMasterRepository** | `@CreatedAt, @UpdatedAt` | `UpdatedAt = GETDATE()` | ❌不整合 |
| **CustomerMasterRepository** | `@CreatedAt, @UpdatedAt` | `UpdatedAt = GETDATE()` | ❌不整合 |
| **SupplierMasterRepository** | `@CreatedAt, @UpdatedAt` | `UpdatedAt = GETDATE()` | ❌不整合 |
| GradeMasterRepository | `@CreatedAt, @UpdatedAt` | `UpdatedAt = @UpdatedAt` | ✅整合 |
| DataSetRepository | `@CreatedAt, @UpdatedAt` | `UpdatedAt = @UpdatedAt` | ✅整合 |
| ReceiptVoucherRepository | `@CreatedAt, @UpdatedAt` | `UpdatedAt = @UpdatedAt` | ✅整合 |
| PaymentVoucherRepository | `@CreatedAt, @UpdatedAt` | `UpdatedAt = @UpdatedAt` | ✅整合 |

## 3. 不整合箇所の特定

### 3.1 レイヤー間の不整合（最重要問題）

#### ProductMaster系統
- **エンティティ**: `CreatedDate`, `UpdatedDate` ✅
- **リポジトリSQL**: `@CreatedAt`, `@UpdatedAt` ❌
- **データベーステーブル**: `CreatedDate`, `UpdatedDate` ✅

#### CustomerMaster系統
- **エンティティ**: `CreatedDate`, `UpdatedDate` ✅
- **リポジトリSQL**: `@CreatedAt`, `@UpdatedAt` ❌
- **データベーステーブル**: `CreatedDate`, `UpdatedDate` ✅

#### SupplierMaster系統
- **エンティティ**: `CreatedDate`, `UpdatedDate` ✅
- **リポジトリSQL**: `@CreatedAt`, `@UpdatedAt` ❌
- **データベーステーブル**: `CreatedDate`, `UpdatedDate` ✅

### 3.2 影響を受けるコンポーネント

#### 🚨 緊急修正が必要（SQL Error 207の原因）
1. **ProductMasterRepository**: 12箇所のSQL修正
2. **CustomerMasterRepository**: 12箇所のSQL修正  
3. **SupplierMasterRepository**: 12箇所のSQL修正

#### ⚠️ 将来的な整合性確保が必要
1. **UnitMaster**: エンティティとデータベースの不整合
2. **GradeMaster**: エンティティとデータベースの不整合
3. **ClassMaster**: エンティティとデータベースの不整合
4. **その他マスタ**: 新規作成テーブルとの一貫性

## 4. 完全移行のための推奨事項

### 4.1 推奨される統一方針

**推奨**: `CreatedDate/UpdatedDate`への統一

**理由**:
1. **既存データの保護**: 主要マスタテーブル（ProductMaster, CustomerMaster, SupplierMaster）は既に`CreatedDate/UpdatedDate`で運用中
2. **データ移行リスク最小化**: フェーズド・マイグレーションで`CreatedDate/UpdatedDate`への移行が完了済み
3. **コード変更範囲の最小化**: リポジトリSQL文の修正のみで解決可能

### 4.2 段階的移行手順

#### フェーズ1: 緊急修正（SQL Error 207 解決）
1. ProductMasterRepository SQL修正: `@CreatedAt/@UpdatedAt` → `@CreatedDate/@UpdatedDate`
2. CustomerMasterRepository SQL修正: `@CreatedAt/@UpdatedAt` → `@CreatedDate/@UpdatedDate`
3. SupplierMasterRepository SQL修正: `@CreatedAt/@UpdatedAt` → `@CreatedDate/@UpdatedDate`

#### フェーズ2: エンティティクラス統一
1. UnitMaster: `CreatedAt/UpdatedAt` → `CreatedDate/UpdatedDate`
2. GradeMaster: `CreatedAt/UpdatedAt` → `CreatedDate/UpdatedDate`
3. ClassMaster: `CreatedAt/UpdatedAt` → `CreatedDate/UpdatedDate`
4. 対応するリポジトリSQL文も同時修正

#### フェーズ3: データベーススキーマ統一
1. 新規テーブルのカラム名変更: `CreatedAt/UpdatedAt` → `CreatedDate/UpdatedDate`
2. マイグレーションスクリプトの更新
3. システム全体での一貫性確保

### 4.3 リスクと考慮事項

#### 高リスク
- **データ損失の可能性**: マイグレーション実行中のデータ不整合
- **ダウンタイム**: 大規模なスキーマ変更時の停止時間

#### 中リスク  
- **テスト範囲**: すべてのマスタテーブル操作の網羅的テスト必要
- **ロールバック準備**: 各段階でのバックアップとロールバック手順

#### 低リスク
- **コンパイルエラー**: エンティティプロパティ名変更時の一時的なエラー

## 5. 詳細データ

### 5.1 マイグレーションファイル分析

#### 古いスキーマ使用（CreatedDate/UpdatedDate）
- `024_CreateProductMaster.sql`: 主要マスタテーブル作成
- `create_schema.sql`: 基本スキーマ定義

#### 新しいスキーマ使用（CreatedAt/UpdatedAt）  
- `035_AddAllMissingTables.sql`: 新規テーブル群
- `05_create_master_tables.sql`: 追加マスタテーブル群

#### 移行対応
- `051_Phase2_AddNewColumns.sql`: 新カラム追加
- `052_Phase3_MigrateDataAndSync.sql`: データ移行
- `053_Phase5_Cleanup.sql`: 古いカラム削除

### 5.2 修正が必要なSQL文の詳細

#### ProductMasterRepository.cs
```sql
-- 現在（エラーの原因）
INSERT INTO ProductMaster (..., CreatedAt, UpdatedAt) VALUES (..., @CreatedAt, @UpdatedAt)

-- 修正後
INSERT INTO ProductMaster (..., CreatedDate, UpdatedDate) VALUES (..., @CreatedDate, @UpdatedDate)
```

#### CustomerMasterRepository.cs
```sql  
-- 現在（エラーの原因）
INSERT INTO CustomerMaster (..., CreatedAt, UpdatedAt) VALUES (..., @CreatedAt, @UpdatedAt)

-- 修正後
INSERT INTO CustomerMaster (..., CreatedDate, UpdatedDate) VALUES (..., @CreatedDate, @UpdatedDate)
```

#### SupplierMasterRepository.cs
```sql
-- 現在（エラーの原因）
INSERT INTO SupplierMaster (..., CreatedAt, UpdatedAt) VALUES (..., @CreatedAt, @UpdatedAt)

-- 修正後  
INSERT INTO SupplierMaster (..., CreatedDate, UpdatedDate) VALUES (..., @CreatedDate, @UpdatedDate)
```

## 6. 実装ロードマップ

### 即座に実施（SQL Error 207 解決）
1. **リポジトリSQL修正**: 3ファイル × 4箇所 = 12箇所
2. **コンパイル確認**: エラーなしを確認
3. **import-folder テスト**: 動作確認

### 1週間以内（システム一貫性確保）
1. **エンティティクラス統一**: 6クラスのプロパティ名変更
2. **対応リポジトリ修正**: SQL文の同期
3. **包括的テスト**: 全マスタテーブル操作の確認

### 1ヶ月以内（完全移行完了）
1. **データベーススキーマ統一**: 新規テーブルのカラム名変更
2. **マイグレーション整理**: 不要なマイグレーションファイル削除
3. **ドキュメント更新**: 開発ガイドラインの更新

## 結論

**SQL Error 207の即座の解決には、3つのマスタリポジトリでのSQL文修正（12箇所）が必要です。**

長期的には、システム全体を`CreatedDate/UpdatedDate`パターンに統一することで、一貫性のある保守しやすいコードベースを実現できます。