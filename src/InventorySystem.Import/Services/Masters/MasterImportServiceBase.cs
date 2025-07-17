using CsvHelper;
using CsvHelper.Configuration;
using InventorySystem.Core.Entities.Masters;
using InventorySystem.Core.Interfaces;
using InventorySystem.Core.Interfaces.Masters;
using InventorySystem.Core.Models;
using InventorySystem.Import.Models.Csv.Masters;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using DataSetStatus = InventorySystem.Core.Interfaces.DataSetStatus;

namespace InventorySystem.Import.Services.Masters;

/// <summary>
/// マスタ系CSVインポートサービスの基底クラス
/// </summary>
/// <typeparam name="TEntity">エンティティクラス</typeparam>
/// <typeparam name="TModel">CSVモデルクラス</typeparam>
public abstract class MasterImportServiceBase<TEntity, TModel> : IImportService
    where TEntity : CategoryMasterBase, new()
    where TModel : CategoryMasterCsv
{
    protected readonly ICategoryMasterRepository<TEntity> _repository;
    protected readonly IUnifiedDataSetService _unifiedDataSetService;
    protected readonly ILogger<MasterImportServiceBase<TEntity, TModel>> _logger;
    
    /// <summary>
    /// ファイル名パターン（サブクラスで実装）
    /// </summary>
    protected abstract string FileNamePattern { get; }
    
    /// <summary>
    /// サービス名（サブクラスで実装）
    /// </summary>
    public abstract string ServiceName { get; }
    
    /// <summary>
    /// 処理順序（サブクラスで実装）
    /// </summary>
    public abstract int ProcessOrder { get; }

    protected MasterImportServiceBase(
        ICategoryMasterRepository<TEntity> repository,
        IUnifiedDataSetService unifiedDataSetService,
        ILogger<MasterImportServiceBase<TEntity, TModel>> logger)
    {
        _repository = repository;
        _unifiedDataSetService = unifiedDataSetService;
        _logger = logger;
    }

    public bool CanHandle(string fileName)
    {
        return fileName.Contains(FileNamePattern, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ImportResult> ImportAsync(string filePath, DateTime importDate)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSVファイルが見つかりません: {filePath}");
        }

        var dataSetId = GenerateDataSetId();
        var importedCount = 0;
        var errorMessages = new List<string>();

        _logger.LogInformation("{ServiceName}CSV取込開始: {FilePath}, DataSetId: {DataSetId}", 
            ServiceName, filePath, dataSetId);

        try
        {
            // 統一データセット作成
            var dataSetInfo = new UnifiedDataSetInfo
            {
                ProcessType = GetProcessType(),
                ImportType = "IMPORT",
                Name = $"{ServiceName}取込 {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                Description = $"{ServiceName}CSV取込: {Path.GetFileName(filePath)}",
                JobDate = importDate,
                FilePath = filePath,
                CreatedBy = $"{GetProcessType().ToLower()}-import"
            };
            
            dataSetId = await _unifiedDataSetService.CreateDataSetAsync(dataSetInfo);

            // CSV読み込み処理
            var entities = new List<TEntity>();
            var records = await ReadCsvFileAsync(filePath);
            _logger.LogInformation("CSVレコード読み込み完了: {Count}件", records.Count);

            // 既存データをクリア（全件入れ替え）
            await _repository.DeleteAllAsync();

            // バリデーションと変換
            foreach (var (record, index) in records.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (record.Code <= 0)
                    {
                        var error = $"行{index}: コードが無効です（{record.Code}）";
                        errorMessages.Add(error);
                        _logger.LogWarning(error);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(record.Name))
                    {
                        var error = $"行{index}: 名称が空です";
                        errorMessages.Add(error);
                        _logger.LogWarning(error);
                        continue;
                    }

                    var entity = ConvertToEntity(record);
                    entities.Add(entity);
                    importedCount++;
                }
                catch (Exception ex)
                {
                    var error = $"行{index}: CSV変換エラー - {ex.Message}";
                    errorMessages.Add(error);
                    _logger.LogError(ex, error);
                }
            }

            // バッチ処理でデータベースに保存
            if (entities.Any())
            {
                await _repository.InsertBulkAsync(entities);
                _logger.LogInformation("{ServiceName}保存完了: {Count}件", ServiceName, entities.Count);
            }

            // データセットレコード数更新
            await _unifiedDataSetService.UpdateRecordCountAsync(dataSetId, importedCount);
            
            if (errorMessages.Any())
            {
                var errorMessage = string.Join("\n", errorMessages);
                await _unifiedDataSetService.UpdateStatusAsync(dataSetId, DataSetStatus.Failed, errorMessage);
                _logger.LogWarning("{ServiceName}CSV取込部分成功: 成功{Success}件, エラー{Error}件", 
                    ServiceName, importedCount, errorMessages.Count);
            }
            else
            {
                await _unifiedDataSetService.CompleteDataSetAsync(dataSetId, importedCount);
                _logger.LogInformation("{ServiceName}CSV取込完了: {Count}件", ServiceName, importedCount);
            }

            return new ImportResult
            {
                DataSetId = dataSetId,
                Status = errorMessages.Any() ? "Failed" : "Completed",
                ImportedCount = importedCount,
                ErrorMessage = errorMessages.Any() ? string.Join("\n", errorMessages) : null,
                FilePath = filePath,
                CreatedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            await _unifiedDataSetService.UpdateStatusAsync(dataSetId, DataSetStatus.Failed, ex.Message);
            _logger.LogError(ex, "{ServiceName}CSV取込エラー: {FilePath}", ServiceName, filePath);
            throw;
        }
    }

    /// <summary>
    /// CSVファイルを読み込む
    /// </summary>
    private async Task<List<TModel>> ReadCsvFileAsync(string filePath)
    {
        _logger.LogInformation("UTF-8エンコーディングでCSVファイルを読み込みます: {FilePath}", filePath);
        
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = context => 
            {
                _logger.LogWarning($"不正なデータ: 行 {context.Context?.Parser?.Row ?? 0}, フィールド {context.Field ?? "不明"}");
            },
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        });

        var records = new List<TModel>();
        
        await csv.ReadAsync();
        csv.ReadHeader();
        _logger.LogInformation("ヘッダー読み込み完了");
        
        var rowNumber = 0;
        while (await csv.ReadAsync())
        {
            rowNumber++;
            try
            {
                var record = csv.GetRecord<TModel>();
                if (record != null)
                {
                    if (rowNumber <= 5)
                    {
                        _logger.LogDebug("行{Row}: コード={Code}, 名称={Name}", 
                            rowNumber, record.Code, record.Name);
                    }
                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"行 {rowNumber} の読み込みでエラー: {ex.Message}");
                continue;
            }
        }
        
        return records;
    }

    /// <summary>
    /// CSVレコードをEntityに変換
    /// </summary>
    protected virtual TEntity ConvertToEntity(TModel csv)
    {
        return new TEntity
        {
            CategoryCode = csv.Code,
            CategoryName = csv.Name?.Trim() ?? string.Empty,
            SearchKana = csv.SearchKana?.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// プロセスタイプを取得
    /// </summary>
    protected virtual string GetProcessType()
    {
        return ServiceName.Replace("マスタ", "").Replace("分類", "CATEGORY").ToUpper();
    }

    /// <summary>
    /// データセットIDを生成
    /// </summary>
    private string GenerateDataSetId()
    {
        var guid = Guid.NewGuid().ToString("N");
        var prefix = GetProcessType().Substring(0, Math.Min(4, GetProcessType().Length));
        return $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{guid.Substring(0, 8)}";
    }
}