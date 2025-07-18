# init-database DIエラー調査結果

**調査日時**: 2025-07-17 14:15  
**調査者**: Claude Code  
**問題**: IReceiptVoucherRepository と IPaymentVoucherRepository の解決エラー

## 🔍 エラーの概要

**init-database --force実行時エラー**:
```
Unable to resolve service for type 'InventorySystem.Core.Interfaces.IReceiptVoucherRepository' 
while attempting to activate 'InventorySystem.Import.Services.ReceiptVoucherImportService'

Unable to resolve service for type 'InventorySystem.Core.Interfaces.IPaymentVoucherRepository' 
while attempting to activate 'InventorySystem.Import.Services.PaymentVoucherImportService'
```

## 📋 詳細調査結果

### 1. サービス登録状況 ✅
前回の修正により正常に登録済み:
- `ReceiptVoucherImportService` → DIコンテナに登録済み
- `PaymentVoucherImportService` → DIコンテナに登録済み

### 2. インターフェース存在確認 ✅
以下のインターフェースは存在:
- `/src/InventorySystem.Core/Interfaces/IReceiptVoucherRepository.cs` ✅
- `/src/InventorySystem.Core/Interfaces/IPaymentVoucherRepository.cs` ✅

### 3. **🚨 根本原因発見: 実装クラス未作成**

**重大な問題**: リポジトリの実装クラスが存在しない

#### 調査結果:
- ❌ `ReceiptVoucherRepository` クラスが存在しない
- ❌ `PaymentVoucherRepository` クラスが存在しない
- ✅ インターフェースは定義済み
- ❌ DIコンテナ登録も当然存在しない

### 4. 既存パターンの確認 ✅
他のリポジトリ実装を確認した結果、以下のパターンが確立されている:
- `SalesVoucherRepository`
- `PurchaseVoucherRepository` 
- `InventoryAdjustmentRepository`

### 5. 期待される実装場所
- `/src/InventorySystem.Data/Repositories/ReceiptVoucherRepository.cs`
- `/src/InventorySystem.Data/Repositories/PaymentVoucherRepository.cs`

## 🎯 問題の根本原因

**リポジトリ実装クラスが未作成**

前回のセッションで以下の作業を実施:
1. ✅ エンティティクラス作成 (`ReceiptVoucher`, `PaymentVoucher`)
2. ✅ CSVモデルクラス作成 (`ReceiptVoucherCsv`, `PaymentVoucherCsv`)
3. ✅ インターフェース作成 (`IReceiptVoucherRepository`, `IPaymentVoucherRepository`)
4. ✅ インポートサービス作成 (`ReceiptVoucherImportService`, `PaymentVoucherImportService`)
5. ❌ **リポジトリ実装クラス作成が漏れた**

## 💡 解決方法

以下の2つのリポジトリ実装クラスを作成する必要がある:

### 1. ReceiptVoucherRepository
```csharp
// /src/InventorySystem.Data/Repositories/ReceiptVoucherRepository.cs
public class ReceiptVoucherRepository : IReceiptVoucherRepository
{
    // 既存パターン（SalesVoucherRepository等）に従って実装
}
```

### 2. PaymentVoucherRepository
```csharp
// /src/InventorySystem.Data/Repositories/PaymentVoucherRepository.cs
public class PaymentVoucherRepository : IPaymentVoucherRepository
{
    // 既存パターン（SalesVoucherRepository等）に従って実装
}
```

### 3. Program.csにDI登録追加
```csharp
builder.Services.AddScoped<IReceiptVoucherRepository>(provider => 
    new ReceiptVoucherRepository(connectionString, provider.GetRequiredService<ILogger<ReceiptVoucherRepository>>()));
builder.Services.AddScoped<IPaymentVoucherRepository>(provider => 
    new PaymentVoucherRepository(connectionString, provider.GetRequiredService<ILogger<PaymentVoucherRepository>>()));
```

## 📊 影響範囲

- **影響度**: 高（アプリケーション起動不可）
- **対象**: すべてのコマンド（init-database含む）
- **症状**: DI解決時の致命的エラー
- **修正ファイル**: 2つのリポジトリクラス作成 + Program.cs登録

## 🔄 次のアクション

1. `ReceiptVoucherRepository.cs` を作成（既存パターンに従う）
2. `PaymentVoucherRepository.cs` を作成（既存パターンに従う）
3. Program.cs に DI登録を追加
4. ビルドテスト実行
5. `init-database --force` で動作確認

## 📚 参考パターン

実装時の参考として、以下の既存リポジトリを参照:
- `/src/InventorySystem.Data/Repositories/SalesVoucherRepository.cs`
- `/src/InventorySystem.Data/Repositories/PurchaseVoucherRepository.cs`

---

**注意**: この問題は前回のセッション時に作業が不完全だったことが原因。インターフェースとサービスは作成したが、実装クラスの作成とDI登録が漏れていた。