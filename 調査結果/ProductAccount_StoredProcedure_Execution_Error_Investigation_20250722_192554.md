# 商品勘定帳票ストアドプロシージャ実行エラー調査結果

## 調査日時
- 実施日: 2025-07-22 19:25:54
- 対象エラー: Could not find stored procedure 'sp_CreateProductLedgerData'

## 1. ProductAccountFastReportService.csの実装状況

### GetConnectionString()メソッド
```csharp
private string GetConnectionString()
{
    // 環境変数または設定ファイルから接続文字列を取得
    // 実際の実装では、IConfiguration等を使用
    return Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
        ?? "Server=(localdb)\\mssqllocaldb;Database=InventoryManagementDB;Trusted_Connection=true;";
}
```

**分析結果:**
- ❌ **問題発見**: IConfigurationが注入されていない
- ❌ **ハードコード**: LocalDBの接続文字列がハードコードされている  
- ❌ **環境依存**: 実際の実行環境（SQL Express）と異なる接続先を参照

### 接続処理の実装
```csharp
// 115行目: エラー発生箇所
using var reader = command.ExecuteReader();
```

**接続文字列の差異:**
- **ProductAccountService**: `Server=(localdb)\\mssqllocaldb` (LocalDB)
- **appsettings.json**: `Data Source=localhost\\SQLEXPRESS` (SQL Express)

## 2. 他サービスでの接続方法

### UnmatchListServiceでの実装
```csharp
// 依存性注入による各Repositoryの使用
private readonly ICpInventoryRepository _cpInventoryRepository;
private readonly ISalesVoucherRepository _salesVoucherRepository;
// ... 他のRepository

// リポジトリ経由でのDB接続（間接的）
await _salesVoucherRepository.GetDataSetIdByJobDateAsync(targetDate.Value);
```

### BaseRepositoryクラスでの実装
```csharp
protected readonly string _connectionString;

protected BaseRepository(string connectionString, ILogger logger)
{
    _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
}

protected SqlConnection CreateConnection()
{
    return new SqlConnection(_connectionString);
}
```

**差異の分析:**
- **他サービス**: Repositoryパターン + DI経由の接続文字列
- **ProductAccountService**: 直接的な環境変数/ハードコード参照

## 3. 依存性注入の設定

### サービス登録（Program.cs:231行目）
```csharp
var productAccountFastReportType = Type.GetType("InventorySystem.Reports.FastReport.Services.ProductAccountFastReportService, InventorySystem.Reports");
if (unmatchListFastReportType != null && dailyReportFastReportType != null && productAccountFastReportType != null)
{
    builder.Services.AddScoped(typeof(InventorySystem.Reports.Interfaces.IProductAccountReportService), productAccountFastReportType);
}
```

### 接続文字列の設定（Program.cs:120-121行目）
```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
```

**問題の特定:**
- ProductAccountFastReportServiceには**IConfigurationが注入されていない**
- 他のRepositoryには正しく接続文字列が注入されている

## 4. 環境設定

### 接続文字列の定義（appsettings.json）
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=localhost\\SQLEXPRESS;Initial Catalog=InventoryManagementDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True"
  }
}
```

**実際の接続先:** SQL Server Express (`localhost\SQLEXPRESS`)
**ProductAccountServiceが参照する接続先:** LocalDB (`(localdb)\mssqllocaldb`)

## 5. 問題の原因分析

### 推定される原因
1. **接続文字列の不一致**: LocalDB vs SQL Express
   - ProductAccountServiceのGetConnectionString()メソッドがLocalDBを参照
   - ストアドプロシージャはSQL Express上に存在

2. **依存性注入の不備**: IConfigurationが注入されていない
   - 他のサービスは正しくRepository + DIを使用
   - ProductAccountServiceのみ直接接続文字列を参照

3. **アーキテクチャの不統一**: 直接DB接続 vs Repositoryパターン
   - UnmatchListServiceはRepositoryパターンを使用
   - ProductAccountServiceは直接SqlConnectionを使用

### 他機能との差異

| サービス | 接続方法 | 接続文字列取得 | 状態 |
|----------|----------|---------------|------|
| UnmatchListService | Repository経由 | DI + appsettings.json | ✅ 正常 |
| DailyReportService | Repository経由 | DI + appsettings.json | ✅ 正常 |
| ProductAccountService | 直接接続 | 環境変数 + ハードコード | ❌ エラー |

## 6. 推奨される修正方針

### 短期的修正案（即座に実施可能）

#### Option A: IConfigurationの注入
```csharp
// コンストラクタにIConfigurationを追加
private readonly IConfiguration _configuration;

public ProductAccountFastReportService(
    ILogger<ProductAccountFastReportService> logger,
    IConfiguration configuration,
    // ... 他の依存性
)
{
    _configuration = configuration;
}

private string GetConnectionString()
{
    return _configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("Connection string not found");
}
```

#### Option B: ハードコード修正（一時的）
```csharp
private string GetConnectionString()
{
    // appsettings.jsonと同一の接続文字列を使用
    return "Data Source=localhost\\SQLEXPRESS;Initial Catalog=InventoryManagementDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True";
}
```

### 長期的改善案（アーキテクチャレベル）

#### 1. Repositoryパターンの統一
```csharp
// 新しいRepositoryを作成
public interface IProductLedgerRepository
{
    Task<IEnumerable<ProductAccountData>> GetProductLedgerDataAsync(DateTime jobDate, string? departmentCode);
}
```

#### 2. サービス層の責務分離
- FastReportServiceはPDF生成のみに集中
- データ取得は専用Repositoryに委譲
- ビジネスロジックはサービス層で実装

#### 3. 統一された接続管理
- 全てのDB接続をRepositoryパターンに統一
- IConfigurationによる一元的な設定管理
- 環境別設定の適切な分離

## 7. 実装優先度

### 緊急対応（即座に実施）
1. **IConfigurationの注入** - ProductAccountFastReportServiceに追加
2. **接続文字列の修正** - SQL Expressを正しく参照

### 中期改善（1-2週間以内）
1. **Repositoryパターン統一** - ProductLedgerRepositoryの作成
2. **アーキテクチャ統一** - 他サービスとの設計一貫性確保

### 長期改善（1ヶ月以内）
1. **設定管理の統一** - 全サービスでの設定パターン標準化
2. **テスト可能性の向上** - モックとDIを活用したテスト環境構築

## 8. 検証手順

### 修正後の確認項目
1. ✅ 接続文字列がSQL Expressを正しく参照している
2. ✅ ストアドプロシージャsp_CreateProductLedgerDataが実行される
3. ✅ 商品勘定帳票のPDF生成が正常に完了する
4. ✅ 他の機能（アンマッチリスト、商品日報）との動作整合性

### テストコマンド
```bash
# 商品勘定帳票生成テスト
dotnet run -- product-account 2025-06-30

# 接続テスト
dotnet run -- test-connection
```

## 9. 結論

**根本原因**: ProductAccountFastReportServiceが他のサービスと異なる接続文字列取得方式を採用しており、LocalDBではなくSQL Expressに接続すべきところ、間違った接続先を参照している。

**最優先対応**: IConfigurationを注入し、appsettings.jsonから正しい接続文字列を取得するよう修正する。

**システム統一性**: 将来的には全サービスでRepositoryパターンを統一し、アーキテクチャの一貫性を確保することが望ましい。