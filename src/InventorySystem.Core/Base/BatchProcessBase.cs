using Microsoft.Extensions.Logging;
using InventorySystem.Core.Models;
using InventorySystem.Core.Services.Validation;
using InventorySystem.Core.Services.DataSet;
using InventorySystem.Core.Services.History;

namespace InventorySystem.Core.Base;

/// <summary>
/// バッチ処理基底クラス
/// </summary>
public abstract class BatchProcessBase
{
    protected readonly IDateValidationService _dateValidator;
    protected readonly IDataSetManager _dataSetManager;
    protected readonly IProcessHistoryService _historyService;
    protected readonly ILogger _logger;
    
    protected BatchProcessBase(
        IDateValidationService dateValidator,
        IDataSetManager dataSetManager,
        IProcessHistoryService historyService,
        ILogger logger)
    {
        _dateValidator = dateValidator;
        _dataSetManager = dataSetManager;
        _historyService = historyService;
        _logger = logger;
    }
    
    /// <summary>
    /// 処理を初期化
    /// </summary>
    /// <param name="jobDate">ジョブ日付</param>
    /// <param name="processType">処理種別</param>
    /// <param name="importedFiles">インポートファイル一覧</param>
    /// <param name="executedBy">実行者</param>
    /// <returns>処理コンテキスト</returns>
    protected async Task<ProcessContext> InitializeProcess(
        DateTime jobDate,
        string processType,
        List<string>? importedFiles = null,
        string executedBy = "System")
    {
        _logger.LogInformation("処理初期化開始: ProcessType={ProcessType}, JobDate={JobDate}", 
            processType, jobDate);
        
        // 1. 日付検証
        var validation = await _dateValidator.ValidateJobDate(jobDate, processType);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Message);
        }
        
        // 2. オペレータに日付範囲を表示
        _logger.LogInformation("処理対象日付: {JobDate:yyyy/MM/dd}", jobDate);
        
        // 年末年始の場合は範囲表示
        if (_dateValidator.IsSpecialDateRange(jobDate))
        {
            var range = _dateValidator.GetSpecialDateRange(jobDate);
            _logger.LogInformation("処理日付範囲: {Start:yyyy/MM/dd} ～ {End:yyyy/MM/dd}", 
                range.Start, range.End);
        }
        
        // 3. データセットID生成と登録
        var datasetId = _dataSetManager.GenerateDataSetId(jobDate, processType);
        var dataset = DataSetManager.CreateDataSet(
            datasetId, 
            jobDate, 
            processType, 
            importedFiles, 
            executedBy);
        
        await _dataSetManager.RegisterDataSet(dataset);
        
        // 4. 処理履歴開始
        var history = await _historyService.StartProcess(datasetId, jobDate, processType, executedBy);
        
        return new ProcessContext
        {
            JobDate = jobDate,
            DataSetId = datasetId,
            ProcessType = processType,
            ProcessHistory = history,
            ImportedFiles = importedFiles ?? new List<string>(),
            ExecutedBy = executedBy
        };
    }
    
    /// <summary>
    /// 処理を終了
    /// </summary>
    /// <param name="context">処理コンテキスト</param>
    /// <param name="success">成功フラグ</param>
    /// <param name="message">メッセージ</param>
    protected async Task FinalizeProcess(ProcessContext context, bool success, string? message = null)
    {
        if (context.ProcessHistory == null)
        {
            _logger.LogWarning("処理履歴が存在しません");
            return;
        }
        
        _logger.LogInformation("処理終了: DataSetId={DataSetId}, Success={Success}, Message={Message}", 
            context.DataSetId, success, message);
        
        // 処理履歴を更新
        await _historyService.CompleteProcess(context.ProcessHistory.Id, success, message);
        
        // 日次終了処理の場合はメール送信
        if (context.ProcessType == "DAILY_CLOSE" && success)
        {
            await _historyService.SendCompletionEmail(context.ProcessHistory);
        }
    }
}