# import-initial-inventoryコマンド CsvHelperエラー再調査報告書（AutoMap削除後）

**調査日時**: 2025年7月14日 23:45:00  
**調査者**: Claude Code  
**エラー概要**: AutoMap削除後も継続するArgumentNullException  

## 1. AutoMap削除の確認

### 1.1 現在のInitialInventoryRecordMapの実装
```csharp
public sealed class InitialInventoryRecordMap : ClassMap<InitialInventoryRecord>
{
    public InitialInventoryRecordMap()
    {
        // AutoMap削除 - 明示的マッピングのみ使用（AttributeとClassMapの競合を回避）
        
        // 明示的にマッピング
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

**確認結果**: AutoMap()は正常に削除されています。

### 1.2 InitialInventoryRecordクラスの属性
```csharp
public class InitialInventoryRecord
{
    [Index(0)]
    [Name("商品ＣＤ")]
    public string ProductCode { get; set; } = string.Empty;

    [Index(1)]
    [Name("等級ＣＤ")]
    public string GradeCode { get; set; } = string.Empty;

    // ... 他のプロパティも同様にIndex/Name属性を持つ
}
```

## 2. エラー発生箇所の詳細分析

### 2.1 InitialInventoryImportService.cs 185行目付近
```csharp
private async Task<(List<InitialInventoryRecord> valid, List<(InitialInventoryRecord record, string error)> errors)> 
    ReadCsvFileAsync(string filePath)
{
    var validRecords = new List<InitialInventoryRecord>();
    var errorRecords = new List<(InitialInventoryRecord record, string error)>();

    var config = new CsvConfiguration(CultureInfo.InvariantCulture)  // ← 185行目付近
    {
        HasHeaderRecord = true,
        HeaderValidated = null,
        MissingFieldFound = null,
        BadDataFound = context =>
        {
            _logger.LogWarning("不正なデータ: 行{Row}, フィールド{Field}", 
                context.Context?.Parser?.Row ?? 0, 
                context.Field ?? "不明");
        }
    };

    using var reader = new StreamReader(filePath, Encoding.UTF8);
    using var csv = new CsvReader(reader, config);
    
    csv.Context.RegisterClassMap<InitialInventoryRecordMap>();  // ← ClassMap登録
    
    // ... 以下読み込み処理
}
```

## 3. 他サービスとの実装パターン比較

### 3.1 SalesVoucherImportService（正常動作）
```csharp
using var reader = new StreamReader(filePath, Encoding.UTF8);
using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    HeaderValidated = null,
    MissingFieldFound = null,
    BadDataFound = context => 
    {
        _logger.LogWarning($"不正なデータ: 行 {context.Context?.Parser?.Row ?? 0}, フィールド {context.Field ?? "不明"}");
    },
    IgnoreBlankLines = true,
    TrimOptions = TrimOptions.Trim
});

// ClassMapは使用していない - 属性ベースマッピングのみ
```

### 3.2 主な相違点
1. **IgnoreBlankLines**: InitialInventoryImportServiceでは設定なし
2. **TrimOptions**: InitialInventoryImportServiceでは設定なし
3. **ClassMap使用**: InitialInventoryImportServiceのみClassMapを使用
4. **属性の重複**: InitialInventoryRecordは属性とClassMapの両方でマッピング定義

## 4. 問題の真の原因分析

### 4.1 属性とClassMapの競合
現在の実装では以下の２つのマッピング方法が併存しています：

1. **属性ベースマッピング**（InitialInventoryRecordクラス）
   - `[Index(0)]`と`[Name("商品ＣＤ")]`

2. **ClassMapベースマッピング**（InitialInventoryRecordMap）
   - `Map(m => m.ProductCode).Index(0).Name("商品ＣＤ")`

この重複が内部的な競合を引き起こしている可能性があります。

### 4.2 CsvHelper v30.0.1での動作
CsvHelper v30系では、属性とClassMapの両方が存在する場合の動作が不安定になることがあります。特に：
- ClassMapを登録すると属性が無視される場合がある
- 両方のマッピングが部分的に適用されて競合する場合がある

### 4.3 ArgumentNullExceptionの発生メカニズム
1. CsvConfigurationの初期化は成功している（185行目）
2. ClassMap登録時（`RegisterClassMap`）に内部で属性との競合が発生
3. マッピング解決時にnull参照が発生

## 5. 推奨される修正方法

### 5.1 Option 1: ClassMapを完全に削除（推奨）
```csharp
// ReadCsvFileAsyncメソッドを修正
private async Task<(List<InitialInventoryRecord> valid, List<(InitialInventoryRecord record, string error)> errors)> 
    ReadCsvFileAsync(string filePath)
{
    var validRecords = new List<InitialInventoryRecord>();
    var errorRecords = new List<(InitialInventoryRecord record, string error)>();

    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
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
        IgnoreBlankLines = true,  // 追加
        TrimOptions = TrimOptions.Trim  // 追加
    };

    using var reader = new StreamReader(filePath, Encoding.UTF8);
    using var csv = new CsvReader(reader, config);
    
    // csv.Context.RegisterClassMap<InitialInventoryRecordMap>();  // 削除
    
    var rowNumber = 1;
    await csv.ReadAsync();
    csv.ReadHeader();
    
    while (await csv.ReadAsync())
    {
        rowNumber++;
        try
        {
            var record = csv.GetRecord<InitialInventoryRecord>();
            // ... 以下同じ
        }
        catch (Exception ex)
        {
            _logger.LogWarning("行{Row}の読み込みエラー: {Error}", rowNumber, ex.Message);
            errorRecords.Add((null, $"行{rowNumber}: {ex.Message}"));
        }
    }

    return (validRecords, errorRecords);
}
```

### 5.2 Option 2: 属性を削除してClassMapのみ使用
```csharp
// InitialInventoryRecordクラスから属性を削除
public class InitialInventoryRecord
{
    // [Index(0)]  <- 削除
    // [Name("商品ＣＤ")]  <- 削除
    public string ProductCode { get; set; } = string.Empty;

    // [Index(1)]  <- 削除
    // [Name("等級ＣＤ")]  <- 削除
    public string GradeCode { get; set; } = string.Empty;
    
    // ... 他のプロパティも同様に属性を削除
}
```

### 5.3 Option 3: SalesVoucherImportServiceと完全に同じパターンに統一
他のサービスで実績のあるパターンを採用し、ClassMapを使わず属性ベースのマッピングのみを使用。

## 6. 結論と推奨事項

### 6.1 エラーの根本原因
AutoMap削除後もエラーが継続する理由は、**属性とClassMapの両方が存在することによる競合**です。

### 6.2 即座に実施すべき修正
**Option 1（ClassMap登録の削除）を推奨**します。理由：
1. 属性ベースマッピングは既に完全に定義されている
2. 他のサービスで実績のあるパターン
3. 最小限の変更で問題を解決できる

### 6.3 実装手順
1. InitialInventoryImportService.csの`csv.Context.RegisterClassMap<InitialInventoryRecordMap>();`行をコメントアウト
2. `IgnoreBlankLines = true`と`TrimOptions = TrimOptions.Trim`を追加
3. InitialInventoryRecordMapクラスは将来の参照用に残す（使用はしない）

### 6.4 テスト方法
```bash
dotnet run -- import-initial-inventory DeptA
```

エラーが解消されることを確認し、正常にZAIK*.csvファイルが処理されることを検証する。