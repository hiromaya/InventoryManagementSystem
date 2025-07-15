using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Core.Services.History;

/// <summary>
/// 処理履歴サービス実装
/// </summary>
public class ProcessHistoryService : IProcessHistoryService
{
    private readonly IProcessHistoryRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProcessHistoryService> _logger;
    
    public ProcessHistoryService(
        IProcessHistoryRepository repository,
        IConfiguration configuration,
        ILogger<ProcessHistoryService> logger)
    {
        _repository = repository;
        _configuration = configuration;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public async Task<ProcessHistory> StartProcess(string datasetId, DateTime jobDate, string processType, string executedBy = "System")
    {
        _logger.LogInformation("処理開始: DatasetId={DatasetId}, JobDate={JobDate}, ProcessType={ProcessType}, ExecutedBy={ExecutedBy}", 
            datasetId, jobDate, processType, executedBy);
        
        var history = new ProcessHistory
        {
            DataSetId = datasetId,
            JobDate = jobDate,
            ProcessType = processType,
            StartTime = DateTime.Now,
            Status = ProcessStatus.Running,
            ExecutedBy = executedBy
        };
        
        try
        {
            var created = await _repository.CreateAsync(history);
            _logger.LogInformation("処理履歴作成完了: Id={Id}", created.Id);
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理履歴作成エラー");
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task CompleteProcess(int historyId, bool success, string? message = null)
    {
        _logger.LogInformation("処理完了: HistoryId={HistoryId}, Success={Success}, Message={Message}", 
            historyId, success, message);
        
        var history = await _repository.GetByIdAsync(historyId);
        if (history == null)
        {
            throw new InvalidOperationException($"処理履歴が見つかりません: ID={historyId}");
        }
        
        history.EndTime = DateTime.Now;
        history.Status = success ? ProcessStatus.Completed : ProcessStatus.Failed;
        history.ErrorMessage = message;
        
        try
        {
            await _repository.UpdateAsync(history);
            _logger.LogInformation("処理履歴更新完了: Id={Id}, Status={Status}", historyId, history.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "処理履歴更新エラー: Id={Id}", historyId);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task<ProcessHistory?> GetLastSuccessfulProcess(string processType)
    {
        return await _repository.GetLastSuccessfulAsync(processType);
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<ProcessHistory>> GetProcessHistory(DateTime jobDate, string processType)
    {
        return await _repository.GetByJobDateAndTypeAsync(jobDate, processType);
    }
    
    /// <inheritdoc/>
    public async Task SendCompletionEmail(ProcessHistory history)
    {
        // 日次終了処理以外はメール送信しない
        if (history.ProcessType != "DAILY_CLOSE")
        {
            return;
        }
        
        var emailEnabled = _configuration.GetValue<bool>("InventorySystem:Email:Enabled", false);
        if (!emailEnabled)
        {
            _logger.LogInformation("メール送信は無効化されています");
            return;
        }
        
        try
        {
            var smtpServer = _configuration["InventorySystem:Email:SmtpServer"];
            var smtpPort = _configuration.GetValue<int>("InventorySystem:Email:SmtpPort", 587);
            var fromAddress = _configuration["InventorySystem:Email:FromAddress"];
            var toAddresses = _configuration.GetSection("InventorySystem:Email:ToAddresses").Get<string[]>() ?? Array.Empty<string>();
            var subject = _configuration["InventorySystem:Email:Subject"] ?? "在庫管理システム - 日次終了処理完了通知";
            
            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(fromAddress) || !toAddresses.Any())
            {
                _logger.LogWarning("メール設定が不完全です");
                return;
            }
            
            var body = CreateEmailBody(history);
            
            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                // 認証情報が必要な場合は設定から取得
                // Credentials = new NetworkCredential(username, password)
            };
            
            var message = new MailMessage
            {
                From = new MailAddress(fromAddress),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            
            foreach (var toAddress in toAddresses)
            {
                message.To.Add(toAddress);
            }
            
            await client.SendMailAsync(message);
            _logger.LogInformation("完了メール送信成功: {ToAddresses}", string.Join(", ", toAddresses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "メール送信エラー");
            // メール送信エラーは処理を中断しない
        }
    }
    
    private string CreateEmailBody(ProcessHistory history)
    {
        var duration = history.EndTime.HasValue 
            ? (history.EndTime.Value - history.StartTime).TotalMinutes 
            : 0;
        
        return $@"
在庫管理システム - 日次終了処理完了通知

処理日付: {history.JobDate:yyyy/MM/dd}
開始時刻: {history.StartTime:yyyy/MM/dd HH:mm:ss}
完了時刻: {history.EndTime:yyyy/MM/dd HH:mm:ss}
処理時間: {duration:F1} 分
処理結果: {(history.Status == ProcessStatus.Completed ? "正常終了" : "異常終了")}
実行者: {history.ExecutedBy}

{(string.IsNullOrEmpty(history.ErrorMessage) ? "" : $"メッセージ: {history.ErrorMessage}")}

このメールは自動送信されています。
";
    }
}