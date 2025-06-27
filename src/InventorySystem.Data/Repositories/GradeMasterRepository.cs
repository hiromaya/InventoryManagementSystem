using System.Data;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using InventorySystem.Core.Interfaces;
using Microsoft.Data.SqlClient;
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
    private readonly string _connectionString;
    private const string CacheKey = "GradeMaster";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public GradeMasterRepository(
        ILogger<GradeMasterRepository> logger,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _logger = logger;
        _cache = cache;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("接続文字列が設定されていません");
        
        // CSVファイルパスの設定（環境変数またはデフォルトパスを使用）
        var basePath = Environment.GetEnvironmentVariable("MASTER_DATA_PATH") ?? @"D:\InventoryImport\DeptA\Import";
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

    public async Task<int> ImportFromCsvAsync()
    {
        if (!File.Exists(_csvFilePath))
        {
            _logger.LogError("CSVファイルが見つかりません: {Path}", _csvFilePath);
            throw new FileNotFoundException($"CSVファイルが見つかりません: {_csvFilePath}");
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // トランザクション開始
            using var transaction = connection.BeginTransaction();
            
            try
            {
                // 既存データをクリア
                await connection.ExecuteAsync("TRUNCATE TABLE GradeMaster", transaction: transaction);

                // CSVを読み込み
                using var reader = new StreamReader(_csvFilePath, Encoding.UTF8);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    BadDataFound = null,
                    MissingFieldFound = null
                });

                var importCount = 0;
                await csv.ReadAsync(); // ヘッダーを読む
                csv.ReadHeader();

                while (await csv.ReadAsync())
                {
                    try
                    {
                        var gradeCode = csv.GetField<string>(0)?.Trim();
                        var gradeName = csv.GetField<string>(1)?.Trim();
                        var searchKana = csv.GetField<string>(2)?.Trim() ?? "";

                        if (!string.IsNullOrEmpty(gradeCode) && !string.IsNullOrEmpty(gradeName))
                        {
                            var sql = @"
                                INSERT INTO GradeMaster (GradeCode, GradeName, SearchKana, CreatedAt, UpdatedAt)
                                VALUES (@GradeCode, @GradeName, @SearchKana, GETDATE(), GETDATE())";

                            await connection.ExecuteAsync(sql, new
                            {
                                GradeCode = gradeCode,
                                GradeName = gradeName,
                                SearchKana = searchKana
                            }, transaction);

                            importCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "等級マスタの行読み込みでエラーが発生しました");
                        continue;
                    }
                }

                await transaction.CommitAsync();
                
                // キャッシュをクリア
                _cache.Remove(CacheKey);
                
                _logger.LogInformation("等級マスタのインポートが完了しました。件数: {Count}", importCount);
                return importCount;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等級マスタのインポートでエラーが発生しました");
            throw;
        }
    }
}