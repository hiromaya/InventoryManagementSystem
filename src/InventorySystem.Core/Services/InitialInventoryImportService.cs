using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Core.Services;

/// <summary>
/// 初期在庫インポートサービス
/// </summary>
public class InitialInventoryImportService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductMasterRepository _productRepository;
    private readonly IDatasetManagementRepository _dataSetRepository;
    private readonly ILogger<InitialInventoryImportService> _logger;
    private readonly string _importPath;
    private readonly string _processedPath;
    private readonly string _errorPath;

    public InitialInventoryImportService(
        IInventoryRepository inventoryRepository,
        IProductMasterRepository productRepository,
        IDatasetManagementRepository dataSetRepository,
        ILogger<InitialInventoryImportService> logger,
        string importPath,
        string processedPath,
        string errorPath)
    {
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _dataSetRepository = dataSetRepository;
        _logger = logger;
        _importPath = importPath;
        _processedPath = processedPath;
        _errorPath = errorPath;
    }

    /// <summary>
    /// 初期在庫データをインポート
    /// </summary>
    public async Task<ImportResult> ImportAsync(string department)
    {
        var result = new ImportResult
        {
            StartTime = DateTime.Now,
            Department = department
        };

        try
        {
            _logger.LogInformation("=== 初期在庫インポート開始 ===");
            _logger.LogInformation("部門: {Department}", department);
            _logger.LogInformation("インポートパス: {Path}", _importPath);

            // ZAIK*.csvファイルを検索
            var files = Directory.GetFiles(_importPath, "ZAIK*.csv")
                .OrderByDescending(f => f)
                .ToList();

            if (!files.Any())
            {
                throw new FileNotFoundException($"ZAIK*.csvファイルが見つかりません: {_importPath}");
            }

            var targetFile = files.First();
            _logger.LogInformation("対象ファイル: {File}", Path.GetFileName(targetFile));

            // ファイル名から日付を抽出
            if (!TryExtractDateFromFileName(Path.GetFileName(targetFile), out var jobDate))
            {
                throw new InvalidOperationException($"ファイル名から日付を抽出できません: {Path.GetFileName(targetFile)}");
            }
            _logger.LogInformation("推定日付: {JobDate:yyyy-MM-dd}", jobDate);

            // DataSetId生成
            var dataSetId = $"INITIAL_{jobDate:yyyyMMdd}_{DateTime.Now:HHmmss}";
            _logger.LogInformation("DataSetId: {DataSetId}", dataSetId);

            // CSVデータ読み込み
            var (validRecords, errorRecords) = await ReadCsvFileAsync(targetFile);
            _logger.LogInformation("読み込み完了 - 有効: {Valid}件, エラー: {Error}件", 
                validRecords.Count, errorRecords.Count);

            // エラーレコードがある場合はエラーファイルに出力
            if (errorRecords.Any())
            {
                await WriteErrorRecordsAsync(targetFile, errorRecords);
            }

            // 有効レコードをInventoryMasterに変換
            var inventories = new List<InventoryMaster>();
            var conversionErrors = new List<(InitialInventoryRecord record, string error)>();

            foreach (var record in validRecords)
            {
                try
                {
                    var inventory = await ConvertToInventoryMasterAsync(record, jobDate, dataSetId);
                    inventories.Add(inventory);
                }
                catch (Exception ex)
                {
                    conversionErrors.Add((record, ex.Message));
                    _logger.LogWarning("レコード変換エラー: 商品{ProductCode} - {Error}", 
                        record.ProductCode, ex.Message);
                }
            }

            // 変換エラーがある場合は追記
            if (conversionErrors.Any())
            {
                errorRecords.AddRange(conversionErrors.Select(e => (e.record, e.error)));
                await WriteErrorRecordsAsync(targetFile, errorRecords);
            }

            // ErrorCountを設定
            result.ErrorCount = errorRecords.Count;
            _logger.LogInformation("変換完了 - 成功: {Success}件, エラー: {Error}件", inventories.Count, result.ErrorCount);

            // データベースに一括登録
            if (inventories.Any())
            {
                await BulkInsertInventoriesAsync(inventories, dataSetId, jobDate, department);
                result.SuccessCount = inventories.Count;
            }

            // 処理済みフォルダに移動
            await MoveToProcessedAsync(targetFile, jobDate);

            result.EndTime = DateTime.Now;
            result.IsSuccess = true;
            result.Message = $"初期在庫インポート完了: {inventories.Count}件";
            result.DataSetId = dataSetId;

            _logger.LogInformation("=== 初期在庫インポート完了 ===");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初期在庫インポートエラー");
            result.EndTime = DateTime.Now;
            result.IsSuccess = false;
            result.Message = $"エラー: {ex.Message}";
            result.Errors.Add(ex.ToString());
            return result;
        }
    }

    /// <summary>
    /// ファイル名から日付を抽出
    /// </summary>
    private bool TryExtractDateFromFileName(string fileName, out DateTime date)
    {
        // ZAIK20250531.csv → 2025-05-31
        var match = Regex.Match(fileName, @"ZAIK(\d{8})\.csv", RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            string dateString = match.Groups[1].Value;
            if (DateTime.TryParseExact(dateString, "yyyyMMdd", 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }
        }
        date = default;
        return false;
    }

    /// <summary>
    /// CSVファイルを読み込み
    /// </summary>
    private async Task<(List<InitialInventoryRecord> valid, List<(InitialInventoryRecord record, string error)> errors)> 
        ReadCsvFileAsync(string filePath)
    {
        var validRecords = new List<InitialInventoryRecord>();
        var errorRecords = new List<(InitialInventoryRecord record, string error)>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = context =>
            {
                _logger.LogWarning("不正なデータ: 行{Row}, フィールド{Field}", 
                    context.Context?.Parser?.Row ?? 0, 
                    context.Field ?? "不明");
            },
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(filePath, Encoding.UTF8);
        using var csv = new CsvReader(reader, config);
        
        // csv.Context.RegisterClassMap<InitialInventoryRecordMap>();  // 削除（属性ベースマッピングと競合するため）
        
        var rowNumber = 1;
        await csv.ReadAsync();
        csv.ReadHeader();
        
        while (await csv.ReadAsync())
        {
            rowNumber++;
            try
            {
                var record = csv.GetRecord<InitialInventoryRecord>();
                
                // 基本的なバリデーション
                var validationErrors = ValidateRecord(record, rowNumber);
                if (validationErrors.Any())
                {
                    foreach (var error in validationErrors)
                    {
                        errorRecords.Add((record, error));
                    }
                }
                else
                {
                    validRecords.Add(record);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("行{Row}の読み込みエラー: {Error}", rowNumber, ex.Message);
                // エラー行は記録するが処理は継続
                errorRecords.Add((null, $"行{rowNumber}: {ex.Message}"));
            }
        }

        return (validRecords, errorRecords);
    }

    /// <summary>
    /// レコードのバリデーション
    /// </summary>
    private List<string> ValidateRecord(InitialInventoryRecord record, int rowNumber)
    {
        var errors = new List<string>();

        // 必須項目チェック
        if (string.IsNullOrWhiteSpace(record.ProductCode))
            errors.Add($"行{rowNumber}: 商品コードが空です");

        if (string.IsNullOrWhiteSpace(record.GradeCode))
            errors.Add($"行{rowNumber}: 等級コードが空です");

        if (string.IsNullOrWhiteSpace(record.ClassCode))
            errors.Add($"行{rowNumber}: 階級コードが空です");

        if (string.IsNullOrWhiteSpace(record.ShippingMarkCode))
            errors.Add($"行{rowNumber}: 荷印コードが空です");

        if (string.IsNullOrWhiteSpace(record.ShippingMarkName))
            errors.Add($"行{rowNumber}: 荷印名が空です");

        // 数値妥当性チェック
        if (record.CurrentStockQuantity < 0)
            errors.Add($"行{rowNumber}: 在庫数量が負の値です ({record.CurrentStockQuantity})");

        if (record.CurrentStockAmount < 0)
            errors.Add($"行{rowNumber}: 在庫金額が負の値です ({record.CurrentStockAmount})");

        if (record.StandardPrice < 0)
            errors.Add($"行{rowNumber}: 単価が負の値です ({record.StandardPrice})");

        // データ整合性チェック（金額 = 数量 × 単価）
        if (record.CurrentStockQuantity > 0 && record.StandardPrice > 0)
        {
            var calculatedAmount = record.CurrentStockQuantity * record.StandardPrice;
            var difference = Math.Abs(calculatedAmount - record.CurrentStockAmount);
            
            // 誤差許容範囲: ±1円
            if (difference > 1)
            {
                errors.Add($"行{rowNumber}: 在庫金額の整合性エラー - 計算値: {calculatedAmount:F2}, 実際値: {record.CurrentStockAmount:F2}, 差額: {difference:F2}");
            }
        }

        // 除外対象チェック（商品コード00000）
        if (record.ProductCode == "00000")
        {
            errors.Add($"行{rowNumber}: 商品コード00000は除外対象です");
        }

        return errors;
    }

    /// <summary>
    /// InitialInventoryRecordをInventoryMasterに変換
    /// </summary>
    private async Task<InventoryMaster> ConvertToInventoryMasterAsync(
        InitialInventoryRecord record, DateTime jobDate, string dataSetId)
    {
        // 商品マスタから商品情報を取得
        var product = await _productRepository.GetByCodeAsync(record.ProductCode);

        // デバッグログを追加
        _logger.LogDebug($"商品{record.ProductCode}: PersonInChargeCode={record.PersonInChargeCode}");
        _logger.LogDebug($"商品{record.ProductCode}: AveragePrice={record.AveragePrice}");

        return new InventoryMaster
        {
            Key = new InventoryKey
            {
                ProductCode = record.ProductCode.PadLeft(5, '0'),
                GradeCode = record.GradeCode.PadLeft(3, '0'),
                ClassCode = record.ClassCode.PadLeft(3, '0'),
                ShippingMarkCode = record.ShippingMarkCode.PadLeft(4, '0'),
                ShippingMarkName = (record.ShippingMarkName ?? "").PadRight(8).Substring(0, 8)
            },
            
            // 商品情報
            ProductName = product?.ProductName ?? $"商品{record.ProductCode}",
            PersonInChargeCode = record.PersonInChargeCode,  // 追加
            Unit = product?.UnitCode ?? "PCS",
            StandardPrice = record.StandardPrice,
            AveragePrice = record.AveragePrice,  // 追加
            ProductCategory1 = product?.ProductCategory1 ?? "",
            ProductCategory2 = product?.ProductCategory2 ?? "",
            
            // 在庫数量・金額
            PreviousMonthQuantity = record.PreviousStockQuantity,
            PreviousMonthAmount = record.PreviousStockAmount,
            CurrentStock = record.CurrentStockQuantity,
            CurrentStockAmount = record.CurrentStockAmount,
            DailyStock = 0, // 初期データなので0
            DailyStockAmount = 0,
            
            // メタデータ
            JobDate = jobDate,
            DataSetId = dataSetId,
            ImportType = "INITIAL",
            IsActive = true,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now,
            CreatedBy = "import-initial-inventory",
            DailyFlag = '9'
        };
    }

    /// <summary>
    /// 在庫データを一括登録
    /// </summary>
    private async Task BulkInsertInventoriesAsync(
        List<InventoryMaster> inventories, string dataSetId, DateTime jobDate, string department)
    {
        _logger.LogInformation("データベース登録開始: {Count}件", inventories.Count);

        // 既存データの確認
        var existingCount = await _inventoryRepository.GetCountByJobDateAsync(jobDate);
        if (existingCount > 0)
        {
            _logger.LogWarning("JobDate {JobDate:yyyy-MM-dd} の既存データが{Count}件存在します", 
                jobDate, existingCount);
        }

        // DatasetManagementエンティティを作成
        var datasetManagement = new DatasetManagement
        {
            DatasetId = dataSetId,
            JobDate = jobDate,
            ProcessType = "INITIAL_INVENTORY",
            ImportType = "INITIAL",
            RecordCount = inventories.Count,
            TotalRecordCount = inventories.Count,
            IsActive = true,
            IsArchived = false,
            Department = department,
            CreatedAt = DateTime.Now,
            CreatedBy = "import-initial-inventory",
            Notes = $"初期在庫インポート: {inventories.Count}件"
        };

        // トランザクション内で処理を実行
        var processedCount = await _inventoryRepository.ProcessInitialInventoryInTransactionAsync(
            inventories,
            datasetManagement,
            true  // 既存のINITデータを無効化
        );

        _logger.LogInformation("データベース登録完了: {ProcessedCount}件処理", processedCount);
    }

    /// <summary>
    /// エラーレコードをファイルに出力
    /// </summary>
    private async Task WriteErrorRecordsAsync(
        string originalFilePath, List<(InitialInventoryRecord record, string error)> errorRecords)
    {
        var errorFileName = $"{Path.GetFileNameWithoutExtension(originalFilePath)}_errors_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var errorPath = Path.Combine(_errorPath, errorFileName);

        Directory.CreateDirectory(_errorPath);

        using var writer = new StreamWriter(errorPath, false, Encoding.UTF8);
        await writer.WriteLineAsync("エラー内容,商品コード,等級コード,階級コード,荷印コード,荷印名,前日在庫数量,前日在庫金額,当日在庫数量,当日在庫単価,当日在庫金額");
        
        foreach (var (record, error) in errorRecords)
        {
            if (record != null)
            {
                await writer.WriteLineAsync($"{error},{record.ProductCode},{record.GradeCode}," +
                    $"{record.ClassCode},{record.ShippingMarkCode},{record.ShippingMarkName}," +
                    $"{record.PreviousStockQuantity},{record.PreviousStockAmount}," +
                    $"{record.CurrentStockQuantity},{record.StandardPrice},{record.CurrentStockAmount}");
            }
            else
            {
                await writer.WriteLineAsync($"{error},,,,,,,,,,");
            }
        }

        _logger.LogInformation("エラーファイル出力: {Path}", errorPath);
    }

    /// <summary>
    /// 処理済みフォルダに移動
    /// </summary>
    private async Task MoveToProcessedAsync(string filePath, DateTime jobDate)
    {
        var processedFolder = Path.Combine(_processedPath, jobDate.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(processedFolder);
        
        var destPath = Path.Combine(processedFolder, Path.GetFileName(filePath));
        File.Move(filePath, destPath, true);
        
        _logger.LogInformation("ファイルを処理済みフォルダに移動: {Path}", destPath);
        await Task.CompletedTask;
    }
}

/// <summary>
/// インポート結果
/// </summary>
public class ImportResult
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Department { get; set; } = string.Empty;
    public string DataSetId { get; set; } = string.Empty;
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
}