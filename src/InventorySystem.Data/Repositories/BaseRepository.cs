using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Data.Repositories;

public abstract class BaseRepository
{
    protected readonly string _connectionString;
    protected readonly ILogger _logger;

    protected BaseRepository(string connectionString, ILogger logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    protected void LogError(Exception ex, string operation, object? parameters = null)
    {
        _logger.LogError(ex, "Error in {Operation}. Parameters: {@Parameters}", operation, parameters);
    }

    protected void LogInfo(string message, object? parameters = null)
    {
        _logger.LogInformation("{Message}. Parameters: {@Parameters}", message, parameters);
    }
}