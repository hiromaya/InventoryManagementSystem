using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace InventorySystem.Core.Services
{
    public class BusinessDailyReportService : IBusinessDailyReportService
    {
        private readonly IBusinessDailyReportRepository _repository;
        private readonly IFileManagementService _fileManagementService;
        private readonly ILogger<BusinessDailyReportService> _logger;
        private readonly IBusinessDailyReportReportService _reportService;

        public BusinessDailyReportService(
            IBusinessDailyReportRepository repository,
            IFileManagementService fileManagementService,
            ILogger<BusinessDailyReportService> logger,
            IBusinessDailyReportReportService reportService)
        {
            _repository = repository;
            _fileManagementService = fileManagementService;
            _logger = logger;
            _reportService = reportService;
        }

        public async Task<BusinessDailyReportResult> ExecuteAsync(DateTime jobDate, string dataSetId)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("営業日報処理を開始します: JobDate={JobDate}, DataSetId={DataSetId}", jobDate, dataSetId);

                // 1. 当日エリアクリア（日計16項目を0リセット）
                _logger.LogInformation("日計エリアをクリアしています...");
                await _repository.ClearDailyAreaAsync();

                // 2. 36レコード初期化（000-035 + 999 = 37レコード固定保証）
                _logger.LogInformation("BusinessDailyReportテーブル36レコード初期化...");
                await _repository.InitializeAllClassificationsAsync();

                // 3. 売上伝票集計（伝票種×明細種の組み合わせで判定）
                _logger.LogInformation("売上伝票データを集計しています...");
                await _repository.AggregateSalesDataAsync(jobDate);

                // 4. 仕入伝票集計
                _logger.LogInformation("仕入伝票データを集計しています...");
                await _repository.AggregatePurchaseDataAsync(jobDate);

                // 5. 入金伝票集計
                _logger.LogInformation("入金伝票データを集計しています...");
                await _repository.AggregateReceiptDataAsync(jobDate);

                // 6. 支払伝票集計
                _logger.LogInformation("支払伝票データを集計しています...");
                await _repository.AggregatePaymentDataAsync(jobDate);

                // 7. 分類名をデータベースに更新（集計完了後に実行）
                _logger.LogInformation("分類名をデータベースに更新しています...");
                await _repository.UpdateClassificationNamesInDatabaseAsync();

                // 8. レポートデータ取得
                _logger.LogInformation("レポートデータを取得しています...");
                var reportData = await _repository.GetReportDataAsync();

                // 9. データ検証（36レコード固定保証）
                if (reportData.Count < 37) // 000-035 + 999 = 37件最小保証
                {
                    _logger.LogError("営業日報データが37件未満です: 期待37件以上、実際{ActualRecords}件", reportData.Count);
                    throw new InvalidOperationException($"営業日報データ不整合：期待37件以上、実際{reportData.Count}件");
                }
                _logger.LogInformation("営業日報データ検証完了: {RecordCount}件", reportData.Count);

                // 10. PDF生成
                _logger.LogInformation("PDFを生成しています...");
                var pdfBytes = _reportService.GenerateBusinessDailyReport(reportData, jobDate);

                // 11. ファイル保存（他の帳票と統一）
                var outputPath = await _fileManagementService.GetReportOutputPathAsync("BusinessDailyReport", jobDate, "pdf");
                await File.WriteAllBytesAsync(outputPath, pdfBytes);
                
                _logger.LogInformation("営業日報ファイルを保存しました: {FilePath}", outputPath);

                stopwatch.Stop();

                _logger.LogInformation("営業日報処理が完了しました: 処理時間={ProcessingTime}ms, 出力ファイル={OutputPath}", 
                    stopwatch.ElapsedMilliseconds, outputPath);

                return new BusinessDailyReportResult
                {
                    Success = true,
                    DataSetId = dataSetId,
                    ProcessedCount = reportData.Count,
                    OutputPath = outputPath,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "営業日報処理中にエラーが発生しました: JobDate={JobDate}, DataSetId={DataSetId}", jobDate, dataSetId);

                return new BusinessDailyReportResult
                {
                    Success = false,
                    DataSetId = dataSetId,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }
    }
}