using Microsoft.Extensions.Logging;
using InventorySystem.Core.Base;
using InventorySystem.Core.Constants;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;
using InventorySystem.Core.Services.Dataset;
using InventorySystem.Core.Services.History;
using InventorySystem.Core.Services.Validation;
using System.Security.Cryptography;
using System.Text;

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
    private readonly ISalesVoucherRepository _salesRepository;
    private readonly IPurchaseVoucherRepository _purchaseRepository;
    private readonly IInventoryAdjustmentRepository _adjustmentRepository;
    
    public DailyCloseService(
        IDateValidationService dateValidator,
        IDatasetManager datasetManager,
        IProcessHistoryService historyService,
        IBackupService backupService,
        IInventoryRepository inventoryRepository,
        ICpInventoryRepository cpInventoryRepository,
        IDailyCloseManagementRepository dailyCloseRepository,
        ISalesVoucherRepository salesRepository,
        IPurchaseVoucherRepository purchaseRepository,
        IInventoryAdjustmentRepository adjustmentRepository,
        ILogger<DailyCloseService> logger)
        : base(dateValidator, datasetManager, historyService, logger)
    {
        _backupService = backupService;
        _inventoryRepository = inventoryRepository;
        _cpInventoryRepository = cpInventoryRepository;
        _dailyCloseRepository = dailyCloseRepository;
        _salesRepository = salesRepository;
        _purchaseRepository = purchaseRepository;
        _adjustmentRepository = adjustmentRepository;
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
        
        // 2. 時間的制約の検証
        await ValidateProcessingTime(jobDate, dailyReportDatasetId);
        
        // 3. データ整合性の詳細確認
        var validationResult = await ValidateDataIntegrity(jobDate, dailyReportDatasetId);
        if (!validationResult.IsValid)
        {
            var changesDetails = string.Join(", ", validationResult.Changes);
            throw new InvalidOperationException(string.Format(ErrorMessages.DataIntegrityError, changesDetails));
        }
        
        // 4. 同じデータセットで既に処理済みかチェック
        var existingHistories = await _historyService.GetProcessHistory(jobDate, "DAILY_CLOSE");
        if (existingHistories.Any(h => h.DatasetId == dailyReportDatasetId && h.Status == ProcessStatus.Completed))
        {
            throw new InvalidOperationException("このデータセットは既に日次終了処理済みです。");
        }
        
        // 5. バックアップ作成
        _logger.LogInformation("バックアップ作成開始");
        var backupPath = await _backupService.CreateBackup("DAILY_CLOSE", jobDate);
        _logger.LogInformation("バックアップ作成完了: {BackupPath}", backupPath);
        
        // 6. 処理実行（商品日報と同じデータセットIDを使用）
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
            
            // 日次終了管理テーブルに記録（ハッシュ値付き）
            await RecordDailyClose(jobDate, dailyReportDatasetId, backupPath, executedBy, validationResult.CurrentHash);
            
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
    /// 日次終了処理の確認情報を取得
    /// </summary>
    public async Task<DailyCloseConfirmation> GetConfirmationInfo(DateTime jobDate)
    {
        var confirmation = new DailyCloseConfirmation
        {
            JobDate = jobDate,
            CurrentTime = DateTime.Now,
            CanProcess = true
        };
        
        try
        {
            // 商品日報情報の取得
            var dailyReportDatasetId = await _datasetManager.GetLatestDatasetId("DAILY_REPORT", jobDate);
            if (!string.IsNullOrEmpty(dailyReportDatasetId))
            {
                var dailyReportHistory = await _historyService.GetProcessHistory(jobDate, "DAILY_REPORT");
                var latestDailyReport = dailyReportHistory
                    .Where(h => h.DatasetId == dailyReportDatasetId && h.Status == ProcessStatus.Completed)
                    .OrderByDescending(h => h.EndTime)
                    .FirstOrDefault();
                    
                if (latestDailyReport != null)
                {
                    confirmation.DailyReport = new DailyReportInfo
                    {
                        CreatedAt = latestDailyReport.EndTime ?? latestDailyReport.StartTime,
                        CreatedBy = latestDailyReport.ExecutedBy,
                        DatasetId = latestDailyReport.DatasetId,
                        DataHash = latestDailyReport.DataHash
                    };
                }
            }
            else
            {
                confirmation.ValidationResults.Add(new ValidationMessage
                {
                    Level = ValidationLevel.Error,
                    Message = "商品日報が作成されていません",
                    Detail = ErrorMessages.DailyReportNotFound
                });
                confirmation.CanProcess = false;
            }
            
            // 最新CSV取込情報の取得
            var importHistories = await _historyService.GetProcessHistory(jobDate, "CSV_IMPORT");
            var latestImport = importHistories
                .Where(h => h.Status == ProcessStatus.Completed)
                .OrderByDescending(h => h.EndTime)
                .FirstOrDefault();
                
            if (latestImport != null)
            {
                confirmation.LatestCsvImport = new CsvImportInfo
                {
                    ImportedAt = latestImport.EndTime ?? latestImport.StartTime,
                    ImportedBy = latestImport.ExecutedBy,
                    FileNames = "売上伝票, 仕入伝票, 在庫調整" // TODO: 実際のファイル名を取得
                };
            }
            
            // データ件数サマリー
            confirmation.DataCounts = new DataCountSummary
            {
                SalesCount = await _salesRepository.GetCountAsync(jobDate),
                PurchaseCount = await _purchaseRepository.GetCountAsync(jobDate),
                AdjustmentCount = await _adjustmentRepository.GetCountAsync(jobDate),
                CpInventoryCount = await _cpInventoryRepository.GetCountAsync(jobDate)
            };
            
            // 金額サマリー
            var salesAmount = await _salesRepository.GetTotalAmountAsync(jobDate);
            var purchaseAmount = await _purchaseRepository.GetTotalAmountAsync(jobDate);
            confirmation.Amounts = new AmountSummary
            {
                SalesAmount = salesAmount,
                PurchaseAmount = purchaseAmount,
                EstimatedGrossProfit = salesAmount - purchaseAmount
            };
            
            // 現在のデータハッシュ
            confirmation.CurrentDataHash = await CalculateCurrentDataHash(jobDate);
            
            // 時間的制約の検証
            try
            {
                if (!string.IsNullOrEmpty(dailyReportDatasetId))
                {
                    await ValidateProcessingTime(jobDate, dailyReportDatasetId);
                }
            }
            catch (InvalidOperationException ex)
            {
                confirmation.ValidationResults.Add(new ValidationMessage
                {
                    Level = ValidationLevel.Error,
                    Message = "時間的制約違反",
                    Detail = ex.Message
                });
                confirmation.CanProcess = false;
            }
            
            // データ整合性の検証
            if (!string.IsNullOrEmpty(dailyReportDatasetId))
            {
                var validationResult = await ValidateDataIntegrity(jobDate, dailyReportDatasetId);
                if (!validationResult.IsValid)
                {
                    confirmation.ValidationResults.Add(new ValidationMessage
                    {
                        Level = ValidationLevel.Error,
                        Message = "データ整合性エラー",
                        Detail = string.Format(ErrorMessages.DataIntegrityError, string.Join(", ", validationResult.Changes))
                    });
                    confirmation.CanProcess = false;
                }
                
                foreach (var warning in validationResult.Warnings)
                {
                    confirmation.ValidationResults.Add(new ValidationMessage
                    {
                        Level = ValidationLevel.Warning,
                        Message = warning
                    });
                }
            }
            
            // 既に日次終了済みかチェック
            if (await IsDailyClosedAsync(jobDate))
            {
                var dailyClose = await _dailyCloseRepository.GetByJobDateAsync(jobDate);
                confirmation.ValidationResults.Add(new ValidationMessage
                {
                    Level = ValidationLevel.Error,
                    Message = "既に日次終了処理済み",
                    Detail = string.Format(ErrorMessages.DailyCloseAlreadyExecuted, 
                        dailyClose?.ProcessedAt.ToString("yyyy-MM-dd HH:mm"))
                });
                confirmation.CanProcess = false;
            }
            
            // 情報メッセージ
            if (confirmation.CanProcess)
            {
                confirmation.ValidationResults.Add(new ValidationMessage
                {
                    Level = ValidationLevel.Info,
                    Message = "日次終了処理を実行可能です"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "確認情報取得エラー");
            confirmation.ValidationResults.Add(new ValidationMessage
            {
                Level = ValidationLevel.Error,
                Message = "確認情報の取得に失敗しました",
                Detail = ex.Message
            });
            confirmation.CanProcess = false;
        }
        
        return confirmation;
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
        string executedBy,
        string? dataHash)
    {
        var dailyClose = new DailyCloseManagement
        {
            JobDate = jobDate,
            DatasetId = datasetId,
            DailyReportDatasetId = datasetId, // 商品日報と同じID
            BackupPath = backupPath,
            ProcessedAt = DateTime.Now,
            ProcessedBy = executedBy,
            DataHash = dataHash,
            ValidationStatus = "PASSED"
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
    
    /// <summary>
    /// 時間的制約の検証
    /// </summary>
    private async Task ValidateProcessingTime(DateTime jobDate, string dailyReportDatasetId)
    {
        var now = DateTime.Now;
        
        // 1. 現在時刻が15:00以降かチェック
        if (now.Hour < 15)
        {
            throw new InvalidOperationException(
                string.Format(ErrorMessages.DailyCloseTimeTooEarly, now.ToString("HH:mm")));
        }
        
        // 2. 商品日報の作成時刻を取得
        var dailyReportHistory = await _historyService.GetProcessHistory(jobDate, "DAILY_REPORT");
        var latestDailyReport = dailyReportHistory
            .Where(h => h.DatasetId == dailyReportDatasetId && h.Status == ProcessStatus.Completed)
            .OrderByDescending(h => h.EndTime)
            .FirstOrDefault();
            
        if (latestDailyReport?.EndTime != null)
        {
            var elapsedMinutes = (now - latestDailyReport.EndTime.Value).TotalMinutes;
            if (elapsedMinutes < 30)
            {
                throw new InvalidOperationException(
                    string.Format(ErrorMessages.DailyReportTooRecent, 
                        latestDailyReport.EndTime.Value.ToString("HH:mm"), 
                        (int)elapsedMinutes));
            }
        }
        
        // 3. 最新CSV取込時刻を取得
        var importHistories = await _historyService.GetProcessHistory(jobDate, "CSV_IMPORT");
        var latestImport = importHistories
            .Where(h => h.Status == ProcessStatus.Completed)
            .OrderByDescending(h => h.EndTime)
            .FirstOrDefault();
            
        if (latestImport?.EndTime != null)
        {
            var elapsedMinutes = (now - latestImport.EndTime.Value).TotalMinutes;
            if (elapsedMinutes < 5)
            {
                throw new InvalidOperationException(
                    string.Format(ErrorMessages.CsvImportTooRecent, 
                        latestImport.EndTime.Value.ToString("HH:mm"), 
                        (int)elapsedMinutes));
            }
        }
    }
    
    /// <summary>
    /// データ整合性の検証
    /// </summary>
    private async Task<DataValidationResult> ValidateDataIntegrity(DateTime jobDate, string dailyReportDatasetId)
    {
        var result = new DataValidationResult { IsValid = true };
        
        try
        {
            // 1. 商品日報のハッシュ値を取得
            var dailyReportHistory = await _historyService.GetProcessHistory(jobDate, "DAILY_REPORT");
            var latestDailyReport = dailyReportHistory
                .Where(h => h.DatasetId == dailyReportDatasetId && h.Status == ProcessStatus.Completed)
                .OrderByDescending(h => h.EndTime)
                .FirstOrDefault();
                
            result.ExpectedHash = latestDailyReport?.DataHash;
            
            // 2. 現在のデータハッシュを計算
            result.CurrentHash = await CalculateCurrentDataHash(jobDate);
            
            // 3. ハッシュ値の比較
            if (!string.IsNullOrEmpty(result.ExpectedHash) && result.ExpectedHash != result.CurrentHash)
            {
                result.IsValid = false;
                
                // 変更内容の詳細を検出
                await DetectDataChanges(jobDate, dailyReportDatasetId, result);
            }
            
            // 4. データ件数の確認
            result.DataCounts["売上伝票"] = await _salesRepository.GetCountAsync(jobDate);
            result.DataCounts["仕入伝票"] = await _purchaseRepository.GetCountAsync(jobDate);
            result.DataCounts["在庫調整"] = await _adjustmentRepository.GetCountAsync(jobDate);
            
            // 5. 異常データのチェック
            if (result.DataCounts.Values.Any(count => count == 0))
            {
                result.Warnings.Add("データ件数が0の種別があります");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データ整合性検証エラー");
            result.IsValid = false;
            result.Changes.Add($"検証エラー: {ex.Message}");
        }
        
        return result;
    }
    
    /// <summary>
    /// 現在のデータハッシュを計算
    /// </summary>
    private async Task<string> CalculateCurrentDataHash(DateTime jobDate)
    {
        using var sha256 = SHA256.Create();
        var dataBuilder = new StringBuilder();
        
        // 売上伝票データ
        var salesVouchers = await _salesRepository.GetByJobDateAsync(jobDate);
        foreach (var voucher in salesVouchers.OrderBy(v => v.Id))
        {
            dataBuilder.AppendLine($"SALES:{voucher.Id},{voucher.ProductCode},{voucher.Quantity},{voucher.Amount}");
        }
        
        // 仕入伝票データ
        var purchaseVouchers = await _purchaseRepository.GetByJobDateAsync(jobDate);
        foreach (var voucher in purchaseVouchers.OrderBy(v => v.Id))
        {
            dataBuilder.AppendLine($"PURCHASE:{voucher.Id},{voucher.ProductCode},{voucher.Quantity},{voucher.Amount}");
        }
        
        // 在庫調整データ
        var adjustments = await _adjustmentRepository.GetByJobDateAsync(jobDate);
        foreach (var adjustment in adjustments.OrderBy(a => a.VoucherId).ThenBy(a => a.LineNumber))
        {
            dataBuilder.AppendLine($"ADJUST:{adjustment.VoucherId}-{adjustment.LineNumber},{adjustment.ProductCode},{adjustment.Quantity},{adjustment.Amount}");
        }
        
        var dataBytes = Encoding.UTF8.GetBytes(dataBuilder.ToString());
        var hashBytes = sha256.ComputeHash(dataBytes);
        return Convert.ToBase64String(hashBytes);
    }
    
    /// <summary>
    /// データ変更内容を検出
    /// </summary>
    private async Task DetectDataChanges(DateTime jobDate, string dailyReportDatasetId, DataValidationResult result)
    {
        // 商品日報作成時刻を取得
        var dailyReportHistory = await _historyService.GetProcessHistory(jobDate, "DAILY_REPORT");
        var dailyReportTime = dailyReportHistory
            .Where(h => h.DatasetId == dailyReportDatasetId && h.Status == ProcessStatus.Completed)
            .Select(h => h.EndTime)
            .FirstOrDefault();
            
        if (dailyReportTime == null) return;
        
        // 商品日報作成後に更新されたデータを検出
        var salesChanges = await _salesRepository.GetModifiedAfterAsync(jobDate, dailyReportTime.Value);
        if (salesChanges > 0)
        {
            result.Changes.Add($"売上伝票 {salesChanges}件");
        }
        
        var purchaseChanges = await _purchaseRepository.GetModifiedAfterAsync(jobDate, dailyReportTime.Value);
        if (purchaseChanges > 0)
        {
            result.Changes.Add($"仕入伝票 {purchaseChanges}件");
        }
        
        var adjustmentChanges = await _adjustmentRepository.GetModifiedAfterAsync(jobDate, dailyReportTime.Value);
        if (adjustmentChanges > 0)
        {
            result.Changes.Add($"在庫調整 {adjustmentChanges}件");
        }
    }
}