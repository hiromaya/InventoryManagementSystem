using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

/// <summary>
/// 階級マスタリポジトリ実装
/// CSVファイル「階級汎用マスター２.csv」からデータを読み込む
/// </summary>
public class ClassMasterRepository : IClassMasterRepository
{
    private readonly ILogger<ClassMasterRepository> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _csvFilePath;
    private const string CacheKey = "ClassMaster";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public ClassMasterRepository(
        ILogger<ClassMasterRepository> logger,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _logger = logger;
        _cache = cache;
        
        // CSVファイルパスの設定（設定ファイルから読み込むか、デフォルトパスを使用）
        var basePath = configuration["MasterDataPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "MasterData");
        _csvFilePath = Path.Combine(basePath, "階級汎用マスター２.csv");
    }

    public async Task<string?> GetClassNameAsync(string classCode)
    {
        if (string.IsNullOrEmpty(classCode))
            return null;

        try
        {
            var allClasses = await GetAllClassesAsync();
            return allClasses.TryGetValue(classCode, out var className) ? className : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "階級名の取得でエラーが発生しました。コード: {ClassCode}", classCode);
            return $"階{classCode}"; // エラー時のデフォルト値
        }
    }

    public async Task<Dictionary<string, string>> GetAllClassesAsync()
    {
        // キャッシュから取得を試みる
        if (_cache.TryGetValue<Dictionary<string, string>>(CacheKey, out var cachedData))
        {
            return cachedData!;
        }

        try
        {
            var classes = await LoadClassesFromCsvAsync();
            
            // キャッシュに保存
            _cache.Set(CacheKey, classes, _cacheExpiration);
            
            return classes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "階級マスタの読み込みでエラーが発生しました");
            return new Dictionary<string, string>();
        }
    }

    private async Task<Dictionary<string, string>> LoadClassesFromCsvAsync()
    {
        var classes = new Dictionary<string, string>();

        if (!File.Exists(_csvFilePath))
        {
            _logger.LogWarning("階級マスタCSVファイルが見つかりません: {Path}", _csvFilePath);
            return classes;
        }

        using var reader = new StreamReader(_csvFilePath, Encoding.GetEncoding("Shift_JIS"));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null
        });

        await csv.ReadAsync(); // 最初の行を読む
        
        while (await csv.ReadAsync())
        {
            try
            {
                // 列0: 階級コード、列1: 階級名
                var classCode = csv.GetField<string>(0)?.Trim();
                var className = csv.GetField<string>(1)?.Trim();

                if (!string.IsNullOrEmpty(classCode) && !string.IsNullOrEmpty(className))
                {
                    classes[classCode] = className;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "階級マスタの行読み込みでエラーが発生しました");
                continue;
            }
        }

        _logger.LogInformation("階級マスタを読み込みました。件数: {Count}", classes.Count);
        return classes;
    }
}