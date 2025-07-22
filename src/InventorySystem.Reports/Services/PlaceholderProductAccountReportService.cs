using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Reports.Services;

/// <summary>
/// 商品勘定帳票サービスのプレースホルダー実装（Linux環境用）
/// </summary>
public class PlaceholderProductAccountReportService : IProductAccountReportService
{
    private readonly ILogger<PlaceholderProductAccountReportService> _logger;

    public PlaceholderProductAccountReportService(ILogger<PlaceholderProductAccountReportService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 商品勘定帳票を生成（プレースホルダー実装）
    /// </summary>
    public byte[] GenerateProductAccountReport(DateTime jobDate, string? departmentCode = null)
    {
        _logger.LogWarning("Placeholder実装: 商品勘定帳票の生成はWindows環境でのみ利用可能です");
        _logger.LogInformation($"要求されたパラメータ: JobDate={jobDate:yyyy-MM-dd}, DepartmentCode={departmentCode ?? "全部門"}");
        
        // プレースホルダーとして空のPDFを返す代わりに、エラーメッセージを含むテキストファイルを返す
        var message = $"商品勘定帳票生成機能はWindows環境でのみ利用可能です。\n" +
                     $"対象日: {jobDate:yyyy-MM-dd}\n" +
                     $"部門: {departmentCode ?? "全部門"}\n" +
                     $"実行時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                     $"この機能を使用するには、Windows環境でアプリケーションを実行してください。";
        
        return System.Text.Encoding.UTF8.GetBytes(message);
    }
}