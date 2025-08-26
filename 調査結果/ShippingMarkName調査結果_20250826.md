# CP在庫マスタ ShippingMarkName実装調査結果

## 調査日時
2025-08-26 10:00

## 1. データベース定義

### CreateDatabase.sql (62-67行目)
```sql
CREATE TABLE CpInventoryMaster (
    ProductCode NVARCHAR(15) NOT NULL,          -- 商品コード
    GradeCode NVARCHAR(15) NOT NULL,            -- 等級コード
    ClassCode NVARCHAR(15) NOT NULL,            -- 階級コード
    ShippingMarkCode NVARCHAR(15) NOT NULL,     -- 荷印コード
    ShippingMarkName NVARCHAR(50) NOT NULL,     -- 荷印名
```
**分析**:
- ShippingMarkNameの定義: NVARCHAR(50) NOT NULL、5項目複合キーの一部
- ManualShippingMarkの有無: **無**
- コメント: 「荷印名」として定義（手入力か荷印マスタ由来かの区別なし）

### ストアドプロシージャ
#### sp_CreateCpInventoryFromInventoryMasterCumulative.sql (33行目)
```sql
INSERT INTO CpInventoryMaster (
    -- 5項目キー
    ProductCode, GradeCode, ClassCode, ShippingMarkCode, ShippingMarkName,
```
**使用状況**:
- ShippingMarkNameの扱い: InventoryMasterからそのまま転写

#### sp_CreateProductLedgerData.sql (99行目, 137行目)
```sql
-- 前残高レコード (99行目)
cp.ShippingMarkName as ManualShippingMark,

-- 売上伝票データ (137行目)  
RIGHT('        ' + ISNULL(s.ShippingMarkName, ''), 8) as ManualShippingMark,
```
**使用状況**:
- ShippingMarkNameの扱い: **ShippingMarkNameをManualShippingMarkとして8文字パディング処理**
- 409行目: `cp.ShippingMarkName as ManualShippingMark` （CP在庫マスタから）
- 450行目: `s.ShippingMarkName as ManualShippingMark` （売上伝票から）
- 502行目: `p.ShippingMarkName as ManualShippingMark` （仕入伝票から）
- 554行目: `ia.ShippingMarkName as ManualShippingMark` （在庫調整から）

## 2. モデル定義

### CpInventoryMaster.cs (1-100行目)
```csharp
public class CpInventoryMaster
{
    public InventoryKey Key { get; set; } = new();
    
    // 基本情報
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    // ...
    
    // マスタ参照情報（商品勘定・在庫表で使用）
    public string GradeName { get; set; } = string.Empty;        // 等級名
    public string ClassName { get; set; } = string.Empty;        // 階級名
```
**現状**:
- ManualShippingMarkプロパティ: **無**
- ShippingMarkNameは`InventoryKey`クラス内で管理

## 3. ビジネスロジック

### ImportFolderCommand.cs
**結果**: ファイルが存在しない（Program.cs内で実装）

### Program.cs (3613-3641行目)
```csharp
// CP在庫マスタの等級名・階級名設定処理を追加
var masterSyncService = scopedServices.GetService<IMasterSyncService>();
if (masterSyncService != null)
{
    System.Console.WriteLine($"[{currentDate:yyyy-MM-dd}] CP在庫マスタの等級名・階級名を設定中...");
    var masterSyncConnectionString = scopedServices.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
    
    using var connection = new SqlConnection(masterSyncConnectionString);
    await connection.OpenAsync();
    using var transaction = connection.BeginTransaction();
    
    try
    {
        await masterSyncService.UpdateCpInventoryMasterNamesAsync(connection, transaction, currentDate);
        await transaction.CommitAsync();
        System.Console.WriteLine($"✅ CP在庫マスタの等級名・階級名設定完了 [{currentDate:yyyy-MM-dd}]");
    }
```
**実装内容**:
- CP在庫マスタ作成箇所: 在庫マスタ最適化処理後（3613行目）
- ShippingMarkName設定方法: MasterSyncServiceは等級名・階級名のみ更新、ShippingMarkNameは更新対象外

## 4. レポート処理

### ProductAccountFastReportService.cs
```csharp
// 409行目
cp.ShippingMarkName as ManualShippingMark,

// 450行目（売上伝票）
s.ShippingMarkName as ManualShippingMark,

// 502行目（仕入伝票）
p.ShippingMarkName as ManualShippingMark,

// 554行目（在庫調整）
ia.ShippingMarkName as ManualShippingMark,
```
**問題箇所**:
- 現在のマッピング: **ShippingMarkNameをManualShippingMarkにエイリアスしている**
- 本来: ManualShippingMark（手入力項目）は伝票の別カラムから取得すべき

### ProductAccountReportModel.cs (33-39行目)
```csharp
/// <summary>
/// 荷印名
/// </summary>
[MaxLength(50)]
public string ShippingMarkName { get; set; } = string.Empty;

/// <summary>
/// 手入力荷印（8文字固定）
/// </summary>
[MaxLength(8)]
public string ManualShippingMark { get; set; } = string.Empty;
```
**現状**:
- ShippingMarkNameとManualShippingMarkは別プロパティとして定義済み

## 5. 影響範囲まとめ

### 修正が必要なファイル一覧
1. **database/CreateDatabase.sql** - CpInventoryMasterテーブルにManualShippingMarkカラム追加
2. **src/InventorySystem.Core/Entities/CpInventoryMaster.cs** - ManualShippingMarkプロパティ追加
3. **database/procedures/sp_CreateCpInventoryFromInventoryMasterCumulative.sql** - ManualShippingMark設定ロジック追加
4. **database/procedures/sp_CreateProductLedgerData.sql** - ShippingMarkNameとManualShippingMarkの正しいマッピング
5. **src/InventorySystem.Reports/FastReport/Services/ProductAccountFastReportService.cs** - ManualShippingMarkの正しい取得元修正
6. **Program.cs (import-folder)** - CP在庫マスタ作成時のManualShippingMark設定追加

### リスク評価
- **高**: ストアドプロシージャの修正（sp_CreateProductLedgerData）- 商品勘定の表示が変わる
- **中**: CpInventoryMasterテーブル構造変更 - 既存データの移行が必要
- **低**: C#モデルクラスの修正 - コンパイル時にエラー検出可能

## 6. 推奨事項

### 方針A（ManualShippingMarkカラム追加）実施における推奨事項

1. **段階的実装**
   - Phase 1: データベーススキーマ変更（ALTER TABLE）
   - Phase 2: モデルクラス更新
   - Phase 3: ビジネスロジック修正（伝票からの正しい取得）
   - Phase 4: レポート処理の修正

2. **手入力項目の取得元確認**
   - 売上伝票CSV: 155列目（Index=154）から取得
   - 仕入伝票CSV: 147列目（Index=146）から取得  
   - 在庫調整CSV: 153列目（Index=152）から取得

3. **互換性維持**
   - 既存のShippingMarkNameは荷印マスタ由来として維持
   - ManualShippingMarkは新規追加（8文字固定、スペースパディング）
   - 移行期間中は両方のカラムを保持

4. **テスト重点項目**
   - 商品勘定帳票での手入力荷印表示
   - 5項目複合キーの一意性確保（ShippingMarkNameは維持）
   - 既存データとの互換性

5. **データ移行戦略**
   - 既存のCP在庫マスタは仮テーブルのため、削除・再作成で対応可能
   - 在庫マスタからCP在庫マスタへの転写時に手入力項目を正しく設定

## 7. 現状の問題点

1. **ShippingMarkNameの二重性**
   - データベース定義: 5項目キーの一部として使用
   - ストアドプロシージャ: ManualShippingMarkとしてエイリアス
   - 実際の値: 伝票の手入力項目が格納されている

2. **ManualShippingMarkの欠如**
   - CpInventoryMasterテーブル: カラムなし
   - CpInventoryMaster.csモデル: プロパティなし
   - しかしレポートモデルには存在し、ストアドで無理やりエイリアス

3. **import-folderコマンドの不完全性**
   - 等級名・階級名は更新している
   - ManualShippingMark（手入力項目）の設定処理がない

この調査により、方針A（ManualShippingMarkカラム追加）の実施が妥当であることが確認されました。