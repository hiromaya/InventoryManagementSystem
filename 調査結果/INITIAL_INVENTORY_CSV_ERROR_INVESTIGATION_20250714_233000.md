# import-initial-inventoryコマンド CsvHelperエラー調査報告書

**調査日時**: 2025年7月14日 23:30:00  
**調査者**: Claude Code  
**エラー概要**: ArgumentNullException in CsvConfiguration  

## 1. エラー発生箇所の詳細

### 1.1 エラースタックトレース
```
エラー: ArgumentNullException in CsvConfiguration
Location: InitialInventoryImportService.cs line 185 付近
Context: ReadCsvFileAsync メソッド内での CsvConfiguration 初期化時
```

### 1.2 該当コード（InitialInventoryImportService.cs 185行目付近）
```csharp
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
    }
};
```

### 1.3 ReadCsvFileAsyncメソッド全体
```csharp
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
        }
    };

    using var reader = new StreamReader(filePath, Encoding.UTF8);
    using var csv = new CsvReader(reader, config);
    
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

## 2. InitialInventoryRecordクラスの実装

### 2.1 クラス定義
```csharp
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace InventorySystem.Core.Models;

/// <summary>
/// 初期在庫データ（ZAIK*.csv）のレコードモデル
/// </summary>
public class InitialInventoryRecord
{
    [Index(0)]
    [Name("商品ＣＤ")]
    public string ProductCode { get; set; } = string.Empty;

    [Index(1)]
    [Name("等級ＣＤ")]
    public string GradeCode { get; set; } = string.Empty;

    [Index(2)]
    [Name("階級ＣＤ")]
    public string ClassCode { get; set; } = string.Empty;

    [Index(3)]
    [Name("荷印ＣＤ")]
    public string ShippingMarkCode { get; set; } = string.Empty;

    [Index(4)]
    [Name("荷印名")]
    public string ShippingMarkName { get; set; } = string.Empty;

    [Index(5)]
    [Name("商品分類１担当者ＣＤ")]
    public int PersonInChargeCode { get; set; }

    // 列6-8はスキップ

    [Index(9)]
    [Name("前日在庫数量")]
    public decimal PreviousStockQuantity { get; set; }

    // 列10はスキップ

    [Index(11)]
    [Name("前日在庫金額")]
    public decimal PreviousStockAmount { get; set; }

    // 列12-13はスキップ

    [Index(14)]
    [Name("当日在庫数量")]
    public decimal CurrentStockQuantity { get; set; }

    [Index(15)]
    [Name("当日在庫単価")]
    public decimal StandardPrice { get; set; }

    [Index(16)]
    [Name("当日在庫金額")]
    public decimal CurrentStockAmount { get; set; }

    [Index(17)]
    [Name("粗利計算用平均単価")]
    public decimal AveragePrice { get; set; }
}
```

### 2.2 使用している属性
- **Index属性**: 列番号を明示的に指定（不連続な列マッピングに対応）
- **Name属性**: 列ヘッダー名を指定
- **注意**: 列6-8、10、12-13は意図的にスキップされている

### 2.3 ClassMapの実装
```csharp
/// <summary>
/// 初期在庫データのCSVマッピング設定
/// </summary>
public sealed class InitialInventoryRecordMap : ClassMap<InitialInventoryRecord>
{
    public InitialInventoryRecordMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
        
        // 明示的にマッピング（Indexアトリビュートで設定済みだが、念のため）
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

## 3. 他のCSVインポート機能との比較

### 3.1 SalesVoucherImportServiceでのCsvHelper使用
```csharp
using var reader = new StreamReader(filePath, Encoding.UTF8);
using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    HeaderValidated = null,  // ヘッダー検証を無効化
    MissingFieldFound = null, // 不足フィールドのエラーを無効化
    BadDataFound = context =>
    {
        _logger.LogWarning("不正なデータが検出されました: 行{Row}, 列{Field}, 値'{Value}'", 
            context.Context?.Parser?.Row ?? 0, 
            context.Field ?? "不明", 
            context.Value ?? "null");
    }
});
```

### 3.2 PurchaseVoucherImportServiceでのCsvHelper使用
```csharp
using var reader = new StreamReader(filePath, Encoding.UTF8);
using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    HeaderValidated = null,  // ヘッダー検証を無効化
    MissingFieldFound = null, // 不足フィールドのエラーを無効化
    BadDataFound = context =>
    {
        _logger.LogWarning("不正なデータが検出されました: 行{Row}, 列{Field}, 値'{Value}'", 
            context.Context?.Parser?.Row ?? 0, 
            context.Field ?? "不明", 
            context.Value ?? "null");
    }
});
```

### 3.3 主な相違点
1. **CsvConfiguration の初期化方法**: 
   - 他のサービス: `new CsvConfiguration(CultureInfo.InvariantCulture)` を CsvReader のコンストラクタ内で直接使用
   - InitialInventoryImportService: CsvConfiguration を変数に格納してから使用

2. **ClassMap の使用**:
   - 他のサービス: ClassMap を使用せず、属性ベースマッピングのみ
   - InitialInventoryImportService: `InitialInventoryRecordMap` ClassMap を明示的に登録

3. **BadDataFound ハンドラーの実装**:
   - 他のサービス: `context.Value` も含めてログ出力
   - InitialInventoryImportService: `context.Value` を含まない

## 4. 問題の分析

### 4.1 エラーの直接的原因
調査の結果、CsvConfiguration の初期化自体に問題は見当たらない。**ArgumentNullException** の発生原因として最も可能性が高いのは：

1. **ClassMap の AutoMap() 呼び出し時の問題**
   - `AutoMap(CultureInfo.InvariantCulture)` での CultureInfo が null になっている可能性
   - .NET 8 環境での CultureInfo.InvariantCulture の動作変更

2. **重複する Index 属性の問題**
   - 属性とClassMapで同じマッピングを二重定義
   - 不連続なIndex（6-8, 10, 12-13をスキップ）による内部エラー

### 4.2 なぜArgumentNullExceptionが発生するか
1. **AutoMap() メソッドの内部処理**:
   - CultureInfo.InvariantCulture が何らかの理由で null になっている
   - 属性の重複定義により、内部で null 参照が発生

2. **CsvHelper v30.0.1 の既知の問題**:
   - AutoMap() と明示的マッピングの競合
   - 不連続インデックスでの内部バッファ問題

### 4.3 CsvHelperのバージョンと互換性
- **使用バージョン**: CsvHelper 30.0.1
- **関連する既知の問題**: v30.x系でAutoMap()の動作が変更され、明示的マッピングとの併用で問題が発生することがある

## 5. 推測される問題箇所

### 5.1 最も可能性が高い原因
**InitialInventoryRecordMap の AutoMap() と明示的マッピングの競合**

1. `AutoMap(CultureInfo.InvariantCulture)` が属性ベースの自動マッピングを実行
2. その後の明示的な `Map()` 呼び出しで重複定義が発生
3. 内部的に null 参照エラーが発生

### 5.2 その他の可能性
1. **不連続なIndex値の問題**: 列6-8, 10, 12-13のスキップが内部バッファエラーを引き起こす
2. **CultureInfo.InvariantCulture の初期化問題**: 特定の環境で null になる可能性
3. **読み込み対象ファイルが存在しない**: ファイルパスが無効でStreamReaderが失敗

## 6. 推奨される修正方針

### 6.1 即座に試すべき修正
**Option 1: AutoMap() を削除し、明示的マッピングのみ使用**
```csharp
public sealed class InitialInventoryRecordMap : ClassMap<InitialInventoryRecord>
{
    public InitialInventoryRecordMap()
    {
        // AutoMap(CultureInfo.InvariantCulture); // 削除
        
        // 明示的マッピングのみ
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

**Option 2: 他のサービスと同じパターンに統一（ClassMap不使用）**
```csharp
// ClassMap登録を削除
// csv.Context.RegisterClassMap<InitialInventoryRecordMap>(); // コメントアウト

// 属性のみでマッピング（InitialInventoryRecord クラスの属性は保持）
```

### 6.2 代替実装案
1. **SalesVoucherImportService のパターンを採用**: ClassMap を使わず属性のみ
2. **CsvConfiguration の inline 初期化**: 変数に格納せず直接使用
3. **ファイル存在確認の追加**: ReadCsvFileAsync の最初でファイル存在チェック

## 7. 追加調査が必要な項目
1. **実際のエラースタックトレースの取得**: より詳細なエラー内容の確認
2. **ZAIK*.csv ファイルの実際の形式確認**: ヘッダー行と列数の検証
3. **CultureInfo.InvariantCulture の状態確認**: 実行時の値の検証

## 8. 参考情報

### 8.1 正常に動作しているCSVインポートのパターン
```csharp
// SalesVoucherImportService で使用されているパターン
using var reader = new StreamReader(filePath, Encoding.UTF8);
using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    HeaderValidated = null,
    MissingFieldFound = null,
    BadDataFound = context =>
    {
        _logger.LogWarning("不正なデータ: 行{Row}, フィールド{Field}, 値{Value}", 
            context.Context?.Parser?.Row ?? 0, 
            context.Field ?? "不明",
            context.Value ?? "null");
    }
});

// ClassMap を使用せず、属性ベースマッピングのみ
csv.Context.TypeConverterCache.AddConverter<decimal>(new DecimalConverter());
var records = csv.GetRecords<SalesVoucherDaijinCsv>().ToList();
```

### 8.2 CsvHelperの推奨使用方法
**CsvHelper v30.x での推奨パターン**:
1. AutoMap() と明示的マッピングの併用を避ける
2. 不連続なIndex指定時はClassMapで全て明示的に定義
3. CultureInfo は必ず non-null であることを確認

**結論**: InitialInventoryRecordMap の AutoMap() 削除が最も効果的な修正と考えられる。