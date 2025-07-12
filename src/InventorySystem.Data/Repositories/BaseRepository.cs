using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

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

    protected void LogDebug(string message, object? parameters = null)
    {
        _logger.LogDebug("{Message}. Parameters: {@Parameters}", message, parameters);
    }

    protected void LogWarning(string message, object? parameters = null)
    {
        _logger.LogWarning("{Message}. Parameters: {@Parameters}", message, parameters);
    }
    
    /// <summary>
    /// トランザクション内で処理を実行するヘルパーメソッド
    /// </summary>
    protected async Task<T> ExecuteInTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> operation)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            var result = await operation(connection, transaction);
            await transaction.CommitAsync();
            LogDebug("Transaction committed successfully");
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, "Transaction failed, rolling back");
            try
            {
                await transaction.RollbackAsync();
                LogDebug("Transaction rolled back successfully");
            }
            catch (Exception rollbackEx)
            {
                LogError(rollbackEx, "Failed to rollback transaction");
            }
            throw;
        }
    }
    
    /// <summary>
    /// トランザクション内で処理を実行するヘルパーメソッド（戻り値なし版）
    /// </summary>
    protected async Task ExecuteInTransactionAsync(Func<SqlConnection, SqlTransaction, Task> operation)
    {
        await ExecuteInTransactionAsync(async (conn, tran) =>
        {
            await operation(conn, tran);
            return true;
        });
    }
}