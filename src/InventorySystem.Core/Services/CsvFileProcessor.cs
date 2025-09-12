using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InventorySystem.Core.Configuration;

namespace InventorySystem.Core.Services
{
    public interface ICsvFileProcessor
    {
        Task ProcessFileAsync(string filePath, string departmentCode);
        Task MoveToProcessedAsync(string sourceFile, string departmentCode);
        Task MoveToErrorAsync(string sourceFile, string departmentCode, Exception error);
    }

    public class CsvFileProcessor : ICsvFileProcessor
    {
        private readonly DepartmentSettings _settings;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<CsvFileProcessor> _logger;

        public CsvFileProcessor(
            IOptions<DepartmentSettings> options,
            IHostEnvironment environment,
            ILogger<CsvFileProcessor> logger)
        {
            _settings = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task ProcessFileAsync(string filePath, string departmentCode)
        {
            var fileName = Path.GetFileName(filePath);
            var department = _settings.GetDepartment(departmentCode);
            
            try
            {
                // ここで実際のCSV処理を行う（既存の実装を呼び出す）
                _logger.LogInformation("ファイル {FileName} の処理を開始します", fileName);
                
                // 処理成功時：Processedフォルダへ移動
                await MoveToProcessedAsync(filePath, departmentCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル {FileName} の処理中にエラーが発生しました", fileName);
                
                // エラー時：Errorフォルダへ移動
                await MoveToErrorAsync(filePath, departmentCode, ex);
                throw;
            }
        }

        public async Task MoveToProcessedAsync(string sourceFile, string departmentCode)
        {
            var department = _settings.GetDepartment(departmentCode);
            var processedPath = department.GetProcessedPath(_settings.BasePath, _environment);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = Path.GetFileNameWithoutExtension(sourceFile);
            var extension = Path.GetExtension(sourceFile);
            var destFile = Path.Combine(processedPath, $"{fileName}_{timestamp}{extension}");
            
            // ディレクトリが存在しない場合は作成
            Directory.CreateDirectory(processedPath);
            
            File.Move(sourceFile, destFile);
            _logger.LogInformation("ファイルを処理済みフォルダへ移動しました: {DestFile}", destFile);
            
            await Task.CompletedTask;
        }

        public async Task MoveToErrorAsync(string sourceFile, string departmentCode, Exception error)
        {
            var department = _settings.GetDepartment(departmentCode);
            var errorPath = department.GetErrorPath(_settings.BasePath, _environment);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = Path.GetFileNameWithoutExtension(sourceFile);
            var extension = Path.GetExtension(sourceFile);
            var destFile = Path.Combine(errorPath, $"{fileName}_{timestamp}{extension}");
            var errorLogFile = Path.Combine(errorPath, $"{fileName}_{timestamp}_error.log");
            
            // ディレクトリが存在しない場合は作成
            Directory.CreateDirectory(errorPath);
            
            File.Move(sourceFile, destFile);
            
            // エラーログの作成
            var errorContent = $@"エラー発生日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
ファイル名: {Path.GetFileName(sourceFile)}
部門コード: {departmentCode}
エラー種別: {error.GetType().Name}
エラーメッセージ: {error.Message}

スタックトレース:
{error.StackTrace}

内部例外:
{error.InnerException?.ToString() ?? "なし"}
";
            
            await File.WriteAllTextAsync(errorLogFile, errorContent);
            
            _logger.LogInformation("ファイルをエラーフォルダへ移動しました: {DestFile}", destFile);
        }
    }
}