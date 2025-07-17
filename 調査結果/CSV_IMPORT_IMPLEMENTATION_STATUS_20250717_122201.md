# CSV Import Implementation Status - 調査結果

**調査日時**: 2025年7月17日 12:22:01  
**調査対象**: import-folderコマンドの分類マスタファイル処理エラー  
**エラー**: 「未対応のCSVファイル形式」により分類マスタファイルが処理されない問題

## 🔍 問題の概要

### 発生している問題
```
処理中: 商品分類１.csv
⚠️ 商品分類１.csv は現在未対応です（スキップ）
```

```
処理中: 得意先分類１.csv
⚠️ 得意先分類１.csv は現在未対応です（スキップ）
```

### 対象ファイル
- 商品分類１.csv ～ 商品分類３.csv
- 得意先分類１.csv ～ 得意先分類５.csv
- 仕入先分類１.csv ～ 仕入先分類３.csv
- 担当者分類１.csv

## 📋 調査結果詳細

### 1. サービスクラスの実装状況

#### ✅ 実装済みサービス
以下のサービスクラスはすべて実装済み：

| ファイル名 | サービスクラス | 実装場所 |
|-----------|---------------|----------|
| 商品分類１.csv | ProductCategory1ImportService | /src/InventorySystem.Import/Services/Masters/ProductCategory1ImportService.cs |
| 商品分類２.csv | ProductCategory2ImportService | /src/InventorySystem.Import/Services/Masters/ProductCategory2ImportService.cs |
| 商品分類３.csv | ProductCategory3ImportService | /src/InventorySystem.Import/Services/Masters/ProductCategory3ImportService.cs |
| 得意先分類１.csv | CustomerCategory1ImportService | /src/InventorySystem.Import/Services/Masters/CustomerCategoryImportServices.cs |
| 得意先分類２.csv | CustomerCategory2ImportService | /src/InventorySystem.Import/Services/Masters/CustomerCategoryImportServices.cs |
| 得意先分類３.csv | CustomerCategory3ImportService | /src/InventorySystem.Import/Services/Masters/CustomerCategoryImportServices.cs |
| 得意先分類４.csv | CustomerCategory4ImportService | /src/InventorySystem.Import/Services/Masters/CustomerCategoryImportServices.cs |
| 得意先分類５.csv | CustomerCategory5ImportService | /src/InventorySystem.Import/Services/Masters/CustomerCategoryImportServices.cs |
| 仕入先分類１.csv | SupplierCategory1ImportService | /src/InventorySystem.Import/Services/Masters/SupplierCategoryImportServices.cs |
| 仕入先分類２.csv | SupplierCategory2ImportService | /src/InventorySystem.Import/Services/Masters/SupplierCategoryImportServices.cs |
| 仕入先分類３.csv | SupplierCategory3ImportService | /src/InventorySystem.Import/Services/Masters/SupplierCategoryImportServices.cs |
| 担当者分類１.csv | StaffCategory1ImportService | /src/InventorySystem.Import/Services/Masters/StaffMasterImportService.cs |

#### 🔧 実装特徴
すべてのサービスは`MasterImportServiceBase<TEntity, TModel>`を継承し、以下の機能を持つ：
- `FileNamePattern`プロパティでファイル名パターンを定義
- `ServiceName`プロパティでサービス名を定義
- `ProcessOrder`プロパティで処理順序を定義
- 一括削除→一括挿入の処理フロー

### 2. エラーの根本原因

#### 🚨 原因1: DIコンテナへの登録不足
**場所**: `/src/InventorySystem.Console/Program.cs`

分類マスタサービスはDIコンテナに登録されていない：
```csharp
// 現在の登録状況（抜粋）
builder.Services.AddScoped<IGradeMasterImportService, GradeMasterImportService>();
builder.Services.AddScoped<IClassMasterImportService, ClassMasterImportService>();
// ❌ 分類マスタサービスの登録が不足
```

#### 🚨 原因2: import-folderでの処理ロジック不足
**場所**: `/src/InventorySystem.Console/Program.cs` - `ExecuteImportFromFolderAsync`メソッド

分類マスタを処理するコードが存在しない：
```csharp
// 現在の処理分岐（抜粋）
if (fileName.Contains("等級汎用マスター")) { /* 処理あり */ }
if (fileName.Contains("階級汎用マスター")) { /* 処理あり */ }
// ❌ 分類マスタの処理分岐が不足
```

#### 🚨 原因3: knownButUnsupported配列の問題
**場所**: `/src/InventorySystem.Console/Program.cs` 2314-2317行

実装済みのサービスが「未対応」リストに含まれている：
```csharp
string[] knownButUnsupported = {
    "担当者", "単位", "商品分類", "得意先分類", 
    "仕入先分類", "担当者分類", "支払伝票", "入金伝票"
};
```

### 3. 処理順序の未定義

**場所**: `/src/InventorySystem.Console/Program.cs` - `GetFileProcessOrder`メソッド

分類マスタの処理順序が定義されていない：
```csharp
private static int GetFileProcessOrder(string fileName)
{
    // Phase 1: マスタファイル（優先度1-8）
    if (fileName.Contains("等級汎用マスター")) return 1;
    if (fileName.Contains("階級汎用マスター")) return 2;
    if (fileName.Contains("荷印汎用マスター")) return 3;
    if (fileName.Contains("産地汎用マスター")) return 4;
    if (fileName == "商品.csv") return 5;
    if (fileName == "得意先.csv") return 6;
    if (fileName == "仕入先.csv") return 7;
    if (fileName == "単位.csv") return 8;
    
    // ❌ 分類マスタの処理順序が未定義
    // 結果：優先度99（その他）として処理される
    
    return 99;
}
```

### 4. ファイル移動処理の現状

#### MoveToErrorAsyncメソッドの実装
**場所**: `/src/InventorySystem.Core/Services/FileManagementService.cs`

```csharp
public async Task MoveToErrorAsync(string filePath, string department, string errorMessage)
{
    // エラーファイルをタイムスタンプ付きでErrorフォルダに移動
    // 例: 20250717_123456_商品分類１.csv
    // エラー内容を.error.txtファイルとして記録
}
```

#### 実際の処理状況
Program.csでは`MoveToErrorAsync`呼び出しが**すべてコメントアウト**されている：
```csharp
// await fileService.MoveToErrorAsync(file, department, "未対応のCSVファイル形式");
```

**理由**: 複数日付のデータを処理可能にするため、エラーファイルも移動せずに保持

### 5. 実装と設定の乖離

| 項目 | 実装状況 | 設定状況 | 結果 |
|------|----------|----------|------|
| サービスクラス | ✅ 実装済み | ❌ 未設定 | 呼び出し不可 |
| エンティティクラス | ✅ 実装済み | ✅ 設定済み | 正常 |
| リポジトリクラス | ✅ 実装済み | ✅ 設定済み | 正常 |
| DIコンテナ登録 | ✅ 実装済み | ❌ 未設定 | 解決不可 |
| import-folder処理 | ✅ 実装済み | ❌ 未設定 | 処理されない |

## 🔧 解決策

### 即座に実装可能な修正項目

#### 1. DIコンテナへの登録追加
```csharp
// Program.cs のDIコンテナセットアップ箇所に追加
builder.Services.AddScoped<IImportService, ProductCategory1ImportService>();
builder.Services.AddScoped<IImportService, ProductCategory2ImportService>();
builder.Services.AddScoped<IImportService, ProductCategory3ImportService>();
// ... 他の分類マスタサービスも同様に追加
```

#### 2. import-folderでの処理ロジック追加
```csharp
// ExecuteImportFromFolderAsync メソッドに分類マスタ処理分岐を追加
else if (fileName.Contains("商品分類"))
{
    // 商品分類マスタ処理
}
else if (fileName.Contains("得意先分類"))
{
    // 得意先分類マスタ処理
}
// ... 他の分類マスタも同様に追加
```

#### 3. knownButUnsupported配列の修正
```csharp
string[] knownButUnsupported = {
    "担当者", "単位", "支払伝票", "入金伝票"
    // ❌ 削除: "商品分類", "得意先分類", "仕入先分類", "担当者分類"
};
```

#### 4. 処理順序の定義
```csharp
// GetFileProcessOrder メソッドに追加
if (fileName.Contains("商品分類")) return 9;
if (fileName.Contains("得意先分類")) return 10;
if (fileName.Contains("仕入先分類")) return 11;
if (fileName.Contains("担当者分類")) return 12;
```

### 修正対象ファイル
- `/src/InventorySystem.Console/Program.cs`
  - DIコンテナセットアップ箇所
  - `ExecuteImportFromFolderAsync`メソッド
  - `GetFileProcessOrder`メソッド
  - `knownButUnsupported`配列

## 📊 影響範囲

### 修正後の期待結果
```
処理中: 商品分類１.csv
✅ 商品分類マスタとして処理完了

処理中: 得意先分類１.csv
✅ 得意先分類マスタとして処理完了
```

### 処理順序（修正後）
1. 等級汎用マスター（優先度1）
2. 階級汎用マスター（優先度2）
3. 荷印汎用マスター（優先度3）
4. 産地汎用マスター（優先度4）
5. 商品.csv（優先度5）
6. 得意先.csv（優先度6）
7. 仕入先.csv（優先度7）
8. 単位.csv（優先度8）
9. **商品分類１-３.csv（優先度9）** ← 新規追加
10. **得意先分類１-５.csv（優先度10）** ← 新規追加
11. **仕入先分類１-３.csv（優先度11）** ← 新規追加
12. **担当者分類１.csv（優先度12）** ← 新規追加
13. 前月末在庫.csv（優先度13）
14. 売上伝票.csv（優先度20）
15. 仕入伝票.csv（優先度21）
16. 在庫調整.csv（優先度22）

## 🎯 結論

**問題の本質**: 技術的な実装は完了しているが、**設定面での接続が不足**している

**解決の容易さ**: 高い（コード実装は不要、設定追加のみ）

**修正工数**: 約30分（DIコンテナ登録、処理分岐追加、配列修正）

**影響範囲**: 限定的（Program.csのみ）

**リスク**: 低い（既存処理への影響なし）

---

**次のステップ**: 上記の修正項目を順次実装し、import-folderコマンドで分類マスタファイルが正常に処理されることを確認する。