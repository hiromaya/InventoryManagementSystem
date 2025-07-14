# import-initial-inventoryコマンド CsvHelperエラー詳細調査報告書

**調査日時**: 2025年7月14日 23:52:00  
**調査者**: Claude Code  
**エラー概要**: ArgumentNullException in CsvConfiguration  

## 1. エラー発生箇所の詳細

### 1.1 エラースタックトレース
```
System.ArgumentNullException: Value cannot be null. (Parameter 'element')
   at System.ArgumentNullException.Throw(String paramName)
   at System.Attribute.GetCustomAttributes(MemberInfo element, Boolean inherit)
   at CsvHelper.Configuration.CsvConfiguration.ApplyAttributes(Type type)
   at CsvHelper.Configuration.CsvConfiguration..ctor(CultureInfo cultureInfo, Type attributesType)
   at InventorySystem.Core.Services.InitialInventoryImportService.ReadCsvFileAsync(String filePath) in C:\Development\InventoryManagementSystem\src\InventorySystem.Core\Services\InitialInventoryImportService.cs:line 187
```

### 1.2 該当コード（180-200行目）
```csharp
// line 180-200
        ReadCsvFileAsync(string filePath)
    {
        var validRecords = new List<InitialInventoryRecord>();
        var errorRecords = new List<(InitialInventoryRecord record, string error)>();

        // CsvReader内で直接CsvConfigurationを初期化（他のサービスと同じパターン）
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)    // line 187
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = context =>
            {
                _logger.LogWarning("不正なデータ: 行{Row}, フィールド{Field}", 
                    context.Context?.Parser?.Row ?? 0, 
                    context.Field ?? "不明");
            },
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        });
```

### 1.3 ReadCsvFileAsyncメソッド全体
```csharp
/// <summary>
/// CSVファイルを読み込み
/// </summary>
private async Task<(List<InitialInventoryRecord> valid, List<(InitialInventoryRecord record, string error)> errors)> 
    ReadCsvFileAsync(string filePath)
{
    var validRecords = new List<InitialInventoryRecord>();
    var errorRecords = new List<(InitialInventoryRecord record, string error)>();

    // CsvReader内で直接CsvConfigurationを初期化（他のサービスと同じパターン）
    using var reader = new StreamReader(filePath, Encoding.UTF8);
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        HeaderValidated = null,
        MissingFieldFound = null,
        BadDataFound = context =>
        {
            _logger.LogWarning("不正なデータ: 行{Row}, フィールド{Field}", 
                context.Context?.Parser?.Row ?? 0, 
                context.Field ?? "不明");
        },
        IgnoreBlankLines = true,
        TrimOptions = TrimOptions.Trim
    });
    
    // ClassMapを明示的に登録（属性を削除したため必須）
    csv.Context.RegisterClassMap<InitialInventoryRecordMap>();
    
    var rowNumber = 1;
    await csv.ReadAsync();
    csv.ReadHeader();
    
    while (await csv.ReadAsync())
    {
        rowNumber++;
        try
        {
            var record = csv.GetRecord<InitialInventoryRecord>();
            
            // 基本的なバリデーション
            var validationErrors = ValidateRecord(record, rowNumber);
            if (validationErrors.Any())
            {
                foreach (var error in validationErrors)
                {
                    errorRecords.Add((record, error));
                }
            }
            else
            {
                validRecords.Add(record);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("行{Row}の読み込みエラー: {Error}", rowNumber, ex.Message);
            // エラー行は記録するが処理は継続
            errorRecords.Add((null, $"行{rowNumber}: {ex.Message}"));
        }
    }

    return (validRecords, errorRecords);
}
```

### 1.4 using文一覧
```csharp
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Core.Models;
using Microsoft.Extensions.Logging;
```

## 2. InitialInventoryRecord関連ファイル

### 2.1 InitialInventoryRecord.cs
```csharp
using CsvHelper.Configuration;
using System.Globalization;

namespace InventorySystem.Core.Models;

/// <summary>
/// 初期在庫データ（ZAIK*.csv）のレコードモデル
/// 注意：属性ベースマッピングを削除し、ClassMapのみを使用（トリミング問題対策）
/// </summary>
public class InitialInventoryRecord
{
    public string ProductCode { get; set; } = string.Empty;
    public string GradeCode { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string ShippingMarkCode { get; set; } = string.Empty;
    public string ShippingMarkName { get; set; } = string.Empty;
    public int PersonInChargeCode { get; set; }
    public decimal PreviousStockQuantity { get; set; }
    public decimal PreviousStockAmount { get; set; }
    public decimal CurrentStockQuantity { get; set; }
    public decimal StandardPrice { get; set; }
    public decimal CurrentStockAmount { get; set; }
    public decimal AveragePrice { get; set; }
}

/// <summary>
/// 初期在庫データのCSVマッピング設定（トリミング耐性のClassMapのみ使用）
/// </summary>
public sealed class InitialInventoryRecordMap : ClassMap<InitialInventoryRecord>
{
    public InitialInventoryRecordMap()
    {
        // 属性を使わず、ClassMapのみでマッピングを定義（トリミング問題回避）
        Map(m => m.ProductCode).Index(0).Name("商品ＣＤ");
        Map(m => m.GradeCode).Index(1).Name("等級ＣＤ");
        Map(m => m.ClassCode).Index(2).Name("階級ＣＤ");
        Map(m => m.ShippingMarkCode).Index(3).Name("荷印ＣＤ");
        Map(m => m.ShippingMarkName).Index(4).Name("荷印名");
        Map(m => m.PersonInChargeCode).Index(5).Name("商品分類１担当者ＣＤ");
        Map(m => m.PreviousStockQuantity).Index(9).Name("前日在庫数量");
        Map(m => m.PreviousStockAmount).Index(11).Name("前日在庫金額");
        Map(m => m.CurrentStockQuantity).Index(14).Name("当日在庫数量");
        Map(m => m.StandardPrice).Index(15).Name("当日在庫単価");
        Map(m => m.CurrentStockAmount).Index(16).Name("当日在庫金額");
        Map(m => m.AveragePrice).Index(17).Name("粗利計算用平均単価");
    }
}
```

### 2.2 InitialInventoryRecordMap.cs
**ファイルは単独では存在しません**（InitialInventoryRecord.cs内に定義）

## 3. CsvHelperバージョン情報

### 3.1 プロジェクト別バージョン
- **InventorySystem.Core**: CsvHelper Version="30.0.1"
- **InventorySystem.Import**: CsvHelper Version="33.0.1"

### 3.2 ⚠️ 重大な発見：バージョン不整合
**異なるCsvHelperバージョンが混在している！**
- Core: v30.0.1
- Import: v33.0.1

## 4. 他サービスとの比較

### 4.1 SalesVoucherImportService（正常動作）
```csharp
// line 295-315
private async Task<List<SalesVoucherDaijinCsv>> ReadDaijinCsvFileAsync(string filePath)
{
    // UTF-8エンコーディングで直接読み込む
    _logger.LogInformation("UTF-8エンコーディングでCSVファイルを読み込みます: {FilePath}", filePath);
    using var reader = new StreamReader(filePath, Encoding.UTF8);
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        HeaderValidated = null,  // ヘッダー検証を無効化
        MissingFieldFound = null, // 不足フィールドのエラーを無効化
        BadDataFound = context => 
        {
            _logger.LogWarning($"不正なデータ: 行 {context.Context?.Parser?.Row ?? 0}, フィールド {context.Field ?? "不明"}");
        },
        IgnoreBlankLines = true,
        TrimOptions = TrimOptions.Trim
    });

    // ヘッダーを読み込む
    await csv.ReadAsync();
    // ... 以下処理
}
```

### 4.2 PurchaseVoucherImportService（正常動作）
```csharp
// line 256-275
private async Task<List<PurchaseVoucherDaijinCsv>> ReadDaijinCsvFileAsync(string filePath)
{
    // UTF-8エンコーディングで直接読み込む
    _logger.LogInformation("UTF-8エンコーディングでCSVファイルを読み込みます: {FilePath}", filePath);
    using var reader = new StreamReader(filePath, Encoding.UTF8);
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        HeaderValidated = null,  // ヘッダー検証を無効化
        MissingFieldFound = null, // 不足フィールドのエラーを無効化
        BadDataFound = context => 
        {
            _logger.LogWarning($"不正なデータ: 行 {context.Context?.Parser?.Row ?? 0}, フィールド {context.Field ?? "不明"}");
        },
        IgnoreBlankLines = true,
        TrimOptions = TrimOptions.Trim
    });

    // ヘッダーを読み込む
    // ... 以下処理
}
```

## 5. 問題の分析

### 5.1 確認されたコードパターン
**InitialInventoryImportService**: 
- 正常なCsvConfigurationコンストラクタ使用 `new CsvConfiguration(CultureInfo.InvariantCulture)`
- 第2引数（Type attributesType）は渡していない

**他のサービス**: 
- 全く同じパターンで初期化
- 正常に動作している

### 5.2 エラーの根本原因

#### 原因1: CsvHelperバージョン不整合（最重要）
- **InventorySystem.Core**: v30.0.1
- **InventorySystem.Import**: v33.0.1
- **影響**: 異なるバージョンのアセンブリが混在し、実行時に互換性問題が発生

#### 原因2: スタックトレースの詳細分析
```
at CsvHelper.Configuration.CsvConfiguration..ctor(CultureInfo cultureInfo, Type attributesType)
```
- エラーログでは第2引数（Type attributesType）を持つコンストラクタが呼ばれている
- しかし実際のコードでは `new CsvConfiguration(CultureInfo.InvariantCulture)` のみ
- **推測**: バージョン不整合により、異なるコンストラクタが実行時に呼ばれている

#### 原因3: ClassMap登録時の問題
- `csv.Context.RegisterClassMap<InitialInventoryRecordMap>()`実行時
- 内部でInitialInventoryRecordの型情報を解析
- バージョン不整合により、型情報取得時にnullが返される

### 5.3 他サービスとの相違点

1. **プロジェクト所属**:
   - **InitialInventoryImportService**: InventorySystem.Core（v30.0.1）
   - **SalesVoucherImportService**: InventorySystem.Import（v33.0.1）
   - **PurchaseVoucherImportService**: InventorySystem.Import（v33.0.1）

2. **ClassMap使用**:
   - **InitialInventoryImportService**: ClassMapを明示的に登録
   - **他サービス**: 属性ベースマッピングのみ（ClassMap不使用）

3. **実行環境**:
   - 他サービスは同一プロジェクト内で統一されたCsvHelperバージョンを使用
   - InitialInventoryImportServiceは異なるバージョンと相互作用

## 6. 推奨される修正方針

### 修正1: CsvHelperバージョン統一（最優先）
```xml
<!-- InventorySystem.Core.csproj を修正 -->
<PackageReference Include="CsvHelper" Version="33.0.1" />
```

### 修正2: 代替実装案（バージョン統一が困難な場合）
- InitialInventoryImportServiceをInventorySystem.Importプロジェクトに移動
- または属性ベースマッピングに戻してClassMap削除

### 修正3: ClassMap登録の最適化
```csharp
// RegisterClassMapの呼び出しをtry-catchで保護
try
{
    csv.Context.RegisterClassMap<InitialInventoryRecordMap>();
}
catch (Exception ex)
{
    _logger.LogError(ex, "ClassMap登録エラー - フォールバック処理を実行");
    // 属性ベースマッピングへのフォールバック
}
```

## 7. 緊急度と影響範囲

### 緊急度: 🔴 HIGH
- 移行用在庫マスタ取込が完全に停止
- 運用開始に直接影響

### 影響範囲
- import-initial-inventoryコマンドのみ
- 他のCSVインポート機能は正常動作

### 推奨される修正順序
1. **CsvHelperバージョン統一**（最も効果的）
2. **テスト実行**
3. **必要に応じて追加修正**

---

**結論**: バージョン不整合が根本原因である可能性が極めて高い。v30.0.1をv33.0.1に統一することで解決すると推測される。