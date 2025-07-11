using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Interfaces;

public interface IInventoryRepository
{
    Task<IEnumerable<InventoryMaster>> GetByJobDateAsync(DateTime jobDate);
    Task<InventoryMaster?> GetByKeyAsync(InventoryKey key, DateTime jobDate);
    Task<int> CreateAsync(InventoryMaster inventory);
    Task<int> UpdateAsync(InventoryMaster inventory);
    Task<int> DeleteByJobDateAsync(DateTime jobDate);
    Task<int> ClearDailyFlagAsync(DateTime jobDate);
    Task<int> BulkInsertAsync(IEnumerable<InventoryMaster> inventories);
    
    /// <summary>
    /// 売上・仕入伝票に対応する在庫マスタのJobDateを更新する
    /// </summary>
    Task<int> UpdateJobDateForVouchersAsync(DateTime jobDate);
    
    /// <summary>
    /// 新規商品を在庫マスタに登録する
    /// </summary>
    Task<int> RegisterNewProductsAsync(DateTime jobDate);
    
    /// <summary>
    /// CP在庫マスタから在庫マスタを更新する（日次終了処理用）
    /// </summary>
    Task<int> UpdateFromCpInventoryAsync(string dataSetId, DateTime jobDate);
    
    /// <summary>
    /// 在庫マスタから任意の日付で商品キーに一致するレコードを取得（最新日付優先）
    /// </summary>
    Task<InventoryMaster?> GetByKeyAnyDateAsync(InventoryKey key);
    
    /// <summary>
    /// 売上・仕入・在庫調整から在庫マスタの初期データを作成
    /// </summary>
    Task<int> CreateInitialInventoryFromVouchersAsync(DateTime jobDate);
    
    /// <summary>
    /// 指定日付の在庫マスタ件数を取得
    /// </summary>
    Task<int> GetCountByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// 伝票データから在庫マスタを更新または作成（累積管理対応）
    /// </summary>
    Task<int> UpdateOrCreateFromVouchersAsync(DateTime jobDate, string datasetId);
    
    /// <summary>
    /// 重複レコードのクリーンアップ（一時的な修正処理）
    /// </summary>
    Task<int> CleanupDuplicateRecordsAsync();
    
    /// <summary>
    /// 月初に前月末在庫からCurrentStockを初期化
    /// </summary>
    Task<int> InitializeMonthlyInventoryAsync(string yearMonth);
    
    /// <summary>
    /// 指定されたキーで最新の在庫マスタを取得（全期間対象）
    /// </summary>
    Task<InventoryMaster?> GetLatestByKeyAsync(InventoryKey key);
    
    /// <summary>
    /// 指定日付のアクティブな在庫マスタを取得
    /// </summary>
    Task<List<InventoryMaster>> GetActiveByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// 指定日付のアクティブな初期在庫を取得
    /// </summary>
    Task<List<InventoryMaster>> GetActiveInitInventoryAsync(DateTime lastMonthEnd);
    
    /// <summary>
    /// データセットIDを指定して無効化
    /// </summary>
    Task DeactivateDataSetAsync(string dataSetId);
    
    /// <summary>
    /// 指定日付のデータを無効化
    /// </summary>
    Task DeactivateByJobDateAsync(DateTime jobDate);
    
    /// <summary>
    /// 最新のINIT（前月末在庫）データを取得
    /// </summary>
    Task<List<InventoryMaster>> GetLatestInitInventoryAsync();
    
    /// <summary>
    /// 最新の有効な在庫データを取得（日付に関係なく）
    /// </summary>
    Task<List<InventoryMaster>> GetLatestActiveInventoryAsync();
    
    /// <summary>
    /// 最終処理日（最新のJobDate）を取得
    /// </summary>
    Task<DateTime> GetMaxJobDateAsync();
    
    /// <summary>
    /// 全有効在庫データを取得（日付関係なく最新の状態）
    /// </summary>
    Task<List<InventoryMaster>> GetAllActiveInventoryAsync();
    
    /// <summary>
    /// 在庫データのMERGE処理（既存は更新、新規は挿入）
    /// </summary>
    Task<int> MergeInventoryAsync(List<InventoryMaster> inventories, DateTime targetDate, string dataSetId);
    
    /// <summary>
    /// ImportTypeで在庫データを取得
    /// </summary>
    Task<IEnumerable<InventoryMaster>> GetByImportTypeAsync(string importType);
    
    /// <summary>
    /// ImportTypeで在庫データを無効化
    /// </summary>
    Task<int> DeactivateByImportTypeAsync(string importType);
    
    /// <summary>
    /// トランザクション内で初期在庫データを一括処理
    /// </summary>
    /// <param name="inventories">登録する在庫データリスト</param>
    /// <param name="datasetManagement">データセット管理情報</param>
    /// <param name="deactivateExisting">既存のINITデータを無効化するか</param>
    /// <returns>処理件数</returns>
    Task<int> ProcessInitialInventoryInTransactionAsync(
        List<InventoryMaster> inventories, 
        DatasetManagement datasetManagement,
        bool deactivateExisting = true);
    
    /// <summary>
    /// トランザクション内で在庫引継ぎ処理を実行
    /// </summary>
    /// <param name="inventories">更新する在庫データリスト</param>
    /// <param name="targetDate">処理対象日</param>
    /// <param name="dataSetId">データセットID</param>
    /// <param name="datasetManagement">データセット管理情報</param>
    /// <returns>処理件数</returns>
    Task<int> ProcessCarryoverInTransactionAsync(
        List<InventoryMaster> inventories,
        DateTime targetDate,
        string dataSetId,
        DatasetManagement datasetManagement);
}