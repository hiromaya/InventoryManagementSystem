using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace InventorySystem.Core.Models;

/// <summary>
/// 初期在庫データ（ZAIK*.csv）のレコードモデル
/// </summary>
public class InitialInventoryRecord
{
    [Index(0)]
    [Name("商品ＣＤ")]
    public string ProductCode { get; set; } = string.Empty;

    [Index(1)]
    [Name("等級ＣＤ")]
    public string GradeCode { get; set; } = string.Empty;

    [Index(2)]
    [Name("階級ＣＤ")]
    public string ClassCode { get; set; } = string.Empty;

    [Index(3)]
    [Name("荷印ＣＤ")]
    public string ShippingMarkCode { get; set; } = string.Empty;

    [Index(4)]
    [Name("荷印名")]
    public string ShippingMarkName { get; set; } = string.Empty;

    [Index(5)]
    [Name("商品分類１担当者ＣＤ")]
    public int PersonInChargeCode { get; set; }

    // 列6-8はスキップ

    [Index(9)]
    [Name("前日在庫数量")]
    public decimal PreviousStockQuantity { get; set; }

    // 列10はスキップ

    [Index(11)]
    [Name("前日在庫金額")]
    public decimal PreviousStockAmount { get; set; }

    // 列12-13はスキップ

    [Index(14)]
    [Name("当日在庫数量")]
    public decimal CurrentStockQuantity { get; set; }

    [Index(15)]
    [Name("当日在庫単価")]
    public decimal StandardPrice { get; set; }

    [Index(16)]
    [Name("当日在庫金額")]
    public decimal CurrentStockAmount { get; set; }

    [Index(17)]
    [Name("粗利計算用平均単価")]
    public decimal AveragePrice { get; set; }
}

/// <summary>
/// 初期在庫データのCSVマッピング設定
/// </summary>
public sealed class InitialInventoryRecordMap : ClassMap<InitialInventoryRecord>
{
    public InitialInventoryRecordMap()
    {
        // AutoMap削除 - 明示的マッピングのみ使用（AttributeとClassMapの競合を回避）
        
        // 明示的にマッピング
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