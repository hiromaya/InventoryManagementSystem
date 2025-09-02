using System.Data;
using System.Reflection;
using FR = FastReport;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Reports.FastReport.Services;

public class InventoryListService
{
    private readonly ILogger<InventoryListService> _logger;

    public InventoryListService(ILogger<InventoryListService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GenerateInventoryListAsync(DateTime reportDate, string dataSetId)
    {
        _logger.LogInformation("在庫表PDF生成開始: reportDate={ReportDate}, dataSetId={DataSetId}", reportDate, dataSetId);

        try
        {
            var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FastReport", "Templates", "InventoryList.frx");
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"FastReportテンプレートファイルが見つかりません: {templatePath}");
            }

            using var report = new FR.Report();
            
            SetScriptLanguageToNone(report);
            
            report.Load(templatePath);

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
}