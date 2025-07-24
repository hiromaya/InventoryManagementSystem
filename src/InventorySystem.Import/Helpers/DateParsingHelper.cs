using System;
using System.Globalization;

namespace InventorySystem.Import.Helpers
{
    /// <summary>
    /// CSV日付解析用ヘルパークラス
    /// 販売大臣AXのCSVで使用される様々な日付形式をサポート
    /// </summary>
    public static class DateParsingHelper
    {
        private static readonly string[] SupportedDateFormats = new[]
        {
            "yyyy/MM/dd",     // CSVで最も使用される形式（例：2025/06/30）
            "yyyy-MM-dd",     // ISO形式
            "yyyyMMdd",       // 8桁数値形式
            "yyyy/M/d",       // 月日が1桁の場合
            "yyyy-M-d",       // ISO形式で月日が1桁
            "dd/MM/yyyy",     // ヨーロッパ形式
            "dd.MM.yyyy"      // ドイツ語圏形式
        };

        /// <summary>
        /// CSV日付文字列を解析
        /// 複数の形式をサポートし、InvariantCultureで解析する
        /// </summary>
        /// <param name="dateStr">解析対象の日付文字列</param>
        /// <returns>解析された日付。失敗時はDateTime.MinValue</returns>
        public static DateTime ParseCsvDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
            {
                return DateTime.MinValue;
            }

            // 主要な形式で解析を試行
            if (DateTime.TryParseExact(dateStr.Trim(), SupportedDateFormats, 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date.Date; // 時刻部分を除去
            }

            // 最終手段：標準解析を試行
            if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture, 
                DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate.Date;
            }

            // 解析失敗
            return DateTime.MinValue;
        }

        /// <summary>
        /// 日付の妥当性を検証
        /// </summary>
        /// <param name="dateStr">検証対象の日付文字列</param>
        /// <returns>有効な日付の場合true</returns>
        public static bool IsValidCsvDate(string dateStr)
        {
            return ParseCsvDate(dateStr) != DateTime.MinValue;
        }

        /// <summary>
        /// サポートされている日付形式の文字列を取得
        /// エラーメッセージで使用
        /// </summary>
        /// <returns>サポート形式のカンマ区切り文字列</returns>
        public static string GetSupportedFormatsString()
        {
            return string.Join(", ", SupportedDateFormats);
        }

        /// <summary>
        /// JobDate解析（SalesVoucherImportService互換）
        /// エラー時は例外をスローする
        /// </summary>
        /// <param name="jobDateStr">JobDate文字列</param>
        /// <returns>解析されたJobDate</returns>
        /// <exception cref="InvalidOperationException">解析失敗時</exception>
        public static DateTime ParseJobDate(string jobDateStr)
        {
            var result = ParseCsvDate(jobDateStr);
            
            if (result == DateTime.MinValue)
            {
                throw new InvalidOperationException(
                    $"JobDateの解析に失敗しました: {jobDateStr} " +
                    $"(サポート形式: {GetSupportedFormatsString()})");
            }

            return result;
        }

        /// <summary>
        /// 配列形式での日付解析（既存コードとの互換性）
        /// </summary>
        /// <param name="dateStr">日付文字列</param>
        /// <param name="formats">試行する形式の配列</param>
        /// <param name="result">解析結果</param>
        /// <returns>解析成功時true</returns>
        public static bool TryParseExactMultipleFormats(string dateStr, string[] formats, out DateTime result)
        {
            result = DateTime.MinValue;
            
            if (string.IsNullOrEmpty(dateStr))
            {
                return false;
            }

            return DateTime.TryParseExact(dateStr.Trim(), formats, 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }
    }
}