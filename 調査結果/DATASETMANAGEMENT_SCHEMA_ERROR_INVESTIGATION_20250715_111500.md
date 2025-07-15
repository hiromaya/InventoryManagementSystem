# DatasetManagementテーブル スキーマ不一致エラー調査報告書

**調査日時**: 2025年7月15日 11:15:00  
**調査者**: Claude Code  
**エラー発生コマンド**: `import-initial-inventory DeptA`  
**エラー概要**: DatasetManagementテーブルに必要なカラムが存在しない

## 1. エラー内容

```
Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid column name 'ImportType'.
Invalid column name 'RecordCount'.
Invalid column name 'IsActive'.
Invalid column name 'IsArchived'.
Invalid column name 'ParentDataSetId'.
Invalid column name 'Notes'.
Invalid column name 'Department'.
```

## 2. 根本原因

**データベーススキーマとアプリケーションコードの不一致**

### 2.1 期待される状態
アプリケーションコード（`DatasetManagement.cs`）では以下のカラムが必要：
- ImportType
- RecordCount
- IsActive
- IsArchived
- ParentDataSetId
- Notes
- Department

### 2.2 実際の状態
データベースのDatasetManagementテーブルに上記カラムが存在しない。

## 3. 解決策

### 3.1 既存のマイグレーションスクリプト
`database/migrations/008_UpdateDatasetManagement.sql`が存在し、以下のカラムを追加する処理が含まれています：
- ImportType
- RecordCount
- IsActive
- IsArchived
- ParentDataSetId
- Notes
- CreatedBy

**ただし、Departmentカラムの追加は含まれていません。**

### 3.2 必要なアクション

#### 即座の解決方法
1. **既存のマイグレーションスクリプトを実行**
   ```sql
   -- SQL Server Management StudioまたはAzure Data Studioで実行
   -- database/migrations/008_UpdateDatasetManagement.sql の内容を実行
   ```

2. **Departmentカラムを手動で追加**
   ```sql
   -- Departmentカラムの追加（マイグレーションスクリプトに含まれていないため）
   IF NOT EXISTS (SELECT * FROM sys.columns 
                  WHERE object_id = OBJECT_ID(N'[dbo].[DatasetManagement]') 
                  AND name = 'Department')
   BEGIN
       ALTER TABLE DatasetManagement
       ADD Department NVARCHAR(50) NOT NULL DEFAULT 'DeptA';
   END
   ```

## 4. 詳細分析

### 4.1 マイグレーションスクリプトの内容
`008_UpdateDatasetManagement.sql`は以下の処理を行います：
1. 不足しているカラムを条件付きで追加（IF NOT EXISTS）
2. 既存データのImportTypeを適切に設定
3. RecordCountをTotalRecordCountから設定

### 4.2 Departmentカラムの欠落
- エンティティクラスには`Department`プロパティが存在（デフォルト値: "DeptA"）
- マイグレーションスクリプトにはDepartmentカラムの追加が含まれていない
- これは開発中に後から追加されたプロパティの可能性が高い

## 5. 推奨事項

### 5.1 短期的対応
1. 既存の`008_UpdateDatasetManagement.sql`を実行
2. Departmentカラムを手動で追加（上記SQL参照）
3. `import-initial-inventory`コマンドを再実行

### 5.2 長期的対応
1. **新しいマイグレーションスクリプトの作成**
   - `009_AddDepartmentToDatasetManagement.sql`を作成
   - Departmentカラムの追加を正式にマイグレーション管理に含める

2. **マイグレーション管理の改善**
   - データベーススキーマの変更履歴を適切に管理
   - エンティティクラスの変更時は必ずマイグレーションスクリプトを作成

## 6. 検証事項

### 6.1 他の環境への影響
- 開発環境では問題なく動作していた可能性
- 本番環境や他の開発者の環境でも同様の問題が発生する可能性

### 6.2 データの整合性
- Departmentカラム追加時のデフォルト値（'DeptA'）が適切か確認
- 既存データがある場合の影響を検討

## 7. 結論

このエラーは、データベーススキーマの更新が適切に実行されていないことが原因です。特に：
1. `008_UpdateDatasetManagement.sql`が未実行
2. Departmentカラムがマイグレーションスクリプトに含まれていない

上記の解決策を実行することで、エラーは解消されます。

---

**注意**: データベース変更を実行する前に、必ずバックアップを取得してください。