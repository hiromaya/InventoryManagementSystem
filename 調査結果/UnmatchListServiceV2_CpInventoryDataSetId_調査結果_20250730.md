# UnmatchListServiceV2.csとCP在庫マスタDataSetId削除調査結果

## 調査日時
2025年07月30日 15:30

## 1. UnmatchListServiceV2.cs調査結果

### 1.1 ファイル存在確認
- **ファイルパス**: `/src/InventorySystem.Core/Services/UnmatchListServiceV2.cs`
- **状態**: ✅ 存在する
- **作成目的**: 誤操作防止機能対応版のアンマッチリストサービス
- **実装日**: 2025年頃（コメントから推測）

### 1.2 使用状況
#### Program.csでのDI登録状況
- **現在の登録**: `builder.Services.AddScoped<IUnmatchListService, UnmatchListService>();` (215行目)
- **UnmatchListServiceV2の登録**: ❌ **登録されていない**
- **実際の使用箇所**: 
  - 569行目: `var unmatchListService = scopedServices.GetRequiredService<IUnmatchListService>();`
  - 3474行目: `var unmatchListService = services.GetRequiredService<IUnmatchListService>();`

#### クラス継承とインターフェース
- **継承**: `BatchProcessBase`を継承
- **実装インターフェース**: `IUnmatchListService`
- **機能**: 既存のUnmatchListServiceとほぼ同じ機能を実装

#### 主な特徴
1. **誤操作防止機能**: 処理初期化時のデータセット登録と履歴開始
2. **日付フィルタリング対応**: `targetDate`パラメータによる期間限定処理
3. **未完成部分**: 192行目のTODOコメントで一時的に既存実装を使用

### 1.3 削除可否判定
**🔴 削除推奨**
- **理由1**: DIコンテナに登録されておらず、実際に使用されていない
- **理由2**: 既存のUnmatchListServiceが本番で稼働中
- **理由3**: 実装が未完成（TODO部分が残存）
- **理由4**: 重複したコードベースの維持コストが発生

## 2. CP在庫マスタDataSetId使用状況

### 2.1 エンティティクラス
**ファイル**: `/src/InventorySystem.Core/Entities/CpInventoryMaster.cs`
- **DataSetId使用状況**: ❌ **既に削除済み**
- **コメント**: 86行目で「DataSetId管理を廃止（仮テーブル設計のため）」と明記
- **現在の管理方式**: 5項目複合キーのみで管理

### 2.2 リポジトリインターフェース
**ファイル**: `/src/InventorySystem.Core/Interfaces/ICpInventoryRepository.cs`
- **DataSetIdパラメータ**: ✅ **全メソッドで使用中**
- **主要メソッド**:
  - `CreateCpInventoryFromInventoryMasterAsync(string dataSetId, DateTime? jobDate)`
  - `GetByKeyAsync(InventoryKey key, string dataSetId)`
  - `GetAllAsync(string dataSetId)`
  - `DeleteByDataSetIdAsync(string dataSetId)`
  - その他18メソッドでDataSetIdを使用

### 2.3 リポジトリ実装
**ファイル**: `/src/InventorySystem.Data/Repositories/CpInventoryRepository.cs`
- **DataSetId使用箇所**: ✅ **50箇所以上で使用**
- **SQL文での使用**:
  - WHERE句でのフィルタリング: `WHERE DataSetId = @DataSetId`
  - 更新時の条件: `AND DataSetId = @DataSetId`
  - 削除処理: `DELETE FROM CpInventoryMaster WHERE DataSetId = @DataSetId`

### 2.4 サービスクラス
**影響を受けるサービス（DataSetIdを使用中）**:
1. `/src/InventorySystem.Core/Services/UnmatchListService.cs`
2. `/src/InventorySystem.Core/Services/UnmatchListServiceV2.cs`
3. `/src/InventorySystem.Core/Services/DailyReportService.cs`
4. `/src/InventorySystem.Core/Services/InventoryListService.cs`
5. `/src/InventorySystem.Core/Services/CpInventoryCreationService.cs`
6. その他11サービス

### 2.5 Program.cs
**ファイル**: `/src/InventorySystem.Console/Program.cs`
- **ExecuteProductAccountAsyncメソッド**: ✅ **DataSetIdを使用**
  - 1402行目: `var dataSetId = await salesVoucherRepository.GetDataSetIdByJobDateAsync(jobDate);`
  - 1412-1418行目: CP在庫マスタ操作でDataSetIdを使用

## 3. ストアドプロシージャ調査結果

### 3.1 sp_CreateCpInventoryFromInventoryMasterCumulative
**ファイル**: `/database/procedures/sp_CreateCpInventoryFromInventoryMasterCumulative.sql`
- **DataSetIdパラメータ**: ❌ **使用なし**
- **現在のパラメータ**: `@JobDate DATE = NULL`
- **テーブル処理**: `TRUNCATE TABLE CpInventoryMaster`（全削除方式）

### 3.2 その他のストアドプロシージャ
- **sp_AggregateSalesData**: リポジトリ内で動的SQL生成、DataSetIdを条件で使用
- **sp_AggregatePurchaseData**: リポジトリ内で動的SQL生成、DataSetIdを条件で使用
- **sp_AggregateInventoryAdjustmentData**: リポジトリ内で動的SQL生成、DataSetIdを条件で使用

## 4. 影響範囲まとめ

### 4.1 修正が必要なファイル一覧
**CP在庫マスタDataSetId削除時に修正が必要**:
1. `/src/InventorySystem.Core/Interfaces/ICpInventoryRepository.cs` - 全メソッドシグネチャ
2. `/src/InventorySystem.Data/Repositories/CpInventoryRepository.cs` - 50箇所以上のSQL修正
3. `/src/InventorySystem.Console/Program.cs` - ExecuteProductAccountAsyncメソッド
4. `/src/InventorySystem.Core/Services/UnmatchListService.cs` - メソッド呼び出し
5. `/src/InventorySystem.Core/Services/DailyReportService.cs` - メソッド呼び出し
6. `/src/InventorySystem.Core/Services/InventoryListService.cs` - メソッド呼び出し
7. その他14サービスクラス

### 4.2 修正が必要なメソッド一覧
**ICpInventoryRepositoryの全メソッド（25個）**:
- CreateCpInventoryFromInventoryMasterAsync
- ClearDailyAreaAsync
- GetByKeyAsync
- GetAllAsync
- AggregateSalesDataAsync
- AggregatePurchaseDataAsync
- AggregateInventoryAdjustmentDataAsync
- CalculateDailyStockAsync
- SetDailyFlagToProcessedAsync
- DeleteByDataSetIdAsync
- その他15メソッド

### 4.3 削除可能なファイル
1. **UnmatchListServiceV2.cs** - ✅ 削除推奨（使用されていない）

## 5. 推奨アクション

### 5.1 UnmatchListServiceV2.cs
**推奨**: 🔴 **即座に削除**
- **理由**: 使用されておらず、コードベースの複雑化を招いている
- **リスク**: なし（DIコンテナに登録されていないため）
- **作業**: ファイル削除のみ

### 5.2 CP在庫マスタDataSetId削除
**推奨**: 🟡 **段階的に実施**
- **Phase 1**: マイグレーション実行（041_RemoveDataSetIdFromCpInventory.sql）
- **Phase 2**: インターフェース修正（ICpInventoryRepository.cs）
- **Phase 3**: リポジトリ実装修正（CpInventoryRepository.cs）
- **Phase 4**: サービス層修正（17サービスクラス）
- **Phase 5**: Program.cs修正
- **Phase 6**: テスト実行・検証

## 6. リスク評価

### 6.1 UnmatchListServiceV2.cs削除
- **技術的リスク**: 🟢 **低**（使用されていない）
- **運用リスク**: 🟢 **低**（影響なし）
- **作業工数**: 🟢 **低**（1-2時間）

### 6.2 CP在庫マスタDataSetId削除
- **技術的リスク**: 🟡 **中**（大規模修正）
- **運用リスク**: 🔴 **高**（商品勘定・アンマッチリスト機能に影響）
- **作業工数**: 🔴 **高**（2-3日）

### 6.3 特に注意すべき点
1. **商品勘定帳票機能**: ExecuteProductAccountAsyncでDataSetIdを使用
2. **アンマッチリスト機能**: 全処理フローでDataSetIdが前提
3. **仮テーブル設計**: TRUNCATE方式への完全移行が必要
4. **データ整合性**: 複数サービス間でのDataSetId依存関係

## 7. 実装優先度

### 高優先度（即座に実施）
1. **UnmatchListServiceV2.cs削除** - 技術債務の解消

### 中優先度（計画的に実施）
2. **CP在庫マスタDataSetId削除** - アーキテクチャ改善

### 実施時期の推奨
- **UnmatchListServiceV2.cs削除**: 今すぐ
- **DataSetId削除**: 次期メンテナンス時期（影響範囲の大きさを考慮）

---

**調査実施者**: Claude Code  
**調査完了時刻**: 2025-07-30 15:30  
**調査対象システム**: InventoryManagementSystem v2.0