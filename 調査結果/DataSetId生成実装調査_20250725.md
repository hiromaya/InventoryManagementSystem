# DataSetId生成実装調査報告

## 調査日時
2025年7月25日

## エラー概要
外部キー制約違反: FK_DataSetManagement_Parent
エラー発生DataSetId: CARRYOVER_20250601_113830_10EP3H

## SQL調査結果

### DataSetManagementテーブル構造
クエリ２/16.csvより確認されたテーブル構造：

| カラム名 | データ型 | NULL許可 | デフォルト値 | 説明 |
|---------|----------|----------|--------------|------|
| DataSetId | nvarchar | NO | NULL | 主キー |
| JobDate | date | NO | NULL | ジョブ実行日 |
| ImportType | nvarchar | NO | NULL | インポート種別 |
| RecordCount | int | NO | ((0)) | レコード数 |
| IsActive | bit | NO | ((1)) | アクティブフラグ |
| IsArchived | bit | NO | ((0)) | アーカイブフラグ |
| ParentDataSetId | nvarchar | YES | NULL | 親DataSetId（自己参照） |
| CreatedAt | datetime2 | NO | (getdate()) | 作成日時 |
| CreatedBy | nvarchar | NO | NULL | 作成者 |
| DeactivatedAt | datetime2 | YES | NULL | 無効化日時 |
| DeactivatedBy | nvarchar | YES | NULL | 無効化者 |
| ArchivedAt | datetime2 | YES | NULL | アーカイブ日時 |
| ArchivedBy | nvarchar | YES | NULL | アーカイブ者 |
| Notes | nvarchar | YES | NULL | 備考 |
| ProcessType | nvarchar | NO | NULL | 処理種別 |
| TotalRecordCount | int | NO | ((0)) | 総レコード数 |
| ImportedFiles | nvarchar | YES | NULL | インポートファイル |
| Department | nvarchar | NO | NULL | 部門 |
| UpdatedAt | datetime2 | YES | NULL | 更新日時 |
| DataSetType | nvarchar | YES | NULL | データセット種別 |
| Name | nvarchar | YES | NULL | 名称 |
| Description | nvarchar | YES | NULL | 説明 |
| ErrorMessage | nvarchar | YES | NULL | エラーメッセージ |
| FilePath | nvarchar | YES | NULL | ファイルパス |
| Status | nvarchar | YES | NULL | ステータス |

### 外部キー制約詳細
クエリ２/17.csvより確認：
- **制約名**: FK_DataSetManagement_Parent
- **親テーブル**: DataSetManagement
- **親カラム**: ParentDataSetId
- **参照テーブル**: DataSetManagement
- **参照カラム**: DataSetId

これは自己参照外部キー制約で、ParentDataSetIdに指定された値が同テーブルのDataSetIdに存在しない場合にエラーが発生します。

### 問題のレコード分析
クエリ２/18.csvから確認されたエラー関連DataSetId：
- **CARRYOVER_20250601_113830_10EP3H** というDataSetIdが存在しない
- クエリ２/18.csvには存在するレコードはすべてParentDataSetId=NULLまたはGUID形式
- 問題のCARRYOVER形式のDataSetIdが見当たらない

## コード調査結果

### 1. DataSetId生成の実装状況

#### DataSetIdManager.cs
- **主要機能**: JobExecutionLogテーブルを使用してDataSetIdを管理
- **生成方式**: `Guid.NewGuid().ToString()` で常にGUID形式を生成
- **CreateNewDataSetIdAsync()**: line 193-223で新しいDataSetIdを生成
- **重要な仕様**: 常にGUID形式でDataSetIdを生成（CARRYOVER_形式は生成しない）

```csharp
// line 201: 常に新しいDataSetIdを生成
var newDataSetId = Guid.NewGuid().ToString();
```

#### DataSetManagementFactory.cs
- **CreateForCarryover()**: line 87-126で繰越処理用エンティティを作成
- **Name設定**: `CARRYOVER_{targetDate:yyyyMMdd}_{currentTime:HHmmss}` 形式でNameを設定
- **DataSetId**: 外部から渡されるGUID形式を使用
- **ParentDataSetId**: line 107でparentDataSetIdパラメータをそのまま設定

### 2. 在庫引継ぎ処理での問題箇所

#### InventoryRepository.cs (line 1503)
```csharp
await connection.ExecuteAsync(datasetSql, dataSetManagement, transaction);
```
- DataSetManagementレコードをINSERTする際に外部キー制約違反が発生
- dataSetManagementオブジェクトのParentDataSetIdが存在しないDataSetIdを参照

#### Program.cs (line 3982)
```csharp
// line 3975: ParentDataSetIdの設定
parentDataSetId: previousInventory.FirstOrDefault()?.DataSetId,

// line 3982: ProcessCarryoverInTransactionAsyncの呼び出し
var affectedRows = await inventoryRepository.ProcessCarryoverInTransactionAsync(
    carryoverInventory, 
    targetDate, 
    dataSetId,
    datasetManagement);
```

### 3. 問題の原因分析

#### 根本原因
1. **ParentDataSetIdの不整合**: Program.cs line 3975で設定される`previousInventory.FirstOrDefault()?.DataSetId`が存在しないDataSetIdを参照
2. **DataSetId形式の不一致**: 
   - エラーメッセージの`CARRYOVER_20250601_113830_10EP3H`は実際のDataSetIdではなく、Nameフィールドまたはログメッセージの可能性
   - 実際のDataSetIdはGUID形式だが、参照しようとしているParentDataSetIdが存在しない

#### 具体的な問題シナリオ
1. 前日の在庫データから`previousInventory.FirstOrDefault()?.DataSetId`を取得
2. 取得したDataSetIdが既に削除されているか、存在しない
3. このDataSetIdをParentDataSetIdとして設定
4. DataSetManagementテーブルにINSERT時に外部キー制約違反が発生

### 4. 修正方針の提案

#### 即座の対応
1. **ParentDataSetId検証の追加**:
   ```csharp
   // DataSetManagementレコード作成前にParentDataSetIdの存在確認
   if (!string.IsNullOrEmpty(parentDataSetId))
   {
       var exists = await connection.QuerySingleOrDefaultAsync<int>(
           "SELECT COUNT(*) FROM DataSetManagement WHERE DataSetId = @ParentId",
           new { ParentId = parentDataSetId });
       
       if (exists == 0)
       {
           logger.LogWarning("ParentDataSetIdが存在しないためNULLに設定: {ParentId}", parentDataSetId);
           parentDataSetId = null;
       }
   }
   ```

2. **トランザクション改善**:
   - ParentDataSetId設定時の厳密な検証
   - エラー発生時の詳細ログ出力

#### 長期的改善
1. **DataSetIdライフサイクル管理の改善**
2. **外部キー制約違反の予防機構**
3. **データ整合性チェックの強化**

## 補足情報

### データ不整合の可能性
- クエリ２/18.csvで確認されるDataSetIdはすべてGUID形式
- エラーメッセージの`CARRYOVER_20250601_113830_10EP3H`形式は実際のDataSetIdではない可能性
- 実際の問題は、ParentDataSetIdとして指定されたGUID形式のDataSetIdが存在しないこと

### 調査で判明した設計仕様
- DataSetIdは常にGUID形式で生成される
- Nameフィールドに`CARRYOVER_YYYYMMDD_HHMMSS_形式`の識別名を設定
- ParentDataSetIdによる階層構造を持つデータセット管理
- 自己参照外部キー制約による親子関係の保証

### 今後の監視ポイント
- previousInventory.FirstOrDefault()?.DataSetIdの値の妥当性
- DataSetManagementテーブルの整合性
- 在庫引継ぎ処理の前段階でのデータ状態