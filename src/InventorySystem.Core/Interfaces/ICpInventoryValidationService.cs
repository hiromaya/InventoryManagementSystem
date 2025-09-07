using Microsoft.Data.SqlClient;
using InventorySystem.Core.Models;

namespace InventorySystem.Core.Interfaces
{
    public interface ICpInventoryValidationService
    {
        Task<CpInventoryValidationResult> ValidateAsync(
            DateTime jobDate,
            string? departmentCode = null);

        Task<int> ApplyCorrectionsAsync(
            DateTime jobDate,
            CpInventoryValidationResult result,
            SqlTransaction? transaction = null);
    }
}
