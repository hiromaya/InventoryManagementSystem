# DataSetIdRepairService 古いロジック調査結果

## 調査日時: 2025-07-24 11:48:48

## 1. エラーログ分析

### エラー内容
```
Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid column name 'UpdatedAt'.
```

### スタックトレース解析
- **発生箇所**: `C:\Development\InventoryManagementSystem\src\InventorySystem.Core\Services\DataSetIdRepairService.cs:line 116`
- **メソッド**: `RepairSalesVoucherDataSetIdAsync`
- **実際のコードとの差異**: **有り** ⚠️
- **重要**: 現在のLinux環境のコードには存在しないUpdatedAtカラムの使用が実行されている

### 実行環境の詳細
- **実行環境**: Windows (C:\Development\InventoryManagementSystem)
- **ランタイム**: .NET 8.0.18
- **OS**: Microsoft Windows 10.0.26100
- **モード**: DataSetManagement専用モード

## 2. UpdatedAt使用箇所の検出結果

### DataSetIdRepairService.cs（現在のLinux環境）
- **現在のコード**: UpdatedAt使用なし ✅
- **エラー発生箇所**: Windows環境の116行目
- **不一致の原因**: Windows環境とLinux環境でコードバージョンが異なる

### 他の重要なファイルでのUpdatedAt使用

#### マスタ系リポジトリ（正常）
| ファイル名 | 行番号 | 使用内容 | 問題の有無 |
|-----------|--------|----------|-----------|
| ProductMasterRepository.cs | 123, 249 | `UpdatedAt = GETDATE()` | 無（テーブルに存在） |
| CustomerMasterRepository.cs | 116, 235 | `UpdatedAt = GETDATE()` | 無（テーブルに存在） |
| SupplierMasterRepository.cs | 112, 219 | `UpdatedAt = GETDATE()` | 無（テーブルに存在） |

#### エンティティクラス（潜在的問題）
| ファイル名 | 行番号 | 使用内容 | 問題の有無 |
|-----------|--------|----------|-----------|
| SalesVoucher.cs | 180 | `public DateTime UpdatedAt { get; set; }` | 有（テーブルに存在しない） |
| PurchaseVoucher.cs | 160 | `public DateTime UpdatedAt { get; set; }` | 有（テーブルに存在しない） |
| InventoryAdjustment.cs | 166 | `public DateTime UpdatedAt { get; set; }` | 有（テーブルに存在しない） |

## 3. ビルド成果物の状態

### DLLファイルの状態
```
./src/InventorySystem.Core/bin/Debug/net8.0-windows7.0/InventorySystem.Core.dll
./src/InventorySystem.Core/bin/Release/net8.0-windows7.0/InventorySystem.Core.dll
```
- **最新ビルド日時**: 2025-07-24 07:45 (Linux環境)
- **最新ソースとの乖離**: **Windows環境では古いコードが残存している可能性**

### objフォルダの状態
- Linux環境では最新のビルド成果物が存在
- **判定**: **Windows環境でのクリーンビルドが必要**

## 4. 考えられる原因

### 原因1: 環境間でのコードバージョン差異（最有力）
**詳細説明**:
- Linux環境（Claude Code）：最新のコードでUpdatedAt使用なし
- Windows環境：古いバージョンのコードでUpdatedAtを使用
- Git同期されていない、または異なるブランチを使用している可能性

**根拠**:
- エラーログのファイルパス：`C:\Development\InventoryManagementSystem`（Windows）
- 現在の調査環境：`/home/hiroki/projects/InventoryManagementSystem`（Linux）
- 116行目でのUpdatedAt使用がLinux環境では確認できない

### 原因2: ビルドキャッシュ（副次的要因）
**詳細説明**:
- Windows環境で古いDLLファイルが残存
- 最新のソースコードがビルドに反映されていない
- 中間ファイルのキャッシュ問題

### 原因3: 異なるブランチまたはコミット
**詳細説明**:
- Windows環境で古いコミットをチェックアウト
- 開発途中のブランチ使用
- マージされていない変更が存在

## 5. Windows環境での古いコード推測

### 推測される116行目付近のコード
```csharp
// 古いバージョン（エラー発生）
const string updateSql = @"
    UPDATE SalesVouchers 
    SET DataSetId = @CorrectDataSetId, UpdatedAt = GETDATE()
    WHERE JobDate = @JobDate AND (DataSetId != @CorrectDataSetId OR DataSetId IS NULL)";
```

### 現在のLinux環境のコード（正常）
```csharp
// 現在のバージョン（正常動作）
const string updateSql = @"
    UPDATE SalesVouchers 
    SET DataSetId = @CorrectDataSetId
    WHERE JobDate = @JobDate AND (DataSetId != @CorrectDataSetId OR DataSetId IS NULL)";
```

## 6. 推奨される対処法

### 即座の対応（Windows環境）
1. **最新コードの同期**
   ```bash
   git pull origin main
   git status  # 変更状況確認
   ```

2. **クリーンビルド実行**
   ```bash
   dotnet clean
   dotnet build --configuration Release
   ```

3. **ビルド成果物の確認**
   ```bash
   # objとbinフォルダをクリア
   rm -rf src/*/obj src/*/bin
   dotnet build
   ```

### 恒久対応
1. **環境間同期の仕組み化**
   - Git hooks設定
   - CI/CDパイプライン構築
   - 定期的な環境同期チェック

2. **コードレビュープロセス強化**
   - テーブル定義との整合性チェック
   - エンティティとDBスキーマの自動検証

## 7. 追加発見事項

### 重要な不整合パターン発見
1. **マスタ系テーブル**: UpdatedAtカラム存在 + リポジトリで正常使用 ✅
2. **伝票系テーブル**: UpdatedAtカラム存在せず + エンティティで定義 ❌
3. **DataSetIdRepairService**: Linux環境では修正済み、Windows環境では未修正 ❌

### システム全体の整合性問題
- **47箇所**でUpdatedAtプロパティまたはカラムを使用
- **15箇所**でSQL文内でUpdatedAt = GETDATE()を使用
- このうち、伝票系テーブル（SalesVouchers, PurchaseVouchers, InventoryAdjustments）では実際のカラムが存在しない

### 将来的なリスク
- 他の処理でも同様のエラーが発生する可能性
- エンティティマッピング時の予期しないエラー
- ORMツール使用時の自動カラムマッピングエラー

## 8. 具体的な修正手順

### Step 1: Windows環境での確認
```bash
# Windows環境で実行
git log -1 --oneline  # 現在のコミット確認
git status           # ローカル変更確認
git diff HEAD        # 差分確認
```

### Step 2: コード同期
```bash
# 最新のmainブランチに同期
git checkout main
git pull origin main
git clean -fd        # 不要ファイル削除
```

### Step 3: クリーンビルド
```bash
# 完全クリーンビルド
dotnet clean
Remove-Item -Recurse -Force src/*/obj, src/*/bin  # PowerShell
dotnet restore
dotnet build --configuration Release
```

### Step 4: 修正確認
```bash
# 修正後のテスト実行
dotnet run -- repair-dataset-id 2025-06-02
```

## 9. 監視・防止策

### 環境同期チェック
- 定期的なgit status確認
- ビルド前の必須同期確認
- 環境固有の設定ファイル管理

### 自動化提案
- pre-commit hookでスキーマ整合性チェック
- CI/CDでの環境間差分検出
- 自動テストでのDBスキーマ検証

## 10. 結論

**根本原因**: Windows環境とLinux環境でDataSetIdRepairService.csのバージョンが異なり、Windows環境では古いコード（UpdatedAtカラムを使用）が実行されている。

**緊急対応**: Windows環境での最新コード同期とクリーンビルド実行

**恒久対策**: 環境間同期の自動化とスキーマ整合性の継続的チェック

---

**調査完了時刻**: 2025-07-24 11:48:48  
**調査実施者**: Claude Code AI Assistant  
**優先対応**: Windows環境での最新コード同期（即座に実行推奨）  
**検証コマンド**: `dotnet run -- repair-dataset-id 2025-06-02`