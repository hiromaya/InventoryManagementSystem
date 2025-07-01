using System;
using System.Globalization;
using Microsoft.Data.SqlClient;
using System.Data;

class DateInvestigationTest
{
    static async Task Main(string[] args)
    {
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=InventorySystemDB;Trusted_Connection=true;";
        
        Console.WriteLine("=== Date Investigation Test ===");
        Console.WriteLine($"Current Culture: {System.Globalization.CultureInfo.CurrentCulture.Name}");
        Console.WriteLine($"Today's Date: {DateTime.Today}");
        Console.WriteLine($"Date Format: {DateTime.Today.ToString("yyyy-MM-dd")}");
        
        // Test date parsing like the CSV import does
        Console.WriteLine("\n=== CSV Date Parsing Test ===");
        TestDateParsing("20250630");  // YYYYMMDD format from CSV
        
        // Test SQL Server date handling
        Console.WriteLine("\n=== SQL Server Date Test ===");
        await TestSqlServerDates(connectionString);
        
        // Test actual data in database
        Console.WriteLine("\n=== Database Data Investigation ===");
        await InvestigateActualData(connectionString);
    }
    
    static void TestDateParsing(string dateStr)
    {
        Console.WriteLine($"Parsing date string: '{dateStr}'");
        
        // YYYYMMDD format parsing (same as in SalesVoucherDaijinCsv.ParseDate)
        if (dateStr.Length == 8 && int.TryParse(dateStr, out _))
        {
            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                Console.WriteLine($"  Parsed as: {date} (DateTime)");
                Console.WriteLine($"  Formatted ISO: {date.ToString("yyyy-MM-dd")}");
                Console.WriteLine($"  Formatted German: {date.ToString("dd.MM.yyyy")}");
                Console.WriteLine($"  As SQL Parameter: {date}");
            }
        }
    }
    
    static async Task TestSqlServerDates(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var testDate = new DateTime(2025, 6, 30);
            Console.WriteLine($"Test DateTime: {testDate}");
            
            // Test CAST operation like in the optimization service
            var sql = @"
                SELECT 
                    @testDate as OriginalParameter,
                    CAST(@testDate AS DATE) as CastResult,
                    FORMAT(@testDate, 'yyyy-MM-dd') as FormattedResult
            ";
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@testDate", testDate);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"  Parameter Value: {reader[0]}");
                Console.WriteLine($"  CAST Result: {reader[1]}");
                Console.WriteLine($"  Formatted: {reader[2]}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SQL Server test failed: {ex.Message}");
        }
    }
    
    static async Task InvestigateActualData(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            // Check if tables exist and get sample data
            var tables = new[] { "SalesVouchers", "PurchaseVouchers", "InventoryAdjustments" };
            
            foreach (var table in tables)
            {
                Console.WriteLine($"\n--- {table} Investigation ---");
                
                // Check if table exists
                var existsSql = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{table}'";
                var existsCommand = new SqlCommand(existsSql, connection);
                var exists = (int)await existsCommand.ExecuteScalarAsync() > 0;
                
                if (!exists)
                {
                    Console.WriteLine($"  Table {table} does not exist");
                    continue;
                }
                
                // Get record count
                var countSql = $"SELECT COUNT(*) FROM {table}";
                var countCommand = new SqlCommand(countSql, connection);
                var count = (int)await countCommand.ExecuteScalarAsync();
                Console.WriteLine($"  Total records: {count}");
                
                if (count > 0)
                {
                    // Get sample JobDate values
                    var sampleSql = $@"
                        SELECT TOP 3 
                            JobDate,
                            VoucherDate,
                            FORMAT(JobDate, 'yyyy-MM-dd') as JobDateFormatted,
                            FORMAT(VoucherDate, 'yyyy-MM-dd') as VoucherDateFormatted,
                            CAST(JobDate AS DATE) as JobDateCast,
                            CreatedAt
                        FROM {table}
                        ORDER BY CreatedAt DESC
                    ";
                    
                    var sampleCommand = new SqlCommand(sampleSql, connection);
                    using var reader = await sampleCommand.ExecuteReaderAsync();
                    
                    int rowNum = 1;
                    while (await reader.ReadAsync())
                    {
                        Console.WriteLine($"  Row {rowNum}:");
                        Console.WriteLine($"    JobDate: {reader["JobDate"]}");
                        Console.WriteLine($"    VoucherDate: {reader["VoucherDate"]}");
                        Console.WriteLine($"    JobDate Formatted: {reader["JobDateFormatted"]}");
                        Console.WriteLine($"    JobDate CAST: {reader["JobDateCast"]}");
                        Console.WriteLine($"    Created: {reader["CreatedAt"]}");
                        rowNum++;
                    }
                    
                    reader.Close();
                    
                    // Test the exact query from optimization service
                    var optimizationTestSql = $@"
                        SELECT COUNT(*) as RecordCount
                        FROM {table}
                        WHERE CAST(JobDate AS DATE) = CAST(@jobDate AS DATE)
                    ";
                    
                    var testDate = new DateTime(2025, 6, 30);
                    var optimizationCommand = new SqlCommand(optimizationTestSql, connection);
                    optimizationCommand.Parameters.AddWithValue("@jobDate", testDate);
                    
                    var optimizationCount = (int)await optimizationCommand.ExecuteScalarAsync();
                    Console.WriteLine($"  Optimization query result for 2025-06-30: {optimizationCount} records");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database investigation failed: {ex.Message}");
        }
    }
}