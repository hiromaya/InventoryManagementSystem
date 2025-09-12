using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace InventorySystem.Reports.Services
{
    public class BusinessDailyReportPlaceholderService : InventorySystem.Reports.Interfaces.IBusinessDailyReportService, InventorySystem.Core.Interfaces.IBusinessDailyReportReportService
    {
        private readonly ILogger<BusinessDailyReportPlaceholderService> _logger;

        public BusinessDailyReportPlaceholderService(ILogger<BusinessDailyReportPlaceholderService> logger)
        {
            _logger = logger;
        }

        public byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            _logger.LogWarning("Linux環境のため、営業日報のプレースホルダーPDFを生成します: JobDate={JobDate}", jobDate);

            var content = new StringBuilder();
            content.AppendLine("営業日報 - プレースホルダー出力");
            content.AppendLine($"作成日: {DateTime.Now:yyyy年MM月dd日 HH時mm分}");
            content.AppendLine($"対象日: {jobDate:yyyy年MM月dd日}");
            content.AppendLine();
            content.AppendLine("※ Linux環境のため、実際のPDF出力は行われません。");
            content.AppendLine("※ Windows環境で実行してください。");
            content.AppendLine();

            var itemList = items.ToList();
            content.AppendLine($"データ件数: {itemList.Count}件");
            content.AppendLine();

            // 合計行の表示
            var totalItem = itemList.FirstOrDefault(x => x.ClassificationCode == "000");
            if (totalItem != null)
            {
                content.AppendLine("=== 合計 ===");
                content.AppendLine($"現金売上: {totalItem.DailyCashSales:N0}");
                content.AppendLine($"掛売上: {totalItem.DailyCreditSales:N0}");
                content.AppendLine($"現金仕入: {totalItem.DailyCashPurchase:N0}");
                content.AppendLine($"掛仕入: {totalItem.DailyCreditPurchase:N0}");
                content.AppendLine($"売上合計: {totalItem.DailySalesTotal:N0}");
                content.AppendLine($"仕入合計: {totalItem.DailyPurchaseTotal:N0}");
            }

            return Encoding.UTF8.GetBytes(content.ToString());
        }
    }
}