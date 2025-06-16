using CsvHelper.Configuration.Attributes;
using InventorySystem.Core.Entities;

namespace InventorySystem.Import.Models;

public class SalesVoucherCsv
{
    [Index(0)]
    public int VoucherId { get; set; }
    
    [Index(1)]
    public int LineNumber { get; set; }
    
    [Index(2)]
    [Format("yyyy/MM/dd")]
    public DateTime VoucherDate { get; set; }
    
    [Index(3)]
    [Format("yyyy/MM/dd")]
    public DateTime JobDate { get; set; }
    
    [Index(4)]
    public string ProductCode { get; set; } = string.Empty;
    
    [Index(5)]
    public string GradeCode { get; set; } = string.Empty;
    
    [Index(6)]
    public string ClassCode { get; set; } = string.Empty;
    
    [Index(7)]
    public string ShippingMarkCode { get; set; } = string.Empty;
    
    [Index(8)]
    public string ShippingMarkName { get; set; } = string.Empty;
    
    [Index(9)]
    public decimal Quantity { get; set; }
    
    [Index(10)]
    public decimal SalesUnitPrice { get; set; }
    
    [Index(11)]
    public decimal SalesAmount { get; set; }
    
    [Index(12)]
    public decimal InventoryUnitPrice { get; set; }
    
    public SalesVoucher ToEntity(string dataSetId)
    {
        return new SalesVoucher
        {
            VoucherId = VoucherId,
            LineNumber = LineNumber,
            VoucherDate = VoucherDate,
            JobDate = JobDate,
            InventoryKey = new InventoryKey
            {
                ProductCode = ProductCode.Trim(),
                GradeCode = GradeCode.Trim(),
                ClassCode = ClassCode.Trim(),
                ShippingMarkCode = ShippingMarkCode.Trim(),
                ShippingMarkName = ShippingMarkName.Trim()
            },
            Quantity = Quantity,
            UnitPrice = SalesUnitPrice,
            Amount = SalesAmount,
            InventoryUnitPrice = InventoryUnitPrice,
            DataSetId = dataSetId
        };
    }
}