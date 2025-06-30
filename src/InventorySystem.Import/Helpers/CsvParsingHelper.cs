using System.Globalization;

namespace InventorySystem.Import.Helpers;

/// <summary>
/// CSVインポート処理の数値パース用ヘルパークラス
/// ロケールに依存しない数値解析を提供
/// </summary>
public static class CsvParsingHelper
{
    /// <summary>
    /// 不変カルチャを使用してdecimal値を解析
    /// ドイツ語圏などでのカンマ区切り誤認識を防止
    /// </summary>
    /// <param name="value">解析対象の文字列</param>
    /// <returns>解析されたdecimal値</returns>
    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        // 不変カルチャで解析（小数点は常にピリオド）
        if (decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // フォールバック：現在のカルチャで試行
        if (decimal.TryParse(value.Trim(), out var fallbackResult))
        {
            return fallbackResult;
        }

        return 0m;
    }

    /// <summary>
    /// 不変カルチャを使用してint値を解析
    /// </summary>
    /// <param name="value">解析対象の文字列</param>
    /// <returns>解析されたint値</returns>
    public static int ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        // 不変カルチャで解析
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // フォールバック：現在のカルチャで試行
        if (int.TryParse(value.Trim(), out var fallbackResult))
        {
            return fallbackResult;
        }

        return 0;
    }

    /// <summary>
    /// 不変カルチャを使用してnullable int値を解析
    /// </summary>
    /// <param name="value">解析対象の文字列</param>
    /// <returns>解析されたnullable int値</returns>
    public static int? ParseNullableInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // 不変カルチャで解析
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // フォールバック：現在のカルチャで試行
        if (int.TryParse(value.Trim(), out var fallbackResult))
        {
            return fallbackResult;
        }

        return null;
    }

    /// <summary>
    /// 不変カルチャを使用してdouble値を解析
    /// </summary>
    /// <param name="value">解析対象の文字列</param>
    /// <returns>解析されたdouble値</returns>
    public static double ParseDouble(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0.0;

        // 不変カルチャで解析（小数点は常にピリオド）
        if (double.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // フォールバック：現在のカルチャで試行
        if (double.TryParse(value.Trim(), out var fallbackResult))
        {
            return fallbackResult;
        }

        return 0.0;
    }

    /// <summary>
    /// 不変カルチャを使用してfloat値を解析
    /// </summary>
    /// <param name="value">解析対象の文字列</param>
    /// <returns>解析されたfloat値</returns>
    public static float ParseFloat(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0f;

        // 不変カルチャで解析（小数点は常にピリオド）
        if (float.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // フォールバック：現在のカルチャで試行
        if (float.TryParse(value.Trim(), out var fallbackResult))
        {
            return fallbackResult;
        }

        return 0f;
    }
}