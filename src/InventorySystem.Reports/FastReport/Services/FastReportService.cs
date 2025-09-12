#if WINDOWS
using System;
using System.Data;
using System.Drawing;
using System.IO;
using FastReport;
using FastReport.Export.Pdf;
using Microsoft.Extensions.Logging;
#else
using System;
using System.Data;
using Microsoft.Extensions.Logging;
#endif

namespace InventorySystem.Reports.FastReport.Services
{
    public class FastReportService
    {
        private readonly ILogger<FastReportService> _logger;
        
        public FastReportService(ILogger<FastReportService> logger)
        {
            _logger = logger;
        }
        
        public byte[] GenerateReportFromTemplate(string templatePath, DataSet dataSet, Dictionary<string, object> parameters)
        {
#if WINDOWS
            try
            {
                using var report = new Report();
                
                // テンプレート読み込み
                if (File.Exists(templatePath))
                {
                    report.Load(templatePath);
                }
                else
                {
                    throw new FileNotFoundException($"レポートテンプレートが見つかりません: {templatePath}");
                }
                
                // データソース登録
                report.RegisterData(dataSet);
                
                // パラメータ設定
                foreach (var param in parameters)
                {
                    report.SetParameterValue(param.Key, param.Value);
                }
                
                // レポート生成
                report.Prepare();
                
                // PDF出力
                using var pdfExport = new PDFExport
                {
                    EmbeddingFonts = true,
                    PrintOptimized = true
                };
                
                using var stream = new MemoryStream();
                report.Export(pdfExport, stream);
                
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "レポート生成エラー");
                throw;
            }
#else
            throw new PlatformNotSupportedException("FastReport機能は Windows でのみ利用可能です");
#endif
        }
    }
}