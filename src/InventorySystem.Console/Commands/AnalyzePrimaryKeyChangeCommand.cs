using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Dapper;

namespace InventorySystem.Console.Commands
{
    /// <summary>
    /// 主キー変更前のデータ分析コマンド
    /// </summary>
    public class AnalyzePrimaryKeyChangeCommand
    {
        private readonly string _connectionString;
        private readonly ILogger<AnalyzePrimaryKeyChangeCommand> _logger;

        public AnalyzePrimaryKeyChangeCommand(IConfiguration configuration, ILogger<AnalyzePrimaryKeyChangeCommand> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("接続文字列が設定されていません");
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("========== InventoryMaster データ分析開始 ==========");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // 1. 全レコード数の確認
            var totalRecords = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM InventoryMaster");
            _logger.LogInformation("1. 全レコード数: {Count}", totalRecords);

            // 2. JobDate別のレコード数（上位10件）
            _logger.LogInformation("2. JobDate別のレコード数（上位10件）");
            var jobDateCounts = await connection.QueryAsync<dynamic>(@"
                SELECT TOP 10
                    JobDate,
                    COUNT(*) as RecordCount
                FROM InventoryMaster
                GROUP BY JobDate
                ORDER BY JobDate DESC");

            foreach (var item in jobDateCounts)
            {
                _logger.LogInformation("  JobDate: {JobDate:yyyy-MM-dd}, レコード数: {Count}",
                    (DateTime)item.JobDate, (int)item.RecordCount);
            }

            // 3. 5項目キーで見た場合の重複状況
            _logger.LogInformation("3. 5項目キーで見た場合の重複状況");
            var duplicateInfo = await connection.QuerySingleAsync<dynamic>(@"
                WITH DuplicateKeys AS (
                    SELECT 
                        ProductCode, 
                        GradeCode, 
                        ClassCode, 
                        ShippingMarkCode, 
                        ManualShippingMark,
                        COUNT(DISTINCT JobDate) as JobDateCount,
                        COUNT(*) as RecordCount
                    FROM InventoryMaster
                    GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                    HAVING COUNT(*) > 1
                )
                SELECT 
                    COUNT(*) as DuplicateKeyCount,
                    SUM(RecordCount) as TotalDuplicateRecords,
                    MAX(JobDateCount) as MaxJobDatesPerKey,
                    AVG(CAST(JobDateCount as FLOAT)) as AvgJobDatesPerKey
                FROM DuplicateKeys");

            _logger.LogInformation("  重複キー数: {Count}", (int)(duplicateInfo.DuplicateKeyCount ?? 0));
            _logger.LogInformation("  重複レコード総数: {Count}", (int)(duplicateInfo.TotalDuplicateRecords ?? 0));
            _logger.LogInformation("  最大JobDate数/キー: {Max}", (int)(duplicateInfo.MaxJobDatesPerKey ?? 0));
            _logger.LogInformation("  平均JobDate数/キー: {Avg:F2}", (double)(duplicateInfo.AvgJobDatesPerKey ?? 0));

            // 4. 重複キーの詳細（上位5件）
            _logger.LogInformation("4. 重複キーの詳細（上位5件）");
            var duplicateDetails = await connection.QueryAsync<dynamic>(@"
                SELECT TOP 5
                    ProductCode, 
                    GradeCode, 
                    ClassCode, 
                    ShippingMarkCode, 
                    LEFT(ManualShippingMark, 20) as ManualShippingMark_Short,
                    COUNT(DISTINCT JobDate) as JobDateCount,
                    MIN(JobDate) as MinJobDate,
                    MAX(JobDate) as MaxJobDate,
                    COUNT(*) as RecordCount
                FROM InventoryMaster
                GROUP BY ProductCode, GradeCode, ClassCode, ShippingMarkCode, ManualShippingMark
                HAVING COUNT(*) > 1
                ORDER BY COUNT(*) DESC, ProductCode");

            foreach (var detail in duplicateDetails)
            {
                _logger.LogInformation("  商品: {ProductCode}, JobDate数: {Count}, 期間: {MinDate:yyyy-MM-dd} ～ {MaxDate:yyyy-MM-dd}",
                    (string)detail.ProductCode, (int)detail.JobDateCount, (DateTime)detail.MinJobDate, (DateTime)detail.MaxJobDate);
            }

            // 5. 削減見込み
            var uniqueKeys = await connection.QuerySingleAsync<int>(@"
                SELECT COUNT(DISTINCT ProductCode + '|' + GradeCode + '|' + ClassCode + '|' + 
                                    ShippingMarkCode + '|' + ManualShippingMark) 
                FROM InventoryMaster");

            var reductionCount = totalRecords - uniqueKeys;
            var reductionRate = totalRecords > 0 ? (double)reductionCount / totalRecords * 100 : 0;

            _logger.LogInformation("5. 削減見込み");
            _logger.LogInformation("  総レコード数: {Total}", totalRecords);
            _logger.LogInformation("  ユニークキー数: {Unique}", uniqueKeys);
            _logger.LogInformation("  削減されるレコード数: {Reduction}", reductionCount);
            _logger.LogInformation("  削減率: {Rate:F1}%", reductionRate);

            _logger.LogInformation("========== 分析完了 ==========");

            _logger.LogWarning("【重要】この分析結果を基に、以下を検討してください：");
            _logger.LogWarning("1. 履歴データの保存が必要かどうか");
            _logger.LogWarning("2. 最新データのみで業務に影響がないか");
            _logger.LogWarning("3. 削減されるデータ量が許容範囲内か");
        }
    }
}