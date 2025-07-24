using Xunit;
using InventorySystem.Import.Helpers;
using System;

namespace InventorySystem.Import.Tests.Helpers
{
    /// <summary>
    /// DateParsingHelperのテストクラス
    /// 販売大臣AXのCSVで使用される様々な日付形式をテスト
    /// </summary>
    public class DateParsingHelperTests
    {
        [Theory]
        [InlineData("2025/06/02", 2025, 6, 2)]
        [InlineData("2025-06-02", 2025, 6, 2)]
        [InlineData("20250602", 2025, 6, 2)]
        [InlineData("2025/6/2", 2025, 6, 2)]
        [InlineData("2025-6-2", 2025, 6, 2)]
        [InlineData("02/06/2025", 2025, 6, 2)] // ヨーロッパ形式
        [InlineData("02.06.2025", 2025, 6, 2)] // ドイツ語圏形式
        public void ParseCsvDate_ValidFormats_ShouldReturnCorrectDate(
            string input, int expectedYear, int expectedMonth, int expectedDay)
        {
            // Act
            var result = DateParsingHelper.ParseCsvDate(input);

            // Assert
            Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("invalid")]
        [InlineData("2025/13/32")]
        [InlineData("32/13/2025")]
        [InlineData("abcd/ef/ghij")]
        [InlineData("2025-13-45")]
        public void ParseCsvDate_InvalidFormats_ShouldReturnMinValue(string input)
        {
            // Act
            var result = DateParsingHelper.ParseCsvDate(input);

            // Assert
            Assert.Equal(DateTime.MinValue, result);
        }

        [Theory]
        [InlineData("2025/06/02")]
        [InlineData("2025-06-02")]
        [InlineData("20250602")]
        [InlineData("2025/6/2")]
        [InlineData("2025-6-2")]
        public void IsValidCsvDate_ValidFormats_ShouldReturnTrue(string input)
        {
            // Act
            var result = DateParsingHelper.IsValidCsvDate(input);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("invalid")]
        [InlineData("2025/13/32")]
        public void IsValidCsvDate_InvalidFormats_ShouldReturnFalse(string input)
        {
            // Act
            var result = DateParsingHelper.IsValidCsvDate(input);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetSupportedFormatsString_ShouldReturnFormatsString()
        {
            // Act
            var result = DateParsingHelper.GetSupportedFormatsString();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Contains("yyyy/MM/dd", result);
            Assert.Contains("yyyy-MM-dd", result);
            Assert.Contains("yyyyMMdd", result);
        }

        [Fact]
        public void ParseJobDate_ValidDate_ShouldReturnCorrectDate()
        {
            // Arrange
            var input = "2025/06/02";
            var expected = new DateTime(2025, 6, 2);

            // Act
            var result = DateParsingHelper.ParseJobDate(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseJobDate_InvalidDate_ShouldThrowException()
        {
            // Arrange
            var input = "invalid";

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => DateParsingHelper.ParseJobDate(input));
            Assert.Contains("JobDateの解析に失敗しました", exception.Message);
            Assert.Contains("invalid", exception.Message);
            Assert.Contains("サポート形式", exception.Message);
        }

        [Theory]
        [InlineData("2025/06/02")]
        [InlineData("2025-06-02")]
        [InlineData("20250602")]
        public void TryParseExactMultipleFormats_ValidFormats_ShouldReturnTrue(string input)
        {
            // Arrange
            string[] formats = { "yyyy/MM/dd", "yyyy-MM-dd", "yyyyMMdd" };

            // Act
            var success = DateParsingHelper.TryParseExactMultipleFormats(input, formats, out var result);

            // Assert
            Assert.True(success);
            Assert.Equal(new DateTime(2025, 6, 2), result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("invalid")]
        public void TryParseExactMultipleFormats_InvalidFormats_ShouldReturnFalse(string input)
        {
            // Arrange
            string[] formats = { "yyyy/MM/dd", "yyyy-MM-dd", "yyyyMMdd" };

            // Act
            var success = DateParsingHelper.TryParseExactMultipleFormats(input, formats, out var result);

            // Assert
            Assert.False(success);
            Assert.Equal(DateTime.MinValue, result);
        }

        [Fact]
        public void ParseCsvDate_WithWhitespace_ShouldTrimAndParse()
        {
            // Arrange
            var input = "  2025/06/02  ";
            var expected = new DateTime(2025, 6, 2);

            // Act
            var result = DateParsingHelper.ParseCsvDate(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseCsvDate_WithTime_ShouldReturnDateOnly()
        {
            // Arrange - 標準解析で時刻付きが処理される場合
            var input = "2025/06/02 14:30:45";
            var expected = new DateTime(2025, 6, 2); // 日付のみ

            // Act
            var result = DateParsingHelper.ParseCsvDate(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}