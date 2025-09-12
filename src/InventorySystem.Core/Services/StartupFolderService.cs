using System;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InventorySystem.Core.Configuration;

namespace InventorySystem.Core.Services
{
    public interface IStartupFolderService
    {
        void EnsureFoldersExist();
    }

    public class StartupFolderService : IStartupFolderService
    {
        private readonly DepartmentSettings _settings;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<StartupFolderService> _logger;

        public StartupFolderService(
            IOptions<DepartmentSettings> options,
            IHostEnvironment environment,
            ILogger<StartupFolderService> logger)
        {
            _settings = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public void EnsureFoldersExist()
        {
            _logger.LogInformation("部門フォルダの作成を開始します。環境: {Environment}", _environment.EnvironmentName);

            foreach (var dept in _settings.Departments)
            {
                try
                {
                    var importPath = dept.GetImportPath(_settings.BasePath, _environment);
                    var processedPath = dept.GetProcessedPath(_settings.BasePath, _environment);
                    var errorPath = dept.GetErrorPath(_settings.BasePath, _environment);

                    Directory.CreateDirectory(importPath);
                    Directory.CreateDirectory(processedPath);
                    Directory.CreateDirectory(errorPath);

                    _logger.LogInformation(
                        "部門 {DeptCode} のフォルダを作成しました: {Path}", 
                        dept.Code, 
                        dept.GetDepartmentPath(_settings.BasePath, _environment));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "部門 {DeptCode} のフォルダ作成中にエラーが発生しました", dept.Code);
                    throw;
                }
            }
        }
    }
}