using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Constants;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;
using InventorySystem.Core.Entities;

namespace InventorySystem.Core.Services.Validation;

/// <summary>
/// 日付検証サービス実装
/// </summary>
public class DateValidationService : IDateValidationService
{
    private readonly IProcessHistoryRepository _historyRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DateValidationService> _logger;
    
    public DateValidationService(
        IProcessHistoryRepository historyRepository,
        IConfiguration configuration,
        ILogger<DateValidationService> logger)
    {
        _historyRepository = historyRepository;
        _configuration = configuration;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public async Task<Models.ValidationResult> ValidateJobDate(DateTime jobDate, string processType, bool allowDuplicateProcessing = false)
    {
        _logger.LogInformation("日付検証開始: JobDate={JobDate}, ProcessType={ProcessType}", 
            jobDate, processType);
        
        // 1. 未来日チェック
        if (jobDate.Date > DateTime.Today)
        {
            _logger.LogError("未来日エラー: {JobDate}", jobDate);
            return Models.ValidationResult.Failure(ErrorMessages.FutureDateError);
        }
        
        // 2. 過去日付範囲チェック（開発環境では無視）
        var isDevelopment = _configuration.GetValue<string>("Environment") == "Development" ||
                           Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ||
                           Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
        
        if (!isDevelopment)
        {
            var maxDaysInPast = _configuration.GetValue<int>("InventorySystem:Validation:MaxDaysInPast", 7);
            if (jobDate.Date < DateTime.Today.AddDays(-maxDaysInPast))
            {
                _logger.LogError("過去日付範囲超過: {JobDate}", jobDate);
                return Models.ValidationResult.Failure(
                    string.Format(ErrorMessages.PastDateRangeError, maxDaysInPast));
            }
        }
        else
        {
            _logger.LogWarning("開発環境のため過去日付範囲チェックをスキップしました");
        }
        
        // 3. 重複処理チェック（日次終了処理以外）
        if (processType != "DAILY_CLOSE" && !allowDuplicateProcessing)
        {
            var isProcessed = await IsDateAlreadyProcessed(jobDate, processType);
            if (isProcessed)
            {
                _logger.LogError("重複処理エラー: JobDate={JobDate}, ProcessType={ProcessType}", 
                    jobDate, processType);
                return Models.ValidationResult.Failure(ErrorMessages.AlreadyProcessedError);
            }
        }
        else if (allowDuplicateProcessing)
        {
            _logger.LogWarning("重複処理チェックをスキップしました（開発用）: JobDate={JobDate}, ProcessType={ProcessType}", 
                jobDate, processType);
        }
        
        // 4. 特殊日付範囲の警告ログ
        if (IsSpecialDateRange(jobDate))
        {
            var range = GetSpecialDateRange(jobDate);
            _logger.LogWarning("特殊日付範囲での処理: {Start:yyyy/MM/dd} ～ {End:yyyy/MM/dd}", 
                range.Start, range.End);
        }
        
        _logger.LogInformation("日付検証成功: JobDate={JobDate}", jobDate);
        return Models.ValidationResult.Success();
    }
    
    /// <inheritdoc/>
    public async Task<DateTime?> GetLastProcessedDate(string processType)
    {
        var lastHistory = await _historyRepository.GetLastSuccessfulAsync(processType);
        return lastHistory?.JobDate;
    }
    
    /// <inheritdoc/>
    public async Task<bool> IsDateAlreadyProcessed(DateTime jobDate, string processType)
    {
        var histories = await _historyRepository.GetByJobDateAndTypeAsync(jobDate, processType);
        return histories.Any(h => h.Status == ProcessStatus.Completed);
    }
    
    /// <inheritdoc/>
    public bool IsSpecialDateRange(DateTime jobDate)
    {
        // 年末年始チェック（12/29 - 1/5）
        var month = jobDate.Month;
        var day = jobDate.Day;
        
        if ((month == 12 && day >= 29) || (month == 1 && day <= 5))
        {
            return true;
        }
        
        // 設定ファイルから特殊日付範囲を読み込む
        var specialRanges = _configuration.GetSection("InventorySystem:Validation:SpecialDateRanges")
            .GetChildren();
        
        foreach (var range in specialRanges)
        {
            var fromStr = range["From"];
            var toStr = range["To"];
            
            if (IsInRange(jobDate, fromStr, toStr))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <inheritdoc/>
    public (DateTime Start, DateTime End) GetSpecialDateRange(DateTime jobDate)
    {
        // 年末年始の場合
        if ((jobDate.Month == 12 && jobDate.Day >= 29) || (jobDate.Month == 1 && jobDate.Day <= 5))
        {
            var year = jobDate.Month == 12 ? jobDate.Year : jobDate.Year - 1;
            return (new DateTime(year, 12, 29), new DateTime(year + 1, 1, 5));
        }
        
        // その他の特殊日付範囲
        return (jobDate, jobDate);
    }
    
    private bool IsInRange(DateTime date, string? fromStr, string? toStr)
    {
        if (string.IsNullOrEmpty(fromStr) || string.IsNullOrEmpty(toStr))
            return false;
        
        // MM-dd形式をパース
        var parts = fromStr.Split('-');
        if (parts.Length != 2) return false;
        
        if (int.TryParse(parts[0], out var fromMonth) && int.TryParse(parts[1], out var fromDay))
        {
            parts = toStr.Split('-');
            if (parts.Length != 2) return false;
            
            if (int.TryParse(parts[0], out var toMonth) && int.TryParse(parts[1], out var toDay))
            {
                var fromDate = new DateTime(date.Year, fromMonth, fromDay);
                var toDate = new DateTime(date.Year, toMonth, toDay);
                
                // 年をまたぐ場合の処理
                if (toDate < fromDate)
                {
                    toDate = toDate.AddYears(1);
                }
                
                return date >= fromDate && date <= toDate;
            }
        }
        
        return false;
    }
}