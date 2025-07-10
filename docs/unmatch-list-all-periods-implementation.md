# アンマッチリストコマンドの全期間対象化 実装報告書

## 概要
アンマッチリストコマンドを累積在庫管理に対応させ、全期間のデータを対象とするよう修正しました。

## 実装日
2025-07-10

## 修正内容

### 1. インターフェースの修正

#### ICpInventoryRepository
- `CreateCpInventoryFromInventoryMasterAsync`のJobDateパラメータをnullable化
- `AggregateSalesDataAsync`、`AggregatePurchaseDataAsync`、`AggregateInventoryAdjustmentDataAsync`のJobDateパラメータをnullable化

#### IInventoryRepository
- `GetLatestByKeyAsync`メソッドを追加（全期間から最新の在庫マスタを取得）

#### 伝票リポジトリインターフェース
- ISalesVoucherRepository、IPurchaseVoucherRepository、IInventoryAdjustmentRepositoryに`GetAllAsync`メソッドを追加

### 2. サービス層の修正

#### UnmatchListServiceV2.cs
- CP在庫マスタ作成時にnullを渡して全期間対象に変更
- 伝票データの集計時にnullを渡して全期間対象に変更
- 伝票チェック時に`GetAllAsync`を使用して全期間のデータを取得
- 在庫マスタから商品分類1を取得する際に`GetLatestByKeyAsync`を使用

### 3. リポジトリ層の修正

#### CpInventoryRepository.cs
- `CreateCpInventoryFromInventoryMasterAsync`：JobDateパラメータをnullable化
- `AggregateSalesDataAsync`：動的WHERE句構築で全期間対応
- `AggregatePurchaseDataAsync`：動的WHERE句構築で全期間対応
- `AggregateInventoryAdjustmentDataAsync`：動的WHERE句構築で全期間対応

#### InventoryRepository.cs
- `GetLatestByKeyAsync`メソッドを実装（JobDateとUpdatedDateの降順で最新レコードを取得）

#### 伝票リポジトリ
- SalesVoucherRepository、PurchaseVoucherRepository、InventoryAdjustmentRepositoryに`GetAllAsync`メソッドを実装

### 4. ストアドプロシージャの修正

#### sp_CreateCpInventoryFromInventoryMasterCumulative
- @JobDateパラメータをNULL許容に変更
- WHERE句でNULLチェックを追加（`@JobDate IS NULL OR sv.JobDate = @JobDate`）
- 全期間のデータを対象とする処理を実装

## 動作仕様

### コマンドの使用方法
```bash
dotnet run unmatch-list [日付]
# 例: dotnet run unmatch-list 2025-06-02
```

### 処理内容
1. **CP在庫マスタ作成**：在庫マスタの全レコードをCP在庫マスタにコピー
2. **伝票データ集計**：全期間の売上・仕入・在庫調整データを集計
3. **アンマッチチェック**：全期間の伝票データに対してアンマッチをチェック
4. **PDF生成**：指定日付をタイトルに使用してPDFを生成

### パフォーマンス考慮事項
- 全期間のデータを対象とするため、データ量が多い場合は処理時間が増加
- 適切なインデックスの設定が重要
- 必要に応じてバッチ処理の実装を検討

## テスト項目
1. 全期間のデータが正しく抽出されることを確認
2. PDFタイトルに指定日付が正しく表示されることを確認
3. パフォーマンスが許容範囲内であることを確認
4. 既存の日付指定処理との互換性を確認

## 注意事項
- ストアドプロシージャの更新が必要（本番環境への適用時は要注意）
- 大量データ時のメモリ使用量に注意
- タイムアウト設定の見直しが必要な場合がある