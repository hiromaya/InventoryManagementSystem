using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Services;

/// <summary>
/// アンマッチチェック検証サービス実装
/// 帳票実行前のアンマッチ0件必須チェック機能
/// </summary>
public class UnmatchCheckValidationService : IUnmatchCheckValidationService
{
    private readonly IUnmatchCheckRepository _unmatchCheckRepository;
    private readonly ILogger<UnmatchCheckValidationService> _logger;

    public UnmatchCheckValidationService(
        IUnmatchCheckRepository unmatchCheckRepository,
        ILogger<UnmatchCheckValidationService> logger)
    {
        _unmatchCheckRepository = unmatchCheckRepository;
        _logger = logger;
    }

    /// <summary>
    /// 帳票実行前の検証を実行
    /// </summary>
    public async Task<Interfaces.ValidationResult> ValidateForReportExecutionAsync(string dataSetId, ReportType reportType)
    {
        try
        {
            _logger.LogInformation("帳票実行前検証開始 - DataSetId: {DataSetId}, ReportType: {ReportType}",
                dataSetId, reportType);

            if (string.IsNullOrEmpty(dataSetId))
            {
                var errorMessage = "DataSetIdが指定されていません";
                _logger.LogError(errorMessage);
                return Interfaces.ValidationResult.Failure(errorMessage);
            }

            // アンマッチチェック結果を取得
            var checkResult = await _unmatchCheckRepository.GetByDataSetIdAsync(dataSetId);

            if (checkResult == null)
            {
                var errorMessage = "アンマッチチェックが実行されていません。先にアンマッチチェックを実行してください。";
                _logger.LogWarning("アンマッチチェック未実行 - DataSetId: {DataSetId}", dataSetId);
                return Interfaces.ValidationResult.Failure(errorMessage);
            }

            // チェック結果の検証
            if (!checkResult.CanExecuteReport())
            {
                var errorMessage = checkResult.GetErrorMessage();
                _logger.LogWarning("帳票実行不可 - DataSetId: {DataSetId}, Reason: {Reason}",
                    dataSetId, errorMessage);

                return Interfaces.ValidationResult.Failure(
                    errorMessage,
                    checkResult.UnmatchCount,
                    checkResult.CheckDateTime,
                    checkResult.HasFullWidthError);
            }

            // 検証成功
            _logger.LogInformation("✅ 帳票実行前検証成功 - DataSetId: {DataSetId}, ReportType: {ReportType}",
                dataSetId, reportType);

            return Interfaces.ValidationResult.Success(checkResult.CheckDateTime);
        }
        catch (Exception ex)
        {
            var errorMessage = $"帳票実行前検証でエラーが発生しました: {ex.Message}";
            _logger.LogError(ex, "帳票実行前検証エラー - DataSetId: {DataSetId}, ReportType: {ReportType}",
                dataSetId, reportType);
            return Interfaces.ValidationResult.Failure(errorMessage);
        }
    }

    /// <summary>
    /// 最新のアンマッチチェック結果を取得
    /// </summary>
    public async Task<Interfaces.ValidationResult> GetLatestCheckResultAsync(string dataSetId)
    {
        try
        {
            _logger.LogDebug("最新アンマッチチェック結果取得 - DataSetId: {DataSetId}", dataSetId);

            if (string.IsNullOrEmpty(dataSetId))
            {
                return Interfaces.ValidationResult.Failure("DataSetIdが指定されていません");
            }

            var checkResult = await _unmatchCheckRepository.GetByDataSetIdAsync(dataSetId);

            if (checkResult == null)
            {
                return Interfaces.ValidationResult.Failure("アンマッチチェック結果が見つかりません");
            }

            if (checkResult.CanExecuteReport())
            {
                return Interfaces.ValidationResult.Success(checkResult.CheckDateTime);
            }
            else
            {
                return Interfaces.ValidationResult.Failure(
                    checkResult.GetErrorMessage(),
                    checkResult.UnmatchCount,
                    checkResult.CheckDateTime,
                    checkResult.HasFullWidthError);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"アンマッチチェック結果取得でエラーが発生しました: {ex.Message}";
            _logger.LogError(ex, "アンマッチチェック結果取得エラー - DataSetId: {DataSetId}", dataSetId);
            return Interfaces.ValidationResult.Failure(errorMessage);
        }
    }

    /// <summary>
    /// 指定されたDataSetIdのアンマッチチェック状況を取得
    /// </summary>
    public async Task<UnmatchCheckStatus> GetCheckStatusAsync(string dataSetId)
    {
        try
        {
            _logger.LogDebug("アンマッチチェック状況取得 - DataSetId: {DataSetId}", dataSetId);

            if (string.IsNullOrEmpty(dataSetId))
            {
                return new UnmatchCheckStatus
                {
                    DataSetId = dataSetId,
                    IsChecked = false,
                    CheckStatus = "NotChecked"
                };
            }

            var checkResult = await _unmatchCheckRepository.GetByDataSetIdAsync(dataSetId);

            if (checkResult == null)
            {
                return new UnmatchCheckStatus
                {
                    DataSetId = dataSetId,
                    IsChecked = false,
                    CheckStatus = "NotChecked"
                };
            }

            return new UnmatchCheckStatus
            {
                DataSetId = dataSetId,
                IsChecked = true,
                IsPassed = checkResult.IsPassed,
                UnmatchCount = checkResult.UnmatchCount,
                LastCheckDateTime = checkResult.CheckDateTime,
                CheckStatus = checkResult.CheckStatus,
                HasFullWidthError = checkResult.HasFullWidthError
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アンマッチチェック状況取得エラー - DataSetId: {DataSetId}", dataSetId);
            
            return new UnmatchCheckStatus
            {
                DataSetId = dataSetId,
                IsChecked = false,
                CheckStatus = "Error"
            };
        }
    }
}