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
/// 階級マスタリポジトリ実装
/// CSVファイル「階級汎用マスター２.csv」からデータを読み込む
/// </summary>
public class ClassMasterRepository : IClassMasterRepository
{
    private readonly ILogger<ClassMasterRepository> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _csvFilePath;
    private readonly string _connectionString;
    private const string CacheKey = "ClassMaster";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public ClassMasterRepository(
        ILogger<ClassMasterRepository> logger,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _logger = logger;
        _cache = cache;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("接続文字列が設定されていません");
        
        // CSVファイルパスの設定（環境変数またはデフォルトパスを使用）
        var basePath = Environment.GetEnvironmentVariable("MASTER_DATA_PATH") ?? @"D:\InventoryImport\DeptA\Import";
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
                await connection.ExecuteAsync("TRUNCATE TABLE ClassMaster", transaction: transaction);

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
                        var classCode = csv.GetField<string>(0)?.Trim();
                        var className = csv.GetField<string>(1)?.Trim();
                        var searchKana = csv.GetField<string>(2)?.Trim() ?? "";

                        if (!string.IsNullOrEmpty(classCode) && !string.IsNullOrEmpty(className))
                        {
                            var sql = @"
                                INSERT INTO ClassMaster (ClassCode, ClassName, SearchKana, CreatedAt, UpdatedAt)
                                VALUES (@ClassCode, @ClassName, @SearchKana, GETDATE(), GETDATE())";

                            await connection.ExecuteAsync(sql, new
                            {
                                ClassCode = classCode,
                                ClassName = className,
                                SearchKana = searchKana
                            }, transaction);

                            importCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "階級マスタの行読み込みでエラーが発生しました");
                        continue;
                    }
                }

                await transaction.CommitAsync();
                
                // キャッシュをクリア
                _cache.Remove(CacheKey);
                
                _logger.LogInformation("階級マスタのインポートが完了しました。件数: {Count}", importCount);
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
            _logger.LogError(ex, "階級マスタのインポートでエラーが発生しました");
            throw;
        }
    }

    public async Task<int> GetCountAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT COUNT(*) FROM ClassMaster";
            var count = await connection.QuerySingleAsync<int>(sql);
            
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "階級マスタの件数取得でエラーが発生しました");
            throw;
        }
    }
}