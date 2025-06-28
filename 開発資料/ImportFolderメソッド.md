# ImportFolderメソッド完全修正仕様書

## 1. 概要

### 1.1 現在の問題
- `import-folder`コマンドは売上伝票・仕入伝票・在庫調整の3種類のCSVのみ処理
- マスタ系CSV（等級、階級、荷印、商品、得意先など）が「未対応のファイル形式」としてエラーフォルダに移動される
- アンマッチリスト生成に必要なマスタデータと前月末在庫データが取り込めない

### 1.2 修正目標
- すべての必要なCSVファイルを一括取込可能にする
- マスタファイルを伝票ファイルより先に処理する
- 前月末在庫（初期在庫）の取込に対応する

## 2. 修正対象ファイル

```
src/InventorySystem.Console/Program.cs
```

## 3. 必要なサービスインターフェース一覧

### 3.1 既存サービス（変更なし）
```csharp
ISalesVoucherImportService          // 売上伝票
IPurchaseVoucherImportService       // 仕入伝票
IInventoryAdjustmentImportService   // 在庫調整
IFileManagementService              // ファイル管理
```

### 3.2 追加が必要なサービス
```csharp
// マスタ系
IGradeMasterImportService           // 等級マスタ
IClassMasterImportService           // 階級マスタ
IShippingMarkMasterImportService    // 荷印マスタ
IRegionMasterImportService          // 産地マスタ
IProductMasterImportService         // 商品マスタ
ICustomerMasterImportService        // 得意先マスタ
ISupplierMasterImportService        // 仕入先マスタ

// 初期在庫
IPreviousMonthInventoryImportService // 前月末在庫
```

## 4. ファイル認識パターン詳細

### 4.1 伝票系ファイル
| ファイル名パターン | 判定方法 | サービス | メソッド |
|-------------------|---------|----------|---------|
| `売上伝票*.csv` | StartsWith("売上伝票") | ISalesVoucherImportService | ImportAsync(file, jobDate, department) |
| `仕入伝票*.csv` | StartsWith("仕入伝票") | IPurchaseVoucherImportService | ImportAsync(file, jobDate, department) |
| `在庫調整*.csv` | StartsWith("在庫調整") | IInventoryAdjustmentImportService | ImportAsync(file, jobDate, department) |
| `受注伝票*.csv` | StartsWith("受注伝票") | IInventoryAdjustmentImportService | ImportAsync(file, jobDate, department) |

### 4.2 マスタ系ファイル
| ファイル名パターン | 判定方法 | サービス | メソッド |
|-------------------|---------|----------|---------|
| `*等級汎用マスター*.csv` | Contains("等級汎用マスター") | IGradeMasterImportService | ImportAsync(file) |
| `*階級汎用マスター*.csv` | Contains("階級汎用マスター") | IClassMasterImportService | ImportAsync(file) |
| `*荷印汎用マスター*.csv` | Contains("荷印汎用マスター") | IShippingMarkMasterImportService | ImportAsync(file) |
| `*産地汎用マスター*.csv` | Contains("産地汎用マスター") | IRegionMasterImportService | ImportAsync(file) |
| `商品.csv` | == "商品.csv" | IProductMasterImportService | ImportAsync(file) |
| `得意先.csv` | == "得意先.csv" | ICustomerMasterImportService | ImportAsync(file) |
| `仕入先.csv` | == "仕入先.csv" | ISupplierMasterImportService | ImportAsync(file) |

### 4.3 初期在庫ファイル
| ファイル名 | 判定方法 | サービス | メソッド |
|-----------|---------|----------|---------|
| `前月末在庫.csv` | == "前月末在庫.csv" | IPreviousMonthInventoryImportService | ImportAsync(jobDate) |

### 4.4 未対応ファイル（警告表示）
```csharp
// 以下のファイルは警告メッセージを表示してエラーフォルダへ移動
string[] knownButUnsupported = {
    "担当者", "単位", "商品分類", "得意先分類", 
    "仕入先分類", "担当者分類", "支払伝票", "入金伝票"
};
```

## 5. 処理順序の実装

### 5.1 GetFileProcessOrderメソッド（新規追加）
```csharp
private static int GetFileProcessOrder(string fileName)
{
    // Phase 1: マスタファイル（優先度1-7）
    if (fileName.Contains("等級汎用マスター")) return 1;
    if (fileName.Contains("階級汎用マスター")) return 2;
    if (fileName.Contains("荷印汎用マスター")) return 3;
    if (fileName.Contains("産地汎用マスター")) return 4;
    if (fileName == "商品.csv") return 5;
    if (fileName == "得意先.csv") return 6;
    if (fileName == "仕入先.csv") return 7;
    
    // Phase 2: 初期在庫（優先度8）
    if (fileName == "前月末在庫.csv") return 8;
    
    // Phase 3: 伝票ファイル（優先度10-12）
    if (fileName.StartsWith("売上伝票")) return 10;
    if (fileName.StartsWith("仕入伝票")) return 11;
    if (fileName.StartsWith("在庫調整") || fileName.StartsWith("受注伝票")) return 12;
    
    // Phase 4: その他（優先度99）
    return 99;
}
```

## 6. ImportFolderメソッドの完全な実装

```csharp
private static async Task ImportFolder(IHost host, string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("使用方法: dotnet run import-folder <部門コード> [YYYY-MM-DD]");
        return;
    }

    var department = args[1];
    
    // サービスの取得（全15個）
    var fileService = host.Services.GetRequiredService<IFileManagementService>();
    var salesImportService = host.Services.GetRequiredService<ISalesVoucherImportService>();
    var purchaseImportService = host.Services.GetRequiredService<IPurchaseVoucherImportService>();
    var adjustmentImportService = host.Services.GetRequiredService<IInventoryAdjustmentImportService>();
    
    // マスタインポートサービス
    var gradeImportService = host.Services.GetRequiredService<IGradeMasterImportService>();
    var classImportService = host.Services.GetRequiredService<IClassMasterImportService>();
    var shippingMarkImportService = host.Services.GetRequiredService<IShippingMarkMasterImportService>();
    var regionImportService = host.Services.GetRequiredService<IRegionMasterImportService>();
    var productImportService = host.Services.GetRequiredService<IProductMasterImportService>();
    var customerImportService = host.Services.GetRequiredService<ICustomerMasterImportService>();
    var supplierImportService = host.Services.GetRequiredService<ISupplierMasterImportService>();
    
    // 初期在庫サービス
    var previousMonthInventoryService = host.Services.GetRequiredService<IPreviousMonthInventoryImportService>();
    
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    
    // ジョブ日付の解析
    var jobDate = args.Length > 2 && DateTime.TryParse(args[2], out var date) 
        ? date : DateTime.Today;
    
    Console.WriteLine($"=== フォルダ監視取込開始 ===");
    Console.WriteLine($"部門: {department}");
    Console.WriteLine($"ジョブ日付: {jobDate:yyyy-MM-dd}");
    
    try
    {
        // ファイル一覧の取得
        var files = await fileService.GetPendingFilesAsync(department);
        Console.WriteLine($"取込対象ファイル数: {files.Count}");
        
        // ファイルを処理順序でソート
        var sortedFiles = files
            .OrderBy(f => GetFileProcessOrder(Path.GetFileName(f)))
            .ThenBy(f => Path.GetFileName(f))
            .ToList();
        
        // 各ファイルの処理
        foreach (var file in sortedFiles)
        {
            var fileName = Path.GetFileName(file);
            Console.WriteLine($"\n処理中: {fileName}");
            
            try
            {
                // ========== 伝票系ファイル ==========
                if (fileName.StartsWith("売上伝票"))
                {
                    await salesImportService.ImportAsync(file, jobDate, department);
                    Console.WriteLine("✅ 売上伝票として処理完了");
                }
                else if (fileName.StartsWith("仕入伝票"))
                {
                    await purchaseImportService.ImportAsync(file, jobDate, department);
                    Console.WriteLine("✅ 仕入伝票として処理完了");
                }
                else if (fileName.StartsWith("在庫調整") || fileName.StartsWith("受注伝票"))
                {
                    await adjustmentImportService.ImportAsync(file, jobDate, department);
                    Console.WriteLine("✅ 在庫調整として処理完了");
                }
                // ========== マスタ系ファイル ==========
                else if (fileName.Contains("等級汎用マスター"))
                {
                    await gradeImportService.ImportAsync(file);
                    Console.WriteLine("✅ 等級マスタとして処理完了");
                }
                else if (fileName.Contains("階級汎用マスター"))
                {
                    await classImportService.ImportAsync(file);
                    Console.WriteLine("✅ 階級マスタとして処理完了");
                }
                else if (fileName.Contains("荷印汎用マスター"))
                {
                    await shippingMarkImportService.ImportAsync(file);
                    Console.WriteLine("✅ 荷印マスタとして処理完了");
                }
                else if (fileName.Contains("産地汎用マスター"))
                {
                    await regionImportService.ImportAsync(file);
                    Console.WriteLine("✅ 産地マスタとして処理完了");
                }
                else if (fileName == "商品.csv")
                {
                    await productImportService.ImportAsync(file);
                    Console.WriteLine("✅ 商品マスタとして処理完了");
                }
                else if (fileName == "得意先.csv")
                {
                    await customerImportService.ImportAsync(file);
                    Console.WriteLine("✅ 得意先マスタとして処理完了");
                }
                else if (fileName == "仕入先.csv")
                {
                    await supplierImportService.ImportAsync(file);
                    Console.WriteLine("✅ 仕入先マスタとして処理完了");
                }
                // ========== 初期在庫ファイル ==========
                else if (fileName == "前月末在庫.csv")
                {
                    await previousMonthInventoryService.ImportAsync(jobDate);
                    Console.WriteLine("✅ 前月末在庫（初期在庫）として処理完了");
                }
                // ========== 未対応ファイル ==========
                else if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    // 既知の未対応ファイル
                    string[] knownButUnsupported = {
                        "担当者", "単位", "商品分類", "得意先分類", 
                        "仕入先分類", "担当者分類", "支払伝票", "入金伝票"
                    };
                    
                    if (knownButUnsupported.Any(pattern => fileName.Contains(pattern)))
                    {
                        Console.WriteLine($"⚠️ {fileName} は現在未対応です（スキップ）");
                        await fileService.MoveToErrorAsync(file, department, "未対応のCSVファイル形式");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ {fileName} は認識できないCSVファイルです");
                        await fileService.MoveToErrorAsync(file, department, "不明なCSVファイル");
                    }
                }
                else
                {
                    // CSV以外のファイル
                    await fileService.MoveToErrorAsync(file, department, "CSVファイル以外は処理対象外");
                    Console.WriteLine("⚠️ CSVファイル以外のためエラーフォルダへ移動");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ファイル処理エラー: {File}", file);
                Console.WriteLine($"❌ エラー: {ex.Message}");
                
                // エラーが発生してもファイルをエラーフォルダに移動
                try
                {
                    await fileService.MoveToErrorAsync(file, department, ex.Message);
                }
                catch (Exception moveEx)
                {
                    logger.LogError(moveEx, "エラーファイルの移動に失敗: {File}", file);
                }
            }
        }
        
        Console.WriteLine("\n=== フォルダ監視取込完了 ===");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ エラー: {ex.Message}");
        logger.LogError(ex, "フォルダ監視取込でエラーが発生しました");
    }
}
```

## 7. 特殊な処理の詳細

### 7.1 前月末在庫.csvの処理
```csharp
// 特殊な処理内容
- 初期在庫として在庫マスタに反映
- 「前月末在庫数量・金額」と「前日在庫数量・金額」の両方に設定
- 商品コード00000（オール0）の行は除外
- ImportAsyncメソッドはjobDateのみを引数に取る（ファイルパスは内部で固定）
```

### 7.2 受注伝票.csvの処理
```csharp
// 在庫調整として処理
- IInventoryAdjustmentImportServiceを使用
- 96列目の「区分(1:ﾛｽ,4:振替,6:調整)」で在庫調整区分を判定
- 単位コード別の集計先：
  - 01,03,06 → 在庫調整
  - 02,05 → 加工
  - 04 → 振替
```

### 7.3 全角数字を含むファイル名への対応
```csharp
// Contains()メソッドで部分一致判定
- "等級汎用マスター１.csv"（全角１）→ Contains("等級汎用マスター")で判定
- "階級汎用マスター２.csv"（全角２）→ Contains("階級汎用マスター")で判定
- "荷印汎用マスター３.csv"（全角３）→ Contains("荷印汎用マスター")で判定
- "産地汎用マスター４.csv"（全角４）→ Contains("産地汎用マスター")で判定
```

## 8. エラーハンドリング仕様

### 8.1 ファイル単位のエラー処理
- 各ファイルの処理は独立したtry-catchブロック内で実行
- エラー発生時も次のファイルの処理を継続
- エラーファイルはエラーフォルダに移動（移動自体のエラーも考慮）

### 8.2 サービスが存在しない場合の対処
```csharp
// IRegionMasterImportServiceが未実装の場合のスタブ例
public class RegionMasterImportService : IRegionMasterImportService
{
    private readonly ILogger<RegionMasterImportService> _logger;
    
    public RegionMasterImportService(ILogger<RegionMasterImportService> logger)
    {
        _logger = logger;
    }
    
    public async Task<ImportResult> ImportAsync(string filePath)
    {
        _logger.LogWarning("産地マスタインポートは未実装です: {FilePath}", filePath);
        await Task.Delay(100); // 仮の処理
        return new ImportResult 
        { 
            Status = DataSetStatus.Completed,
            ImportedCount = 0,
            ErrorMessage = "未実装"
        };
    }
}
```

## 9. 期待される実行結果

```
=== フォルダ監視取込開始 ===
部門: DeptA
ジョブ日付: 2025-06-27
取込対象ファイル数: 28

処理中: 等級汎用マスター１.csv
✅ 等級マスタとして処理完了

処理中: 階級汎用マスター２.csv
✅ 階級マスタとして処理完了

処理中: 荷印汎用マスター３.csv
✅ 荷印マスタとして処理完了

処理中: 産地汎用マスター４.csv
✅ 産地マスタとして処理完了

処理中: 商品.csv
✅ 商品マスタとして処理完了

処理中: 得意先.csv
✅ 得意先マスタとして処理完了

処理中: 仕入先.csv
✅ 仕入先マスタとして処理完了

処理中: 前月末在庫.csv
✅ 前月末在庫（初期在庫）として処理完了

処理中: 売上伝票.csv
✅ 売上伝票として処理完了

処理中: 仕入伝票.csv
✅ 仕入伝票として処理完了

処理中: 受注伝票.csv
✅ 在庫調整として処理完了

処理中: 単位.csv
⚠️ 単位.csv は現在未対応です（スキップ）

処理中: 担当者.csv
⚠️ 担当者.csv は現在未対応です（スキップ）

=== フォルダ監視取込完了 ===
```

## 10. テスト手順

### 10.1 実行コマンド
```powershell
dotnet run --project src\InventorySystem.Console\InventorySystem.Console.csproj -- import-folder DeptA 2025-06-27
```

### 10.2 成功基準
1. マスタファイルがエラーフォルダに移動されない
2. 処理順序が正しい（マスタ→初期在庫→伝票）
3. 前月末在庫.csvが正しく処理される
4. 未対応ファイルは警告メッセージと共にエラーフォルダへ
5. エラーが発生しても処理が継続される

### 10.3 確認SQL
```sql
-- マスタデータの確認
SELECT COUNT(*) FROM GradeMaster;
SELECT COUNT(*) FROM ClassMaster;
SELECT COUNT(*) FROM ShippingMarkMaster;
SELECT COUNT(*) FROM ProductMaster;

-- 初期在庫の確認
SELECT COUNT(*) FROM InventoryMaster 
WHERE PreviousMonthQuantity > 0;

-- 伝票データの確認
SELECT COUNT(*) FROM SalesVouchers;
SELECT COUNT(*) FROM PurchaseVouchers;
SELECT COUNT(*) FROM InventoryAdjustments;
```