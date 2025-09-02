using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InventorySystem.Reports.FastReport.Services;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<InventoryListService>();
var service = new InventoryListService(logger);

try
{
    Console.WriteLine("在庫表PDF生成テスト開始...");
    
    var reportDate = new DateTime(2025, 6, 1);
    var dataSetId = "TEST_001";
    
    var pdfBytes = await service.GenerateInventoryListAsync(reportDate, dataSetId);
    
    var outputPath = Path.Combine(Directory.GetCurrentDirectory(), $"inventory_list_{reportDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
    await File.WriteAllBytesAsync(outputPath, pdfBytes);
    
    Console.WriteLine($"PDF生成成功: {outputPath}");
    Console.WriteLine($"ファイルサイズ: {pdfBytes.Length} bytes");
}
catch (Exception ex)
{
    Console.WriteLine($"エラー: {ex.Message}");
    Console.WriteLine($"スタックトレース: {ex.StackTrace}");
}