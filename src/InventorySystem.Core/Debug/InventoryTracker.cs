using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Core.Debug
{
    /// <summary>
    /// 在庫データの追跡情報
    /// </summary>
    public class InventoryTrackingData
    {
        public string ProcessName { get; set; } = string.Empty;           // 処理名
        public DateTime Timestamp { get; set; }           // 記録時刻
        public string ProductCode { get; set; } = string.Empty;           // 商品コード
        public string GradeCode { get; set; } = string.Empty;            // 等級コード
        public string ClassCode { get; set; } = string.Empty;            // 階級コード
        public string ShippingMarkCode { get; set; } = string.Empty;     // 荷印コード
        public string ManualShippingMark { get; set; } = string.Empty;   // 手入力荷印（記録のみ）
        public decimal PreviousDayUnitPrice { get; set; } // CP前日単価
        public decimal DailyUnitPrice { get; set; }      // CP当日単価
        public decimal StandardPrice { get; set; }        // CP標準単価
        public decimal AveragePrice { get; set; }         // CP平均単価
        public decimal SalesUnitPrice { get; set; }       // 売上単価
        public string VoucherNumber { get; set; } = string.Empty;         // 伝票番号
        public decimal PreviousDayStock { get; set; }     // 前日在庫数
        public decimal PreviousDayStockAmount { get; set; } // 前日在庫金額
        public decimal DailyPurchaseQuantity { get; set; } // 当日仕入数
        public decimal DailyPurchaseAmount { get; set; }   // 当日仕入金額
        public decimal DailySalesQuantity { get; set; }    // 当日売上数
        public string Diagnosis { get; set; } = string.Empty;              // 診断結果
    }

    /// <summary>
    /// 在庫データ追跡クラス
    /// </summary>
    public static class InventoryTracker
    {
        private static readonly List<InventoryTrackingData> _trackingData = new();
        private static bool _isEnabled = false;
        // 追跡対象キー（デフォルトは従来の 00104-025-028、荷印コードは未指定）
        private static string _trackProductCode = "00104";
        private static string _trackGradeCode = "025";
        private static string _trackClassCode = "028";
        private static string? _trackShippingMarkCode = null;

        /// <summary>
        /// デバッグモードの有効/無効
        /// </summary>
        public static bool IsEnabled 
        { 
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public static void SetTrackingKey(string productCode, string gradeCode, string classCode, string shippingMarkCode)
        {
            _trackProductCode = productCode ?? _trackProductCode;
            _trackGradeCode = gradeCode ?? _trackGradeCode;
            _trackClassCode = classCode ?? _trackClassCode;
            _trackShippingMarkCode = string.IsNullOrWhiteSpace(shippingMarkCode) ? null : shippingMarkCode;
        }

        public static (string ProductCode, string GradeCode, string ClassCode, string? ShippingMarkCode) GetTrackingKey()
            => (_trackProductCode, _trackGradeCode, _trackClassCode, _trackShippingMarkCode);

        /// <summary>
        /// 追跡データを記録
        /// </summary>
        public static void Track(string processName, InventoryTrackingData data)
        {
            if (!_isEnabled) return;

            data.ProcessName = processName;
            data.Timestamp = DateTime.Now;
            data.Diagnosis = GetDiagnosis(data);
            _trackingData.Add(data);
        }

        /// <summary>
        /// 診断結果を判定
        /// </summary>
        public static string GetDiagnosis(InventoryTrackingData data)
        {
            // 売上単価と当日単価が同じ場合は問題
            if (data.SalesUnitPrice > 0 && Math.Abs(data.DailyUnitPrice - data.SalesUnitPrice) < 0.01m)
                return "【問題】同じ値";
            
            // 標準単価が0の場合は注意
            if (data.StandardPrice == 0)
                return "【注意】標準単価0";
                
            return "正常";
        }

        /// <summary>
        /// 追跡データをJSONファイルに保存
        /// </summary>
        public static void SaveToJson(string filePath)
        {
            if (!_isEnabled || _trackingData.Count == 0) return;

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) // 日本語対応
            };
            
            var json = JsonSerializer.Serialize(_trackingData, options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 全追跡データを取得
        /// </summary>
        public static List<InventoryTrackingData> GetAll() => new(_trackingData);

        /// <summary>
        /// 追跡データをクリア
        /// </summary>
        public static void Clear() => _trackingData.Clear();

        /// <summary>
        /// コンソールにサマリーを出力
        /// </summary>
        public static void LogSummary(ILogger logger)
        {
            if (!_isEnabled) return;

            var key = GetTrackingKey();
            var targetData = _trackingData.Where(d =>
                d.ProductCode == key.ProductCode &&
                d.GradeCode == key.GradeCode &&
                d.ClassCode == key.ClassCode &&
                (string.IsNullOrEmpty(key.ShippingMarkCode) || d.ShippingMarkCode == key.ShippingMarkCode)
            ).ToList();

            var title = string.IsNullOrEmpty(key.ShippingMarkCode)
                ? $"=== {key.ProductCode}-{key.GradeCode}-{key.ClassCode} 追跡サマリー ==="
                : $"=== {key.ProductCode}-{key.GradeCode}-{key.ClassCode}-{key.ShippingMarkCode} 追跡サマリー ===";
            logger.LogInformation(title);
            foreach (var data in targetData)
            {
                logger.LogInformation(
                    $"{data.ProcessName}: " +
                    $"当日単価={data.DailyUnitPrice:N2}, " +
                    $"売上単価={data.SalesUnitPrice:N2}, " +
                    $"前日在庫={data.PreviousDayStock:N2}, " +
                    $"前日金額={data.PreviousDayStockAmount:N2}, " +
                    $"診断={data.Diagnosis}");
            }
        }
    }
}
