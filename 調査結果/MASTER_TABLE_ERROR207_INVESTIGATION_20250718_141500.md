# マスタテーブルSQL Error 207調査結果

生成日時: 2025-07-18 14:15:00

## 🚨 根本原因の特定

### 問題概要
**SQLエラー207（Invalid column name）の根本原因は、リポジトリクラスのSQL文で使用しているカラム名と、実際のエンティティクラスのプロパティ名の不一致です。**

## 1. エンティティクラスの現在の状態

### ProductMaster.cs
- `CreatedDate` (DateTime) ✅
- `UpdatedDate` (DateTime) ✅

### CustomerMaster.cs  
- `CreatedDate` (DateTime) ✅
- `UpdatedDate` (DateTime) ✅

### SupplierMaster.cs
- `CreatedDate` (DateTime) ✅ 
- `UpdatedDate` (DateTime) ✅

## 2. リポジトリSQL文の分析

### 🚨 ProductMasterRepository (重大な不一致)

**SQL文で使用されているカラム名:**
```sql
-- 行70-77: InsertBulkAsync
CreatedAt, UpdatedAt  ❌

-- 行257-258: UpsertAsync INSERT部分  
CreatedAt, UpdatedAt  ❌
```

**エンティティクラスのプロパティ名:**
```csharp
CreatedDate, UpdatedDate  ✅
```

### 🚨 CustomerMasterRepository (重大な不一致)

**SQL文で使用されているカラム名:**
```sql
-- 行69, 74: InsertBulkAsync
CreatedAt, UpdatedAt  ❌

-- 行241-242: UpsertAsync INSERT部分
CreatedAt, UpdatedAt  ❌
```

**エンティティクラスのプロパティ名:**
```csharp
CreatedDate, UpdatedDate  ✅
```

### 🚨 SupplierMasterRepository (重大な不一致)

**SQL文で使用されているカラム名:**
```sql
-- 行68, 73: InsertBulkAsync  
CreatedAt, UpdatedAt  ❌

-- 行225-226: UpsertAsync INSERT部分
CreatedAt, UpdatedAt  ❌
```

**エンティティクラスのプロパティ名:**
```csharp
CreatedDate, UpdatedDate  ✅
```

## 3. エラー発生のメカニズム

### 問題の詳細
1. **リポジトリのSQL文**: `@CreatedAt`, `@UpdatedAt` パラメータを使用
2. **エンティティクラス**: `CreatedDate`, `UpdatedDate` プロパティを持つ
3. **Dapperの動作**: `@CreatedAt` パラメータに対応する `CreatedAt` プロパティが見つからない
4. **結果**: SQL Error 207 "Invalid column name 'CreatedAt'" が発生

### 影響するメソッド
- `InsertBulkAsync` - 一括挿入時
- `UpsertAsync` - 挿入・更新時
- `UpsertBulkAsync` - 一括挿入・更新時

## 4. 移行作業の矛盾

### 実施済みの変更
- ✅ エンティティクラス: `CreatedAt/UpdatedAt` → `CreatedDate/UpdatedDate`
- ✅ ImportService: `CreatedAt/UpdatedAt` → `CreatedDate/UpdatedDate`

### 未実施の変更
- ❌ リポジトリSQL文: 依然として `CreatedAt/UpdatedAt` を使用

## 5. 修正が必要な箇所

### ProductMasterRepository.cs
```sql
-- 行70, 77: 修正前
CreatedAt, UpdatedAt
-- 修正後  
CreatedDate, UpdatedDate

-- 行257, 258: 修正前
CreatedAt, UpdatedAt
-- 修正後
CreatedDate, UpdatedDate
```

### CustomerMasterRepository.cs
```sql
-- 行69, 74: 修正前
CreatedAt, UpdatedAt
-- 修正後
CreatedDate, UpdatedDate

-- 行241, 242: 修正前  
CreatedAt, UpdatedAt
-- 修正後
CreatedDate, UpdatedDate
```

### SupplierMasterRepository.cs  
```sql
-- 行68, 73: 修正前
CreatedAt, UpdatedAt
-- 修正後
CreatedDate, UpdatedDate

-- 行225, 226: 修正前
CreatedAt, UpdatedAt
-- 修正後
CreatedDate, UpdatedDate
```

## 6. データベーステーブル構造への影響

### 重要な注意点
- 実際のデータベーステーブルは `CreatedDate/UpdatedDate` カラムを持つ
- SQL文の修正により、正しいカラムが参照される
- migrate-phase3/5 の移行作業結果と整合性が取れる

## 7. 修正優先度

### 🔴 最高優先度（即座に修正が必要）
1. ProductMasterRepository.cs - 4箇所のSQL修正
2. CustomerMasterRepository.cs - 4箇所のSQL修正  
3. SupplierMasterRepository.cs - 4箇所のSQL修正

### 修正後の期待結果
- ✅ import-folder コマンドが正常実行される
- ✅ マスタデータの一括インポートが機能する
- ✅ SQL Error 207 が完全に解消される

## 8. テスト計画

### 修正後の検証手順
1. リポジトリ修正の実装
2. コンパイル確認（エラーなし）
3. `import-folder` コマンドの実行テスト
4. マスタデータのインポート確認

## 結論

**SQL Error 207 の根本原因は、migrate-phase3/5 実行後のエンティティクラス変更（CreatedAt/UpdatedAt → CreatedDate/UpdatedDate）に対して、リポジトリクラスのSQL文が更新されていないことです。**

3つのマスタリポジトリで合計12箇所のSQL修正が必要です。この修正により、SQLエラー207は完全に解決されます。