using Microsoft.Extensions.Logging;
using InventorySystem.Core.Base;
using InventorySystem.Core.Constants;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;
using InventorySystem.Core.Services.Dataset;
using InventorySystem.Core.Services.History;
using InventorySystem.Core.Services.Validation;

namespace InventorySystem.Core.Services;

/// <summary>
/// 日次終了処理サービス
/// </summary>
public class DailyCloseService : BatchProcessBase, IDailyCloseService
{
    private readonly IBackupService _backupService;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICpInventoryRepository _cpInventoryRepository;
    private readonly IDailyCloseManagementRepository _dailyCloseRepository;
    
    public DailyCloseService(
        IDateValidationService dateValidator,
        IDatasetManager datasetManager,
        IProcessHistoryService historyService,
        IBackupService backupService,
        IInventoryRepository inventoryRepository,
        ICpInventoryRepository cpInventoryRepository,
        IDailyCloseManagementRepository dailyCloseRepository,
        ILogger<DailyCloseService> logger)
        : base(dateValidator, datasetManager, historyService, logger)
    {
        _backupService = backupService;
        _inventoryRepository = inventoryRepository;
        _cpInventoryRepository = cpInventoryRepository;
        _dailyCloseRepository = dailyCloseRepository;
    }
    
    /// <inheritdoc/>
    public async Task ExecuteDailyClose(DateTime jobDate, string executedBy = "System")
    {
        _logger.LogInformation("日次終了処理開始: JobDate={JobDate}, ExecutedBy={ExecutedBy}", 
            jobDate, executedBy);
        
        // 1. 商品日報との紐付けチェック
        var dailyReportDatasetId = await _datasetManager.GetLatestDatasetId("DAILY_REPORT", jobDate);
        if (string.IsNullOrEmpty(dailyReportDatasetId))
        {
            throw new InvalidOperationException(ErrorMessages.NoDailyReportError);
        }
        
        // 2. 同じデータセットで既に処理済みかチェック
        var existingHistories = await _historyService.GetProcessHistory(jobDate, "DAILY_CLOSE");
        if (existingHistories.Any(h => h.DatasetId == dailyReportDatasetId && h.Status == ProcessStatus.Completed))
        {
            throw new InvalidOperationException("このデータセットは既に日次終了処理済みです。");
        }
        
        // 3. バックアップ作成
        _logger.LogInformation("バックアップ作成開始");
        var backupPath = await _backupService.CreateBackup("DAILY_CLOSE", jobDate);
        _logger.LogInformation("バックアップ作成完了: {BackupPath}", backupPath);
        
        // 4. 処理実行（商品日報と同じデータセットIDを使用）
        var context = new ProcessContext
        {
            JobDate = jobDate,
            DatasetId = dailyReportDatasetId, // 商品日報と同じID
            ProcessType = "DAILY_CLOSE",
            ExecutedBy = executedBy
        };
        
        var history = await _historyService.StartProcess(
            dailyReportDatasetId, jobDate, "DAILY_CLOSE", executedBy);
        context.ProcessHistory = history;
        
        try
        {
            // 在庫マスタ更新（原本を直接更新）
            await UpdateInventoryMaster(context);
            
            // 日次終了管理テーブルに記録
            await RecordDailyClose(jobDate, dailyReportDatasetId, backupPath, executedBy);
            
            // 古いバックアップのクリーンアップ
            var retentionDays = 30; // 設定から読み取ることも可能
            await _backupService.CleanupOldBackups(retentionDays);
            
            // 完了処理
            await _historyService.CompleteProcess(history.Id, true, "日次終了処理が正常に完了しました。");
            
            // メール送信
            await _historyService.SendCompletionEmail(history);
            
            _logger.LogInformation("日次終了処理完了: JobDate={JobDate}", jobDate);
        }
        catch (Exception ex)
        {
            await _historyService.CompleteProcess(history.Id, false, ex.Message);
            _logger.LogError(ex, "日次終了処理エラー。バックアップから復元可能: {BackupPath}", backupPath);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<bool> IsDailyClosedAsync(DateTime jobDate)
    {
        var dailyClose = await _dailyCloseRepository.GetByJobDateAsync(jobDate);
        return dailyClose != null;
    }
    
    /// <summary>
    /// 在庫マスタを更新
    /// </summary>
    private async Task UpdateInventoryMaster(ProcessContext context)
    {
        _logger.LogInformation("在庫マスタ更新開始: DatasetId={DatasetId}", context.DatasetId);
        
        try
        {
            // CP在庫マスタから在庫マスタへ反映
            var updateCount = await _inventoryRepository.UpdateFromCpInventoryAsync(
                context.DatasetId, context.JobDate);
            
            _logger.LogInformation("在庫マスタ更新完了: 更新件数={Count}", updateCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫マスタ更新エラー");
            throw;
        }
    }
    
    /// <summary>
    /// 日次終了管理テーブルに記録
    /// </summary>
    private async Task RecordDailyClose(
        DateTime jobDate, 
        string datasetId, 
        string backupPath,
        string executedBy)
    {
        var dailyClose = new DailyCloseManagement
        {
            JobDate = jobDate,
            DatasetId = datasetId,
            DailyReportDatasetId = datasetId, // 商品日報と同じID
            BackupPath = backupPath,
            ProcessedAt = DateTime.Now,
            ProcessedBy = executedBy
        };
        
        try
        {
            await _dailyCloseRepository.CreateAsync(dailyClose);
            _logger.LogInformation("日次終了管理記録完了: JobDate={JobDate}", jobDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日次終了管理記録エラー");
            throw;
        }
    }
}