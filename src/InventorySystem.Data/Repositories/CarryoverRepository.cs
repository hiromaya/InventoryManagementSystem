using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using InventorySystem.Core.Interfaces;

namespace InventorySystem.Data.Repositories
{
    public class CarryoverRepository : BaseRepository, ICarryoverRepository
    {
        public CarryoverRepository(string connectionString, ILogger<CarryoverRepository> logger)
            : base(connectionString, logger)
        {
        }

        /// <inheritdoc />
        public async Task<int> MergeFromCpInventoryAsync(DateTime jobDate, string dataSetId)
        {
            try
            {
                using var connection = CreateConnection();
                var parameters = new DynamicParameters();
                parameters.Add("@JobDate", jobDate.Date, DbType.Date);
                parameters.Add("@DataSetId", dataSetId, DbType.String, size: 50);

                await connection.ExecuteAsync(
                    "sp_MergeCarryoverFromCpInventory",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                LogInfo($"CP→Carryover MERGE完了: JobDate={jobDate:yyyy-MM-dd}, DataSetId={dataSetId}");
                return 0; // 影響件数はSP側でPRINT/SELECTしているため0を返す
            }
            catch (Exception ex)
            {
                LogError(ex, nameof(MergeFromCpInventoryAsync), new { jobDate, dataSetId });
                throw;
            }
        }
    }
}

