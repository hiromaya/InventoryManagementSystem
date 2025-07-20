# UnmatchListコマンド引数処理問題の詳細調査結果

**調査日時**: 2025-07-20 19:55  
**調査対象**: InventoryManagementSystem/src/InventorySystem.Console/Program.cs  
**問題**: UnmatchListコマンドでtargetDateがnullになり、5,152件の異常なアンマッチが発生

## 重要な発見：根本的な問題を特定

### 1. Environment.GetCommandLineArgs()の使用が原因

**問題箇所（Program.cs 305行目）:**
```csharp
// Parse command line arguments
var commandArgs = Environment.GetCommandLineArgs();
```

**問題の詳細:**
- `Environment.GetCommandLineArgs()`は実行ファイル名を含む**完全な**コマンドライン引数を返す
- `dotnet run unmatch-list 2025-06-30`の場合、配列の内容は以下のようになる：
  - `args[0]` = `"dotnet"`（または実行ファイルのフルパス）
  - `args[1]` = `"run"`
  - `args[2]` = `"unmatch-list"`
  - `args[3]` = `"2025-06-30"`

### 2. commandの設定ロジック（357行目）

```csharp
var command = commandArgs[1].ToLower();
```

**問題:**
- `commandArgs[1]`は`"run"`であり、`"unmatch-list"`ではない
- これにより不正なコマンドとして処理される可能性がある

### 3. ExecuteUnmatchListAsyncメソッドの引数処理（546行目）

```csharp
// 日付指定の確認（オプション）
DateTime? targetDate = null;
if (args.Length >= 2 && DateTime.TryParse(args[1], out var parsedDate))
{
    targetDate = parsedDate;
    logger.LogInformation("指定された対象日: {TargetDate:yyyy-MM-dd}", targetDate);
}
```

**問題:**
- `args[1]`は`"run"`文字列であり、DateTime.TryParseは失敗する
- 実際の日付は`args[3]`（`"2025-06-30"`）に存在するが、参照されていない

### 4. 引数配列の構造比較

#### 通常のコンソールアプリケーション（Main(string[] args)）
```
dotnet run unmatch-list 2025-06-30
→ args[0] = "unmatch-list"
→ args[1] = "2025-06-30"
```

#### 現在の実装（Environment.GetCommandLineArgs()）
```
dotnet run unmatch-list 2025-06-30
→ commandArgs[0] = "dotnet" (または実行ファイルパス)
→ commandArgs[1] = "run"
→ commandArgs[2] = "unmatch-list"
→ commandArgs[3] = "2025-06-30"
```

## 修正方針

### 1. 修正パターンA：argsパラメータの正しい使用

```csharp
// Main メソッドの引数をそのまま使用
static async Task ExecuteUnmatchListAsync(IServiceProvider services, string[] args)
{
    // args は Main(string[] args) から渡される実際のコマンドライン引数
    // dotnet run unmatch-list 2025-06-30 の場合:
    // args[0] = "unmatch-list"
    // args[1] = "2025-06-30"
    
    DateTime? targetDate = null;
    if (args.Length >= 2 && DateTime.TryParse(args[1], out var parsedDate))
    {
        targetDate = parsedDate;
        logger.LogInformation("指定された対象日: {TargetDate:yyyy-MM-dd}", targetDate);
    }
}
```

### 2. 修正パターンB：Environment.GetCommandLineArgs()の継続使用（非推奨）

```csharp
static async Task ExecuteUnmatchListAsync(IServiceProvider services, string[] args)
{
    // Environment.GetCommandLineArgs() を使用する場合のオフセット修正
    DateTime? targetDate = null;
    if (args.Length >= 4 && DateTime.TryParse(args[3], out var parsedDate)) // args[3] に修正
    {
        targetDate = parsedDate;
        logger.LogInformation("指定された対象日: {TargetDate:yyyy-MM-dd}", targetDate);
    }
    
    // 部門指定も同様にオフセット修正
    string? department = null;
    if (args.Length >= 5) // args[4] に修正
    {
        department = args[4];
        logger.LogInformation("指定された部門: {Department}", department);
    }
}
```

## 推奨される修正方法

### **修正対象ファイル**: `/src/InventorySystem.Console/Program.cs`

### **修正1**: Main メソッドからの引数渡しを確認

**現在のコード（371行目）:**
```csharp
case "unmatch-list":
    await ExecuteUnmatchListAsync(host.Services, commandArgs);
    break;
```

**修正後:**
```csharp
case "unmatch-list":
    // Main(string[] args) の args を渡す（commandArgs ではなく）
    await ExecuteUnmatchListAsync(host.Services, args.Skip(1).ToArray()); // "unmatch-list" を除く
    break;
```

### **修正2**: ExecuteUnmatchListAsync メソッドの引数処理

**現在のコード（546-550行目）:**
```csharp
// 日付指定の確認（オプション）
DateTime? targetDate = null;
if (args.Length >= 2 && DateTime.TryParse(args[1], out var parsedDate))
{
    targetDate = parsedDate;
    logger.LogInformation("指定された対象日: {TargetDate:yyyy-MM-dd}", targetDate);
}
```

**修正後:**
```csharp
// 日付指定の確認（オプション）
DateTime? targetDate = null;
if (args.Length >= 1 && DateTime.TryParse(args[0], out var parsedDate)) // args[0] に修正
{
    targetDate = parsedDate;
    logger.LogInformation("指定された対象日: {TargetDate:yyyy-MM-dd}", targetDate);
}
```

### **修正3**: 部門指定の処理

**現在のコード（553-558行目）:**
```csharp
// 部門指定（オプション）
string? department = null;
if (args.Length >= 3)
{
    department = args[2];
    logger.LogInformation("指定された部門: {Department}", department);
}
```

**修正後:**
```csharp
// 部門指定（オプション）
string? department = null;
if (args.Length >= 2) // args[1] に修正
{
    department = args[1];
    logger.LogInformation("指定された部門: {Department}", department);
}
```

## 影響範囲の調査

### 同様の問題が存在する可能性のあるメソッド

1. **ExecuteDailyReportAsync**
2. **ExecuteInventoryListAsync**  
3. **ExecuteImportSalesAsync**
4. **ExecuteImportPurchaseAsync**
5. **ExecuteImportAdjustmentAsync**

これらすべてのメソッドで同じパターンの修正が必要。

## 検証方法

### 修正前のテスト
```bash
dotnet run unmatch-list 2025-06-30
# → targetDate = null (問題発生)
# → 全期間のアンマッチ処理（5,152件）
```

### 修正後のテスト
```bash
dotnet run unmatch-list 2025-06-30
# → targetDate = 2025-06-30 (正常)
# → 指定日以前のアンマッチ処理（適切な件数）
```

## 緊急度

**緊急度: 高**

- この問題により、アンマッチリスト機能が設計通りに動作していない
- 日付指定が無効化され、全期間処理による不正確な結果を生成
- 5,152件という異常な件数の原因はこの引数処理問題

## 結論

`Environment.GetCommandLineArgs()`の使用により、配列インデックスが2つずつずれており、日付パラメータが正しく取得できていない。これが targetDate = null の直接的な原因であり、5,152件の異常なアンマッチ件数の根本原因である。

修正により、アンマッチリスト処理が設計通りの動作（指定日付以前のデータのみ処理）に戻る。