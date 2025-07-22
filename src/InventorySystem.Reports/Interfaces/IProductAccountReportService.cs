namespace InventorySystem.Reports.Interfaces;

/// <summary>
/// 商品勘定帳票サービスのインターフェース
/// </summary>
public interface IProductAccountReportService
{
    /// <summary>
    /// 商品勘定帳票を生成
    /// </summary>
    /// <param name="jobDate">対象日</param>
    /// <param name="departmentCode">部門コード（省略時は全部門）</param>
    /// <returns>PDF帳票のバイト配列</returns>
    byte[] GenerateProductAccountReport(DateTime jobDate, string? departmentCode = null);
}