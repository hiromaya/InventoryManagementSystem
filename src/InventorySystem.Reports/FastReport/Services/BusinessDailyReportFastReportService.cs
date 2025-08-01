#pragma warning disable CA1416
#if WINDOWS

using FastReport;
using FastReport.Export.PdfSimple;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Reports.Interfaces;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;

namespace InventorySystem.Reports.FastReport.Services
{
    public class BusinessDailyReportFastReportService : InventorySystem.Reports.Interfaces.IBusinessDailyReportService, InventorySystem.Core.Interfaces.IBusinessDailyReportReportService
    {
        private readonly ILogger<BusinessDailyReportFastReportService> _logger;

        public BusinessDailyReportFastReportService(ILogger<BusinessDailyReportFastReportService> logger)
        {
            _logger = logger;
        }

        public byte[] GenerateBusinessDailyReport(IEnumerable<BusinessDailyReportItem> items, DateTime jobDate)
        {
            try
            {
                _logger.LogInformation("営業日報PDF生成を開始します: JobDate={JobDate}", jobDate);

                using var report = new Report();
                
                // テンプレートファイルのパス
                var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    "FastReport", "Templates", "BusinessDailyReport.frx");

                if (!File.Exists(templatePath))
                {
                    throw new FileNotFoundException($"営業日報テンプレートファイルが見つかりません: {templatePath}");
                }

                // テンプレート読み込み
                report.Load(templatePath);

                // データソース作成
                var dataTable = CreateDataTable(items);
                report.RegisterData(dataTable, "BusinessDailyReportItems");

                // パラメータ設定
                report.SetParameterValue("JobDate", jobDate.ToString("yyyy年MM月dd日"));
                report.SetParameterValue("CreateDate", DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分"));

                // レポート準備
                report.Prepare();

                // PDF出力
                using var pdfExport = new PDFSimpleExport();
                using var stream = new MemoryStream();
                
                // PDF設定
                pdfExport.ShowProgress = false;
                pdfExport.Subject = $"営業日報 {jobDate:yyyy年MM月dd日}";
                pdfExport.Title = "営業日報";
                pdfExport.Author = "在庫管理システム";
                pdfExport.Creator = "FastReport.NET";

                report.Export(pdfExport, stream);
                
                var result = stream.ToArray();
                
                _logger.LogInformation("営業日報PDF生成が完了しました: サイズ={Size}bytes", result.Length);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "営業日報PDF生成中にエラーが発生しました: JobDate={JobDate}", jobDate);
                throw;
            }
        }

        private DataTable CreateDataTable(IEnumerable<BusinessDailyReportItem> items)
        {
            var dataTable = new DataTable("BusinessDailyReportItems");

            // カラム定義
            dataTable.Columns.Add("ClassificationCode", typeof(string));
            dataTable.Columns.Add("CustomerClassName", typeof(string));
            dataTable.Columns.Add("SupplierClassName", typeof(string));
            
            // 日計項目（16項目）
            dataTable.Columns.Add("DailyCashSales", typeof(decimal));
            dataTable.Columns.Add("DailyCashSalesTax", typeof(decimal));
            dataTable.Columns.Add("DailyCreditSales", typeof(decimal));
            dataTable.Columns.Add("DailySalesDiscount", typeof(decimal));
            dataTable.Columns.Add("DailyCreditSalesTax", typeof(decimal));
            dataTable.Columns.Add("DailyCashPurchase", typeof(decimal));
            dataTable.Columns.Add("DailyCashPurchaseTax", typeof(decimal));
            dataTable.Columns.Add("DailyCreditPurchase", typeof(decimal));
            dataTable.Columns.Add("DailyPurchaseDiscount", typeof(decimal));
            dataTable.Columns.Add("DailyCreditPurchaseTax", typeof(decimal));
            dataTable.Columns.Add("DailyCashReceipt", typeof(decimal));
            dataTable.Columns.Add("DailyBankReceipt", typeof(decimal));
            dataTable.Columns.Add("DailyOtherReceipt", typeof(decimal));
            dataTable.Columns.Add("DailyCashPayment", typeof(decimal));
            dataTable.Columns.Add("DailyBankPayment", typeof(decimal));
            dataTable.Columns.Add("DailyOtherPayment", typeof(decimal));
            
            // 計算項目
            dataTable.Columns.Add("DailySalesTotal", typeof(decimal));
            dataTable.Columns.Add("DailyPurchaseTotal", typeof(decimal));

            // データ追加（分類000～035の36行）
            var itemList = items.ToList();
            
            // 36行分のデータを確保（不足分は空行で補完）
            for (int i = 0; i < 36; i++)
            {
                var classificationCode = i == 0 ? "000" : i.ToString("D3");
                var item = itemList.FirstOrDefault(x => x.ClassificationCode == classificationCode);

                if (item != null)
                {
                    dataTable.Rows.Add(
                        item.ClassificationCode,
                        item.CustomerClassName ?? "",
                        item.SupplierClassName ?? "",
                        item.DailyCashSales,
                        item.DailyCashSalesTax,
                        item.DailyCreditSales,
                        item.DailySalesDiscount,
                        item.DailyCreditSalesTax,
                        item.DailyCashPurchase,
                        item.DailyCashPurchaseTax,
                        item.DailyCreditPurchase,
                        item.DailyPurchaseDiscount,
                        item.DailyCreditPurchaseTax,
                        item.DailyCashReceipt,
                        item.DailyBankReceipt,
                        item.DailyOtherReceipt,
                        item.DailyCashPayment,
                        item.DailyBankPayment,
                        item.DailyOtherPayment,
                        item.DailySalesTotal,
                        item.DailyPurchaseTotal
                    );
                }
                else
                {
                    // 空行
                    dataTable.Rows.Add(
                        classificationCode,
                        "",
                        "",
                        0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m,
                        0m, 0m
                    );
                }
            }

            return dataTable;
        }
    }
}

#endif
#pragma warning restore CA1416