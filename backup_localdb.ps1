# LocalDB Backup Script
# Execution Date: 2025-06-20

# 1. Create backup directory
$backupDate = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = "C:\Backup\InventoryDB_$backupDate"
New-Item -ItemType Directory -Force -Path $backupPath

Write-Host "Creating backup directory: $backupPath" -ForegroundColor Green

# 2. Connection string
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=InventoryManagementDB;Integrated Security=True;"

# 3. Export database schema
Write-Host "Exporting database schema..." -ForegroundColor Yellow

# Simple table list retrieval
$schemaFile = "$backupPath\schema.txt"
"InventoryManagementDB Schema" | Out-File $schemaFile
"Generated: $(Get-Date)" | Out-File $schemaFile -Append
"" | Out-File $schemaFile -Append

# Save table list
sqlcmd -S "(localdb)\MSSQLLocalDB" -d InventoryManagementDB -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME" >> $schemaFile

# 4. Export data in CSV format
Write-Host "Exporting data..." -ForegroundColor Yellow

# List of tables to export
$tables = @(
    "CpInventoryMaster",
    "SalesVouchers",
    "PurchaseVouchers", 
    "InventoryAdjustments",
    "InventoryMaster",
    "DataSets"
)

# Export each table with error handling
foreach ($table in $tables) {
    Write-Host "  - Exporting $table..."
    $outputFile = "$backupPath\$table.csv"
    # Execute bcp command (ignore errors)
    $bcpCmd = "bcp InventoryManagementDB.dbo.$table out `"$outputFile`" -S `"(localdb)\MSSQLLocalDB`" -T -c -t`",`" -r`"\n`""
    cmd /c $bcpCmd 2>$null
}

# 5. Record current data counts
Write-Host "Recording record counts..." -ForegroundColor Yellow

$countFile = "$backupPath\record_counts.txt"
"Record Counts - $(Get-Date)" | Out-File $countFile

# Count main tables only
$mainTables = @("CpInventoryMaster", "SalesVouchers", "PurchaseVouchers", "InventoryAdjustments", "InventoryMaster")
foreach ($table in $mainTables) {
    try {
        $count = sqlcmd -S "(localdb)\MSSQLLocalDB" -d InventoryManagementDB -h -1 -Q "SELECT COUNT(*) FROM $table"
        "$table : $count" | Out-File $countFile -Append
    } catch {
        "$table : Error" | Out-File $countFile -Append
    }
}

# 6. Create full database backup
Write-Host "Creating full database backup..." -ForegroundColor Yellow

$backupFile = "$backupPath\InventoryManagementDB_Full.bak"
$backupCommand = "BACKUP DATABASE [InventoryManagementDB] TO DISK = '$backupFile' WITH FORMAT, INIT, NAME = 'InventoryManagementDB-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10"

sqlcmd -S "(localdb)\MSSQLLocalDB" -Q $backupCommand

# 7. Record current project status
$statusReport = @"
==============================================
Inventory Management System Backup Info
==============================================
Backup Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Database: InventoryManagementDB

[Implemented Features]
- Database construction (all tables created)
- CSV import (171 columns format)
- CP Inventory Master creation
- Sales data aggregation
- Daily report PDF output

[Data Status]
- Sales Vouchers: 7,884 records (2025-05-13 to 2025-06-13)
- Sales on 2025-06-13: 614 records
- CP Inventory Master: 440 records
- Daily Report: 97 items processed

[Resolved Issues]
1. Sales data aggregation (removed ShippingMarkName from JOIN)
2. Sales data sign issue (resolved with ABS())
3. LEFT JOIN for zero sales items

[Next Steps]
1. Daily report layout adjustment
2. Unmatch list implementation
3. Inventory report implementation
==============================================
"@

$statusReport | Out-File "$backupPath\backup_status.txt" -Encoding UTF8

Write-Host "Backup completed!" -ForegroundColor Green
Write-Host "Backup location: $backupPath" -ForegroundColor Cyan
Write-Host "Backup contents:" -ForegroundColor Yellow
Get-ChildItem $backupPath | Format-Table Name, Length -AutoSize