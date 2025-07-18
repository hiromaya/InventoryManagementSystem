# Database Schema Check Script
$serverInstance = "localhost\SQLEXPRESS"
$database = "InventoryManagementDB"

Write-Host "=== Database Schema Check ===" -ForegroundColor Green
Write-Host "Server: $serverInstance" -ForegroundColor Yellow
Write-Host "Database: $database" -ForegroundColor Yellow

# SQL Query
$query = @"
-- ProductMaster table structure
PRINT '=== ProductMaster Table Structure ==='
SELECT 
    c.COLUMN_NAME as 'Column Name',
    c.DATA_TYPE as 'Data Type',
    c.CHARACTER_MAXIMUM_LENGTH as 'Max Length',
    c.IS_NULLABLE as 'Allow NULL'
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'ProductMaster'
ORDER BY c.ORDINAL_POSITION;

-- CustomerMaster table structure
PRINT ''
PRINT '=== CustomerMaster Table Structure ==='
SELECT 
    c.COLUMN_NAME as 'Column Name',
    c.DATA_TYPE as 'Data Type',
    c.CHARACTER_MAXIMUM_LENGTH as 'Max Length',
    c.IS_NULLABLE as 'Allow NULL'
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'CustomerMaster'
ORDER BY c.ORDINAL_POSITION;

-- SupplierMaster table structure
PRINT ''
PRINT '=== SupplierMaster Table Structure ==='
SELECT 
    c.COLUMN_NAME as 'Column Name',
    c.DATA_TYPE as 'Data Type',
    c.CHARACTER_MAXIMUM_LENGTH as 'Max Length',
    c.IS_NULLABLE as 'Allow NULL'
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'SupplierMaster'
ORDER BY c.ORDINAL_POSITION;

-- Date columns check
PRINT ''
PRINT '=== Date Columns Check ==='
SELECT 
    TABLE_NAME as 'Table Name',
    COLUMN_NAME as 'Column Name',
    DATA_TYPE as 'Data Type'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('ProductMaster', 'CustomerMaster', 'SupplierMaster')
AND (COLUMN_NAME LIKE '%Date%' OR COLUMN_NAME LIKE '%At%')
ORDER BY TABLE_NAME, COLUMN_NAME;
"@

try {
    # Execute with sqlcmd
    $result = sqlcmd -S $serverInstance -d $database -Q $query -E -s "," -W
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nExecution Result:" -ForegroundColor Green
        $result
    } else {
        Write-Host "Error occurred (Exit Code: $LASTEXITCODE)" -ForegroundColor Red
        $result
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Completed ===" -ForegroundColor Green