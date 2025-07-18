# 入金・支払伝票サービス未登録問題の調査結果

**調査日時**: 2025-07-17 14:05  
**調査者**: Claude Code  
**問題**: ReceiptVoucherImportService と PaymentVoucherImportService が見つからないエラー

## 🔍 問題の概要

実行時エラー:
```
❌ サービスが見つかりません: ReceiptVoucherImportService
❌ サービスが見つかりません: PaymentVoucherImportService
```

## 📋 詳細調査結果

### 1. サービス実装状況 ✅
以下のサービスクラスは正常に実装されている:
- `/src/InventorySystem.Import/Services/ReceiptVoucherImportService.cs` ✅
- `/src/InventorySystem.Import/Services/PaymentVoucherImportService.cs` ✅  
- `/src/InventorySystem.Import/Services/Masters/StaffCategory1ImportService.cs` ✅

### 2. DI登録定義状況 ✅
`ImportServiceExtensions.cs` で正しく定義されている:
```csharp
// 入金・支払伝票
services.AddScoped<IImportService, ReceiptVoucherImportService>();
services.AddScoped<IImportService, PaymentVoucherImportService>();
```

### 3. **🚨 根本原因発見: DI登録呼び出しが存在しない**

**重大な問題**: `Program.cs` で `AddImportServices` 拡張メソッドが呼び出されていない

#### 調査結果:
- ✅ `ImportServiceExtensions.cs` は存在し、正しいサービス登録を含む
- ❌ `Program.cs` で `services.AddImportServices(connectionString)` の呼び出しが**存在しない**
- ❌ `using InventorySystem.Import.Services;` はあるが、拡張メソッドが使用されていない

#### Program.cs の現状:
```csharp
// 個別のリポジトリやサービスは登録されているが、
// ImportServiceExtensions.AddImportServices() の呼び出しがない
```

### 4. サービス検索ロジック確認 ✅
`Program.cs:2478` および `Program.cs:2509`:
```csharp
var importServices = scopedServices.GetServices<IImportService>();
var service = importServices.FirstOrDefault(s => s.GetType().Name == "ReceiptVoucherImportService");
```
検索ロジック自体は正しい。

### 5. ビルド状況 ✅
前回のビルドは成功しており、該当クラスはコンパイルされている。

## 🎯 問題の根本原因

**DIコンテナにサービスが登録されていない**

`ImportServiceExtensions.AddImportServices()` メソッドが `Program.cs` で呼び出されていないため、以下のサービスがDIコンテナに登録されていない:

### 未登録のサービス一覧:
1. **単位マスタ**
   - `UnitMasterImportService`

2. **商品分類マスタ**
   - `ProductCategory1ImportService`
   - `ProductCategory2ImportService` 
   - `ProductCategory3ImportService`

3. **得意先分類マスタ**
   - `CustomerCategory1ImportService`
   - `CustomerCategory2ImportService`
   - `CustomerCategory3ImportService`
   - `CustomerCategory4ImportService`
   - `CustomerCategory5ImportService`

4. **仕入先分類マスタ**
   - `SupplierCategory1ImportService`
   - `SupplierCategory2ImportService`
   - `SupplierCategory3ImportService`

5. **担当者マスタ**
   - `StaffMasterImportService`
   - `StaffCategory1ImportService`

6. **🎯 入金・支払伝票（今回の問題）**
   - `ReceiptVoucherImportService`
   - `PaymentVoucherImportService`

## 💡 解決方法

`Program.cs` の DI 設定部分に以下の呼び出しを追加する必要がある:

```csharp
// ImportServiceExtensions の拡張メソッドを呼び出し
builder.Services.AddImportServices(connectionString);
```

## 📊 影響範囲

- **影響度**: 高
- **対象**: すべての分類マスタ、単位マスタ、担当者マスタ、入金・支払伝票のインポート機能
- **症状**: 「未対応のCSVファイル形式」または「サービスが見つかりません」エラー
- **対象ファイル**: 16種類のCSVファイル中、約14種類が機能しない状態

## 🔄 次のアクション

1. `Program.cs` に `builder.Services.AddImportServices(connectionString);` を追加
2. ビルドして動作確認
3. 実際のCSVファイルでテスト実行

---

**注意**: 分類マスタファイルの処理が成功していた理由は、Program.cs で個別の処理ロジックが書かれていたためと推測される。しかし、入金・支払伝票は完全にIImportServiceパターンに依存しているため、DI登録がないと動作しない。