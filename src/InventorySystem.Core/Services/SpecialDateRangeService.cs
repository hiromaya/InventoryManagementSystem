using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces.Services;

namespace InventorySystem.Core.Services
{
    /// <summary>
    /// 年末年始などの特殊期間の日付範囲を管理するサービス
    /// </summary>
    public class SpecialDateRangeService : ISpecialDateRangeService
    {
        private readonly ILogger<SpecialDateRangeService> _logger;
        private readonly List<SpecialDateRange> _specialDateRanges;

        public SpecialDateRangeService(ILogger<SpecialDateRangeService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _specialDateRanges = new List<SpecialDateRange>();

            // 設定から特殊期間を読み込む
            var specialRanges = configuration.GetSection("InventorySystem:Validation:SpecialDateRanges").Get<List<SpecialDateRangeConfig>>();
            if (specialRanges != null)
            {
                foreach (var range in specialRanges)
                {
                    _specialDateRanges.Add(new SpecialDateRange
                    {
                        Name = range.Name,
                        FromMonthDay = ParseMonthDay(range.From),
                        ToMonthDay = ParseMonthDay(range.To)
                    });
                }
            }

            _logger.LogInformation("特殊日付範囲を{Count}件読み込みました", _specialDateRanges.Count);
        }

        /// <summary>
        /// 指定された日付が特殊期間内かどうかを判定
        /// </summary>
        public bool IsInSpecialDateRange(DateTime date)
        {
            foreach (var range in _specialDateRanges)
            {
                if (IsDateInRange(date, range))
                {
                    _logger.LogDebug("日付 {Date} は特殊期間 '{RangeName}' 内です", date, range.Name);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 指定された日付範囲が特殊期間と重複するかどうかを判定
        /// </summary>
        public bool HasOverlapWithSpecialRange(DateTime fromDate, DateTime toDate)
        {
            // fromDateからtoDateまでの各日付をチェック
            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
            {
                if (IsInSpecialDateRange(date))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 特殊期間を考慮して日付範囲を調整
        /// </summary>
        public (DateTime adjustedFrom, DateTime adjustedTo) AdjustDateRangeForSpecialPeriod(DateTime fromDate, DateTime toDate)
        {
            var adjustedFrom = fromDate;
            var adjustedTo = toDate;

            // 年末年始期間の場合、前年12月29日～当年1月5日を含むように拡張
            foreach (var range in _specialDateRanges)
            {
                if (range.Name == "年末年始")
                {
                    // fromDateが12月29日以降の場合
                    if (fromDate.Month == 12 && fromDate.Day >= 29)
                    {
                        // toDateを翌年1月5日まで拡張
                        var nextYear = fromDate.Year + 1;
                        adjustedTo = new DateTime(nextYear, 1, 5);
                        _logger.LogInformation("年末年始期間のため、終了日を {AdjustedTo} に拡張しました", adjustedTo);
                    }
                    // toDateが1月5日以前の場合
                    else if (toDate.Month == 1 && toDate.Day <= 5)
                    {
                        // fromDateを前年12月29日まで拡張
                        var prevYear = toDate.Year - 1;
                        adjustedFrom = new DateTime(prevYear, 12, 29);
                        _logger.LogInformation("年末年始期間のため、開始日を {AdjustedFrom} に拡張しました", adjustedFrom);
                    }
                }
            }

            return (adjustedFrom, adjustedTo);
        }

        /// <summary>
        /// 月日文字列（MM-dd）をパース
        /// </summary>
        private (int month, int day) ParseMonthDay(string monthDay)
        {
            var parts = monthDay.Split('-');
            if (parts.Length != 2)
            {
                throw new ArgumentException($"無効な月日形式: {monthDay}");
            }

            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        /// <summary>
        /// 日付が指定範囲内かどうかを判定（年またぎ対応）
        /// </summary>
        private bool IsDateInRange(DateTime date, SpecialDateRange range)
        {
            var dateMonthDay = (date.Month, date.Day);

            // 年またぎの場合（例：12-29 ～ 01-05）
            if (range.FromMonthDay.month > range.ToMonthDay.month)
            {
                // 12月側または1月側のいずれかに該当
                return (dateMonthDay.Month >= range.FromMonthDay.month && dateMonthDay.Day >= range.FromMonthDay.day) ||
                       (dateMonthDay.Month <= range.ToMonthDay.month && dateMonthDay.Day <= range.ToMonthDay.day);
            }
            // 同一年内の場合
            else
            {
                return dateMonthDay.Month >= range.FromMonthDay.month && dateMonthDay.Day >= range.FromMonthDay.day &&
                       dateMonthDay.Month <= range.ToMonthDay.month && dateMonthDay.Day <= range.ToMonthDay.day;
            }
        }

        /// <summary>
        /// 特殊日付範囲の定義
        /// </summary>
        private class SpecialDateRange
        {
            public string Name { get; set; } = string.Empty;
            public (int month, int day) FromMonthDay { get; set; }
            public (int month, int day) ToMonthDay { get; set; }
        }

        /// <summary>
        /// 設定用のクラス
        /// </summary>
        private class SpecialDateRangeConfig
        {
            public string Name { get; set; } = string.Empty;
            public string From { get; set; } = string.Empty;
            public string To { get; set; } = string.Empty;
        }
    }
}