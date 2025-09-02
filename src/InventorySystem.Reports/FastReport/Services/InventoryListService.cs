using System.Data;
using System.Reflection;
using FR = FastReport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace InventorySystem.Reports.FastReport.Services;

public class InventoryListService
{
    private readonly ILogger<InventoryListService> _logger;
    private readonly string _templatePath;
    private readonly IConfiguration _configuration;
    private readonly string _outputBasePath;

    public InventoryListService(ILogger<InventoryListService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // テンプレートパス
        _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "FastReport", "Templates", "InventoryList.frx");
        
        // 出力先パスをappsettings.jsonから取得
        _outputBasePath = _configuration["FileStorage:ReportOutputPath"] 
            ?? _configuration["ReportSettings:OutputFolder"]
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "Reports");
            
        _logger.LogInformation("在庫表テンプレートパス: {TemplatePath}", _templatePath);
        _logger.LogInformation("在庫表出力先ベースパス: {OutputBasePath}", _outputBasePath);
    }

    public async Task<byte[]> GenerateInventoryListAsync(DateTime reportDate, string dataSetId)
    {
        var pdfBytes = await GenerateInventoryListPdfAsync(reportDate, dataSetId);
        var filePath = await SavePdfFileAsync(pdfBytes, reportDate);
        return pdfBytes;
    }

    private async Task<byte[]> GenerateInventoryListPdfAsync(DateTime reportDate, string dataSetId)
    {
        _logger.LogInformation("在庫表PDF生成開始: reportDate={ReportDate}, dataSetId={DataSetId}", reportDate, dataSetId);

        try
        {
            if (!File.Exists(_templatePath))
            {
                throw new FileNotFoundException($"FastReportテンプレートファイルが見つかりません: {_templatePath}");
            }

            using var report = new FR.Report();
            
            SetScriptLanguageToNone(report);
            
            report.Load(_templatePath);

            var dataTable = CreateInventoryDataTable();
            report.RegisterData(dataTable, "InventoryData");
            
            report.SetParameterValue("ReportDate", reportDate.ToString("yyyy年MM月dd日"));

            report.Prepare();

            using var pdfExport = new FR.Export.Pdf.PDFExport();
            using var stream = new MemoryStream();
            
            pdfExport.Export(report, stream);
            
            _logger.LogInformation("在庫表PDF生成完了: ファイルサイズ={FileSize}bytes", stream.Length);
            
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "在庫表PDF生成エラー");
            throw;
        }
    }

    private void SetScriptLanguageToNone(FR.Report report)
    {
        try
        {
            var scriptLanguageProperty = report.GetType().GetProperty("ScriptLanguage");
            if (scriptLanguageProperty != null)
            {
                var scriptLanguageType = scriptLanguageProperty.PropertyType;
                if (scriptLanguageType.IsEnum)
                {
                    var noneValue = Enum.GetValues(scriptLanguageType)
                        .Cast<object>()
                        .FirstOrDefault(v => v.ToString() == "None");
                    
                    if (noneValue != null)
                    {
                        scriptLanguageProperty.SetValue(report, noneValue);
                        _logger.LogInformation("ScriptLanguageをNoneに設定しました");
                    }
                }
            }
            
            var scriptProperty = report.GetType().GetProperty("Script", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (scriptProperty != null)
            {
                scriptProperty.SetValue(report, null);
                _logger.LogInformation("Scriptプロパティをnullに設定しました");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"ScriptLanguage設定時の警告: {ex.Message}");
        }
    }

    private DataTable CreateInventoryDataTable()
    {
        var dataTable = new DataTable("InventoryData");
        
        dataTable.Columns.Add("ProductCode", typeof(string));
        dataTable.Columns.Add("ProductName", typeof(string));
        dataTable.Columns.Add("Quantity", typeof(decimal));

        dataTable.Rows.Add("12345", "テスト商品A", 100.50m);
        dataTable.Rows.Add("67890", "テスト商品B", 200.75m);
        dataTable.Rows.Add("11111", "テスト商品C", 50.25m);

        return dataTable;
    }

    private async Task<string> SavePdfFileAsync(byte[] pdfBytes, DateTime reportDate)
    {
        // appsettings.jsonの設定を使用して年月フォルダ構造で保存
        var outputDir = Path.Combine(_outputBasePath, reportDate.Year.ToString(), 
            reportDate.Month.ToString("00"));
        
        // ディレクトリ作成
        Directory.CreateDirectory(outputDir);
        
        // ファイル名生成
        var fileName = $"InventoryList_{reportDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf";
        var filePath = Path.Combine(outputDir, fileName);
        
        // ファイル保存
        await File.WriteAllBytesAsync(filePath, pdfBytes);
        
        _logger.LogInformation("在庫表PDFを保存しました: {FilePath}", filePath);
        return filePath;
    }
}