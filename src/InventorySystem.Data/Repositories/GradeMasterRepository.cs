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
/// 等級マスタリポジトリ実装
/// CSVファイル「等級汎用マスター１.csv」からデータを読み込む
/// </summary>
public class GradeMasterRepository : IGradeMasterRepository
{
    private readonly ILogger<GradeMasterRepository> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _csvFilePath;
    private const string CacheKey = "GradeMaster";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public GradeMasterRepository(
        ILogger<GradeMasterRepository> logger,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _logger = logger;
        _cache = cache;
        
        // CSVファイルパスの設定（設定ファイルから読み込むか、デフォルトパスを使用）
        var basePath = configuration["MasterDataPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "MasterData");
        _csvFilePath = Path.Combine(basePath, "等級汎用マスター１.csv");
    }

    public async Task<string?> GetGradeNameAsync(string gradeCode)
    {
        if (string.IsNullOrEmpty(gradeCode))
            return null;

        try
        {
            var allGrades = await GetAllGradesAsync();
            return allGrades.TryGetValue(gradeCode, out var gradeName) ? gradeName : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等級名の取得でエラーが発生しました。コード: {GradeCode}", gradeCode);
            return $"等{gradeCode}"; // エラー時のデフォルト値
        }
    }

    public async Task<Dictionary<string, string>> GetAllGradesAsync()
    {
        // キャッシュから取得を試みる
        if (_cache.TryGetValue<Dictionary<string, string>>(CacheKey, out var cachedData))
        {
            return cachedData!;
        }

        try
        {
            var grades = await LoadGradesFromCsvAsync();
            
            // キャッシュに保存
            _cache.Set(CacheKey, grades, _cacheExpiration);
            
            return grades;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等級マスタの読み込みでエラーが発生しました");
            return new Dictionary<string, string>();
        }
    }

    private async Task<Dictionary<string, string>> LoadGradesFromCsvAsync()
    {
        var grades = new Dictionary<string, string>();

        if (!File.Exists(_csvFilePath))
        {
            _logger.LogWarning("等級マスタCSVファイルが見つかりません: {Path}", _csvFilePath);
            return grades;
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
                // 列0: 等級コード、列1: 等級名
                var gradeCode = csv.GetField<string>(0)?.Trim();
                var gradeName = csv.GetField<string>(1)?.Trim();

                if (!string.IsNullOrEmpty(gradeCode) && !string.IsNullOrEmpty(gradeName))
                {
                    grades[gradeCode] = gradeName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "等級マスタの行読み込みでエラーが発生しました");
                continue;
            }
        }

        _logger.LogInformation("等級マスタを読み込みました。件数: {Count}", grades.Count);
        return grades;
    }
}