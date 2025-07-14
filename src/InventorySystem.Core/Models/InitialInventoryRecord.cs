using CsvHelper.Configuration;
using System.Globalization;

namespace InventorySystem.Core.Models;

/// <summary>
/// 初期在庫データ（ZAIK*.csv）のレコードモデル
/// 注意：属性ベースマッピングを削除し、ClassMapのみを使用（トリミング問題対策）
/// </summary>
public class InitialInventoryRecord
{
    public string ProductCode { get; set; } = string.Empty;
    public string GradeCode { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string ShippingMarkCode { get; set; } = string.Empty;
    public string ShippingMarkName { get; set; } = string.Empty;
    public int PersonInChargeCode { get; set; }
    public decimal PreviousStockQuantity { get; set; }
    public decimal PreviousStockAmount { get; set; }
    public decimal CurrentStockQuantity { get; set; }
    public decimal StandardPrice { get; set; }
    public decimal CurrentStockAmount { get; set; }
    public decimal AveragePrice { get; set; }
}

/// <summary>
/// 初期在庫データのCSVマッピング設定（トリミング耐性のClassMapのみ使用）
/// </summary>
public sealed class InitialInventoryRecordMap : ClassMap<InitialInventoryRecord>
{
    public InitialInventoryRecordMap()
    {
        // 属性を使わず、ClassMapのみでマッピングを定義（トリミング問題回避）
        Map(m => m.ProductCode).Index(0).Name("商品ＣＤ");
        Map(m => m.GradeCode).Index(1).Name("等級ＣＤ");
        Map(m => m.ClassCode).Index(2).Name("階級ＣＤ");
        Map(m => m.ShippingMarkCode).Index(3).Name("荷印ＣＤ");
        Map(m => m.ShippingMarkName).Index(4).Name("荷印名");
        Map(m => m.PersonInChargeCode).Index(5).Name("商品分類１担当者ＣＤ");
        Map(m => m.PreviousStockQuantity).Index(9).Name("前日在庫数量");
        Map(m => m.PreviousStockAmount).Index(11).Name("前日在庫金額");
        Map(m => m.CurrentStockQuantity).Index(14).Name("当日在庫数量");
        Map(m => m.StandardPrice).Index(15).Name("当日在庫単価");
        Map(m => m.CurrentStockAmount).Index(16).Name("当日在庫金額");
        Map(m => m.AveragePrice).Index(17).Name("粗利計算用平均単価");
    }
}